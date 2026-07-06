using TateYoko.App.Services;
using TateYoko.Core.Domain;

namespace TateYoko.App.Tests;

/// <summary>
/// Tests the pure <see cref="ErrorKind"/>-to-resource-key mapping in <see cref="ErrorMessages"/>.
/// Only the mapping (no resource load) is exercised, so no WinUI resource runtime is required.
/// </summary>
public sealed class ErrorMessagesMappingTests
{
    [Theory]
    [InlineData(ErrorKind.PdfCorrupted, "ErrorPdfCorrupted")]
    [InlineData(ErrorKind.PdfPasswordProtected, "ErrorPdfPasswordProtected")]
    [InlineData(ErrorKind.PdfNotFound, "ErrorPdfNotFound")]
    [InlineData(ErrorKind.PdfInvalidPage, "ErrorPdfInvalidPage")]
    [InlineData(ErrorKind.PdfWriteFailed, "ErrorPdfWriteFailed")]
    [InlineData(ErrorKind.InvalidParameter, "ErrorInvalidParameter")]
    [InlineData(ErrorKind.Internal, "ErrorInternal")]
    public void MapsEachErrorKindToItsResourceKey(ErrorKind kind, string expectedKey) =>
        Assert.Equal(expectedKey, ErrorMessages.ResourceKeyForKind(kind));

    [Fact]
    public void UndefinedErrorKindFallsBackToInternal() =>
        Assert.Equal("ErrorInternal", ErrorMessages.ResourceKeyForKind((ErrorKind)999));

    [Fact]
    public void EveryDefinedErrorKindMapsToANonEmptyKey()
    {
        foreach (ErrorKind kind in Enum.GetValues<ErrorKind>())
        {
            Assert.False(string.IsNullOrWhiteSpace(ErrorMessages.ResourceKeyForKind(kind)));
        }
    }

    [Fact]
    public void EveryNonInternalErrorKindMapsToADistinctKey()
    {
        // Only the Internal fallback key may be shared (the default arm); the rest must be unique.
        ErrorKind[] kinds = Enum.GetValues<ErrorKind>();
        var distinctKeys = kinds.Select(ErrorMessages.ResourceKeyForKind).Distinct().ToList();
        Assert.Equal(kinds.Length, distinctKeys.Count);
    }
}
