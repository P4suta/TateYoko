using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.Annotations;
using PdfSharp.Pdf.IO;

namespace TateYoko.Engine.Internal;

internal sealed class PdfSource : IDisposable
{
    private readonly PdfDocument _document;
    private readonly XPdfForm _form;
    private readonly FileStream _sourceStream;
    private readonly SensitiveMemoryStream? _decryptedStream;
    private bool _disposed;

    private PdfSource(
        PdfDocument document,
        string sourcePath,
        FileStream sourceStream,
        SensitiveMemoryStream? decryptedStream
    )
    {
        _document = document;
        _sourceStream = sourceStream;
        _decryptedStream = decryptedStream;
        _form = decryptedStream is null
            ? XPdfForm.FromFile(sourcePath)
            : XPdfForm.FromStream(decryptedStream);
    }

    internal int PageCount => _document.PageCount;

    internal bool WasPasswordProtected { get; private init; }

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
            (PdfDocument openedDocument, bool wasPasswordProtected) = OpenDocument(
                sourceStream,
                password
            );
            document = openedDocument;
            if (document.PageCount <= 0)
            {
                throw new PdfSpreadException(PdfSpreadError.InvalidPage, "empty-document");
            }

            ValidateSupportedDocumentFeatures(document);
            ValidateXmpMetadata(document);
            ValidateSupportedAnnotations(document);
            if (wasPasswordProtected)
            {
                decryptedStream = SaveDecryptedCopy(document);
            }

            var source = new PdfSource(document, path, sourceStream, decryptedStream)
            {
                WasPasswordProtected = wasPasswordProtected,
            };
            return source;
        }
        catch (PdfSpreadException)
        {
            document?.Dispose();
            decryptedStream?.Dispose();
            sourceStream.Dispose();
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            document?.Dispose();
            decryptedStream?.Dispose();
            sourceStream.Dispose();
            throw new PdfSpreadException(PdfSpreadError.ReadFailed, "input-read-failed", exception);
        }
        catch (Exception exception)
        {
            document?.Dispose();
            decryptedStream?.Dispose();
            sourceStream.Dispose();
            throw new PdfSpreadException(
                PdfSpreadError.CorruptedPdf,
                "pdf-initialization-failed",
                exception
            );
        }
    }

    internal PageSize GetPageSize(int pageIndex)
    {
        if ((uint)pageIndex >= (uint)_document.PageCount)
        {
            throw new PdfSpreadException(PdfSpreadError.InvalidPage, "page-index-out-of-range");
        }

        PdfPage page = _document.Pages[pageIndex];
        PdfRectangle box = page.CropBox;
        if (box.Width <= 0 || box.Height <= 0)
        {
            box = page.MediaBox;
        }

        double width = box.Width;
        double height = box.Height;
        int rotation = GetPageRotation(page);
        if (rotation is 90 or 270)
        {
            (width, height) = (height, width);
        }

        return new PageSize(width, height);
    }

    internal XPdfForm SelectPage(int pageIndex)
    {
        if ((uint)pageIndex >= (uint)_document.PageCount)
        {
            throw new PdfSpreadException(PdfSpreadError.InvalidPage, "page-index-out-of-range");
        }

        _form.PageNumber = pageIndex + 1;
        return _form;
    }

    internal PageProjection CreateProjection(
        int pageIndex,
        PdfPage destinationPage,
        Point drawingOffset,
        PageSize displayedSize
    )
    {
        ArgumentNullException.ThrowIfNull(destinationPage);
        if ((uint)pageIndex >= (uint)_document.PageCount)
        {
            throw new PdfSpreadException(PdfSpreadError.InvalidPage, "page-index-out-of-range");
        }

        PdfPage sourcePage = _document.Pages[pageIndex];
        int rotation = GetPageRotation(sourcePage);
        return new PageProjection(
            destinationPage,
            drawingOffset,
            displayedSize,
            GetVisibleBox(sourcePage),
            rotation
        );
    }

    internal void CopyMetadataTo(PdfDocument destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        PdfDocumentInformation source = _document.Info;
        PdfDocumentInformation target = destination.Info;
        target.Title = source.Title;
        target.Author = source.Author;
        target.Subject = source.Subject;
        target.Keywords = source.Keywords;
        target.Creator = source.Creator;
        if (!string.IsNullOrEmpty(source.Producer))
        {
            target.Elements.SetString("/Producer", source.Producer);
        }

        if (source.CreationDate != default)
        {
            target.CreationDate = source.CreationDate;
        }

        target.ModificationDate = DateTime.UtcNow;
        string trapped = source.Elements.GetName("/Trapped");
        if (!string.IsNullOrEmpty(trapped))
        {
            target.Elements.SetName("/Trapped", trapped);
        }

        destination.Version = Math.Max(17, _document.Version);
        destination.Language = _document.Language;
        destination.PageLayout = PdfPageLayout.SinglePage;
        destination.PageMode = _document.PageMode;
        CopyViewerPreferences(destination);
    }

    internal void CopyNamedDestinationsTo(
        PdfDocument destination,
        IReadOnlyList<PageProjection?> pageProjections
    )
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(pageProjections);
        if (
            pageProjections.Count != _document.PageCount
            || pageProjections.Any(projection => projection is null)
        )
        {
            throw new PdfSpreadException(
                PdfSpreadError.Internal,
                "destination-page-map-incomplete"
            );
        }

        Dictionary<PdfObjectID, int> sourcePageIndices = CreateSourcePageIndex();
        var destinations = new SortedDictionary<string, PdfArray>(StringComparer.Ordinal);
        PdfCatalog catalog = _document.Internals.Catalog;
        if (catalog.Elements.GetValue("/Dests") is not null)
        {
            foreach (string sourceName in catalog.Destinations.Names)
            {
                string name = PdfName.RemoveSlash(sourceName);
                if (string.IsNullOrEmpty(name))
                {
                    throw new PdfSpreadException(
                        PdfSpreadError.CorruptedPdf,
                        "named-destination-name-invalid"
                    );
                }

                destinations.Add(
                    name,
                    catalog.Destinations.GetDestination(sourceName)
                        ?? throw new PdfSpreadException(
                            PdfSpreadError.CorruptedPdf,
                            "named-destination-missing"
                        )
                );
            }
        }

        PdfDictionary? names = catalog.Elements.GetDictionary("/Names");
        if (names?.Elements.GetValue("/Dests") is not null)
        {
            PdfNameTreeNode tree =
                catalog.Names.NameTree
                ?? throw new PdfSpreadException(
                    PdfSpreadError.CorruptedPdf,
                    "named-destination-tree-missing"
                );
            foreach (string? candidate in tree.GetNames(true))
            {
                string name =
                    candidate
                    ?? throw new PdfSpreadException(
                        PdfSpreadError.CorruptedPdf,
                        "named-destination-name-invalid"
                    );
                PdfItem item =
                    tree.GetValue(name, true)
                    ?? throw new PdfSpreadException(
                        PdfSpreadError.CorruptedPdf,
                        "named-destination-missing"
                    );
                PdfArray sourceDestination = item switch
                {
                    PdfArray array => array,
                    PdfDictionary dictionary => dictionary.Elements.GetArray("/D")
                        ?? throw new PdfSpreadException(
                            PdfSpreadError.CorruptedPdf,
                            "named-destination-invalid"
                        ),
                    _ => throw new PdfSpreadException(
                        PdfSpreadError.CorruptedPdf,
                        "named-destination-invalid"
                    ),
                };
                if (!destinations.TryAdd(name, sourceDestination))
                {
                    throw new PdfSpreadException(
                        PdfSpreadError.CorruptedPdf,
                        "duplicate-named-destination"
                    );
                }
            }
        }

        foreach ((string name, PdfArray sourceDestination) in destinations)
        {
            PdfArray mapped = MapDestination(sourceDestination, sourcePageIndices, pageProjections);
            destination.AddNamedDestination(
                name,
                FindDestinationPageNumber(destination, mapped),
                CreateNamedDestinationParameters(mapped)
            );
        }
    }

    internal void CopyOutlinesTo(
        PdfDocument destination,
        IReadOnlyList<PageProjection?> pageProjections
    )
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(pageProjections);
        if (_document.Outlines.Count == 0)
        {
            return;
        }

        if (
            pageProjections.Count != _document.PageCount
            || pageProjections.Any(projection => projection is null)
        )
        {
            throw new PdfSpreadException(PdfSpreadError.Internal, "outline-page-map-incomplete");
        }

        var sourcePageIndices = new Dictionary<PdfPage, int>(ReferenceEqualityComparer.Instance);
        for (int index = 0; index < _document.PageCount; index++)
        {
            sourcePageIndices.Add(_document.Pages[index], index);
        }

        CopyOutlineCollection(
            _document.Outlines,
            destination.Outlines,
            sourcePageIndices,
            pageProjections
        );
        destination.PageMode = PdfPageMode.UseOutlines;
    }

    internal void CopyAnnotationsTo(
        PdfDocument destination,
        IReadOnlyList<PageProjection?> pageProjections,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(pageProjections);
        if (
            pageProjections.Count != _document.PageCount
            || pageProjections.Any(projection => projection is null)
        )
        {
            throw new PdfSpreadException(PdfSpreadError.Internal, "annotation-page-map-incomplete");
        }

        try
        {
            Dictionary<PdfObjectID, int> sourcePageIndices = CreateSourcePageIndex();
            for (int pageIndex = 0; pageIndex < _document.PageCount; pageIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PdfPage sourcePage = _document.Pages[pageIndex];
                PdfArray? sourceItems = sourcePage.Elements.GetArray("/Annots");
                if (sourceItems is null || sourceItems.Elements.Count == 0)
                {
                    continue;
                }

                PageProjection projection =
                    pageProjections[pageIndex]
                    ?? throw new PdfSpreadException(
                        PdfSpreadError.Internal,
                        "annotation-page-map-incomplete"
                    );
                using MemoryStream annotationPageStream = CreateAnnotationPageStream(sourcePage);
                using PdfDocument annotationSource = PdfReader.Open(
                    annotationPageStream,
                    PdfDocumentOpenMode.Import
                );
                PdfPage stagingPage = destination.AddPage(annotationSource.Pages[0]);
                try
                {
                    CopyPageAnnotations(
                        sourcePage,
                        stagingPage,
                        projection,
                        sourcePageIndices,
                        pageProjections
                    );
                }
                finally
                {
                    destination.Pages.Remove(stagingPage);
                }
            }
        }
        catch (PdfSpreadException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new PdfSpreadException(
                PdfSpreadError.CorruptedPdf,
                "annotation-copy-failed",
                exception
            );
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
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
        bool passwordRejected = false;
        void ProvidePassword(PdfPasswordProviderArgs args)
        {
            passwordRequested = true;
            if (password is null)
            {
                args.Abort = true;
            }
            else if (passwordProvided)
            {
                passwordRejected = true;
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
        catch (Exception exception) when (passwordRequested && password is null)
        {
            throw new PdfSpreadException(
                PdfSpreadError.PasswordRequired,
                "password-required",
                exception
            );
        }
        catch (Exception exception)
            when (passwordRequested
                && password is not null
                && (passwordRejected || LooksLikePasswordRejection(exception))
            )
        {
            throw new PdfSpreadException(
                PdfSpreadError.InvalidPassword,
                "password-rejected",
                exception
            );
        }
        catch (Exception exception)
        {
            throw new PdfSpreadException(PdfSpreadError.CorruptedPdf, "pdf-open-failed", exception);
        }
    }

    private static bool LooksLikePasswordRejection(Exception exception) =>
        exception.Message.Contains("password", StringComparison.OrdinalIgnoreCase)
        || exception.GetType().Name.Contains("Password", StringComparison.OrdinalIgnoreCase);

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

            decrypted.SecurityHandler.SetEncryptionToNoneAndResetPasswords();
            PdfDocumentInformation sourceInfo = source.Info;
            decrypted.Info.Title = sourceInfo.Title;
            decrypted.Info.Author = sourceInfo.Author;
            decrypted.Info.Subject = sourceInfo.Subject;
            decrypted.Info.Keywords = sourceInfo.Keywords;
            decrypted.Info.Creator = sourceInfo.Creator;
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

    private static void ValidateSupportedDocumentFeatures(PdfDocument document)
    {
        PdfCatalog catalog = document.Internals.Catalog;
        var supportedInformationKeys = new HashSet<string>(
            [
                "/Title",
                "/Author",
                "/Subject",
                "/Keywords",
                "/Creator",
                "/Producer",
                "/CreationDate",
                "/ModDate",
                "/Trapped",
            ],
            StringComparer.Ordinal
        );
        foreach ((string key, _) in document.Info)
        {
            if (!supportedInformationKeys.Contains(key))
            {
                throw new PdfSpreadException(
                    PdfSpreadError.UnsupportedPdfFeature,
                    "unsupported-document-information"
                );
            }
        }

        var supportedCatalogKeys = new HashSet<string>(
            [
                "/Type",
                "/Version",
                "/Pages",
                "/PageLayout",
                "/PageMode",
                "/Outlines",
                "/Names",
                "/Dests",
                "/ViewerPreferences",
                "/Metadata",
                "/Lang",
            ],
            StringComparer.Ordinal
        );
        foreach ((string key, _) in catalog)
        {
            if (!supportedCatalogKeys.Contains(key))
            {
                throw new PdfSpreadException(
                    PdfSpreadError.UnsupportedPdfFeature,
                    "unsupported-document-feature"
                );
            }
        }

        string pageMode = catalog.Elements.GetName("/PageMode");
        if (pageMode is "/UseAttachments" or "/UseOC")
        {
            throw new PdfSpreadException(
                PdfSpreadError.UnsupportedPdfFeature,
                "unsupported-page-mode"
            );
        }

        PdfDictionary? names = catalog.Elements.GetDictionary("/Names");
        if (names is not null)
        {
            foreach ((string key, _) in names)
            {
                if (!string.Equals(key, "/Dests", StringComparison.Ordinal))
                {
                    throw new PdfSpreadException(
                        PdfSpreadError.UnsupportedPdfFeature,
                        "unsupported-document-feature"
                    );
                }
            }
        }

        PdfDictionary? viewerPreferences = catalog.Elements.GetDictionary("/ViewerPreferences");
        if (viewerPreferences is not null)
        {
            var supportedPreferenceKeys = new HashSet<string>(
                [
                    "/HideToolbar",
                    "/HideMenubar",
                    "/HideWindowUI",
                    "/FitWindow",
                    "/CenterWindow",
                    "/DisplayDocTitle",
                    "/Direction",
                ],
                StringComparer.Ordinal
            );
            foreach ((string key, _) in viewerPreferences)
            {
                if (!supportedPreferenceKeys.Contains(key))
                {
                    throw new PdfSpreadException(
                        PdfSpreadError.UnsupportedPdfFeature,
                        "unsupported-viewer-preference"
                    );
                }
            }
        }

        string[] unsupportedPageKeys =
        [
            "/AA",
            "/AF",
            "/ArtBox",
            "/B",
            "/BleedBox",
            "/BoxColorInfo",
            "/Dur",
            "/LastModified",
            "/Metadata",
            "/PieceInfo",
            "/PresSteps",
            "/SeparationInfo",
            "/StructParents",
            "/Tabs",
            "/TemplateInstantiated",
            "/Thumb",
            "/Trans",
            "/TrimBox",
            "/VP",
        ];
        foreach (PdfPage page in document.Pages)
        {
            if (unsupportedPageKeys.Any(page.Elements.ContainsKey))
            {
                throw new PdfSpreadException(
                    PdfSpreadError.UnsupportedPdfFeature,
                    "unsupported-page-feature"
                );
            }

            if (
                page.Elements.ContainsKey("/UserUnit")
                && Math.Abs(page.Elements.GetReal("/UserUnit") - 1) > 0.000_001
            )
            {
                throw new PdfSpreadException(
                    PdfSpreadError.UnsupportedPdfFeature,
                    "unsupported-page-unit"
                );
            }
        }
    }

    private static void ValidateSupportedAnnotations(PdfDocument document)
    {
        foreach (PdfPage page in document.Pages)
        {
            PdfArray? annotations = page.Elements.GetArray("/Annots");
            if (annotations is null)
            {
                continue;
            }

            for (int index = 0; index < annotations.Elements.Count; index++)
            {
                PdfDictionary annotation =
                    annotations.Elements.GetDictionary(index)
                    ?? throw new PdfSpreadException(
                        PdfSpreadError.CorruptedPdf,
                        "annotation-dictionary-invalid"
                    );
                string subtype = annotation.Elements.GetName("/Subtype");
                if (string.IsNullOrEmpty(subtype))
                {
                    throw new PdfSpreadException(
                        PdfSpreadError.CorruptedPdf,
                        "annotation-subtype-missing"
                    );
                }

                if (!string.Equals(subtype, "/Link", StringComparison.Ordinal))
                {
                    throw new PdfSpreadException(
                        PdfSpreadError.UnsupportedPdfFeature,
                        "unsupported-annotation"
                    );
                }
            }
        }
    }

    private static void ValidateXmpMetadata(PdfDocument document)
    {
        PdfDictionary? metadata = document.Internals.Catalog.Elements.GetDictionary("/Metadata");
        if (metadata is null)
        {
            return;
        }

        if (metadata.Stream is null)
        {
            throw new PdfSpreadException(PdfSpreadError.CorruptedPdf, "metadata-stream-missing");
        }

        XDocument xmp;
        try
        {
            using var stream = new MemoryStream(metadata.Stream.UnfilteredValue, writable: false);
            using XmlReader reader = XmlReader.Create(
                stream,
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }
            );
            xmp = XDocument.Load(reader, LoadOptions.None);
        }
        catch (Exception exception) when (exception is XmlException or InvalidOperationException)
        {
            throw new PdfSpreadException(
                PdfSpreadError.CorruptedPdf,
                "metadata-xml-invalid",
                exception
            );
        }

        XNamespace meta = "adobe:ns:meta/";
        XNamespace xml = "http://www.w3.org/XML/1998/namespace";
        XNamespace rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        XNamespace dc = "http://purl.org/dc/elements/1.1/";
        XNamespace pdf = "http://ns.adobe.com/pdf/1.3/";
        XNamespace xap = "http://ns.adobe.com/xap/1.0/";
        XNamespace xapMm = "http://ns.adobe.com/xap/1.0/mm/";
        XNamespace pdfAid = "http://www.aiim.org/pdfa/ns/id/";
        var supportedElements = new HashSet<XName>([
            meta + "xmpmeta",
            rdf + "RDF",
            rdf + "Description",
            rdf + "Alt",
            rdf + "Seq",
            rdf + "Bag",
            rdf + "li",
            dc + "format",
            dc + "title",
            dc + "creator",
            dc + "description",
            dc + "subject",
            pdf + "Producer",
            pdf + "Keywords",
            pdf + "PDFVersion",
            pdf + "Trapped",
            xap + "CreatorTool",
            xap + "CreateDate",
            xap + "ModifyDate",
            xap + "MetadataDate",
            xapMm + "DocumentID",
            xapMm + "InstanceID",
            pdfAid + "part",
            pdfAid + "conformance",
        ]);
        var supportedAttributes = new HashSet<XName>([
            meta + "xmptk",
            rdf + "about",
            rdf + "parseType",
            xml + "lang",
        ]);
        bool hasUnsupportedItem =
            xmp.Descendants().Any(element => !supportedElements.Contains(element.Name))
            || xmp.Descendants()
                .Attributes()
                .Where(attribute => !attribute.IsNamespaceDeclaration)
                .Any(attribute => !supportedAttributes.Contains(attribute.Name));
        if (hasUnsupportedItem)
        {
            throw new PdfSpreadException(
                PdfSpreadError.UnsupportedPdfFeature,
                "unsupported-metadata-schema"
            );
        }
    }

    private void CopyViewerPreferences(PdfDocument destination)
    {
        PdfDictionary? sourceDictionary = _document.Internals.Catalog.Elements.GetDictionary(
            "/ViewerPreferences"
        );
        if (sourceDictionary is null)
        {
            return;
        }

        PdfViewerPreferences target = destination.ViewerPreferences;
        target.HideToolbar = sourceDictionary.Elements.GetBoolean("/HideToolbar");
        target.HideMenubar = sourceDictionary.Elements.GetBoolean("/HideMenubar");
        target.HideWindowUI = sourceDictionary.Elements.GetBoolean("/HideWindowUI");
        target.FitWindow = sourceDictionary.Elements.GetBoolean("/FitWindow");
        target.CenterWindow = sourceDictionary.Elements.GetBoolean("/CenterWindow");
        target.DisplayDocTitle = sourceDictionary.Elements.GetBoolean("/DisplayDocTitle");
        target.Direction = sourceDictionary.Elements.GetName("/Direction") switch
        {
            "" => null,
            "/L2R" => PdfReadingDirection.LeftToRight,
            "/R2L" => PdfReadingDirection.RightToLeft,
            _ => throw new PdfSpreadException(
                PdfSpreadError.CorruptedPdf,
                "viewer-direction-invalid"
            ),
        };
    }

    internal static int FindDestinationPageNumber(PdfDocument destination, PdfArray mapped)
    {
        if (mapped.Elements.Count == 0 || mapped.Elements[0] is not PdfReference pageReference)
        {
            throw new PdfSpreadException(
                PdfSpreadError.Internal,
                "mapped-destination-page-missing"
            );
        }

        for (int index = 0; index < destination.PageCount; index++)
        {
            if (destination.Pages[index].ReferenceNotNull.ObjectID == pageReference.ObjectID)
            {
                return index + 1;
            }
        }

        throw new PdfSpreadException(PdfSpreadError.Internal, "mapped-destination-page-invalid");
    }

    internal static PdfNamedDestinationParameters CreateNamedDestinationParameters(PdfArray mapped)
    {
        if (mapped.Elements.Count < 2 || mapped.Elements[1] is not PdfName destinationType)
        {
            throw new PdfSpreadException(PdfSpreadError.Internal, "mapped-destination-invalid");
        }

        return destinationType.Value switch
        {
            "/XYZ" => PdfNamedDestinationParameters.CreatePosition(
                mapped.Elements.GetNullableReal(2),
                mapped.Elements.GetNullableReal(3),
                mapped.Elements.GetNullableReal(4)
            ),
            "/Fit" => PdfNamedDestinationParameters.CreateFit(),
            "/FitH" => PdfNamedDestinationParameters.CreateFitHorizontally(
                mapped.Elements.GetNullableReal(2)
            ),
            "/FitV" => PdfNamedDestinationParameters.CreateFitVertically(
                mapped.Elements.GetNullableReal(2)
            ),
            "/FitR" => PdfNamedDestinationParameters.CreateFitRectangle(
                mapped.Elements.GetReal(2),
                mapped.Elements.GetReal(3),
                mapped.Elements.GetReal(4),
                mapped.Elements.GetReal(5)
            ),
            "/FitB" => PdfNamedDestinationParameters.CreateFitBoundingBox(),
            "/FitBH" => PdfNamedDestinationParameters.CreateFitBoundingBoxHorizontally(
                mapped.Elements.GetNullableReal(2)
            ),
            "/FitBV" => PdfNamedDestinationParameters.CreateFitBoundingBoxVertically(
                mapped.Elements.GetNullableReal(2)
            ),
            _ => throw new PdfSpreadException(
                PdfSpreadError.Internal,
                "mapped-destination-type-invalid"
            ),
        };
    }

    private Dictionary<PdfObjectID, int> CreateSourcePageIndex()
    {
        var indices = new Dictionary<PdfObjectID, int>();
        for (int index = 0; index < _document.PageCount; index++)
        {
            indices.Add(_document.Pages[index].ReferenceNotNull.ObjectID, index);
        }

        return indices;
    }

    private static MemoryStream CreateAnnotationPageStream(PdfPage sourcePage)
    {
        using var clone = new PdfDocument();
        PdfPage clonePage = clone.AddPage(sourcePage);
        NeutralizeInternalLinkDestinations(sourcePage, clonePage);

        // Only the annotation graph is imported into the output. Page content is already
        // represented by the XPdfForm and importing it again would duplicate the document.
        clonePage.Elements.Remove("/Contents");
        clonePage.Elements.Remove("/Resources");

        var stream = new MemoryStream();
        try
        {
            clone.Save(stream, closeStream: false);
            stream.Position = 0;
            return stream;
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    private static void NeutralizeInternalLinkDestinations(PdfPage sourcePage, PdfPage clonePage)
    {
        PdfAnnotations sourceAnnotations = sourcePage.Annotations;
        PdfAnnotations cloneAnnotations = clonePage.Annotations;
        if (sourceAnnotations.Count != cloneAnnotations.Count)
        {
            throw new PdfSpreadException(
                PdfSpreadError.Internal,
                "annotation-clone-count-mismatch"
            );
        }

        for (int index = 0; index < sourceAnnotations.Count; index++)
        {
            PdfAnnotation source = sourceAnnotations[index];
            PdfAnnotation target = cloneAnnotations[index];
            if (source.Elements.GetValue("/Dest") is not null)
            {
                target.Elements["/Dest"] = CreateDestinationPlaceholder(clonePage.Owner);
                continue;
            }

            PdfDictionary? sourceAction = source.Elements.GetDictionary("/A");
            if (sourceAction?.Elements.GetName("/S") != "/GoTo")
            {
                continue;
            }

            PdfDictionary targetAction =
                target.Elements.GetDictionary("/A")
                ?? throw new PdfSpreadException(
                    PdfSpreadError.CorruptedPdf,
                    "link-action-missing-after-clone"
                );
            targetAction.Elements["/D"] = CreateDestinationPlaceholder(clonePage.Owner);
        }
    }

    private static PdfArray CreateDestinationPlaceholder(PdfDocument owner)
    {
        var destination = new PdfArray(owner);
        destination.Elements.Add(new PdfInteger(0));
        destination.Elements.Add(new PdfName("/Fit"));
        return destination;
    }

    private void CopyPageAnnotations(
        PdfPage sourcePage,
        PdfPage stagingPage,
        PageProjection projection,
        Dictionary<PdfObjectID, int> sourcePageIndices,
        IReadOnlyList<PageProjection?> pageProjections
    )
    {
        PdfAnnotations sourceAnnotations = sourcePage.Annotations;
        PdfAnnotations stagingAnnotations = stagingPage.Annotations;
        if (sourceAnnotations.Count != stagingAnnotations.Count)
        {
            throw new PdfSpreadException(
                PdfSpreadError.Internal,
                "annotation-import-count-mismatch"
            );
        }

        PdfAnnotations destinationAnnotations = projection.DestinationPage.Annotations;
        for (int index = 0; index < sourceAnnotations.Count; index++)
        {
            PdfAnnotation source = sourceAnnotations[index];
            PdfAnnotation target = stagingAnnotations[index];
            target.Rectangle = projection.TransformRectangle(source.Rectangle);
            TransformQuadPoints(source, target, projection);
            RemapLinkDestination(source, target, sourcePageIndices, pageProjections);
            target.Elements["/P"] = projection.DestinationPage.ReferenceNotNull;

            // The imported annotation already belongs to the destination document. Reuse its
            // indirect reference instead of re-registering it through PdfAnnotations.Add.
            destinationAnnotations.Elements.Add(stagingAnnotations.Elements[index]);
        }
    }

    private static void TransformQuadPoints(
        PdfAnnotation source,
        PdfAnnotation destination,
        PageProjection projection
    )
    {
        PdfArray? sourcePoints = source.Elements.GetArray("/QuadPoints");
        if (sourcePoints is null)
        {
            return;
        }

        if (sourcePoints.Elements.Count == 0 || sourcePoints.Elements.Count % 8 != 0)
        {
            throw new PdfSpreadException(PdfSpreadError.CorruptedPdf, "link-quadpoints-invalid");
        }

        PdfArray destinationPoints =
            destination.Elements.GetArray("/QuadPoints")
            ?? throw new PdfSpreadException(
                PdfSpreadError.CorruptedPdf,
                "link-quadpoints-missing-after-clone"
            );
        if (destinationPoints.Elements.Count != sourcePoints.Elements.Count)
        {
            throw new PdfSpreadException(
                PdfSpreadError.CorruptedPdf,
                "link-quadpoints-count-mismatch"
            );
        }

        for (int index = 0; index < sourcePoints.Elements.Count; index += 2)
        {
            XPoint point = projection.TransformPoint(
                sourcePoints.Elements.GetReal(index),
                sourcePoints.Elements.GetReal(index + 1)
            );
            destinationPoints.Elements[index] = new PdfReal(point.X);
            destinationPoints.Elements[index + 1] = new PdfReal(point.Y);
        }
    }

    private void RemapLinkDestination(
        PdfAnnotation source,
        PdfAnnotation destination,
        Dictionary<PdfObjectID, int> sourcePageIndices,
        IReadOnlyList<PageProjection?> pageProjections
    )
    {
        PdfItem? directDestination = source.Elements.GetValue("/Dest");
        PdfDictionary? sourceAction = source.Elements.GetDictionary("/A");
        if (directDestination is not null && sourceAction is not null)
        {
            throw new PdfSpreadException(PdfSpreadError.CorruptedPdf, "link-has-dest-and-action");
        }

        if (directDestination is not null)
        {
            destination.Elements["/Dest"] = MapDestination(
                ResolveDestination(directDestination),
                sourcePageIndices,
                pageProjections
            );
            return;
        }

        if (sourceAction?.Elements.GetName("/S") != "/GoTo")
        {
            return;
        }

        PdfItem sourceActionDestination =
            sourceAction.Elements.GetValue("/D")
            ?? throw new PdfSpreadException(
                PdfSpreadError.CorruptedPdf,
                "link-destination-missing"
            );
        PdfDictionary destinationAction =
            destination.Elements.GetDictionary("/A")
            ?? throw new PdfSpreadException(
                PdfSpreadError.CorruptedPdf,
                "link-action-missing-after-import"
            );
        destinationAction.Elements["/D"] = MapDestination(
            ResolveDestination(sourceActionDestination),
            sourcePageIndices,
            pageProjections
        );
    }

    private PdfArray ResolveDestination(PdfItem item)
    {
        if (item is PdfArray destination)
        {
            return destination;
        }

        string? name = item switch
        {
            PdfString text => text.Value,
            PdfName pdfName => PdfName.RemoveSlash(pdfName.Value),
            _ => null,
        };
        if (string.IsNullOrEmpty(name))
        {
            throw new PdfSpreadException(PdfSpreadError.CorruptedPdf, "link-destination-invalid");
        }

        PdfCatalog catalog = _document.Internals.Catalog;
        if (catalog.Destinations.Contains(name))
        {
            return catalog.Destinations.GetDestination(name)
                ?? throw new PdfSpreadException(
                    PdfSpreadError.CorruptedPdf,
                    "named-link-destination-missing"
                );
        }

        PdfItem? namedItem = catalog.Names.NameTree?.GetValue(name, true);
        PdfArray? resolved = namedItem switch
        {
            PdfDictionary dictionary => dictionary.Elements.GetArray("/D"),
            PdfArray array => array,
            _ => null,
        };
        return resolved
            ?? throw new PdfSpreadException(
                PdfSpreadError.CorruptedPdf,
                "named-link-destination-missing"
            );
    }

    internal static PdfArray MapDestination(
        PdfArray source,
        Dictionary<PdfObjectID, int> sourcePageIndices,
        IReadOnlyList<PageProjection?> pageProjections
    )
    {
        if (
            source.Elements.Count < 2
            || source.Elements[0] is not PdfReference sourcePageReference
            || source.Elements[1] is not PdfName destinationType
            || !sourcePageIndices.TryGetValue(sourcePageReference.ObjectID, out int pageIndex)
        )
        {
            throw new PdfSpreadException(PdfSpreadError.CorruptedPdf, "link-destination-invalid");
        }

        PageProjection projection =
            pageProjections[pageIndex]
            ?? throw new PdfSpreadException(
                PdfSpreadError.Internal,
                "annotation-page-map-incomplete"
            );
        var mapped = new PdfArray(projection.DestinationPage.Owner);
        mapped.Elements.Add(projection.DestinationPage.ReferenceNotNull);
        if (projection.SourceRotation != 0)
        {
            mapped.Elements.Add(new PdfName("/Fit"));
            return mapped;
        }

        string type = destinationType.Value;
        mapped.Elements.Add(new PdfName(type));
        switch (type)
        {
            case "/XYZ":
                RequireDestinationElements(source, 5);
                AddNullableCoordinate(mapped, source, 2, projection.TransformX);
                AddNullableCoordinate(mapped, source, 3, projection.TransformY);
                AddNullableReal(mapped, source.Elements.GetNullableReal(4));
                break;

            case "/Fit":
            case "/FitB":
                break;

            case "/FitH":
            case "/FitBH":
                RequireDestinationElements(source, 3);
                AddNullableCoordinate(mapped, source, 2, projection.TransformY);
                break;

            case "/FitV":
            case "/FitBV":
                RequireDestinationElements(source, 3);
                AddNullableCoordinate(mapped, source, 2, projection.TransformX);
                break;

            case "/FitR":
                RequireDestinationElements(source, 6);
                mapped.Elements.Add(new PdfReal(projection.TransformX(source.Elements.GetReal(2))));
                mapped.Elements.Add(new PdfReal(projection.TransformY(source.Elements.GetReal(3))));
                mapped.Elements.Add(new PdfReal(projection.TransformX(source.Elements.GetReal(4))));
                mapped.Elements.Add(new PdfReal(projection.TransformY(source.Elements.GetReal(5))));
                break;

            default:
                throw new PdfSpreadException(
                    PdfSpreadError.CorruptedPdf,
                    "link-destination-type-invalid"
                );
        }

        return mapped;
    }

    private static void RequireDestinationElements(PdfArray destination, int minimum)
    {
        if (destination.Elements.Count < minimum)
        {
            throw new PdfSpreadException(
                PdfSpreadError.CorruptedPdf,
                "link-destination-parameters-missing"
            );
        }
    }

    private static void AddNullableCoordinate(
        PdfArray destination,
        PdfArray source,
        int index,
        Func<double, double> transform
    )
    {
        AddNullableReal(
            destination,
            source.Elements.GetNullableReal(index) is double value ? transform(value) : null
        );
    }

    private static void AddNullableReal(PdfArray destination, double? value)
    {
        destination.Elements.Add(value is double number ? new PdfReal(number) : PdfNull.Value);
    }

    private static void CopyOutlineCollection(
        PdfOutlineCollection source,
        PdfOutlineCollection destination,
        IReadOnlyDictionary<PdfPage, int> sourcePageIndices,
        IReadOnlyList<PageProjection?> pageProjections
    )
    {
        foreach (PdfOutline sourceOutline in source)
        {
            PdfPage sourcePage =
                sourceOutline.DestinationPage
                ?? throw new PdfSpreadException(
                    PdfSpreadError.CorruptedPdf,
                    "outline-destination-missing"
                );
            if (!sourcePageIndices.TryGetValue(sourcePage, out int sourcePageIndex))
            {
                throw new PdfSpreadException(
                    PdfSpreadError.CorruptedPdf,
                    "outline-destination-invalid"
                );
            }

            PageProjection projection =
                pageProjections[sourcePageIndex]
                ?? throw new PdfSpreadException(
                    PdfSpreadError.Internal,
                    "outline-page-map-incomplete"
                );
            PdfOutline targetOutline = destination.Add(
                sourceOutline.Title,
                projection.DestinationPage,
                sourceOutline.Opened,
                sourceOutline.Style,
                sourceOutline.TextColor
            );
            CopyOutlineDestination(sourceOutline, targetOutline, projection);
            CopyOutlineCollection(
                sourceOutline.Outlines,
                targetOutline.Outlines,
                sourcePageIndices,
                pageProjections
            );
        }
    }

    private static void CopyOutlineDestination(
        PdfOutline source,
        PdfOutline destination,
        PageProjection projection
    )
    {
        if (projection.SourceRotation != 0)
        {
            destination.PageDestinationType = PdfPageDestinationType.Fit;
            return;
        }

        destination.PageDestinationType = source.PageDestinationType;
        destination.Left = source.Left is double left ? projection.TransformX(left) : null;
        destination.Right = double.IsNaN(source.Right)
            ? double.NaN
            : projection.TransformX(source.Right);
        destination.Top = source.Top is double top ? projection.TransformY(top) : null;
        destination.Bottom = double.IsNaN(source.Bottom)
            ? double.NaN
            : projection.TransformY(source.Bottom);
        destination.Zoom = source.Zoom;
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

    private sealed class SensitiveMemoryStream : MemoryStream
    {
        protected override void Dispose(bool disposing)
        {
            if (disposing && TryGetBuffer(out ArraySegment<byte> buffer))
            {
                CryptographicOperations.ZeroMemory(buffer.AsSpan());
            }

            base.Dispose(disposing);
        }
    }
}

internal readonly record struct PageProjection(
    PdfPage DestinationPage,
    Point DrawingOffset,
    PageSize DisplayedSize,
    PdfRectangle SourceBox,
    int SourceRotation
)
{
    internal double TransformX(double sourceX)
    {
        if (SourceRotation != 0)
        {
            throw new PdfSpreadException(PdfSpreadError.Internal, "rotated-axis-transform");
        }

        return sourceX - SourceBox.X1 + PdfOffsetX;
    }

    internal double TransformY(double sourceY)
    {
        if (SourceRotation != 0)
        {
            throw new PdfSpreadException(PdfSpreadError.Internal, "rotated-axis-transform");
        }

        return sourceY - SourceBox.Y1 + PdfOffsetY;
    }

    internal XPoint TransformPoint(double sourceX, double sourceY)
    {
        if (!double.IsFinite(sourceX) || !double.IsFinite(sourceY))
        {
            throw new PdfSpreadException(
                PdfSpreadError.CorruptedPdf,
                "annotation-coordinate-invalid"
            );
        }

        double x = sourceX - SourceBox.X1;
        double y = sourceY - SourceBox.Y1;
        (double transformedX, double transformedY) = SourceRotation switch
        {
            0 => (x, y),
            90 => (y, SourceBox.Width - x),
            180 => (SourceBox.Width - x, SourceBox.Height - y),
            270 => (SourceBox.Height - y, x),
            _ => throw new PdfSpreadException(PdfSpreadError.Internal, "page-rotation-invalid"),
        };
        return new XPoint(transformedX + PdfOffsetX, transformedY + PdfOffsetY);
    }

    internal PdfRectangle TransformRectangle(PdfRectangle source)
    {
        XPoint[] corners =
        [
            TransformPoint(source.X1, source.Y1),
            TransformPoint(source.X1, source.Y2),
            TransformPoint(source.X2, source.Y1),
            TransformPoint(source.X2, source.Y2),
        ];
        return new PdfRectangle(
            new XPoint(corners.Min(point => point.X), corners.Min(point => point.Y)),
            new XPoint(corners.Max(point => point.X), corners.Max(point => point.Y))
        );
    }

    private double PdfOffsetX => DrawingOffset.X;

    private double PdfOffsetY =>
        DestinationPage.Height.Point - DrawingOffset.Y - DisplayedSize.Height;
}
