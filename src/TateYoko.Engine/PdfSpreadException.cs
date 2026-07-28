namespace TateYoko.Engine;

#pragma warning disable CA1064 // The app-only engine has no public CLR contract.
#pragma warning disable CA1032 // Construction must preserve the domain error invariant.
internal sealed class PdfSpreadException : Exception
{
    internal PdfSpreadException(
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

    internal PdfSpreadError Error { get; }

    internal string? TechnicalDetail { get; }
}
#pragma warning restore CA1032
#pragma warning restore CA1064
