using System.Diagnostics;
using TateYoko.Presentation.Abstractions;

namespace TateYoko.App.Services;

/// <summary><see cref="IShellLauncher"/> backed by <see cref="Process"/> with shell execution.</summary>
public sealed class ShellLauncher : IShellLauncher
{
    public void Open(string path) =>
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });

    public void ShowInFolder(string path) =>
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
}
