using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.Annotations;
using TateYoko.Engine.Internal;

namespace TateYoko.Engine.Tests;

public sealed class MutationBoundaryTests
{
    [Fact]
    public void PublicValueObjectsEnforceAndExposeTheirWholeContract()
    {
        ArgumentNullException inputError = Assert.Throws<ArgumentNullException>(() =>
            new PdfSpreadRequest(
                null!,
                @"C:\output.pdf",
                FirstPageMode.Standard,
                OutputCollisionPolicy.CreateUnique
            )
        );
        ArgumentNullException outputError = Assert.Throws<ArgumentNullException>(() =>
            new PdfSpreadRequest(
                @"C:\input.pdf",
                null!,
                FirstPageMode.Standard,
                OutputCollisionPolicy.CreateUnique
            )
        );
        ArgumentException resultError = Assert.Throws<ArgumentException>(() =>
            new PdfSpreadResult("output.pdf", 1, 1)
        );
        ArgumentNullException nullResultError = Assert.Throws<ArgumentNullException>(() =>
            new PdfSpreadResult(null!, 1, 1)
        );
        var conversionError = new PdfSpreadException(PdfSpreadError.InvalidPage);

        Assert.Equal("inputPath", inputError.ParamName);
        Assert.Equal("outputPath", outputError.ParamName);
        Assert.Equal("outputPath", resultError.ParamName);
        Assert.Equal("outputPath", nullResultError.ParamName);
        Assert.StartsWith(
            "The committed output path must be fully qualified.",
            resultError.Message,
            StringComparison.Ordinal
        );
        Assert.Equal("PDF spread conversion failed: InvalidPage.", conversionError.Message);
    }

    [Fact]
    public void PageGroupsAcceptPageZeroAsTheDistinctSecondPage()
    {
        PageGroup group = PageGroup.Pair(1, 0);

        Assert.Equal(1, group.FirstIndex);
        Assert.Equal(0, group.SecondIndex);
    }

    [Fact]
    public void SinglePageGroupsRejectUndefinedHalves()
    {
        PdfSpreadException exception = Assert.Throws<PdfSpreadException>(() =>
            PageGroup.Single(0, (SpreadHalf)99)
        );

        Assert.Equal(PdfSpreadError.InvalidRequest, exception.Error);
        Assert.Equal("undefined-singleHalf", exception.TechnicalDetail);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(2, 2)]
    public void PageGroupsRejectEveryInvalidIndexRelationship(int firstIndex, int secondIndex)
    {
        PdfSpreadException exception = Assert.Throws<PdfSpreadException>(() =>
            PageGroup.Pair(firstIndex, secondIndex)
        );

        Assert.Equal(PdfSpreadError.InvalidPage, exception.Error);
        Assert.Equal("invalid-page-group", exception.TechnicalDetail);
    }

    [Fact]
    public void InternalGuardsPreserveDiagnosticTokens()
    {
        PdfSpreadException enumError = Assert.Throws<PdfSpreadException>(() =>
            Guard.Defined((FirstPageMode)99, "mode")
        );
        PdfSpreadException widthError = Assert.Throws<PdfSpreadException>(() =>
            new PageSize(double.NaN, 1)
        );
        PdfSpreadException heightError = Assert.Throws<PdfSpreadException>(() =>
            new PageSize(1, 0)
        );
        PdfSpreadException halfError = Assert.Throws<PdfSpreadException>(() =>
            SpreadLayout.Single(new PageSize(1, 1), (SpreadHalf)99)
        );

        Assert.Equal("undefined-mode", enumError.TechnicalDetail);
        Assert.Equal("invalid-width", widthError.TechnicalDetail);
        Assert.Equal("invalid-height", heightError.TechnicalDetail);
        Assert.Equal("undefined-half", halfError.TechnicalDetail);
    }

    [Fact]
    public void AtomicOutputRejectsUndefinedPoliciesBeforeTouchingFiles()
    {
        using var directory = new TempDirectory();
        string temporary = directory.File("temporary.pdf");
        string output = directory.File("output.pdf");
        File.WriteAllText(temporary, "content");

        PdfSpreadException exception = Assert.Throws<PdfSpreadException>(() =>
            AtomicOutput.Commit(temporary, output, (OutputCollisionPolicy)99)
        );

        Assert.Equal(PdfSpreadError.InvalidRequest, exception.Error);
        Assert.Equal("undefined-collisionPolicy", exception.TechnicalDetail);
        Assert.True(File.Exists(temporary));
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void AtomicOutputCandidateSpaceHasAnExactFailClosedBound()
    {
        string requested = Path.Combine(Path.GetTempPath(), "book.pdf");

        string[] candidates = AtomicOutput.EnumerateCandidates(requested).ToArray();

        Assert.Equal(10_000, candidates.Length);
        Assert.Equal(requested, candidates[0]);
        Assert.Equal(Path.Combine(Path.GetTempPath(), "book (2).pdf"), candidates[1]);
        Assert.Equal(Path.Combine(Path.GetTempPath(), "book (10000).pdf"), candidates[^1]);
        Assert.Equal(candidates.Length, candidates.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void PdfSourceValidatesEveryCopyMethodArgument()
    {
        using var directory = new TempDirectory();
        string input = directory.File("features.pdf");
        CreateTwoPageFeaturePdf(input);
        using PdfSource source = PdfSource.Open(input, password: null);
        using var destination = new PdfDocument();
        PageProjection?[] complete = CreateCompleteProjectionMap(source, destination);

        Assert.Equal(
            "destination",
            Assert.Throws<ArgumentNullException>(() => source.CopyMetadataTo(null!)).ParamName
        );
        Assert.Equal(
            "destination",
            Assert
                .Throws<ArgumentNullException>(() =>
                    source.CopyNamedDestinationsTo(null!, complete)
                )
                .ParamName
        );
        Assert.Equal(
            "pageProjections",
            Assert
                .Throws<ArgumentNullException>(() =>
                    source.CopyNamedDestinationsTo(destination, null!)
                )
                .ParamName
        );
        Assert.Equal(
            "destination",
            Assert
                .Throws<ArgumentNullException>(() => source.CopyOutlinesTo(null!, complete))
                .ParamName
        );
        Assert.Equal(
            "pageProjections",
            Assert
                .Throws<ArgumentNullException>(() => source.CopyOutlinesTo(destination, null!))
                .ParamName
        );
        Assert.Equal(
            "destination",
            Assert
                .Throws<ArgumentNullException>(() =>
                    source.CopyAnnotationsTo(null!, complete, CancellationToken.None)
                )
                .ParamName
        );
        Assert.Equal(
            "pageProjections",
            Assert
                .Throws<ArgumentNullException>(() =>
                    source.CopyAnnotationsTo(destination, null!, CancellationToken.None)
                )
                .ParamName
        );
    }

    [Fact]
    public void PdfSourceRejectsPartiallyPopulatedProjectionMaps()
    {
        using var directory = new TempDirectory();
        string input = directory.File("features.pdf");
        CreateTwoPageFeaturePdf(input);
        using PdfSource source = PdfSource.Open(input, password: null);
        using var destination = new PdfDocument();
        PageProjection first = CreateProjection(source, destination, 0);
        PageProjection?[] partial = [first, null];

        AssertDetail(
            "destination-page-map-incomplete",
            () => source.CopyNamedDestinationsTo(destination, partial)
        );
        AssertDetail(
            "outline-page-map-incomplete",
            () => source.CopyOutlinesTo(destination, partial)
        );
        AssertDetail(
            "annotation-page-map-incomplete",
            () => source.CopyAnnotationsTo(destination, partial, CancellationToken.None)
        );
    }

    [Fact]
    public void PdfSourceRejectsProjectionMapsWithTheWrongCountEvenWhenPopulated()
    {
        using var directory = new TempDirectory();
        string input = directory.File("features.pdf");
        CreateTwoPageFeaturePdf(input);
        using PdfSource source = PdfSource.Open(input, password: null);
        using var destination = new PdfDocument();
        PageProjection?[] shortMap = [CreateProjection(source, destination, 0)];

        AssertDetail(
            "destination-page-map-incomplete",
            () => source.CopyNamedDestinationsTo(destination, shortMap)
        );
        AssertDetail(
            "outline-page-map-incomplete",
            () => source.CopyOutlinesTo(destination, shortMap)
        );
        AssertDetail(
            "annotation-page-map-incomplete",
            () => source.CopyAnnotationsTo(destination, shortMap, CancellationToken.None)
        );
    }

    [Fact]
    public void PdfSourceRejectsTheExclusiveUpperPageBound()
    {
        using var directory = new TempDirectory();
        string input = directory.File("input.pdf");
        SamplePdf.Create(input, (200, 300, 0));
        using PdfSource source = PdfSource.Open(input, password: null);

        AssertDetail("page-index-out-of-range", () => source.GetPageSize(source.PageCount));
    }

    [Theory]
    [InlineData("0 0 0 300")]
    [InlineData("0 0 200 0")]
    public void PdfSourceFallsBackWhenEitherCropBoxDimensionIsEmpty(string cropBox)
    {
        using var directory = new TempDirectory();
        string input = directory.File("crop.pdf");
        SamplePdf.CreateWithRawCropBox(input, cropBox);
        using PdfSource source = PdfSource.Open(input, password: null);
        using var destination = new PdfDocument();
        PdfPage destinationPage = destination.AddPage();
        destinationPage.Width = XUnit.FromPoint(400);
        destinationPage.Height = XUnit.FromPoint(300);

        PageSize size = source.GetPageSize(0);
        PageProjection projection = source.CreateProjection(
            0,
            destinationPage,
            new Point(0, 0),
            size
        );

        Assert.Equal(200, size.Width);
        Assert.Equal(300, size.Height);
        Assert.Equal(200, projection.SourceBox.Width);
        Assert.Equal(300, projection.SourceBox.Height);
    }

    [Fact]
    public void PdfSourceAcceptsSupportedCatalogVersionAndMissingViewerDirection()
    {
        using var directory = new TempDirectory();
        string input = directory.File("settings.pdf");
        SamplePdf.CreateCustomized(
            input,
            (document, _) =>
            {
                document.Internals.Catalog.Elements.SetName("/Version", "/2.0");
                document.ViewerPreferences.HideToolbar = true;
            }
        );
        using PdfSource source = PdfSource.Open(input, password: null);
        using var destination = new PdfDocument();

        source.CopyMetadataTo(destination);

        Assert.True(destination.ViewerPreferences.HideToolbar);
        Assert.Null(destination.ViewerPreferences.Direction);
    }

    [Fact]
    public void PdfSourceDisposeReleasesItsFileAndIsIdempotent()
    {
        using var directory = new TempDirectory();
        string input = directory.File("input.pdf");
        string moved = directory.File("moved.pdf");
        SamplePdf.Create(input, (200, 300, 0));
        PdfSource source = PdfSource.Open(input, password: null);

        source.Dispose();
        source.Dispose();
        File.Move(input, moved);

        Assert.False(File.Exists(input));
        Assert.True(File.Exists(moved));
    }

    [Fact]
    public void ConverterValidationPreservesSpecificFailClosedDiagnostics()
    {
        using var directory = new TempDirectory();
        string input = directory.File("input.pdf");
        string missing = directory.File("missing.pdf");
        string output = directory.File("output.pdf");
        SamplePdf.Create(input, (200, 300, 0));
        var converter = new PdfSpreadConverter();

        PdfSpreadException samePath = Assert.Throws<PdfSpreadException>(() =>
            converter.Convert(
                new PdfSpreadRequest(
                    input,
                    input,
                    FirstPageMode.Standard,
                    OutputCollisionPolicy.CreateUnique
                ),
                cancellationToken: TestContext.Current.CancellationToken
            )
        );
        PdfSpreadException missingInput = Assert.Throws<PdfSpreadException>(() =>
            converter.Convert(
                new PdfSpreadRequest(
                    missing,
                    output,
                    FirstPageMode.Standard,
                    OutputCollisionPolicy.CreateUnique
                ),
                cancellationToken: TestContext.Current.CancellationToken
            )
        );
        PdfSpreadException invalidPolicy = Assert.Throws<PdfSpreadException>(() =>
            converter.Convert(
                new PdfSpreadRequest(
                    input,
                    output,
                    FirstPageMode.Standard,
                    (OutputCollisionPolicy)99
                ),
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

        Assert.Equal("input-equals-output", samePath.TechnicalDetail);
        Assert.Null(samePath.InnerException);
        Assert.Equal("input-not-found", missingInput.TechnicalDetail);
        Assert.Null(missingInput.InnerException);
        Assert.Equal("undefined-CollisionPolicy", invalidPolicy.TechnicalDetail);
        Assert.Null(invalidPolicy.InnerException);
    }

    private static PageProjection?[] CreateCompleteProjectionMap(
        PdfSource source,
        PdfDocument destination
    ) =>
        Enumerable
            .Range(0, source.PageCount)
            .Select(index => (PageProjection?)CreateProjection(source, destination, index))
            .ToArray();

    private static PageProjection CreateProjection(
        PdfSource source,
        PdfDocument destination,
        int sourceIndex
    )
    {
        PageSize size = source.GetPageSize(sourceIndex);
        PdfPage page = destination.AddPage();
        page.Width = XUnit.FromPoint(size.Width * 2);
        page.Height = XUnit.FromPoint(size.Height);
        return source.CreateProjection(sourceIndex, page, new Point(0, 0), size);
    }

    private static void CreateTwoPageFeaturePdf(string path)
    {
        using var document = new PdfDocument();
        PdfPage first = document.AddPage();
        first.Width = XUnit.FromPoint(200);
        first.Height = XUnit.FromPoint(300);
        PdfPage second = document.AddPage();
        second.Width = XUnit.FromPoint(200);
        second.Height = XUnit.FromPoint(300);
        document.AddNamedDestination("first", 1, PdfNamedDestinationParameters.CreateFit());
        document.Outlines.Add("First", first);
        first.Annotations.Add(
            PdfLinkAnnotation.CreateWebLink(
                new PdfRectangle(new XPoint(10, 20), new XPoint(60, 40)),
                "https://example.test"
            )
        );
        document.Save(path);
    }

    private static void AssertDetail(string expected, Action action) =>
        Assert.Equal(expected, Assert.Throws<PdfSpreadException>(action).TechnicalDetail);
}
