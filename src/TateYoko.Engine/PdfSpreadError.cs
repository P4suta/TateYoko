namespace TateYoko.Engine;

/// <summary>A stable, localizable category for a conversion failure.</summary>
public enum PdfSpreadError
{
    /// <summary>The conversion request is malformed or contains an unsupported value.</summary>
    InvalidRequest = 0,

    /// <summary>The input file does not exist.</summary>
    InputNotFound = 1,

    /// <summary>The input exists but could not be opened for reading.</summary>
    ReadFailed = 2,

    /// <summary>The selected input is not a PDF.</summary>
    UnsupportedFile = 3,

    /// <summary>The input requires a password.</summary>
    PasswordRequired = 4,

    /// <summary>The supplied password did not unlock the input.</summary>
    InvalidPassword = 5,

    /// <summary>The input PDF is corrupt or unreadable.</summary>
    CorruptedPdf = 6,

    /// <summary>The input PDF contains no usable pages.</summary>
    InvalidPage = 7,

    /// <summary>The output could not be written or committed.</summary>
    WriteFailed = 8,

    /// <summary>An unexpected conversion invariant was violated.</summary>
    Internal = 9,

    /// <summary>The PDF uses an interactive feature that cannot be preserved safely.</summary>
    UnsupportedPdfFeature = 10,
}
