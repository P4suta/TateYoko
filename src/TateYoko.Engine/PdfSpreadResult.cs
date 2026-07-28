namespace TateYoko.Engine;

internal sealed class PdfSpreadResult
{
    internal PdfSpreadResult(string outputPath, int inputPageCount, int spreadCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        if (!Path.IsPathFullyQualified(outputPath))
        {
            throw new ArgumentException(
                "The committed output path must be fully qualified.",
                nameof(outputPath)
            );
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(inputPageCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(spreadCount);

        OutputPath = Path.GetFullPath(outputPath);
        InputPageCount = inputPageCount;
        SpreadCount = spreadCount;
    }

    internal string OutputPath { get; }

    internal int InputPageCount { get; }

    internal int SpreadCount { get; }
}
