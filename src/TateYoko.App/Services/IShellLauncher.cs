namespace TateYoko.App.Services;

internal interface IShellLauncher
{
    void Open(string path);

    void ShowInFolder(string path);
}
