using CsCheck;
using TateYoko.Core.Domain;

namespace TateYoko.Core.Tests;

/// <summary>Tests <see cref="PageDimension"/>: positive-size guard, the <see cref="PageDimension.Max"/> lattice, and value equality.</summary>
public sealed class PageDimensionTests
{
    private const float Eps = 0.001f;

    /// <summary>Arbitrary valid dimensions (both sides strictly positive and finite).</summary>
    private static readonly Gen<PageDimension> GenDim =
        Gen.Select(Gen.Float[0.01f, 100_000f], Gen.Float[0.01f, 100_000f],
            (w, h) => new PageDimension(w, h));

    public sealed class Construction
    {
        [Theory]
        [InlineData(1f, 1f)]
        [InlineData(0.001f, 5000f)]
        [InlineData(612f, 792f)]
        public void AcceptsPositiveSides(float w, float h)
        {
            var dim = new PageDimension(w, h);
            Assert.Equal(w, dim.WidthPt, Eps);
            Assert.Equal(h, dim.HeightPt, Eps);
        }

        [Theory]
        [InlineData(0f, 100f)]
        [InlineData(-1f, 100f)]
        [InlineData(100f, 0f)]
        [InlineData(100f, -1f)]
        [InlineData(float.NaN, 100f)]
        [InlineData(100f, float.NaN)]
        public void RejectsNonPositiveOrNaNSides(float w, float h)
        {
            var ex = Assert.Throws<SpreadException>(() => new PageDimension(w, h));
            Assert.Equal(ErrorKind.InvalidParameter, ex.Kind);
        }
    }

    public sealed class MaxLattice
    {
        [Fact]
        public void TakesComponentWiseMaximum()
        {
            var a = new PageDimension(80f, 300f);
            var b = new PageDimension(120f, 200f);
            var max = PageDimension.Max(a, b);
            Assert.Equal(120f, max.WidthPt, Eps);
            Assert.Equal(300f, max.HeightPt, Eps);
        }

        [Fact]
        public void ComponentWiseMaximumHoldsForAnyPair() =>
            Gen.Select(GenDim, GenDim, (a, b) => (a, b)).Sample(p =>
            {
                var max = PageDimension.Max(p.a, p.b);
                Assert.Equal(MathF.Max(p.a.WidthPt, p.b.WidthPt), max.WidthPt, Eps);
                Assert.Equal(MathF.Max(p.a.HeightPt, p.b.HeightPt), max.HeightPt, Eps);
            });

        [Fact]
        public void IsCommutative() =>
            Gen.Select(GenDim, GenDim, (a, b) => (a, b)).Sample(p =>
                Assert.Equal(PageDimension.Max(p.a, p.b), PageDimension.Max(p.b, p.a)));

        [Fact]
        public void IsIdempotent() =>
            GenDim.Sample(a => Assert.Equal(a, PageDimension.Max(a, a)));

        [Fact]
        public void IsAssociative() =>
            Gen.Select(GenDim, GenDim, GenDim, (a, b, c) => (a, b, c)).Sample(t =>
                Assert.Equal(
                    PageDimension.Max(PageDimension.Max(t.a, t.b), t.c),
                    PageDimension.Max(t.a, PageDimension.Max(t.b, t.c))));

        [Fact]
        public void ResultDominatesBothInputs() =>
            Gen.Select(GenDim, GenDim, (a, b) => (a, b)).Sample(p =>
            {
                var max = PageDimension.Max(p.a, p.b);
                Assert.True(max.WidthPt >= p.a.WidthPt && max.WidthPt >= p.b.WidthPt);
                Assert.True(max.HeightPt >= p.a.HeightPt && max.HeightPt >= p.b.HeightPt);
            });
    }

    public sealed class Equality
    {
        [Fact]
        public void EqualWhenBothSidesMatch()
        {
            Assert.Equal(new PageDimension(100f, 200f), new PageDimension(100f, 200f));
            Assert.Equal(
                new PageDimension(100f, 200f).GetHashCode(),
                new PageDimension(100f, 200f).GetHashCode());
        }

        [Fact]
        public void DiffersWhenASideDiffers()
        {
            Assert.NotEqual(new PageDimension(100f, 200f), new PageDimension(101f, 200f));
            Assert.NotEqual(new PageDimension(100f, 200f), new PageDimension(100f, 201f));
        }
    }
}
