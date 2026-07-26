using System.Text;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.Annotations;
using PdfSharp.Pdf.IO;
using TateYoko.Engine.Internal;

namespace TateYoko.Engine.Tests;

public sealed class PdfSpreadConverterTests
{
    private static readonly double[] ExpectedWebQuadPoints = [210, 40, 260, 40, 210, 20, 260, 20];
    private static readonly double[] ExpectedRotatedWebQuadPoints =
    [
        340,
        240,
        340,
        190,
        320,
        240,
        320,
        190,
    ];
    private readonly PdfSpreadConverter _converter = new();

    [Theory]
    [InlineData(FirstPageMode.Standard, 5, 3)]
    [InlineData(FirstPageMode.Cover, 5, 3)]
    [InlineData(FirstPageMode.LeadingBlank, 5, 3)]
    public void ConvertsEveryFirstPageMode(FirstPageMode mode, int sourcePages, int expectedSpreads)
    {
        using var temp = new TempDirectory();
        string input = temp.File("input.pdf");
        string output = temp.File("input_spread.pdf");
        SamplePdf.Create(input, [.. Enumerable.Repeat((200d, 300d, 0), sourcePages)]);

        PdfSpreadResult result = _converter.Convert(
            new PdfSpreadRequest(input, output, mode),
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Equal(output, result.OutputPath);
        Assert.Equal(sourcePages, result.InputPageCount);
        Assert.Equal(expectedSpreads, result.SpreadCount);
        using PdfDocument document = PdfReader.Open(result.OutputPath, PdfDocumentOpenMode.Import);
        Assert.Equal(expectedSpreads, document.PageCount);
        Assert.Equal("TateYoko fixture", document.Info.Title);
        Assert.Equal("TateYoko tests", document.Info.Author);
    }

    [Fact]
    public void MixedPageSizesProduceCenteredTwoPageBounds()
    {
        using var temp = new TempDirectory();
        string input = temp.File("mixed.pdf");
        string output = temp.File("mixed_spread.pdf");
        SamplePdf.Create(input, (200, 300, 0), (300, 200, 0));

        _converter.Convert(
            new PdfSpreadRequest(input, output, FirstPageMode.Standard),
            cancellationToken: TestContext.Current.CancellationToken
        );

        using PdfDocument document = PdfReader.Open(output, PdfDocumentOpenMode.Import);
        Assert.Equal(600d, document.Pages[0].Width.Point, precision: 5);
        Assert.Equal(300d, document.Pages[0].Height.Point, precision: 5);
    }

    [Fact]
    public void RotationChangesDisplayedBounds()
    {
        using var temp = new TempDirectory();
        string input = temp.File("rotated.pdf");
        string output = temp.File("rotated_spread.pdf");
        SamplePdf.Create(input, (200, 300, 90));

        _converter.Convert(
            new PdfSpreadRequest(input, output, FirstPageMode.Standard),
            cancellationToken: TestContext.Current.CancellationToken
        );

        using PdfDocument document = PdfReader.Open(output, PdfDocumentOpenMode.Import);
        Assert.Equal(600d, document.Pages[0].Width.Point, precision: 5);
        Assert.Equal(200d, document.Pages[0].Height.Point, precision: 5);
    }

    [Fact]
    public void PreservesOutlineHierarchyAndRemapsDestinationsToSpreads()
    {
        using var temp = new TempDirectory();
        string input = temp.File("outlines.pdf");
        string output = temp.File("outlines_spread.pdf");
        SamplePdf.CreateWithOutlines(input);
        bool sourceOpened;
        using (PdfDocument source = PdfReader.Open(input, PdfDocumentOpenMode.Import))
        {
            sourceOpened = Assert.Single(source.Outlines).Opened;
        }

        _converter.Convert(
            new PdfSpreadRequest(input, output, FirstPageMode.Standard),
            cancellationToken: TestContext.Current.CancellationToken
        );

        using PdfDocument document = PdfReader.Open(output, PdfDocumentOpenMode.Modify);
        PdfOutline chapter = Assert.Single(document.Outlines);
        Assert.Equal("Chapter", chapter.Title);
        Assert.Equal(sourceOpened, chapter.Opened);
        Assert.Equal(PdfOutlineStyle.Bold, chapter.Style);
        Assert.Equal(document.Pages[0], chapter.DestinationPage);
        Assert.Equal(212d, chapter.Left);
        Assert.Equal(250d, chapter.Top);
        Assert.Equal(1.25d, chapter.Zoom);

        PdfOutline section = Assert.Single(chapter.Outlines);
        Assert.Equal("Section", section.Title);
        Assert.Equal(document.Pages[0], section.DestinationPage);
        Assert.Equal(PdfPageMode.UseOutlines, document.PageMode);
    }

    [Fact]
    public void RotatedOutlineUsesASafeFitDestinationOnTheMappedSpread()
    {
        using var temp = new TempDirectory();
        string input = temp.File("rotated-outline.pdf");
        string output = temp.File("rotated-outline_spread.pdf");
        SamplePdf.CreateWithOutlines(input, firstPageRotation: 90);

        _converter.Convert(
            new PdfSpreadRequest(input, output, FirstPageMode.Cover),
            cancellationToken: TestContext.Current.CancellationToken
        );

        using PdfDocument document = PdfReader.Open(output, PdfDocumentOpenMode.Modify);
        PdfOutline chapter = Assert.Single(document.Outlines);
        Assert.Equal(document.Pages[0], chapter.DestinationPage);
        Assert.Equal(PdfPageDestinationType.Fit, chapter.PageDestinationType);
    }

    [Fact]
    public void PreservesWebAndDocumentLinksAndRemapsTheirCoordinates()
    {
        using var temp = new TempDirectory();
        string input = temp.File("links.pdf");
        string output = temp.File("links_spread.pdf");
        SamplePdf.CreateWithLinks(input);

        _converter.Convert(
            new PdfSpreadRequest(input, output, FirstPageMode.Standard),
            cancellationToken: TestContext.Current.CancellationToken
        );

        using PdfDocument document = PdfReader.Open(output, PdfDocumentOpenMode.Modify);
        Assert.Equal(1, document.PageCount);
        PdfPage spread = document.Pages[0];
        PdfAnnotations annotations = spread.Annotations;
        Assert.Equal(3, annotations.Count);

        PdfAnnotation web = annotations[0];
        AssertRectangle(web.Rectangle, 210, 20, 260, 40);
        PdfDictionary webAction = Assert.IsType<PdfDictionary>(web.Elements.GetDictionary("/A"));
        Assert.Equal("/URI", webAction.Elements.GetName("/S"));
        Assert.Equal("https://example.test/path?q=1", webAction.Elements.GetString("/URI"));
        PdfArray quadPoints = Assert.IsType<PdfArray>(web.Elements.GetArray("/QuadPoints"));
        Assert.Equal(
            ExpectedWebQuadPoints,
            Enumerable.Range(0, quadPoints.Elements.Count).Select(quadPoints.Elements.GetReal)
        );

        PdfAnnotation direct = annotations[1];
        AssertRectangle(direct.Rectangle, 270, 20, 320, 40);
        AssertDestination(direct.Elements.GetArray("/Dest"), spread, 15, 250, 0);

        PdfAnnotation named = annotations[2];
        AssertRectangle(named.Rectangle, 330, 20, 380, 40);
        PdfDictionary namedAction = Assert.IsType<PdfDictionary>(
            named.Elements.GetDictionary("/A")
        );
        Assert.Equal("/GoTo", namedAction.Elements.GetName("/S"));
        AssertDestination(namedAction.Elements.GetArray("/D"), spread, 25, 200, 1.5);

        PdfNameTreeNode names =
            document.Internals.Catalog.Names.NameTree
            ?? throw new InvalidDataException("Output has no destination name tree.");
        PdfArray retainedNamedDestination = Assert.IsType<PdfArray>(
            names.GetValue("chapter-two", true)
        );
        AssertDestination(retainedNamedDestination, spread, 25, 200, 1.5);
    }

    [Fact]
    public void RotatedLinkGeometryFollowsTheRenderedPage()
    {
        using var temp = new TempDirectory();
        string input = temp.File("rotated-links.pdf");
        string output = temp.File("rotated-links_spread.pdf");
        SamplePdf.CreateWithLinks(input, firstPageRotation: 90);

        _converter.Convert(
            new PdfSpreadRequest(input, output, FirstPageMode.Standard),
            cancellationToken: TestContext.Current.CancellationToken
        );

        using PdfDocument document = PdfReader.Open(output, PdfDocumentOpenMode.Modify);
        Assert.Equal(1, document.PageCount);
        PdfPage spread = document.Pages[0];
        PdfAnnotation web = spread.Annotations[0];
        AssertRectangle(web.Rectangle, 320, 190, 340, 240);
        PdfArray quadPoints = Assert.IsType<PdfArray>(web.Elements.GetArray("/QuadPoints"));
        Assert.Equal(
            ExpectedRotatedWebQuadPoints,
            Enumerable.Range(0, quadPoints.Elements.Count).Select(quadPoints.Elements.GetReal)
        );
    }

    [Fact]
    public void CropBoxOriginIsRemovedWhenMappingOutlineCoordinates()
    {
        using var temp = new TempDirectory();
        string input = temp.File("cropped-outline.pdf");
        string output = temp.File("cropped-outline_spread.pdf");
        SamplePdf.CreateWithOutlines(input, cropOffsetX: 50, cropOffsetY: 40);

        _converter.Convert(
            new PdfSpreadRequest(input, output, FirstPageMode.Standard),
            cancellationToken: TestContext.Current.CancellationToken
        );

        using PdfDocument document = PdfReader.Open(output, PdfDocumentOpenMode.Modify);
        PdfOutline chapter = Assert.Single(document.Outlines);
        Assert.Equal(212d, chapter.Left);
        Assert.Equal(250d, chapter.Top);
    }

    [Fact]
    public void RejectsAnnotationsThatWouldOtherwiseBeLost()
    {
        using var temp = new TempDirectory();
        string input = temp.File("note.pdf");
        string output = temp.File("note_spread.pdf");
        SamplePdf.CreateWithTextAnnotation(input);

        PdfSpreadException exception = Assert.Throws<PdfSpreadException>(() =>
            _converter.Convert(
                new PdfSpreadRequest(input, output, FirstPageMode.Standard),
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(PdfSpreadError.UnsupportedPdfFeature, exception.Error);
        Assert.Equal("unsupported-annotation", exception.TechnicalDetail);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void PreservesDocumentMetadataLanguageAndViewerPreferences()
    {
        using var temp = new TempDirectory();
        string input = temp.File("metadata.pdf");
        string output = temp.File("metadata_spread.pdf");
        SamplePdf.CreateWithDocumentSettings(input);

        _converter.Convert(
            new PdfSpreadRequest(input, output, FirstPageMode.Standard),
            cancellationToken: TestContext.Current.CancellationToken
        );

        using PdfDocument document = PdfReader.Open(output, PdfDocumentOpenMode.Modify);
        Assert.Equal("Preserved title", document.Info.Title);
        Assert.Equal("Preserved author", document.Info.Author);
        Assert.Equal("Preserved subject", document.Info.Subject);
        Assert.Equal("one, two", document.Info.Keywords);
        Assert.Equal("Fixture creator", document.Info.Creator);
        Assert.Contains("Fixture producer", document.Info.Producer, StringComparison.Ordinal);
        Assert.Equal("/False", document.Info.Elements.GetName("/Trapped"));
        Assert.Equal(
            new DateTime(2024, 3, 2, 1, 0, 0, DateTimeKind.Utc),
            document.Info.CreationDate.ToUniversalTime()
        );
        Assert.True(
            document.Info.ModificationDate.ToUniversalTime()
                > new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        );
        Assert.Equal(20, document.Version);
        Assert.Equal("ja-JP", document.Language);
        Assert.Equal(PdfPageLayout.SinglePage, document.PageLayout);
        Assert.Equal(PdfPageMode.UseThumbs, document.PageMode);
        PdfDictionary viewerPreferences =
            document.Internals.Catalog.Elements.GetDictionary("/ViewerPreferences")
            ?? throw new InvalidDataException("Output has no viewer preferences.");
        Assert.True(viewerPreferences.Elements.GetBoolean("/HideToolbar"));
        Assert.True(viewerPreferences.Elements.GetBoolean("/HideMenubar"));
        Assert.True(viewerPreferences.Elements.GetBoolean("/HideWindowUI"));
        Assert.True(viewerPreferences.Elements.GetBoolean("/FitWindow"));
        Assert.True(viewerPreferences.Elements.GetBoolean("/CenterWindow"));
        Assert.True(viewerPreferences.Elements.GetBoolean("/DisplayDocTitle"));
        Assert.Equal("/R2L", viewerPreferences.Elements.GetName("/Direction"));

        PdfDictionary metadata =
            document.Internals.Catalog.Elements.GetDictionary("/Metadata")
            ?? throw new InvalidDataException("Output has no XMP metadata.");
        Assert.Contains(
            "Preserved title",
            Encoding.UTF8.GetString(metadata.Stream.UnfilteredValue),
            StringComparison.Ordinal
        );
    }

    [Theory]
    [InlineData("/StructTreeRoot")]
    [InlineData("/AcroForm")]
    [InlineData("/PageLabels")]
    [InlineData("/OpenAction")]
    [InlineData("/OCProperties")]
    [InlineData("/OutputIntents")]
    public void RejectsUnsupportedDocumentFeaturesInsteadOfDroppingThem(string feature)
    {
        using var temp = new TempDirectory();
        string input = temp.File("catalog-feature.pdf");
        string output = temp.File("catalog-feature_spread.pdf");
        SamplePdf.CreateWithUnsupportedCatalogFeature(input, feature);

        PdfSpreadException exception = Assert.Throws<PdfSpreadException>(() =>
            _converter.Convert(
                new PdfSpreadRequest(input, output, FirstPageMode.Standard),
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(PdfSpreadError.UnsupportedPdfFeature, exception.Error);
        Assert.Equal("unsupported-document-feature", exception.TechnicalDetail);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void RejectsEmbeddedFilesInsteadOfDroppingThem()
    {
        using var temp = new TempDirectory();
        string input = temp.File("attachment.pdf");
        string output = temp.File("attachment_spread.pdf");
        SamplePdf.CreateWithEmbeddedFile(input);

        PdfSpreadException exception = Assert.Throws<PdfSpreadException>(() =>
            _converter.Convert(
                new PdfSpreadRequest(input, output, FirstPageMode.Standard),
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(PdfSpreadError.UnsupportedPdfFeature, exception.Error);
        Assert.Equal("unsupported-document-feature", exception.TechnicalDetail);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void RejectsCustomXmpThatCannotBeRegeneratedWithoutLoss()
    {
        using var temp = new TempDirectory();
        string input = temp.File("custom-xmp.pdf");
        string output = temp.File("custom-xmp_spread.pdf");
        SamplePdf.CreateWithCustomXmp(input);

        PdfSpreadException exception = Assert.Throws<PdfSpreadException>(() =>
            _converter.Convert(
                new PdfSpreadRequest(input, output, FirstPageMode.Standard),
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(PdfSpreadError.UnsupportedPdfFeature, exception.Error);
        Assert.Equal("unsupported-metadata-schema", exception.TechnicalDetail);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void RejectsUnrepresentableFieldsInsideStandardXmpSchemas()
    {
        using var temp = new TempDirectory();
        string input = temp.File("xmp-rights.pdf");
        string output = temp.File("xmp-rights_spread.pdf");
        SamplePdf.CreateWithUnsupportedStandardXmp(input);

        PdfSpreadException exception = Assert.Throws<PdfSpreadException>(() =>
            _converter.Convert(
                new PdfSpreadRequest(input, output, FirstPageMode.Standard),
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(PdfSpreadError.UnsupportedPdfFeature, exception.Error);
        Assert.Equal("unsupported-metadata-schema", exception.TechnicalDetail);
        Assert.False(File.Exists(output));
    }

    [Theory]
    [InlineData("/AA", "unsupported-page-feature")]
    [InlineData("/AF", "unsupported-page-feature")]
    [InlineData("/ArtBox", "unsupported-page-feature")]
    [InlineData("/B", "unsupported-page-feature")]
    [InlineData("/BleedBox", "unsupported-page-feature")]
    [InlineData("/BoxColorInfo", "unsupported-page-feature")]
    [InlineData("/Dur", "unsupported-page-feature")]
    [InlineData("/LastModified", "unsupported-page-feature")]
    [InlineData("/Metadata", "unsupported-page-feature")]
    [InlineData("/PieceInfo", "unsupported-page-feature")]
    [InlineData("/PresSteps", "unsupported-page-feature")]
    [InlineData("/SeparationInfo", "unsupported-page-feature")]
    [InlineData("/StructParents", "unsupported-page-feature")]
    [InlineData("/Tabs", "unsupported-page-feature")]
    [InlineData("/TemplateInstantiated", "unsupported-page-feature")]
    [InlineData("/Thumb", "unsupported-page-feature")]
    [InlineData("/Trans", "unsupported-page-feature")]
    [InlineData("/TrimBox", "unsupported-page-feature")]
    [InlineData("/UserUnit", "unsupported-page-unit")]
    [InlineData("/VP", "unsupported-page-feature")]
    public void RejectsUnsupportedPageSemanticsInsteadOfDroppingThem(
        string feature,
        string technicalDetail
    )
    {
        using var temp = new TempDirectory();
        string input = temp.File("page-feature.pdf");
        string output = temp.File("page-feature_spread.pdf");
        SamplePdf.CreateWithUnsupportedPageFeature(input, feature);

        PdfSpreadException exception = Assert.Throws<PdfSpreadException>(() =>
            _converter.Convert(
                new PdfSpreadRequest(input, output, FirstPageMode.Standard),
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(technicalDetail, exception.TechnicalDetail);
        Assert.Equal(PdfSpreadError.UnsupportedPdfFeature, exception.Error);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void ReportsEverySpreadInOrder()
    {
        using var temp = new TempDirectory();
        string input = temp.File("progress.pdf");
        string output = temp.File("progress_spread.pdf");
        SamplePdf.Create(input, (100, 200, 0), (100, 200, 0), (100, 200, 0));
        var values = new List<PdfSpreadProgress>();
        var progress = new InlineProgress<PdfSpreadProgress>(values.Add);

        _converter.Convert(
            new PdfSpreadRequest(input, output, FirstPageMode.Standard),
            progress,
            TestContext.Current.CancellationToken
        );

        Assert.Collection(
            values,
            value => Assert.Equal((1, 2), (value.CompletedSpreads, value.TotalSpreads)),
            value => Assert.Equal((2, 2), (value.CompletedSpreads, value.TotalSpreads))
        );
    }

    [Fact]
    public void UniquePolicyNeverOverwritesExistingOutput()
    {
        using var temp = new TempDirectory();
        string input = temp.File("unique.pdf");
        string output = temp.File("unique_spread.pdf");
        SamplePdf.Create(input, (100, 200, 0));
        File.WriteAllText(output, "keep");

        PdfSpreadResult result = _converter.Convert(
            new PdfSpreadRequest(input, output, FirstPageMode.Standard),
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Equal("keep", File.ReadAllText(output));
        Assert.Equal(temp.File("unique_spread (2).pdf"), result.OutputPath);
        Assert.True(File.Exists(result.OutputPath));
    }

    [Fact]
    public void ReplacePolicyCommitsValidatedPdfOverExistingOutput()
    {
        using var temp = new TempDirectory();
        string input = temp.File("replace.pdf");
        string output = temp.File("replace_spread.pdf");
        SamplePdf.Create(input, (100, 200, 0));
        File.WriteAllText(output, "old");

        PdfSpreadResult result = _converter.Convert(
            new PdfSpreadRequest(
                input,
                output,
                FirstPageMode.Standard,
                OutputCollisionPolicy.ReplaceExisting
            ),
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Equal(output, result.OutputPath);
        using PdfDocument document = PdfReader.Open(output, PdfDocumentOpenMode.Import);
        Assert.Single(document.Pages);
    }

    [Fact]
    public void CancellationLeavesNoOutputOrTemporaryFile()
    {
        using var temp = new TempDirectory();
        string input = temp.File("cancel.pdf");
        string output = temp.File("cancel_spread.pdf");
        SamplePdf.Create(input, [.. Enumerable.Repeat((100d, 200d, 0), 8)]);
        using var cancellation = new CancellationTokenSource();
        var progress = new InlineProgress<PdfSpreadProgress>(_ => cancellation.Cancel());

        Assert.Throws<OperationCanceledException>(() =>
            _converter.Convert(
                new PdfSpreadRequest(input, output, FirstPageMode.Standard),
                progress,
                cancellation.Token
            )
        );

        Assert.False(File.Exists(output));
        Assert.Empty(Directory.EnumerateFiles(temp.Path, ".*.tmp"));
    }

    [Fact]
    public void PasswordProtectedInputRequiresPassword()
    {
        using var temp = new TempDirectory();
        string input = temp.File("protected.pdf");
        string output = temp.File("protected_spread.pdf");
        SamplePdf.CreateProtected(input, "secret");

        PdfSpreadException exception = Assert.Throws<PdfSpreadException>(() =>
            _converter.Convert(
                new PdfSpreadRequest(input, output, FirstPageMode.Standard),
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(PdfSpreadError.PasswordRequired, exception.Error);
        Assert.Equal("password-required", exception.TechnicalDetail);
        Assert.DoesNotContain(input, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WrongPasswordIsDistinguished()
    {
        using var temp = new TempDirectory();
        string input = temp.File("protected.pdf");
        string output = temp.File("protected_spread.pdf");
        SamplePdf.CreateProtected(input, "secret");

        PdfSpreadException exception = Assert.Throws<PdfSpreadException>(() =>
            _converter.Convert(
                new PdfSpreadRequest(input, output, FirstPageMode.Standard, password: "wrong"),
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(PdfSpreadError.InvalidPassword, exception.Error);
        Assert.Equal("password-rejected", exception.TechnicalDetail);
    }

    [Fact]
    public void CorrectPasswordProducesProtectedOutput()
    {
        using var temp = new TempDirectory();
        string input = temp.File("protected.pdf");
        string output = temp.File("protected_spread.pdf");
        SamplePdf.CreateProtected(input, "secret");

        _converter.Convert(
            new PdfSpreadRequest(input, output, FirstPageMode.Standard, password: "secret"),
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.ThrowsAny<Exception>(() => PdfReader.Open(output, PdfDocumentOpenMode.Import));
        using PdfDocument opened = PdfReader.Open(output, "secret", PdfDocumentOpenMode.Import);
        Assert.Single(opened.Pages);
    }

    [Fact]
    public void SuperfluousPasswordDoesNotProtectAnUnprotectedInput()
    {
        using var temp = new TempDirectory();
        string input = temp.File("unprotected.pdf");
        string output = temp.File("unprotected_spread.pdf");
        SamplePdf.Create(input, (100, 200, 0), (100, 200, 0));

        _converter.Convert(
            new PdfSpreadRequest(input, output, FirstPageMode.Standard, password: "unused"),
            cancellationToken: TestContext.Current.CancellationToken
        );

        using PdfDocument opened = PdfReader.Open(output, PdfDocumentOpenMode.Import);
        Assert.Single(opened.Pages);
        Assert.False(opened.SecuritySettings.IsEncrypted);
    }

    [Fact]
    public void ProtectedConversionCreatesNoDecryptedDiskWorkingCopy()
    {
        using var temp = new TempDirectory();
        string input = temp.File("protected.pdf");
        string output = temp.File("protected_spread.pdf");
        SamplePdf.CreateProtected(input, "secret");
        HashSet<string> before = Directory
            .EnumerateFiles(Path.GetTempPath(), "TateYoko-*.pdf")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _converter.Convert(
            new PdfSpreadRequest(input, output, FirstPageMode.Standard, password: "secret"),
            cancellationToken: TestContext.Current.CancellationToken
        );

        string[] leaked =
        [
            .. Directory
                .EnumerateFiles(Path.GetTempPath(), "TateYoko-*.pdf")
                .Where(path => !before.Contains(path)),
        ];
        Assert.Empty(leaked);
    }

    [Fact]
    public void LockedInputIsReportedAsAReadFailure()
    {
        using var temp = new TempDirectory();
        string input = temp.File("locked.pdf");
        SamplePdf.Create(input, (100, 200, 0));
        using var inputLock = new FileStream(input, FileMode.Open, FileAccess.Read, FileShare.None);

        PdfSpreadException exception = Assert.Throws<PdfSpreadException>(() =>
            _converter.Convert(
                new PdfSpreadRequest(input, temp.File("locked_spread.pdf"), FirstPageMode.Standard),
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(PdfSpreadError.ReadFailed, exception.Error);
    }

    [Fact]
    public void InputCannotChangeDuringConversion()
    {
        using var temp = new TempDirectory();
        string input = temp.File("stable.pdf");
        SamplePdf.Create(input, (100, 200, 0), (100, 200, 0));
        var progress = new InlineProgress<PdfSpreadProgress>(_ =>
            Assert.Throws<IOException>(() =>
            {
                using var writer = new FileStream(
                    input,
                    FileMode.Open,
                    FileAccess.Write,
                    FileShare.None
                );
            })
        );

        _converter.Convert(
            new PdfSpreadRequest(input, temp.File("stable_spread.pdf"), FirstPageMode.Standard),
            progress,
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task OneConverterSupportsConcurrentDistinctOutputs()
    {
        using var temp = new TempDirectory();
        string input = temp.File("concurrent.pdf");
        SamplePdf.Create(input, (100, 200, 0), (100, 200, 0), (100, 200, 0));
        Task<PdfSpreadResult>[] conversions =
        [
            .. Enumerable
                .Range(0, 8)
                .Select(index =>
                    Task.Run(
                        () =>
                            _converter.Convert(
                                new PdfSpreadRequest(
                                    input,
                                    temp.File($"concurrent-{index}.pdf"),
                                    FirstPageMode.Standard
                                ),
                                cancellationToken: TestContext.Current.CancellationToken
                            ),
                        TestContext.Current.CancellationToken
                    )
                ),
        ];

        PdfSpreadResult[] results = await Task.WhenAll(conversions);

        Assert.Equal(8, results.Select(result => result.OutputPath).Distinct().Count());
        Assert.All(results, result => Assert.True(File.Exists(result.OutputPath)));
    }

    [Fact]
    public void SourceReusesOnePdfFormAcrossPages()
    {
        using var temp = new TempDirectory();
        string input = temp.File("forms.pdf");
        SamplePdf.Create(input, (100, 200, 0), (100, 200, 0));
        using PdfSource source = PdfSource.Open(input, password: null);

        object first = source.SelectPage(0);
        object second = source.SelectPage(1);

        Assert.Same(first, second);
    }

    [Theory]
    [InlineData("relative.pdf", PdfSpreadError.InvalidRequest)]
    [InlineData(@"C:\input.txt", PdfSpreadError.UnsupportedFile)]
    public void RejectsInvalidInputPath(string input, PdfSpreadError expected)
    {
        using var temp = new TempDirectory();

        PdfSpreadException exception = Assert.Throws<PdfSpreadException>(() =>
            _converter.Convert(
                new PdfSpreadRequest(input, temp.File("out.pdf"), FirstPageMode.Standard),
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(expected, exception.Error);
    }

    [Fact]
    public void RejectsInputEqualToOutput()
    {
        using var temp = new TempDirectory();
        string path = temp.File("same.pdf");
        SamplePdf.Create(path, (100, 200, 0));

        PdfSpreadException exception = Assert.Throws<PdfSpreadException>(() =>
            _converter.Convert(
                new PdfSpreadRequest(path, path, FirstPageMode.Standard),
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(PdfSpreadError.InvalidRequest, exception.Error);
    }

    [Fact]
    public void RejectsUndefinedEnumsBeforeOpeningInput()
    {
        using var temp = new TempDirectory();
        string missing = temp.File("missing.pdf");

        PdfSpreadException exception = Assert.Throws<PdfSpreadException>(() =>
            _converter.Convert(
                new PdfSpreadRequest(missing, temp.File("out.pdf"), (FirstPageMode)99),
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(PdfSpreadError.InvalidRequest, exception.Error);
    }

    private static void AssertRectangle(
        PdfRectangle rectangle,
        double x1,
        double y1,
        double x2,
        double y2
    )
    {
        Assert.Equal(x1, rectangle.X1, precision: 5);
        Assert.Equal(y1, rectangle.Y1, precision: 5);
        Assert.Equal(x2, rectangle.X2, precision: 5);
        Assert.Equal(y2, rectangle.Y2, precision: 5);
    }

    private static void AssertDestination(
        PdfArray? value,
        PdfPage expectedPage,
        double left,
        double top,
        double zoom
    )
    {
        PdfArray destination = Assert.IsType<PdfArray>(value);
        Assert.Equal(5, destination.Elements.Count);
        PdfReference pageReference = Assert.IsType<PdfReference>(destination.Elements[0]);
        Assert.Equal(expectedPage.ReferenceNotNull.ObjectID, pageReference.ObjectID);
        Assert.Equal("/XYZ", Assert.IsType<PdfName>(destination.Elements[1]).Value);
        Assert.Equal(left, destination.Elements.GetReal(2), precision: 5);
        Assert.Equal(top, destination.Elements.GetReal(3), precision: 5);
        Assert.Equal(zoom, destination.Elements.GetReal(4), precision: 5);
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
