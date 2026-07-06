using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using TateYoko.Core.Domain;
using TateYoko.Core.Ports;

namespace TateYoko.Pdf;

/// <summary>
/// PDFsharp source document. Provides the page count, rotation-adjusted display sizes, and metadata,
/// and lazily builds and caches an <see cref="XPdfForm"/> per page for drawing.
/// </summary>
internal sealed class PdfSharpSourceDocument : ISourceDocument
{
    private readonly string _path;
    private readonly PdfDocument _document;
    private readonly Dictionary<int, PdfSharpPageContent> _contentCache = new();
    private bool _disposed;

    internal PdfSharpSourceDocument(string path)
    {
        _path = path;
        _document = OpenOrThrow(path);
    }

    public int PageCount => _document.PageCount;

    public DocumentMetadata Metadata => ReadMetadata(_document.Info);

    public PageDimension GetPageDimension(int pageIndex)
    {
        PdfPage page = _document.Pages[pageIndex];

        // Prefer CropBox; fall back to MediaBox when it is unset (empty).
        PdfRectangle box = page.CropBox;
        if (box.Width <= 0 || box.Height <= 0)
        {
            box = page.MediaBox;
        }

        double width = box.Width;
        double height = box.Height;

        // 90/270-degree rotation swaps displayed width and height.
        int rotation = ((page.Rotate % 360) + 360) % 360;
        if (rotation is 90 or 270)
        {
            (width, height) = (height, width);
        }

        return new PageDimension((float)width, (float)height);
    }

    public IPageContent GetPageContent(int pageIndex)
    {
        if (_contentCache.TryGetValue(pageIndex, out PdfSharpPageContent? cached))
        {
            return cached;
        }

        // XPdfForm is per page; PageNumber is 1-based.
        var form = XPdfForm.FromFile(_path);
        form.PageNumber = pageIndex + 1;
        var content = new PdfSharpPageContent(form);
        _contentCache[pageIndex] = content;
        return content;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (PdfSharpPageContent content in _contentCache.Values)
        {
            content.Form.Dispose();
        }

        _contentCache.Clear();
        _document.Dispose();
    }

    private static PdfDocument OpenOrThrow(string path)
    {
        if (!File.Exists(path))
        {
            throw new SpreadException(ErrorKind.PdfNotFound, path);
        }

        try
        {
            return PdfReader.Open(path, PdfDocumentOpenMode.Import);
        }
        catch (PdfReaderException e) when (IsPasswordError(e))
        {
            throw new SpreadException(ErrorKind.PdfPasswordProtected, path, e);
        }
        catch (Exception e) when (e is PdfReaderException or InvalidOperationException or IOException)
        {
            throw new SpreadException(ErrorKind.PdfCorrupted, path, e);
        }
    }

    private static bool IsPasswordError(Exception e) =>
        e.Message.Contains("password", StringComparison.OrdinalIgnoreCase)
        || e.Message.Contains("encrypt", StringComparison.OrdinalIgnoreCase);

    private static DocumentMetadata ReadMetadata(PdfDocumentInformation info) =>
        new(
            Title: NullIfEmpty(info.Title),
            Author: NullIfEmpty(info.Author),
            Subject: NullIfEmpty(info.Subject),
            Keywords: NullIfEmpty(info.Keywords),
            Creator: NullIfEmpty(info.Creator),
            CreationDate: info.CreationDate == default ? null : info.CreationDate);

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrEmpty(value) ? null : value;
}
