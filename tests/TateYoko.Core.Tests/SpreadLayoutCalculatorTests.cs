using TateYoko.Core.Domain;

namespace TateYoko.Core.Tests;

/// <summary>Tests <see cref="SpreadLayoutCalculator"/> (RTL): frame is two pages wide, centered, leading page on the right half.</summary>
public sealed class SpreadLayoutCalculatorTests
{
    private const float Eps = 0.001f;

    private readonly SpreadLayoutCalculator _calc = new();

    [Fact]
    public void EqualSizedPairPlacesFirstOnRight()
    {
        var dim = new PageDimension(100f, 200f);
        var layout = _calc.Calculate(dim, dim);

        Assert.Equal(200f, layout.Spec.WidthPt, Eps);
        Assert.Equal(200f, layout.Spec.HeightPt, Eps);
        Assert.Equal(100f, layout.FirstPosition.OffsetXPt, Eps);
        Assert.NotNull(layout.SecondPosition);
        Assert.Equal(0f, layout.SecondPosition!.Value.OffsetXPt, Eps);
    }

    [Fact]
    public void VerticalCenteringWhenHeightsDiffer()
    {
        var first = new PageDimension(100f, 200f);
        var second = new PageDimension(100f, 100f);
        var layout = _calc.Calculate(first, second);

        Assert.Equal(200f, layout.Spec.HeightPt, Eps);
        Assert.Equal(0f, layout.FirstPosition.OffsetYPt, Eps);
        Assert.Equal(50f, layout.SecondPosition!.Value.OffsetYPt, Eps);
    }

    [Fact]
    public void HorizontalCenteringWhenWidthsDiffer()
    {
        var first = new PageDimension(80f, 100f);
        var second = new PageDimension(100f, 100f);
        var layout = _calc.Calculate(first, second);

        Assert.Equal(200f, layout.Spec.WidthPt, Eps);
        // first (width 80) centered in the right half [100,200] -> 100 + (100-80)/2 = 110
        Assert.Equal(110f, layout.FirstPosition.OffsetXPt, Eps);
        // second (width 100) in the left half [0,100] -> (100-100)/2 = 0
        Assert.Equal(0f, layout.SecondPosition!.Value.OffsetXPt, Eps);
    }

    [Fact]
    public void PairFirstOnRightOfSecond()
    {
        var dim = new PageDimension(100f, 200f);
        var layout = _calc.Calculate(dim, dim);
        Assert.True(layout.FirstPosition.OffsetXPt > layout.SecondPosition!.Value.OffsetXPt);
    }

    [Fact]
    public void PortraitPair()
    {
        var portrait = new PageDimension(100f, 200f);
        var layout = _calc.Calculate(portrait, portrait);
        Assert.Equal(200f, layout.Spec.WidthPt, Eps);
        Assert.Equal(200f, layout.Spec.HeightPt, Eps);
    }

    [Fact]
    public void LandscapePair()
    {
        var landscape = new PageDimension(200f, 100f);
        var layout = _calc.Calculate(landscape, landscape);
        Assert.Equal(400f, layout.Spec.WidthPt, Eps);
        Assert.Equal(100f, layout.Spec.HeightPt, Eps);
    }

    [Fact]
    public void MixedSizesUseMaxBounds()
    {
        var a = new PageDimension(80f, 100f);
        var b = new PageDimension(120f, 200f);
        var layout = _calc.Calculate(a, b);
        Assert.Equal(240f, layout.Spec.WidthPt, Eps);
        Assert.Equal(200f, layout.Spec.HeightPt, Eps);
    }

    [Fact]
    public void LeadingSinglePlacedOnRightHalf()
    {
        var dim = new PageDimension(100f, 200f);
        var layout = _calc.CalculateSingle(dim, SpreadHalf.Leading);
        Assert.Equal(200f, layout.Spec.WidthPt, Eps);
        Assert.Equal(100f, layout.FirstPosition.OffsetXPt, Eps);
        Assert.Null(layout.SecondPosition);
    }

    [Fact]
    public void TrailingSinglePlacedOnLeftHalf()
    {
        var dim = new PageDimension(100f, 200f);
        var layout = _calc.CalculateSingle(dim, SpreadHalf.Trailing);
        Assert.Equal(200f, layout.Spec.WidthPt, Eps);
        Assert.Equal(0f, layout.FirstPosition.OffsetXPt, Eps);
        Assert.Null(layout.SecondPosition);
    }

    [Fact]
    public void LeadingAndTrailingSinglesSitOnOppositeHalves()
    {
        var dim = new PageDimension(100f, 200f);
        var leading = _calc.CalculateSingle(dim, SpreadHalf.Leading);
        var trailing = _calc.CalculateSingle(dim, SpreadHalf.Trailing);
        Assert.True(leading.FirstPosition.OffsetXPt > trailing.FirstPosition.OffsetXPt);
    }
}
