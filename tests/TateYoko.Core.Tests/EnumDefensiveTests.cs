using TateYoko.Core.Domain;

namespace TateYoko.Core.Tests;

/// <summary>
/// Defensive behavior for undefined enum values and the stability of the <see cref="ErrorKind"/>
/// token set. These cover the <c>default</c> branches that valid inputs never reach and pin the
/// documented "language-independent, stable token" contract.
/// </summary>
public sealed class EnumDefensiveTests
{
    public sealed class UndefinedFirstPageMode
    {
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(100)]
        public void PaginateRejectsUndefinedModeAsInvalidParameter(int totalPages)
        {
            var ex = Assert.Throws<SpreadException>(() => Pagination.Paginate((FirstPageMode)999, totalPages));
            Assert.Equal(ErrorKind.InvalidParameter, ex.Kind);
        }

        [Fact]
        public void PageCountGuardRunsBeforeTheModeSwitch()
        {
            // A non-positive page count is rejected as PdfInvalidPage even for an undefined mode:
            // the total-pages guard runs before the mode is inspected.
            var ex = Assert.Throws<SpreadException>(() => Pagination.Paginate((FirstPageMode)999, 0));
            Assert.Equal(ErrorKind.PdfInvalidPage, ex.Kind);
        }
    }

    public sealed class UndefinedSpreadHalf
    {
        private readonly SpreadLayoutCalculator _calc = new();

        [Fact]
        public void CalculateSingleTreatsUndefinedHalfAsTheLeftHalf()
        {
            // OnRightHalf only returns true for Leading; any other value lands on the left half,
            // making an undefined half behave exactly like Trailing. This pins that fallback.
            var page = new PageDimension(100f, 200f);
            SpreadLayout undefined = _calc.CalculateSingle(page, (SpreadHalf)999);
            SpreadLayout trailing = _calc.CalculateSingle(page, SpreadHalf.Trailing);
            Assert.Equal(trailing.FirstPosition, undefined.FirstPosition);
            Assert.Null(undefined.SecondPosition);
        }
    }

    public sealed class ErrorKindTokens
    {
        [Fact]
        public void UndefinedValueIsNotDefined() => Assert.False(Enum.IsDefined((ErrorKind)999));

        [Fact]
        public void ExposesTheStableTokenSet()
        {
            // Guards against accidental removal or rename of the stable failure tokens.
            string[] expected =
            [
                "PdfCorrupted",
                "PdfPasswordProtected",
                "PdfNotFound",
                "PdfInvalidPage",
                "PdfWriteFailed",
                "InvalidParameter",
                "Internal",
            ];
            Assert.Equal(expected.OrderBy(n => n), Enum.GetNames<ErrorKind>().OrderBy(n => n));
        }
    }
}
