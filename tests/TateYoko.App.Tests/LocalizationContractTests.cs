using System.Xml.Linq;

namespace TateYoko.App.Tests;

public sealed class LocalizationContractTests
{
    private static readonly string[] Languages = ["en-US", "ja-JP", "zh-CN"];
    private static readonly string SourceRoot = Path.Combine(
        AppContext.BaseDirectory,
        "TestInputs",
        "AppSource"
    );

    [Fact]
    public void EveryCultureHasTheSameNonEmptyResourcesAndFormatPlaceholders()
    {
        Dictionary<string, string> baseline = LoadResources(Languages[0]);

        foreach (string language in Languages)
        {
            Dictionary<string, string> actual = LoadResources(language);
            Assert.Equal(
                baseline.Keys.Order(StringComparer.Ordinal),
                actual.Keys.Order(StringComparer.Ordinal)
            );
            Assert.All(actual, pair => Assert.False(string.IsNullOrWhiteSpace(pair.Value)));
            foreach ((string key, string value) in actual)
            {
                Assert.Equal(ExtractPlaceholders(baseline[key]), ExtractPlaceholders(value));
            }
        }
    }

    [Fact]
    public void EveryXamlUidAndCodeLookupIsWiredToAllCultures()
    {
        Dictionary<string, string> resources = LoadResources(Languages[0]);
        XDocument page = XDocument.Load(
            Path.Combine(SourceRoot, "MainPage.xaml"),
            LoadOptions.None
        );
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        string[] uids =
        [
            .. page.Descendants()
                .Select(element => (string?)element.Attribute(xaml + "Uid"))
                .OfType<string>()
                .Distinct(StringComparer.Ordinal),
        ];
        Assert.All(
            uids,
            uid =>
                Assert.Contains(
                    resources.Keys,
                    key => key.StartsWith(uid + ".", StringComparison.Ordinal)
                )
        );

        string[] sourceFiles =
        [
            .. Directory.EnumerateFiles(SourceRoot, "*.cs", SearchOption.AllDirectories),
        ];
        foreach (string sourceFile in sourceFiles)
        {
            string source = File.ReadAllText(sourceFile);
            foreach (string key in ExtractLocalizedKeys(source))
            {
                Assert.Contains(key, resources.Keys);
            }
        }
    }

    [Fact]
    public void InteractiveAutomationIdsAreUniqueAndComplete()
    {
        XDocument page = XDocument.Load(
            Path.Combine(SourceRoot, "MainPage.xaml"),
            LoadOptions.None
        );
        string[] interactiveNames =
        [
            "Button",
            "PasswordBox",
            "ProgressBar",
            "RadioButton",
            "RadioButtons",
        ];
        XElement[] interactive =
        [
            .. page.Descendants()
                .Where(element =>
                    interactiveNames.Contains(element.Name.LocalName, StringComparer.Ordinal)
                ),
        ];
        string[] ids =
        [
            .. interactive.Select(element =>
                element
                    .Attributes()
                    .SingleOrDefault(attribute =>
                        attribute.Name.LocalName == "AutomationProperties.AutomationId"
                    )
                    ?.Value
                ?? throw new InvalidDataException($"{element.Name.LocalName} has no AutomationId.")
            ),
        ];

        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void ExplicitBrushesRemainThemeAware()
    {
        XDocument page = XDocument.Load(
            Path.Combine(SourceRoot, "MainPage.xaml"),
            LoadOptions.None
        );
        string[] brushNames = ["Background", "BorderBrush", "Foreground"];
        XAttribute[] explicitBrushes =
        [
            .. page.Descendants()
                .Attributes()
                .Where(attribute =>
                    brushNames.Contains(attribute.Name.LocalName, StringComparer.Ordinal)
                ),
        ];

        Assert.NotEmpty(explicitBrushes);
        Assert.All(
            explicitBrushes,
            attribute =>
                Assert.True(
                    string.Equals(attribute.Value, "Transparent", StringComparison.Ordinal)
                        || attribute.Value.StartsWith("{ThemeResource ", StringComparison.Ordinal),
                    $"{attribute.Parent?.Name.LocalName}.{attribute.Name.LocalName} "
                        + $"must use a ThemeResource, got '{attribute.Value}'."
                )
        );
    }

    [Fact]
    public void DynamicXBindAlwaysDeclaresItsUpdateMode()
    {
        XDocument page = XDocument.Load(
            Path.Combine(SourceRoot, "MainPage.xaml"),
            LoadOptions.None
        );
        XAttribute[] dynamicBindings =
        [
            .. page.Descendants()
                .Attributes()
                .Where(attribute =>
                    attribute.Value.StartsWith("{x:Bind ", StringComparison.Ordinal)
                    && !string.Equals(attribute.Name.LocalName, "Command", StringComparison.Ordinal)
                ),
        ];

        Assert.NotEmpty(dynamicBindings);
        Assert.All(
            dynamicBindings,
            attribute => Assert.Contains("Mode=", attribute.Value, StringComparison.Ordinal)
        );
    }

    [Fact]
    public void LayoutUsesFluentTokensInsteadOfRawVisualConstants()
    {
        XDocument page = XDocument.Load(
            Path.Combine(SourceRoot, "MainPage.xaml"),
            LoadOptions.None
        );
        Assert.DoesNotContain(
            page.Descendants().Attributes(),
            attribute =>
                string.Equals(attribute.Name.LocalName, "FontSize", StringComparison.Ordinal)
        );
        Assert.All(
            page.Descendants().Attributes("CornerRadius"),
            attribute =>
                Assert.StartsWith("{ThemeResource ", attribute.Value, StringComparison.Ordinal)
        );

        Assert.DoesNotContain(
            page.Descendants()
                .Where(element => element.Name.LocalName == "Button")
                .Descendants()
                .Where(element => element.Name.LocalName == "KeyboardAccelerator"),
            _ => true
        );
    }

    private static Dictionary<string, string> LoadResources(string language)
    {
        XDocument document = XDocument.Load(
            Path.Combine(SourceRoot, "Strings", language, "Resources.resw"),
            LoadOptions.None
        );
        return document
            .Descendants("data")
            .ToDictionary(
                element =>
                    (string?)element.Attribute("name")
                    ?? throw new InvalidDataException("Resource has no name."),
                element =>
                    element.Element("value")?.Value
                    ?? throw new InvalidDataException("Resource has no value."),
                StringComparer.Ordinal
            );
    }

    private static string[] ExtractPlaceholders(string value)
    {
        var placeholders = new SortedSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < value.Length; index++)
        {
            if (value[index] != '{')
            {
                continue;
            }

            int end = value.IndexOf('}', index + 1);
            if (end > index)
            {
                string candidate = value[index..(end + 1)];
                if (candidate.Length >= 3 && char.IsDigit(candidate[1]))
                {
                    placeholders.Add(candidate);
                }

                index = end;
            }
        }

        return [.. placeholders];
    }

    private static List<string> ExtractLocalizedKeys(string source)
    {
        const string marker = "Localized.Get(\"";
        var keys = new List<string>();
        int start = 0;
        while ((start = source.IndexOf(marker, start, StringComparison.Ordinal)) >= 0)
        {
            start += marker.Length;
            int end = source.IndexOf('"', start);
            if (end < 0)
            {
                break;
            }

            keys.Add(source[start..end]);
            start = end + 1;
        }

        return keys;
    }
}
