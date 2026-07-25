using System.Text;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.Annotations;
using PdfSharp.Pdf.IO;

namespace TateYoko.Engine.Tests;

internal static class SamplePdf
{
    internal static void Create(
        string path,
        params (double Width, double Height, int Rotation)[] pages
    )
    {
        using var document = new PdfDocument();
        document.Info.Title = "TateYoko fixture";
        document.Info.Author = "TateYoko tests";
        for (int index = 0; index < pages.Length; index++)
        {
            (double width, double height, int rotation) = pages[index];
            PdfPage page = document.AddPage();
            page.Width = XUnit.FromPoint(width);
            page.Height = XUnit.FromPoint(height);
            page.Rotate = rotation;
            using XGraphics graphics = XGraphics.FromPdfPage(page);
            graphics.DrawRectangle(
                index % 2 == 0 ? XBrushes.Red : XBrushes.Blue,
                0,
                0,
                width,
                height
            );
        }

        document.Save(path);
    }

    internal static void CreateProtected(string path, string password, int pages = 2)
    {
        using var document = new PdfDocument();
        for (int index = 0; index < pages; index++)
        {
            PdfPage page = document.AddPage();
            page.Width = XUnit.FromPoint(200);
            page.Height = XUnit.FromPoint(300);
        }

        document.SecuritySettings.UserPassword = password;
        document.SecuritySettings.OwnerPassword = password;
        document.Save(path);
    }

    internal static void CreateWithOutlines(
        string path,
        int firstPageRotation = 0,
        double cropOffsetX = 0,
        double cropOffsetY = 0
    )
    {
        using var document = new PdfDocument();
        PdfPage first = document.AddPage();
        first.Width = XUnit.FromPoint(200 + (cropOffsetX * 2));
        first.Height = XUnit.FromPoint(300 + (cropOffsetY * 2));
        first.CropBox = new PdfRectangle(
            new XPoint(cropOffsetX, cropOffsetY),
            new XPoint(cropOffsetX + 200, cropOffsetY + 300)
        );
        first.Rotate = firstPageRotation;
        PdfPage second = document.AddPage();
        second.Width = XUnit.FromPoint(200);
        second.Height = XUnit.FromPoint(300);

        PdfOutline chapter = document.Outlines.Add(
            "Chapter",
            first,
            opened: true,
            PdfOutlineStyle.Bold,
            XColors.DarkRed
        );
        chapter.Left = cropOffsetX + 12;
        chapter.Top = cropOffsetY + 250;
        chapter.Zoom = 1.25;
        chapter.Outlines.Add("Section", second);
        chapter.Opened = true;
        document.PageMode = PdfPageMode.UseOutlines;
        document.Save(path);
    }

    internal static void CreateWithLinks(
        string path,
        int firstPageRotation = 0,
        int secondPageRotation = 0
    )
    {
        using var document = new PdfDocument();
        PdfPage first = document.AddPage();
        first.Width = XUnit.FromPoint(200);
        first.Height = XUnit.FromPoint(300);
        first.Rotate = firstPageRotation;
        PdfPage second = document.AddPage();
        second.Width = XUnit.FromPoint(200);
        second.Height = XUnit.FromPoint(300);
        second.Rotate = secondPageRotation;

        PdfLinkAnnotation web = PdfLinkAnnotation.CreateWebLink(
            Rectangle(10, 20, 60, 40),
            "https://example.test/path?q=1"
        );
        var quadPoints = new PdfArray(document);
        foreach (double value in new[] { 10d, 40d, 60d, 40d, 10d, 20d, 60d, 20d })
        {
            quadPoints.Elements.Add(new PdfReal(value));
        }

        web.Elements["/QuadPoints"] = quadPoints;
        first.Annotations.Add(web);
        first.Annotations.Add(
            PdfLinkAnnotation.CreateDocumentLink(
                Rectangle(70, 20, 120, 40),
                destinationPage: 2,
                new XPoint(15, 250)
            )
        );

        document.AddNamedDestination(
            "chapter-two",
            destinationPage: 2,
            PdfNamedDestinationParameters.CreatePosition(new XPoint(25, 200), zoom: 1.5)
        );
        first.Annotations.Add(
            PdfLinkAnnotation.CreateDocumentLink(Rectangle(130, 20, 180, 40), "chapter-two")
        );
        document.Save(path);
    }

    internal static void CreateWithTextAnnotation(string path)
    {
        using var document = new PdfDocument();
        PdfPage page = document.AddPage();
        page.Width = XUnit.FromPoint(200);
        page.Height = XUnit.FromPoint(300);
        var annotation = new PdfTextAnnotation(document)
        {
            Rectangle = Rectangle(10, 20, 40, 50),
            Contents = "A note that must never disappear silently.",
        };
        page.Annotations.Add(annotation);
        document.Save(path);
    }

    internal static void CreateWithDocumentSettings(string path)
    {
        using var document = new PdfDocument();
        PdfPage page = document.AddPage();
        page.Width = XUnit.FromPoint(200);
        page.Height = XUnit.FromPoint(300);
        document.Info.Title = "Preserved title";
        document.Info.Author = "Preserved author";
        document.Info.Subject = "Preserved subject";
        document.Info.Keywords = "one, two";
        document.Info.Creator = "Fixture creator";
        document.Info.Elements.SetString("/Producer", "Fixture producer");
        document.Info.Elements.SetName("/Trapped", "/False");
        document.Info.CreationDate = new DateTime(2024, 3, 2, 1, 0, 0, DateTimeKind.Utc);
        document.Info.ModificationDate = new DateTime(2024, 3, 3, 1, 0, 0, DateTimeKind.Utc);
        document.Version = 20;
        document.Language = "ja-JP";
        document.PageLayout = PdfPageLayout.TwoPageRight;
        document.PageMode = PdfPageMode.UseThumbs;
        document.ViewerPreferences.HideToolbar = true;
        document.ViewerPreferences.HideMenubar = true;
        document.ViewerPreferences.HideWindowUI = true;
        document.ViewerPreferences.FitWindow = true;
        document.ViewerPreferences.CenterWindow = true;
        document.ViewerPreferences.DisplayDocTitle = true;
        document.ViewerPreferences.Direction = PdfReadingDirection.RightToLeft;

        document.Save(path);
    }

    internal static void CreateCustomized(string path, Action<PdfDocument, PdfPage> customize)
    {
        ArgumentNullException.ThrowIfNull(customize);
        using var document = new PdfDocument();
        PdfPage page = document.AddPage();
        page.Width = XUnit.FromPoint(200);
        page.Height = XUnit.FromPoint(300);
        customize(document, page);
        document.Save(path);
    }

    internal static void CreateWithEveryNamedDestination(string path)
    {
        using var document = new PdfDocument();
        PdfPage page = document.AddPage();
        page.Width = XUnit.FromPoint(200);
        page.Height = XUnit.FromPoint(300);
        document.AddNamedDestination(
            "position",
            1,
            PdfNamedDestinationParameters.CreatePosition(10, 250, 1.5)
        );
        document.AddNamedDestination("fit", 1, PdfNamedDestinationParameters.CreateFit());
        document.AddNamedDestination(
            "fit-horizontal",
            1,
            PdfNamedDestinationParameters.CreateFitHorizontally(250)
        );
        document.AddNamedDestination(
            "fit-horizontal-null",
            1,
            PdfNamedDestinationParameters.CreateFitHorizontally(null)
        );
        document.AddNamedDestination(
            "fit-vertical",
            1,
            PdfNamedDestinationParameters.CreateFitVertically(10)
        );
        document.AddNamedDestination(
            "fit-vertical-null",
            1,
            PdfNamedDestinationParameters.CreateFitVertically(null)
        );
        document.AddNamedDestination(
            "fit-rectangle",
            1,
            PdfNamedDestinationParameters.CreateFitRectangle(10, 20, 190, 280)
        );
        document.AddNamedDestination(
            "fit-bounding-box",
            1,
            PdfNamedDestinationParameters.CreateFitBoundingBox()
        );
        document.AddNamedDestination(
            "fit-bounding-box-horizontal",
            1,
            PdfNamedDestinationParameters.CreateFitBoundingBoxHorizontally(250)
        );
        document.AddNamedDestination(
            "fit-bounding-box-horizontal-null",
            1,
            PdfNamedDestinationParameters.CreateFitBoundingBoxHorizontally(null)
        );
        document.AddNamedDestination(
            "fit-bounding-box-vertical",
            1,
            PdfNamedDestinationParameters.CreateFitBoundingBoxVertically(10)
        );
        document.AddNamedDestination(
            "fit-bounding-box-vertical-null",
            1,
            PdfNamedDestinationParameters.CreateFitBoundingBoxVertically(null)
        );
        document.Save(path);
    }

    internal static void CreateWithMalformedNamedDestination(
        string path,
        string destinationType,
        bool omitParameters
    )
    {
        using (var document = new PdfDocument())
        {
            PdfPage page = document.AddPage();
            page.Width = XUnit.FromPoint(200);
            page.Height = XUnit.FromPoint(300);
            document.AddNamedDestination("malformed", 1, PdfNamedDestinationParameters.CreateFit());
            document.Save(path);
        }

        using PdfDocument editable = PdfReader.Open(path, PdfDocumentOpenMode.Modify);
        PdfArray destination =
            editable.Internals.Catalog.Names.NameTree?.GetValue("malformed", true) as PdfArray
            ?? throw new InvalidOperationException(
                "PDFsharp did not create the named destination fixture."
            );
        destination.Elements[1] = new PdfName(destinationType);
        if (omitParameters)
        {
            while (destination.Elements.Count > 2)
            {
                destination.Elements.RemoveAt(destination.Elements.Count - 1);
            }
        }

        editable.Save(path);
    }

    internal static void CreateWithUnsupportedCatalogFeature(string path, string feature)
    {
        using var document = new PdfDocument();
        PdfPage page = document.AddPage();
        page.Width = XUnit.FromPoint(200);
        page.Height = XUnit.FromPoint(300);
        document.Internals.Catalog.Elements[feature] = new PdfDictionary(document);
        document.Save(path);
    }

    internal static void CreateWithEmbeddedFile(string path)
    {
        using var document = new PdfDocument();
        PdfPage page = document.AddPage();
        page.Width = XUnit.FromPoint(200);
        page.Height = XUnit.FromPoint(300);
        using var attachment = new MemoryStream("attached data"u8.ToArray());
        document.AddEmbeddedFile("attachment.txt", attachment);
        document.Save(path);
    }

    internal static void CreateWithUnsupportedPageFeature(string path, string feature)
    {
        using var document = new PdfDocument();
        PdfPage page = document.AddPage();
        page.Width = XUnit.FromPoint(200);
        page.Height = XUnit.FromPoint(300);
        if (string.Equals(feature, "/TrimBox", StringComparison.Ordinal))
        {
            page.TrimBox = new PdfRectangle(new XPoint(5, 5), new XPoint(195, 295));
        }
        else
        {
            page.Elements[feature] = string.Equals(feature, "/UserUnit", StringComparison.Ordinal)
                ? new PdfReal(2)
                : new PdfDictionary(document);
        }

        document.Save(path);
    }

    internal static void CreateWithCustomXmp(string path)
    {
        const string xmp =
            "<?xpacket begin=\"\"?>"
            + "<x:xmpmeta xmlns:x=\"adobe:ns:meta/\">"
            + "<rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\">"
            + "<rdf:Description xmlns:custom=\"urn:tateyoko:custom\">"
            + "<custom:mustPreserve>true</custom:mustPreserve>"
            + "</rdf:Description></rdf:RDF></x:xmpmeta>"
            + "<?xpacket end=\"w\"?>";
        CreateRawPdfWithXmp(path, xmp);
    }

    internal static void CreateWithUnsupportedStandardXmp(string path)
    {
        const string xmp =
            "<?xpacket begin=\"\"?>"
            + "<x:xmpmeta xmlns:x=\"adobe:ns:meta/\">"
            + "<rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\">"
            + "<rdf:Description xmlns:dc=\"http://purl.org/dc/elements/1.1/\">"
            + "<dc:rights><rdf:Alt><rdf:li xml:lang=\"x-default\">"
            + "must preserve rights</rdf:li></rdf:Alt></dc:rights>"
            + "</rdf:Description></rdf:RDF></x:xmpmeta>"
            + "<?xpacket end=\"w\"?>";
        CreateRawPdfWithXmp(path, xmp);
    }

    internal static void CreateWithInvalidXmp(string path) =>
        CreateRawPdfWithXmp(path, "<not-closed>");

    internal static void CreateWithMetadataDictionaryWithoutStream(string path) =>
        CreateRawPdf(
            path,
            [
                "<< /Type /Catalog /Pages 2 0 R /Metadata 5 0 R >>",
                "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 300] "
                    + "/Resources << >> /Contents 4 0 R >>",
                "<< /Length 0 >>\nstream\n\nendstream",
                "<< /Type /Metadata /Subtype /XML >>",
            ]
        );

    internal static void CreateWithRawCropBox(string path, string cropBox) =>
        CreateRawPdf(
            path,
            [
                "<< /Type /Catalog /Pages 2 0 R >>",
                "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 300] "
                    + $"/CropBox [{cropBox}] /Resources << >> /Contents 4 0 R >>",
                "<< /Length 0 >>\nstream\n\nendstream",
            ]
        );

    internal static void CreateWithLegacyNamedDestination(string path) =>
        CreateRawPdf(
            path,
            [
                "<< /Type /Catalog /Pages 2 0 R /Dests << /legacy [3 0 R /Fit] >> >>",
                "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 300] "
                    + "/Resources << >> /Contents 4 0 R >>",
                "<< /Length 0 >>\nstream\n\nendstream",
            ]
        );

    internal static void CreateWithConflictingLinkDestination(string path)
    {
        using var document = new PdfDocument();
        PdfPage page = document.AddPage();
        page.Width = XUnit.FromPoint(200);
        page.Height = XUnit.FromPoint(300);
        PdfLinkAnnotation link = PdfLinkAnnotation.CreateDocumentLink(
            Rectangle(10, 20, 60, 40),
            1,
            new XPoint(10, 250)
        );
        var action = new PdfDictionary(document);
        action.Elements.SetName("/S", "/GoTo");
        var destination = new PdfArray(document);
        destination.Elements.Add(page.ReferenceNotNull);
        destination.Elements.Add(new PdfName("/Fit"));
        action.Elements["/D"] = destination;
        link.Elements["/A"] = action;
        page.Annotations.Add(link);
        document.Save(path);
    }

    internal static void CreateWithInvalidQuadPoints(string path, bool empty)
    {
        using var document = new PdfDocument();
        PdfPage page = document.AddPage();
        page.Width = XUnit.FromPoint(200);
        page.Height = XUnit.FromPoint(300);
        PdfLinkAnnotation link = PdfLinkAnnotation.CreateWebLink(
            Rectangle(10, 20, 60, 40),
            "https://example.test"
        );
        var points = new PdfArray(document);
        if (!empty)
        {
            points.Elements.Add(new PdfReal(10));
            points.Elements.Add(new PdfReal(20));
        }

        link.Elements["/QuadPoints"] = points;
        page.Annotations.Add(link);
        document.Save(path);
    }

    internal static void CreateWithPdfNameDestinationLink(string path)
    {
        using (var document = new PdfDocument())
        {
            PdfPage page = document.AddPage();
            page.Width = XUnit.FromPoint(200);
            page.Height = XUnit.FromPoint(300);
            document.AddNamedDestination("chapter", 1, PdfNamedDestinationParameters.CreateFit());
            page.Annotations.Add(
                PdfLinkAnnotation.CreateDocumentLink(Rectangle(10, 20, 60, 40), "chapter")
            );
            document.Save(path);
        }

        using PdfDocument editable = PdfReader.Open(path, PdfDocumentOpenMode.Modify);
        PdfAnnotation link = editable.Pages[0].Annotations[0];
        link.Elements.Remove("/A");
        link.Elements["/Dest"] = new PdfName("/chapter");
        editable.Save(path);
    }

    private static void CreateRawPdfWithXmp(string path, string xmp)
    {
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R /Metadata 5 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 300] "
                + "/Resources << >> /Contents 4 0 R >>",
            "<< /Length 0 >>\nstream\n\nendstream",
            $"<< /Type /Metadata /Subtype /XML /Length {Encoding.ASCII.GetByteCount(xmp)} "
                + $">>\nstream\n{xmp}\nendstream",
        ];

        CreateRawPdf(path, objects);
    }

    private static void CreateRawPdf(string path, string[] objects)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        WriteAscii(stream, "%PDF-1.7\n");
        var offsets = new List<long>(objects.Length);
        for (int index = 0; index < objects.Length; index++)
        {
            offsets.Add(stream.Position);
            WriteAscii(stream, $"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }

        long xrefOffset = stream.Position;
        WriteAscii(stream, $"xref\n0 {objects.Length + 1}\n");
        WriteAscii(stream, "0000000000 65535 f \n");
        foreach (long offset in offsets)
        {
            WriteAscii(stream, $"{offset:0000000000} 00000 n \n");
        }

        WriteAscii(
            stream,
            $"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\n"
                + $"startxref\n{xrefOffset}\n%%EOF\n"
        );
    }

    internal static void CreateColored(
        string path,
        params (byte Red, byte Green, byte Blue, double Width, double Height)[] pages
    )
    {
        using var document = new PdfDocument();
        foreach ((byte red, byte green, byte blue, double width, double height) in pages)
        {
            PdfPage page = document.AddPage();
            page.Width = XUnit.FromPoint(width);
            page.Height = XUnit.FromPoint(height);
            using XGraphics graphics = XGraphics.FromPdfPage(page);
            var brush = new XSolidBrush(XColor.FromArgb(red, green, blue));
            graphics.DrawRectangle(brush, 0, 0, width, height);
        }

        document.Save(path);
    }

    private static PdfRectangle Rectangle(double x1, double y1, double x2, double y2) =>
        new(new XPoint(x1, y1), new XPoint(x2, y2));

    private static void WriteAscii(Stream stream, string value)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(value);
        stream.Write(bytes);
    }
}
