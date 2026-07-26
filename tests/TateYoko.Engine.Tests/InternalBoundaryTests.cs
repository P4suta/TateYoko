using PdfSharp.Drawing;
using PdfSharp.Pdf;
using TateYoko.Engine.Internal;

namespace TateYoko.Engine.Tests;

public sealed class InternalBoundaryTests
{
    [Fact]
    public void ExceptionConstructorsPreserveThePublicContract()
    {
        var cause = new IOException("synthetic");

        var empty = new PdfSpreadException();
        var message = new PdfSpreadException("diagnostic");
        var nested = new PdfSpreadException("diagnostic", cause);
        var categorized = new PdfSpreadException(PdfSpreadError.WriteFailed, "write-failed", cause);

        Assert.Equal(PdfSpreadError.Internal, empty.Error);
        Assert.Equal(PdfSpreadError.Internal, message.Error);
        Assert.Equal("diagnostic", message.Message);
        Assert.Equal(PdfSpreadError.Internal, nested.Error);
        Assert.Same(cause, nested.InnerException);
        Assert.Equal(PdfSpreadError.WriteFailed, categorized.Error);
        Assert.Equal("write-failed", categorized.TechnicalDetail);
        Assert.Same(cause, categorized.InnerException);
    }

    [Fact]
    public void AtomicOutputCreatesAnOpaqueSiblingTemporaryPath()
    {
        string output = Path.Combine(Path.GetTempPath(), "book.pdf");

        string temporary = AtomicOutput.CreateTemporaryPath(output);

        Assert.Equal(Path.GetDirectoryName(output), Path.GetDirectoryName(temporary));
        Assert.StartsWith(".book.pdf.", Path.GetFileName(temporary), StringComparison.Ordinal);
        Assert.EndsWith(".tmp", temporary, StringComparison.Ordinal);
        Assert.NotEqual(temporary, AtomicOutput.CreateTemporaryPath(output));
    }

    [Fact]
    public void AtomicOutputUsesTheFirstAvailableUniqueName()
    {
        using var directory = new TempDirectory();
        string requested = directory.File("book.pdf");
        string second = directory.File("book (2).pdf");
        string temporary = directory.File("temporary.pdf");
        File.WriteAllText(requested, "original");
        File.WriteAllText(second, "second");
        File.WriteAllText(temporary, "replacement");

        string committed = AtomicOutput.Commit(
            temporary,
            requested,
            OutputCollisionPolicy.CreateUnique
        );

        Assert.Equal(directory.File("book (3).pdf"), committed);
        Assert.Equal("replacement", File.ReadAllText(committed));
        Assert.Equal("original", File.ReadAllText(requested));
        Assert.Equal("second", File.ReadAllText(second));
    }

    [Fact]
    public void AtomicOutputWrapsCommitFailures()
    {
        using var directory = new TempDirectory();
        string temporary = directory.File("temporary.pdf");
        File.WriteAllText(temporary, "replacement");
        string missingParent = Path.Combine(directory.Path, "missing", "book.pdf");

        PdfSpreadException exception = Assert.Throws<PdfSpreadException>(() =>
            AtomicOutput.Commit(temporary, missingParent, OutputCollisionPolicy.ReplaceExisting)
        );

        Assert.Equal(PdfSpreadError.WriteFailed, exception.Error);
        Assert.Equal("output-commit-failed", exception.TechnicalDetail);
        Assert.IsAssignableFrom<IOException>(exception.InnerException);
    }

    [Fact]
    public void AtomicOutputCleanupIsIdempotentAndBestEffort()
    {
        using var directory = new TempDirectory();
        string path = directory.File("temporary.pdf");
        File.WriteAllText(path, "temporary");

        using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            AtomicOutput.DeleteTemporary(path);
            Assert.True(File.Exists(path));
        }

        AtomicOutput.DeleteTemporary(path);
        AtomicOutput.DeleteTemporary(path);
        Assert.False(File.Exists(path));
    }

    [Theory]
    [InlineData(0, 40, 270)]
    [InlineData(90, 40, 350)]
    [InlineData(180, 120, 450)]
    [InlineData(270, 220, 270)]
    public void PageProjectionTransformsEverySupportedRotation(
        int rotation,
        double expectedX,
        double expectedY
    )
    {
        using var destination = new PdfDocument();
        PdfPage page = destination.AddPage();
        page.Width = XUnit.FromPoint(400);
        page.Height = XUnit.FromPoint(500);
        var projection = new PageProjection(
            page,
            new Point(30, 40),
            new PageSize(100, 200),
            new PdfRectangle(new XPoint(10, 20), new XPoint(110, 220)),
            rotation
        );

        XPoint point = projection.TransformPoint(20, 30);

        Assert.Equal(expectedX, point.X, precision: 8);
        Assert.Equal(expectedY, point.Y, precision: 8);
    }

    [Fact]
    public void PageProjectionMapsAxesAndRectangles()
    {
        using var destination = new PdfDocument();
        PdfPage page = destination.AddPage();
        page.Width = XUnit.FromPoint(400);
        page.Height = XUnit.FromPoint(500);
        var projection = new PageProjection(
            page,
            new Point(30, 40),
            new PageSize(100, 200),
            new PdfRectangle(new XPoint(10, 20), new XPoint(110, 220)),
            0
        );

        Assert.Equal(40, projection.TransformX(20), precision: 8);
        Assert.Equal(270, projection.TransformY(30), precision: 8);
        PdfRectangle rectangle = projection.TransformRectangle(
            new PdfRectangle(new XPoint(20, 30), new XPoint(50, 80))
        );
        Assert.Equal(40, rectangle.X1, precision: 8);
        Assert.Equal(270, rectangle.Y1, precision: 8);
        Assert.Equal(70, rectangle.X2, precision: 8);
        Assert.Equal(320, rectangle.Y2, precision: 8);
    }

    [Fact]
    public void PageProjectionRejectsInvalidCoordinatesAndAxisTransforms()
    {
        using var destination = new PdfDocument();
        PdfPage page = destination.AddPage();
        page.Width = XUnit.FromPoint(400);
        page.Height = XUnit.FromPoint(500);
        var rotated = new PageProjection(
            page,
            new Point(0, 0),
            new PageSize(100, 200),
            new PdfRectangle(new XPoint(0, 0), new XPoint(100, 200)),
            90
        );
        var invalidRotation = rotated with { SourceRotation = 45 };

        Assert.Equal(
            "rotated-axis-transform",
            Assert.Throws<PdfSpreadException>(() => rotated.TransformX(1)).TechnicalDetail
        );
        Assert.Equal(
            "rotated-axis-transform",
            Assert.Throws<PdfSpreadException>(() => rotated.TransformY(1)).TechnicalDetail
        );
        Assert.Equal(
            "annotation-coordinate-invalid",
            Assert
                .Throws<PdfSpreadException>(() => rotated.TransformPoint(double.NaN, 1))
                .TechnicalDetail
        );
        Assert.Equal(
            "page-rotation-invalid",
            Assert
                .Throws<PdfSpreadException>(() => invalidRotation.TransformPoint(1, 1))
                .TechnicalDetail
        );
    }

    [Fact]
    public void PdfSourceRejectsMissingDirectoriesAndCorruptedInputs()
    {
        using var directory = new TempDirectory();
        string missing = directory.File("missing.pdf");
        string corrupted = directory.File("corrupted.pdf");
        File.WriteAllText(corrupted, "not a PDF");

        PdfSpreadException missingError = Assert.Throws<PdfSpreadException>(() =>
            PdfSource.Open(missing, password: null)
        );
        PdfSpreadException directoryError = Assert.Throws<PdfSpreadException>(() =>
            PdfSource.Open(directory.Path, password: null)
        );
        PdfSpreadException corruptedError = Assert.Throws<PdfSpreadException>(() =>
            PdfSource.Open(corrupted, password: null)
        );

        Assert.Equal(PdfSpreadError.InputNotFound, missingError.Error);
        Assert.Equal(PdfSpreadError.ReadFailed, directoryError.Error);
        Assert.Equal(PdfSpreadError.CorruptedPdf, corruptedError.Error);
        Assert.Equal("pdf-open-failed", corruptedError.TechnicalDetail);
    }

    [Fact]
    public void PdfSourceRejectsInvalidPageAndIncompleteProjectionMaps()
    {
        using var directory = new TempDirectory();
        string input = directory.File("input.pdf");
        string outlineInput = directory.File("outlines.pdf");
        string linkInput = directory.File("links.pdf");
        SamplePdf.Create(input, (200, 300, 0));
        SamplePdf.CreateWithOutlines(outlineInput);
        SamplePdf.CreateWithLinks(linkInput);
        using PdfSource source = PdfSource.Open(input, password: null);
        using PdfSource outlineSource = PdfSource.Open(outlineInput, password: null);
        using PdfSource linkSource = PdfSource.Open(linkInput, password: null);
        using var destination = new PdfDocument();
        PdfPage destinationPage = destination.AddPage();
        destinationPage.Width = XUnit.FromPoint(400);
        destinationPage.Height = XUnit.FromPoint(300);
        PageProjection?[] incomplete = [null];

        Assert.Equal(
            "page-index-out-of-range",
            Assert.Throws<PdfSpreadException>(() => source.GetPageSize(-1)).TechnicalDetail
        );
        Assert.Equal(
            "page-index-out-of-range",
            Assert.Throws<PdfSpreadException>(() => source.SelectPage(1)).TechnicalDetail
        );
        Assert.Throws<ArgumentNullException>(() =>
            source.CreateProjection(0, null!, new Point(0, 0), new PageSize(200, 300))
        );
        Assert.Equal(
            "page-index-out-of-range",
            Assert
                .Throws<PdfSpreadException>(() =>
                    source.CreateProjection(
                        1,
                        destinationPage,
                        new Point(0, 0),
                        new PageSize(200, 300)
                    )
                )
                .TechnicalDetail
        );
        Assert.Equal(
            "destination-page-map-incomplete",
            Assert
                .Throws<PdfSpreadException>(() =>
                    source.CopyNamedDestinationsTo(destination, incomplete)
                )
                .TechnicalDetail
        );
        Assert.Equal(
            "outline-page-map-incomplete",
            Assert
                .Throws<PdfSpreadException>(() =>
                    outlineSource.CopyOutlinesTo(destination, incomplete)
                )
                .TechnicalDetail
        );
        Assert.Equal(
            "annotation-page-map-incomplete",
            Assert
                .Throws<PdfSpreadException>(() =>
                    linkSource.CopyAnnotationsTo(destination, incomplete, CancellationToken.None)
                )
                .TechnicalDetail
        );

        source.Dispose();
    }

    [Fact]
    public void PaginationRejectsEmptyDocumentsWhenEnumerationBegins()
    {
        PdfSpreadException countError = Assert.Throws<PdfSpreadException>(() =>
            Pagination.Count(FirstPageMode.Standard, 0)
        );
        IEnumerable<PageGroup> sequence = Pagination.Enumerate(FirstPageMode.Standard, 0);
        PdfSpreadException enumerateError = Assert.Throws<PdfSpreadException>(() =>
            sequence.ToArray()
        );

        Assert.Equal("empty-document", countError.TechnicalDetail);
        Assert.Equal("empty-document", enumerateError.TechnicalDetail);
    }

    [Fact]
    public void ConverterRejectsNullCanceledAndMissingOutputDirectoryRequests()
    {
        var converter = new PdfSpreadConverter();
        Assert.Throws<ArgumentNullException>(() =>
            converter.Convert(null!, cancellationToken: TestContext.Current.CancellationToken)
        );

        using var directory = new TempDirectory();
        string input = directory.File("input.pdf");
        SamplePdf.Create(input, (200, 300, 0));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var canceled = new PdfSpreadRequest(
            input,
            directory.File("canceled.pdf"),
            FirstPageMode.Standard,
            OutputCollisionPolicy.CreateUnique
        );
        var missingDirectory = new PdfSpreadRequest(
            input,
            Path.Combine(directory.Path, "missing", "output.pdf"),
            FirstPageMode.Standard,
            OutputCollisionPolicy.CreateUnique
        );

        Assert.Throws<OperationCanceledException>(() =>
            converter.Convert(canceled, cancellationToken: cancellation.Token)
        );
        PdfSpreadException outputError = Assert.Throws<PdfSpreadException>(() =>
            converter.Convert(
                missingDirectory,
                cancellationToken: TestContext.Current.CancellationToken
            )
        );
        Assert.Equal(PdfSpreadError.InvalidRequest, outputError.Error);
        Assert.Equal("output-directory-not-found", outputError.TechnicalDetail);
    }
}
