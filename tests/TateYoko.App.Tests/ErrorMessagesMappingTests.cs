using TateYoko.App.Services;
using TateYoko.Engine;

namespace TateYoko.App.Tests;

public sealed class ErrorMessagesMappingTests
{
    [Theory]
    [InlineData(0, "ErrorInvalidRequest")]
    [InlineData(1, "ErrorInputNotFound")]
    [InlineData(2, "ErrorReadFailed")]
    [InlineData(3, "ErrorUnsupportedFile")]
    [InlineData(4, "ErrorPasswordRequired")]
    [InlineData(5, "ErrorInvalidPassword")]
    [InlineData(6, "ErrorCorruptedPdf")]
    [InlineData(7, "ErrorInvalidPage")]
    [InlineData(8, "ErrorWriteFailed")]
    [InlineData(9, "ErrorInternal")]
    [InlineData(10, "ErrorUnsupportedPdfFeature")]
    public void MapsEveryError(int error, string expected) =>
        Assert.Equal(expected, ErrorMessages.ResourceKey((PdfSpreadError)error));

    [Fact]
    public void UndefinedErrorFallsBackToInternal() =>
        Assert.Equal("ErrorInternal", ErrorMessages.ResourceKey((PdfSpreadError)int.MaxValue));

    [Fact]
    public void EveryDefinedErrorHasADistinctResource()
    {
        string[] keys =
        [
            .. Enum.GetValues<PdfSpreadError>().Select(error => ErrorMessages.ResourceKey(error)),
        ];

        Assert.Equal(keys.Length, keys.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData("unsupported-annotation", "ErrorUnsupportedAnnotation")]
    [InlineData("unsupported-interactive-form", "ErrorUnsupportedForm")]
    [InlineData("unsupported-document-permissions", "ErrorUnsupportedForm")]
    [InlineData("unsupported-javascript", "ErrorUnsupportedAction")]
    [InlineData("unsupported-document-open-action", "ErrorUnsupportedAction")]
    [InlineData("unsupported-document-additional-action", "ErrorUnsupportedAction")]
    [InlineData("unsupported-page-additional-action", "ErrorUnsupportedAction")]
    [InlineData("unsupported-presentation-step", "ErrorUnsupportedAction")]
    [InlineData("unsupported-page-transition", "ErrorUnsupportedAction")]
    [InlineData("unsupported-multimedia-rendition", "ErrorUnsupportedAction")]
    [InlineData("unsupported-embedded-file", "ErrorUnsupportedAttachment")]
    [InlineData("unsupported-associated-file", "ErrorUnsupportedAttachment")]
    [InlineData("unsupported-page-associated-file", "ErrorUnsupportedAttachment")]
    [InlineData("unsupported-pdf-collection", "ErrorUnsupportedAttachment")]
    [InlineData("unsupported-optional-content", "ErrorUnsupportedLayer")]
    [InlineData("unsupported-viewport-content", "ErrorUnsupportedLayer")]
    public void MapsUnsupportedProfileDetails(string detail, string expected) =>
        Assert.Equal(
            expected,
            ErrorMessages.ResourceKey(PdfSpreadError.UnsupportedPdfFeature, detail)
        );
}
