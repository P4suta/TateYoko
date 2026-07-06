namespace TateYoko.Core.Domain;

/// <summary>
/// One unit of pairing: the two pages side by side in a spread, or a single page on one half.
/// A closed hierarchy — the private constructor limits subtypes to the two nested records.
/// </summary>
public abstract record PagePairSpec
{
    private PagePairSpec()
    {
    }

    private static int RequireNonNegative(int index, string name)
    {
        SpreadException.Require(index >= 0, ErrorKind.PdfInvalidPage, $"{name}={index}");
        return index;
    }

    /// <summary>Two consecutive pages sharing one spread (zero-based indices).</summary>
    public sealed record Pair(int FirstIndex, int SecondIndex) : PagePairSpec
    {
        public int FirstIndex { get; } = RequireNonNegative(FirstIndex, nameof(FirstIndex));

        public int SecondIndex { get; } = RequireNonNegative(SecondIndex, nameof(SecondIndex));
    }

    /// <summary>A single page filling one half (a cover or a trailing leftover page).</summary>
    public sealed record Single(int PageIndex, SpreadHalf Half) : PagePairSpec
    {
        public int PageIndex { get; } = RequireNonNegative(PageIndex, nameof(PageIndex));

        /// <summary>A single page on the leading side.</summary>
        public Single(int pageIndex)
            : this(pageIndex, SpreadHalf.Leading)
        {
        }
    }
}
