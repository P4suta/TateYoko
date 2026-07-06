using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace TateYoko.Pdf.Tests;

/// <summary>Generates sample PDFs for tests (font-independent: only a border and diagonals). Mimics a scanned book's portrait pages.</summary>
internal static class SamplePdf
{
    /// <summary>Creates a PDF at <paramref name="path"/> with the given pages.</summary>
    /// <param name="path">Output path.</param>
    /// <param name="pages">Per-page (width pt, height pt, rotation degrees).</param>
    internal static void Create(string path, IReadOnlyList<(double WidthPt, double HeightPt, int Rotate)> pages)
    {
        using var doc = new PdfDocument();
        foreach ((double widthPt, double heightPt, int rotate) in pages)
        {
            PdfPage page = doc.AddPage();
            page.Width = XUnit.FromPoint(widthPt);
            page.Height = XUnit.FromPoint(heightPt);

            using (XGraphics gfx = XGraphics.FromPdfPage(page))
            {
                double w = gfx.PageSize.Width;
                double h = gfx.PageSize.Height;
                gfx.DrawRectangle(new XPen(XColors.Black, 3), 4, 4, w - 8, h - 8);
                gfx.DrawLine(new XPen(XColors.Black, 1), 0, 0, w, h);
                gfx.DrawLine(new XPen(XColors.Black, 1), w, 0, 0, h);
            }

            // Apply rotation after drawing (drawing uses unrotated coordinates).
            page.Rotate = rotate;
        }

        doc.Save(path);
    }

    /// <summary>Creates a PDF of <paramref name="pageCount"/> equally sized portrait pages.</summary>
    internal static void CreatePortrait(string path, int pageCount, double widthPt = 200, double heightPt = 400)
    {
        var pages = Enumerable.Range(0, pageCount)
            .Select(_ => (widthPt, heightPt, 0))
            .ToList();
        Create(path, pages);
    }

    /// <summary>Creates a portrait PDF whose document information is populated via <paramref name="setInfo"/>.</summary>
    internal static void CreateWithMetadata(string path, Action<PdfDocumentInformation> setInfo, int pageCount = 2)
    {
        using var doc = new PdfDocument();
        for (int i = 0; i < pageCount; i++)
        {
            PdfPage page = doc.AddPage();
            page.Width = XUnit.FromPoint(200);
            page.Height = XUnit.FromPoint(400);
        }

        setInfo(doc.Info);
        doc.Save(path);
    }

    /// <summary>Creates a one-page PDF whose CropBox is smaller than its MediaBox, optionally rotated.</summary>
    internal static void CreateWithCropBox(string path, double mediaW, double mediaH, double cropW, double cropH, int rotate = 0)
    {
        using var doc = new PdfDocument();
        PdfPage page = doc.AddPage();
        page.Width = XUnit.FromPoint(mediaW);
        page.Height = XUnit.FromPoint(mediaH);
        page.CropBox = new PdfRectangle(new XPoint(0, 0), new XPoint(cropW, cropH));
        page.Rotate = rotate;
        doc.Save(path);
    }

    /// <summary>Creates a password-protected (encrypted) PDF requiring <paramref name="userPassword"/> to open.</summary>
    internal static void CreateEncrypted(string path, string userPassword, int pageCount = 2)
    {
        using var doc = new PdfDocument();
        for (int i = 0; i < pageCount; i++)
        {
            PdfPage page = doc.AddPage();
            page.Width = XUnit.FromPoint(200);
            page.Height = XUnit.FromPoint(400);
        }

        doc.SecuritySettings.UserPassword = userPassword;
        doc.Save(path);
    }

    /// <summary>Writes bytes that are not a readable PDF, to exercise the corrupted-file path.</summary>
    internal static void CreateCorrupted(string path) =>
        File.WriteAllText(path, "%PDF-1.5\nnot a real pdf body — missing xref and trailer\n");
}
