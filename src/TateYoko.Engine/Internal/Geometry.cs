namespace TateYoko.Engine.Internal;

internal enum SpreadHalf
{
    Leading = 0,
    Trailing = 1,
}

internal readonly record struct PageSize
{
    internal PageSize(double width, double height)
    {
        Guard.FinitePositive(width, nameof(width));
        Guard.FinitePositive(height, nameof(height));
        Width = width;
        Height = height;
    }

    internal double Width { get; }

    internal double Height { get; }

    internal static PageSize Max(PageSize first, PageSize second) =>
        new(Math.Max(first.Width, second.Width), Math.Max(first.Height, second.Height));
}

internal readonly record struct Point(double X, double Y);

internal readonly record struct PageLayout(PageSize SpreadSize, Point First, Point? Second);

internal static class SpreadLayout
{
    internal static PageLayout Pair(PageSize first, PageSize second)
    {
        PageSize bounds = PageSize.Max(first, second);
        return new PageLayout(
            new PageSize(bounds.Width * 2, bounds.Height),
            Place(SpreadHalf.Leading, bounds, first),
            Place(SpreadHalf.Trailing, bounds, second)
        );
    }

    internal static PageLayout Single(PageSize page, SpreadHalf half)
    {
        Guard.Defined(half, nameof(half));
        return new PageLayout(
            new PageSize(page.Width * 2, page.Height),
            Place(half, page, page),
            null
        );
    }

    private static Point Place(SpreadHalf half, PageSize bounds, PageSize page)
    {
        double x = (bounds.Width - page.Width) / 2;
        if (half == SpreadHalf.Leading)
        {
            x += bounds.Width;
        }

        return new Point(x, (bounds.Height - page.Height) / 2);
    }
}
