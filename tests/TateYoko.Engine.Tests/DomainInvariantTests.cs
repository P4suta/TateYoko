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
    public void PageGroupRejectsInvalidIndices()
    {
        PdfSpreadException negative = Assert.Throws<PdfSpreadException>(() =>
            PageGroup.Pair(-1, 0)
        );
        Assert.Throws<PdfSpreadException>(() => PageGroup.Pair(0, -1));
        Assert.Throws<PdfSpreadException>(() => PageGroup.Pair(2, 2));
        PageGroup reverse = PageGroup.Pair(1, 0);

        Assert.Equal("invalid-page-group", negative.TechnicalDetail);
        Assert.Equal(0, reverse.SecondIndex);
        Assert.Equal(
            "undefined-singleHalf",
            Assert
                .Throws<PdfSpreadException>(() => PageGroup.Single(0, (SpreadHalf)99))
                .TechnicalDetail
        );
    }

    [Fact]
    public void SingleLayoutRejectsUndefinedHalf() =>
        Assert.Throws<PdfSpreadException>(() =>
            SpreadLayout.Single(new PageSize(100, 200), (SpreadHalf)99)
        );

    [Theory]
    [InlineData(0, 5, 3)]
    [InlineData(1, 5, 3)]
    [InlineData(2, 5, 3)]
    [InlineData(1, 2, 2)]
    [InlineData(2, 1, 1)]
    public void PaginationCountIsExact(int mode, int pages, int expected) =>
        Assert.Equal(expected, Pagination.Count((FirstPageMode)mode, pages));

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
    public void PaginationRejectsUndefinedModeInBothOperations()
    {
        PdfSpreadException count = Assert.Throws<PdfSpreadException>(() =>
            Pagination.Count((FirstPageMode)99, 1)
        );
        PdfSpreadException sequence = Assert.Throws<PdfSpreadException>(() =>
            Pagination.Enumerate((FirstPageMode)99, 1).ToArray()
        );

        Assert.Equal("undefined-first-page-mode", count.TechnicalDetail);
        Assert.Equal("undefined-first-page-mode", sequence.TechnicalDetail);
    }

    [Fact]
    public void PaginationRejectsEmptyDocumentsInBothOperations()
    {
        PdfSpreadException count = Assert.Throws<PdfSpreadException>(() =>
            Pagination.Count(FirstPageMode.Standard, 0)
        );
        PdfSpreadException sequence = Assert.Throws<PdfSpreadException>(() =>
            Pagination.Enumerate(FirstPageMode.Standard, 0).ToArray()
        );

        Assert.Equal("empty-document", count.TechnicalDetail);
        Assert.Equal("empty-document", sequence.TechnicalDetail);
    }

    [Fact]
    public void ConversionExceptionRejectsUndefinedError()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PdfSpreadException((PdfSpreadError)int.MaxValue)
        );
    }

    [Fact]
    public void DomainObjectsRejectEveryInvalidConstruction()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new PdfSpreadRequest(null!, @"C:\output.pdf", FirstPageMode.Standard)
        );
        Assert.Throws<ArgumentNullException>(() =>
            new PdfSpreadRequest(@"C:\input.pdf", null!, FirstPageMode.Standard)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfSpreadProgress(-1, 1));
        ArgumentException blankOutput = Assert.Throws<ArgumentException>(() =>
            new PdfSpreadResult(" ", 1, 1)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PdfSpreadResult(@"C:\output.pdf", 0, 1)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PdfSpreadResult(@"C:\output.pdf", 1, 0)
        );
        Assert.Contains("empty", blankOutput.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DomainDiagnosticsRemainActionable()
    {
        PdfSpreadException invalidWidth = Assert.Throws<PdfSpreadException>(() =>
            new PageSize(0, 1)
        );
        var conversion = new PdfSpreadException(PdfSpreadError.InvalidPage);
        ArgumentException relative = Assert.Throws<ArgumentException>(() =>
            new PdfSpreadResult("relative.pdf", 1, 1)
        );

        Assert.Equal("invalid-width", invalidWidth.TechnicalDetail);
        Assert.Contains(
            nameof(PdfSpreadError.InvalidPage),
            conversion.Message,
            StringComparison.Ordinal
        );
        Assert.Contains("fully qualified", relative.Message, StringComparison.Ordinal);
    }
}
