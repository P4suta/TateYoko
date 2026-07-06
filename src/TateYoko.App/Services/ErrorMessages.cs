using TateYoko.Core.Domain;

namespace TateYoko.App.Services;

/// <summary>Maps a language-independent <see cref="ErrorKind"/> to a localized display message.</summary>
public static class ErrorMessages
{
    /// <summary>Message shown when a non-PDF file is selected.</summary>
    public static string NotPdf => Localized.Get("NotPdf");

    public static string ForKind(ErrorKind kind) => Localized.Get(kind switch
    {
        ErrorKind.PdfCorrupted => "ErrorPdfCorrupted",
        ErrorKind.PdfPasswordProtected => "ErrorPdfPasswordProtected",
        ErrorKind.PdfNotFound => "ErrorPdfNotFound",
        ErrorKind.PdfInvalidPage => "ErrorPdfInvalidPage",
        ErrorKind.PdfWriteFailed => "ErrorPdfWriteFailed",
        ErrorKind.InvalidParameter => "ErrorInvalidParameter",
        _ => "ErrorInternal",
    });
}
