using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using TateYoko.Core.Application;
using TateYoko.Core.Domain;

namespace TateYoko.Pdf.Tests;

/// <summary>End-to-end PDF conversion tests via PDFsharp: spread count, frame size, and rotated dimensions.</summary>
public sealed class PdfSharpConversionTests : IDisposable
{
    private const double Eps = 0.5;

    private readonly string _workDir;
    private readonly SpreadConversionService _service = new(new PdfSharpEngine());

    public PdfSharpConversionTests()
    {
        _workDir = Path.Combine(Path.GetTempPath(), "tateyoko-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workDir);
    }

    [Theory]
    [InlineData(4, FirstPageMode.Standard, 2)]
    [InlineData(5, FirstPageMode.Standard, 3)]
    [InlineData(1, FirstPageMode.Standard, 1)]
    [InlineData(5, FirstPageMode.Cover, 3)]
    [InlineData(6, FirstPageMode.Cover, 4)]
    [InlineData(5, FirstPageMode.LeadingBlank, 3)]
    public void ProducesExpectedSpreadCount(int sourcePages, FirstPageMode mode, int expectedSpreads)
    {
        string input = NewPath("in.pdf");
        string output = NewPath("out.pdf");
        SamplePdf.CreatePortrait(input, sourcePages);

        _service.Convert(new SpreadRequest(input, output, mode));

        using PdfDocument result = Open(output);
        Assert.Equal(expectedSpreads, result.PageCount);
    }

    [Fact]
    public void EqualPagesGiveDoubleWidthFrame()
    {
        string input = NewPath("in.pdf");
        string output = NewPath("out.pdf");
        SamplePdf.CreatePortrait(input, 4, widthPt: 200, heightPt: 400);

        _service.Convert(new SpreadRequest(input, output, FirstPageMode.Standard));

        using PdfDocument result = Open(output);
        foreach (PdfPage page in result.Pages)
        {
            Assert.Equal(400, page.Width.Point, Eps);
            Assert.Equal(400, page.Height.Point, Eps);
        }
    }

    [Fact]
    public void MixedSizesUseMaxBoundsForFrame()
    {
        string input = NewPath("in.pdf");
        string output = NewPath("out.pdf");
        SamplePdf.Create(input, [(200, 400, 0), (240, 300, 0)]);

        _service.Convert(new SpreadRequest(input, output, FirstPageMode.Standard));

        using PdfDocument result = Open(output);
        // One spread. Frame = max(200,240)*2 x max(400,300) = 480 x 400.
        Assert.Equal(1, result.PageCount);
        Assert.Equal(480, result.Pages[0].Width.Point, Eps);
        Assert.Equal(400, result.Pages[0].Height.Point, Eps);
    }

    [Fact]
    public void RotatedPageUsesSwappedDimensions()
    {
        string input = NewPath("in.pdf");
        string output = NewPath("out.pdf");
        // 200x400 rotated 90 degrees displays as 400x200; both pages rotated.
        SamplePdf.Create(input, [(200, 400, 90), (200, 400, 90)]);

        _service.Convert(new SpreadRequest(input, output, FirstPageMode.Standard));

        using PdfDocument result = Open(output);
        Assert.Equal(1, result.PageCount);
        // Display width 400 per half -> frame width 800, height 200.
        Assert.Equal(800, result.Pages[0].Width.Point, Eps);
        Assert.Equal(200, result.Pages[0].Height.Point, Eps);
    }

    [Fact]
    public void MissingInputThrowsNotFound()
    {
        var ex = Assert.Throws<SpreadException>(() =>
            _service.Convert(new SpreadRequest(NewPath("nope.pdf"), NewPath("out.pdf"), FirstPageMode.Standard)));
        Assert.Equal(ErrorKind.PdfNotFound, ex.Kind);
    }

    private string NewPath(string name) => Path.Combine(_workDir, name);

    private static PdfDocument Open(string path) => PdfReader.Open(path, PdfDocumentOpenMode.Import);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_workDir, recursive: true);
        }
        catch (IOException)
        {
            // Best effort.
        }
    }
}
