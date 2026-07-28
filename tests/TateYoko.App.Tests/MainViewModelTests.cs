using TateYoko.App.ViewModels;
using TateYoko.Engine;

namespace TateYoko.App.Tests;

public sealed class MainViewModelTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public void ConstructorRejectsEveryNullDependency()
    {
        using var harness = new TestHarness();

        Assert.Throws<ArgumentNullException>(() =>
            new MainViewModel(null!, harness.Strings, harness.Shell, harness.Log)
        );
        Assert.Throws<ArgumentNullException>(() =>
            new MainViewModel(harness.Converter, null!, harness.Shell, harness.Log)
        );
        Assert.Throws<ArgumentNullException>(() =>
            new MainViewModel(harness.Converter, harness.Strings, null!, harness.Log)
        );
        Assert.Throws<ArgumentNullException>(() =>
            new MainViewModel(harness.Converter, harness.Strings, harness.Shell, null!)
        );
    }

    [Fact]
    public async Task StartsIdleWithoutAnOutputDestination()
    {
        using var harness = new TestHarness();

        Assert.Equal(ConversionState.Idle, harness.ViewModel.State);
        Assert.True(harness.ViewModel.IsIdle);
        Assert.False(harness.ViewModel.IsDone);
        Assert.False(harness.ViewModel.HasActionError);
        Assert.Equal("spread.pdf", harness.ViewModel.SuggestedOutputFileName);
        Assert.Empty(harness.ViewModel.OutputFileName);
        Assert.False(harness.ViewModel.CancelCommand.CanExecute(null));
        Assert.False(harness.ViewModel.OpenOutputCommand.CanExecute(null));
        Assert.False(harness.ViewModel.ShowInFolderCommand.CanExecute(null));

        await harness.ViewModel.ConvertWithPasswordAsync("unused");
        await harness.ViewModel.ConvertToAsync(harness.PathFor("unused.pdf"));

        Assert.Equal(PdfSpreadError.InvalidRequest, harness.ViewModel.LastError);
        Assert.Empty(harness.Converter.Requests);
    }

    [Fact]
    public void ValidInputMovesToReadyButDoesNotPreselectAPath()
    {
        using var harness = new TestHarness();
        string input = harness.CreatePdf("novel.v2.PDF");
        File.WriteAllBytes(harness.PathFor("novel.v2_spread.pdf"), []);

        harness.ViewModel.SetInput(input);
        harness.ViewModel.SetFirstPageMode(FirstPageMode.Standard);
        harness.ViewModel.SetFirstPageMode(FirstPageMode.Cover);

        Assert.Equal(ConversionState.Ready, harness.ViewModel.State);
        Assert.Equal("novel.v2.PDF", harness.ViewModel.InputFileName);
        Assert.Equal((int)FirstPageMode.Cover, harness.ViewModel.FirstPageModeIndex);
        Assert.Equal("novel.v2_spread.pdf", harness.ViewModel.SuggestedOutputFileName);
        Assert.Empty(harness.ViewModel.OutputFileName);
        Assert.Empty(harness.Converter.Requests);
    }

    [Theory]
    [InlineData("image.png", 3)]
    [InlineData("missing.pdf", 1)]
    public void InvalidInputMovesToActionableError(string name, int expectedError)
    {
        ArgumentNullException.ThrowIfNull(name);
        using var harness = new TestHarness();
        string path = harness.PathFor(name);
        if (!name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            File.WriteAllBytes(path, []);
        }

        harness.ViewModel.SetInput(path);

        Assert.Equal(ConversionState.Error, harness.ViewModel.State);
        Assert.Equal((PdfSpreadError)expectedError, harness.ViewModel.LastError);
        Assert.False(harness.ViewModel.CanRetryCurrentError);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("C:\\invalid\u0000.pdf")]
    public void MalformedInputIsRejectedWithoutTouchingTheFileSystem(string path)
    {
        using var harness = new TestHarness();

        harness.ViewModel.SetInput(path);

        Assert.Equal(PdfSpreadError.UnsupportedFile, harness.ViewModel.LastError);
        Assert.Empty(harness.Converter.Requests);
    }

    [Fact]
    public void SelectionAndUnexpectedErrorsAreSafeForPresentation()
    {
        using var harness = new TestHarness();
        var failure = new InvalidOperationException("private details");

        harness.ViewModel.ShowSelectionError("selection failed");
        Assert.Equal(PdfSpreadError.InvalidRequest, harness.ViewModel.LastError);
        Assert.Equal("selection failed", harness.ViewModel.ErrorMessage);
        Assert.Equal("selection failed", harness.ViewModel.StatusAnnouncement);

        harness.ViewModel.ShowUnexpectedError(failure);
        Assert.Same(failure, harness.Log.LastException);
        Assert.Equal("error:Internal", harness.ViewModel.ErrorMessage);
    }

    [Fact]
    public async Task DestinationMustBeAnExistingFolderAndDifferFromInput()
    {
        using var harness = new TestHarness();
        string input = harness.CreatePdf();
        harness.ViewModel.SetInput(input);

        await harness.ViewModel.ConvertToAsync(input);

        Assert.Equal(PdfSpreadError.InvalidRequest, harness.ViewModel.LastError);
        Assert.Empty(harness.Converter.Requests);
        Assert.True(harness.ViewModel.PrepareRetry());
        Assert.True(harness.ViewModel.IsReady);

        await harness.ViewModel.ConvertToAsync(
            Path.Combine(harness.PathFor("missing"), "output.pdf")
        );
        Assert.Equal(PdfSpreadError.InvalidRequest, harness.ViewModel.LastError);
        Assert.Empty(harness.Converter.Requests);
    }

    [Fact]
    public void OpeningModeRejectsUndefinedValues()
    {
        using var harness = new TestHarness();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            harness.ViewModel.SetFirstPageMode((FirstPageMode)int.MaxValue)
        );
    }

    [Fact]
    public async Task HappyPathUsesThePickerDestinationAndMovesToDone()
    {
        using var harness = new TestHarness();
        string input = harness.CreatePdf();
        string output = harness.PathFor("chosen.pdf");
        harness.ViewModel.SetInput(input);
        harness.ViewModel.SetFirstPageMode(FirstPageMode.Cover);

        await harness.ViewModel.ConvertToAsync(output);

        PdfSpreadRequest request = Assert.Single(harness.Converter.Requests);
        Assert.Equal(input, request.InputPath);
        Assert.Equal(output, request.OutputPath);
        Assert.Equal(FirstPageMode.Cover, request.FirstPageMode);
        Assert.Null(request.Password);
        Assert.Equal(ConversionState.Done, harness.ViewModel.State);
        Assert.Equal("chosen.pdf", harness.ViewModel.OutputFileName);
        Assert.True(File.Exists(output));
        Assert.True(harness.ViewModel.IsDone);
        Assert.True(harness.ViewModel.OpenOutputCommand.CanExecute(null));
        Assert.True(harness.ViewModel.ShowInFolderCommand.CanExecute(null));
    }

    [Fact]
    public async Task PasswordChallengeReusesOnlyTheChosenDestination()
    {
        using var harness = new TestHarness();
        string input = harness.CreatePdf();
        string output = harness.PathFor("protected.pdf");
        harness.Converter.Behavior = (request, _, _) =>
        {
            if (request.Password != "secret")
            {
                throw new PdfSpreadException(
                    request.Password is null
                        ? PdfSpreadError.PasswordRequired
                        : PdfSpreadError.InvalidPassword
                );
            }

            File.WriteAllBytes(request.OutputPath, []);
            return Task.FromResult(new PdfSpreadResult(request.OutputPath, 2, 1));
        };
        harness.ViewModel.SetInput(input);

        await harness.ViewModel.ConvertToAsync(output);
        await harness.ViewModel.ConvertWithPasswordAsync("wrong");
        await harness.ViewModel.ConvertWithPasswordAsync("secret");

        Assert.Equal([null, "wrong", "secret"], harness.Converter.Requests.Select(r => r.Password));
        Assert.All(harness.Converter.Requests, request => Assert.Equal(output, request.OutputPath));
        Assert.Equal(ConversionState.Done, harness.ViewModel.State);
        Assert.DoesNotContain(
            harness.ViewModel.GetType().GetProperties(),
            property => property.Name.Contains("Password", StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public async Task CancellationReturnsReadyAndForgetsTheDestination()
    {
        using var harness = new TestHarness();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Converter.Behavior = async (_, _, cancellationToken) =>
        {
            entered.SetResult();
            await Task.Delay(System.Threading.Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable.");
        };
        harness.ViewModel.SetInput(harness.CreatePdf());

        Task conversion = harness.ViewModel.ConvertToAsync(harness.PathFor("cancelled.pdf"));
        await entered.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        string ignored = harness.CreatePdf("ignored.pdf");
        harness.ViewModel.SetInput(ignored);
        Assert.NotEqual("ignored.pdf", harness.ViewModel.InputFileName);
        Assert.True(harness.ViewModel.CancelCommand.CanExecute(null));
        harness.ViewModel.CancelCommand.Execute(null);
        await conversion.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        Assert.True(harness.ViewModel.IsReady);
        Assert.Empty(harness.ViewModel.OutputFileName);
        Assert.Equal("cancelled", harness.ViewModel.StatusAnnouncement);
    }

    [Theory]
    [InlineData(2, true)]
    [InlineData(6, false)]
    [InlineData(7, false)]
    [InlineData(8, true)]
    [InlineData(9, true)]
    [InlineData(10, false)]
    public async Task KnownFailuresOfferRetryOnlyWhenASecondSaveCanHelp(int error, bool canRetry)
    {
        using var harness = new TestHarness();
        PdfSpreadError expected = (PdfSpreadError)error;
        harness.Converter.Behavior = (_, _, _) => throw new PdfSpreadException(expected);
        harness.ViewModel.SetInput(harness.CreatePdf());

        await harness.ViewModel.ConvertToAsync(harness.PathFor("failed.pdf"));

        Assert.Equal(expected, harness.ViewModel.LastError);
        Assert.Equal(canRetry, harness.ViewModel.CanRetryCurrentError);
        Assert.Equal(canRetry, harness.ViewModel.PrepareRetry());
        Assert.Equal(
            canRetry ? ConversionState.Ready : ConversionState.Error,
            harness.ViewModel.State
        );
    }

    [Fact]
    public async Task UnsupportedDetailIsPreservedForLocalizedReasonSelection()
    {
        using var harness = new TestHarness();
        harness.Converter.Behavior = (_, _, _) =>
            throw new PdfSpreadException(
                PdfSpreadError.UnsupportedPdfFeature,
                "unsupported-annotation"
            );
        harness.ViewModel.SetInput(harness.CreatePdf());

        await harness.ViewModel.ConvertToAsync(harness.PathFor("failed.pdf"));

        Assert.Equal(
            "error:UnsupportedPdfFeature:unsupported-annotation",
            harness.ViewModel.ErrorMessage
        );
    }

    [Fact]
    public async Task RetryRequiresASecondExplicitDestination()
    {
        using var harness = new TestHarness();
        var attempt = 0;
        harness.Converter.Behavior = (request, _, _) =>
        {
            if (Interlocked.Increment(ref attempt) == 1)
            {
                throw new PdfSpreadException(PdfSpreadError.WriteFailed);
            }

            File.WriteAllBytes(request.OutputPath, []);
            return Task.FromResult(new PdfSpreadResult(request.OutputPath, 2, 1));
        };
        harness.ViewModel.SetInput(harness.CreatePdf());
        string first = harness.PathFor("first.pdf");
        string second = harness.PathFor("second.pdf");

        await harness.ViewModel.ConvertToAsync(first);
        Assert.True(harness.ViewModel.PrepareRetry());
        await harness.ViewModel.ConvertToAsync(second);

        Assert.Equal([first, second], harness.Converter.Requests.Select(r => r.OutputPath));
        Assert.Equal(ConversionState.Done, harness.ViewModel.State);
    }

    [Fact]
    public async Task UnexpectedFailureIsRedactedAndLogged()
    {
        using var harness = new TestHarness();
        var failure = new InvalidOperationException("private details");
        harness.Converter.Behavior = (_, _, _) => throw failure;
        harness.ViewModel.SetInput(harness.CreatePdf());

        await harness.ViewModel.ConvertToAsync(harness.PathFor("failed.pdf"));

        Assert.Same(failure, harness.Log.LastException);
        Assert.Equal("error:Internal", harness.ViewModel.ErrorMessage);
    }

    [Fact]
    public async Task CompletionActionsRequireARealOutput()
    {
        using var harness = new TestHarness();
        harness.ViewModel.SetInput(harness.CreatePdf());
        string output = harness.PathFor("output.pdf");

        await harness.ViewModel.ConvertToAsync(output);
        harness.ViewModel.OpenOutputCommand.Execute(null);
        harness.ViewModel.ShowInFolderCommand.Execute(null);

        Assert.Equal(output, harness.Shell.OpenedPath);
        Assert.Equal(output, harness.Shell.ShownPath);
    }

    [Fact]
    public async Task OutputActionFailureStaysDoneAndIsLogged()
    {
        using var harness = new TestHarness();
        var failure = new InvalidOperationException("private shell details");
        harness.Shell.Failure = failure;
        harness.ViewModel.SetInput(harness.CreatePdf());
        await harness.ViewModel.ConvertToAsync(harness.PathFor("output.pdf"));

        harness.ViewModel.OpenOutputCommand.Execute(null);

        Assert.Equal(ConversionState.Done, harness.ViewModel.State);
        Assert.Equal("output-action-failed", harness.ViewModel.ActionErrorMessage);
        Assert.True(harness.ViewModel.HasActionError);
        Assert.Same(failure, harness.Log.LastException);
    }

    [Fact]
    public async Task DisposalDuringConversionIgnoresLateCompletion()
    {
        using var harness = new TestHarness();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Converter.Behavior = async (request, _, _) =>
        {
            entered.SetResult();
            await release.Task;
            File.WriteAllBytes(request.OutputPath, []);
            return new PdfSpreadResult(request.OutputPath, 2, 1);
        };
        harness.ViewModel.SetInput(harness.CreatePdf());

        Task conversion = harness.ViewModel.ConvertToAsync(harness.PathFor("late.pdf"));
        await entered.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        harness.ViewModel.Dispose();
        release.SetResult();
        await conversion.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        Assert.Equal(ConversionState.Converting, harness.ViewModel.State);
    }

    [Fact]
    public void StateChangeRaisesAllDerivedFlagNotifications()
    {
        using var harness = new TestHarness();
        var notifications = new HashSet<string>(StringComparer.Ordinal);
        harness.ViewModel.PropertyChanged += (_, args) => notifications.Add(args.PropertyName!);

        harness.ViewModel.SetInput(harness.CreatePdf());

        string[] expected =
        [
            nameof(MainViewModel.State),
            nameof(MainViewModel.IsIdle),
            nameof(MainViewModel.IsReady),
            nameof(MainViewModel.IsConverting),
            nameof(MainViewModel.IsPasswordRequired),
            nameof(MainViewModel.IsDone),
            nameof(MainViewModel.IsError),
        ];
        Assert.All(expected, name => Assert.Contains(name, notifications));
    }

    [Fact]
    public async Task DisposalIsIdempotentAndRejectsFurtherMutation()
    {
        using var harness = new TestHarness();
        harness.ViewModel.Dispose();
        harness.ViewModel.Dispose();

        Assert.Throws<ObjectDisposedException>(() =>
            harness.ViewModel.SetInput(harness.PathFor("unused.pdf"))
        );
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            harness.ViewModel.ConvertToAsync(harness.PathFor("unused.pdf"))
        );
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            harness.ViewModel.ConvertWithPasswordAsync("unused")
        );
    }
}
