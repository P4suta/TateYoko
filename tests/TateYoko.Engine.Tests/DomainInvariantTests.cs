using TateYoko.Engine.Internal;

namespace TateYoko.Engine.Tests;

public sealed class DomainInvariantTests
{
    [Theory]
    [InlineData(double.NaN, 1)]
    [InlineData(double.PositiveInfinity, 1)]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, double.NaN)]
    [InlineData(1, double.NegativeInfinity)]
    public void PageSizeRejectsNonFiniteOrNonPositiveDimensions(double width, double height) =>
        Assert.Throws<PdfSpreadException>(() => new PageSize(width, height));

    [Fact]
    public void PageGroupRejectsSamePageTwice() =>
        Assert.Throws<PdfSpreadException>(() => PageGroup.Pair(2, 2));

    [Fact]
    public void SingleLayoutRejectsUndefinedHalf() =>
        Assert.Throws<PdfSpreadException>(() =>
            SpreadLayout.Single(new PageSize(100, 200), (SpreadHalf)99)
        );

    [Theory]
    [InlineData(FirstPageMode.Standard, 5, 3)]
    [InlineData(FirstPageMode.Cover, 5, 3)]
    [InlineData(FirstPageMode.LeadingBlank, 5, 3)]
    [InlineData(FirstPageMode.Cover, 2, 2)]
    [InlineData(FirstPageMode.LeadingBlank, 1, 1)]
    public void PaginationCountIsExact(FirstPageMode mode, int pages, int expected) =>
        Assert.Equal(expected, Pagination.Count(mode, pages));

    [Fact]
    public void PaginationIsStreaming()
    {
        IEnumerable<PageGroup> sequence = Pagination.Enumerate(FirstPageMode.Standard, 500);

        using IEnumerator<PageGroup> enumerator = sequence.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.Equal(0, enumerator.Current.FirstIndex);
        Assert.Equal(1, enumerator.Current.SecondIndex);
    }

    [Fact]
    public void PaginationRejectsUndefinedMode() =>
        Assert.Throws<PdfSpreadException>(() => Pagination.Count((FirstPageMode)99, 1));
}
