using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using TateYoko.Core.Application;
using TateYoko.Core.Domain;
using TateYoko.Core.Ports;

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
    public void MixedRotationPairUsesMaxOfDisplayDimensions()
    {
        string input = NewPath("mixrot-in.pdf");
        string output = NewPath("mixrot-out.pdf");
        // page0: 200x400 at 0 displays 200x400; page1: 200x400 at 90 displays 400x200.
        SamplePdf.Create(input, [(200, 400, 0), (200, 400, 90)]);

        _service.Convert(new SpreadRequest(input, output, FirstPageMode.Standard));

        using PdfDocument result = Open(output);
        Assert.Equal(1, result.PageCount);
        // Frame = max(200,400)*2 x max(400,200) = 800 x 400.
        Assert.Equal(800, result.Pages[0].Width.Point, Eps);
        Assert.Equal(400, result.Pages[0].Height.Point, Eps);
    }

    [Fact]
    public void MissingInputThrowsNotFound()
    {
        var ex = Assert.Throws<SpreadException>(() =>
            _service.Convert(new SpreadRequest(NewPath("nope.pdf"), NewPath("out.pdf"), FirstPageMode.Standard)));
        Assert.Equal(ErrorKind.PdfNotFound, ex.Kind);
    }

    [Fact]
    public void CorruptedInputThrowsCorrupted()
    {
        string input = NewPath("broken.pdf");
        SamplePdf.CreateCorrupted(input);

        var ex = Assert.Throws<SpreadException>(() =>
            _service.Convert(new SpreadRequest(input, NewPath("out.pdf"), FirstPageMode.Standard)));
        Assert.Equal(ErrorKind.PdfCorrupted, ex.Kind);
    }

    [Fact]
    public void PasswordProtectedInputThrowsPasswordProtected()
    {
        string input = NewPath("locked.pdf");
        SamplePdf.CreateEncrypted(input, "secret");

        var ex = Assert.Throws<SpreadException>(() =>
            _service.Convert(new SpreadRequest(input, NewPath("out.pdf"), FirstPageMode.Standard)));
        Assert.Equal(ErrorKind.PdfPasswordProtected, ex.Kind);
    }

    [Fact]
    public void CarriesMetadataFromSourceToOutput()
    {
        string input = NewPath("meta-in.pdf");
        string output = NewPath("meta-out.pdf");
        SamplePdf.CreateWithMetadata(input, info =>
        {
            info.Title = "Vertical Book";
            info.Author = "Author";
        }, pageCount: 4);

        _service.Convert(new SpreadRequest(input, output, FirstPageMode.Standard));

        using PdfDocument result = Open(output);
        Assert.Equal("Vertical Book", result.Info.Title);
        Assert.Equal("Author", result.Info.Author);
    }

    [Fact]
    public void CarriesAllMetadataFieldsFromSourceToOutput()
    {
        string input = NewPath("meta6-in.pdf");
        string output = NewPath("meta6-out.pdf");
        var created = new DateTime(2023, 5, 6, 7, 8, 9, DateTimeKind.Utc);
        SamplePdf.CreateWithMetadata(input, info =>
        {
            info.Title = "Vertical Book";
            info.Author = "Author";
            info.Subject = "Subject";
            info.Keywords = "a, b, c";
            info.Creator = "Creator";
            info.CreationDate = created;
        }, pageCount: 4);

        _service.Convert(new SpreadRequest(input, output, FirstPageMode.Standard));

        // Reopen through the same adapter read path so DateTime handling matches the source side.
        using ISourceDocument reopened = new PdfSharpEngine().OpenSource(output);
        DocumentMetadata meta = reopened.Metadata;
        Assert.Equal("Vertical Book", meta.Title);
        Assert.Equal("Author", meta.Author);
        Assert.Equal("Subject", meta.Subject);
        Assert.Equal("a, b, c", meta.Keywords);
        Assert.Equal("Creator", meta.Creator);
        Assert.Equal(created, meta.CreationDate);
    }

    [Fact]
    public void CoverSingleSpreadIsDoubleWidth()
    {
        string input = NewPath("cover-in.pdf");
        string output = NewPath("cover-out.pdf");
        SamplePdf.CreatePortrait(input, 1, widthPt: 200, heightPt: 400);

        // Cover mode with a single page -> one leading single spread, still double width.
        _service.Convert(new SpreadRequest(input, output, FirstPageMode.Cover));

        using PdfDocument result = Open(output);
        Assert.Equal(1, result.PageCount);
        Assert.Equal(400, result.Pages[0].Width.Point, Eps);
        Assert.Equal(400, result.Pages[0].Height.Point, Eps);
    }

    [Fact]
    public void LeadingBlankLeftoverProducesExpectedSpreads()
    {
        string input = NewPath("lb-in.pdf");
        string output = NewPath("lb-out.pdf");
        SamplePdf.CreatePortrait(input, 6);

        _service.Convert(new SpreadRequest(input, output, FirstPageMode.LeadingBlank));

        // [▢|1], 2·3, 4·5, [6] -> 4 spreads.
        using PdfDocument result = Open(output);
        Assert.Equal(4, result.PageCount);
    }

    [Fact]
    public void OverwritesAnExistingOutputFile()
    {
        string input = NewPath("in.pdf");
        string output = NewPath("out.pdf");
        SamplePdf.CreatePortrait(input, 4);
        File.WriteAllText(output, "stale");

        _service.Convert(new SpreadRequest(input, output, FirstPageMode.Standard));

        using PdfDocument result = Open(output);
        Assert.Equal(2, result.PageCount);
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
