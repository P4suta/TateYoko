using PdfSharp.Drawing;
using PdfSharp.Pdf;
using TateYoko.Core.Domain;
using TateYoko.Core.Ports;

namespace TateYoko.Pdf;

/// <summary>
/// PDFsharp output writer. Builds each spread page and places every source page as a form,
/// translated only (no scaling).
/// </summary>
internal sealed class PdfSharpSpreadWriter : ISpreadWriter
{
    private readonly PdfDocument _document = new();
    private readonly QpdfLinearizer _linearizer = new();
    private bool _disposed;

    public void AddSpread(SpreadSpec spec, IReadOnlyList<PagePlacement> placements)
    {
        ArgumentNullException.ThrowIfNull(placements);

        PdfPage page = _document.AddPage();
        page.Width = XUnit.FromPoint(spec.WidthPt);
        page.Height = XUnit.FromPoint(spec.HeightPt);

        using XGraphics gfx = XGraphics.FromPdfPage(page);
        foreach (PagePlacement placement in placements)
        {
            if (placement.Content is not PdfSharpPageContent content)
            {
                throw new SpreadException(
                    ErrorKind.Internal,
                    $"unsupported page content: {placement.Content.GetType().Name}");
            }

            XPdfForm form = content.Form;
            gfx.DrawImage(
                form,
                placement.Position.OffsetXPt,
                placement.Position.OffsetYPt,
                form.PointWidth,
                form.PointHeight);
        }
    }

    public void ApplyMetadata(DocumentMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        PdfDocumentInformation info = _document.Info;
        if (metadata.Title is not null)
        {
            info.Title = metadata.Title;
        }

        if (metadata.Author is not null)
        {
            info.Author = metadata.Author;
        }

        if (metadata.Subject is not null)
        {
            info.Subject = metadata.Subject;
        }

        if (metadata.Keywords is not null)
        {
            info.Keywords = metadata.Keywords;
        }

        if (metadata.Creator is not null)
        {
            info.Creator = metadata.Creator;
        }

        if (metadata.CreationDate is { } created)
        {
            info.CreationDate = created;
        }
    }

    public void Save(string destinationPath)
    {
        // PDFsharp cannot write a linearized ("Fast Web View") file, so save to a temp next to the
        // destination (same volume, so the fallback move is atomic) and let qpdf linearize into the
        // final path. If qpdf is unavailable (non-x64/x86) or fails, ship the un-linearized file.
        string tempPath = destinationPath + ".prelinear.tmp";
        try
        {
            _document.Save(tempPath);

            if (!_linearizer.TryLinearize(tempPath, destinationPath))
            {
                File.Move(tempPath, destinationPath, overwrite: true);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new SpreadException(ErrorKind.PdfWriteFailed, destinationPath, e);
        }
        finally
        {
            TryDeleteTemp(tempPath);
        }
    }

    private static void TryDeleteTemp(string tempPath)
    {
        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Leftover temp file is harmless; never mask the real result over cleanup.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _document.Dispose();
    }
}
