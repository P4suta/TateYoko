using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using TateYoko.App.Services;
using TateYoko.App.ViewModels;
using Windows.Graphics;

namespace TateYoko.App;

/// <summary>The compact, single-purpose application window.</summary>
public sealed partial class MainWindow : Window
{
    private const int InitialWidthDip = 640;
    private const int InitialHeightDip = 560;
    private const int MinimumWidthDip = 520;
    private const int MinimumHeightDip = 480;
    private readonly MainPage _mainPage;
    private readonly MainViewModel _viewModel;

    internal MainWindow(MainViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        InitializeComponent();

        Title = Localized.Get("AppTitle");
        AppTitleBar.Title = Title;
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        _mainPage = new MainPage(viewModel, AppWindow.Id);
        RootContent.Content = _mainPage;
        SizeWindow();
        AppWindow.Changed += OnAppWindowChanged;
        Closed += OnClosed;
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32.dll")]
    private static partial uint GetDpiForWindow(nint windowHandle);

    private double DpiScale
    {
        get
        {
            nint windowHandle = Win32Interop.GetWindowFromWindowId(AppWindow.Id);
            uint dpi = GetDpiForWindow(windowHandle);
            return dpi == 0 ? 1d : dpi / 96d;
        }
    }

    private void SizeWindow() => AppWindow.Resize(ToPixels(InitialWidthDip, InitialHeightDip));

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (!args.DidSizeChange)
        {
            return;
        }

        SizeInt32 minimum = ToPixels(MinimumWidthDip, MinimumHeightDip);
        SizeInt32 current = sender.Size;
        int width = Math.Max(current.Width, minimum.Width);
        int height = Math.Max(current.Height, minimum.Height);
        if (width != current.Width || height != current.Height)
        {
            sender.Resize(new SizeInt32(width, height));
        }
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        AppWindow.Changed -= OnAppWindowChanged;
        Closed -= OnClosed;
        _mainPage.Detach();
        RootContent.Content = null;
        _viewModel.Dispose();
    }

    private SizeInt32 ToPixels(int widthDip, int heightDip)
    {
        double scale = DpiScale;
        return new SizeInt32(
            (int)Math.Ceiling(widthDip * scale),
            (int)Math.Ceiling(heightDip * scale)
        );
    }
}
