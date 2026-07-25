namespace TateYoko.Engine;

/// <summary>Converts one PDF into right-to-left two-page spreads.</summary>
public interface IPdfSpreadConverter
{
    /// <summary>
    /// Converts the requested PDF and returns only after a validated output has been atomically
    /// committed. Implementations must not modify the input or expose passwords in diagnostics.
    /// </summary>
    /// <param name="request">The immutable conversion request.</param>
    /// <param name="progress">
    /// Optional per-spread progress. Reports are ordered and run through the supplied
    /// <see cref="IProgress{T}"/> implementation.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancellation checked before opening, between spreads, before validation, and before commit.
    /// </param>
    /// <returns>The actual committed path and input/output page counts.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="PdfSpreadException">
    /// Validation, input, PDF, or output processing failed. Inspect
    /// <see cref="PdfSpreadException.Error"/> instead of displaying the diagnostic message.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was cancelled before commit.
    /// </exception>
    /// <remarks>
    /// The built-in implementation is safe for concurrent calls with distinct output targets. It
    /// serializes PDFsharp work within the process to avoid races in library-global state. Callers
    /// must coordinate requests that use the same explicit replacement target.
    /// </remarks>
    PdfSpreadResult Convert(
        PdfSpreadRequest request,
        IProgress<PdfSpreadProgress>? progress = null,
        CancellationToken cancellationToken = default
    );
}
