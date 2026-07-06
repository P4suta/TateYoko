using TateYoko.Core.Domain;

namespace TateYoko.Core.Tests;

/// <summary>Tests <see cref="PagePairSpec"/>: non-negative index guards, the default half, and value equality of the closed hierarchy.</summary>
public sealed class PagePairSpecTests
{
    public sealed class PairGuards
    {
        [Theory]
        [InlineData(0, 1)]
        [InlineData(0, 0)]
        [InlineData(998, 999)]
        public void AcceptsNonNegativeIndices(int first, int second)
        {
            var pair = new PagePairSpec.Pair(first, second);
            Assert.Equal(first, pair.FirstIndex);
            Assert.Equal(second, pair.SecondIndex);
        }

        [Theory]
        [InlineData(-1, 0)]
        [InlineData(0, -1)]
        [InlineData(-5, -5)]
        public void RejectsNegativeIndices(int first, int second)
        {
            var ex = Assert.Throws<SpreadException>(() => new PagePairSpec.Pair(first, second));
            Assert.Equal(ErrorKind.PdfInvalidPage, ex.Kind);
        }
    }

    public sealed class SingleGuards
    {
        [Fact]
        public void DefaultHalfIsLeading() =>
            Assert.Equal(SpreadHalf.Leading, new PagePairSpec.Single(3).Half);

        [Theory]
        [InlineData(SpreadHalf.Leading)]
        [InlineData(SpreadHalf.Trailing)]
        public void KeepsGivenHalf(SpreadHalf half) =>
            Assert.Equal(half, new PagePairSpec.Single(2, half).Half);

        [Fact]
        public void AcceptsZeroIndex() => Assert.Equal(0, new PagePairSpec.Single(0).PageIndex);

        [Fact]
        public void RejectsNegativeIndex()
        {
            var ex = Assert.Throws<SpreadException>(() => new PagePairSpec.Single(-1));
            Assert.Equal(ErrorKind.PdfInvalidPage, ex.Kind);
        }
    }

    public sealed class Equality
    {
        [Fact]
        public void PairsEqualByBothIndices()
        {
            Assert.Equal(new PagePairSpec.Pair(0, 1), new PagePairSpec.Pair(0, 1));
            Assert.NotEqual(new PagePairSpec.Pair(0, 1), new PagePairSpec.Pair(1, 0));
        }

        [Fact]
        public void SinglesEqualByIndexAndHalf()
        {
            Assert.Equal(new PagePairSpec.Single(0), new PagePairSpec.Single(0, SpreadHalf.Leading));
            Assert.NotEqual(
                new PagePairSpec.Single(0, SpreadHalf.Leading),
                new PagePairSpec.Single(0, SpreadHalf.Trailing));
        }

        [Fact]
        public void PairNeverEqualsSingle() =>
            Assert.NotEqual<PagePairSpec>(new PagePairSpec.Pair(0, 1), new PagePairSpec.Single(0));
    }
}
