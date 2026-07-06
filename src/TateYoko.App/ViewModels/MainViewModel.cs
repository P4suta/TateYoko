using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using TateYoko.App.Services;
using TateYoko.Core.Application;
using TateYoko.Core.Domain;

namespace TateYoko.App.ViewModels;

/// <summary>
/// ViewModel for the single screen. Delegates conversion to <see cref="SpreadConversionService"/>
/// and marshals progress (<see cref="IConversionProgress"/>) back to the UI thread.
/// </summary>
public partial class MainViewModel : ObservableObject, IConversionProgress
{
    private readonly SpreadConversionService _service;
    private readonly DispatcherQueue _dispatcher;

    public MainViewModel(SpreadConversionService service)
    {
        _service = service;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle), nameof(IsReady), nameof(IsConverting), nameof(IsDone), nameof(IsError))]
    [NotifyCanExecuteChangedFor(nameof(ConvertCommand))]
    public partial ConversionState State { get; set; } = ConversionState.Idle;

    public bool IsIdle => State == ConversionState.Idle;

    public bool IsReady => State == ConversionState.Ready;

    public bool IsConverting => State == ConversionState.Converting;

    public bool IsDone => State == ConversionState.Done;

    public bool IsError => State == ConversionState.Error;

    [ObservableProperty]
    public partial string? InputPath { get; set; }

    [ObservableProperty]
    public partial string InputFileName { get; set; } = string.Empty;

    /// <summary>0 = from the right (standard), 1 = cover, 2 = from the left.</summary>
    [ObservableProperty]
    public partial int SelectedOpeningIndex { get; set; }

    [ObservableProperty]
    public partial double ProgressValue { get; set; }

    [ObservableProperty]
    public partial bool ProgressIndeterminate { get; set; } = true;

    [ObservableProperty]
    public partial string ProgressText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? OutputPath { get; set; }

    [ObservableProperty]
    public partial string OutputFileName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    /// <summary>Accepts an input PDF, derives the output path, and moves to Ready. Called from drop and picker.</summary>
    public void SetInput(string path)
    {
        if (State == ConversionState.Converting)
        {
            return;
        }

        if (!path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = ErrorMessages.NotPdf;
            State = ConversionState.Error;
            return;
        }

        InputPath = path;
        InputFileName = Path.GetFileName(path);
        OutputPath = DeriveOutputPath(path);
        OutputFileName = Path.GetFileName(OutputPath);
        State = ConversionState.Ready;
    }

    [RelayCommand(CanExecute = nameof(CanConvert))]
    private async Task ConvertAsync()
    {
        if (InputPath is null || OutputPath is null)
        {
            return;
        }

        var request = new SpreadRequest(InputPath, OutputPath, SelectedMode);
        ProgressIndeterminate = true;
        ProgressValue = 0;
        ProgressText = Localized.Get("ProgressStarting");
        State = ConversionState.Converting;

        try
        {
            await Task.Run(() => _service.Convert(request, this));
            State = ConversionState.Done;
        }
        catch (SpreadException ex)
        {
            ErrorMessage = ErrorMessages.ForKind(ex.Kind);
            State = ConversionState.Error;
        }
        catch (Exception)
        {
            ErrorMessage = ErrorMessages.ForKind(ErrorKind.Internal);
            State = ConversionState.Error;
        }
    }

    [RelayCommand(CanExecute = nameof(CanOpenOutput))]
    private void OpenOutput() => LaunchShell(OutputPath!, selectInFolder: false);

    [RelayCommand(CanExecute = nameof(CanOpenOutput))]
    private void ShowInFolder() => LaunchShell(OutputPath!, selectInFolder: true);

    [RelayCommand]
    private void Reset()
    {
        InputPath = null;
        InputFileName = string.Empty;
        OutputPath = null;
        OutputFileName = string.Empty;
        ErrorMessage = string.Empty;
        ProgressValue = 0;
        ProgressText = string.Empty;
        State = ConversionState.Idle;
    }

    /// <summary>Marshals progress reported from a background thread onto the UI thread.</summary>
    public void Report(int completedSpreads, int totalSpreads)
    {
        _dispatcher.TryEnqueue(() =>
        {
            ProgressIndeterminate = false;
            ProgressValue = totalSpreads == 0 ? 0 : (double)completedSpreads / totalSpreads * 100;
            ProgressText = Localized.Get("ProgressFormat", completedSpreads, totalSpreads);
        });
    }

    partial void OnStateChanged(ConversionState value)
    {
        OpenOutputCommand.NotifyCanExecuteChanged();
        ShowInFolderCommand.NotifyCanExecuteChanged();
    }

    private bool CanConvert => State == ConversionState.Ready;

    private bool CanOpenOutput() => OutputPath is not null && File.Exists(OutputPath);

    private FirstPageMode SelectedMode => SelectedOpeningIndex switch
    {
        1 => FirstPageMode.Cover,
        2 => FirstPageMode.LeadingBlank,
        _ => FirstPageMode.Standard,
    };

    private static string DeriveOutputPath(string input)
    {
        string dir = Path.GetDirectoryName(input) ?? ".";
        string name = Path.GetFileNameWithoutExtension(input);
        return Path.Combine(dir, $"{name}_spread.pdf");
    }

    private static void LaunchShell(string path, bool selectInFolder)
    {
        if (selectInFolder)
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
        }
        else
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
    }
}
