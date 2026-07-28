using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;

namespace TateYoko.App.Services;

internal static class Localized
{
    private static readonly ResourceLoader Loader = new();

    public static string Get(string key) => Loader.GetString(key);

    public static string Get(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, Loader.GetString(key), args);
}
