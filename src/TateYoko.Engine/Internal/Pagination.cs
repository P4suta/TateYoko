namespace TateYoko.Engine.Internal;

internal readonly record struct PageGroup
{
    private PageGroup(int firstIndex, int? secondIndex, SpreadHalf singleHalf)
    {
        if (firstIndex < 0 || secondIndex < 0 || secondIndex == firstIndex)
        {
            throw new PdfSpreadException(PdfSpreadError.InvalidPage, "invalid-page-group");
        }

        Guard.Defined(singleHalf, nameof(singleHalf));
        FirstIndex = firstIndex;
        SecondIndex = secondIndex;
        SingleHalf = singleHalf;
    }

    internal int FirstIndex { get; }

    internal int? SecondIndex { get; }

    internal SpreadHalf SingleHalf { get; }

    internal static PageGroup Pair(int firstIndex, int secondIndex) =>
        new(firstIndex, secondIndex, SpreadHalf.Leading);

    internal static PageGroup Single(int pageIndex, SpreadHalf half) => new(pageIndex, null, half);
}

internal static class Pagination
{
    internal static int Count(FirstPageMode mode, int totalPages)
    {
        Validate(mode, totalPages);
        return mode == FirstPageMode.Standard ? (totalPages + 1) / 2 : 1 + (totalPages / 2);
    }

    internal static IEnumerable<PageGroup> Enumerate(FirstPageMode mode, int totalPages)
    {
        Validate(mode, totalPages);

        int start;
        switch (mode)
        {
            case FirstPageMode.Standard:
                start = 0;
                break;
            case FirstPageMode.Cover:
                yield return PageGroup.Single(0, SpreadHalf.Leading);
                start = 1;
                break;
            case FirstPageMode.LeadingBlank:
                yield return PageGroup.Single(0, SpreadHalf.Trailing);
                start = 1;
                break;
            default:
                throw new PdfSpreadException(
                    PdfSpreadError.InvalidRequest,
                    "undefined-first-page-mode"
                );
        }

        for (int pageIndex = start; pageIndex < totalPages; pageIndex += 2)
        {
            yield return pageIndex + 1 < totalPages
                ? PageGroup.Pair(pageIndex, pageIndex + 1)
                : PageGroup.Single(pageIndex, SpreadHalf.Leading);
        }
    }

    private static void Validate(FirstPageMode mode, int totalPages)
    {
        Guard.Defined(mode, nameof(mode));
        if (totalPages <= 0)
        {
            throw new PdfSpreadException(PdfSpreadError.InvalidPage, "empty-document");
        }
    }
}
