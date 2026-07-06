using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using TateYoko.App.ViewModels;
using TateYoko.Core.Application;
using TateYoko.Core.Ports;
using TateYoko.Pdf;

namespace TateYoko.App;

/// <summary>
/// Application and composition root. Configures the DI container and injects the PDFsharp adapter
/// (<see cref="PdfSharpEngine"/>) into Core's services.
/// </summary>
public partial class App : Application
{
    /// <summary>DI service provider, used to resolve pages and view models.</summary>
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>The main window, used for HWND interop (e.g. file pickers).</summary>
    public static Window Window { get; private set; } = null!;

    /// <summary>The UI thread dispatcher.</summary>
    public static Microsoft.UI.Dispatching.DispatcherQueue DispatcherQueue { get; private set; } = null!;

    /// <summary>The native window handle (HWND) for picker InitializeWithWindow.</summary>
    public static nint WindowHandle => WinRT.Interop.WindowNative.GetWindowHandle(Window);

    public App()
    {
        InitializeComponent();
        Services = ConfigureServices();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Window = new MainWindow();
        DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        Window.Activate();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // The only PDF dependency: the injection point at the composition root.
        services.AddSingleton<IPdfEngine, PdfSharpEngine>();
        services.AddSingleton<SpreadConversionService>();
        services.AddTransient<MainViewModel>();

        return services.BuildServiceProvider();
    }
}
