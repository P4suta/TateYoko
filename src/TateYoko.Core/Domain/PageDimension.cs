namespace TateYoko.Core.Domain;

/// <summary>A source page's display size in points (1/72 inch); both sides are positive.</summary>
public readonly record struct PageDimension
{
    public float WidthPt { get; }

    public float HeightPt { get; }

    public PageDimension(float widthPt, float heightPt)
    {
        SpreadException.Require(widthPt > 0, ErrorKind.InvalidParameter, $"widthPt={widthPt}");
        SpreadException.Require(heightPt > 0, ErrorKind.InvalidParameter, $"heightPt={heightPt}");
        WidthPt = widthPt;
        HeightPt = heightPt;
    }

    /// <summary>The bounding size, taking the larger of each side.</summary>
    public static PageDimension Max(PageDimension a, PageDimension b) =>
        new(MathF.Max(a.WidthPt, b.WidthPt), MathF.Max(a.HeightPt, b.HeightPt));
}
