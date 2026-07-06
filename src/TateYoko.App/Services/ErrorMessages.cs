using TateYoko.Core.Domain;

namespace TateYoko.App.Services;

/// <summary>Maps a language-independent <see cref="ErrorKind"/> to a localized display message.</summary>
public static class ErrorMessages
{
    /// <summary>Resource key for the non-PDF message.</summary>
    internal const string NotPdfKey = "NotPdf";

    /// <summary>Message shown when a non-PDF file is selected.</summary>
    public static string NotPdf => Localized.Get(NotPdfKey);

    public static string ForKind(ErrorKind kind) => Localized.Get(ResourceKeyForKind(kind));

    /// <summary>
    /// Maps a stable <see cref="ErrorKind"/> token to its resource key. Pure (no resource load), so
    /// the mapping can be unit tested without the WinUI resource runtime.
    /// </summary>
    internal static string ResourceKeyForKind(ErrorKind kind) => kind switch
    {
        ErrorKind.PdfCorrupted => "ErrorPdfCorrupted",
        ErrorKind.PdfPasswordProtected => "ErrorPdfPasswordProtected",
        ErrorKind.PdfNotFound => "ErrorPdfNotFound",
        ErrorKind.PdfInvalidPage => "ErrorPdfInvalidPage",
        ErrorKind.PdfWriteFailed => "ErrorPdfWriteFailed",
        ErrorKind.InvalidParameter => "ErrorInvalidParameter",
        _ => "ErrorInternal",
    };
}
