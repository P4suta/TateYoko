using NSubstitute;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using TateYoko.Core.Domain;
using TateYoko.Core.Ports;

namespace TateYoko.Pdf.Tests;

/// <summary>Tests the PDFsharp output adapter (via <see cref="PdfSharpEngine.CreateWriter"/>): guards, metadata application, save-error mapping, and disposal.</summary>
public sealed class PdfSharpSpreadWriterTests : IDisposable
{
    private readonly TempDir _dir = new();
    private readonly PdfSharpEngine _engine = new();

    private static readonly SpreadSpec Frame = new(400f, 400f);

    /// <summary>Opens a sample source and returns a real page content usable by the writer.</summary>
    private ISourceDocument OpenSample(out IPageContent content, [System.Runtime.CompilerServices.CallerMemberName] string name = "")
    {
        string path = _dir.File($"{name}-src.pdf");
        SamplePdf.CreatePortrait(path, 2, widthPt: 200, heightPt: 400);
        ISourceDocument src = _engine.OpenSource(path);
        content = src.GetPageContent(0);
        return src;
    }

    private void AddRealSpread(ISpreadWriter writer, IPageContent content) =>
        writer.AddSpread(Frame, [new PagePlacement(content, new LayoutPosition(0f, 0f))]);

    [Fact]
    public void AddSpreadRejectsNullPlacements()
    {
        using ISpreadWriter writer = _engine.CreateWriter();
        Assert.Throws<ArgumentNullException>(() => writer.AddSpread(Frame, null!));
    }

    [Fact]
    public void AddSpreadRejectsForeignPageContent()
    {
        using ISpreadWriter writer = _engine.CreateWriter();
        var foreign = Substitute.For<IPageContent>();
        var placements = new[] { new PagePlacement(foreign, new LayoutPosition(0f, 0f)) };

        var ex = Assert.Throws<SpreadException>(() => writer.AddSpread(Frame, placements));
        Assert.Equal(ErrorKind.Internal, ex.Kind);
    }

    [Fact]
    public void ApplyMetadataRejectsNull()
    {
        using ISpreadWriter writer = _engine.CreateWriter();
        Assert.Throws<ArgumentNullException>(() => writer.ApplyMetadata(null!));
    }

    [Fact]
    public void AppliesOnlyNonNullMetadataFields()
    {
        string outPath = _dir.File("meta-out.pdf");
        using (ISourceDocument src = OpenSample(out IPageContent content))
        using (ISpreadWriter writer = _engine.CreateWriter())
        {
            AddRealSpread(writer, content);
            writer.ApplyMetadata(new DocumentMetadata(Title: "Spread Book"));
            writer.Save(outPath);
        }

        using PdfDocument reopened = PdfReader.Open(outPath, PdfDocumentOpenMode.Import);
        Assert.Equal("Spread Book", reopened.Info.Title);
        Assert.True(string.IsNullOrEmpty(reopened.Info.Author));
    }

    [Fact]
    public void ApplyMetadataPreservesEarlierFieldsAcrossCalls()
    {
        string outPath = _dir.File("preserve-out.pdf");
        using (ISourceDocument src = OpenSample(out IPageContent content))
        using (ISpreadWriter writer = _engine.CreateWriter())
        {
            AddRealSpread(writer, content);
            // A later call with null fields must not clear values set by an earlier call.
            writer.ApplyMetadata(new DocumentMetadata(Author: "First Author"));
            writer.ApplyMetadata(new DocumentMetadata(Title: "Later Title"));
            writer.Save(outPath);
        }

        using PdfDocument reopened = PdfReader.Open(outPath, PdfDocumentOpenMode.Import);
        Assert.Equal("Later Title", reopened.Info.Title);
        Assert.Equal("First Author", reopened.Info.Author);
    }

    [Fact]
    public void SavesSpreadsInTheOrderTheyWereAdded()
    {
        string outPath = _dir.File("order-out.pdf");
        using (ISourceDocument src = OpenSample(out IPageContent content))
        using (ISpreadWriter writer = _engine.CreateWriter())
        {
            var pos = new LayoutPosition(0f, 0f);
            writer.AddSpread(new SpreadSpec(400f, 400f), [new PagePlacement(content, pos)]);
            writer.AddSpread(new SpreadSpec(500f, 450f), [new PagePlacement(content, pos)]);
            writer.AddSpread(new SpreadSpec(600f, 500f), [new PagePlacement(content, pos)]);
            writer.Save(outPath);
        }

        using PdfDocument reopened = PdfReader.Open(outPath, PdfDocumentOpenMode.Import);
        Assert.Equal(3, reopened.PageCount);
        // Distinct frame widths let us assert the pages kept their insertion order.
        Assert.Equal(400, reopened.Pages[0].Width.Point, 0.5);
        Assert.Equal(500, reopened.Pages[1].Width.Point, 0.5);
        Assert.Equal(600, reopened.Pages[2].Width.Point, 0.5);
    }

    [Fact]
    public void SaveToUnwritablePathMapsToWriteFailed()
    {
        using ISourceDocument src = OpenSample(out IPageContent content);
        using ISpreadWriter writer = _engine.CreateWriter();
        AddRealSpread(writer, content);

        // A path under a non-existent directory triggers a DirectoryNotFoundException (IOException).
        string bad = _dir.File(Path.Combine("does", "not", "exist", "out.pdf"));
        var ex = Assert.Throws<SpreadException>(() => writer.Save(bad));
        Assert.Equal(ErrorKind.PdfWriteFailed, ex.Kind);
    }

    [Fact]
    public void DisposeIsIdempotent()
    {
        ISpreadWriter writer = _engine.CreateWriter();
        writer.Dispose();
        writer.Dispose(); // must not throw
    }

    public void Dispose() => _dir.Dispose();
}
