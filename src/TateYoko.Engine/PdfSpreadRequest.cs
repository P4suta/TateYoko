namespace TateYoko.Engine;

/// <summary>Immutable input for one PDF spread conversion.</summary>
public sealed class PdfSpreadRequest
{
    /// <summary>
    /// Initializes a conversion request. Path and enum validation occurs when conversion starts so
    /// a request remains a side-effect-free value.
    /// </summary>
    /// <param name="inputPath">Fully-qualified path to an existing PDF.</param>
    /// <param name="outputPath">Fully-qualified requested output PDF path.</param>
    /// <param name="firstPageMode">How page 1 is placed.</param>
    /// <param name="collisionPolicy">How an existing output name is handled at atomic commit.</param>
    /// <param name="password">Optional input password. It is never included in <see cref="ToString"/>.</param>
    /// <param name="preservePasswordProtection">
    /// Whether a protected input is re-encrypted with <paramref name="password"/>.
    /// This has no effect for an unprotected input.
    /// </param>
    public PdfSpreadRequest(
        string inputPath,
        string outputPath,
        FirstPageMode firstPageMode,
        OutputCollisionPolicy collisionPolicy = OutputCollisionPolicy.CreateUnique,
        string? password = null,
        bool preservePasswordProtection = true
    )
    {
        ArgumentNullException.ThrowIfNull(inputPath);
        ArgumentNullException.ThrowIfNull(outputPath);

        InputPath = inputPath;
        OutputPath = outputPath;
        FirstPageMode = firstPageMode;
        CollisionPolicy = collisionPolicy;
        Password = password;
        PreservePasswordProtection = preservePasswordProtection;
    }

    /// <summary>Gets the fully-qualified input PDF path.</summary>
    public string InputPath { get; }

    /// <summary>Gets the fully-qualified requested output PDF path.</summary>
    public string OutputPath { get; }

    /// <summary>Gets how the first page is placed.</summary>
    public FirstPageMode FirstPageMode { get; }

    /// <summary>Gets the output collision behavior.</summary>
    public OutputCollisionPolicy CollisionPolicy { get; }

    /// <summary>Gets the optional input password. Callers must not persist or log this value.</summary>
    public string? Password { get; }

    /// <summary>Gets whether a protected input should produce a password-protected output.</summary>
    public bool PreservePasswordProtection { get; }

    /// <inheritdoc />
    public override string ToString() =>
        $"{nameof(PdfSpreadRequest)} {{ FirstPageMode = {FirstPageMode}, CollisionPolicy = {CollisionPolicy}, PreservePasswordProtection = {PreservePasswordProtection} }}";
}
