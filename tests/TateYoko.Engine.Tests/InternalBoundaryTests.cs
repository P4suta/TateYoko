using PdfSharp.Pdf;
using TateYoko.Engine.Internal;

namespace TateYoko.Engine.Tests;

public sealed class InternalBoundaryTests
{
    [Fact]
    public void AtomicOutputCreatesOpaqueSiblingPath()
    {
        string output = Path.Combine(Path.GetTempPath(), "book.pdf");

        string first = AtomicOutput.CreateTemporaryPath(output);
        string second = AtomicOutput.CreateTemporaryPath(output);

        Assert.Equal(Path.GetDirectoryName(output), Path.GetDirectoryName(first));
        Assert.StartsWith(".book.pdf.", Path.GetFileName(first), StringComparison.Ordinal);
        Assert.EndsWith(".tmp", first, StringComparison.Ordinal);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void AtomicOutputMovesNewFileAndReplacesExistingFile()
    {
        using var temp = new TempDirectory();
        string destination = temp.File("book.pdf");
        string firstTemporary = temp.File("first.tmp");
        string secondTemporary = temp.File("second.tmp");
        File.WriteAllText(firstTemporary, "first");
        File.WriteAllText(secondTemporary, "second");

        Assert.Equal(destination, AtomicOutput.Commit(firstTemporary, destination));
        Assert.Equal("first", File.ReadAllText(destination));
        Assert.Equal(destination, AtomicOutput.Commit(secondTemporary, destination));
        Assert.Equal("second", File.ReadAllText(destination));
    }

    [Fact]
    public void AtomicOutputWrapsCommitFailureWithoutDeletingTemporary()
    {
        using var temp = new TempDirectory();
        string temporary = temp.File("temporary.tmp");
        string destination = Path.Combine(temp.Path, "missing", "book.pdf");
        File.WriteAllText(temporary, "replacement");

        PdfSpreadException error = Assert.Throws<PdfSpreadException>(() =>
            AtomicOutput.Commit(temporary, destination)
        );

        Assert.Equal(PdfSpreadError.WriteFailed, error.Error);
        Assert.Equal("output-commit-failed", error.TechnicalDetail);
        Assert.True(File.Exists(temporary));
    }

    [Fact]
    public void TemporaryCleanupIsIdempotentAndReportsPersistentFailure()
    {
        using var temp = new TempDirectory();
        string path = temp.File("temporary.tmp");
        File.WriteAllText(path, "temporary");

        using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            PdfSpreadException error = Assert.Throws<PdfSpreadException>(() =>
                AtomicOutput.DeleteTemporary(path)
            );
            Assert.Equal("temporary-cleanup-failed", error.TechnicalDetail);
        }

        AtomicOutput.DeleteTemporary(path);
        AtomicOutput.DeleteTemporary(path);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void SourceRejectsCorruptedInputAndReleasesValidInput()
    {
        using var temp = new TempDirectory();
        string corrupted = temp.File("corrupted.pdf");
        string input = temp.File("input.pdf");
        string moved = temp.File("moved.pdf");
        File.WriteAllText(corrupted, "not a PDF");
        SamplePdf.Create(input, (200, 300, 0));

        PdfSpreadException error = Assert.Throws<PdfSpreadException>(() =>
            PdfSource.Open(corrupted, password: null)
        );
        PdfSource source = PdfSource.Open(input, password: null);
        source.Dispose();
        source.Dispose();
        File.Move(input, moved);

        Assert.Equal(PdfSpreadError.CorruptedPdf, error.Error);
        Assert.Equal("pdf-open-failed", error.TechnicalDetail);
        Assert.True(File.Exists(moved));
    }

    [Fact]
    public void SourceClassifiesMissingAndUnreadablePaths()
    {
        using var temp = new TempDirectory();
        PdfSpreadException missing = Assert.Throws<PdfSpreadException>(() =>
            PdfSource.Open(temp.File("missing.pdf"), password: null)
        );
        PdfSpreadException unreadable = Assert.Throws<PdfSpreadException>(() =>
            PdfSource.Open(temp.Path, password: null)
        );

        Assert.Equal(PdfSpreadError.InputNotFound, missing.Error);
        Assert.Equal(PdfSpreadError.ReadFailed, unreadable.Error);
    }

    [Fact]
    public void SourceRejectsAnEmptyDocument()
    {
        using var temp = new TempDirectory();
        string input = temp.File("empty.pdf");
        SamplePdf.CreateEmpty(input);

        PdfSpreadException error = Assert.Throws<PdfSpreadException>(() =>
            PdfSource.Open(input, password: null)
        );

        Assert.Equal(PdfSpreadError.InvalidPage, error.Error);
        Assert.Equal("empty-document", error.TechnicalDetail);
    }

    [Fact]
    public void OutputVerifierRejectsIncompleteAndUnreadableArtifacts()
    {
        using var temp = new TempDirectory();
        string output = temp.File("output.pdf");
        SamplePdf.Create(output, (200, 300, 0));

        PdfSpreadException count = Assert.Throws<PdfSpreadException>(() =>
            OutputVerifier.Verify(
                output,
                [new PageSize(200, 300), new PageSize(200, 300)],
                password: null
            )
        );
        PdfSpreadException size = Assert.Throws<PdfSpreadException>(() =>
            OutputVerifier.Verify(output, [new PageSize(201, 300)], password: null)
        );

        string corrupt = temp.File("corrupt.pdf");
        File.WriteAllText(corrupt, "not a PDF");
        PdfSpreadException unreadable = Assert.Throws<PdfSpreadException>(() =>
            OutputVerifier.Verify(corrupt, [new PageSize(200, 300)], password: null)
        );

        Assert.Equal("output-page-count-mismatch", count.TechnicalDetail);
        Assert.Equal("output-page-size-mismatch", size.TechnicalDetail);
        Assert.Equal("output-validation-failed", unreadable.TechnicalDetail);
    }

    [Fact]
    public void OutputVerifierAcceptsItsDeclaredDimensionTolerance()
    {
        using var temp = new TempDirectory();
        string output = temp.File("output.pdf");
        SamplePdf.Create(output, (200, 300, 0));

        OutputVerifier.Verify(
            output,
            [new PageSize(200 - OutputVerifier.DimensionTolerancePoints, 300)],
            password: null
        );
        OutputVerifier.Verify(
            output,
            [new PageSize(200, 300 - OutputVerifier.DimensionTolerancePoints)],
            password: null
        );
    }

    [Fact]
    public void SensitiveMemoryIsZeroedAndClosedOnDispose()
    {
        var stream = new SensitiveMemoryStream();
        stream.Write([1, 2, 3, 4]);
        Assert.True(stream.TryGetBuffer(out ArraySegment<byte> buffer));

        stream.Dispose();

        Assert.Equal([0, 0, 0, 0], buffer.AsSpan(0, 4).ToArray());
        Assert.Throws<ObjectDisposedException>(() => stream.WriteByte(1));
    }

    [Fact]
    public void SourceValidatesPageBoundsAndInvalidRotation()
    {
        using var temp = new TempDirectory();
        string input = temp.File("input.pdf");
        string invalid = temp.File("invalid.pdf");
        SamplePdf.Create(input, (200, 300, 0));
        SamplePdf.CreateCustomized(invalid, (_, page) => page.Elements.SetInteger("/Rotate", 45));
        using PdfSource source = PdfSource.Open(input, password: null);

        Assert.Equal(
            "page-index-out-of-range",
            Assert.Throws<PdfSpreadException>(() => source.GetPageSize(1)).TechnicalDetail
        );
        Assert.Equal(
            "page-index-out-of-range",
            Assert.Throws<PdfSpreadException>(() => source.SelectPage(1)).TechnicalDetail
        );
        Assert.Equal(
            "page-rotation-invalid",
            Assert.Throws<PdfSpreadException>(() => PdfSource.Open(invalid, null)).TechnicalDetail
        );
    }

    [Fact]
    public void SourceRejectsDimensionsOutsideTheStaticPageBudget()
    {
        using var temp = new TempDirectory();
        string input = temp.File("oversized.pdf");
        SamplePdf.Create(input, (14_401, 300, 0));

        PdfSpreadException error = Assert.Throws<PdfSpreadException>(() =>
            PdfSource.Open(input, password: null)
        );

        Assert.Equal(PdfSpreadError.InvalidPage, error.Error);
        Assert.Equal("page-size-invalid", error.TechnicalDetail);
    }

    [Fact]
    public void SourceAcceptsBoundaryDimensionsAndFallsBackFromAnEmptyCropBox()
    {
        using var temp = new TempDirectory();
        string maximumWidth = temp.File("maximum-width.pdf");
        string maximumHeight = temp.File("maximum-height.pdf");
        string emptyCrop = temp.File("empty-crop.pdf");
        SamplePdf.Create(maximumWidth, (14_400, 300, 0));
        SamplePdf.Create(maximumHeight, (300, 14_400, 0));
        SamplePdf.CreateWithRawCropBox(emptyCrop, "0 0 0 0");

        using PdfSource width = PdfSource.Open(maximumWidth, password: null);
        using PdfSource height = PdfSource.Open(maximumHeight, password: null);
        using PdfSource crop = PdfSource.Open(emptyCrop, password: null);

        Assert.Equal(14_400, width.GetPageSize(0).Width);
        Assert.Equal(14_400, height.GetPageSize(0).Height);
        Assert.Equal(new PageSize(200, 300), crop.GetPageSize(0));
        Assert.False(width.WasPasswordProtected);
    }

    [Theory]
    [InlineData("10 20 110 220", 100, 200)]
    [InlineData("10 20 10 220", 200, 300)]
    [InlineData("10 20 110 20", 200, 300)]
    public void SourceUsesOnlyAValidTwoDimensionalCropBox(
        string cropBox,
        double expectedWidth,
        double expectedHeight
    )
    {
        using var temp = new TempDirectory();
        string input = temp.File("crop.pdf");
        SamplePdf.CreateWithRawCropBox(input, cropBox);

        using PdfSource source = PdfSource.Open(input, password: null);

        Assert.Equal(new PageSize(expectedWidth, expectedHeight), source.GetPageSize(0));
    }

    [Theory]
    [InlineData("0 0 0 300")]
    [InlineData("0 0 200 0")]
    public void SourceRejectsEachNonPositiveMediaBoxDimension(string mediaBox)
    {
        using var temp = new TempDirectory();
        string input = temp.File("invalid-dimension.pdf");
        SamplePdf.CreateWithRawMediaBox(input, mediaBox);

        PdfSpreadException error = Assert.Throws<PdfSpreadException>(() =>
            PdfSource.Open(input, password: null)
        );

        Assert.Equal("page-size-invalid", error.TechnicalDetail);
    }

    [Fact]
    public void SourceAllowsEmptyAnnotationsAndDefaultUserUnits()
    {
        using var temp = new TempDirectory();
        string annotations = temp.File("empty-annotations.pdf");
        string userUnit = temp.File("default-user-unit.pdf");
        SamplePdf.CreateCustomized(
            annotations,
            (document, page) => page.Elements["/Annots"] = new PdfArray(document)
        );
        SamplePdf.CreateCustomized(userUnit, (_, page) => page.Elements.SetReal("/UserUnit", 1));

        using PdfSource annotationsSource = PdfSource.Open(annotations, password: null);
        using PdfSource userUnitSource = PdfSource.Open(userUnit, password: null);

        Assert.Equal(1, annotationsSource.PageCount);
        Assert.Equal(1, userUnitSource.PageCount);
    }

    [Fact]
    public void PaginationAndGeometryEnforceEveryInvariant()
    {
        Assert.Throws<PdfSpreadException>(() => new PageSize(double.NaN, 1));
        Assert.Throws<PdfSpreadException>(() => new PageSize(1, 0));
        Assert.Throws<PdfSpreadException>(() => PageGroup.Pair(1, 1));
        Assert.Throws<PdfSpreadException>(() =>
            SpreadLayout.Single(new PageSize(1, 1), (SpreadHalf)99)
        );
        Assert.Throws<PdfSpreadException>(() => Pagination.Count((FirstPageMode)99, 1));
        Assert.Throws<PdfSpreadException>(() => Pagination.Count(FirstPageMode.Standard, 0));

        Assert.Equal(2, Pagination.Count(FirstPageMode.Standard, 3));
        Assert.Equal(3, Pagination.Count(FirstPageMode.Cover, 4));
    }

    [Fact]
    public void InternalValueObjectsNeverExposePathsOrPasswords()
    {
        using var temp = new TempDirectory();
        string input = temp.File("private-input.pdf");
        string output = temp.File("private-output.pdf");
        var request = new PdfSpreadRequest(input, output, FirstPageMode.Cover, "top-secret");

        string text = request.ToString();

        Assert.DoesNotContain(input, text, StringComparison.Ordinal);
        Assert.DoesNotContain(output, text, StringComparison.Ordinal);
        Assert.DoesNotContain("top-secret", text, StringComparison.Ordinal);
        Assert.Contains(nameof(FirstPageMode.Cover), text, StringComparison.Ordinal);
    }

    [Fact]
    public void ProgressAndResultRejectInvalidValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfSpreadProgress(0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfSpreadProgress(2, 1));
        Assert.Throws<ArgumentException>(() => new PdfSpreadResult("relative.pdf", 1, 1));

        var progress = new PdfSpreadProgress(1, 2);
        Assert.Equal(0.5, progress.Fraction);
    }
}
