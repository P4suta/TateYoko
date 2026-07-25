namespace TateYoko.Engine.Tests;

public sealed class PublicContractTests
{
    [Fact]
    public void RequestToStringDoesNotExposePathsOrPassword()
    {
        var request = new PdfSpreadRequest(
            @"C:\private\input.pdf",
            @"C:\private\output.pdf",
            FirstPageMode.Cover,
            OutputCollisionPolicy.ReplaceExisting,
            "super-secret"
        );

        string text = request.ToString();

        Assert.DoesNotContain("private", text, StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret", text, StringComparison.Ordinal);
        Assert.Contains(nameof(FirstPageMode.Cover), text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, 1, 0)]
    [InlineData(1, 1, 1)]
    [InlineData(1, 2, 0.5)]
    public void ProgressCalculatesFraction(int completed, int total, double expected)
    {
        var progress = new PdfSpreadProgress(completed, total);

        Assert.Equal(expected, progress.Fraction);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(2, 1)]
    [InlineData(0, 0)]
    public void ProgressRejectsInvalidCounts(int completed, int total) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfSpreadProgress(completed, total));

    [Fact]
    public void ResultRejectsNonPositiveCounts()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfSpreadResult(@"C:\out.pdf", 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfSpreadResult(@"C:\out.pdf", 1, 0));
    }

    [Fact]
    public void ResultRejectsARelativeCommittedPath() =>
        Assert.Throws<ArgumentException>(() => new PdfSpreadResult("out.pdf", 1, 1));

    [Fact]
    public void PublicEnumValuesAreStable()
    {
        Assert.Equal(0, (int)FirstPageMode.Standard);
        Assert.Equal(1, (int)FirstPageMode.Cover);
        Assert.Equal(2, (int)FirstPageMode.LeadingBlank);
        Assert.Equal(0, (int)OutputCollisionPolicy.CreateUnique);
        Assert.Equal(1, (int)OutputCollisionPolicy.ReplaceExisting);
        Assert.Equal(0, (int)PdfSpreadError.InvalidRequest);
        Assert.Equal(1, (int)PdfSpreadError.InputNotFound);
        Assert.Equal(2, (int)PdfSpreadError.ReadFailed);
        Assert.Equal(3, (int)PdfSpreadError.UnsupportedFile);
        Assert.Equal(4, (int)PdfSpreadError.PasswordRequired);
        Assert.Equal(5, (int)PdfSpreadError.InvalidPassword);
        Assert.Equal(6, (int)PdfSpreadError.CorruptedPdf);
        Assert.Equal(7, (int)PdfSpreadError.InvalidPage);
        Assert.Equal(8, (int)PdfSpreadError.WriteFailed);
        Assert.Equal(9, (int)PdfSpreadError.Internal);
        Assert.Equal(10, (int)PdfSpreadError.UnsupportedPdfFeature);
    }

    [Fact]
    public void ExceptionRejectsUndefinedError() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PdfSpreadException((PdfSpreadError)int.MaxValue)
        );
}
