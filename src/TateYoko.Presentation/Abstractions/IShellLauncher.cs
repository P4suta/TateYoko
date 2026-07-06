namespace TateYoko.Presentation.Abstractions;

/// <summary>Opens files and folders in the OS shell. Abstracts <c>Process.Start</c> so commands can be tested without launching anything.</summary>
public interface IShellLauncher
{
    /// <summary>Opens <paramref name="path"/> with its default application.</summary>
    void Open(string path);

    /// <summary>Reveals <paramref name="path"/> in the file manager, selected.</summary>
    void ShowInFolder(string path);
}
