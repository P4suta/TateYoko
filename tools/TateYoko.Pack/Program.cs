using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

return PackApplication.Run(args);

internal static partial class PackApplication
{
    private const string BuildToolsVersion = "10.0.28000.2526";
    private const string ExpectedPublisher = "CN=Yasunobu Sakashita";
    private const string PackageIdentity = "P4suta.TateYoko";
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    internal static int Run(string[] args)
    {
        try
        {
            (string command, PackOptions options) = PackOptions.Parse(args);
            new Packager(FindRepositoryRoot(), options).Execute(command);
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
        catch (TimeoutException exception)
        {
            return ReportExpectedFailure(exception);
        }
        catch (Win32Exception exception)
        {
            return ReportExpectedFailure(exception);
        }
        catch (CryptographicException exception)
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

    [GeneratedRegex(@"^[0-9]+\.[0-9]+\.[0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex ReleaseVersionPattern();

    private sealed class Packager
    {
        private static readonly string[] Architectures = ["x64", "arm64"];
        private static readonly string[] SupportedResourceLanguages = ["en-US", "ja-JP", "zh-CN"];
        private readonly string _appProject;
        private readonly string _packageDirectory;
        private readonly string _publishDirectory;
        private readonly string _repositoryRoot;
        private readonly string _signingInputDirectory;
        private readonly string _signingOutputDirectory;
        private readonly string _signedArtifactsDirectory;
        private readonly string _unsignedDirectory;
        private readonly PackOptions _options;

        internal Packager(string repositoryRoot, PackOptions options)
        {
            _repositoryRoot = repositoryRoot;
            _options = options;
            _appProject = Path.Combine(
                repositoryRoot,
                "src",
                "TateYoko.App",
                "TateYoko.App.csproj"
            );
            _publishDirectory = Path.Combine(repositoryRoot, "publish");
            _unsignedDirectory = Path.Combine(_publishDirectory, "unsigned");
            _signingInputDirectory = Path.Combine(_publishDirectory, "sign-stage");
            _signingOutputDirectory = Path.Combine(_publishDirectory, "signed");
            _signedArtifactsDirectory = Path.Combine(_publishDirectory, "signed-artifacts");
            _packageDirectory = Path.Combine(_publishDirectory, "package");
        }

        private string AppInstallerPath =>
            Path.Combine(_unsignedDirectory, "TateYoko.appinstaller");

        private string BundlePath => Path.Combine(_unsignedDirectory, "TateYoko.msixbundle");

        private string AppxVersion => $"{_options.Version}.0";

        private static readonly string[] SignableNames = ["TateYoko.msixbundle"];

        internal void Execute(string command)
        {
            switch (command)
            {
                case "build":
                    Build();
                    break;
                case "stage-signing":
                    StageSigning();
                    break;
                case "collect-signing":
                    CollectSigning();
                    break;
                case "verify":
                    VerifySignedArtifacts();
                    break;
                case "package":
                    Package();
                    break;
                case "list-signable":
                    foreach (string name in SignableNames)
                    {
                        Console.WriteLine(name);
                    }

                    break;
                default:
                    throw new ArgumentException(
                        "Command must be build, stage-signing, collect-signing, verify, package, or list-signable."
                    );
            }
        }

        private void Build()
        {
            Step("Preparing unsigned release artifacts");
            SafeDeleteDirectory(_unsignedDirectory);
            SafeDeleteDirectory(_signingInputDirectory);
            SafeDeleteDirectory(_signingOutputDirectory);
            SafeDeleteDirectory(_signedArtifactsDirectory);
            SafeDeleteDirectory(_packageDirectory);
            Directory.CreateDirectory(_unsignedDirectory);

            var msixPackages = Architectures.Select(BuildMsix).ToArray();
            BuildBundle(msixPackages);
            WriteAppInstaller();
            ValidateAppInstaller();
            Step($"Unsigned artifacts ready: {_unsignedDirectory}");
        }

        private string BuildMsix(string architecture)
        {
            string runtimeIdentifier = $"win-{architecture}";
            string platform = architecture == "arm64" ? "ARM64" : "x64";
            string output = Path.Combine(_unsignedDirectory, "msix", architecture);
            Directory.CreateDirectory(output);
            string releaseManifest = WriteReleaseAppxManifest(output);
            RestoreAppForDistribution(runtimeIdentifier, platform);
            Step($"Building unsigned MSIX {architecture}");
            RunProcess(
                "dotnet",
                [
                    "publish",
                    _appProject,
                    "-c",
                    _options.Configuration,
                    "-r",
                    runtimeIdentifier,
                    "--no-restore",
                    $"-p:Platform={platform}",
                    "-p:DistributionMode=Msix",
                    "-p:PublishReadyToRun=true",
                    "-p:GenerateAppxPackageOnBuild=true",
                    "-p:AppxBundle=Never",
                    "-p:AppxPackageSigningEnabled=false",
                    "-p:UapAppxPackageBuildMode=SideloadOnly",
                    $"-p:ReleaseAppxManifest={releaseManifest}",
                    $"-p:Version={_options.Version}",
                    $"-p:FileVersion={AppxVersion}",
                    $"-p:InformationalVersion={_options.Version}",
                    "-p:IncludeSourceRevisionInInformationalVersion=false",
                    "-p:ContinuousIntegrationBuild=true",
                    $"-p:AppxPackageDir={EnsureTrailingSeparator(output)}",
                ]
            );

            string[] packages =
            [
                .. Directory
                    .EnumerateFiles(output, "*.msix", SearchOption.AllDirectories)
                    .Where(path => !PathContainsSegment(path, "Dependencies")),
            ];
            if (packages.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Expected one {architecture} MSIX, found {packages.Length}."
                );
            }

            ValidateMsix(packages[0], architecture);
            return packages[0];
        }

        private void RestoreAppForDistribution(string runtimeIdentifier, string platform)
        {
            Step($"Restoring MSIX dependencies for {runtimeIdentifier}");
            RunProcess(
                "dotnet",
                [
                    "restore",
                    _appProject,
                    "--locked-mode",
                    $"-p:Configuration={_options.Configuration}",
                    $"-p:Platform={platform}",
                    "-p:DistributionMode=Msix",
                    "-p:PublishReadyToRun=true",
                ]
            );
        }

        private string WriteReleaseAppxManifest(string outputDirectory)
        {
            string sourcePath = Path.Combine(
                _repositoryRoot,
                "src",
                "TateYoko.App",
                "Package.appxmanifest"
            );
            using FileStream input = File.OpenRead(sourcePath);
            XDocument document = LoadXml(input);
            XElement identity = document
                .Descendants()
                .Single(element => element.Name.LocalName == "Identity");
            AssertAttribute(identity, "Name", PackageIdentity);
            AssertAttribute(identity, "Publisher", ExpectedPublisher);
            identity.SetAttributeValue("Version", AppxVersion);

            string releasePath = Path.Combine(outputDirectory, "Package.release.appxmanifest");
            using var writer = new StreamWriter(releasePath, append: false, Utf8NoBom);
            document.Save(writer);
            return releasePath;
        }

        private void BuildBundle(IReadOnlyList<string> packages)
        {
            string input = Path.Combine(_unsignedDirectory, "bundle-input");
            SafeDeleteDirectory(input);
            Directory.CreateDirectory(input);
            foreach (string package in packages)
            {
                File.Copy(package, Path.Combine(input, Path.GetFileName(package)), overwrite: true);
            }

            Step("Bundling x64 and ARM64 MSIX packages");
            RunProcess(
                FindBuildTool("makeappx.exe"),
                ["bundle", "/d", input, "/p", BundlePath, "/bv", AppxVersion, "/o"]
            );
            ValidateBundle(BundlePath);
            SafeDeleteDirectory(input);
        }

        private void WriteAppInstaller()
        {
            XNamespace ns = "http://schemas.microsoft.com/appx/appinstaller/2018";
            var document = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement(
                    ns + "AppInstaller",
                    new XAttribute(
                        "Uri",
                        "https://github.com/P4suta/TateYoko/releases/latest/download/TateYoko.appinstaller"
                    ),
                    new XAttribute("Version", AppxVersion),
                    new XElement(
                        ns + "MainBundle",
                        new XAttribute("Name", PackageIdentity),
                        new XAttribute("Publisher", ExpectedPublisher),
                        new XAttribute("Version", AppxVersion),
                        new XAttribute(
                            "Uri",
                            "https://github.com/P4suta/TateYoko/releases/latest/download/TateYoko.msixbundle"
                        )
                    ),
                    new XElement(
                        ns + "UpdateSettings",
                        new XElement(
                            ns + "OnLaunch",
                            new XAttribute("HoursBetweenUpdateChecks", 12),
                            new XAttribute("ShowPrompt", true),
                            new XAttribute("UpdateBlocksActivation", false)
                        ),
                        new XElement(ns + "AutomaticBackgroundTask")
                    )
                )
            );
            using var writer = new StreamWriter(AppInstallerPath, append: false, Utf8NoBom);
            document.Save(writer);
        }

        private void StageSigning()
        {
            RequireDirectory(_unsignedDirectory, "Run the build command first.");
            SafeDeleteDirectory(_signingInputDirectory);
            SafeDeleteDirectory(_signingOutputDirectory);
            Directory.CreateDirectory(_signingInputDirectory);
            Directory.CreateDirectory(_signingOutputDirectory);
            foreach (string name in SignableNames)
            {
                CopyRequired(
                    Path.Combine(_unsignedDirectory, name),
                    Path.Combine(_signingInputDirectory, name)
                );
            }
        }

        private void CollectSigning()
        {
            RequireDirectory(_unsignedDirectory, "Run the build command first.");
            SafeDeleteDirectory(_signedArtifactsDirectory);
            Directory.CreateDirectory(_signedArtifactsDirectory);
            foreach (string name in SignableNames)
            {
                CopyRequired(
                    Path.Combine(_signingOutputDirectory, name),
                    Path.Combine(_signedArtifactsDirectory, name)
                );
            }

            CopyRequired(
                AppInstallerPath,
                Path.Combine(_signedArtifactsDirectory, "TateYoko.appinstaller")
            );
            VerifySignedArtifacts();
        }

        private void VerifySignedArtifacts()
        {
            RequireDirectory(
                _signedArtifactsDirectory,
                "Signed artifacts are absent. Release packaging is fail-closed."
            );
            ValidateExactFileSet(
                _signedArtifactsDirectory,
                SignableNames.Append("TateYoko.appinstaller")
            );
            foreach (string name in SignableNames)
            {
                string path = Path.Combine(_signedArtifactsDirectory, name);
                if (!File.Exists(path))
                {
                    throw new InvalidOperationException($"Signed artifact is missing: {name}");
                }

                RunProcess(
                    FindBuildTool("signtool.exe"),
                    ["verify", "/pa", "/all", "/tw", "/v", path]
                );
                VerifySignerAndTimestamp(path);
            }

            ValidateBundle(Path.Combine(_signedArtifactsDirectory, "TateYoko.msixbundle"));
            ValidateAppInstaller(Path.Combine(_signedArtifactsDirectory, "TateYoko.appinstaller"));
            Step("All release signatures, timestamps, identities, and architectures are valid");
        }

        private void Package()
        {
            VerifySignedArtifacts();
            string sbom = Path.Combine(_repositoryRoot, "build", "sbom", "tateyoko.cdx.json");
            string sourceSbom = Path.Combine(
                _repositoryRoot,
                "build",
                "sbom",
                "tateyoko-sources.spdx"
            );
            string notices = Path.Combine(
                _repositoryRoot,
                "build",
                "legal",
                "THIRD-PARTY-NOTICES.txt"
            );
            string license = Path.Combine(_repositoryRoot, "LICENSE");
            foreach (string required in new[] { sbom, sourceSbom, notices, license })
            {
                if (!File.Exists(required))
                {
                    throw new InvalidOperationException(
                        $"Required release evidence is missing: {required}"
                    );
                }
            }

            ValidateSbom(sbom);
            ValidateSourceSbom(sourceSbom);
            ValidateNotice(notices);
            SafeDeleteDirectory(_packageDirectory);
            Directory.CreateDirectory(_packageDirectory);
            CopyRequired(
                Path.Combine(_signedArtifactsDirectory, "TateYoko.msixbundle"),
                Path.Combine(_packageDirectory, "TateYoko.msixbundle")
            );
            CopyRequired(
                Path.Combine(_signedArtifactsDirectory, "TateYoko.appinstaller"),
                Path.Combine(_packageDirectory, "TateYoko.appinstaller")
            );
            File.Copy(sbom, Path.Combine(_packageDirectory, "tateyoko.cdx.json"));
            File.Copy(sourceSbom, Path.Combine(_packageDirectory, "tateyoko-sources.spdx"));
            File.Copy(notices, Path.Combine(_packageDirectory, Path.GetFileName(notices)));
            File.Copy(license, Path.Combine(_packageDirectory, "LICENSE.txt"));
            WriteChecksums();
            Step($"Signed release package ready: {_packageDirectory}");
        }

        private void WriteChecksums()
        {
            var lines = new List<string>();
            foreach (
                string path in Directory
                    .EnumerateFiles(_packageDirectory)
                    .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
            )
            {
                using FileStream stream = File.OpenRead(path);
                string hash = Convert.ToHexStringLower(SHA256.HashData(stream));
                lines.Add($"{hash}  {Path.GetFileName(path)}");
            }

            File.WriteAllText(
                Path.Combine(_packageDirectory, "SHA256SUMS.txt"),
                string.Join('\n', lines) + '\n',
                Utf8NoBom
            );
        }

        private void ValidateMsix(string path, string architecture)
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length is <= 0 or > 80L * 1024 * 1024)
            {
                throw new InvalidOperationException(
                    $"MSIX has an invalid size or exceeds the 80 MiB release budget: {path}"
                );
            }

            using ZipArchive archive = ZipFile.OpenRead(path);
            ValidateArchiveEntries(archive);
            string[] requiredEntries =
            [
                "TateYoko.exe",
                "TateYoko.dll",
                "TateYoko.Engine.dll",
                "PdfSharp.dll",
                "PdfSharp.Shared.dll",
                "PdfSharp.System.dll",
                "resources.pri",
                "Assets/AppIcon.ico",
                "LICENSE.txt",
                "THIRD-PARTY-NOTICES.txt",
            ];
            string[] missingEntries =
            [
                .. requiredEntries.Where(required =>
                    archive.Entries.Count(entry =>
                        string.Equals(entry.FullName, required, StringComparison.Ordinal)
                    ) != 1
                ),
            ];
            if (missingEntries.Length > 0)
            {
                throw new InvalidOperationException(
                    $"MSIX is missing required runtime files: {string.Join(", ", missingEntries)}"
                );
            }

            string[] forbiddenFiles =
            [
                .. archive
                    .Entries.Select(entry => entry.FullName)
                    .Where(name =>
                        name.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase)
                        || name.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase)
                        || name.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)
                        || name.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)
                        || name.EndsWith(
                            "PdfSharp.BarCodes.dll",
                            StringComparison.OrdinalIgnoreCase
                        )
                        || name.EndsWith(
                            "PdfSharp.Charting.dll",
                            StringComparison.OrdinalIgnoreCase
                        )
                        || name.EndsWith(
                            "PdfSharp.Cryptography.dll",
                            StringComparison.OrdinalIgnoreCase
                        )
                        || name.EndsWith("PdfSharp.Quality.dll", StringComparison.OrdinalIgnoreCase)
                        || name.EndsWith(
                            "PdfSharp.Snippets.dll",
                            StringComparison.OrdinalIgnoreCase
                        )
                        || name.EndsWith("PdfSharp.WPFonts.dll", StringComparison.OrdinalIgnoreCase)
                        || name.EndsWith(
                            "Microsoft.DiaSymReader.Native.amd64.dll",
                            StringComparison.OrdinalIgnoreCase
                        )
                        || name.EndsWith(
                            "Microsoft.DiaSymReader.Native.arm64.dll",
                            StringComparison.OrdinalIgnoreCase
                        )
                        || name.EndsWith("mscordaccore.dll", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("mscordaccore_", StringComparison.OrdinalIgnoreCase)
                        || name.EndsWith("mscordbi.dll", StringComparison.OrdinalIgnoreCase)
                    ),
            ];
            if (forbiddenFiles.Length > 0)
            {
                throw new InvalidOperationException(
                    $"MSIX contains forbidden release files: {string.Join(", ", forbiddenFiles)}"
                );
            }

            XDocument manifest = LoadXmlEntry(archive, "AppxManifest.xml");
            XElement identity = manifest
                .Descendants()
                .Single(element => element.Name.LocalName == "Identity");
            AssertAttribute(identity, "Name", PackageIdentity);
            AssertAttribute(identity, "Publisher", ExpectedPublisher);
            AssertAttribute(identity, "Version", AppxVersion);
            AssertAttribute(identity, "ProcessorArchitecture", architecture);
            XElement application = manifest
                .Descendants()
                .Single(element => element.Name.LocalName == "Application");
            AssertAttribute(application, "Id", "App");
            AssertAttribute(application, "Executable", "TateYoko.exe");
            AssertAttribute(application, "EntryPoint", "Windows.FullTrustApplication");

            XElement packageIntegrity = manifest
                .Descendants()
                .Single(element => element.Name.LocalName == "PackageIntegrity");
            XElement integrityContent = packageIntegrity
                .Elements()
                .Single(element => element.Name.LocalName == "Content");
            AssertAttribute(integrityContent, "Enforcement", "on");

            string[] resourceLanguages =
            [
                .. manifest
                    .Descendants()
                    .Where(element => element.Name.LocalName == "Resource")
                    .Select(element => (string?)element.Attribute("Language") ?? string.Empty)
                    .Order(StringComparer.Ordinal),
            ];
            string[] expectedResourceLanguages =
            [
                .. SupportedResourceLanguages
                    .Select(language => language.ToUpperInvariant())
                    .Order(StringComparer.Ordinal),
            ];
            if (!resourceLanguages.SequenceEqual(expectedResourceLanguages, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Unexpected MSIX resource languages: {string.Join(", ", resourceLanguages)}"
                );
            }

            string[] capabilities =
            [
                .. manifest
                    .Descendants()
                    .Where(element => element.Name.LocalName == "Capability")
                    .Select(element => (string?)element.Attribute("Name") ?? string.Empty),
            ];
            if (!capabilities.SequenceEqual(["runFullTrust"], StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Unexpected MSIX capabilities: {string.Join(", ", capabilities)}"
                );
            }

            XElement[] exposedExtensions =
            [
                .. manifest
                    .Descendants()
                    .Where(element =>
                        element.Name.LocalName == "Extension"
                        && !IsFrameworkInternalExtension(element)
                    ),
            ];
            if (exposedExtensions.Length > 0)
            {
                string categories = string.Join(
                    ", ",
                    exposedExtensions.Select(extension =>
                        (string?)extension.Attribute("Category") ?? "<missing>"
                    )
                );
                throw new InvalidOperationException(
                    $"MSIX contains forbidden public extensions: {categories}."
                );
            }

            XElement dependencies = manifest
                .Descendants()
                .Single(element => element.Name.LocalName == "Dependencies");
            XElement[] dependencyEntries = [.. dependencies.Elements()];
            XElement[] targetFamilies =
            [
                .. dependencyEntries.Where(entry => entry.Name.LocalName == "TargetDeviceFamily"),
            ];
            XElement[] packageDependencies =
            [
                .. dependencyEntries.Where(entry => entry.Name.LocalName == "PackageDependency"),
            ];
            if (
                targetFamilies.Length != 1
                || packageDependencies.Length == 0
                || dependencyEntries.Length != targetFamilies.Length + packageDependencies.Length
                || packageDependencies.Any(dependency =>
                    !((string?)dependency.Attribute("Name") ?? string.Empty).StartsWith(
                        "Microsoft.WindowsAppRuntime.",
                        StringComparison.Ordinal
                    )
                )
            )
            {
                throw new InvalidOperationException(
                    "MSIX must depend only on Windows 11 and the serviced Windows App Runtime. "
                        + $"Found: {string.Join(", ", dependencyEntries.Select(entry => entry.Name.LocalName))}"
                );
            }

            XElement targetFamily = targetFamilies[0];
            AssertAttribute(targetFamily, "MinVersion", "10.0.22000.0");
            AssertAttribute(targetFamily, "MaxVersionTested", "10.0.26100.0");
            if (!Path.GetFileName(path).Contains(architecture, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"MSIX filename does not identify {architecture}: {path}"
                );
            }
        }

        private void ValidateBundle(string path)
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length is <= 0 or > 110L * 1024 * 1024)
            {
                throw new InvalidOperationException(
                    $"MSIX bundle has an invalid size or exceeds the 110 MiB release budget: {path}"
                );
            }

            using ZipArchive archive = ZipFile.OpenRead(path);
            ValidateArchiveEntries(archive);
            XDocument manifest = LoadXmlEntry(archive, "AppxMetadata/AppxBundleManifest.xml");
            XElement identity = manifest
                .Descendants()
                .Single(element => element.Name.LocalName == "Identity");
            AssertAttribute(identity, "Name", PackageIdentity);
            AssertAttribute(identity, "Publisher", ExpectedPublisher);
            AssertAttribute(identity, "Version", AppxVersion);
            XElement[] packages =
            [
                .. manifest.Descendants().Where(element => element.Name.LocalName == "Package"),
            ];
            string[] architectures =
            [
                .. packages
                    .Select(element => (string?)element.Attribute("Architecture") ?? string.Empty)
                    .Order(StringComparer.Ordinal),
            ];
            if (
                packages.Length != 2
                || !architectures.SequenceEqual(["arm64", "x64"], StringComparer.Ordinal)
            )
            {
                throw new InvalidOperationException(
                    "Bundle must contain exactly one x64 and one ARM64 application package. "
                        + $"Found: {string.Join(", ", architectures)}"
                );
            }

            foreach (XElement package in packages)
            {
                AssertAttribute(package, "Type", "application");
                AssertAttribute(package, "Version", AppxVersion);
                string fileName = (string?)package.Attribute("FileName") ?? string.Empty;
                ZipArchiveEntry[] matchingEntries =
                [
                    .. archive.Entries.Where(entry =>
                        string.Equals(entry.FullName, fileName, StringComparison.Ordinal)
                    ),
                ];
                if (
                    !fileName.EndsWith(".msix", StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(
                        Path.GetFileName(fileName),
                        fileName,
                        StringComparison.Ordinal
                    )
                    || matchingEntries.Length != 1
                    || matchingEntries[0].Length is <= 0 or > 80L * 1024 * 1024
                )
                {
                    throw new InvalidOperationException(
                        $"Bundle package entry is invalid or missing: {fileName}"
                    );
                }
            }

            string verificationDirectory = Path.Combine(
                _publishDirectory,
                $"bundle-verification-{Guid.NewGuid():N}"
            );
            Directory.CreateDirectory(verificationDirectory);
            try
            {
                foreach (XElement package in packages)
                {
                    string architecture =
                        (string?)package.Attribute("Architecture") ?? string.Empty;
                    string fileName = (string?)package.Attribute("FileName") ?? string.Empty;
                    ZipArchiveEntry entry = archive.Entries.Single(item =>
                        string.Equals(item.FullName, fileName, StringComparison.Ordinal)
                    );
                    string extractedPath = Path.Combine(
                        verificationDirectory,
                        $"TateYoko-{architecture}.msix"
                    );
                    entry.ExtractToFile(extractedPath);
                    ValidateMsix(extractedPath, architecture);
                }
            }
            finally
            {
                SafeDeleteDirectory(verificationDirectory);
            }
        }

        private void ValidateAppInstaller() => ValidateAppInstaller(AppInstallerPath);

        private void ValidateAppInstaller(string path)
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length is <= 0 or > 1024 * 1024)
            {
                throw new InvalidOperationException($"AppInstaller has an invalid size: {path}");
            }

            using FileStream stream = File.OpenRead(path);
            XDocument document = LoadXml(stream);
            XElement root =
                document.Root
                ?? throw new InvalidOperationException("AppInstaller has no root element.");
            XNamespace expectedNamespace = "http://schemas.microsoft.com/appx/appinstaller/2018";
            if (root.Name != expectedNamespace + "AppInstaller")
            {
                throw new InvalidOperationException("Invalid AppInstaller root.");
            }

            AssertAttribute(root, "Version", AppxVersion);
            XElement mainBundle = root.Descendants()
                .Single(element => element.Name.LocalName == "MainBundle");
            AssertAttribute(mainBundle, "Name", PackageIdentity);
            AssertAttribute(mainBundle, "Publisher", ExpectedPublisher);
            AssertAttribute(mainBundle, "Version", AppxVersion);
            AssertAttribute(
                root,
                "Uri",
                "https://github.com/P4suta/TateYoko/releases/latest/download/TateYoko.appinstaller"
            );
            AssertAttribute(
                mainBundle,
                "Uri",
                "https://github.com/P4suta/TateYoko/releases/latest/download/TateYoko.msixbundle"
            );
            XElement onLaunch = root.Descendants()
                .Single(element => element.Name.LocalName == "OnLaunch");
            AssertAttribute(onLaunch, "HoursBetweenUpdateChecks", "12");
            AssertAttribute(onLaunch, "ShowPrompt", "true");
            AssertAttribute(onLaunch, "UpdateBlocksActivation", "false");
            if (
                !root.Descendants()
                    .Any(element => element.Name.LocalName == "AutomaticBackgroundTask")
            )
            {
                throw new InvalidOperationException(
                    "AppInstaller automatic background updates are not enabled."
                );
            }

            string[] rootChildren =
            [
                .. root.Elements()
                    .Select(element => element.Name.LocalName)
                    .Order(StringComparer.Ordinal),
            ];
            if (
                !rootChildren.SequenceEqual(
                    ["MainBundle", "UpdateSettings"],
                    StringComparer.Ordinal
                )
            )
            {
                throw new InvalidOperationException(
                    $"Unexpected AppInstaller elements: {string.Join(", ", rootChildren)}"
                );
            }
        }

        private void ValidateSbom(string path)
        {
            using JsonDocument document = JsonDocument.Parse(
                File.ReadAllBytes(path),
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 128,
                }
            );
            JsonElement root = document.RootElement;
            if (
                !string.Equals(
                    root.GetProperty("bomFormat").GetString(),
                    "CycloneDX",
                    StringComparison.Ordinal
                )
                || root.GetProperty("specVersion").GetString() is not string specVersion
                || specVersion.Length == 0
            )
            {
                throw new InvalidOperationException(
                    "The release SBOM is not valid CycloneDX JSON."
                );
            }

            JsonElement component = root.GetProperty("metadata").GetProperty("component");
            if (
                !string.Equals(
                    component.GetProperty("name").GetString(),
                    "TateYoko",
                    StringComparison.Ordinal
                )
                || !string.Equals(
                    component.GetProperty("type").GetString(),
                    "application",
                    StringComparison.Ordinal
                )
                || !string.Equals(
                    component.GetProperty("version").GetString(),
                    _options.Version,
                    StringComparison.Ordinal
                )
                || root.GetProperty("components").GetArrayLength() == 0
            )
            {
                throw new InvalidOperationException(
                    "The SBOM does not describe this release and its dependency graph."
                );
            }
        }

        private static void ValidateSourceSbom(string path)
        {
            string document = File.ReadAllText(path, Encoding.UTF8).Replace('\\', '/');
            string[] requiredFields =
            [
                "SPDXVersion: SPDX-2.1",
                "DataLicense: CC0-1.0",
                "DocumentName: TateYoko",
                "Creator: Person: Yasunobu Sakashita",
                "Creator: Tool: reuse-6.2.0",
                "FileName: ./REUSE.toml",
                "FileName: ./src/TateYoko.Engine/PdfSpreadConverter.cs",
                "LicenseInfoInFile: Apache-2.0",
                "LicenseInfoInFile: CC-BY-4.0",
            ];
            if (
                new FileInfo(path).Length is <= 0 or > 2L * 1024 * 1024
                || requiredFields.Any(field => !document.Contains(field, StringComparison.Ordinal))
            )
            {
                throw new InvalidOperationException(
                    "The source SBOM is not a complete REUSE-generated SPDX document."
                );
            }
        }

        private static void ValidateNotice(string path)
        {
            string notice = File.ReadAllText(path);
            if (
                !notice.StartsWith("TATEYOKO THIRD-PARTY NOTICES", StringComparison.Ordinal)
                || notice.Length < 1024
            )
            {
                throw new InvalidOperationException(
                    "The generated third-party notice is incomplete."
                );
            }
        }

        private static void ValidateExactFileSet(
            string directory,
            IEnumerable<string> expectedNames
        )
        {
            string[] expected = [.. expectedNames.Order(StringComparer.Ordinal)];
            string[] actualPaths =
            [
                .. Directory.EnumerateFileSystemEntries(
                    directory,
                    "*",
                    SearchOption.TopDirectoryOnly
                ),
            ];
            string[] reparsePoints =
            [
                .. actualPaths
                    .Where(path => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    .Select(path => Path.GetFileName(path)),
            ];
            if (reparsePoints.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Signed artifact set contains reparse points: {string.Join(", ", reparsePoints)}"
                );
            }

            string[] actual =
            [
                .. actualPaths.Select(path => Path.GetFileName(path)).Order(StringComparer.Ordinal),
            ];
            if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    "Signed artifact set is invalid. Expected ["
                        + string.Join(", ", expected)
                        + "], found ["
                        + string.Join(", ", actual)
                        + "]."
                );
            }
        }

        private static XDocument LoadXmlEntry(ZipArchive archive, string entryName)
        {
            ZipArchiveEntry[] matches =
            [
                .. archive.Entries.Where(entry =>
                    string.Equals(entry.FullName, entryName, StringComparison.Ordinal)
                ),
            ];
            if (matches.Length != 1 || matches[0].Length is <= 0 or > 1024 * 1024)
            {
                throw new InvalidOperationException(
                    $"Package entry is missing, duplicated, or oversized: {entryName}"
                );
            }

            ZipArchiveEntry entry = matches[0];
            using Stream stream = entry.Open();
            return LoadXml(stream);
        }

        private static XDocument LoadXml(Stream stream)
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                MaxCharactersInDocument = 1024 * 1024,
                XmlResolver = null,
            };
            using XmlReader reader = XmlReader.Create(stream, settings);
            return XDocument.Load(reader, LoadOptions.None);
        }

        private static void ValidateArchiveEntries(ZipArchive archive)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string name = entry.FullName.Replace('\\', '/');
                if (
                    name.StartsWith('/')
                    || name.StartsWith("../", StringComparison.Ordinal)
                    || name.Contains("/../", StringComparison.Ordinal)
                    || !names.Add(name)
                )
                {
                    throw new InvalidOperationException(
                        $"Package contains an unsafe or duplicate path: {entry.FullName}"
                    );
                }

                if (name.EndsWith(".mui", StringComparison.OrdinalIgnoreCase))
                {
                    string[] segments = name.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    if (
                        segments.Length != 2
                        || !SupportedResourceLanguages.Contains(
                            segments[0],
                            StringComparer.OrdinalIgnoreCase
                        )
                    )
                    {
                        throw new InvalidOperationException(
                            $"Package contains an unsupported MUI resource: {entry.FullName}"
                        );
                    }
                }
            }
        }

        private static void AssertAttribute(XElement element, string attributeName, string expected)
        {
            string actual = (string?)element.Attribute(attributeName) ?? string.Empty;
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"{element.Name.LocalName}.{attributeName} is '{actual}', expected '{expected}'."
                );
            }
        }

        private static bool IsFrameworkInternalExtension(XElement extension)
        {
            if (
                (string?)extension.Attribute("Category")
                    != "windows.activatableClass.inProcessServer"
                || extension.Parent?.Parent?.Name.LocalName != "Package"
            )
            {
                return false;
            }

            XElement[] servers =
            [
                .. extension
                    .Elements()
                    .Where(element => element.Name.LocalName == "InProcessServer"),
            ];
            if (servers.Length != 1 || extension.Elements().Count() != 1)
            {
                return false;
            }

            XElement server = servers[0];
            XElement[] paths =
            [
                .. server.Elements().Where(element => element.Name.LocalName == "Path"),
            ];
            XElement[] classes =
            [
                .. server.Elements().Where(element => element.Name.LocalName == "ActivatableClass"),
            ];
            return server.Elements().Count() == 2
                && paths.Length == 1
                && paths[0].Value == "Microsoft.Web.WebView2.Core.dll"
                && classes.Length == 1
                && (string?)classes[0].Attribute("ActivatableClassId")
                    == "Microsoft.Web.WebView2.Core.CoreWebView2EnvironmentOptions"
                && (string?)classes[0].Attribute("ThreadingModel") == "both";
        }

        private static bool PathContainsSegment(string path, string segment) =>
            path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Contains(segment, StringComparer.OrdinalIgnoreCase);

        private static string FindBuildTool(string fileName)
        {
            string packagesRoot =
                Environment.GetEnvironmentVariable("NUGET_PACKAGES")
                ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".nuget",
                    "packages"
                );
            string packageRoot = Path.Combine(
                packagesRoot,
                "microsoft.windows.sdk.buildtools",
                BuildToolsVersion,
                "bin"
            );
            string[] matches = Directory.Exists(packageRoot)
                ?
                [
                    .. Directory
                        .GetFiles(packageRoot, fileName, SearchOption.AllDirectories)
                        .Where(path => PathContainsSegment(path, "x64")),
                ]
                : [];
            return matches.Length == 1
                ? matches[0]
                : throw new InvalidOperationException(
                    $"Could not resolve the pinned {fileName} from BuildTools {BuildToolsVersion}."
                );
        }

        private static void VerifySignerAndTimestamp(string path)
        {
            const string script =
                "$s=Get-AuthenticodeSignature -LiteralPath $args[0];"
                + "if($s.Status -ne 'Valid'){Write-Error ('invalid signature: '+$s.Status);exit 1};"
                + "if(-not $s.TimeStamperCertificate){Write-Error 'timestamp missing';exit 1};"
                + "if(-not [string]::Equals($s.SignerCertificate.Subject,$args[1],"
                + "[System.StringComparison]::OrdinalIgnoreCase))"
                + "{Write-Error ('unexpected signer: '+$s.SignerCertificate.Subject);exit 1}";
            string windowsPowerShell = Path.Combine(
                Environment.SystemDirectory,
                "WindowsPowerShell",
                "v1.0",
                "powershell.exe"
            );
            if (!File.Exists(windowsPowerShell))
            {
                throw new InvalidOperationException(
                    $"Windows PowerShell is unavailable: {windowsPowerShell}"
                );
            }

            RunProcess(
                windowsPowerShell,
                ["-NoProfile", "-NonInteractive", "-Command", script, path, ExpectedPublisher]
            );
        }

        private void SafeDeleteDirectory(string path)
        {
            string fullPath = Path.GetFullPath(path);
            string safeRoot =
                Path.GetFullPath(_publishDirectory).TrimEnd(Path.DirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(safeRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Refusing to delete outside the publish directory: {fullPath}"
                );
            }

            if (Directory.Exists(fullPath))
            {
                EnsureNoReparsePoints(fullPath);
                Directory.Delete(fullPath, recursive: true);
            }
        }

        private static void EnsureNoReparsePoints(string root)
        {
            var pending = new Stack<string>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                string current = pending.Pop();
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidOperationException(
                        $"Refusing to recursively delete a reparse point: {current}"
                    );
                }

                foreach (
                    string child in Directory.EnumerateFileSystemEntries(
                        current,
                        "*",
                        SearchOption.TopDirectoryOnly
                    )
                )
                {
                    if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0)
                    {
                        throw new InvalidOperationException(
                            $"Refusing to recursively delete a reparse point: {child}"
                        );
                    }

                    if (Directory.Exists(child))
                    {
                        pending.Push(child);
                    }
                }
            }
        }

        private static void CopyRequired(string source, string destination)
        {
            var sourceInfo = new FileInfo(source);
            if (!sourceInfo.Exists)
            {
                throw new InvalidOperationException($"Required file is missing: {source}");
            }

            if ((sourceInfo.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException(
                    $"Required file must not be a reparse point: {source}"
                );
            }

            if (
                File.Exists(destination)
                && (File.GetAttributes(destination) & FileAttributes.ReparsePoint) != 0
            )
            {
                throw new InvalidOperationException(
                    $"Refusing to overwrite a reparse point: {destination}"
                );
            }

            File.Copy(source, destination, overwrite: true);
        }

        private static void RequireDirectory(string path, string message)
        {
            if (!Directory.Exists(path))
            {
                throw new InvalidOperationException(message);
            }
        }

        private static string EnsureTrailingSeparator(string path) =>
            path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        private static void Step(string message) => Console.WriteLine($"==> {message}");
    }

    private sealed record PackOptions(string Version, string Configuration)
    {
        internal static (string Command, PackOptions Options) Parse(string[] args)
        {
            string command = "build";
            string version = "0.1.0";
            string configuration = "Release";
            bool commandSeen = false;
            for (int index = 0; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--version" when index + 1 < args.Length:
                        version = args[++index];
                        break;
                    case "--configuration" when index + 1 < args.Length:
                        configuration = args[++index];
                        break;
                    default:
                        if (args[index].StartsWith('-'))
                        {
                            throw new ArgumentException($"Unknown option: {args[index]}");
                        }

                        if (commandSeen)
                        {
                            throw new ArgumentException("Only one command may be specified.");
                        }

                        command = args[index];
                        commandSeen = true;
                        break;
                }
            }

            if (!ReleaseVersionPattern().IsMatch(version))
            {
                throw new ArgumentException("Version must be numeric SemVer X.Y.Z.");
            }

            string[] rawParts = version.Split('.');
            var parts = new ushort[rawParts.Length];
            for (int index = 0; index < rawParts.Length; index++)
            {
                if (
                    !ushort.TryParse(
                        rawParts[index],
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out parts[index]
                    )
                )
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(args),
                        "Every version component must be between 0 and 65535 for MSIX."
                    );
                }
            }

            if (parts.All(part => part == 0))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(args),
                    "Version 0.0.0 is reserved and cannot produce a deterministic MSIX bundle."
                );
            }

            if (!string.Equals(configuration, "Release", StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Release packaging only accepts Release configuration."
                );
            }

            return (command, new PackOptions(version, configuration));
        }
    }

    private static void RunProcess(string fileName, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo(fileName) { UseShellExecute = false };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process =
            Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start {fileName}.");
        if (!process.WaitForExit(30 * 60 * 1000))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            throw new TimeoutException(
                $"{Path.GetFileName(fileName)} exceeded the 30-minute process budget."
            );
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"{Path.GetFileName(fileName)} failed with exit code {process.ExitCode}."
            );
        }
    }
}
