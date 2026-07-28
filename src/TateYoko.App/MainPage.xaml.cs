using System.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Windows.Storage.Pickers;
using TateYoko.App.Services;
using TateYoko.App.ViewModels;
using TateYoko.Engine;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace TateYoko.App;

public sealed partial class MainPage : Page
{
    private readonly WindowId _windowId;
    private bool _detached;

    internal MainPage(MainViewModel viewModel, WindowId windowId)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ViewModel = viewModel;
        _windowId = windowId;
        InitializeComponent();
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    internal MainViewModel ViewModel { get; }

    internal void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (ViewModel.IsConverting || !e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.None;
            DragOverlay.Visibility = Visibility.Collapsed;
            return;
        }

        e.AcceptedOperation = DataPackageOperation.Copy;
        e.DragUIOverride.Caption = Localized.Get("DragCaption");
        e.DragUIOverride.IsCaptionVisible = true;
        e.DragUIOverride.IsGlyphVisible = true;
        DragOverlay.Visibility = Visibility.Visible;
    }

    private void OnDragLeave(object sender, DragEventArgs e) =>
        DragOverlay.Visibility = Visibility.Collapsed;

    private async void OnDrop(object sender, DragEventArgs e)
    {
        DragOverlay.Visibility = Visibility.Collapsed;
        if (ViewModel.IsConverting || !e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        DragOperationDeferral deferral = e.GetDeferral();
        try
        {
            IReadOnlyList<IStorageItem> items = await e
                .DataView.GetStorageItemsAsync()
                .AsTask()
                .ConfigureAwait(true);
            if (_detached)
            {
                return;
            }

            if (items.Count != 1 || items[0] is not StorageFile file)
            {
                ViewModel.ShowSelectionError(Localized.Get("DropOnePdf"));
                return;
            }

            ViewModel.SetInput(file.Path);
        }
        catch (Exception exception)
        {
            ReportUnexpectedError(exception);
        }
        finally
        {
            deferral.Complete();
        }
    }

    private async void OnChoosePdfClicked(object sender, RoutedEventArgs e) =>
        await PickPdfAsync().ConfigureAwait(true);

    private async void OnOpenAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args
    )
    {
        args.Handled = true;
        await PickPdfAsync().ConfigureAwait(true);
    }

    private async Task PickPdfAsync()
    {
        if (_detached || ViewModel.IsConverting)
        {
            return;
        }

        try
        {
            var picker = new FileOpenPicker(_windowId)
            {
                Title = Localized.Get("OpenPickerTitle"),
                CommitButtonText = Localized.Get("OpenPickerCommit"),
            };
            picker.FileTypeFilter.Add(".pdf");
            PickFileResult? result = await picker
                .PickSingleFileAsync()
                .AsTask()
                .ConfigureAwait(true);
            if (!_detached && result is not null)
            {
                ViewModel.SetInput(result.Path);
            }
        }
        catch (Exception exception)
        {
            ReportUnexpectedError(exception);
        }
    }

    private async void OnConvertClicked(object sender, RoutedEventArgs e) =>
        await PickOutputAndConvertAsync().ConfigureAwait(true);

    private async void OnRetryClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel.PrepareRetry())
        {
            await PickOutputAndConvertAsync().ConfigureAwait(true);
        }
    }

    private async Task PickOutputAndConvertAsync()
    {
        if (_detached || !ViewModel.IsReady)
        {
            return;
        }

        try
        {
            var picker = new FileSavePicker(_windowId)
            {
                Title = Localized.Get("SavePickerTitle"),
                CommitButtonText = Localized.Get("SavePickerCommit"),
                SuggestedFileName = ViewModel.SuggestedOutputFileName,
                DefaultFileExtension = ".pdf",
                ShowOverwritePrompt = true,
            };
            picker.FileTypeChoices.Add(Localized.Get("PdfFileType"), [".pdf"]);
            PickFileResult? result = await picker.PickSaveFileAsync().AsTask().ConfigureAwait(true);
            if (!_detached && result is not null)
            {
                await ViewModel.ConvertToAsync(result.Path).ConfigureAwait(true);
            }
        }
        catch (Exception exception)
        {
            ReportUnexpectedError(exception);
        }
    }

    private void OnOpeningModeChanged(object sender, SelectionChangedEventArgs e)
    {
        FirstPageMode? mode = OpeningModeRadioButtons.SelectedIndex switch
        {
            0 => FirstPageMode.Standard,
            1 => FirstPageMode.Cover,
            2 => FirstPageMode.LeadingBlank,
            _ => null,
        };
        if (mode is FirstPageMode selectedMode)
        {
            ViewModel.SetFirstPageMode(selectedMode);
        }
    }

    private async void OnRetryPasswordClicked(object sender, RoutedEventArgs e)
    {
        if (_detached)
        {
            return;
        }

        string password = PasswordInput.Password;
        PasswordInput.Password = string.Empty;
        await ViewModel.ConvertWithPasswordAsync(password).ConfigureAwait(true);
    }

    private void ReportUnexpectedError(Exception exception)
    {
        if (!_detached)
        {
            ViewModel.ShowUnexpectedError(exception);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_detached || e.PropertyName != nameof(MainViewModel.State))
        {
            return;
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            if (_detached)
            {
                return;
            }

            FocusState state = FocusState.Programmatic;
            switch (ViewModel.State)
            {
                case ConversionState.Idle:
                    ChoosePdfButton?.Focus(state);
                    break;
                case ConversionState.Ready:
                    ConvertButton?.Focus(state);
                    break;
                case ConversionState.Converting:
                    CancelButton?.Focus(state);
                    break;
                case ConversionState.PasswordRequired:
                    PasswordInput?.Focus(state);
                    break;
                case ConversionState.Done:
                    OpenOutputButton?.Focus(state);
                    break;
                case ConversionState.Error:
                    if (ViewModel.CanRetryCurrentError)
                    {
                        RetryButton?.Focus(state);
                    }
                    else
                    {
                        ErrorChooseAnotherButton?.Focus(state);
                    }

                    break;
            }

            AutomationPeer? peer =
                FrameworkElementAutomationPeer.FromElement(LiveRegionText)
                ?? FrameworkElementAutomationPeer.CreatePeerForElement(LiveRegionText);
            peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        });
    }
}
