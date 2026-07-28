namespace TateYoko.Engine;

internal sealed class PdfSpreadProgress
{
    internal PdfSpreadProgress(int completedSpreads, int totalSpreads)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(totalSpreads);
        ArgumentOutOfRangeException.ThrowIfNegative(completedSpreads);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(completedSpreads, totalSpreads);

        CompletedSpreads = completedSpreads;
        TotalSpreads = totalSpreads;
    }

    internal int CompletedSpreads { get; }

    internal int TotalSpreads { get; }

    internal double Fraction => (double)CompletedSpreads / TotalSpreads;
}
