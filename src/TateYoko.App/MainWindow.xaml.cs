using System;
using System.IO;
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

        // AppWindow.SetIcon takes a file-system path (not an ms-appx URI), so resolve it against
        // the app folder rather than the process CWD: the launcher (TateYoko.exe) starts us with an
        // arbitrary working directory, and dotnet publish drops loose Assets\ unless we copy them
        // back (see CopyAppIconToPublishDir in the .csproj).
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));

        RootFrame.Navigate(typeof(MainPage));
    }
}
