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
}
