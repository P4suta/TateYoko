using ImageMagick;

// Regenerates the app icon assets from assets/AppIcon.png:
//   - src/TateYoko.App/Assets/AppIcon.ico  (multi-size, Lanczos-resampled)
//   - the MSIX logo PNGs in the same Assets folder
//
// Usage: dotnet run --project tools/TateYoko.Icons

var repoRoot = FindRepoRoot();
var sourcePath = Path.Combine(repoRoot, "assets", "AppIcon.png");
var assetsDir = Path.Combine(repoRoot, "src", "TateYoko.App", "Assets");

if (!File.Exists(sourcePath))
{
    throw new FileNotFoundException($"Icon source not found: {sourcePath}");
}

Directory.CreateDirectory(assetsDir);

// --- AppIcon.ico ----------------------------------------------------------
// One frame per size; ImageMagick stores 256 as PNG and smaller sizes as BMP.
Step("Writing AppIcon.ico");
uint[] icoSizes = [16, 20, 24, 32, 40, 48, 64, 128, 256];
using (var frames = new MagickImageCollection())
{
    foreach (uint size in icoSizes)
    {
        frames.Add(LoadResized(size, size));
    }

    frames.Write(Path.Combine(assetsDir, "AppIcon.ico"), MagickFormat.Ico);
}
Console.WriteLine("  sizes: " + string.Join(", ", icoSizes));

// --- MSIX logo PNGs -------------------------------------------------------
Step("Writing logo PNGs");
WriteSquare(88, "Square44x44Logo.scale-200.png");
WriteSquare(24, "Square44x44Logo.targetsize-24_altform-unplated.png");
WriteSquare(48, "Square44x44Logo.targetsize-48_altform-lightunplated.png");
WriteSquare(300, "Square150x150Logo.scale-200.png");
WriteSquare(50, "StoreLogo.png");
WriteSquare(48, "LockScreenLogo.scale-200.png");
WriteCanvas(620, 300, "Wide310x150Logo.scale-200.png");
WriteCanvas(620, 300, "SplashScreen.scale-200.png");

Step("Done");
Console.WriteLine($"  output: {assetsDir}");
return 0;

// --------------------------------------------------------------------------

// Loads the source and resizes it to an exact width x height with Lanczos.
MagickImage LoadResized(uint width, uint height)
{
    var image = new MagickImage(sourcePath);
    image.BackgroundColor = MagickColors.Transparent;
    image.FilterType = FilterType.Lanczos;
    image.Resize(new MagickGeometry(width, height) { IgnoreAspectRatio = true });
    image.Format = MagickFormat.Png32;
    return image;
}

// Writes a square logo of the given side length.
void WriteSquare(uint side, string name)
{
    using MagickImage image = LoadResized(side, side);
    image.Write(Path.Combine(assetsDir, name), MagickFormat.Png32);
}

// Writes a non-square logo: the glyph centered on a transparent canvas.
void WriteCanvas(uint width, uint height, string name)
{
    uint glyph = (uint)(Math.Min(width, height) * 0.8);
    using MagickImage image = LoadResized(glyph, glyph);
    using var canvas = new MagickImage(MagickColors.Transparent, width, height);
    canvas.Composite(image, Gravity.Center, CompositeOperator.Over);
    canvas.Write(Path.Combine(assetsDir, name), MagickFormat.Png32);
}

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
