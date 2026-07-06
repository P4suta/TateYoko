using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using TateYoko.App.Services;
using TateYoko.Presentation.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace TateYoko.App;

/// <summary>
/// The single conversion page. Handles UI/platform actions (drag-and-drop, file picking) and
/// delegates state and conversion to <see cref="MainViewModel"/>.
/// </summary>
public sealed partial class MainPage : Page
{
    public MainPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<MainViewModel>();
    }

    public MainViewModel ViewModel { get; }

    /// <summary>bool to Visibility helper for x:Bind.</summary>
    public Visibility ShowIf(bool condition) => condition ? Visibility.Visible : Visibility.Collapsed;

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = Localized.Get("DragCaption");
            e.DragUIOverride.IsGlyphVisible = true;
            DragOverlay.Visibility = Visibility.Visible;
        }
        else
        {
            e.AcceptedOperation = DataPackageOperation.None;
        }
    }

    private void OnDragLeave(object sender, DragEventArgs e) =>
        DragOverlay.Visibility = Visibility.Collapsed;

    private async void OnDrop(object sender, DragEventArgs e)
    {
        DragOverlay.Visibility = Visibility.Collapsed;

        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        DragOperationDeferral deferral = e.GetDeferral();
        try
        {
            IReadOnlyList<IStorageItem> items = await e.DataView.GetStorageItemsAsync();
            if (items.FirstOrDefault(i => i is StorageFile) is StorageFile file)
            {
                ViewModel.SetInput(file.Path);
            }
        }
        finally
        {
            deferral.Complete();
        }
    }

    // While idle, a click anywhere on the window opens the file picker.
    private async void OnRootTapped(object sender, TappedRoutedEventArgs e)
    {
        if (!ViewModel.IsIdle)
        {
            return;
        }

        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".pdf");

        // Unpackaged apps must associate the picker with the HWND.
        WinRT.Interop.InitializeWithWindow.Initialize(picker, App.WindowHandle);

        StorageFile? file = await picker.PickSingleFileAsync();
        if (file is not null)
        {
            ViewModel.SetInput(file.Path);
        }
    }
}
