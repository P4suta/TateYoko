using Microsoft.UI.Xaml;

namespace TateYoko.App;

/// <summary>The application window: a custom title bar and a Frame hosting the content.</summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Window.Title is not resolved via x:Uid, so set it from code.
        Title = "縦横";

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");

        RootFrame.Navigate(typeof(MainPage));
    }
}
