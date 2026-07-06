using PdfSharp.Pdf;
using TateYoko.Core.Domain;
using TateYoko.Core.Ports;

namespace TateYoko.Pdf.Tests;

/// <summary>
/// Tests the PDFsharp source adapter (via <see cref="PdfSharpEngine.OpenSource"/>): rotation-adjusted
/// dimensions, CropBox preference, metadata reading, content caching, and error mapping.
/// </summary>
public class PdfSharpSourceDocumentTests : IDisposable
{
    private const double Eps = 0.5;

    private readonly TempDir _dir = new();
    private readonly PdfSharpEngine _engine = new();

    public sealed class Dimensions : PdfSharpSourceDocumentTests
    {
        [Theory]
        [InlineData(0, 200, 400)]
        [InlineData(180, 200, 400)] // 180 keeps orientation
        [InlineData(90, 400, 200)] // 90/270 swap width and height
        [InlineData(270, 400, 200)]
        public void AppliesRotationToDisplaySize(int rotate, double expectedW, double expectedH)
        {
            string path = _dir.File("rot.pdf");
            SamplePdf.Create(path, [(200, 400, rotate)]);

            using ISourceDocument src = _engine.OpenSource(path);
            PageDimension dim = src.GetPageDimension(0);

            Assert.Equal(expectedW, dim.WidthPt, Eps);
            Assert.Equal(expectedH, dim.HeightPt, Eps);
        }

        [Fact]
        public void PrefersCropBoxOverMediaBox()
        {
            string path = _dir.File("crop.pdf");
            SamplePdf.CreateWithCropBox(path, mediaW: 400, mediaH: 600, cropW: 300, cropH: 500);

            using ISourceDocument src = _engine.OpenSource(path);
            PageDimension dim = src.GetPageDimension(0);

            Assert.Equal(300, dim.WidthPt, Eps);
            Assert.Equal(500, dim.HeightPt, Eps);
        }

        [Fact]
        public void FallsBackToMediaBoxWhenNoCropBox()
        {
            string path = _dir.File("media.pdf");
            SamplePdf.CreatePortrait(path, 1, widthPt: 250, heightPt: 350);

            using ISourceDocument src = _engine.OpenSource(path);
            PageDimension dim = src.GetPageDimension(0);

            Assert.Equal(250, dim.WidthPt, Eps);
            Assert.Equal(350, dim.HeightPt, Eps);
        }

        [Fact]
        public void CombinesCropBoxWithRotation()
        {
            string path = _dir.File("crop-rot.pdf");
            // CropBox 300x500 wins over MediaBox, then a 90-degree rotation swaps it to 500x300.
            SamplePdf.CreateWithCropBox(path, mediaW: 400, mediaH: 600, cropW: 300, cropH: 500, rotate: 90);

            using ISourceDocument src = _engine.OpenSource(path);
            PageDimension dim = src.GetPageDimension(0);

            Assert.Equal(500, dim.WidthPt, Eps);
            Assert.Equal(300, dim.HeightPt, Eps);
        }
    }

    public sealed class PageCountAndContent : PdfSharpSourceDocumentTests
    {
        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(10)]
        public void ReportsPageCount(int count)
        {
            string path = _dir.File("count.pdf");
            SamplePdf.CreatePortrait(path, count);

            using ISourceDocument src = _engine.OpenSource(path);
            Assert.Equal(count, src.PageCount);
        }

        [Fact]
        public void CachesPageContentPerIndex()
        {
            string path = _dir.File("content.pdf");
            SamplePdf.CreatePortrait(path, 3);

            using ISourceDocument src = _engine.OpenSource(path);
            IPageContent first = src.GetPageContent(0);

            Assert.Same(first, src.GetPageContent(0));
            Assert.NotSame(first, src.GetPageContent(1));
        }
    }

    public sealed class Metadata : PdfSharpSourceDocumentTests
    {
        [Fact]
        public void ReadsPopulatedFields()
        {
            string path = _dir.File("meta.pdf");
            var created = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            SamplePdf.CreateWithMetadata(path, info =>
            {
                info.Title = "My Book";
                info.Author = "Jane";
                info.Subject = "Reading";
                info.Keywords = "vertical, rtl";
                info.Creator = "Scanner";
                info.CreationDate = created;
            });

            using ISourceDocument src = _engine.OpenSource(path);
            DocumentMetadata meta = src.Metadata;

            Assert.Equal("My Book", meta.Title);
            Assert.Equal("Jane", meta.Author);
            Assert.Equal("Reading", meta.Subject);
            Assert.Equal("vertical, rtl", meta.Keywords);
            Assert.Equal("Scanner", meta.Creator);
            Assert.Equal(created, meta.CreationDate);
        }

        [Fact]
        public void MapsEmptyFieldsToNull()
        {
            string path = _dir.File("nometa.pdf");
            SamplePdf.CreatePortrait(path, 1);

            using ISourceDocument src = _engine.OpenSource(path);
            DocumentMetadata meta = src.Metadata;

            Assert.Null(meta.Title);
            Assert.Null(meta.Author);
            Assert.Null(meta.Subject);
            Assert.Null(meta.Keywords);
        }
    }

    public sealed class Errors : PdfSharpSourceDocumentTests
    {
        [Fact]
        public void MissingFileMapsToNotFound()
        {
            var ex = Assert.Throws<SpreadException>(() => _engine.OpenSource(_dir.File("nope.pdf")));
            Assert.Equal(ErrorKind.PdfNotFound, ex.Kind);
        }

        [Fact]
        public void CorruptedFileMapsToCorrupted()
        {
            string path = _dir.File("broken.pdf");
            SamplePdf.CreateCorrupted(path);

            var ex = Assert.Throws<SpreadException>(() => _engine.OpenSource(path));
            Assert.Equal(ErrorKind.PdfCorrupted, ex.Kind);
        }

        [Fact]
        public void PasswordProtectedMapsToPasswordProtected()
        {
            string path = _dir.File("locked.pdf");
            SamplePdf.CreateEncrypted(path, "secret");

            var ex = Assert.Throws<SpreadException>(() => _engine.OpenSource(path));
            Assert.Equal(ErrorKind.PdfPasswordProtected, ex.Kind);
        }
    }

    public sealed class Lifetime : PdfSharpSourceDocumentTests
    {
        [Fact]
        public void DisposeIsIdempotent()
        {
            string path = _dir.File("dispose.pdf");
            SamplePdf.CreatePortrait(path, 1);

            ISourceDocument src = _engine.OpenSource(path);
            src.Dispose();
            src.Dispose(); // must not throw
        }
    }

    public void Dispose() => _dir.Dispose();
}
