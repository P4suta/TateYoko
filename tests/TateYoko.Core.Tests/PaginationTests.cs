using TateYoko.Core.Domain;

namespace TateYoko.Core.Tests;

/// <summary>Tests <see cref="Pagination.Paginate"/>: each mode's opening element and how even/odd page counts pair.</summary>
public sealed class PaginationTests
{
    public sealed class Standard
    {
        [Fact]
        public void SinglePageProducesOneSingle() =>
            Assert.Equal(
                [new PagePairSpec.Single(0)],
                Pagination.Paginate(FirstPageMode.Standard, 1));

        [Fact]
        public void TwoPagesProduceOnePair() =>
            Assert.Equal(
                [new PagePairSpec.Pair(0, 1)],
                Pagination.Paginate(FirstPageMode.Standard, 2));

        [Fact]
        public void EvenPagesAllPaired() =>
            Assert.Equal(
                [new PagePairSpec.Pair(0, 1), new PagePairSpec.Pair(2, 3)],
                Pagination.Paginate(FirstPageMode.Standard, 4));

        [Fact]
        public void OddPagesEndWithSingle() =>
            Assert.Equal(
                [
                    new PagePairSpec.Pair(0, 1),
                    new PagePairSpec.Pair(2, 3),
                    new PagePairSpec.Single(4),
                ],
                Pagination.Paginate(FirstPageMode.Standard, 5));

        [Fact]
        public void LargeInputHandled()
        {
            var result = Pagination.Paginate(FirstPageMode.Standard, 1000);
            Assert.Equal(500, result.Count);
            Assert.Equal(new PagePairSpec.Pair(0, 1), result[0]);
            Assert.Equal(new PagePairSpec.Pair(998, 999), result[499]);
        }
    }

    public sealed class Cover
    {
        [Fact]
        public void SinglePageProducesOnlyCover() =>
            Assert.Equal(
                [new PagePairSpec.Single(0)],
                Pagination.Paginate(FirstPageMode.Cover, 1));

        [Fact]
        public void TwoPagesProduceCoverPlusSingle() =>
            Assert.Equal(
                [new PagePairSpec.Single(0), new PagePairSpec.Single(1)],
                Pagination.Paginate(FirstPageMode.Cover, 2));

        [Fact]
        public void FivePagesProduceCoverThenPaired() =>
            Assert.Equal(
                [
                    new PagePairSpec.Single(0),
                    new PagePairSpec.Pair(1, 2),
                    new PagePairSpec.Pair(3, 4),
                ],
                Pagination.Paginate(FirstPageMode.Cover, 5));

        [Fact]
        public void SixPagesProduceCoverThenPairedThenSingle() =>
            Assert.Equal(
                [
                    new PagePairSpec.Single(0),
                    new PagePairSpec.Pair(1, 2),
                    new PagePairSpec.Pair(3, 4),
                    new PagePairSpec.Single(5),
                ],
                Pagination.Paginate(FirstPageMode.Cover, 6));

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(6)]
        [InlineData(99)]
        public void CoverIsAlwaysFirst(int pages) =>
            Assert.Equal(
                new PagePairSpec.Single(0, SpreadHalf.Leading),
                Pagination.Paginate(FirstPageMode.Cover, pages)[0]);
    }

    public sealed class LeadingBlank
    {
        [Fact]
        public void SinglePageProducesTrailingSingle() =>
            Assert.Equal(
                [new PagePairSpec.Single(0, SpreadHalf.Trailing)],
                Pagination.Paginate(FirstPageMode.LeadingBlank, 1));

        [Fact]
        public void TwoPagesProduceTrailingThenLeadingSingle() =>
            Assert.Equal(
                [
                    new PagePairSpec.Single(0, SpreadHalf.Trailing),
                    new PagePairSpec.Single(1, SpreadHalf.Leading),
                ],
                Pagination.Paginate(FirstPageMode.LeadingBlank, 2));

        [Fact]
        public void SixPagesLeadWithBlankThenPairedThenTrailingLeftover() =>
            Assert.Equal(
                [
                    new PagePairSpec.Single(0, SpreadHalf.Trailing),
                    new PagePairSpec.Pair(1, 2),
                    new PagePairSpec.Pair(3, 4),
                    new PagePairSpec.Single(5, SpreadHalf.Leading),
                ],
                Pagination.Paginate(FirstPageMode.LeadingBlank, 6));

        /// <summary>The two offset modes share the same grouping and differ only in page 0's half.</summary>
        [Fact]
        public void MirrorsCoverExceptForPageZerosHalf()
        {
            var leadingBlank = Pagination.Paginate(FirstPageMode.LeadingBlank, 7);
            var cover = Pagination.Paginate(FirstPageMode.Cover, 7);

            Assert.Equal(leadingBlank.Skip(1), cover.Skip(1));
            Assert.Equal(new PagePairSpec.Single(0, SpreadHalf.Trailing), leadingBlank[0]);
            Assert.Equal(new PagePairSpec.Single(0, SpreadHalf.Leading), cover[0]);
        }
    }

    public sealed class ModeSelection
    {
        [Fact]
        public void StandardOpensWithAPair() =>
            Assert.Equal(
                new PagePairSpec.Pair(0, 1),
                Pagination.Paginate(FirstPageMode.Standard, 4)[0]);

        [Fact]
        public void CoverOpensWithALeadingSingle() =>
            Assert.Equal(
                new PagePairSpec.Single(0, SpreadHalf.Leading),
                Pagination.Paginate(FirstPageMode.Cover, 4)[0]);

        [Fact]
        public void LeadingBlankOpensWithATrailingSingle() =>
            Assert.Equal(
                new PagePairSpec.Single(0, SpreadHalf.Trailing),
                Pagination.Paginate(FirstPageMode.LeadingBlank, 4)[0]);
    }

    public sealed class Guards
    {
        [Theory]
        [InlineData(FirstPageMode.Standard)]
        [InlineData(FirstPageMode.Cover)]
        [InlineData(FirstPageMode.LeadingBlank)]
        public void ZeroRejected(FirstPageMode mode)
        {
            var ex = Assert.Throws<SpreadException>(() => Pagination.Paginate(mode, 0));
            Assert.Equal(ErrorKind.PdfInvalidPage, ex.Kind);
        }

        [Theory]
        [InlineData(FirstPageMode.Standard)]
        [InlineData(FirstPageMode.Cover)]
        [InlineData(FirstPageMode.LeadingBlank)]
        public void NegativeRejected(FirstPageMode mode) =>
            Assert.Throws<SpreadException>(() => Pagination.Paginate(mode, -5));
    }
}
