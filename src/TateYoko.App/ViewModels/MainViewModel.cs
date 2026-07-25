using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TateYoko.App.Services;
using TateYoko.Engine;

namespace TateYoko.App.ViewModels;

internal partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IPdfSpreadConverter _converter;
    private readonly IDiagnosticLog _diagnosticLog;
    private readonly IShellLauncher _shell;
    private readonly IUiStrings _strings;
    private CancellationTokenSource? _conversionCancellation;
    private string? _inputPath;
    private string? _outputPath;
    private OutputCollisionPolicy _collisionPolicy = OutputCollisionPolicy.CreateUnique;
    private bool _disposed;

    internal MainViewModel(
        IPdfSpreadConverter converter,
        IUiStrings strings,
        IShellLauncher shell,
        IDiagnosticLog diagnosticLog
    )
    {
        _converter = converter ?? throw new ArgumentNullException(nameof(converter));
        _strings = strings ?? throw new ArgumentNullException(nameof(strings));
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _diagnosticLog = diagnosticLog ?? throw new ArgumentNullException(nameof(diagnosticLog));
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(IsIdle),
        nameof(IsReady),
        nameof(IsConverting),
        nameof(IsPasswordRequired),
        nameof(IsDone),
        nameof(IsError),
        nameof(CanRetryCurrentError)
    )]
    [NotifyCanExecuteChangedFor(nameof(ConvertCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    [NotifyCanExecuteChangedFor(nameof(RetryCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenOutputCommand))]
    [NotifyCanExecuteChangedFor(nameof(ShowInFolderCommand))]
    public partial ConversionState State { get; private set; } = ConversionState.Idle;

    internal bool IsIdle => State == ConversionState.Idle;

    internal bool IsReady => State == ConversionState.Ready;

    internal bool IsConverting => State == ConversionState.Converting;

    internal bool IsPasswordRequired => State == ConversionState.PasswordRequired;

    internal bool IsDone => State == ConversionState.Done;

    internal bool IsError => State == ConversionState.Error;

    [ObservableProperty]
    public partial string InputFileName { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string InputFolder { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string OutputFileName { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string OutputFolder { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial double ProgressValue { get; private set; }

    [ObservableProperty]
    public partial string ProgressText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string ErrorMessage { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusAnnouncement { get; private set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActionError))]
    public partial string ActionErrorMessage { get; private set; } = string.Empty;

    internal bool HasActionError => ActionErrorMessage.Length > 0;

    internal FirstPageMode FirstPageMode { get; private set; }

    internal int FirstPageModeIndex => (int)FirstPageMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRetryCurrentError))]
    [NotifyCanExecuteChangedFor(nameof(RetryCommand))]
    internal partial PdfSpreadError? LastError { get; private set; }

    internal bool CanRetryCurrentError => CanRetry();

    internal void SetInput(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsConverting)
        {
            return;
        }

        if (!TryNormalizePdfPath(path, out string fullPath))
        {
            ShowError(PdfSpreadError.UnsupportedFile);
            return;
        }

        if (!File.Exists(fullPath))
        {
            ShowError(PdfSpreadError.InputNotFound);
            return;
        }

        _inputPath = fullPath;
        _outputPath = DeriveOutputPath(fullPath);
        _collisionPolicy = OutputCollisionPolicy.CreateUnique;
        InputFileName = Path.GetFileName(fullPath);
        InputFolder = Path.GetDirectoryName(fullPath) ?? string.Empty;
        SetOutputDisplay(_outputPath);
        ErrorMessage = string.Empty;
        LastError = null;
        ProgressValue = 0;
        ProgressText = string.Empty;
        ActionErrorMessage = string.Empty;
        StatusAnnouncement = InputFileName;
        State = ConversionState.Ready;
    }

    internal void SetExplicitOutput(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsReady || !TryNormalizePdfPath(path, out string fullPath))
        {
            ShowError(PdfSpreadError.InvalidRequest);
            return;
        }

        if (
            string.Equals(_inputPath, fullPath, StringComparison.OrdinalIgnoreCase)
            || !Directory.Exists(Path.GetDirectoryName(fullPath))
        )
        {
            ShowError(PdfSpreadError.InvalidRequest);
            return;
        }

        _outputPath = fullPath;
        _collisionPolicy = OutputCollisionPolicy.ReplaceExisting;
        SetOutputDisplay(_outputPath);
    }

    internal void SetFirstPageMode(FirstPageMode mode)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        if (FirstPageMode != mode)
        {
            FirstPageMode = mode;
            OnPropertyChanged(nameof(FirstPageModeIndex));
        }
    }

    internal void ShowSelectionError(string message)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ErrorMessage = message;
        LastError = PdfSpreadError.InvalidRequest;
        StatusAnnouncement = message;
        State = ConversionState.Error;
    }

    internal void ShowUnexpectedError(Exception exception)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _diagnosticLog.Write(exception);
        ShowError(PdfSpreadError.Internal);
    }

    internal Task ConvertWithPasswordAsync(string password)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsPasswordRequired)
        {
            return Task.CompletedTask;
        }

        return RunConversionAsync(password);
    }

    [RelayCommand(CanExecute = nameof(CanConvert))]
    private Task ConvertAsync() => RunConversionAsync(password: null);

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => _conversionCancellation?.Cancel();

    [RelayCommand(CanExecute = nameof(CanRetry))]
    private Task RetryAsync()
    {
        if (LastError == PdfSpreadError.InvalidRequest)
        {
            ErrorMessage = string.Empty;
            LastError = null;
            StatusAnnouncement = InputFileName;
            State = ConversionState.Ready;
            return Task.CompletedTask;
        }

        return RunConversionAsync(password: null);
    }

    [RelayCommand(CanExecute = nameof(CanOpenOutput))]
    private void OpenOutput() => RunOutputAction(_shell.Open);

    [RelayCommand(CanExecute = nameof(CanOpenOutput))]
    private void ShowInFolder() => RunOutputAction(_shell.ShowInFolder);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _conversionCancellation?.Cancel();
        _conversionCancellation?.Dispose();
        _conversionCancellation = null;
        GC.SuppressFinalize(this);
    }

    internal static string DeriveOutputPath(string inputPath)
    {
        string directory =
            Path.GetDirectoryName(inputPath)
            ?? throw new ArgumentException("Input path has no directory.", nameof(inputPath));
        string stem = Path.GetFileNameWithoutExtension(inputPath);
        return Path.Combine(directory, $"{stem}_spread.pdf");
    }

    private async Task RunConversionAsync(string? password)
    {
        if (_inputPath is null || _outputPath is null || IsConverting)
        {
            return;
        }

        _conversionCancellation?.Dispose();
        var conversionCancellation = new CancellationTokenSource();
        _conversionCancellation = conversionCancellation;
        CancellationToken cancellationToken = conversionCancellation.Token;
        ProgressValue = 0;
        ProgressText = _strings.ProgressStarting;
        ErrorMessage = string.Empty;
        ActionErrorMessage = string.Empty;
        LastError = null;
        StatusAnnouncement = ProgressText;
        State = ConversionState.Converting;

        void ApplyProgress(PdfSpreadProgress value)
        {
            if (
                _disposed
                || !ReferenceEquals(_conversionCancellation, conversionCancellation)
                || !IsConverting
            )
            {
                return;
            }

            ProgressValue = value.Fraction * 100;
            ProgressText = _strings.Progress(value.CompletedSpreads, value.TotalSpreads);
            StatusAnnouncement = ProgressText;
        }

        IProgress<PdfSpreadProgress> progress = SynchronizationContext.Current is null
            ? new InlineProgress<PdfSpreadProgress>(ApplyProgress)
            : new Progress<PdfSpreadProgress>(ApplyProgress);

        try
        {
            var request = new PdfSpreadRequest(
                _inputPath,
                _outputPath,
                FirstPageMode,
                _collisionPolicy,
                password,
                preservePasswordProtection: true
            );
            PdfSpreadResult result = await Task.Run(
                    () => _converter.Convert(request, progress, cancellationToken),
                    cancellationToken
                )
                .ConfigureAwait(true);
            if (_disposed || !ReferenceEquals(_conversionCancellation, conversionCancellation))
            {
                return;
            }

            _outputPath = result.OutputPath;
            SetOutputDisplay(result.OutputPath);
            ProgressValue = 100;
            ProgressText = _strings.Progress(result.SpreadCount, result.SpreadCount);
            StatusAnnouncement = _strings.Done;
            State = ConversionState.Done;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (!_disposed && ReferenceEquals(_conversionCancellation, conversionCancellation))
            {
                ProgressValue = 0;
                ProgressText = string.Empty;
                StatusAnnouncement = _strings.Cancelled;
                State = ConversionState.Ready;
            }
        }
        catch (PdfSpreadException exception)
            when (exception.Error
                    is PdfSpreadError.PasswordRequired
                        or PdfSpreadError.InvalidPassword
            )
        {
            if (!_disposed && ReferenceEquals(_conversionCancellation, conversionCancellation))
            {
                LastError = exception.Error;
                ErrorMessage = _strings.ForError(exception.Error);
                StatusAnnouncement = ErrorMessage;
                State = ConversionState.PasswordRequired;
            }
        }
        catch (PdfSpreadException exception)
        {
            if (!_disposed && ReferenceEquals(_conversionCancellation, conversionCancellation))
            {
                ShowError(exception.Error);
            }
        }
        catch (Exception exception)
        {
            _diagnosticLog.Write(exception);
            if (!_disposed && ReferenceEquals(_conversionCancellation, conversionCancellation))
            {
                ShowError(PdfSpreadError.Internal);
            }
        }
        finally
        {
            conversionCancellation.Dispose();
            if (ReferenceEquals(_conversionCancellation, conversionCancellation))
            {
                _conversionCancellation = null;
            }

            CancelCommand.NotifyCanExecuteChanged();
            OpenOutputCommand.NotifyCanExecuteChanged();
            ShowInFolderCommand.NotifyCanExecuteChanged();
        }
    }

    private void ShowError(PdfSpreadError error)
    {
        LastError = error;
        ErrorMessage = _strings.ForError(error);
        StatusAnnouncement = ErrorMessage;
        State = ConversionState.Error;
    }

    private void SetOutputDisplay(string outputPath)
    {
        OutputFileName = Path.GetFileName(outputPath);
        OutputFolder = Path.GetDirectoryName(outputPath) ?? string.Empty;
    }

    private static bool TryNormalizePdfPath(string? path, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            if (
                !Path.IsPathFullyQualified(path)
                || !string.Equals(
                    Path.GetExtension(path),
                    ".pdf",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return false;
            }

            fullPath = Path.GetFullPath(path);
            return true;
        }
        catch (Exception exception)
            when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private bool CanConvert() => IsReady && _inputPath is not null && _outputPath is not null;

    private bool CanCancel() => IsConverting && _conversionCancellation is not null;

    private bool CanRetry() =>
        IsError
        && LastError
            is PdfSpreadError.InvalidRequest
                or PdfSpreadError.ReadFailed
                or PdfSpreadError.WriteFailed
                or PdfSpreadError.Internal
        && _inputPath is not null
        && _outputPath is not null;

    private bool CanOpenOutput() => IsDone && _outputPath is not null && File.Exists(_outputPath);

    private void RunOutputAction(Action<string> action)
    {
        try
        {
            action(_outputPath!);
            ActionErrorMessage = string.Empty;
        }
        catch (Exception exception)
        {
            _diagnosticLog.Write(exception);
            ActionErrorMessage = _strings.OutputActionFailed;
            StatusAnnouncement = ActionErrorMessage;
        }
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
