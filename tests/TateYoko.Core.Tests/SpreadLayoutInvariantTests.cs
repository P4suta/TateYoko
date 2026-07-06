using CsCheck;
using TateYoko.Core.Domain;

namespace TateYoko.Core.Tests;

/// <summary>
/// Geometric invariants of <see cref="SpreadLayoutCalculator"/> that must hold for any page sizes:
/// frame size, in-bounds placement, non-overlap, centering symmetry, and RTL ordering. Complements
/// the example-based <see cref="SpreadLayoutCalculatorTests"/>.
/// </summary>
public sealed class SpreadLayoutInvariantTests
{
    private static readonly Gen<PageDimension> GenDim =
        Gen.Select(Gen.Float[1f, 5000f], Gen.Float[1f, 5000f], (w, h) => new PageDimension(w, h));

    private static Gen<(PageDimension First, PageDimension Second)> GenPair =>
        Gen.Select(GenDim, GenDim, (a, b) => (a, b));

    public sealed class Pairs
    {
        private const float Tol = 0.1f;
        private readonly SpreadLayoutCalculator _calc = new();

        [Fact]
        public void FrameIsTwiceMaxWidthByMaxHeight() =>
            GenPair.Sample(p =>
            {
                SpreadLayout layout = _calc.Calculate(p.First, p.Second);
                float maxW = MathF.Max(p.First.WidthPt, p.Second.WidthPt);
                float maxH = MathF.Max(p.First.HeightPt, p.Second.HeightPt);
                Assert.Equal(maxW * 2f, layout.Spec.WidthPt, Tol);
                Assert.Equal(maxH, layout.Spec.HeightPt, Tol);
            });

        [Fact]
        public void BothPagesStayWithinTheFrame() =>
            GenPair.Sample(p =>
            {
                SpreadLayout layout = _calc.Calculate(p.First, p.Second);
                float fw = layout.Spec.WidthPt;
                float fh = layout.Spec.HeightPt;

                AssertWithin(layout.FirstPosition, p.First, fw, fh);
                AssertWithin(layout.SecondPosition!.Value, p.Second, fw, fh);
            });

        [Fact]
        public void PagesNeverOverlapAcrossTheGutter() =>
            GenPair.Sample(p =>
            {
                SpreadLayout layout = _calc.Calculate(p.First, p.Second);
                float halfWidth = layout.Spec.WidthPt / 2f;

                // Leading page sits entirely in the right half; trailing entirely in the left half.
                Assert.True(layout.FirstPosition.OffsetXPt >= halfWidth - Tol);
                Assert.True(layout.SecondPosition!.Value.OffsetXPt + p.Second.WidthPt <= halfWidth + Tol);
            });

        [Fact]
        public void LeadingPageIsStrictlyRightOfTrailing() =>
            GenPair.Sample(p =>
            {
                SpreadLayout layout = _calc.Calculate(p.First, p.Second);
                Assert.True(layout.FirstPosition.OffsetXPt > layout.SecondPosition!.Value.OffsetXPt);
            });

        [Fact]
        public void EachPageIsCenteredInItsHalfAndVertically() =>
            GenPair.Sample(p =>
            {
                SpreadLayout layout = _calc.Calculate(p.First, p.Second);
                float halfWidth = layout.Spec.WidthPt / 2f;
                float fh = layout.Spec.HeightPt;

                // Leading: centered in the right half [halfWidth, 2*halfWidth].
                Assert.Equal(halfWidth + (halfWidth - p.First.WidthPt) / 2f, layout.FirstPosition.OffsetXPt, Tol);
                Assert.Equal((fh - p.First.HeightPt) / 2f, layout.FirstPosition.OffsetYPt, Tol);

                // Trailing: centered in the left half [0, halfWidth].
                Assert.Equal((halfWidth - p.Second.WidthPt) / 2f, layout.SecondPosition!.Value.OffsetXPt, Tol);
                Assert.Equal((fh - p.Second.HeightPt) / 2f, layout.SecondPosition!.Value.OffsetYPt, Tol);
            });

        [Fact]
        public void FrameIsSymmetricInArgumentOrder() =>
            GenPair.Sample(p =>
            {
                SpreadLayout ab = _calc.Calculate(p.First, p.Second);
                SpreadLayout ba = _calc.Calculate(p.Second, p.First);
                Assert.Equal(ab.Spec.WidthPt, ba.Spec.WidthPt, Tol);
                Assert.Equal(ab.Spec.HeightPt, ba.Spec.HeightPt, Tol);
            });

        private static void AssertWithin(LayoutPosition pos, PageDimension page, float frameW, float frameH)
        {
            Assert.True(pos.OffsetXPt >= -Tol);
            Assert.True(pos.OffsetXPt + page.WidthPt <= frameW + Tol);
            Assert.True(pos.OffsetYPt >= -Tol);
            Assert.True(pos.OffsetYPt + page.HeightPt <= frameH + Tol);
        }
    }

    public sealed class Singles
    {
        private const float Tol = 0.1f;
        private readonly SpreadLayoutCalculator _calc = new();

        [Fact]
        public void FrameIsDoubleWidthAndSecondPositionIsNull() =>
            GenDim.Sample(page =>
            {
                SpreadLayout layout = _calc.CalculateSingle(page, SpreadHalf.Leading);
                Assert.Equal(page.WidthPt * 2f, layout.Spec.WidthPt, Tol);
                Assert.Equal(page.HeightPt, layout.Spec.HeightPt, Tol);
                Assert.Null(layout.SecondPosition);
            });

        [Fact]
        public void LeadingSingleSitsInTheRightHalf() =>
            GenDim.Sample(page =>
            {
                SpreadLayout layout = _calc.CalculateSingle(page, SpreadHalf.Leading);
                float halfWidth = layout.Spec.WidthPt / 2f;
                Assert.True(layout.FirstPosition.OffsetXPt >= halfWidth - Tol);
            });

        [Fact]
        public void TrailingSingleSitsInTheLeftHalf() =>
            GenDim.Sample(page =>
            {
                SpreadLayout layout = _calc.CalculateSingle(page, SpreadHalf.Trailing);
                float halfWidth = layout.Spec.WidthPt / 2f;
                Assert.True(layout.FirstPosition.OffsetXPt + page.WidthPt <= halfWidth + Tol);
            });

        [Fact]
        public void LeadingIsAlwaysRightOfTrailing() =>
            GenDim.Sample(page =>
            {
                SpreadLayout leading = _calc.CalculateSingle(page, SpreadHalf.Leading);
                SpreadLayout trailing = _calc.CalculateSingle(page, SpreadHalf.Trailing);
                Assert.True(leading.FirstPosition.OffsetXPt > trailing.FirstPosition.OffsetXPt);
            });
    }

    /// <summary>
    /// Invariants that tie singles and pairs together: both reserve a full two-page frame, the pair
    /// frame tracks the max height, and a leading single lands exactly where the leading page of an
    /// equal-size pair would.
    /// </summary>
    public sealed class SingleVsPair
    {
        private const float Tol = 0.1f;
        private readonly SpreadLayoutCalculator _calc = new();

        [Fact]
        public void SingleFrameEqualsPairFrameForEqualBounds() =>
            GenDim.Sample(page =>
            {
                // A single reserves the same two-page-wide frame as a pair of that page with itself.
                SpreadLayout single = _calc.CalculateSingle(page, SpreadHalf.Leading);
                SpreadLayout pair = _calc.Calculate(page, page);
                Assert.Equal(pair.Spec.WidthPt, single.Spec.WidthPt, Tol);
                Assert.Equal(pair.Spec.HeightPt, single.Spec.HeightPt, Tol);
            });

        [Fact]
        public void PairFrameHeightEqualsMaxHeight() =>
            GenPair.Sample(p =>
            {
                SpreadLayout layout = _calc.Calculate(p.First, p.Second);
                Assert.Equal(MathF.Max(p.First.HeightPt, p.Second.HeightPt), layout.Spec.HeightPt, Tol);
            });

        [Fact]
        public void LeadingSingleMatchesLeadingPlacementOfAnEqualPair() =>
            GenDim.Sample(page =>
            {
                // For equal-size pages the leading single sits exactly where the pair's leading page sits.
                SpreadLayout single = _calc.CalculateSingle(page, SpreadHalf.Leading);
                SpreadLayout pair = _calc.Calculate(page, page);
                Assert.Equal(pair.FirstPosition.OffsetXPt, single.FirstPosition.OffsetXPt, Tol);
                Assert.Equal(pair.FirstPosition.OffsetYPt, single.FirstPosition.OffsetYPt, Tol);
            });
    }
}
