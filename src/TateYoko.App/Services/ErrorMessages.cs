using TateYoko.Engine;

namespace TateYoko.App.Services;

internal static class ErrorMessages
{
    internal static string For(PdfSpreadError error, string? technicalDetail = null) =>
        Localized.Get(ResourceKey(error, technicalDetail));

    internal static string ResourceKey(PdfSpreadError error, string? technicalDetail = null) =>
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
            PdfSpreadError.UnsupportedPdfFeature => UnsupportedFeatureResourceKey(technicalDetail),
            _ => "ErrorInternal",
        };

    private static string UnsupportedFeatureResourceKey(string? technicalDetail) =>
        technicalDetail switch
        {
            "unsupported-annotation" => "ErrorUnsupportedAnnotation",
            "unsupported-interactive-form" or "unsupported-document-permissions" =>
                "ErrorUnsupportedForm",
            "unsupported-javascript"
            or "unsupported-document-open-action"
            or "unsupported-document-additional-action"
            or "unsupported-page-additional-action"
            or "unsupported-presentation-step"
            or "unsupported-page-transition"
            or "unsupported-multimedia-rendition" => "ErrorUnsupportedAction",
            "unsupported-embedded-file"
            or "unsupported-associated-file"
            or "unsupported-page-associated-file"
            or "unsupported-pdf-collection" => "ErrorUnsupportedAttachment",
            "unsupported-optional-content" or "unsupported-viewport-content" =>
                "ErrorUnsupportedLayer",
            _ => "ErrorUnsupportedPdfFeature",
        };
}
