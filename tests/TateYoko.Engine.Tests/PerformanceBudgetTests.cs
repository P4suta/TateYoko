using System.Diagnostics;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace TateYoko.Engine.Tests;

public sealed class PerformanceBudgetTests
{
    [Fact]
    [Trait("Category", "Performance")]
    public async Task ThousandPageDocumentStaysWithinReleaseBudgets()
    {
        using var temp = new TempDirectory();
        string input = temp.File("thousand-pages.pdf");
        string output = temp.File("thousand-pages_spread.pdf");
        SamplePdf.Create(input, [.. Enumerable.Repeat((100d, 150d, 0), 1_000)]);
        var converter = new PdfSpreadConverter();

        long allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        var stopwatch = Stopwatch.StartNew();
        PdfSpreadResult result = await converter.ConvertAsync(
            new PdfSpreadRequest(input, output, FirstPageMode.Standard),
            cancellationToken: TestContext.Current.CancellationToken
        );
        stopwatch.Stop();
        long allocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;

        Assert.Equal(500, result.SpreadCount);
        using PdfDocument document = PdfReader.Open(output, PdfDocumentOpenMode.Import);
        Assert.Equal(500, document.PageCount);
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(10),
            $"Conversion took {stopwatch.Elapsed}."
        );
        Assert.True(
            allocatedBytes < 256L * 1024 * 1024,
            $"Conversion allocated {allocatedBytes / 1024d / 1024d:F1} MiB."
        );
        Assert.True(
            new FileInfo(output).Length < (new FileInfo(input).Length * 1.1) + (1024 * 1024),
            "Output grew beyond the allowed structural overhead."
        );
    }
}
