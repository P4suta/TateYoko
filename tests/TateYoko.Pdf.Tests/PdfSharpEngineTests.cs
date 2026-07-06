using TateYoko.Core.Ports;

namespace TateYoko.Pdf.Tests;

/// <summary>Tests <see cref="PdfSharpEngine"/>: input-path guard and writer factory behavior.</summary>
public sealed class PdfSharpEngineTests : IDisposable
{
    private readonly TempDir _dir = new();
    private readonly PdfSharpEngine _engine = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void OpenSourceRejectsBlankPath(string? path) =>
        Assert.ThrowsAny<ArgumentException>(() => _engine.OpenSource(path!));

    [Fact]
    public void OpenSourceReturnsDocumentWithCorrectPageCount()
    {
        string path = _dir.File("in.pdf");
        SamplePdf.CreatePortrait(path, 4);

        using ISourceDocument src = _engine.OpenSource(path);
        Assert.Equal(4, src.PageCount);
    }

    [Fact]
    public void CreateWriterReturnsFreshInstances()
    {
        using ISpreadWriter a = _engine.CreateWriter();
        using ISpreadWriter b = _engine.CreateWriter();
        Assert.NotSame(a, b);
    }

    public void Dispose() => _dir.Dispose();
}
