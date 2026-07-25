using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using TateYoko.Engine.Internal;

namespace TateYoko.Engine;

/// <summary>PDFsharp-backed implementation of the PDF spread conversion use case.</summary>
public sealed class PdfSpreadConverter : IPdfSpreadConverter
{
    private static readonly SemaphoreSlim PdfSharpGate = new(1, 1);

    /// <inheritdoc />
    public PdfSpreadResult Convert(
        PdfSpreadRequest request,
        IProgress<PdfSpreadProgress>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidatedRequest validated = Validate(request);
        cancellationToken.ThrowIfCancellationRequested();

        PdfSharpGate.Wait(cancellationToken);
        try
        {
            return ConvertSerialized(request, validated, progress, cancellationToken);
        }
        finally
        {
            PdfSharpGate.Release();
        }
    }

    private static PdfSpreadResult ConvertSerialized(
        PdfSpreadRequest request,
        ValidatedRequest validated,
        IProgress<PdfSpreadProgress>? progress,
        CancellationToken cancellationToken
    )
    {
        using PdfSource source = PdfSource.Open(validated.InputPath, request.Password);
        int totalSpreads = Pagination.Count(request.FirstPageMode, source.PageCount);
        string temporaryPath = AtomicOutput.CreateTemporaryPath(validated.OutputPath);

        try
        {
            using var destination = new PdfDocument();
            source.CopyMetadataTo(destination);
            if (
                source.WasPasswordProtected
                && request.PreservePasswordProtection
                && !string.IsNullOrEmpty(request.Password)
            )
            {
                destination.SecurityHandler.SetEncryptionToV5();
                destination.SecuritySettings.UserPassword = request.Password;
                destination.SecuritySettings.OwnerPassword = request.Password;
            }

            int completed = 0;
            var pageProjections = new PageProjection?[source.PageCount];
            foreach (
                PageGroup group in Pagination.Enumerate(request.FirstPageMode, source.PageCount)
            )
            {
                cancellationToken.ThrowIfCancellationRequested();
                AddSpreadSafely(destination, source, group, pageProjections);
                progress?.Report(new PdfSpreadProgress(++completed, totalSpreads));
            }

            source.CopyNamedDestinationsTo(destination, pageProjections);
            source.CopyOutlinesTo(destination, pageProjections);
            source.CopyAnnotationsTo(destination, pageProjections, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            Save(destination, temporaryPath);
            cancellationToken.ThrowIfCancellationRequested();
            ValidateOutput(
                temporaryPath,
                totalSpreads,
                destination.SecuritySettings.IsEncrypted ? request.Password : null
            );
            cancellationToken.ThrowIfCancellationRequested();

            string committedPath = AtomicOutput.Commit(
                temporaryPath,
                validated.OutputPath,
                request.CollisionPolicy
            );
            return new PdfSpreadResult(committedPath, source.PageCount, totalSpreads);
        }
        finally
        {
            AtomicOutput.DeleteTemporary(temporaryPath);
        }
    }

    private static void AddSpreadSafely(
        PdfDocument destination,
        PdfSource source,
        PageGroup group,
        PageProjection?[] pageProjections
    )
    {
        try
        {
            AddSpread(destination, source, group, pageProjections);
        }
        catch (PdfSpreadException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new PdfSpreadException(
                PdfSpreadError.InvalidPage,
                "page-render-failed",
                exception
            );
        }
    }

    private static void AddSpread(
        PdfDocument destination,
        PdfSource source,
        PageGroup group,
        PageProjection?[] pageProjections
    )
    {
        PageSize firstSize = source.GetPageSize(group.FirstIndex);
        PageLayout layout;
        PageSize? secondSize = null;
        if (group.SecondIndex is int secondIndex)
        {
            secondSize = source.GetPageSize(secondIndex);
            layout = SpreadLayout.Pair(firstSize, secondSize.Value);
        }
        else
        {
            layout = SpreadLayout.Single(firstSize, group.SingleHalf);
        }

        PdfPage outputPage = destination.AddPage();
        outputPage.Width = XUnit.FromPoint(layout.SpreadSize.Width);
        outputPage.Height = XUnit.FromPoint(layout.SpreadSize.Height);

        using XGraphics graphics = XGraphics.FromPdfPage(outputPage);
        DrawPage(graphics, source, group.FirstIndex, firstSize, layout.First);
        pageProjections[group.FirstIndex] = source.CreateProjection(
            group.FirstIndex,
            outputPage,
            layout.First,
            firstSize
        );
        if (group.SecondIndex is int trailingIndex && layout.Second is Point trailingPoint)
        {
            DrawPage(graphics, source, trailingIndex, secondSize!.Value, trailingPoint);
            pageProjections[trailingIndex] = source.CreateProjection(
                trailingIndex,
                outputPage,
                trailingPoint,
                secondSize.Value
            );
        }
    }

    private static void DrawPage(
        XGraphics graphics,
        PdfSource source,
        int pageIndex,
        PageSize size,
        Point position
    )
    {
        XPdfForm form = source.SelectPage(pageIndex);
        graphics.DrawImage(form, position.X, position.Y, size.Width, size.Height);
    }

    private static ValidatedRequest Validate(PdfSpreadRequest request)
    {
        Guard.Defined(request.FirstPageMode, nameof(request.FirstPageMode));
        Guard.Defined(request.CollisionPolicy, nameof(request.CollisionPolicy));

        string inputPath = ValidatePdfPath(request.InputPath, nameof(request.InputPath));
        string outputPath = ValidatePdfPath(request.OutputPath, nameof(request.OutputPath));
        if (string.Equals(inputPath, outputPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new PdfSpreadException(PdfSpreadError.InvalidRequest, "input-equals-output");
        }

        if (!File.Exists(inputPath))
        {
            throw new PdfSpreadException(PdfSpreadError.InputNotFound, "input-not-found");
        }

        string? outputDirectory = Path.GetDirectoryName(outputPath);
        if (outputDirectory is null || !Directory.Exists(outputDirectory))
        {
            throw new PdfSpreadException(
                PdfSpreadError.InvalidRequest,
                "output-directory-not-found"
            );
        }

        return new ValidatedRequest(inputPath, outputPath);
    }

    private static string ValidatePdfPath(string path, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            throw new PdfSpreadException(PdfSpreadError.InvalidRequest, $"invalid-{parameterName}");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception exception)
            when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new PdfSpreadException(
                PdfSpreadError.InvalidRequest,
                $"invalid-{parameterName}",
                exception
            );
        }

        if (!string.Equals(Path.GetExtension(fullPath), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            PdfSpreadError error =
                parameterName == nameof(PdfSpreadRequest.InputPath)
                    ? PdfSpreadError.UnsupportedFile
                    : PdfSpreadError.InvalidRequest;
            throw new PdfSpreadException(error, $"non-pdf-{parameterName}");
        }

        return fullPath;
    }

    private static void Save(PdfDocument document, string temporaryPath)
    {
        try
        {
            document.Save(temporaryPath);
        }
        catch (Exception exception)
        {
            throw new PdfSpreadException(
                PdfSpreadError.WriteFailed,
                "output-write-failed",
                exception
            );
        }
    }

    private static void ValidateOutput(string path, int expectedPageCount, string? password)
    {
        try
        {
            using PdfDocument document = password is null
                ? PdfReader.Open(path, PdfDocumentOpenMode.Import)
                : PdfReader.Open(path, password, PdfDocumentOpenMode.Import);
            if (document.PageCount != expectedPageCount)
            {
                throw new PdfSpreadException(
                    PdfSpreadError.WriteFailed,
                    "output-page-count-mismatch"
                );
            }
        }
        catch (PdfSpreadException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new PdfSpreadException(
                PdfSpreadError.WriteFailed,
                "output-validation-failed",
                exception
            );
        }
    }

    private readonly record struct ValidatedRequest(string InputPath, string OutputPath);
}
