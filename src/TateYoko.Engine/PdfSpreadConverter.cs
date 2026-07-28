using PdfSharp.Drawing;
using PdfSharp.Pdf;
using TateYoko.Engine.Internal;

namespace TateYoko.Engine;

internal sealed class PdfSpreadConverter : IPdfSpreadConverter
{
    private static readonly SemaphoreSlim PdfSharpGate = new(1, 1);

    public async Task<PdfSpreadResult> ConvertAsync(
        PdfSpreadRequest request,
        IProgress<PdfSpreadProgress>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        ValidatedRequest validated = Validate(request);

        await PdfSharpGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(
                    () => ConvertSerialized(request, validated, progress, cancellationToken),
                    cancellationToken
                )
                .ConfigureAwait(false);
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
            if (source.WasPasswordProtected && !string.IsNullOrEmpty(request.Password))
            {
                destination.SecurityHandler.SetEncryptionToV5();
                destination.SecuritySettings.UserPassword = request.Password;
                destination.SecuritySettings.OwnerPassword = request.Password;
            }

            var expectedPageSizes = new List<PageSize>(totalSpreads);
            int completed = 0;
            foreach (
                PageGroup group in Pagination.Enumerate(request.FirstPageMode, source.PageCount)
            )
            {
                cancellationToken.ThrowIfCancellationRequested();
                expectedPageSizes.Add(AddSpread(destination, source, group));
                progress?.Report(new PdfSpreadProgress(++completed, totalSpreads));
            }

            cancellationToken.ThrowIfCancellationRequested();
            Save(destination, temporaryPath);
            cancellationToken.ThrowIfCancellationRequested();
            OutputVerifier.Verify(
                temporaryPath,
                expectedPageSizes,
                destination.SecuritySettings.IsEncrypted ? request.Password : null
            );
            cancellationToken.ThrowIfCancellationRequested();

            string committedPath = AtomicOutput.Commit(temporaryPath, validated.OutputPath);
            return new PdfSpreadResult(committedPath, source.PageCount, totalSpreads);
        }
        catch (Exception failure)
        {
            try
            {
                AtomicOutput.DeleteTemporary(temporaryPath);
            }
            catch (Exception cleanupFailure)
            {
                throw new PdfSpreadException(
                    PdfSpreadError.WriteFailed,
                    "conversion-and-cleanup-failed",
                    new AggregateException(failure, cleanupFailure)
                );
            }

            throw;
        }
    }

    private static PageSize AddSpread(PdfDocument destination, PdfSource source, PageGroup group)
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
        if (group.SecondIndex is int trailingIndex && layout.Second is Point trailingPoint)
        {
            DrawPage(graphics, source, trailingIndex, secondSize!.Value, trailingPoint);
        }

        return layout.SpreadSize;
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

        string inputPath = ValidatePdfPath(request.InputPath, nameof(request.InputPath));
        string outputPath = ValidatePdfPath(request.OutputPath, nameof(request.OutputPath));
        if (string.Equals(inputPath, outputPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new PdfSpreadException(PdfSpreadError.InvalidRequest, "input-equals-output");
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
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new PdfSpreadException(PdfSpreadError.InvalidRequest, $"invalid-{parameterName}");
        }

        string fullPath;
        try
        {
            if (!Path.IsPathFullyQualified(path))
            {
                throw new ArgumentException("The path must be absolute.", parameterName);
            }

            fullPath = Path.GetFullPath(path);
            if (
                !string.Equals(
                    Path.GetExtension(fullPath),
                    ".pdf",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                PdfSpreadError error =
                    parameterName == nameof(PdfSpreadRequest.InputPath)
                        ? PdfSpreadError.UnsupportedFile
                        : PdfSpreadError.InvalidRequest;
                throw new PdfSpreadException(error, $"non-pdf-{parameterName}");
            }
        }
        catch (PdfSpreadException)
        {
            throw;
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

        return fullPath;
    }

    private static void Save(PdfDocument document, string temporaryPath)
    {
        try
        {
            using var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 64 * 1024,
                FileOptions.WriteThrough
            );
            document.Save(stream, closeStream: false);
            stream.Flush(flushToDisk: true);
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

    private readonly record struct ValidatedRequest(string InputPath, string OutputPath);
}
