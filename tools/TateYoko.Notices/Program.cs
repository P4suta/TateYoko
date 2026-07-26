using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

return NoticeApplication.Run(args);

internal static class NoticeApplication
{
    private static readonly string[] RuntimeAssetGroups =
    [
        "runtime",
        "runtimeTargets",
        "native",
        "resource",
        "resources",
    ];
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    internal static int Run(string[] args)
    {
        try
        {
            if (args.Length > 2)
            {
                throw new ArgumentException(
                    "Usage: TateYoko.Notices [project.assets.json] [output.txt]"
                );
            }

            string repositoryRoot = FindRepositoryRoot();
            string assetsPath =
                args.Length >= 1
                    ? Path.GetFullPath(args[0])
                    : Path.Combine(
                        repositoryRoot,
                        "src",
                        "TateYoko.App",
                        "obj",
                        "project.assets.json"
                    );
            string outputPath =
                args.Length == 2
                    ? Path.GetFullPath(args[1])
                    : Path.Combine(repositoryRoot, "build", "legal", "THIRD-PARTY-NOTICES.txt");

            List<PackageNotice> notices = LoadNotices(assetsPath);
            Directory.CreateDirectory(
                Path.GetDirectoryName(outputPath)
                    ?? throw new InvalidOperationException("Output path has no parent directory.")
            );
            File.WriteAllText(outputPath, Render(notices), Utf8NoBom);
            Console.WriteLine($"Wrote {notices.Count} third-party package notices to {outputPath}");
            return 0;
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
        catch (KeyNotFoundException exception)
        {
            return ReportExpectedFailure(exception);
        }
        catch (FormatException exception)
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

    private static List<PackageNotice> LoadNotices(string assetsPath)
    {
        if (!File.Exists(assetsPath))
        {
            throw new FileNotFoundException(
                "Restore the application before generating notices.",
                assetsPath
            );
        }

        using JsonDocument document = JsonDocument.Parse(
            File.ReadAllBytes(assetsPath),
            new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 128,
            }
        );
        JsonElement root = document.RootElement;
        EnsureSuccessfulRestore(root);
        string[] packageFolders = GetPackageFolders(root);
        HashSet<string> selected = SelectPackages(root);
        JsonElement libraries = root.GetProperty("libraries");
        var notices = new List<PackageNotice>(selected.Count);
        foreach (string libraryName in selected.Order(StringComparer.OrdinalIgnoreCase))
        {
            if (
                !libraries.TryGetProperty(libraryName, out JsonElement library)
                || !string.Equals(
                    library.GetProperty("type").GetString(),
                    "package",
                    StringComparison.Ordinal
                )
            )
            {
                continue;
            }

            string relativePath =
                library.GetProperty("path").GetString()
                ?? throw new InvalidOperationException(
                    $"Package {libraryName} has no package path."
                );
            string packagePath = ResolvePackagePath(libraryName, relativePath, packageFolders);
            notices.Add(LoadPackageNotice(libraryName, packagePath));
        }

        if (notices.Count == 0)
        {
            throw new InvalidOperationException(
                "No shipped runtime NuGet packages were found in project.assets.json."
            );
        }

        return notices;
    }

    private static void EnsureSuccessfulRestore(JsonElement root)
    {
        if (!root.TryGetProperty("logs", out JsonElement logs))
        {
            return;
        }

        if (logs.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException(
                "project.assets.json has an invalid restore diagnostics section."
            );
        }

        string[] diagnostics =
        [
            .. logs.EnumerateArray()
                .Select(log =>
                {
                    string level = log.TryGetProperty("level", out JsonElement levelElement)
                        ? levelElement.GetString() ?? "Unknown"
                        : "Unknown";
                    string code = log.TryGetProperty("code", out JsonElement codeElement)
                        ? codeElement.GetString() ?? "unknown"
                        : "unknown";
                    return $"{level} {code}";
                }),
        ];
        if (diagnostics.Length != 0)
        {
            throw new InvalidOperationException(
                "Refusing an asset graph with restore diagnostics: "
                    + string.Join(", ", diagnostics)
            );
        }
    }

    private static string[] GetPackageFolders(JsonElement root)
    {
        JsonElement packageFolders = root.GetProperty("packageFolders");
        if (packageFolders.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException(
                "project.assets.json has an invalid packageFolders section."
            );
        }

        string[] folders =
        [
            .. packageFolders.EnumerateObject().Select(property => Path.GetFullPath(property.Name)),
        ];
        if (folders.Length == 0)
        {
            throw new InvalidOperationException("No NuGet package folders were declared.");
        }

        if (folders.Distinct(StringComparer.OrdinalIgnoreCase).Count() != folders.Length)
        {
            throw new InvalidOperationException(
                "project.assets.json declares duplicate NuGet package folders."
            );
        }

        return folders;
    }

    private static string ResolvePackagePath(
        string libraryName,
        string relativePath,
        IReadOnlyList<string> packageFolders
    )
    {
        var candidates = new List<string>();
        foreach (string packageFolder in packageFolders)
        {
            string packagePath = Path.GetFullPath(
                Path.Combine(packageFolder, relativePath.Replace('/', Path.DirectorySeparatorChar))
            );
            EnsureDescendant(packageFolder, packagePath);
            if (!Directory.Exists(packagePath))
            {
                continue;
            }

            if ((File.GetAttributes(packageFolder) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException(
                    $"NuGet package folder is a reparse point: {packageFolder}"
                );
            }

            EnsureNoReparsePoints(packageFolder, packagePath);
            candidates.Add(packagePath);
        }

        return candidates.Count switch
        {
            1 => candidates[0],
            0 => throw new DirectoryNotFoundException(
                $"Restored package {libraryName} was not found beneath any declared "
                    + "NuGet package folder."
            ),
            _ => throw new InvalidOperationException(
                $"Restored package {libraryName} exists beneath multiple NuGet package folders."
            ),
        };
    }

    private static HashSet<string> SelectPackages(JsonElement root)
    {
        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        JsonElement libraries = root.GetProperty("libraries");
        foreach (JsonProperty target in root.GetProperty("targets").EnumerateObject())
        {
            foreach (JsonProperty library in target.Value.EnumerateObject())
            {
                if (
                    libraries.TryGetProperty(library.Name, out JsonElement details)
                    && string.Equals(
                        details.GetProperty("type").GetString(),
                        "package",
                        StringComparison.Ordinal
                    )
                    && HasRuntimeAsset(library.Value)
                )
                {
                    selected.Add(library.Name);
                }
            }
        }

        return selected;
    }

    private static bool HasRuntimeAsset(JsonElement targetLibrary)
    {
        foreach (string groupName in RuntimeAssetGroups)
        {
            if (
                !targetLibrary.TryGetProperty(groupName, out JsonElement group)
                || group.ValueKind != JsonValueKind.Object
            )
            {
                continue;
            }

            if (
                group
                    .EnumerateObject()
                    .Any(asset => !asset.Name.EndsWith("/_._", StringComparison.Ordinal))
            )
            {
                return true;
            }
        }

        if (
            targetLibrary.TryGetProperty("contentFiles", out JsonElement contentFiles)
            && contentFiles.ValueKind == JsonValueKind.Object
            && contentFiles
                .EnumerateObject()
                .Any(asset =>
                    asset.Value.ValueKind == JsonValueKind.Object
                    && asset.Value.TryGetProperty("copyToOutput", out JsonElement copyToOutput)
                    && copyToOutput.ValueKind == JsonValueKind.True
                )
        )
        {
            return true;
        }

        return false;
    }

    private static PackageNotice LoadPackageNotice(string libraryName, string packagePath)
    {
        if (!Directory.Exists(packagePath))
        {
            throw new DirectoryNotFoundException($"Restored package is missing: {packagePath}");
        }

        EnsureNoReparsePoints(
            Path.GetDirectoryName(
                Path.GetDirectoryName(packagePath)
                    ?? throw new InvalidOperationException("Package path has no version parent.")
            ) ?? throw new InvalidOperationException("Package path has no ID parent."),
            packagePath
        );
        string nuspecPath = Directory
            .EnumerateFiles(packagePath, "*.nuspec", SearchOption.TopDirectoryOnly)
            .Single();
        EnsureNoReparsePoints(packagePath, nuspecPath);
        var nuspecInfo = new FileInfo(nuspecPath);
        if (nuspecInfo.Length is <= 0 or > 1024 * 1024)
        {
            throw new InvalidOperationException(
                $"NuGet metadata has an invalid size: {nuspecPath}"
            );
        }

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            MaxCharactersInDocument = 1024 * 1024,
            XmlResolver = null,
        };
        using XmlReader reader = XmlReader.Create(nuspecPath, settings);
        XDocument nuspec = XDocument.Load(reader, LoadOptions.None);
        XElement metadata = nuspec
            .Descendants()
            .Single(element => element.Name.LocalName == "metadata");
        string id = ElementValue(metadata, "id");
        string version = ElementValue(metadata, "version");
        if (!string.Equals(libraryName, $"{id}/{version}", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Package metadata does not match restored library {libraryName}."
            );
        }

        XElement? license = metadata
            .Elements()
            .SingleOrDefault(element => element.Name.LocalName == "license");
        string licenseExpression = license is null
            ? "not declared"
            : ((string?)license.Attribute("type"), license.Value.Trim()) switch
            {
                ("expression", string value) when value.Length > 0 => value,
                ("file", string value) when value.Length > 0 => $"file: {value}",
                _ => "not declared",
            };
        string copyright = OptionalElementValue(metadata, "copyright");
        string projectUrl = OptionalElementValue(metadata, "projectUrl");
        List<LegalDocument> legalDocuments = FindLegalDocuments(packagePath, license);
        if (legalDocuments.Count == 0)
        {
            if (
                string.Equals(licenseExpression, "MIT", StringComparison.Ordinal)
                && copyright.Length > 0
            )
            {
                legalDocuments =
                [
                    new LegalDocument("SPDX MIT license text", RenderMitLicense(copyright)),
                ];
            }
            else
            {
                throw new InvalidOperationException(
                    $"{libraryName} provides no distributable license or notice text."
                );
            }
        }

        return new PackageNotice(
            id,
            version,
            licenseExpression,
            copyright,
            projectUrl,
            legalDocuments
        );
    }

    private static List<LegalDocument> FindLegalDocuments(string packagePath, XElement? license)
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (
            string path in Directory.EnumerateFiles(packagePath, "*", SearchOption.TopDirectoryOnly)
        )
        {
            string name = Path.GetFileName(path);
            if (
                name.Contains("license", StringComparison.OrdinalIgnoreCase)
                || name.Contains("notice", StringComparison.OrdinalIgnoreCase)
                || name.Contains("copying", StringComparison.OrdinalIgnoreCase)
            )
            {
                candidates.Add(Path.GetFullPath(path));
            }
        }

        if (
            license is not null
            && string.Equals((string?)license.Attribute("type"), "file", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(license.Value)
        )
        {
            string declaredPath = Path.GetFullPath(
                Path.Combine(
                    packagePath,
                    license.Value.Trim().Replace('/', Path.DirectorySeparatorChar)
                )
            );
            EnsureDescendant(packagePath, declaredPath);
            candidates.Add(declaredPath);
        }

        var documents = new List<LegalDocument>();
        var seenContents = new HashSet<string>(StringComparer.Ordinal);
        foreach (string path in candidates.Order(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "A NuGet package declares a missing license file.",
                    path
                );
            }

            var info = new FileInfo(path);
            if (info.Length is <= 0 or > 5 * 1024 * 1024)
            {
                throw new InvalidOperationException($"Legal document has an invalid size: {path}");
            }

            EnsureNoReparsePoints(packagePath, path);
            string content = NormalizeText(File.ReadAllText(path));
            if (
                content.Length == 0
                || content.Any(character =>
                    char.IsControl(character) && character is not '\n' and not '\t'
                )
            )
            {
                throw new InvalidOperationException($"Legal document is not plain text: {path}");
            }

            if (seenContents.Add(content))
            {
                documents.Add(
                    new LegalDocument(
                        Path.GetRelativePath(packagePath, path).Replace('\\', '/'),
                        content
                    )
                );
            }
        }

        return documents;
    }

    private static string Render(List<PackageNotice> notices)
    {
        var builder = new StringBuilder(
            """
            TATEYOKO THIRD-PARTY NOTICES
            ===========================

            This file is generated from the exact NuGet dependency graph restored for
            the shipping application. TateYoko itself is licensed under Apache-2.0;
            the following components retain their own licenses and notices.

            """
        );
        foreach (PackageNotice notice in notices)
        {
            builder.AppendLine(
                "------------------------------------------------------------------------"
            );
            builder.Append(notice.Id).Append(' ').AppendLine(notice.Version);
            builder.Append("License: ").AppendLine(notice.LicenseExpression);
            if (notice.Copyright.Length > 0)
            {
                builder.Append("Copyright: ").AppendLine(notice.Copyright);
            }

            if (notice.ProjectUrl.Length > 0)
            {
                builder.Append("Project: ").AppendLine(notice.ProjectUrl);
            }

            foreach (LegalDocument document in notice.Documents)
            {
                builder.AppendLine();
                builder.Append("----- ").Append(document.Name).AppendLine(" -----");
                builder.AppendLine(document.Content);
            }

            builder.AppendLine();
        }

        return NormalizeText(builder.ToString()) + '\n';
    }

    private static string RenderMitLicense(string copyright)
    {
        return NormalizeText(
            $"""
            MIT License

            {copyright}

            Permission is hereby granted, free of charge, to any person obtaining a copy
            of this software and associated documentation files (the "Software"), to deal
            in the Software without restriction, including without limitation the rights
            to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
            copies of the Software, and to permit persons to whom the Software is
            furnished to do so, subject to the following conditions:

            The above copyright notice and this permission notice shall be included in all
            copies or substantial portions of the Software.

            THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
            IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
            FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
            AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
            LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
            OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
            SOFTWARE.
            """
        );
    }

    private static string ElementValue(XElement metadata, string name) =>
        metadata.Elements().Single(element => element.Name.LocalName == name).Value.Trim();

    private static string OptionalElementValue(XElement metadata, string name) =>
        metadata.Elements().SingleOrDefault(element => element.Name.LocalName == name)?.Value.Trim()
        ?? string.Empty;

    private static string NormalizeText(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Trim();

    private static void EnsureDescendant(string parent, string candidate)
    {
        string parentPrefix =
            Path.GetFullPath(parent)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(parentPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Path escapes its package root: {candidate}");
        }
    }

    private static void EnsureNoReparsePoints(string parent, string candidate)
    {
        string parentPath = Path.GetFullPath(parent)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string currentPath = Path.GetFullPath(candidate);
        EnsureDescendant(parentPath, currentPath);
        while (!string.Equals(currentPath, parentPath, StringComparison.OrdinalIgnoreCase))
        {
            if ((File.GetAttributes(currentPath) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException(
                    $"Package content contains a reparse point: {currentPath}"
                );
            }

            currentPath =
                Path.GetDirectoryName(currentPath)
                ?? throw new InvalidOperationException($"Could not walk package path: {candidate}");
        }
    }

    private static string FindRepositoryRoot()
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

    private sealed record PackageNotice(
        string Id,
        string Version,
        string LicenseExpression,
        string Copyright,
        string ProjectUrl,
        List<LegalDocument> Documents
    );

    private sealed record LegalDocument(string Name, string Content);
}
