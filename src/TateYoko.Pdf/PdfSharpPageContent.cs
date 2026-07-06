using PdfSharp.Drawing;
using TateYoko.Core.Ports;

namespace TateYoko.Pdf;

/// <summary>PDFsharp page content: wraps one source page as an <see cref="XPdfForm"/> for the writer to draw.</summary>
internal sealed class PdfSharpPageContent : IPageContent
{
    internal PdfSharpPageContent(XPdfForm form)
    {
        Form = form;
    }

    internal XPdfForm Form { get; }
}
