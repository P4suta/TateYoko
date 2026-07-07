using System.Text;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using TateYoko.Core.Application;
using TateYoko.Core.Domain;

namespace TateYoko.Pdf.Tests;

/// <summary>
/// Verifies the output is linearized ("Fast Web View"). qpdf ships x64/x86 only, so these run only
/// on those architectures; elsewhere the writer falls back to an un-linearized (but valid) PDF.
/// </summary>
public sealed class LinearizationTests : IDisposable
{
    private readonly TempDir _dir = new();
    private readonly SpreadConversionService _service = new(new PdfSharpEngine());

    private static bool QpdfSupportedHere =>
        System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture
            is System.Runtime.InteropServices.Architecture.X64
            or System.Runtime.InteropServices.Architecture.X86;

    [Fact]
    public void OutputIsLinearizedForFastWebView()
    {
        if (!QpdfSupportedHere)
        {
            return; // xUnit v2 has no dynamic skip; qpdf is x64/x86 only.
        }

        string input = _dir.File("in.pdf");
        string output = _dir.File("out.pdf");
        SamplePdf.CreatePortrait(input, 4);

        _service.Convert(new SpreadRequest(input, output, FirstPageMode.Standard));

        // A linearized PDF places its linearization parameter dictionary (with "/Linearized")
        // as the first object, right after the header near the start of the file.
        Assert.Contains("/Linearized", ReadHead(output, 2048));
    }

    [Fact]
    public void LinearizedOutputStaysAValidPdfWithExpectedPages()
    {
        if (!QpdfSupportedHere)
        {
            return; // xUnit v2 has no dynamic skip; qpdf is x64/x86 only.
        }

        string input = _dir.File("valid-in.pdf");
        string output = _dir.File("valid-out.pdf");
        SamplePdf.CreatePortrait(input, 4);

        _service.Convert(new SpreadRequest(input, output, FirstPageMode.Standard));

        // Re-opening through PDFsharp proves qpdf produced a structurally valid file.
        using PdfDocument reopened = PdfReader.Open(output, PdfDocumentOpenMode.Import);
        Assert.Equal(2, reopened.PageCount);
    }

    [Fact]
    public void PreservesMetadataThroughLinearization()
    {
        if (!QpdfSupportedHere)
        {
            return; // xUnit v2 has no dynamic skip; qpdf is x64/x86 only.
        }

        string input = _dir.File("meta-in.pdf");
        string output = _dir.File("meta-out.pdf");
        SamplePdf.CreateWithMetadata(input, info =>
        {
            info.Title = "Vertical Book";
            info.Author = "Author";
        }, pageCount: 4);

        _service.Convert(new SpreadRequest(input, output, FirstPageMode.Standard));

        using PdfDocument reopened = PdfReader.Open(output, PdfDocumentOpenMode.Import);
        Assert.Equal("Vertical Book", reopened.Info.Title);
        Assert.Equal("Author", reopened.Info.Author);
    }

    private static string ReadHead(string path, int byteCount)
    {
        using FileStream fs = File.OpenRead(path);
        byte[] buffer = new byte[Math.Min(byteCount, (int)fs.Length)];
        int read = fs.Read(buffer, 0, buffer.Length);
        return Encoding.Latin1.GetString(buffer, 0, read);
    }

    public void Dispose() => _dir.Dispose();
}
