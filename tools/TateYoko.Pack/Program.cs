using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

// Assembles the distribution bundle and, for the signed release pipeline, stages the first-party PEs
// for Authenticode signing. The bundle root holds only the native launcher (TateYoko.exe) plus
// README.txt / BUILDINFO.txt, so it is obvious which exe to run; the self-contained publish output
// (~350 files) is confined to app\.
//
// Commands (first non-option argument; default "all"):
//   all            bundle + package   (the local `mise run publish` flow)
//   bundle         assemble publish\<rid>\ only (no zip) — the step CI signs between
//   package        zip publish\<rid>\ into publish\package\ + write SHA256SUMS.txt
//   sign-stage     copy the first-party PEs into publish\sign-stage\ (flat) for eSigner batch_sign
//   sign-collect   copy the signed PEs from publish\signed\ back over the bundle
//   list-signable  print the bundle-relative first-party PE paths (one per line) — drives verify/gate
//
// The first-party PE list lives here (FirstPartyPes) as the single source of truth: the bundled
// .NET / Windows App SDK runtime DLLs are already Microsoft-signed, so we sign only our own five PEs.
//
// Prerequisite for bundle/all: the launcher is NativeAOT, so Visual Studio C++ build tools
// (MSVC linker + Windows SDK) are required.
//
// Usage: dotnet run --project tools/TateYoko.Pack -- [command] [--rid win-x64] [--version 0.1.0]

Console.OutputEncoding = Encoding.UTF8;

var (command, opts) = Options.Parse(args);
var repoRoot = FindRepoRoot();

var bundleDir    = Path.Combine(repoRoot, "publish", opts.Rid);                // bundle (zip root)
var appDir       = Path.Combine(bundleDir, "app");                            // app body (apphost + runtime)
var packageDir   = Path.Combine(repoRoot, "publish", "package");              // zip + checksum
var launcherTmp  = Path.Combine(repoRoot, "publish", $".launcher-{opts.Rid}"); // temp launcher publish
var signStageDir = Path.Combine(repoRoot, "publish", "sign-stage");           // flat dir handed to eSigner
var signedDir    = Path.Combine(repoRoot, "publish", "signed");              // eSigner output_path

// The only PEs we author. Bundle-root-relative, forward slashes; all basenames are distinct, so a
// flat staging copy never collides (no rename map needed, unlike find-my-files' two FindMyFiles.exe).
string[] firstPartyPes =
[
    "TateYoko.exe",             // root launcher (NativeAOT)
    "app/TateYoko.App.exe",     // apphost
    "app/TateYoko.App.dll",     // managed entry assembly
    "app/TateYoko.Core.dll",    // first-party domain library
    "app/TateYoko.Pdf.dll",     // first-party PDF library
];

switch (command)
{
    case "all":          Bundle(); Package(); break;
    case "bundle":       Bundle(); break;
    case "package":      Package(); break;
    case "sign-stage":   SignStage(); break;
    case "sign-collect": SignCollect(); break;
    case "list-signable": ListSignable(); break;
    default:
        Console.Error.WriteLine($"Unknown command '{command}'. Expected one of: " +
            "all, bundle, package, sign-stage, sign-collect, list-signable.");
        return 2;
}

return 0;

// --------------------------------------------------------------------------

// Steps 1-6: assemble the bundle and self-check it. No zip.
void Bundle()
{
    Step($"Preparing output ({bundleDir})");
    DeleteDirIfExists(bundleDir);
    DeleteDirIfExists(launcherTmp);
    Directory.CreateDirectory(appDir);

    Step("Publishing app into app\\");
    Dotnet("publish", Path.Combine(repoRoot, "src", "TateYoko.App"),
        "-c", opts.Configuration, "-r", opts.Rid, "--self-contained", "-o", appDir);

    Step("Publishing launcher (NativeAOT)");
    Dotnet("publish", Path.Combine(repoRoot, "src", "TateYoko.Launcher"),
        "-c", opts.Configuration, "-r", opts.Rid, "-o", launcherTmp);
    File.Copy(Path.Combine(launcherTmp, "TateYoko.exe"),
              Path.Combine(bundleDir, "TateYoko.exe"), overwrite: true);
    DeleteDirIfExists(launcherTmp);

    Step("Writing README.txt / BUILDINFO.txt");
    WriteTextFile(Path.Combine(bundleDir, "README.txt"),
        """
        縦横 (TateYoko)
        A Windows app that merges a vertical-writing PDF two pages at a time into right-bound (RTL) spreads.

        >> To start, double-click TateYoko.exe.

        No installation required. Copy this folder anywhere and run it.
        (Unpackaged / self-contained: the .NET and Windows App SDK runtimes are bundled.)

        Contents of this folder:
          TateYoko.exe    Launcher. Run this.
          app\            The app and its runtime. Do not touch.
          README.txt      This file.
          BUILDINFO.txt   Build version info.

        How to use:
          1. Start TateYoko.exe and drop a vertical-writing PDF onto the window (or click "Choose file").
          2. Choose how the first page opens.
          3. Click "Make spread". <name>_spread.pdf is written next to the input.
        """);

    var buildDate = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz");
    WriteTextFile(Path.Combine(bundleDir, "BUILDINFO.txt"),
        $"""
        product: 縦横 (TateYoko)
        version: {opts.Version}
        rid:     {opts.Rid}
        built:   {buildDate}
        """);

    Step("Verifying bundle");
    string[] required =
    [
        Path.Combine(bundleDir, "TateYoko.exe"),
        Path.Combine(appDir, "TateYoko.App.exe"),
        Path.Combine(appDir, "TateYoko.App.pri"),
    ];
    var missing = required.Where(p => !File.Exists(p)).ToArray();
    if (missing.Length > 0)
    {
        throw new InvalidOperationException(
            "Bundle is missing required files:\n  " + string.Join("\n  ", missing));
    }

    Console.WriteLine();
    Step("Bundle ready");
    Console.WriteLine($"  bundle : {bundleDir}");
}

// Steps 7-8: zip the (assembled, possibly signed) bundle and write SHA256SUMS.txt.
void Package()
{
    if (!Directory.Exists(bundleDir))
        throw new InvalidOperationException($"No bundle at {bundleDir}. Run the 'bundle' command first.");

    Directory.CreateDirectory(packageDir);

    Step("Creating distribution zip");
    var zipPath = Path.Combine(packageDir, $"TateYoko-v{opts.Version}-{opts.Rid}.zip");
    if (File.Exists(zipPath)) File.Delete(zipPath);
    // includeBaseDirectory: false packs the contents of win-x64\ at the zip root (launcher at top level).
    ZipFile.CreateFromDirectory(bundleDir, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);

    Step("Writing SHA256SUMS.txt");
    var hash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(zipPath)));
    // Pure ASCII, no BOM, LF-terminated for `sha256sum -c` compatibility.
    File.WriteAllText(Path.Combine(packageDir, "SHA256SUMS.txt"),
        $"{hash}  {Path.GetFileName(zipPath)}\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

    Console.WriteLine();
    Step("Package ready");
    Console.WriteLine($"  package: {zipPath}");
    Console.WriteLine($"           {Path.Combine(packageDir, "SHA256SUMS.txt")}");
}

// Copy the first-party PEs into a flat dir for eSigner batch_sign; prepare an empty output dir.
void SignStage()
{
    Step($"Staging first-party PEs ({signStageDir})");
    DeleteDirIfExists(signStageDir);
    DeleteDirIfExists(signedDir);
    Directory.CreateDirectory(signStageDir);
    Directory.CreateDirectory(signedDir);

    foreach (var rel in firstPartyPes)
    {
        var src = BundlePath(rel);
        if (!File.Exists(src))
            throw new InvalidOperationException($"First-party PE not found in bundle: {rel} ({src}).");
        var dst = Path.Combine(signStageDir, Path.GetFileName(rel));
        File.Copy(src, dst, overwrite: true);
        Console.WriteLine($"  staged {rel}");
    }
}

// Copy the signed PEs from publish\signed\ back over their bundle locations.
void SignCollect()
{
    Step("Collecting signed PEs back into the bundle");
    foreach (var rel in firstPartyPes)
    {
        var signed = Path.Combine(signedDir, Path.GetFileName(rel));
        if (!File.Exists(signed))
            throw new InvalidOperationException($"Signed PE not found: {signed}. Did signing run?");
        File.Copy(signed, BundlePath(rel), overwrite: true);
        Console.WriteLine($"  restored {rel}");
    }
}

// Print bundle-relative first-party PE paths, one per line, for the verify / publish gate.
void ListSignable()
{
    foreach (var rel in firstPartyPes) Console.WriteLine(rel);
}

string BundlePath(string rel) =>
    Path.Combine(bundleDir, rel.Replace('/', Path.DirectorySeparatorChar));

static void Step(string msg)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"==> {msg}");
    Console.ResetColor();
}

// Walk up from the base directory to TateYoko.slnx so the tool does not depend on the caller's CWD.
static string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "TateYoko.slnx")))
            return dir.FullName;
        dir = dir.Parent;
    }
    throw new InvalidOperationException("Could not locate the repository root (TateYoko.slnx not found).");
}

static void DeleteDirIfExists(string dir)
{
    if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
}

// Write UTF-8 with BOM and CRLF so Windows Notepad shows it correctly.
static void WriteTextFile(string path, string content)
{
    var normalized = content.ReplaceLineEndings("\r\n");
    File.WriteAllText(path, normalized, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
}

static void Dotnet(params string[] args)
{
    var psi = new ProcessStartInfo("dotnet") { UseShellExecute = false };
    foreach (var a in args) psi.ArgumentList.Add(a);

    // During NativeAOT linking, ILCompiler finds the MSVC toolchain via vswhere.exe, which is not on
    // PATH by default. Add its fixed install location (a Microsoft-guaranteed stable path) to the
    // child process PATH.
    var vsInstaller = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        "Microsoft Visual Studio", "Installer");
    if (File.Exists(Path.Combine(vsInstaller, "vswhere.exe")))
    {
        var path = psi.Environment.TryGetValue("PATH", out var p) ? p : "";
        if (path is null || !path.Split(Path.PathSeparator).Contains(vsInstaller, StringComparer.OrdinalIgnoreCase))
        {
            psi.Environment["PATH"] = string.IsNullOrEmpty(path)
                ? vsInstaller
                : $"{vsInstaller}{Path.PathSeparator}{path}";
        }
    }

    using var proc = Process.Start(psi)
        ?? throw new InvalidOperationException("Could not start the dotnet process.");
    proc.WaitForExit();
    if (proc.ExitCode != 0)
    {
        throw new InvalidOperationException(
            $"`dotnet {string.Join(' ', args)}` failed (exit {proc.ExitCode})." +
            " If the launcher AOT step failed, VS C++ build tools may be missing.");
    }
}

file sealed record Options(string Rid, string Version, string Configuration)
{
    // Returns the command (first bare argument, default "all") and the parsed options.
    public static (string Command, Options Options) Parse(string[] args)
    {
        var command = "all";
        var rid = "win-x64";
        var version = "0.1.0";
        var configuration = "Release";
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--rid" when i + 1 < args.Length: rid = args[++i]; break;
                case "--version" when i + 1 < args.Length: version = args[++i]; break;
                case "--configuration" when i + 1 < args.Length: configuration = args[++i]; break;
                default:
                    if (!args[i].StartsWith('-')) command = args[i];
                    break;
            }
        }
        return (command, new Options(rid, version, configuration));
    }
}
