using NSubstitute;
using TateYoko.Core.Application;
using TateYoko.Core.Domain;
using TateYoko.Core.Ports;

namespace TateYoko.Core.Tests;

/// <summary>
/// Value-equality and immutability of the domain value types that are otherwise exercised only
/// indirectly (through the calculator or the conversion service). These pin the record semantics so
/// a future hand-written Equals/GetHashCode cannot silently drift, and lock the single-vs-pair
/// distinction into <see cref="SpreadLayout"/> equality.
/// </summary>
public sealed class DomainValueEqualityTests
{
    public sealed class SpreadSpecEquality
    {
        [Fact]
        public void EqualWhenComponentsMatch()
        {
            Assert.Equal(new SpreadSpec(400f, 300f), new SpreadSpec(400f, 300f));
            Assert.Equal(new SpreadSpec(400f, 300f).GetHashCode(), new SpreadSpec(400f, 300f).GetHashCode());
        }

        [Theory]
        [InlineData(401f, 300f)]
        [InlineData(400f, 301f)]
        public void DiffersWhenAComponentDiffers(float w, float h) =>
            Assert.NotEqual(new SpreadSpec(400f, 300f), new SpreadSpec(w, h));
    }

    public sealed class LayoutPositionEquality
    {
        [Fact]
        public void EqualWhenOffsetsMatch()
        {
            Assert.Equal(new LayoutPosition(10f, -5f), new LayoutPosition(10f, -5f));
            Assert.Equal(new LayoutPosition(10f, -5f).GetHashCode(), new LayoutPosition(10f, -5f).GetHashCode());
        }

        [Fact]
        public void AllowsNegativeOffsets()
        {
            // Offsets are vertically symmetric and may be negative; construction must not guard.
            var pos = new LayoutPosition(-1f, -2f);
            Assert.Equal(-1f, pos.OffsetXPt);
            Assert.Equal(-2f, pos.OffsetYPt);
        }

        [Theory]
        [InlineData(11f, -5f)]
        [InlineData(10f, -6f)]
        public void DiffersWhenAnOffsetDiffers(float x, float y) =>
            Assert.NotEqual(new LayoutPosition(10f, -5f), new LayoutPosition(x, y));
    }

    public sealed class SpreadLayoutEquality
    {
        private static readonly SpreadSpec Spec = new(400f, 300f);
        private static readonly LayoutPosition First = new(200f, 0f);
        private static readonly LayoutPosition Second = new(0f, 0f);

        [Fact]
        public void EqualWhenAllComponentsMatch() =>
            Assert.Equal(
                new SpreadLayout(Spec, First, Second),
                new SpreadLayout(Spec, First, Second));

        [Fact]
        public void SingleAndPairDifferEvenWithTheSameFirstPosition()
        {
            // SecondPosition == null marks a single; a pair with the same first position must differ.
            var single = new SpreadLayout(Spec, First, null);
            var pair = new SpreadLayout(Spec, First, Second);
            Assert.NotEqual(single, pair);
        }

        [Fact]
        public void TwoSinglesWithTheSameGeometryAreEqual() =>
            Assert.Equal(
                new SpreadLayout(Spec, First, null),
                new SpreadLayout(Spec, First, null));

        [Fact]
        public void WithUpdatesASingleComponentAndLeavesTheRest()
        {
            var layout = new SpreadLayout(Spec, First, Second);
            SpreadLayout moved = layout with { FirstPosition = new LayoutPosition(123f, 0f) };
            Assert.Equal(new LayoutPosition(123f, 0f), moved.FirstPosition);
            Assert.Equal(Second, moved.SecondPosition);
            Assert.Equal(Spec, moved.Spec);
        }
    }

    public sealed class SpreadRequestEquality
    {
        [Fact]
        public void EqualWhenAllFieldsMatch() =>
            Assert.Equal(
                new SpreadRequest("in.pdf", "out.pdf", FirstPageMode.Cover),
                new SpreadRequest("in.pdf", "out.pdf", FirstPageMode.Cover));

        [Theory]
        [InlineData("other.pdf", "out.pdf", FirstPageMode.Cover)]
        [InlineData("in.pdf", "other.pdf", FirstPageMode.Cover)]
        [InlineData("in.pdf", "out.pdf", FirstPageMode.Standard)]
        public void DiffersWhenAFieldDiffers(string input, string output, FirstPageMode mode) =>
            Assert.NotEqual(
                new SpreadRequest("in.pdf", "out.pdf", FirstPageMode.Cover),
                new SpreadRequest(input, output, mode));

        [Fact]
        public void PathComparisonIsOrdinalCaseSensitive() =>
            // Records compare strings with the default (ordinal) comparer, so case matters.
            Assert.NotEqual(
                new SpreadRequest("IN.pdf", "out.pdf", FirstPageMode.Standard),
                new SpreadRequest("in.pdf", "out.pdf", FirstPageMode.Standard));
    }

    public sealed class PagePlacementEquality
    {
        [Fact]
        public void EqualWhenContentReferenceAndPositionMatch()
        {
            var content = Substitute.For<IPageContent>();
            var pos = new LayoutPosition(5f, 6f);
            Assert.Equal(new PagePlacement(content, pos), new PagePlacement(content, pos));
        }

        [Fact]
        public void DiffersWhenContentDiffers()
        {
            var pos = new LayoutPosition(5f, 6f);
            Assert.NotEqual(
                new PagePlacement(Substitute.For<IPageContent>(), pos),
                new PagePlacement(Substitute.For<IPageContent>(), pos));
        }

        [Fact]
        public void DiffersWhenPositionDiffers()
        {
            var content = Substitute.For<IPageContent>();
            Assert.NotEqual(
                new PagePlacement(content, new LayoutPosition(5f, 6f)),
                new PagePlacement(content, new LayoutPosition(5f, 7f)));
        }
    }
}
