using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace TateYoko.Engine.Tests;

public sealed class LargeDocumentTests
{
    [Fact]
    public async Task ThousandPageDocumentKeepsSpreadCountAndSize()
    {
        using var temp = new TempDirectory();
        string input = temp.File("thousand-pages.pdf");
        string output = temp.File("thousand-pages_spread.pdf");
        SamplePdf.Create(input, [.. Enumerable.Repeat((100d, 150d, 0), 1_000)]);
        var converter = new PdfSpreadConverter();

        PdfSpreadResult result = await converter.ConvertAsync(
            new PdfSpreadRequest(input, output, FirstPageMode.Standard),
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Equal(500, result.SpreadCount);
        using PdfDocument document = PdfReader.Open(output, PdfDocumentOpenMode.Import);
        Assert.Equal(500, document.PageCount);
        Assert.True(
            new FileInfo(output).Length < (new FileInfo(input).Length * 1.1) + (1024 * 1024),
            "Output grew beyond the allowed structural overhead."
        );
    }
}
