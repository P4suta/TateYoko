using TateYoko.App.Services;
using TateYoko.Engine;

namespace TateYoko.App.Tests;

public sealed class ErrorMessagesMappingTests
{
    [Theory]
    [InlineData(PdfSpreadError.InvalidRequest, "ErrorInvalidRequest")]
    [InlineData(PdfSpreadError.InputNotFound, "ErrorInputNotFound")]
    [InlineData(PdfSpreadError.ReadFailed, "ErrorReadFailed")]
    [InlineData(PdfSpreadError.UnsupportedFile, "ErrorUnsupportedFile")]
    [InlineData(PdfSpreadError.PasswordRequired, "ErrorPasswordRequired")]
    [InlineData(PdfSpreadError.InvalidPassword, "ErrorInvalidPassword")]
    [InlineData(PdfSpreadError.CorruptedPdf, "ErrorCorruptedPdf")]
    [InlineData(PdfSpreadError.InvalidPage, "ErrorInvalidPage")]
    [InlineData(PdfSpreadError.WriteFailed, "ErrorWriteFailed")]
    [InlineData(PdfSpreadError.Internal, "ErrorInternal")]
    [InlineData(PdfSpreadError.UnsupportedPdfFeature, "ErrorUnsupportedPdfFeature")]
    public void MapsEveryError(PdfSpreadError error, string expected) =>
        Assert.Equal(expected, ErrorMessages.ResourceKey(error));

    [Fact]
    public void UndefinedErrorFallsBackToInternal() =>
        Assert.Equal("ErrorInternal", ErrorMessages.ResourceKey((PdfSpreadError)int.MaxValue));

    [Fact]
    public void EveryDefinedErrorHasADistinctResource()
    {
        string[] keys = [.. Enum.GetValues<PdfSpreadError>().Select(ErrorMessages.ResourceKey)];

        Assert.Equal(keys.Length, keys.Distinct(StringComparer.Ordinal).Count());
    }
}
