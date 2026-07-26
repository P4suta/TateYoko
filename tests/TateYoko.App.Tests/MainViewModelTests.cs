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
    public void StartsInExactlyOneIdleState()
    {
        using var harness = new TestHarness();
        MainViewModel viewModel = harness.ViewModel;

        Assert.Equal(ConversionState.Idle, viewModel.State);
        Assert.True(viewModel.IsIdle);
        Assert.False(viewModel.IsReady);
        Assert.False(viewModel.IsConverting);
        Assert.False(viewModel.IsPasswordRequired);
        Assert.False(viewModel.IsDone);
        Assert.False(viewModel.IsError);
    }

    [Fact]
    public void ValidInputMovesToReadyAndRequestsTheStableBaseOutput()
    {
        using var harness = new TestHarness();
        string input = harness.CreatePdf("novel.PDF");
        File.WriteAllBytes(harness.PathFor("novel_spread.pdf"), []);

        harness.ViewModel.SetInput(input);

        Assert.Equal(ConversionState.Ready, harness.ViewModel.State);
        Assert.Equal("novel.PDF", harness.ViewModel.InputFileName);
        Assert.Equal("novel_spread.pdf", harness.ViewModel.OutputFileName);
        Assert.Equal(Path.GetDirectoryName(input), harness.ViewModel.InputFolder);
        Assert.True(harness.ViewModel.ConvertCommand.CanExecute(null));
    }

    [Theory]
    [InlineData("image.png", PdfSpreadError.UnsupportedFile)]
    [InlineData("missing.pdf", PdfSpreadError.InputNotFound)]
    public void InvalidInputMovesToActionableError(string name, PdfSpreadError expected)
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
        Assert.Equal(expected, harness.ViewModel.LastError);
        Assert.False(harness.ViewModel.RetryCommand.CanExecute(null));
    }

    [Fact]
    public void MalformedInputPathIsRejectedWithoutThrowing()
    {
        using var harness = new TestHarness();

        harness.ViewModel.SetInput("C:\\invalid\0.pdf");

        Assert.Equal(ConversionState.Error, harness.ViewModel.State);
        Assert.Equal(PdfSpreadError.UnsupportedFile, harness.ViewModel.LastError);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("relative.pdf")]
    public void EmptyOrRelativeInputIsRejectedWithoutFilesystemAccess(string path)
    {
        using var harness = new TestHarness();

        harness.ViewModel.SetInput(path);

        Assert.Equal(ConversionState.Error, harness.ViewModel.State);
        Assert.Equal(PdfSpreadError.UnsupportedFile, harness.ViewModel.LastError);
    }

    [Fact]
    public void ExplicitOutputMustDifferFromInput()
    {
        using var harness = new TestHarness();
        string input = harness.CreatePdf();
        harness.ViewModel.SetInput(input);

        harness.ViewModel.SetExplicitOutput(input);

        Assert.Equal(ConversionState.Error, harness.ViewModel.State);
        Assert.Equal(PdfSpreadError.InvalidRequest, harness.ViewModel.LastError);
    }

    [Fact]
    public void MalformedExplicitOutputIsRejectedWithoutThrowing()
    {
        using var harness = new TestHarness();
        harness.ViewModel.SetInput(harness.CreatePdf());

        harness.ViewModel.SetExplicitOutput("C:\\invalid\0.pdf");

        Assert.Equal(ConversionState.Error, harness.ViewModel.State);
        Assert.Equal(PdfSpreadError.InvalidRequest, harness.ViewModel.LastError);
    }

    [Fact]
    public async Task InvalidExplicitOutputRetryReturnsToReadyForCorrection()
    {
        using var harness = new TestHarness();
        string input = harness.CreatePdf();
        harness.ViewModel.SetInput(input);
        harness.ViewModel.SetExplicitOutput(input);

        Assert.True(harness.ViewModel.RetryCommand.CanExecute(null));
        await harness.ViewModel.RetryCommand.ExecuteAsync(null);

        Assert.Equal(ConversionState.Ready, harness.ViewModel.State);
        Assert.Null(harness.ViewModel.LastError);
        Assert.True(harness.ViewModel.ConvertCommand.CanExecute(null));
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
    public void SelectionAndUnexpectedErrorsAreRedactedIntoState()
    {
        using var harness = new TestHarness();
        var failure = new InvalidOperationException("private details");

        Assert.Throws<ArgumentException>(() => harness.ViewModel.ShowSelectionError(" "));
        harness.ViewModel.ShowSelectionError("selection failed");

        Assert.Equal(ConversionState.Error, harness.ViewModel.State);
        Assert.Equal(PdfSpreadError.InvalidRequest, harness.ViewModel.LastError);
        Assert.Equal("selection failed", harness.ViewModel.ErrorMessage);
        Assert.Equal("selection failed", harness.ViewModel.StatusAnnouncement);

        harness.ViewModel.ShowUnexpectedError(failure);

        Assert.Same(failure, harness.Log.LastException);
        Assert.Equal(PdfSpreadError.Internal, harness.ViewModel.LastError);
        Assert.Equal("error:Internal", harness.ViewModel.ErrorMessage);
    }

    [Fact]
    public async Task PasswordSubmissionOutsidePasswordStateIsANoop()
    {
        using var harness = new TestHarness();

        await harness.ViewModel.ConvertWithPasswordAsync("unused");

        Assert.Empty(harness.Converter.Requests);
        Assert.Equal(ConversionState.Idle, harness.ViewModel.State);
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
        Assert.Throws<ObjectDisposedException>(() =>
            harness.ViewModel.ShowSelectionError("unused")
        );
        Assert.Throws<ObjectDisposedException>(() =>
            harness.ViewModel.ShowUnexpectedError(new InvalidOperationException())
        );
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            harness.ViewModel.ConvertWithPasswordAsync("unused")
        );
    }

    [Fact]
    public async Task HappyPathCarriesAllChoicesAndMovesToDone()
    {
        using var harness = new TestHarness();
        string input = harness.CreatePdf();
        string output = harness.PathFor("custom.pdf");
        harness.ViewModel.SetInput(input);
        harness.ViewModel.SetExplicitOutput(output);
        harness.ViewModel.SetFirstPageMode(FirstPageMode.Cover);

        await harness.ViewModel.ConvertCommand.ExecuteAsync(null);

        PdfSpreadRequest request = Assert.Single(harness.Converter.Requests);
        Assert.Equal(input, request.InputPath);
        Assert.Equal(output, request.OutputPath);
        Assert.Equal(FirstPageMode.Cover, request.FirstPageMode);
        Assert.Equal(OutputCollisionPolicy.ReplaceExisting, request.CollisionPolicy);
        Assert.True(request.PreservePasswordProtection);
        Assert.Equal(ConversionState.Done, harness.ViewModel.State);
        Assert.Equal("done", harness.ViewModel.StatusAnnouncement);
        Assert.True(File.Exists(output));
    }

    [Fact]
    public async Task PasswordChallengeRetriesWithoutPersistingItInTheViewModel()
    {
        using var harness = new TestHarness();
        string input = harness.CreatePdf();
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
            return new PdfSpreadResult(request.OutputPath, 2, 1);
        };
        harness.ViewModel.SetInput(input);

        await harness.ViewModel.ConvertCommand.ExecuteAsync(null);
        Assert.Equal(ConversionState.PasswordRequired, harness.ViewModel.State);
        Assert.Equal(PdfSpreadError.PasswordRequired, harness.ViewModel.LastError);

        await harness.ViewModel.ConvertWithPasswordAsync("wrong");
        Assert.Equal(ConversionState.PasswordRequired, harness.ViewModel.State);
        Assert.Equal(PdfSpreadError.InvalidPassword, harness.ViewModel.LastError);

        await harness.ViewModel.ConvertWithPasswordAsync("secret");
        Assert.Equal(ConversionState.Done, harness.ViewModel.State);
        Assert.Equal([null, "wrong", "secret"], harness.Converter.Requests.Select(r => r.Password));
        Assert.DoesNotContain(
            harness.ViewModel.GetType().GetProperties(),
            property => property.Name.Contains("Password", StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public async Task CancellationReturnsToReadyAndCanRunAgain()
    {
        using var harness = new TestHarness();
        using var entered = new ManualResetEventSlim();
        harness.Converter.Behavior = (_, _, cancellationToken) =>
        {
            entered.Set();
            cancellationToken.WaitHandle.WaitOne();
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Unreachable.");
        };
        harness.ViewModel.SetInput(harness.CreatePdf());
        harness.ViewModel.SetFirstPageMode(FirstPageMode.Cover);

        Task conversion = harness.ViewModel.ConvertCommand.ExecuteAsync(null);
        Assert.True(entered.Wait(Timeout, TestContext.Current.CancellationToken));
        await WaitForAsync(() => harness.ViewModel.IsConverting);
        Assert.True(harness.ViewModel.CancelCommand.CanExecute(null));

        harness.ViewModel.CancelCommand.Execute(null);
        await conversion.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        Assert.Equal(ConversionState.Ready, harness.ViewModel.State);
        Assert.Equal(FirstPageMode.Cover, harness.ViewModel.FirstPageMode);
        Assert.Equal((int)FirstPageMode.Cover, harness.ViewModel.FirstPageModeIndex);
        Assert.Equal("cancelled", harness.ViewModel.StatusAnnouncement);
        Assert.True(harness.ViewModel.ConvertCommand.CanExecute(null));
    }

    [Fact]
    public async Task InputChangesAreIgnoredDuringConversion()
    {
        using var harness = new TestHarness();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        harness.Converter.Behavior = (request, _, _) =>
        {
            entered.Set();
            release.Wait(TestContext.Current.CancellationToken);
            File.WriteAllBytes(request.OutputPath, []);
            return new PdfSpreadResult(request.OutputPath, 2, 1);
        };
        harness.ViewModel.SetInput(harness.CreatePdf("first.pdf"));

        Task conversion = harness.ViewModel.ConvertCommand.ExecuteAsync(null);
        Assert.True(entered.Wait(Timeout, TestContext.Current.CancellationToken));
        await WaitForAsync(() => harness.ViewModel.IsConverting);

        harness.ViewModel.SetInput(harness.CreatePdf("second.pdf"));
        release.Set();
        await conversion.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        Assert.Equal("first.pdf", harness.ViewModel.InputFileName);
        Assert.Equal(ConversionState.Done, harness.ViewModel.State);
    }

    [Fact]
    public async Task ReentrantConversionOwnsItsCancellationAndIgnoresOldCleanup()
    {
        using var harness = new TestHarness();
        using var firstEntered = new ManualResetEventSlim();
        using var secondEntered = new ManualResetEventSlim();
        var attempt = 0;
        harness.Converter.Behavior = (_, _, cancellationToken) =>
        {
            int currentAttempt = Interlocked.Increment(ref attempt);
            (currentAttempt == 1 ? firstEntered : secondEntered).Set();
            cancellationToken.WaitHandle.WaitOne();
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Unreachable.");
        };
        harness.ViewModel.SetInput(harness.CreatePdf());

        var secondConversion = new TaskCompletionSource<Task>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var startedSecond = false;
        harness.ViewModel.PropertyChanged += (_, args) =>
        {
            if (
                !startedSecond
                && args.PropertyName == nameof(MainViewModel.State)
                && harness.ViewModel.State == ConversionState.Ready
            )
            {
                startedSecond = true;
                secondConversion.SetResult(harness.ViewModel.ConvertCommand.ExecuteAsync(null));
            }
        };

        Task firstConversion = harness.ViewModel.ConvertCommand.ExecuteAsync(null);
        Assert.True(firstEntered.Wait(Timeout, TestContext.Current.CancellationToken));
        harness.ViewModel.CancelCommand.Execute(null);
        await firstConversion.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        Assert.True(secondEntered.Wait(Timeout, TestContext.Current.CancellationToken));
        Assert.True(harness.ViewModel.IsConverting);
        Assert.True(harness.ViewModel.CancelCommand.CanExecute(null));
        harness.ViewModel.CancelCommand.Execute(null);
        Task runningSecond = await secondConversion.Task.WaitAsync(
            Timeout,
            TestContext.Current.CancellationToken
        );
        await runningSecond.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        Assert.Equal(2, attempt);
        Assert.Equal(ConversionState.Ready, harness.ViewModel.State);
    }

    [Theory]
    [InlineData(PdfSpreadError.CorruptedPdf, false)]
    [InlineData(PdfSpreadError.ReadFailed, true)]
    [InlineData(PdfSpreadError.InvalidPage, false)]
    [InlineData(PdfSpreadError.WriteFailed, true)]
    [InlineData(PdfSpreadError.UnsupportedPdfFeature, false)]
    public async Task KnownConversionFailuresExposeRetryOnlyWhenItCanHelp(
        PdfSpreadError error,
        bool canRetry
    )
    {
        using var harness = new TestHarness();
        harness.Converter.Behavior = (_, _, _) => throw new PdfSpreadException(error);
        harness.ViewModel.SetInput(harness.CreatePdf());

        await harness.ViewModel.ConvertCommand.ExecuteAsync(null);

        Assert.Equal(ConversionState.Error, harness.ViewModel.State);
        Assert.Equal(error, harness.ViewModel.LastError);
        Assert.Equal($"error:{error}", harness.ViewModel.ErrorMessage);
        Assert.Equal(canRetry, harness.ViewModel.CanRetryCurrentError);
        Assert.Equal(canRetry, harness.ViewModel.RetryCommand.CanExecute(null));
        Assert.Null(harness.Log.LastException);
    }

    [Fact]
    public async Task UnexpectedFailureIsRedactedAndLoggedLocally()
    {
        using var harness = new TestHarness();
        var failure = new InvalidOperationException("contains a private path");
        harness.Converter.Behavior = (_, _, _) => throw failure;
        harness.ViewModel.SetInput(harness.CreatePdf());

        await harness.ViewModel.ConvertCommand.ExecuteAsync(null);

        Assert.Same(failure, harness.Log.LastException);
        Assert.Equal(PdfSpreadError.Internal, harness.ViewModel.LastError);
        Assert.Equal("error:Internal", harness.ViewModel.ErrorMessage);
    }

    [Fact]
    public async Task RetryReusesTheSameValidatedRequest()
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
            return new PdfSpreadResult(request.OutputPath, 2, 1);
        };
        harness.ViewModel.SetInput(harness.CreatePdf());

        await harness.ViewModel.ConvertCommand.ExecuteAsync(null);
        await harness.ViewModel.RetryCommand.ExecuteAsync(null);

        Assert.Equal(2, harness.Converter.Requests.Count);
        Assert.Equal(
            harness.Converter.Requests[0].InputPath,
            harness.Converter.Requests[1].InputPath
        );
        Assert.Equal(ConversionState.Done, harness.ViewModel.State);
    }

    [Fact]
    public async Task CompletionActionsDelegateOnlyForARealOutput()
    {
        using var harness = new TestHarness();
        harness.ViewModel.SetInput(harness.CreatePdf());
        await harness.ViewModel.ConvertCommand.ExecuteAsync(null);

        Assert.True(harness.ViewModel.OpenOutputCommand.CanExecute(null));
        Assert.True(harness.ViewModel.ShowInFolderCommand.CanExecute(null));
        harness.ViewModel.OpenOutputCommand.Execute(null);
        harness.ViewModel.ShowInFolderCommand.Execute(null);

        Assert.Equal(harness.ViewModel.OutputFileName, Path.GetFileName(harness.Shell.OpenedPath));
        Assert.Equal(harness.Shell.OpenedPath, harness.Shell.ShownPath);
    }

    [Fact]
    public async Task OutputActionFailureStaysDoneAndSurfacesARetryableLocalError()
    {
        using var harness = new TestHarness();
        var failure = new InvalidOperationException("private shell details");
        harness.Shell.Failure = failure;
        harness.ViewModel.SetInput(harness.CreatePdf());
        await harness.ViewModel.ConvertCommand.ExecuteAsync(null);

        harness.ViewModel.OpenOutputCommand.Execute(null);

        Assert.Equal(ConversionState.Done, harness.ViewModel.State);
        Assert.True(harness.ViewModel.HasActionError);
        Assert.Equal("output-action-failed", harness.ViewModel.ActionErrorMessage);
        Assert.Equal("output-action-failed", harness.ViewModel.StatusAnnouncement);
        Assert.Same(failure, harness.Log.LastException);
        Assert.True(harness.ViewModel.OpenOutputCommand.CanExecute(null));
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

    [Theory]
    [InlineData("book.pdf", "book_spread.pdf")]
    [InlineData("book.v2.PDF", "book.v2_spread.pdf")]
    public void OutputNamingPreservesTheStem(string inputName, string expectedName)
    {
        string input = Path.Combine(Path.GetTempPath(), inputName);
        Assert.Equal(
            Path.Combine(Path.GetTempPath(), expectedName),
            MainViewModel.DeriveOutputPath(input)
        );
    }

    private static async Task WaitForAsync(Func<bool> predicate)
    {
        using var timeout = new CancellationTokenSource(Timeout);
        while (!predicate())
        {
            await Task.Delay(10, timeout.Token);
        }
    }
}
