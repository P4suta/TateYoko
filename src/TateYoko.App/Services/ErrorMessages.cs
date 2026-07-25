using TateYoko.Engine;

namespace TateYoko.App.Services;

internal static class ErrorMessages
{
    internal static string For(PdfSpreadError error) => Localized.Get(ResourceKey(error));

    internal static string ResourceKey(PdfSpreadError error) =>
        error switch
        {
            PdfSpreadError.InvalidRequest => "ErrorInvalidRequest",
            PdfSpreadError.InputNotFound => "ErrorInputNotFound",
            PdfSpreadError.ReadFailed => "ErrorReadFailed",
            PdfSpreadError.UnsupportedFile => "ErrorUnsupportedFile",
            PdfSpreadError.PasswordRequired => "ErrorPasswordRequired",
            PdfSpreadError.InvalidPassword => "ErrorInvalidPassword",
            PdfSpreadError.CorruptedPdf => "ErrorCorruptedPdf",
            PdfSpreadError.InvalidPage => "ErrorInvalidPage",
            PdfSpreadError.WriteFailed => "ErrorWriteFailed",
            PdfSpreadError.UnsupportedPdfFeature => "ErrorUnsupportedPdfFeature",
            _ => "ErrorInternal",
        };
}
