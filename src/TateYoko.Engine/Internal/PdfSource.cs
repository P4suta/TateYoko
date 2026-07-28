using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;

namespace TateYoko.Engine.Internal;

internal sealed class PdfSource : IDisposable
{
    private const double MaximumPageDimensionPoints = 14_400;
    private readonly PdfDocument _document;
    private readonly XPdfForm _form;
    private readonly FileStream _sourceStream;
    private readonly SensitiveMemoryStream? _decryptedStream;

    private PdfSource(
        PdfDocument document,
        string sourcePath,
        FileStream sourceStream,
        SensitiveMemoryStream? decryptedStream,
        bool wasPasswordProtected
    )
    {
        _document = document;
        _sourceStream = sourceStream;
        _decryptedStream = decryptedStream;
        _form = decryptedStream is null
            ? XPdfForm.FromFile(sourcePath)
            : XPdfForm.FromStream(decryptedStream);
        WasPasswordProtected = wasPasswordProtected;
    }

    internal int PageCount => _document.PageCount;

    internal bool WasPasswordProtected { get; }

    internal static PdfSource Open(string path, string? password)
    {
        FileStream sourceStream;
        try
        {
            sourceStream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                FileOptions.RandomAccess
            );
        }
        catch (Exception exception)
            when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new PdfSpreadException(
                PdfSpreadError.InputNotFound,
                "input-not-found",
                exception
            );
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new PdfSpreadException(PdfSpreadError.ReadFailed, "input-read-failed", exception);
        }

        PdfDocument? document = null;
        SensitiveMemoryStream? decryptedStream = null;
        try
        {
            (document, bool wasPasswordProtected) = OpenDocument(sourceStream, password);
            if (document.PageCount <= 0)
            {
                throw new PdfSpreadException(PdfSpreadError.InvalidPage, "empty-document");
            }

            ValidateAppearanceOnlyProfile(document);
            if (wasPasswordProtected)
            {
                decryptedStream = SaveDecryptedCopy(document);
            }

            return new PdfSource(
                document,
                path,
                sourceStream,
                decryptedStream,
                wasPasswordProtected
            );
        }
        catch
        {
            document?.Dispose();
            decryptedStream?.Dispose();
            sourceStream.Dispose();
            throw;
        }
    }

    internal PageSize GetPageSize(int pageIndex)
    {
        PdfPage page = GetPage(pageIndex);
        PdfRectangle box = GetVisibleBox(page);
        double width = box.Width;
        double height = box.Height;
        if (GetPageRotation(page) is 90 or 270)
        {
            (width, height) = (height, width);
        }

        return new PageSize(width, height);
    }

    internal XPdfForm SelectPage(int pageIndex)
    {
        _ = GetPage(pageIndex);
        _form.PageNumber = pageIndex + 1;
        return _form;
    }

    public void Dispose()
    {
        _form.Dispose();
        _document.Dispose();
        _decryptedStream?.Dispose();
        _sourceStream.Dispose();
    }

    private static (PdfDocument Document, bool WasPasswordProtected) OpenDocument(
        Stream stream,
        string? password
    )
    {
        bool passwordRequested = false;
        bool passwordProvided = false;
        void ProvidePassword(PdfPasswordProviderArgs args)
        {
            passwordRequested = true;
            if (password is null)
            {
                args.Abort = true;
            }
            else if (passwordProvided)
            {
                args.Abort = true;
            }
            else
            {
                args.Password = password;
                passwordProvided = true;
            }
        }

        try
        {
            stream.Position = 0;
            PdfDocument? document =
                PdfReader.Open(stream, PdfDocumentOpenMode.Import, ProvidePassword)
                ?? throw new PdfSpreadException(
                    password is null
                        ? PdfSpreadError.PasswordRequired
                        : PdfSpreadError.InvalidPassword,
                    password is null ? "password-required" : "password-rejected"
                );
            return (document, passwordRequested);
        }
        catch (PdfSpreadException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new PdfSpreadException(PdfSpreadError.CorruptedPdf, "pdf-open-failed", exception);
        }
    }

    private static SensitiveMemoryStream SaveDecryptedCopy(PdfDocument source)
    {
        var stream = new SensitiveMemoryStream();
        using var decrypted = new PdfDocument();
        try
        {
            foreach (PdfPage page in source.Pages)
            {
                decrypted.AddPage(page);
            }

            decrypted.Save(stream, closeStream: false);
            stream.Position = 0;
            return stream;
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    private static void ValidateAppearanceOnlyProfile(PdfDocument document)
    {
        PdfCatalog catalog = document.Internals.Catalog;
        RejectCatalogFeature(catalog, "/AcroForm", "interactive-form");
        RejectCatalogFeature(catalog, "/OpenAction", "document-open-action");
        RejectCatalogFeature(catalog, "/AA", "document-additional-action");
        RejectCatalogFeature(catalog, "/OCProperties", "optional-content");
        RejectCatalogFeature(catalog, "/Collection", "pdf-collection");
        RejectCatalogFeature(catalog, "/Perms", "document-permissions");
        RejectCatalogFeature(catalog, "/AF", "associated-file");

        PdfDictionary? names = catalog.Elements.GetDictionary("/Names");
        if (names is not null)
        {
            RejectDictionaryFeature(names, "/JavaScript", "javascript");
            RejectDictionaryFeature(names, "/EmbeddedFiles", "embedded-file");
            RejectDictionaryFeature(names, "/Renditions", "multimedia-rendition");
        }

        foreach (PdfPage page in document.Pages)
        {
            RejectPageFeature(page, "/AA", "page-additional-action");
            RejectPageFeature(page, "/AF", "page-associated-file");
            RejectPageFeature(page, "/PresSteps", "presentation-step");
            RejectPageFeature(page, "/Trans", "page-transition");
            RejectPageFeature(page, "/VP", "viewport-content");

            if (page.Elements.ContainsKey("/Annots"))
            {
                PdfArray? annotations = page.Elements.GetArray("/Annots");
                if (annotations is null)
                {
                    throw new PdfSpreadException(
                        PdfSpreadError.CorruptedPdf,
                        "annotation-array-invalid"
                    );
                }

                if (annotations.Elements.Count > 0)
                {
                    throw Unsupported("annotation");
                }
            }

            if (
                page.Elements.ContainsKey("/UserUnit")
                && Math.Abs(page.Elements.GetReal("/UserUnit") - 1) > 0.000_001
            )
            {
                throw Unsupported("page-unit");
            }

            PdfRectangle visibleBox = GetVisibleBox(page);
            _ = GetPageRotation(page);
            if (
                !double.IsFinite(visibleBox.Width)
                || !double.IsFinite(visibleBox.Height)
                || visibleBox.Width <= 0
                || visibleBox.Height <= 0
                || visibleBox.Width > MaximumPageDimensionPoints
                || visibleBox.Height > MaximumPageDimensionPoints
            )
            {
                throw new PdfSpreadException(PdfSpreadError.InvalidPage, "page-size-invalid");
            }
        }
    }

    private static void RejectCatalogFeature(PdfCatalog catalog, string key, string detail)
    {
        if (catalog.Elements.ContainsKey(key))
        {
            throw Unsupported(detail);
        }
    }

    private static void RejectDictionaryFeature(PdfDictionary value, string key, string detail)
    {
        if (value.Elements.ContainsKey(key))
        {
            throw Unsupported(detail);
        }
    }

    private static void RejectPageFeature(PdfPage page, string key, string detail)
    {
        if (page.Elements.ContainsKey(key))
        {
            throw Unsupported(detail);
        }
    }

    private static PdfSpreadException Unsupported(string detail) =>
        new(PdfSpreadError.UnsupportedPdfFeature, $"unsupported-{detail}");

    private PdfPage GetPage(int pageIndex)
    {
        if ((uint)pageIndex >= (uint)_document.PageCount)
        {
            throw new PdfSpreadException(PdfSpreadError.InvalidPage, "page-index-out-of-range");
        }

        return _document.Pages[pageIndex];
    }

    private static PdfRectangle GetVisibleBox(PdfPage page)
    {
        PdfRectangle box = page.CropBox;
        return box.Width > 0 && box.Height > 0 ? box : page.MediaBox;
    }

    private static int GetPageRotation(PdfPage page)
    {
        int rotation = ((page.Rotate % 360) + 360) % 360;
        if (rotation is not (0 or 90 or 180 or 270))
        {
            throw new PdfSpreadException(PdfSpreadError.InvalidPage, "page-rotation-invalid");
        }

        return rotation;
    }
}
