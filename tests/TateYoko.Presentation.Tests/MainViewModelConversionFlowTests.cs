using NSubstitute;
using TateYoko.Presentation.ViewModels;

namespace TateYoko.Presentation.Tests;

/// <summary>
/// Observes the transient Converting phase of a real in-flight conversion by holding it at
/// <c>OpenSource</c> with <see cref="ViewModelHarness.OpenGate"/>. This exercises the intermediate
/// state and re-entrancy guards that the synchronous happy-path tests cannot see (they only observe
/// the terminal state). Every wait is bounded so a regression fails instead of hanging.
/// </summary>
public sealed class MainViewModelConversionFlowTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private static string Pdf(string name) => Path.Combine("C:", "d", name);

    [Fact]
    public async Task ConvertingStateAndStartingProgressAreObservableWhileInFlight()
    {
        var h = new ViewModelHarness(pageCount: 4);
        h.OpenGate.Reset(); // block the background conversion inside OpenSource
        h.Vm.SetInput(Pdf("book.pdf"));

        Task convert = h.Vm.ConvertCommand.ExecuteAsync(null);

        // The synchronous prologue of ConvertAsync runs before the first await yields.
        Assert.Equal(ConversionState.Converting, h.Vm.State);
        Assert.True(h.Vm.ProgressIndeterminate);
        Assert.Equal("START", h.Vm.ProgressText);
        Assert.False(convert.IsCompleted);

        h.OpenGate.Set();
        await convert.WaitAsync(Timeout);

        Assert.Equal(ConversionState.Done, h.Vm.State);
        Assert.False(h.Vm.ProgressIndeterminate);
        Assert.Equal(100d, h.Vm.ProgressValue);
    }

    [Fact]
    public async Task ConvertCannotBeReExecutedWhileConverting()
    {
        var h = new ViewModelHarness(pageCount: 4);
        h.OpenGate.Reset();
        h.Vm.SetInput(Pdf("book.pdf"));

        Task convert = h.Vm.ConvertCommand.ExecuteAsync(null);
        Assert.Equal(ConversionState.Converting, h.Vm.State);
        Assert.False(h.Vm.ConvertCommand.CanExecute(null));

        h.OpenGate.Set();
        await convert.WaitAsync(Timeout);

        // The source was opened exactly once despite the state staying Converting throughout.
        h.Engine.Received(1).OpenSource(Arg.Any<string>());
    }

    [Fact]
    public async Task SetInputIsIgnoredDuringAnInFlightConversion()
    {
        var h = new ViewModelHarness(pageCount: 4);
        h.OpenGate.Reset();
        string first = Pdf("first.pdf");
        h.Vm.SetInput(first);

        Task convert = h.Vm.ConvertCommand.ExecuteAsync(null);
        Assert.Equal(ConversionState.Converting, h.Vm.State);

        h.Vm.SetInput(Pdf("second.pdf")); // must be ignored while converting

        Assert.Equal(first, h.Vm.InputPath);
        Assert.Equal(ConversionState.Converting, h.Vm.State);

        h.OpenGate.Set();
        await convert.WaitAsync(Timeout);
        Assert.Equal(ConversionState.Done, h.Vm.State);
    }
}
