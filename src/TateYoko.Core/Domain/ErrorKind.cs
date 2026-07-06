namespace TateYoko.Core.Domain;

/// <summary>
/// Language-independent, stable token for a failure cause. Each surface (UI) resolves the
/// display text from this token; the domain carries no message body.
/// </summary>
public enum ErrorKind
{
    /// <summary>The PDF is corrupted or unreadable.</summary>
    PdfCorrupted,

    /// <summary>The PDF is password-protected.</summary>
    PdfPasswordProtected,

    /// <summary>The input file does not exist.</summary>
    PdfNotFound,

    /// <summary>Invalid page specification (e.g. page count is zero or negative).</summary>
    PdfInvalidPage,

    /// <summary>The output could not be written.</summary>
    PdfWriteFailed,

    /// <summary>A parameter is invalid (e.g. out-of-range dimensions).</summary>
    InvalidParameter,

    /// <summary>An unexpected internal error.</summary>
    Internal,
}
