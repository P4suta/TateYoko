using NSubstitute;
using TateYoko.Core.Application;
using TateYoko.Core.Domain;
using TateYoko.Core.Ports;
using TateYoko.Presentation.Abstractions;
using TateYoko.Presentation.ViewModels;

namespace TateYoko.Presentation.Tests;

/// <summary>Runs UI-dispatched actions inline so the view model can be exercised off the UI thread.</summary>
internal sealed class SyncDispatcher : IUiDispatcher
{
    public void Post(Action action) => action();
}

/// <summary>
/// Builds a <see cref="MainViewModel"/> wired to substitutes: a real <see cref="SpreadConversionService"/>
/// over a faked <see cref="IPdfEngine"/>, plus faked strings/shell and a synchronous dispatcher.
/// </summary>
internal sealed class ViewModelHarness
{
    public IUiStrings Strings { get; } = Substitute.For<IUiStrings>();

    public IShellLauncher Shell { get; } = Substitute.For<IShellLauncher>();

    public IPdfEngine Engine { get; } = Substitute.For<IPdfEngine>();

    public ISourceDocument Source { get; } = Substitute.For<ISourceDocument>();

    public ISpreadWriter Writer { get; } = Substitute.For<ISpreadWriter>();

    /// <summary>
    /// Gate that <see cref="IPdfEngine.OpenSource"/> waits on. Signaled by default so conversions run
    /// to completion instantly; reset it to hold a conversion in the Converting state for observation.
    /// </summary>
    public ManualResetEventSlim OpenGate { get; } = new(initialState: true);

    public MainViewModel Vm { get; }

    public ViewModelHarness(int pageCount = 4)
    {
        Strings.NotPdf.Returns("NOT_PDF");
        Strings.ProgressStarting.Returns("START");
        Strings.ForError(Arg.Any<ErrorKind>()).Returns(ci => $"ERR:{ci.Arg<ErrorKind>()}");
        Strings.ProgressFormat(Arg.Any<int>(), Arg.Any<int>())
            .Returns(ci => $"{ci.ArgAt<int>(0)}/{ci.ArgAt<int>(1)}");

        Source.PageCount.Returns(pageCount);
        Source.Metadata.Returns(DocumentMetadata.Empty);
        Source.GetPageDimension(Arg.Any<int>()).Returns(new PageDimension(100f, 200f));
        Source.GetPageContent(Arg.Any<int>()).Returns(_ => Substitute.For<IPageContent>());

        Engine.OpenSource(Arg.Any<string>()).Returns(_ =>
        {
            OpenGate.Wait();
            return Source;
        });
        Engine.CreateWriter().Returns(Writer);

        var service = new SpreadConversionService(Engine);
        Vm = new MainViewModel(service, new SyncDispatcher(), Strings, Shell);
    }
}
