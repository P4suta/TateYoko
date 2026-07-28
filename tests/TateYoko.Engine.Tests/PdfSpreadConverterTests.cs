using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace TateYoko.Engine.Tests;

public sealed class PdfSpreadConverterTests
{
    private readonly PdfSpreadConverter _converter = new();

    [Theory]
    [InlineData(0, 4, 2)]
    [InlineData(1, 4, 3)]
    [InlineData(2, 4, 3)]
    [InlineData(0, 3, 2)]
    [InlineData(1, 3, 2)]
    [InlineData(2, 3, 2)]
    public async Task ConvertsEveryFirstPageMode(int mode, int sourcePages, int expectedSpreads)
    {
        using var temp = new TempDirectory();
        string input = temp.File("input.pdf");
        string output = temp.File("output.pdf");
        SamplePdf.Create(input, [.. Enumerable.Repeat((200d, 300d, 0), sourcePages)]);

        PdfSpreadResult result = await _converter.ConvertAsync(
            new PdfSpreadRequest(input, output, (FirstPageMode)mode),
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Equal(sourcePages, result.InputPageCount);
        Assert.Equal(expectedSpreads, result.SpreadCount);
        Assert.Equal(output, result.OutputPath);
        using PdfDocument document = PdfReader.Open(output, PdfDocumentOpenMode.Import);
        Assert.Equal(expectedSpreads, document.PageCount);
    }

    [Fact]
    public async Task MixedPageSizesAndRotationProduceCenteredTwoPageBounds()
    {
        using var temp = new TempDirectory();
        string input = temp.File("mixed.pdf");
        string output = temp.File("mixed_spread.pdf");
        SamplePdf.Create(input, (200, 300, 0), (400, 200, 90));

        await _converter.ConvertAsync(
            new PdfSpreadRequest(input, output, FirstPageMode.Standard),
            cancellationToken: TestContext.Current.CancellationToken
        );

        using PdfDocument document = PdfReader.Open(output, PdfDocumentOpenMode.Import);
        Assert.Equal(400, document.Pages[0].Width.Point, precision: 4);
        Assert.Equal(400, document.Pages[0].Height.Point, precision: 4);
    }

    [Fact]
    public async Task InputDocumentSemanticsAreNotCopied()
    {
        using var temp = new TempDirectory();
        string input = temp.File("settings.pdf");
        string output = temp.File("settings_spread.pdf");
        SamplePdf.CreateWithDocumentSettings(input);

        await _converter.ConvertAsync(
            new PdfSpreadRequest(input, output, FirstPageMode.Standard),
            cancellationToken: TestContext.Current.CancellationToken
        );

        using PdfDocument document = PdfReader.Open(output, PdfDocumentOpenMode.Import);
        Assert.True(string.IsNullOrEmpty(document.Info.Title));
        Assert.True(string.IsNullOrEmpty(document.Info.Author));
        Assert.True(string.IsNullOrEmpty(document.Language));
        Assert.Empty(document.Outlines);
        Assert.Equal(PdfPageLayout.SinglePage, document.PageLayout);
    }

    [Fact]
    public async Task BookmarksAreDiscardedWithoutExpandingTheProductContract()
    {
        using var temp = new TempDirectory();
        string input = temp.File("outline.pdf");
        string output = temp.File("outline_spread.pdf");
        SamplePdf.CreateWithOutlines(input);

        await _converter.ConvertAsync(
            new PdfSpreadRequest(input, output, FirstPageMode.Standard),
            cancellationToken: TestContext.Current.CancellationToken
        );

        using PdfDocument document = PdfReader.Open(output, PdfDocumentOpenMode.Import);
        Assert.Empty(document.Outlines);
    }

    [Theory]
    [InlineData("text")]
    [InlineData("links")]
    public async Task RejectsAnnotationsBeforeWritingOutput(string kind)
    {
        using var temp = new TempDirectory();
        string input = temp.File("annotated.pdf");
        string output = temp.File("annotated_spread.pdf");
        if (kind == "text")
        {
            SamplePdf.CreateWithTextAnnotation(input);
        }
        else
        {
            SamplePdf.CreateWithLinks(input);
        }

        PdfSpreadException error = await Assert.ThrowsAsync<PdfSpreadException>(() =>
            _converter.ConvertAsync(
                new PdfSpreadRequest(input, output, FirstPageMode.Standard),
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(PdfSpreadError.UnsupportedPdfFeature, error.Error);
        Assert.Equal("unsupported-annotation", error.TechnicalDetail);
        Assert.False(File.Exists(output));
        Assert.Empty(Directory.GetFiles(temp.Path, ".*.tmp"));
    }

    [Theory]
    [InlineData("/AcroForm", "unsupported-interactive-form")]
    [InlineData("/OpenAction", "unsupported-document-open-action")]
    [InlineData("/AA", "unsupported-document-additional-action")]
    [InlineData("/OCProperties", "unsupported-optional-content")]
    [InlineData("/Collection", "unsupported-pdf-collection")]
    [InlineData("/Perms", "unsupported-document-permissions")]
    [InlineData("/AF", "unsupported-associated-file")]
    public async Task RejectsActiveCatalogFeatures(string feature, string expectedDetail)
    {
        using var temp = new TempDirectory();
        string input = temp.File("active.pdf");
        string output = temp.File("active_spread.pdf");
        SamplePdf.CreateWithUnsupportedCatalogFeature(input, feature);

        PdfSpreadException error = await Assert.ThrowsAsync<PdfSpreadException>(() =>
            _converter.ConvertAsync(
                new PdfSpreadRequest(input, output, FirstPageMode.Standard),
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(PdfSpreadError.UnsupportedPdfFeature, error.Error);
        Assert.Equal(expectedDetail, error.TechnicalDetail);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public async Task RejectsEmbeddedFiles()
    {
        using var temp = new TempDirectory();
        string input = temp.File("attachment.pdf");
        string output = temp.File("attachment_spread.pdf");
        SamplePdf.CreateWithEmbeddedFile(input);

        PdfSpreadException error = await Assert.ThrowsAsync<PdfSpreadException>(() =>
            _converter.ConvertAsync(
                new PdfSpreadRequest(input, output, FirstPageMode.Standard),
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

        Assert.Equal("unsupported-embedded-file", error.TechnicalDetail);
        Assert.False(File.Exists(output));
    }

    [Theory]
    [InlineData("/JavaScript", "unsupported-javascript")]
    [InlineData("/Renditions", "unsupported-multimedia-rendition")]
    public async Task RejectsActiveNameTrees(string feature, string expectedDetail)
    {
        using var temp = new TempDirectory();
        string input = temp.File("named-feature.pdf");
        string output = temp.File("named-feature_spread.pdf");
        SamplePdf.CreateCustomized(
            input,
            (document, _) =>
            {
                var names = new PdfDictionary(document);
                names.Elements[feature] = new PdfDictionary(document);
                document.Internals.Catalog.Elements["/Names"] = names;
            }
        );

        PdfSpreadException error = await Assert.ThrowsAsync<PdfSpreadException>(() =>
            _converter.ConvertAsync(
                new PdfSpreadRequest(input, output, FirstPageMode.Standard),
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(expectedDetail, error.TechnicalDetail);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public async Task RejectsMalformedAnnotationCollectionAsCorruption()
    {
        using var temp = new TempDirectory();
        string input = temp.File("malformed-annotations.pdf");
        string output = temp.File("malformed-annotations_spread.pdf");
        SamplePdf.CreateCustomized(
            input,
            (document, page) => page.Elements["/Annots"] = new PdfDictionary(document)
        );

        PdfSpreadException error = await Assert.ThrowsAsync<PdfSpreadException>(() =>
            _converter.ConvertAsync(
                new PdfSpreadRequest(input, output, FirstPageMode.Standard),
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(PdfSpreadError.CorruptedPdf, error.Error);
        Assert.Equal("annotation-array-invalid", error.TechnicalDetail);
        Assert.False(File.Exists(output));
    }

    [Theory]
    [InlineData("/AA", "unsupported-page-additional-action")]
    [InlineData("/AF", "unsupported-page-associated-file")]
    [InlineData("/PresSteps", "unsupported-presentation-step")]
    [InlineData("/Trans", "unsupported-page-transition")]
    [InlineData("/VP", "unsupported-viewport-content")]
    [InlineData("/UserUnit", "unsupported-page-unit")]
    public async Task RejectsPageBehaviorOutsideStaticContent(string feature, string expectedDetail)
    {
        using var temp = new TempDirectory();
        string input = temp.File("page-feature.pdf");
        string output = temp.File("page-feature_spread.pdf");
        SamplePdf.CreateWithUnsupportedPageFeature(input, feature);

        PdfSpreadException error = await Assert.ThrowsAsync<PdfSpreadException>(() =>
            _converter.ConvertAsync(
                new PdfSpreadRequest(input, output, FirstPageMode.Standard),
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(expectedDetail, error.TechnicalDetail);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public async Task ExplicitDestinationAtomicallyReplacesExistingOutput()
    {
        using var temp = new TempDirectory();
        string input = temp.File("input.pdf");
        string output = temp.File("output.pdf");
        SamplePdf.Create(input, (200, 300, 0), (200, 300, 0));
        await File.WriteAllTextAsync(output, "original", TestContext.Current.CancellationToken);

        PdfSpreadResult result = await _converter.ConvertAsync(
            new PdfSpreadRequest(input, output, FirstPageMode.Standard),
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Equal(output, result.OutputPath);
        using PdfDocument document = PdfReader.Open(output, PdfDocumentOpenMode.Import);
        Assert.Single(document.Pages);
        Assert.Empty(Directory.GetFiles(temp.Path, ".*.tmp"));
    }

    [Fact]
    public async Task CancellationLeavesNoOutputOrTemporaryFile()
    {
        using var temp = new TempDirectory();
        string input = temp.File("input.pdf");
        string output = temp.File("output.pdf");
        SamplePdf.Create(input, [.. Enumerable.Repeat((200d, 300d, 0), 20)]);
        using var cancellation = new CancellationTokenSource();
        var progress = new InlineProgress<PdfSpreadProgress>(_ => cancellation.Cancel());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _converter.ConvertAsync(
                new PdfSpreadRequest(input, output, FirstPageMode.Standard),
                progress,
                cancellation.Token
            )
        );

        Assert.False(File.Exists(output));
        Assert.Empty(Directory.GetFiles(temp.Path, ".*.tmp"));
    }

    [Fact]
    public async Task ReportsEverySpreadInOrder()
    {
        using var temp = new TempDirectory();
        string input = temp.File("input.pdf");
        string output = temp.File("output.pdf");
        SamplePdf.Create(input, (200, 300, 0), (200, 300, 0), (200, 300, 0));
        var reports = new List<(int Completed, int Total)>();

        await _converter.ConvertAsync(
            new PdfSpreadRequest(input, output, FirstPageMode.Standard),
            new InlineProgress<PdfSpreadProgress>(value =>
                reports.Add((value.CompletedSpreads, value.TotalSpreads))
            ),
            TestContext.Current.CancellationToken
        );

        Assert.Equal([(1, 2), (2, 2)], reports);
    }

    [Fact]
    public async Task PasswordProtectedInputRequiresAndPreservesPassword()
    {
        using var temp = new TempDirectory();
        string input = temp.File("protected.pdf");
        string output = temp.File("protected_spread.pdf");
        SamplePdf.CreateProtected(input, "secret");

        PdfSpreadException missing = await Assert.ThrowsAsync<PdfSpreadException>(() =>
            _converter.ConvertAsync(
                new PdfSpreadRequest(input, output, FirstPageMode.Standard),
                cancellationToken: TestContext.Current.CancellationToken
            )
        );
        PdfSpreadException wrong = await Assert.ThrowsAsync<PdfSpreadException>(() =>
            _converter.ConvertAsync(
                new PdfSpreadRequest(input, output, FirstPageMode.Standard, "wrong"),
                cancellationToken: TestContext.Current.CancellationToken
            )
        );
        await _converter.ConvertAsync(
            new PdfSpreadRequest(input, output, FirstPageMode.Standard, "secret"),
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Equal(PdfSpreadError.PasswordRequired, missing.Error);
        Assert.Equal("password-required", missing.TechnicalDetail);
        Assert.Equal(PdfSpreadError.InvalidPassword, wrong.Error);
        Assert.Equal("password-rejected", wrong.TechnicalDetail);
        Assert.ThrowsAny<Exception>(() => PdfReader.Open(output, PdfDocumentOpenMode.Import));
        using PdfDocument opened = PdfReader.Open(output, "secret", PdfDocumentOpenMode.Import);
        Assert.Single(opened.Pages);
    }

    [Fact]
    public async Task PasswordOnAnUnprotectedInputDoesNotEncryptTheOutput()
    {
        using var temp = new TempDirectory();
        string input = temp.File("input.pdf");
        string output = temp.File("output.pdf");
        SamplePdf.Create(input, (200, 300, 0));

        await _converter.ConvertAsync(
            new PdfSpreadRequest(input, output, FirstPageMode.Standard, "unused"),
            cancellationToken: TestContext.Current.CancellationToken
        );

        using PdfDocument document = PdfReader.Open(output, PdfDocumentOpenMode.Import);
        Assert.Single(document.Pages);
    }

    [Fact]
    public async Task ConverterSerializesConcurrentPdfSharpWork()
    {
        using var temp = new TempDirectory();
        string input = temp.File("input.pdf");
        string first = temp.File("first.pdf");
        string second = temp.File("second.pdf");
        SamplePdf.Create(input, (200, 300, 0), (200, 300, 0));

        await Task.WhenAll(
            _converter.ConvertAsync(
                new PdfSpreadRequest(input, first, FirstPageMode.Standard),
                cancellationToken: TestContext.Current.CancellationToken
            ),
            _converter.ConvertAsync(
                new PdfSpreadRequest(input, second, FirstPageMode.Standard),
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

        Assert.True(File.Exists(first));
        Assert.True(File.Exists(second));
    }

    [Fact]
    public async Task RejectsInputEqualToOutputBeforeOpeningIt()
    {
        using var temp = new TempDirectory();
        string input = temp.File("input.pdf");
        SamplePdf.Create(input, (200, 300, 0));

        PdfSpreadException error = await Assert.ThrowsAsync<PdfSpreadException>(() =>
            _converter.ConvertAsync(
                new PdfSpreadRequest(input, input, FirstPageMode.Standard),
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(PdfSpreadError.InvalidRequest, error.Error);
        Assert.Equal("input-equals-output", error.TechnicalDetail);
    }

    [Fact]
    public async Task CancellationPrecedesFileSystemValidation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _converter.ConvertAsync(
                new PdfSpreadRequest(
                    @"C:\does-not-exist.pdf",
                    @"C:\also-does-not-exist.pdf",
                    FirstPageMode.Standard
                ),
                cancellationToken: cancellation.Token
            )
        );
    }

    [Fact]
    public async Task CancellationPrecedesSyntacticValidation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _converter.ConvertAsync(
                new PdfSpreadRequest(
                    "relative-input.pdf",
                    "relative-output.pdf",
                    FirstPageMode.Standard
                ),
                cancellationToken: cancellation.Token
            )
        );
    }

    [Fact]
    public async Task RejectsNullRequest()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _converter.ConvertAsync(null!, cancellationToken: TestContext.Current.CancellationToken)
        );
    }

    [Theory]
    [InlineData(" ", @"C:\output.pdf", 0)]
    [InlineData("relative.pdf", @"C:\output.pdf", 0)]
    [InlineData("C:\\invalid\u0000.pdf", @"C:\output.pdf", 0)]
    [InlineData(@"C:\input.txt", @"C:\output.pdf", 3)]
    [InlineData(@"C:\input.pdf", @"C:\output.txt", 0)]
    public async Task RejectsInvalidPdfPaths(string input, string output, int expected)
    {
        PdfSpreadException error = await Assert.ThrowsAsync<PdfSpreadException>(() =>
            _converter.ConvertAsync(
                new PdfSpreadRequest(input, output, FirstPageMode.Standard),
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

        Assert.Equal((PdfSpreadError)expected, error.Error);
    }

    [Fact]
    public async Task RejectsMissingInputAndOutputDirectory()
    {
        using var temp = new TempDirectory();
        string missingInput = temp.File("missing.pdf");
        string output = temp.File("output.pdf");
        PdfSpreadException missing = await Assert.ThrowsAsync<PdfSpreadException>(() =>
            _converter.ConvertAsync(
                new PdfSpreadRequest(missingInput, output, FirstPageMode.Standard),
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

        string input = temp.File("input.pdf");
        SamplePdf.Create(input, (200, 300, 0));
        string missingDirectoryOutput = Path.Combine(temp.Path, "missing", "output.pdf");
        PdfSpreadException directory = await Assert.ThrowsAsync<PdfSpreadException>(() =>
            _converter.ConvertAsync(
                new PdfSpreadRequest(input, missingDirectoryOutput, FirstPageMode.Standard),
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(PdfSpreadError.InputNotFound, missing.Error);
        Assert.Equal("input-not-found", missing.TechnicalDetail);
        Assert.Equal("output-directory-not-found", directory.TechnicalDetail);
    }

    [Fact]
    public async Task RejectsUndefinedFirstPageMode()
    {
        using var temp = new TempDirectory();
        string input = temp.File("input.pdf");
        SamplePdf.Create(input, (200, 300, 0));

        PdfSpreadException error = await Assert.ThrowsAsync<PdfSpreadException>(() =>
            _converter.ConvertAsync(
                new PdfSpreadRequest(input, temp.File("output.pdf"), (FirstPageMode)99),
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

        Assert.Equal("undefined-FirstPageMode", error.TechnicalDetail);
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
