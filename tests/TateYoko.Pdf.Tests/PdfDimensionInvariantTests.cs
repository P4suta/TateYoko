using CsCheck;
using TateYoko.Core.Domain;
using TateYoko.Core.Ports;

namespace TateYoko.Pdf.Tests;

/// <summary>
/// Property-based invariants for the PDFsharp source adapter's rotation-adjusted page sizing:
/// 0/180 keep orientation, 90/270 swap width and height, and non-normalized rotation values are
/// reduced modulo 360. Complements the example-based <see cref="PdfSharpSourceDocumentTests"/>.
/// </summary>
public sealed class PdfDimensionInvariantTests : IDisposable
{
    private const double Eps = 0.5;

    private readonly TempDir _dir = new();
    private readonly PdfSharpEngine _engine = new();

    private static readonly Gen<(float W, float H)> GenSize =
        Gen.Select(Gen.Float[1f, 5000f], Gen.Float[1f, 5000f], (w, h) => (w, h));

    /// <summary>0, 90, 180, or 270 degrees.</summary>
    private static readonly Gen<int> GenQuarterTurn = Gen.Int[0, 3].Select(i => i * 90);

    [Fact]
    public void QuarterTurnFollowsTheDisplayRule() =>
        Gen.Select(GenSize, GenQuarterTurn, (size, rot) => (size, rot)).Sample(t =>
        {
            string path = _dir.File("rot.pdf");
            SamplePdf.Create(path, [(t.size.W, t.size.H, t.rot)]);

            using ISourceDocument src = _engine.OpenSource(path);
            PageDimension dim = src.GetPageDimension(0);

            bool swaps = t.rot is 90 or 270;
            double expectedW = swaps ? t.size.H : t.size.W;
            double expectedH = swaps ? t.size.W : t.size.H;
            Assert.Equal(expectedW, dim.WidthPt, Eps);
            Assert.Equal(expectedH, dim.HeightPt, Eps);
        }, iter: 30, threads: 1); // single-threaded: each iteration reuses the same file path

    [Theory]
    [InlineData(360, 200, 400)]   // 360 -> 0
    [InlineData(450, 400, 200)]   // 450 -> 90, swaps
    [InlineData(720, 200, 400)]   // 720 -> 0
    [InlineData(-90, 400, 200)]   // -90 -> 270, swaps
    [InlineData(-270, 400, 200)]  // -270 -> 90, swaps
    [InlineData(-180, 200, 400)]  // -180 -> 180
    public void NonNormalizedRotationIsReducedModulo360(int rotate, double expectedW, double expectedH)
    {
        string path = _dir.File("norm.pdf");
        SamplePdf.Create(path, [(200, 400, rotate)]);

        using ISourceDocument src = _engine.OpenSource(path);
        PageDimension dim = src.GetPageDimension(0);

        Assert.Equal(expectedW, dim.WidthPt, Eps);
        Assert.Equal(expectedH, dim.HeightPt, Eps);
    }

    public void Dispose() => _dir.Dispose();
}
