using System.ComponentModel;
using CsCheck;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TateYoko.Core.Domain;
using TateYoko.Presentation.Abstractions;
using TateYoko.Presentation.ViewModels;

namespace TateYoko.Presentation.Tests;

/// <summary>
/// Tests the <see cref="MainViewModel"/> state machine and its invariants: input validation, output
/// derivation, the conversion transitions, change notifications, command availability, progress math,
/// and shell delegation.
/// </summary>
public sealed class MainViewModelStateTests
{
    private static readonly ConversionState[] AllStates = Enum.GetValues<ConversionState>();

    public sealed class Construction
    {
        [Fact]
        public void RejectsNullDependencies()
        {
            var h = new ViewModelHarness();
            var service = new Core.Application.SpreadConversionService(h.Engine);
            IUiDispatcher disp = new SyncDispatcher();

            Assert.Throws<ArgumentNullException>(() => new MainViewModel(null!, disp, h.Strings, h.Shell));
            Assert.Throws<ArgumentNullException>(() => new MainViewModel(service, null!, h.Strings, h.Shell));
            Assert.Throws<ArgumentNullException>(() => new MainViewModel(service, disp, null!, h.Shell));
            Assert.Throws<ArgumentNullException>(() => new MainViewModel(service, disp, h.Strings, null!));
        }

        [Fact]
        public void StartsIdle() => Assert.Equal(ConversionState.Idle, new ViewModelHarness().Vm.State);
    }

    public sealed class DerivedFlags
    {
        /// <summary>Exactly one boolean flag is true for every state, and it matches the state.</summary>
        [Fact]
        public void ExactlyOneFlagMatchesTheState()
        {
            MainViewModel vm = new ViewModelHarness().Vm;
            foreach (ConversionState state in AllStates)
            {
                vm.State = state;
                var flags = new[] { vm.IsIdle, vm.IsReady, vm.IsConverting, vm.IsDone, vm.IsError };
                Assert.Equal(1, flags.Count(f => f));
                Assert.True(state switch
                {
                    ConversionState.Idle => vm.IsIdle,
                    ConversionState.Ready => vm.IsReady,
                    ConversionState.Converting => vm.IsConverting,
                    ConversionState.Done => vm.IsDone,
                    ConversionState.Error => vm.IsError,
                    _ => false,
                });
            }
        }

        [Fact]
        public void StateChangeNotifiesStateAndAllFlags()
        {
            MainViewModel vm = new ViewModelHarness().Vm;
            var raised = new List<string>();
            vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

            vm.State = ConversionState.Ready;

            Assert.Contains(nameof(vm.State), raised);
            Assert.Contains(nameof(vm.IsIdle), raised);
            Assert.Contains(nameof(vm.IsReady), raised);
            Assert.Contains(nameof(vm.IsConverting), raised);
            Assert.Contains(nameof(vm.IsDone), raised);
            Assert.Contains(nameof(vm.IsError), raised);
        }
    }

    public sealed class SetInput
    {
        [Fact]
        public void PdfMovesToReadyAndDerivesOutputPath()
        {
            MainViewModel vm = new ViewModelHarness().Vm;

            vm.SetInput(Path.Combine("C:", "books", "novel.pdf"));

            Assert.Equal(ConversionState.Ready, vm.State);
            Assert.Equal(Path.Combine("C:", "books", "novel.pdf"), vm.InputPath);
            Assert.Equal("novel.pdf", vm.InputFileName);
            Assert.Equal(Path.Combine("C:", "books", "novel_spread.pdf"), vm.OutputPath);
            Assert.Equal("novel_spread.pdf", vm.OutputFileName);
        }

        [Theory]
        [InlineData("book.PDF")]
        [InlineData("BOOK.Pdf")]
        public void PdfExtensionIsCaseInsensitive(string name)
        {
            MainViewModel vm = new ViewModelHarness().Vm;
            vm.SetInput(Path.Combine("C:", "d", name));
            Assert.Equal(ConversionState.Ready, vm.State);
        }

        [Fact]
        public void NonPdfMovesToErrorWithNotPdfMessage()
        {
            var h = new ViewModelHarness();

            h.Vm.SetInput(Path.Combine("C:", "d", "image.png"));

            Assert.Equal(ConversionState.Error, h.Vm.State);
            Assert.Equal("NOT_PDF", h.Vm.ErrorMessage);
            Assert.Null(h.Vm.InputPath);
        }

        [Fact]
        public void IsIgnoredWhileConverting()
        {
            MainViewModel vm = new ViewModelHarness().Vm;
            vm.State = ConversionState.Converting;

            vm.SetInput(Path.Combine("C:", "d", "other.pdf"));

            Assert.Equal(ConversionState.Converting, vm.State);
            Assert.Null(vm.InputPath);
        }

        [Theory]
        [InlineData("book.pdf", "book_spread.pdf")]
        [InlineData("a.b.pdf", "a.b_spread.pdf")]
        [InlineData("my book (1).pdf", "my book (1)_spread.pdf")]
        public void DeriveOutputPathAppendsSpreadSuffix(string input, string expected)
        {
            string dir = Path.Combine("C:", "docs");
            Assert.Equal(
                Path.Combine(dir, expected),
                MainViewModel.DeriveOutputPath(Path.Combine(dir, input)));
        }

        [Theory]
        [InlineData("book", "book_spread.pdf")]      // no extension: suffix still appended
        [InlineData("book.pdf", "book_spread.pdf")]  // no directory component
        [InlineData(".pdf", "_spread.pdf")]          // extension-only name
        public void DeriveOutputPathHandlesBareFileNames(string input, string expected) =>
            Assert.Equal(expected, MainViewModel.DeriveOutputPath(input));

        [Fact]
        public void DeriveOutputPathKeepsTheUncDirectory()
        {
            string input = Path.Combine(@"\\server\share", "book.pdf");
            string expected = Path.Combine(@"\\server\share", "book_spread.pdf");
            Assert.Equal(expected, MainViewModel.DeriveOutputPath(input));
        }
    }

    public sealed class ModeSelection
    {
        [Theory]
        [InlineData(0, FirstPageMode.Standard)]
        [InlineData(1, FirstPageMode.Cover)]
        [InlineData(2, FirstPageMode.LeadingBlank)]
        [InlineData(3, FirstPageMode.Standard)]
        [InlineData(-1, FirstPageMode.Standard)]
        public void MapsOpeningIndexToMode(int index, FirstPageMode expected)
        {
            MainViewModel vm = new ViewModelHarness().Vm;
            vm.SelectedOpeningIndex = index;
            Assert.Equal(expected, vm.SelectedMode);
        }
    }

    public sealed class Conversion
    {
        [Fact]
        public async Task HappyPathRunsThroughConvertingToDone()
        {
            var h = new ViewModelHarness(pageCount: 4); // Standard -> 2 spreads
            string input = Path.Combine("C:", "books", "novel.pdf");
            h.Vm.SetInput(input);

            await h.Vm.ConvertCommand.ExecuteAsync(null);

            Assert.Equal(ConversionState.Done, h.Vm.State);
            h.Engine.Received(1).OpenSource(input);
            h.Writer.Received(1).Save(Path.Combine("C:", "books", "novel_spread.pdf"));
            Assert.False(h.Vm.ProgressIndeterminate);
            Assert.Equal(100d, h.Vm.ProgressValue);
            Assert.Equal("2/2", h.Vm.ProgressText);
        }

        [Fact]
        public async Task SpreadExceptionMovesToErrorWithMappedMessage()
        {
            var h = new ViewModelHarness();
            h.Engine.OpenSource(Arg.Any<string>())
                .Throws(new SpreadException(ErrorKind.PdfCorrupted));
            h.Vm.SetInput(Path.Combine("C:", "d", "book.pdf"));

            await h.Vm.ConvertCommand.ExecuteAsync(null);

            Assert.Equal(ConversionState.Error, h.Vm.State);
            Assert.Equal("ERR:PdfCorrupted", h.Vm.ErrorMessage);
        }

        [Fact]
        public async Task UnexpectedExceptionMapsToInternal()
        {
            var h = new ViewModelHarness();
            h.Engine.OpenSource(Arg.Any<string>()).Throws(new InvalidOperationException("boom"));
            h.Vm.SetInput(Path.Combine("C:", "d", "book.pdf"));

            await h.Vm.ConvertCommand.ExecuteAsync(null);

            Assert.Equal(ConversionState.Error, h.Vm.State);
            Assert.Equal("ERR:Internal", h.Vm.ErrorMessage);
        }

        [Fact]
        public async Task ReturnsWithoutConvertingWhenInputMissing()
        {
            var h = new ViewModelHarness();
            h.Vm.State = ConversionState.Ready; // Ready but no InputPath/OutputPath set.

            await h.Vm.ConvertCommand.ExecuteAsync(null);

            h.Engine.DidNotReceive().OpenSource(Arg.Any<string>());
        }

        [Fact]
        public void ConvertCommandExecutableOnlyWhenReady()
        {
            MainViewModel vm = new ViewModelHarness().Vm;
            foreach (ConversionState state in AllStates)
            {
                vm.State = state;
                Assert.Equal(state == ConversionState.Ready, vm.ConvertCommand.CanExecute(null));
            }
        }

        [Fact]
        public void ConvertCommandNotifiesCanExecuteOnStateChange()
        {
            MainViewModel vm = new ViewModelHarness().Vm;
            var raised = 0;
            vm.ConvertCommand.CanExecuteChanged += (_, _) => raised++;

            vm.State = ConversionState.Ready;

            Assert.True(raised > 0);
        }
    }

    public sealed class ProgressReporting
    {
        [Fact]
        public void PercentageIsProportionalAndBounded() =>
            Gen.Int[1, 10_000]
                .SelectMany(total => Gen.Int[0, total].Select(done => (done, total)))
                .Sample(t =>
                {
                    MainViewModel vm = new ViewModelHarness().Vm;
                    vm.Report(t.done, t.total);
                    double expected = (double)t.done / t.total * 100d;
                    Assert.Equal(expected, vm.ProgressValue, 6);
                    Assert.InRange(vm.ProgressValue, 0d, 100d);
                    Assert.False(vm.ProgressIndeterminate);
                });

        [Fact]
        public void ZeroTotalYieldsZeroPercent()
        {
            MainViewModel vm = new ViewModelHarness().Vm;
            vm.Report(0, 0);
            Assert.Equal(0d, vm.ProgressValue);
        }

        [Fact]
        public void ProgressIncreasesMonotonicallyAcrossSuccessiveReports()
        {
            MainViewModel vm = new ViewModelHarness().Vm;
            double previous = -1d;
            for (int done = 0; done <= 5; done++)
            {
                vm.Report(done, 5);
                Assert.True(vm.ProgressValue >= previous);
                Assert.InRange(vm.ProgressValue, 0d, 100d);
                previous = vm.ProgressValue;
            }

            Assert.Equal(100d, vm.ProgressValue);
        }
    }

    public sealed class ResetAndOutput
    {
        [Fact]
        public void ResetClearsEverythingToIdle()
        {
            MainViewModel vm = new ViewModelHarness().Vm;
            vm.SetInput(Path.Combine("C:", "d", "book.pdf"));
            vm.ErrorMessage = "x";
            vm.ProgressValue = 50;

            vm.ResetCommand.Execute(null);

            Assert.Equal(ConversionState.Idle, vm.State);
            Assert.Null(vm.InputPath);
            Assert.Null(vm.OutputPath);
            Assert.Equal(string.Empty, vm.InputFileName);
            Assert.Equal(string.Empty, vm.OutputFileName);
            Assert.Equal(string.Empty, vm.ErrorMessage);
            Assert.Equal(0d, vm.ProgressValue);
        }

        [Fact]
        public void ResetNotifiesEveryClearedProperty()
        {
            MainViewModel vm = new ViewModelHarness().Vm;
            vm.SetInput(Path.Combine("C:", "d", "book.pdf"));
            // Dirty the progress/error fields so Reset actually changes (and notifies) them.
            vm.ErrorMessage = "boom";
            vm.ProgressValue = 42;
            vm.ProgressText = "3/7";

            var raised = new List<string>();
            vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

            vm.ResetCommand.Execute(null);

            foreach (string name in new[]
            {
                nameof(vm.InputPath),
                nameof(vm.InputFileName),
                nameof(vm.OutputPath),
                nameof(vm.OutputFileName),
                nameof(vm.ErrorMessage),
                nameof(vm.ProgressValue),
                nameof(vm.ProgressText),
                nameof(vm.State),
            })
            {
                Assert.Contains(name, raised);
            }
        }

        [Fact]
        public void OpenOutputIsExecutableOnlyWhenTheFileExists()
        {
            MainViewModel vm = new ViewModelHarness().Vm;

            Assert.False(vm.OpenOutputCommand.CanExecute(null)); // OutputPath null

            string tmp = Path.GetTempFileName();
            try
            {
                vm.OutputPath = tmp;
                Assert.True(vm.OpenOutputCommand.CanExecute(null));

                vm.OutputPath = tmp + ".missing";
                Assert.False(vm.OpenOutputCommand.CanExecute(null));
            }
            finally
            {
                File.Delete(tmp);
            }
        }

        [Fact]
        public void OpenOutputAndShowInFolderDelegateToShell()
        {
            var h = new ViewModelHarness();
            string tmp = Path.GetTempFileName();
            try
            {
                h.Vm.OutputPath = tmp;

                h.Vm.OpenOutputCommand.Execute(null);
                h.Vm.ShowInFolderCommand.Execute(null);

                h.Shell.Received(1).Open(tmp);
                h.Shell.Received(1).ShowInFolder(tmp);
            }
            finally
            {
                File.Delete(tmp);
            }
        }
    }

    public sealed class TransitionMatrix
    {
        [Fact]
        public async Task IdleToReadyToDoneToIdle()
        {
            var h = new ViewModelHarness(pageCount: 4);
            Assert.Equal(ConversionState.Idle, h.Vm.State);

            h.Vm.SetInput(Path.Combine("C:", "d", "book.pdf"));
            Assert.Equal(ConversionState.Ready, h.Vm.State);

            await h.Vm.ConvertCommand.ExecuteAsync(null);
            Assert.Equal(ConversionState.Done, h.Vm.State);

            h.Vm.ResetCommand.Execute(null);
            Assert.Equal(ConversionState.Idle, h.Vm.State);
        }

        [Fact]
        public async Task ReadyToErrorToIdle()
        {
            var h = new ViewModelHarness();
            h.Engine.OpenSource(Arg.Any<string>()).Throws(new SpreadException(ErrorKind.PdfWriteFailed));

            h.Vm.SetInput(Path.Combine("C:", "d", "book.pdf"));
            await h.Vm.ConvertCommand.ExecuteAsync(null);
            Assert.Equal(ConversionState.Error, h.Vm.State);

            h.Vm.ResetCommand.Execute(null);
            Assert.Equal(ConversionState.Idle, h.Vm.State);
        }

        [Fact]
        public async Task DoneAcceptsNewInputBackToReady()
        {
            var h = new ViewModelHarness(pageCount: 4);
            h.Vm.SetInput(Path.Combine("C:", "d", "first.pdf"));
            await h.Vm.ConvertCommand.ExecuteAsync(null);
            Assert.Equal(ConversionState.Done, h.Vm.State);

            string second = Path.Combine("C:", "d", "second.pdf");
            h.Vm.SetInput(second);
            Assert.Equal(ConversionState.Ready, h.Vm.State);
            Assert.Equal(second, h.Vm.InputPath);
        }

        [Fact]
        public async Task DoneRejectsNonPdfIntoError()
        {
            var h = new ViewModelHarness(pageCount: 4);
            h.Vm.SetInput(Path.Combine("C:", "d", "book.pdf"));
            await h.Vm.ConvertCommand.ExecuteAsync(null);
            Assert.Equal(ConversionState.Done, h.Vm.State);

            h.Vm.SetInput(Path.Combine("C:", "d", "image.png"));
            Assert.Equal(ConversionState.Error, h.Vm.State);
            Assert.Equal("NOT_PDF", h.Vm.ErrorMessage);
        }

        [Fact]
        public void ErrorRecoversToReadyOnValidInput()
        {
            var h = new ViewModelHarness();
            h.Vm.SetInput(Path.Combine("C:", "d", "image.png")); // -> Error
            Assert.Equal(ConversionState.Error, h.Vm.State);

            h.Vm.SetInput(Path.Combine("C:", "d", "book.pdf"));
            Assert.Equal(ConversionState.Ready, h.Vm.State);
        }

        [Theory]
        [InlineData(ConversionState.Idle)]
        [InlineData(ConversionState.Converting)]
        [InlineData(ConversionState.Done)]
        [InlineData(ConversionState.Error)]
        public void ConvertIsExecutableOnlyFromReady(ConversionState state)
        {
            MainViewModel vm = new ViewModelHarness().Vm;
            vm.State = state;
            Assert.False(vm.ConvertCommand.CanExecute(null));
        }
    }
}
