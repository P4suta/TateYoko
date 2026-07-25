namespace TateYoko.Engine;

/// <summary>Reports committed in-memory spread pages during conversion.</summary>
public sealed class PdfSpreadProgress
{
    /// <summary>Initializes a progress value.</summary>
    /// <param name="completedSpreads">Completed spread count, from zero through the total.</param>
    /// <param name="totalSpreads">Positive total spread count.</param>
    public PdfSpreadProgress(int completedSpreads, int totalSpreads)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(totalSpreads);
        ArgumentOutOfRangeException.ThrowIfNegative(completedSpreads);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(completedSpreads, totalSpreads);

        CompletedSpreads = completedSpreads;
        TotalSpreads = totalSpreads;
    }

    /// <summary>Gets the number of spreads rendered so far.</summary>
    public int CompletedSpreads { get; }

    /// <summary>Gets the total number of spreads that will be rendered.</summary>
    public int TotalSpreads { get; }

    /// <summary>Gets completion as a value from 0 through 1.</summary>
    public double Fraction => (double)CompletedSpreads / TotalSpreads;
}
