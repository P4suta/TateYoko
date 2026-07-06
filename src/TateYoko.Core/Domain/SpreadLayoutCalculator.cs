namespace TateYoko.Core.Domain;

/// <summary>
/// Places source pages within a fixed two-page spread frame. Pairs and singles share the same
/// routine, and each page is centered within its half. The app is RTL, so the leading page
/// always goes in the right half.
/// </summary>
public sealed class SpreadLayoutCalculator
{
    /// <summary>
    /// A two-page spread with <paramref name="first"/> on the leading (right) side and
    /// <paramref name="second"/> on the trailing (left) side.
    /// </summary>
    public SpreadLayout Calculate(PageDimension first, PageDimension second)
    {
        PageDimension bounds = PageDimension.Max(first, second);
        return new SpreadLayout(
            SpreadSpecFor(bounds),
            Place(SpreadHalf.Leading, bounds, first),
            Place(SpreadHalf.Trailing, bounds, second));
    }

    /// <summary>A spread with a single page on <paramref name="half"/>; the other half stays blank.</summary>
    public SpreadLayout CalculateSingle(PageDimension page, SpreadHalf half) =>
        new(SpreadSpecFor(page), Place(half, page, page), null);

    /// <summary>The frame is always two pages wide, whether the spread is a single or a pair.</summary>
    private static SpreadSpec SpreadSpecFor(PageDimension bounds) =>
        new(bounds.WidthPt * 2f, bounds.HeightPt);

    /// <summary>
    /// Centers <paramref name="page"/> within one half (each half is <c>bounds.WidthPt</c> wide) and
    /// shifts it to the requested half. Also centers vertically against <paramref name="bounds"/>.
    /// </summary>
    private static LayoutPosition Place(SpreadHalf half, PageDimension bounds, PageDimension page)
    {
        float halfWidth = bounds.WidthPt;
        float x = (halfWidth - page.WidthPt) / 2f;
        if (OnRightHalf(half))
        {
            x += halfWidth;
        }

        float y = (bounds.HeightPt - page.HeightPt) / 2f;
        return new LayoutPosition(x, y);
    }

    /// <summary>RTL: the right half is the leading (reading-start) side.</summary>
    private static bool OnRightHalf(SpreadHalf half) => half == SpreadHalf.Leading;
}
