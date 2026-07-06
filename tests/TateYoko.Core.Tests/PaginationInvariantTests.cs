using CsCheck;
using TateYoko.Core.Domain;

namespace TateYoko.Core.Tests;

/// <summary>
/// Structural invariants of <see cref="Pagination.Paginate"/> that must hold for every mode and page
/// count: total coverage, reading order, the spread-count formula, pair adjacency, and the
/// Cover/LeadingBlank equivalence. Complements the example-based <see cref="PaginationTests"/>.
/// </summary>
public sealed class PaginationInvariantTests
{
    private static readonly FirstPageMode[] AllModes =
        [FirstPageMode.Standard, FirstPageMode.Cover, FirstPageMode.LeadingBlank];

    /// <summary>The source page indices a spread list references, in reading order (Pair: first then second).</summary>
    private static List<int> Flatten(IEnumerable<PagePairSpec> specs)
    {
        var indices = new List<int>();
        foreach (PagePairSpec spec in specs)
        {
            switch (spec)
            {
                case PagePairSpec.Pair pair:
                    indices.Add(pair.FirstIndex);
                    indices.Add(pair.SecondIndex);
                    break;
                case PagePairSpec.Single single:
                    indices.Add(single.PageIndex);
                    break;
            }
        }

        return indices;
    }

    private static int ExpectedSpreadCount(FirstPageMode mode, int n) => mode switch
    {
        FirstPageMode.Standard => (n + 1) / 2,
        _ => 1 + n / 2, // one opening single, then ceil((n-1)/2) pairs/leftover
    };

    /// <summary>Every source page appears exactly once, in sequential reading order 0..n-1.</summary>
    [Theory]
    [InlineData(FirstPageMode.Standard)]
    [InlineData(FirstPageMode.Cover)]
    [InlineData(FirstPageMode.LeadingBlank)]
    public void CoversEveryPageOnceInOrder(FirstPageMode mode)
    {
        for (int n = 1; n <= 60; n++)
        {
            List<int> flat = Flatten(Pagination.Paginate(mode, n));
            Assert.Equal(Enumerable.Range(0, n), flat);
        }
    }

    [Fact]
    public void CoversEveryPageOnceInOrderForLargeRandomCounts() =>
        Gen.Int[1, 5000].Sample(n =>
        {
            foreach (FirstPageMode mode in AllModes)
            {
                List<int> flat = Flatten(Pagination.Paginate(mode, n));
                Assert.Equal(n, flat.Count);
                for (int i = 0; i < n; i++)
                {
                    Assert.Equal(i, flat[i]);
                }
            }
        });

    [Theory]
    [InlineData(FirstPageMode.Standard)]
    [InlineData(FirstPageMode.Cover)]
    [InlineData(FirstPageMode.LeadingBlank)]
    public void SpreadCountMatchesTheFormula(FirstPageMode mode)
    {
        for (int n = 1; n <= 100; n++)
        {
            Assert.Equal(ExpectedSpreadCount(mode, n), Pagination.Paginate(mode, n).Count);
        }
    }

    [Fact]
    public void EveryPairIsAdjacent() =>
        Gen.Int[1, 2000].Sample(n =>
        {
            foreach (FirstPageMode mode in AllModes)
            {
                foreach (PagePairSpec spec in Pagination.Paginate(mode, n))
                {
                    if (spec is PagePairSpec.Pair pair)
                    {
                        Assert.Equal(pair.FirstIndex + 1, pair.SecondIndex);
                    }
                }
            }
        });

    /// <summary>The two offset modes differ only in page 0's half; everything after is identical.</summary>
    [Fact]
    public void CoverAndLeadingBlankDifferOnlyInFirstHalf() =>
        Gen.Int[1, 2000].Sample(n =>
        {
            IReadOnlyList<PagePairSpec> cover = Pagination.Paginate(FirstPageMode.Cover, n);
            IReadOnlyList<PagePairSpec> blank = Pagination.Paginate(FirstPageMode.LeadingBlank, n);

            Assert.Equal(cover.Count, blank.Count);
            Assert.Equal(cover.Skip(1), blank.Skip(1));
            Assert.Equal(new PagePairSpec.Single(0, SpreadHalf.Leading), cover[0]);
            Assert.Equal(new PagePairSpec.Single(0, SpreadHalf.Trailing), blank[0]);
        });

    /// <summary>Singles appear only as an opening element or the final odd leftover — never in the middle.</summary>
    [Theory]
    [InlineData(FirstPageMode.Standard)]
    [InlineData(FirstPageMode.Cover)]
    [InlineData(FirstPageMode.LeadingBlank)]
    public void SinglesOnlyAtOpeningOrEnd(FirstPageMode mode)
    {
        for (int n = 1; n <= 100; n++)
        {
            IReadOnlyList<PagePairSpec> specs = Pagination.Paginate(mode, n);
            for (int i = 0; i < specs.Count; i++)
            {
                if (specs[i] is PagePairSpec.Single)
                {
                    Assert.True(i == 0 || i == specs.Count - 1, $"mode={mode} n={n} single at {i}");
                }
            }
        }
    }
}
