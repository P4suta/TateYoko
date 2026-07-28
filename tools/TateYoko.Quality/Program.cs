using System.Globalization;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

return QualityApplication.Run(args);

internal static class QualityApplication
{
    internal static int Run(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                throw new ArgumentException(
                    "Usage: TateYoko.Quality coverage [directory] | audit <report.json>"
                );
            }

            return args[0] switch
            {
                "coverage" => CoverageGate.Run(args[1..]),
                "audit" => NuGetAuditGate.Run(args[1..]),
                _ => throw new ArgumentException($"Unknown quality gate: {args[0]}"),
            };
        }
        catch (ArgumentException exception)
        {
            return ReportExpectedFailure(exception);
        }
        catch (InvalidOperationException exception)
        {
            return ReportExpectedFailure(exception);
        }
        catch (IOException exception)
        {
            return ReportExpectedFailure(exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            return ReportExpectedFailure(exception);
        }
        catch (JsonException exception)
        {
            return ReportExpectedFailure(exception);
        }
        catch (XmlException exception)
        {
            return ReportExpectedFailure(exception);
        }
    }

    private static int ReportExpectedFailure(Exception exception)
    {
        Console.Error.WriteLine(exception.Message);
        return 1;
    }
}

internal static class CoverageGate
{
    private static readonly CoverageBudget[] Budgets =
    [
        new("engine", "TateYoko.Engine", 0.95m, 0.90m),
        new("app", "TateYoko", 0.95m, 0.90m),
    ];

    internal static int Run(string[] args)
    {
        if (args.Length > 1)
        {
            throw new ArgumentException("Usage: TateYoko.Quality coverage [directory]");
        }

        string directory =
            args.Length == 0
                ? Path.Combine(Repository.FindRoot(), "build", "coverage")
                : Path.GetFullPath(args[0]);
        if (!Directory.Exists(directory))
        {
            throw new InvalidOperationException($"Coverage directory does not exist: {directory}");
        }

        foreach (CoverageBudget budget in Budgets)
        {
            string reportDirectory = Path.Combine(directory, budget.ReportName);
            string[] reports =
            [
                .. Directory.EnumerateFiles(reportDirectory, "*.xml", SearchOption.AllDirectories),
            ];
            if (reports.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Expected one {budget.ReportName} coverage report, found {reports.Length}."
                );
            }

            Verify(reports[0], budget);
        }

        return 0;
    }

    private static void Verify(string path, CoverageBudget budget)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length is <= 0 or > 256 * 1024 * 1024)
        {
            throw new InvalidOperationException($"Coverage report has an invalid size: {path}");
        }

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            MaxCharactersInDocument = 256L * 1024 * 1024,
            XmlResolver = null,
        };
        using XmlReader reader = XmlReader.Create(path, settings);
        XElement root =
            XDocument.Load(reader, LoadOptions.None).Root
            ?? throw new InvalidOperationException($"Coverage report is empty: {path}");
        if (root.Name.LocalName != "coverage")
        {
            throw new InvalidOperationException(
                $"Coverage report has an unexpected root: {root.Name}"
            );
        }

        string[] packages =
        [
            .. root.Descendants()
                .Where(element => element.Name.LocalName == "package")
                .Select(element => (string?)element.Attribute("name") ?? string.Empty)
                .Distinct(StringComparer.Ordinal),
        ];
        if (!packages.SequenceEqual([budget.PackageName], StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"{budget.ReportName} coverage contains unexpected packages: "
                    + string.Join(", ", packages)
            );
        }

        decimal lineRate = CalculateRate(root, "lines-covered", "lines-valid", "line-rate");
        decimal branchRate = CalculateRate(
            root,
            "branches-covered",
            "branches-valid",
            "branch-rate"
        );
        Console.WriteLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"{budget.ReportName}: lines {lineRate:P2}, branches {branchRate:P2}"
            )
        );
        if (lineRate < budget.MinimumLineRate || branchRate < budget.MinimumBranchRate)
        {
            throw new InvalidOperationException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{budget.ReportName} coverage is below budget "
                        + $"(lines {lineRate:P2}/{budget.MinimumLineRate:P2}, "
                        + $"branches {branchRate:P2}/{budget.MinimumBranchRate:P2})."
                )
            );
        }
    }

    private static decimal CalculateRate(
        XElement root,
        string coveredName,
        string validName,
        string declaredRateName
    )
    {
        int covered = ParseCount(root, coveredName);
        int valid = ParseCount(root, validName);
        if (valid <= 0 || covered > valid)
        {
            throw new InvalidOperationException(
                $"Coverage report has invalid {coveredName}/{validName} counts."
            );
        }

        decimal calculated = (decimal)covered / valid;
        string declaredText =
            (string?)root.Attribute(declaredRateName)
            ?? throw new InvalidOperationException($"Coverage report has no {declaredRateName}.");
        if (
            !decimal.TryParse(
                declaredText,
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out decimal declared
            )
            || declared is < 0 or > 1
            || Math.Abs(declared - calculated) > 0.0001m
        )
        {
            throw new InvalidOperationException(
                $"Coverage report has an inconsistent {declaredRateName}."
            );
        }

        return calculated;
    }

    private static int ParseCount(XElement root, string name)
    {
        string value =
            (string?)root.Attribute(name)
            ?? throw new InvalidOperationException($"Coverage report has no {name}.");
        if (
            !int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int result)
            || result < 0
        )
        {
            throw new InvalidOperationException($"Coverage report has an invalid {name}.");
        }

        return result;
    }

    private sealed record CoverageBudget(
        string ReportName,
        string PackageName,
        decimal MinimumLineRate,
        decimal MinimumBranchRate
    );
}

internal static class NuGetAuditGate
{
    private const long MaximumReportSize = 16L * 1024 * 1024;

    private static readonly string[] ExpectedProjects =
    [
        "src/TateYoko.App/TateYoko.App.csproj",
        "src/TateYoko.Engine/TateYoko.Engine.csproj",
        "tests/TateYoko.App.Tests/TateYoko.App.Tests.csproj",
        "tests/TateYoko.Engine.Tests/TateYoko.Engine.Tests.csproj",
        "tools/TateYoko.Icons/TateYoko.Icons.csproj",
        "tools/TateYoko.Notices/TateYoko.Notices.csproj",
        "tools/TateYoko.Pack/TateYoko.Pack.csproj",
        "tools/TateYoko.Quality/TateYoko.Quality.csproj",
    ];

    internal static int Run(string[] args)
    {
        if (args.Length != 1)
        {
            throw new ArgumentException("Usage: TateYoko.Quality audit <report.json>");
        }

        string path = Path.GetFullPath(args[0]);
        var info = new FileInfo(path);
        if (!info.Exists || info.Length is <= 0 or > MaximumReportSize)
        {
            throw new InvalidOperationException($"NuGet audit report has an invalid size: {path}");
        }

        using FileStream stream = File.OpenRead(path);
        using JsonDocument document = JsonDocument.Parse(
            stream,
            new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 128,
            }
        );
        JsonElement root = document.RootElement;
        RequireKind(root, JsonValueKind.Object, "root");
        if (
            !root.TryGetProperty("version", out JsonElement version)
            || version.ValueKind != JsonValueKind.Number
            || !version.TryGetInt32(out int versionNumber)
            || versionNumber != 1
        )
        {
            throw new InvalidOperationException(
                "NuGet audit report must use JSON output version 1."
            );
        }

        string parameters = RequireString(root, "parameters");
        string[] switches = parameters.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );
        if (
            !switches.Contains("--vulnerable", StringComparer.Ordinal)
            || !switches.Contains("--include-transitive", StringComparer.Ordinal)
        )
        {
            throw new InvalidOperationException(
                "NuGet audit report did not scan direct and transitive vulnerabilities."
            );
        }

        JsonElement sources = RequireProperty(root, "sources", JsonValueKind.Array);
        if (
            sources.GetArrayLength() == 0
            || sources
                .EnumerateArray()
                .Any(source =>
                    source.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(source.GetString())
                )
        )
        {
            throw new InvalidOperationException("NuGet audit report has no valid package sources.");
        }

        string repositoryRoot = Repository.FindRoot();
        JsonElement projects = RequireProperty(root, "projects", JsonValueKind.Array);
        var actualProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var vulnerabilities = new List<string>();
        foreach (JsonElement project in projects.EnumerateArray())
        {
            RequireKind(project, JsonValueKind.Object, "project");
            string projectPath = Path.GetFullPath(RequireString(project, "path"));
            string relativePath = Path.GetRelativePath(repositoryRoot, projectPath)
                .Replace('\\', '/');
            if (
                relativePath.StartsWith("../", StringComparison.Ordinal)
                || !actualProjects.Add(relativePath)
            )
            {
                throw new InvalidOperationException(
                    $"NuGet audit report contains an invalid project path: {projectPath}"
                );
            }

            FindVulnerabilities(project, relativePath, vulnerabilities);
        }

        string[] missing =
        [
            .. ExpectedProjects.Except(actualProjects, StringComparer.OrdinalIgnoreCase),
        ];
        string[] unexpected =
        [
            .. actualProjects.Except(ExpectedProjects, StringComparer.OrdinalIgnoreCase),
        ];
        if (missing.Length != 0 || unexpected.Length != 0)
        {
            throw new InvalidOperationException(
                "NuGet audit project set is incomplete or unexpected. "
                    + $"Missing: {string.Join(", ", missing)}. "
                    + $"Unexpected: {string.Join(", ", unexpected)}."
            );
        }

        if (vulnerabilities.Count != 0)
        {
            throw new InvalidOperationException(
                "NuGet audit found known vulnerabilities: " + string.Join("; ", vulnerabilities)
            );
        }

        Console.WriteLine(
            $"NuGet audit: {actualProjects.Count} projects, 0 known vulnerabilities."
        );
        return 0;
    }

    private static void FindVulnerabilities(
        JsonElement element,
        string project,
        List<string> vulnerabilities
    )
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("vulnerabilities", out JsonElement reportedVulnerabilities))
            {
                RequireKind(reportedVulnerabilities, JsonValueKind.Array, "vulnerabilities");
                string package =
                    element.TryGetProperty("id", out JsonElement id)
                    && id.ValueKind == JsonValueKind.String
                        ? id.GetString() ?? "<unknown>"
                        : "<unknown>";
                string version =
                    element.TryGetProperty("resolvedVersion", out JsonElement resolvedVersion)
                    && resolvedVersion.ValueKind == JsonValueKind.String
                        ? resolvedVersion.GetString() ?? "<unknown>"
                        : "<unknown>";
                foreach (JsonElement vulnerability in reportedVulnerabilities.EnumerateArray())
                {
                    string severity =
                        vulnerability.ValueKind == JsonValueKind.Object
                        && vulnerability.TryGetProperty(
                            "severity",
                            out JsonElement reportedSeverity
                        )
                        && reportedSeverity.ValueKind == JsonValueKind.String
                            ? reportedSeverity.GetString() ?? "unknown"
                            : "unknown";
                    vulnerabilities.Add($"{project}: {package} {version} ({severity})");
                }
            }

            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (property.NameEquals("vulnerabilities"))
                {
                    continue;
                }

                FindVulnerabilities(property.Value, project, vulnerabilities);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                FindVulnerabilities(item, project, vulnerabilities);
            }
        }
    }

    private static JsonElement RequireProperty(JsonElement element, string name, JsonValueKind kind)
    {
        if (!element.TryGetProperty(name, out JsonElement property))
        {
            throw new InvalidOperationException($"NuGet audit report has no {name}.");
        }

        RequireKind(property, kind, name);
        return property;
    }

    private static string RequireString(JsonElement element, string name)
    {
        JsonElement property = RequireProperty(element, name, JsonValueKind.String);
        string? value = property.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"NuGet audit report has an invalid {name}.");
        }

        return value;
    }

    private static void RequireKind(JsonElement element, JsonValueKind kind, string name)
    {
        if (element.ValueKind != kind)
        {
            throw new InvalidOperationException($"NuGet audit report has an invalid {name}.");
        }
    }
}

internal static class Repository
{
    internal static string FindRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TateYoko.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate TateYoko.slnx.");
    }
}
