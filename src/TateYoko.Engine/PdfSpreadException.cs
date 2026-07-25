namespace TateYoko.Engine;

/// <summary>A conversion failure carrying a stable error category and non-display diagnostic token.</summary>
public sealed class PdfSpreadException : Exception
{
    /// <summary>Initializes an internal conversion failure.</summary>
    public PdfSpreadException()
        : this(PdfSpreadError.Internal) { }

    /// <summary>Initializes an internal conversion failure with a diagnostic message.</summary>
    /// <param name="message">The diagnostic message. Applications must not display it directly.</param>
    public PdfSpreadException(string? message)
        : base(message)
    {
        Error = PdfSpreadError.Internal;
    }

    /// <summary>Initializes an internal conversion failure with a cause.</summary>
    /// <param name="message">The diagnostic message. Applications must not display it directly.</param>
    /// <param name="innerException">The underlying failure.</param>
    public PdfSpreadException(string? message, Exception? innerException)
        : base(message, innerException)
    {
        Error = PdfSpreadError.Internal;
    }

    /// <summary>Initializes a conversion exception.</summary>
    public PdfSpreadException(
        PdfSpreadError error,
        string? technicalDetail = null,
        Exception? innerException = null
    )
        : base($"PDF spread conversion failed: {error}.", innerException)
    {
        if (!Enum.IsDefined(error))
        {
            throw new ArgumentOutOfRangeException(nameof(error));
        }

        Error = error;
        TechnicalDetail = technicalDetail;
    }

    /// <summary>Gets the stable failure category.</summary>
    public PdfSpreadError Error { get; }

    /// <summary>Gets a non-localized diagnostic token that must not contain user file paths or passwords.</summary>
    public string? TechnicalDetail { get; }
}
