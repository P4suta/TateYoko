using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.Annotations;
using PdfSharp.Pdf.IO;
using TateYoko.Engine.Internal;

namespace TateYoko.Engine.Tests;

public sealed class PdfFeatureBoundaryTests
{
    [Fact]
    public void ConvertsEveryNamedDestinationForm()
    {
        using var directory = new TempDirectory();
        string input = directory.File("input.pdf");
        string output = directory.File("output.pdf");
        SamplePdf.CreateWithEveryNamedDestination(input);

        new PdfSpreadConverter().Convert(
            new PdfSpreadRequest(
                input,
                output,
                FirstPageMode.Standard,
                OutputCollisionPolicy.CreateUnique
            ),
            cancellationToken: TestContext.Current.CancellationToken
        );

        using PdfDocument converted = PdfReader.Open(output, PdfDocumentOpenMode.Import);
        string[] expected =
        [
            "fit",
            "fit-bounding-box",
            "fit-bounding-box-horizontal",
            "fit-bounding-box-horizontal-null",
            "fit-bounding-box-vertical",
            "fit-bounding-box-vertical-null",
            "fit-horizontal",
            "fit-horizontal-null",
            "fit-rectangle",
            "fit-vertical",
            "fit-vertical-null",
            "position",
        ];
        Assert.Equal(
            expected,
            converted.Internals.Catalog.Names.NameTree!.GetNames(true).Order(StringComparer.Ordinal)
        );
    }

    [Fact]
    public void RejectsUnsupportedInformationPageModesAndViewerPreferences()
    {
        using var directory = new TempDirectory();
        AssertOpenFailure(
            directory,
            "information.pdf",
            (document, _) => document.Info.Elements.SetString("/Private", "value"),
            PdfSpreadError.UnsupportedPdfFeature,
            "unsupported-document-information"
        );
        AssertOpenFailure(
            directory,
            "page-mode.pdf",
            (document, _) => document.PageMode = PdfPageMode.UseAttachments,
            PdfSpreadError.UnsupportedPdfFeature,
            "unsupported-page-mode"
        );
        AssertOpenFailure(
            directory,
            "viewer-preference.pdf",
            (document, _) =>
                document.ViewerPreferences.Elements.SetName(
                    "/NonFullScreenPageMode",
                    "/UseOutlines"
                ),
            PdfSpreadError.UnsupportedPdfFeature,
            "unsupported-viewer-preference"
        );
    }

    [Fact]
    public void RejectsMalformedAnnotationsMetadataAndViewerDirection()
    {
        using var directory = new TempDirectory();
        AssertOpenFailure(
            directory,
            "annotation.pdf",
            (document, page) =>
            {
                var annotations = new PdfArray(document);
                annotations.Elements.Add(new PdfDictionary(document));
                page.Elements["/Annots"] = annotations;
            },
            PdfSpreadError.CorruptedPdf,
            "annotation-subtype-missing"
        );
        string missingStream = directory.File("metadata.pdf");
        SamplePdf.CreateWithMetadataDictionaryWithoutStream(missingStream);
        PdfSpreadException metadataError = Assert.Throws<PdfSpreadException>(() =>
            PdfSource.Open(missingStream, password: null)
        );
        Assert.Equal(PdfSpreadError.CorruptedPdf, metadataError.Error);
        Assert.Equal("metadata-stream-missing", metadataError.TechnicalDetail);

        string invalidDirection = directory.File("viewer-direction.pdf");
        SamplePdf.CreateCustomized(
            invalidDirection,
            (document, _) => document.ViewerPreferences.Elements.SetName("/Direction", "/Diagonal")
        );
        using PdfSource directionSource = PdfSource.Open(invalidDirection, password: null);
        using var directionDestination = new PdfDocument();
        PdfSpreadException directionError = Assert.Throws<PdfSpreadException>(() =>
            directionSource.CopyMetadataTo(directionDestination)
        );
        Assert.Equal(PdfSpreadError.CorruptedPdf, directionError.Error);
        Assert.Equal("viewer-direction-invalid", directionError.TechnicalDetail);

        string invalidXmp = directory.File("invalid-xmp.pdf");
        SamplePdf.CreateWithInvalidXmp(invalidXmp);
        PdfSpreadException xmpError = Assert.Throws<PdfSpreadException>(() =>
            PdfSource.Open(invalidXmp, password: null)
        );
        Assert.Equal(PdfSpreadError.CorruptedPdf, xmpError.Error);
        Assert.Equal("metadata-xml-invalid", xmpError.TechnicalDetail);
    }

    [Fact]
    public void AllowsAnExplicitDefaultUserUnit()
    {
        using var directory = new TempDirectory();
        string input = directory.File("input.pdf");
        SamplePdf.CreateCustomized(input, (_, page) => page.Elements.SetReal("/UserUnit", 1));

        using PdfSource source = PdfSource.Open(input, password: null);

        Assert.Equal(1, source.PageCount);
    }

    [Fact]
    public void CanonicalizesExplicitLeftToRightViewerDirectionToDefault()
    {
        using var directory = new TempDirectory();
        string input = directory.File("left-to-right.pdf");
        SamplePdf.CreateCustomized(
            input,
            (document, _) => document.ViewerPreferences.Elements.SetName("/Direction", "/L2R")
        );
        using PdfSource source = PdfSource.Open(input, password: null);
        using var destination = new PdfDocument();

        source.CopyMetadataTo(destination);

        Assert.Null(destination.ViewerPreferences.Direction);
    }

    [Theory]
    [InlineData("/FitH", true, "link-destination-parameters-missing")]
    [InlineData("/Unknown", false, "link-destination-type-invalid")]
    public void RejectsMalformedNamedDestinations(
        string destinationType,
        bool omitParameters,
        string expectedDetail
    )
    {
        using var directory = new TempDirectory();
        string input = directory.File("input.pdf");
        string output = directory.File("output.pdf");
        SamplePdf.CreateWithMalformedNamedDestination(input, destinationType, omitParameters);

        PdfSpreadException exception = Assert.Throws<PdfSpreadException>(() =>
            new PdfSpreadConverter().Convert(
                new PdfSpreadRequest(
                    input,
                    output,
                    FirstPageMode.Standard,
                    OutputCollisionPolicy.CreateUnique
                ),
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(PdfSpreadError.CorruptedPdf, exception.Error);
        Assert.Equal(expectedDetail, exception.TechnicalDetail);
    }

    [Fact]
    public void AnnotationCopyObservesCancellation()
    {
        using var directory = new TempDirectory();
        string input = directory.File("input.pdf");
        SamplePdf.CreateWithLinks(input);
        using PdfSource source = PdfSource.Open(input, password: null);
        using var destination = new PdfDocument();
        var projections = new PageProjection?[source.PageCount];
        for (int index = 0; index < source.PageCount; index++)
        {
            PageSize size = source.GetPageSize(index);
            PdfPage page = destination.AddPage();
            page.Width = XUnit.FromPoint(size.Width);
            page.Height = XUnit.FromPoint(size.Height);
            projections[index] = source.CreateProjection(index, page, new Point(0, 0), size);
        }

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            source.CopyAnnotationsTo(destination, projections, cancellation.Token)
        );
    }

    [Fact]
    public void DestinationMappingRejectsMalformedInternalStructures()
    {
        using var sourceDocument = new PdfDocument();
        PdfPage sourcePage = sourceDocument.AddPage();
        sourcePage.Width = XUnit.FromPoint(200);
        sourcePage.Height = XUnit.FromPoint(300);
        using var destination = new PdfDocument();
        PdfPage destinationPage = destination.AddPage();
        destinationPage.Width = XUnit.FromPoint(400);
        destinationPage.Height = XUnit.FromPoint(300);
        var projection = new PageProjection(
            destinationPage,
            new Point(0, 0),
            new PageSize(200, 300),
            sourcePage.MediaBox,
            0
        );
        var sourcePageIndices = new Dictionary<PdfObjectID, int>
        {
            [sourcePage.ReferenceNotNull.ObjectID] = 0,
        };
        var malformedSource = new PdfArray(sourceDocument);
        malformedSource.Elements.Add(new PdfInteger(0));
        malformedSource.Elements.Add(new PdfName("/Fit"));

        Assert.Equal(
            "link-destination-invalid",
            Assert
                .Throws<PdfSpreadException>(() =>
                    PdfSource.MapDestination(malformedSource, sourcePageIndices, [projection])
                )
                .TechnicalDetail
        );

        var emptyMapped = new PdfArray(destination);
        Assert.Equal(
            "mapped-destination-page-missing",
            Assert
                .Throws<PdfSpreadException>(() =>
                    PdfSource.FindDestinationPageNumber(destination, emptyMapped)
                )
                .TechnicalDetail
        );

        var nonPage = new PdfDictionary(destination);
        destination.Internals.AddObject(nonPage);
        var foreignMapped = new PdfArray(destination);
        foreignMapped.Elements.Add(nonPage.ReferenceNotNull);
        Assert.Equal(
            "mapped-destination-page-invalid",
            Assert
                .Throws<PdfSpreadException>(() =>
                    PdfSource.FindDestinationPageNumber(destination, foreignMapped)
                )
                .TechnicalDetail
        );

        Assert.Equal(
            "mapped-destination-invalid",
            Assert
                .Throws<PdfSpreadException>(() =>
                    PdfSource.CreateNamedDestinationParameters(emptyMapped)
                )
                .TechnicalDetail
        );
        var unknownMapped = new PdfArray(destination);
        unknownMapped.Elements.Add(destinationPage.ReferenceNotNull);
        unknownMapped.Elements.Add(new PdfName("/Unknown"));
        Assert.Equal(
            "mapped-destination-type-invalid",
            Assert
                .Throws<PdfSpreadException>(() =>
                    PdfSource.CreateNamedDestinationParameters(unknownMapped)
                )
                .TechnicalDetail
        );
    }

    [Fact]
    public void RotatedDestinationFallsBackToSafeFitMapping()
    {
        using var sourceDocument = new PdfDocument();
        PdfPage sourcePage = sourceDocument.AddPage();
        sourcePage.Width = XUnit.FromPoint(200);
        sourcePage.Height = XUnit.FromPoint(300);
        using var destination = new PdfDocument();
        PdfPage destinationPage = destination.AddPage();
        destinationPage.Width = XUnit.FromPoint(400);
        destinationPage.Height = XUnit.FromPoint(300);
        var rotated = new PageProjection(
            destinationPage,
            new Point(0, 0),
            new PageSize(300, 200),
            sourcePage.MediaBox,
            90
        );
        var sourceDestination = new PdfArray(sourceDocument);
        sourceDestination.Elements.Add(sourcePage.ReferenceNotNull);
        sourceDestination.Elements.Add(new PdfName("/XYZ"));
        sourceDestination.Elements.Add(new PdfReal(10));
        sourceDestination.Elements.Add(new PdfReal(250));
        sourceDestination.Elements.Add(new PdfReal(1.5));

        PdfArray mapped = PdfSource.MapDestination(
            sourceDestination,
            new Dictionary<PdfObjectID, int> { [sourcePage.ReferenceNotNull.ObjectID] = 0 },
            [rotated]
        );

        Assert.Equal("/Fit", mapped.Elements.GetName(1));
        Assert.Equal(2, mapped.Elements.Count);
    }

    [Fact]
    public void ConverterRejectsMissingFilesInvalidCharactersAndPageRotations()
    {
        using var directory = new TempDirectory();
        var converter = new PdfSpreadConverter();
        var missing = new PdfSpreadRequest(
            directory.File("missing.pdf"),
            directory.File("output.pdf"),
            FirstPageMode.Standard,
            OutputCollisionPolicy.CreateUnique
        );
        PdfSpreadException missingError = Assert.Throws<PdfSpreadException>(() =>
            converter.Convert(missing, cancellationToken: TestContext.Current.CancellationToken)
        );
        Assert.Equal(PdfSpreadError.InputNotFound, missingError.Error);

        string validInput = directory.File("valid.pdf");
        SamplePdf.Create(validInput, (200, 300, 0));
        var invalidOutput = new PdfSpreadRequest(
            validInput,
            directory.File("output.txt"),
            FirstPageMode.Standard,
            OutputCollisionPolicy.CreateUnique
        );
        PdfSpreadException outputError = Assert.Throws<PdfSpreadException>(() =>
            converter.Convert(
                invalidOutput,
                cancellationToken: TestContext.Current.CancellationToken
            )
        );
        Assert.Equal(PdfSpreadError.InvalidRequest, outputError.Error);

        var invalid = new PdfSpreadRequest(
            "C:\\invalid\0.pdf",
            directory.File("output.pdf"),
            FirstPageMode.Standard,
            OutputCollisionPolicy.CreateUnique
        );
        PdfSpreadException invalidError = Assert.Throws<PdfSpreadException>(() =>
            converter.Convert(invalid, cancellationToken: TestContext.Current.CancellationToken)
        );
        Assert.Equal(PdfSpreadError.InvalidRequest, invalidError.Error);
        Assert.IsType<ArgumentException>(invalidError.InnerException);

        string rotatedInput = directory.File("rotated.pdf");
        SamplePdf.CreateCustomized(
            rotatedInput,
            (_, page) => page.Elements.SetInteger("/Rotate", 45)
        );
        var rotated = new PdfSpreadRequest(
            rotatedInput,
            directory.File("rotated-output.pdf"),
            FirstPageMode.Standard,
            OutputCollisionPolicy.CreateUnique
        );
        PdfSpreadException rotationError = Assert.Throws<PdfSpreadException>(() =>
            converter.Convert(rotated, cancellationToken: TestContext.Current.CancellationToken)
        );
        Assert.Equal(PdfSpreadError.InvalidPage, rotationError.Error);
        Assert.Equal("page-rotation-invalid", rotationError.TechnicalDetail);
    }

    [Fact]
    public void ConvertsLegacyCatalogNamedDestinations()
    {
        using var directory = new TempDirectory();
        string input = directory.File("legacy.pdf");
        string output = directory.File("output.pdf");
        SamplePdf.CreateWithLegacyNamedDestination(input);

        new PdfSpreadConverter().Convert(
            new PdfSpreadRequest(
                input,
                output,
                FirstPageMode.Standard,
                OutputCollisionPolicy.CreateUnique
            ),
            cancellationToken: TestContext.Current.CancellationToken
        );

        using PdfDocument converted = PdfReader.Open(output, PdfDocumentOpenMode.Import);
        Assert.Contains("legacy", converted.Internals.Catalog.Names.NameTree!.GetNames(true));
    }

    [Fact]
    public void RejectsLinksWithConflictingDestinationRepresentations()
    {
        using var directory = new TempDirectory();
        string input = directory.File("conflicting-link.pdf");
        string output = directory.File("output.pdf");
        SamplePdf.CreateWithConflictingLinkDestination(input);

        PdfSpreadException exception = Assert.Throws<PdfSpreadException>(() =>
            new PdfSpreadConverter().Convert(
                new PdfSpreadRequest(
                    input,
                    output,
                    FirstPageMode.Standard,
                    OutputCollisionPolicy.CreateUnique
                ),
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(PdfSpreadError.CorruptedPdf, exception.Error);
        Assert.Equal("link-has-dest-and-action", exception.TechnicalDetail);
        Assert.False(File.Exists(output));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RejectsEmptyAndIncompleteLinkQuadPoints(bool empty)
    {
        using var directory = new TempDirectory();
        string input = directory.File("quadpoints.pdf");
        string output = directory.File("output.pdf");
        SamplePdf.CreateWithInvalidQuadPoints(input, empty);

        PdfSpreadException exception = Assert.Throws<PdfSpreadException>(() =>
            new PdfSpreadConverter().Convert(
                new PdfSpreadRequest(
                    input,
                    output,
                    FirstPageMode.Standard,
                    OutputCollisionPolicy.CreateUnique
                ),
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(PdfSpreadError.CorruptedPdf, exception.Error);
        Assert.Equal("link-quadpoints-invalid", exception.TechnicalDetail);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void ResolvesPdfNameLinkDestinations()
    {
        using var directory = new TempDirectory();
        string input = directory.File("name-link.pdf");
        string output = directory.File("output.pdf");
        SamplePdf.CreateWithPdfNameDestinationLink(input);

        new PdfSpreadConverter().Convert(
            new PdfSpreadRequest(
                input,
                output,
                FirstPageMode.Standard,
                OutputCollisionPolicy.CreateUnique
            ),
            cancellationToken: TestContext.Current.CancellationToken
        );

        using PdfDocument converted = PdfReader.Open(output, PdfDocumentOpenMode.Import);
        Assert.Single(converted.Pages[0].Annotations);
    }

    private static void AssertOpenFailure(
        TempDirectory directory,
        string fileName,
        Action<PdfDocument, PdfPage> customize,
        PdfSpreadError expectedError,
        string expectedDetail
    )
    {
        string path = directory.File(fileName);
        SamplePdf.CreateCustomized(path, customize);

        PdfSpreadException exception = Assert.Throws<PdfSpreadException>(() =>
            PdfSource.Open(path, password: null)
        );

        Assert.Equal(expectedError, exception.Error);
        Assert.Equal(expectedDetail, exception.TechnicalDetail);
    }
}
