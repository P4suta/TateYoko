namespace TateYoko.Core.Domain;

/// <summary>
/// Pairs a document's pages into the sequence of output spreads.
///
/// <para><see cref="FirstPageMode.Standard"/> pairs from the first page (<c>1·2, 3·4, …</c>).
/// <see cref="FirstPageMode.Cover"/> puts page 1 alone on the leading side, then pairs from page 2
/// (<c>[1], 2·3, …</c>). <see cref="FirstPageMode.LeadingBlank"/> puts page 1 on the trailing side
/// after an implicit blank, then pairs from page 2 (<c>[▢|1], 2·3, …</c>).</para>
/// </summary>
public static class Pagination
{
    /// <summary>Pairs the source pages into the ordered sequence of spreads.</summary>
    /// <param name="mode">How the first page opens.</param>
    /// <param name="totalPages">The source page count (1 or more).</param>
    public static IReadOnlyList<PagePairSpec> Paginate(FirstPageMode mode, int totalPages)
    {
        SpreadException.Require(totalPages > 0, ErrorKind.PdfInvalidPage, $"totalPages={totalPages}");

        var result = new List<PagePairSpec>();
        int start;
        switch (mode)
        {
            case FirstPageMode.Standard:
                start = 0;
                break;
            case FirstPageMode.Cover:
                result.Add(new PagePairSpec.Single(0, SpreadHalf.Leading));
                start = 1;
                break;
            case FirstPageMode.LeadingBlank:
                result.Add(new PagePairSpec.Single(0, SpreadHalf.Trailing));
                start = 1;
                break;
            default:
                throw new SpreadException(ErrorKind.InvalidParameter, $"mode={mode}");
        }

        PairFrom(result, start, totalPages);
        return result.AsReadOnly();
    }

    /// <summary>
    /// Pairs <c>[start, totalPages)</c> into adjacent pages (<c>start·start+1</c>, …). An odd-length
    /// range leaves the last page as a single on the leading side.
    /// </summary>
    private static void PairFrom(List<PagePairSpec> output, int start, int totalPages)
    {
        for (int i = start; i < totalPages; i += 2)
        {
            output.Add(
                i + 1 < totalPages
                    ? new PagePairSpec.Pair(i, i + 1)
                    : new PagePairSpec.Single(i));
        }
    }
}
