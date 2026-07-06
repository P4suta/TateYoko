using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TateYoko.Core.Application;
using TateYoko.Core.Domain;
using TateYoko.Core.Ports;

namespace TateYoko.Core.Tests;

/// <summary>
/// Orchestration tests for <see cref="SpreadConversionService"/> using faked ports. Verifies the
/// use case's invariants — open/paginate/write/metadata/save order, per-spread progress, page
/// placement, disposal, and error propagation — without any real PDF library.
/// </summary>
public sealed class SpreadConversionServiceTests
{
    private const float Eps = 0.001f;

    /// <summary>A faked engine wired to a source of <paramref name="pageCount"/> equal pages.</summary>
    private static Fixture NewFixture(int pageCount, PageDimension? pageSize = null)
    {
        PageDimension size = pageSize ?? new PageDimension(100f, 200f);

        var contents = new IPageContent[pageCount];
        for (int i = 0; i < pageCount; i++)
        {
            contents[i] = Substitute.For<IPageContent>();
        }

        var metadata = new DocumentMetadata(Title: "T", Author: "A");

        var source = Substitute.For<ISourceDocument>();
        source.PageCount.Returns(pageCount);
        source.Metadata.Returns(metadata);
        source.GetPageDimension(Arg.Any<int>()).Returns(size);
        source.GetPageContent(Arg.Any<int>())
            .Returns(ci => contents[ci.Arg<int>()]);

        var writer = Substitute.For<ISpreadWriter>();
        var spreads = new List<(SpreadSpec Spec, IReadOnlyList<PagePlacement> Placements)>();
        writer
            .When(w => w.AddSpread(Arg.Any<SpreadSpec>(), Arg.Any<IReadOnlyList<PagePlacement>>()))
            .Do(ci => spreads.Add((ci.Arg<SpreadSpec>(), ci.Arg<IReadOnlyList<PagePlacement>>())));

        var engine = Substitute.For<IPdfEngine>();
        engine.OpenSource(Arg.Any<string>()).Returns(source);
        engine.CreateWriter().Returns(writer);

        return new Fixture(engine, source, writer, contents, metadata, spreads);
    }

    private sealed record Fixture(
        IPdfEngine Engine,
        ISourceDocument Source,
        ISpreadWriter Writer,
        IPageContent[] Contents,
        DocumentMetadata Metadata,
        List<(SpreadSpec Spec, IReadOnlyList<PagePlacement> Placements)> Spreads)
    {
        public SpreadConversionService Service => new(Engine);
    }

    /// <summary>Records progress callbacks for inspection.</summary>
    private sealed class RecordingProgress : IConversionProgress
    {
        public List<(int Completed, int Total)> Calls { get; } = [];

        public void Report(int completedSpreads, int totalSpreads) =>
            Calls.Add((completedSpreads, totalSpreads));
    }

    public sealed class Orchestration
    {
        [Fact]
        public void OpensSourceWithRequestedInputPath()
        {
            Fixture f = NewFixture(2);
            f.Service.Convert(new SpreadRequest("in.pdf", "out.pdf", FirstPageMode.Standard));
            f.Engine.Received(1).OpenSource("in.pdf");
        }

        [Theory]
        [InlineData(4, FirstPageMode.Standard, 2)]
        [InlineData(5, FirstPageMode.Standard, 3)]
        [InlineData(1, FirstPageMode.Standard, 1)]
        [InlineData(5, FirstPageMode.Cover, 3)]
        [InlineData(6, FirstPageMode.Cover, 4)]
        [InlineData(2, FirstPageMode.Cover, 2)]
        [InlineData(5, FirstPageMode.LeadingBlank, 3)]
        [InlineData(1, FirstPageMode.LeadingBlank, 1)]
        public void WritesOneSpreadPerPaginationEntry(int pages, FirstPageMode mode, int expected)
        {
            Fixture f = NewFixture(pages);

            f.Service.Convert(new SpreadRequest("in.pdf", "out.pdf", mode));

            int paginated = Pagination.Paginate(mode, pages).Count;
            Assert.Equal(expected, paginated);
            Assert.Equal(expected, f.Spreads.Count);
        }

        [Fact]
        public void SavesToRequestedOutputPath()
        {
            Fixture f = NewFixture(3);
            f.Service.Convert(new SpreadRequest("in.pdf", @"C:\out\book_spread.pdf", FirstPageMode.Standard));
            f.Writer.Received(1).Save(@"C:\out\book_spread.pdf");
        }

        [Fact]
        public void AppliesSourceMetadataExactlyOnce()
        {
            Fixture f = NewFixture(3);
            f.Service.Convert(new SpreadRequest("in.pdf", "out.pdf", FirstPageMode.Standard));
            f.Writer.Received(1).ApplyMetadata(f.Metadata);
        }

        /// <summary>Every spread must be drawn before metadata is applied and the document is saved.</summary>
        [Fact]
        public void OrdersAllSpreadsThenMetadataThenSave()
        {
            Fixture f = NewFixture(5);

            f.Service.Convert(new SpreadRequest("in.pdf", "out.pdf", FirstPageMode.Standard));

            string[] calls = f.Writer.ReceivedCalls()
                .Select(c => c.GetMethodInfo().Name)
                .Where(n => n is nameof(ISpreadWriter.AddSpread)
                    or nameof(ISpreadWriter.ApplyMetadata)
                    or nameof(ISpreadWriter.Save))
                .ToArray();

            Assert.Equal("Save", calls[^1]);
            Assert.Equal("ApplyMetadata", calls[^2]);
            Assert.All(calls[..^2], name => Assert.Equal("AddSpread", name));
            Assert.Equal(3, calls.Count(n => n == "AddSpread"));
        }
    }

    public sealed class Placement
    {
        [Fact]
        public void PairPlacesFirstPageOnRightAndSecondOnLeft()
        {
            Fixture f = NewFixture(2);

            f.Service.Convert(new SpreadRequest("in.pdf", "out.pdf", FirstPageMode.Standard));

            (SpreadSpec spec, IReadOnlyList<PagePlacement> placements) = f.Spreads.Single();
            Assert.Equal(2, placements.Count);

            // Frame is two equal pages wide (100*2), full height.
            Assert.Equal(200f, spec.WidthPt, Eps);
            Assert.Equal(200f, spec.HeightPt, Eps);

            // placements[0] is page 0's content on the right (leading) half; [1] is page 1 on the left.
            Assert.Same(f.Contents[0], placements[0].Content);
            Assert.Same(f.Contents[1], placements[1].Content);
            Assert.Equal(100f, placements[0].Position.OffsetXPt, Eps);
            Assert.Equal(0f, placements[1].Position.OffsetXPt, Eps);
            Assert.True(placements[0].Position.OffsetXPt > placements[1].Position.OffsetXPt);
        }

        [Fact]
        public void OddTrailingPageIsASingleLeadingPlacement()
        {
            Fixture f = NewFixture(3);

            f.Service.Convert(new SpreadRequest("in.pdf", "out.pdf", FirstPageMode.Standard));

            (SpreadSpec _, IReadOnlyList<PagePlacement> last) = f.Spreads[^1];
            PagePlacement single = Assert.Single(last);
            Assert.Same(f.Contents[2], single.Content);
            Assert.Equal(100f, single.Position.OffsetXPt, Eps); // leading (right) half
        }

        [Fact]
        public void CoverPageIsASingleLeadingPlacement()
        {
            Fixture f = NewFixture(5);

            f.Service.Convert(new SpreadRequest("in.pdf", "out.pdf", FirstPageMode.Cover));

            (SpreadSpec _, IReadOnlyList<PagePlacement> first) = f.Spreads[0];
            PagePlacement cover = Assert.Single(first);
            Assert.Same(f.Contents[0], cover.Content);
            Assert.Equal(100f, cover.Position.OffsetXPt, Eps);
        }

        [Fact]
        public void LeadingBlankPutsFirstPageOnTheLeft()
        {
            Fixture f = NewFixture(5);

            f.Service.Convert(new SpreadRequest("in.pdf", "out.pdf", FirstPageMode.LeadingBlank));

            (SpreadSpec _, IReadOnlyList<PagePlacement> first) = f.Spreads[0];
            PagePlacement blank = Assert.Single(first);
            Assert.Same(f.Contents[0], blank.Content);
            Assert.Equal(0f, blank.Position.OffsetXPt, Eps); // trailing (left) half
        }

        [Fact]
        public void FrameUsesMaxBoundsAcrossThePair()
        {
            Fixture f = NewFixture(2);
            f.Source.GetPageDimension(0).Returns(new PageDimension(200f, 400f));
            f.Source.GetPageDimension(1).Returns(new PageDimension(240f, 300f));

            f.Service.Convert(new SpreadRequest("in.pdf", "out.pdf", FirstPageMode.Standard));

            SpreadSpec spec = f.Spreads.Single().Spec;
            Assert.Equal(480f, spec.WidthPt, Eps); // max(200,240) * 2
            Assert.Equal(400f, spec.HeightPt, Eps); // max(400,300)
        }
    }

    public sealed class Progress
    {
        [Fact]
        public void ReportsOncePerSpreadMonotonicallyToTotal()
        {
            Fixture f = NewFixture(5); // Standard -> 3 spreads
            var progress = new RecordingProgress();

            f.Service.Convert(new SpreadRequest("in.pdf", "out.pdf", FirstPageMode.Standard), progress);

            Assert.Equal(3, progress.Calls.Count);
            Assert.All(progress.Calls, c => Assert.Equal(3, c.Total));
            Assert.Equal([1, 2, 3], progress.Calls.Select(c => c.Completed));
            Assert.Equal(progress.Calls[^1].Total, progress.Calls[^1].Completed);
        }

        [Fact]
        public void CompletedCountNeverExceedsTotalAndIsStrictlyIncreasing()
        {
            Fixture f = NewFixture(10);
            var progress = new RecordingProgress();

            f.Service.Convert(new SpreadRequest("in.pdf", "out.pdf", FirstPageMode.Cover), progress);

            int total = progress.Calls[0].Total;
            for (int i = 0; i < progress.Calls.Count; i++)
            {
                Assert.Equal(i + 1, progress.Calls[i].Completed);
                Assert.True(progress.Calls[i].Completed <= total);
            }
        }

        [Fact]
        public void WorksWithoutAProgressCallback()
        {
            Fixture f = NewFixture(4);
            f.Service.Convert(new SpreadRequest("in.pdf", "out.pdf", FirstPageMode.Standard));
            f.Writer.Received(1).Save("out.pdf");
        }
    }

    public sealed class Disposal
    {
        [Fact]
        public void DisposesSourceAndWriterOnSuccess()
        {
            Fixture f = NewFixture(4);

            f.Service.Convert(new SpreadRequest("in.pdf", "out.pdf", FirstPageMode.Standard));

            f.Source.Received(1).Dispose();
            f.Writer.Received(1).Dispose();
        }

        [Fact]
        public void DisposesSourceAndWriterEvenWhenSaveThrows()
        {
            Fixture f = NewFixture(4);
            f.Writer.When(w => w.Save(Arg.Any<string>()))
                .Do(_ => throw new SpreadException(ErrorKind.PdfWriteFailed));

            Assert.Throws<SpreadException>(() =>
                f.Service.Convert(new SpreadRequest("in.pdf", "out.pdf", FirstPageMode.Standard)));

            f.Source.Received(1).Dispose();
            f.Writer.Received(1).Dispose();
        }
    }

    public sealed class ErrorPropagation
    {
        [Fact]
        public void PropagatesOpenSourceFailure()
        {
            var engine = Substitute.For<IPdfEngine>();
            engine.OpenSource(Arg.Any<string>())
                .Throws(new SpreadException(ErrorKind.PdfNotFound, "missing"));

            var service = new SpreadConversionService(engine);

            var ex = Assert.Throws<SpreadException>(() =>
                service.Convert(new SpreadRequest("in.pdf", "out.pdf", FirstPageMode.Standard)));
            Assert.Equal(ErrorKind.PdfNotFound, ex.Kind);
        }

        [Fact]
        public void PropagatesSaveFailure()
        {
            Fixture f = NewFixture(2);
            f.Writer.When(w => w.Save(Arg.Any<string>()))
                .Do(_ => throw new SpreadException(ErrorKind.PdfWriteFailed, "locked"));

            var ex = Assert.Throws<SpreadException>(() =>
                f.Service.Convert(new SpreadRequest("in.pdf", "out.pdf", FirstPageMode.Standard)));
            Assert.Equal(ErrorKind.PdfWriteFailed, ex.Kind);
        }
    }

    public sealed class Guards
    {
        [Fact]
        public void NullEngineRejected() =>
            Assert.Throws<ArgumentNullException>(() => new SpreadConversionService(null!));

        [Fact]
        public void NullRequestRejected()
        {
            var service = new SpreadConversionService(Substitute.For<IPdfEngine>());
            Assert.Throws<ArgumentNullException>(() => service.Convert(null!));
        }
    }
}
