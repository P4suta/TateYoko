namespace TateYoko.Engine;

/// <summary>Describes a successfully committed conversion.</summary>
public sealed class PdfSpreadResult
{
    /// <summary>Initializes a successful conversion result.</summary>
    /// <param name="outputPath">Full path that was atomically committed.</param>
    /// <param name="inputPageCount">Positive number of input pages.</param>
    /// <param name="spreadCount">Positive number of output spread pages.</param>
    public PdfSpreadResult(string outputPath, int inputPageCount, int spreadCount)
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

    /// <summary>Gets the full path of the committed output file.</summary>
    public string OutputPath { get; }

    /// <summary>Gets the number of pages in the input PDF.</summary>
    public int InputPageCount { get; }

    /// <summary>Gets the number of pages in the output PDF.</summary>
    public int SpreadCount { get; }
}
