using Microsoft.UI.Xaml;
using TateYoko.App.Services;
using TateYoko.App.ViewModels;
using TateYoko.Engine;

namespace TateYoko.App;

public partial class App : Application, IDisposable
{
    private MainViewModel? _viewModel;
    private MainWindow? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (_window is not null)
        {
            _window.Activate();
            return;
        }

        _viewModel = new MainViewModel(
            new PdfSpreadConverter(),
            new ResourceUiStrings(),
            new ShellLauncher(),
            new DiagnosticLog()
        );
        _window = new MainWindow(_viewModel);
        _window.Closed += OnWindowClosed;
        _window.Activate();
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        Dispose();
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        _window?.Closed -= OnWindowClosed;
        _window = null;
        _viewModel?.Dispose();
        _viewModel = null;
    }
}
