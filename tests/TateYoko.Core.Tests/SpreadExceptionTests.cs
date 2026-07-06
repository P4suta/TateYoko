using TateYoko.Core.Domain;

namespace TateYoko.Core.Tests;

/// <summary>Tests <see cref="SpreadException"/> and its <see cref="SpreadException.Require"/> guard helper.</summary>
public sealed class SpreadExceptionTests
{
    public sealed class Require
    {
        [Fact]
        public void DoesNotThrowWhenConditionHolds() =>
            SpreadException.Require(true, ErrorKind.Internal, "detail");

        [Fact]
        public void ThrowsWithKindAndDetailWhenConditionFails()
        {
            var ex = Assert.Throws<SpreadException>(() =>
                SpreadException.Require(false, ErrorKind.PdfInvalidPage, "totalPages=0"));
            Assert.Equal(ErrorKind.PdfInvalidPage, ex.Kind);
            Assert.Equal("totalPages=0", ex.TechnicalDetail);
        }
    }

    public sealed class Construction
    {
        [Fact]
        public void CarriesKindDetailAndInnerException()
        {
            var inner = new InvalidOperationException("boom");
            var ex = new SpreadException(ErrorKind.PdfCorrupted, "bad.pdf", inner);

            Assert.Equal(ErrorKind.PdfCorrupted, ex.Kind);
            Assert.Equal("bad.pdf", ex.TechnicalDetail);
            Assert.Same(inner, ex.InnerException);
            Assert.Contains("PdfCorrupted", ex.Message, StringComparison.Ordinal);
            Assert.Contains("bad.pdf", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AllowsNullDetail()
        {
            var ex = new SpreadException(ErrorKind.Internal);
            Assert.Null(ex.TechnicalDetail);
            Assert.Null(ex.InnerException);
        }
    }
}
