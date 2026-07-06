namespace TateYoko.Core.Domain;

/// <summary>
/// A failure during spread conversion. Carries a stable <see cref="ErrorKind"/> only; the UI layer
/// resolves the user-facing message from the kind.
/// </summary>
public sealed class SpreadException : Exception
{
    public ErrorKind Kind { get; }

    /// <summary>Technical detail for diagnostics, not for display.</summary>
    public string? TechnicalDetail { get; }

    public SpreadException(ErrorKind kind, string? technicalDetail = null, Exception? innerException = null)
        : base($"Error[{kind}]: {technicalDetail}", innerException)
    {
        Kind = kind;
        TechnicalDetail = technicalDetail;
    }

    /// <summary>Throws a <see cref="SpreadException"/> when <paramref name="condition"/> is false.</summary>
    public static void Require(bool condition, ErrorKind kind, string detail)
    {
        if (!condition)
        {
            throw new SpreadException(kind, detail);
        }
    }
}
