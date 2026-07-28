namespace TateYoko.Engine;

internal sealed class PdfSpreadRequest
{
    internal PdfSpreadRequest(
        string inputPath,
        string outputPath,
        FirstPageMode firstPageMode,
        string? password = null
    )
    {
        ArgumentNullException.ThrowIfNull(inputPath);
        ArgumentNullException.ThrowIfNull(outputPath);

        InputPath = inputPath;
        OutputPath = outputPath;
        FirstPageMode = firstPageMode;
        Password = password;
    }

    internal string InputPath { get; }

    internal string OutputPath { get; }

    internal FirstPageMode FirstPageMode { get; }

    internal string? Password { get; }

    public override string ToString() =>
        $"{nameof(PdfSpreadRequest)} {{ FirstPageMode = {FirstPageMode} }}";
}
