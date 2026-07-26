using System.Diagnostics;

namespace TateYoko.App.Services;

internal sealed class ShellLauncher : IShellLauncher
{
    public void Open(string path)
    {
        string validated = ValidateExistingFile(path);
        Start(new ProcessStartInfo(validated) { UseShellExecute = true });
    }

    public void ShowInFolder(string path)
    {
        string validated = ValidateExistingFile(path);
        var startInfo = new ProcessStartInfo(
            Path.Combine(Environment.SystemDirectory, "explorer.exe")
        )
        {
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("/select," + validated);
        Start(startInfo);
    }

    private static string ValidateExistingFile(string path)
    {
        if (!Path.IsPathFullyQualified(path) || !File.Exists(path))
        {
            throw new InvalidOperationException("The requested output is unavailable.");
        }

        return Path.GetFullPath(path);
    }

    private static void Start(ProcessStartInfo startInfo)
    {
        using Process process =
            Process.Start(startInfo)
            ?? throw new InvalidOperationException("Windows did not start the requested action.");
    }
}
