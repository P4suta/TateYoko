using System.Diagnostics;
using System.Runtime.InteropServices;

// Native launcher for the distribution bundle. Starts the neighboring app\TateYoko.App.exe and
// forwards the received arguments (e.g. a path from a file association or "Open with"). The apphost
// resolves its DLLs from its own directory, not the CWD, so the default working directory is fine.
// This is a GUI app, so it exits without waiting once the child is started.

var launcherDir = Path.GetDirectoryName(Environment.ProcessPath)
                  ?? AppContext.BaseDirectory;
var appExe = Path.Combine(launcherDir, "app", "TateYoko.App.exe");

if (!File.Exists(appExe))
{
    Native.ShowError(
        $"Application not found:\n{appExe}\n\n" +
        "Re-extract the bundle and keep TateYoko.exe next to the app folder.");
    return 1;
}

var psi = new ProcessStartInfo
{
    FileName = appExe,
    UseShellExecute = false,
};

// Skip [0] (this launcher's own path) and forward the rest.
var cmdLine = Environment.GetCommandLineArgs();
for (var i = 1; i < cmdLine.Length; i++)
{
    psi.ArgumentList.Add(cmdLine[i]);
}

try
{
    Process.Start(psi);
}
catch (Exception ex)
{
    Native.ShowError($"Failed to start:\n{appExe}\n\n{ex.Message}");
    return 1;
}

return 0;

static partial class Native
{
    private const uint MB_OK = 0x0;
    private const uint MB_ICONERROR = 0x10;

    [LibraryImport("user32.dll", EntryPoint = "MessageBoxW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    public static void ShowError(string message) =>
        MessageBox(IntPtr.Zero, message, "縦横 (TateYoko)", MB_OK | MB_ICONERROR);
}
