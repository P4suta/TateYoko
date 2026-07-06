using TateYoko.Core.Ports;

namespace TateYoko.Pdf;

/// <summary><see cref="IPdfEngine"/> backed by PDFsharp; the single PDF dependency injected at the composition root.</summary>
public sealed class PdfSharpEngine : IPdfEngine
{
    public ISourceDocument OpenSource(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new PdfSharpSourceDocument(path);
    }

    public ISpreadWriter CreateWriter() => new PdfSharpSpreadWriter();
}
