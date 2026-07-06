using Microsoft.Windows.ApplicationModel.Resources;

namespace TateYoko.App.Services;

/// <summary>Resolves localized UI strings from Resources.resw, scoped to the "Resources" subtree.</summary>
public static class Localized
{
    private static readonly ResourceLoader Loader = new(
        Path.Combine(AppContext.BaseDirectory, "TateYoko.App.pri"),
        "Resources");

    public static string Get(string key) => Loader.GetString(key);

    public static string Get(string key, params object[] args) =>
        string.Format(Loader.GetString(key), args);
}
