using ImageMagick;

var repoRoot = FindRepoRoot();
var sourcePath = Path.Combine(repoRoot, "assets", "AppIcon.png");
var assetsDir = Path.Combine(repoRoot, "src", "TateYoko.App", "Assets");

if (!File.Exists(sourcePath))
{
    throw new FileNotFoundException($"Icon source not found: {sourcePath}");
}

Directory.CreateDirectory(assetsDir);

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

Step("Writing MSIX logo files");
WriteSquare(88, "Square44x44Logo.scale-200.png");
WriteSquare(24, "Square44x44Logo.targetsize-24_altform-unplated.png");
WriteSquare(48, "Square44x44Logo.targetsize-48_altform-lightunplated.png");
WriteSquare(300, "Square150x150Logo.scale-200.png");
WriteSquare(50, "StoreLogo.png");
WriteCanvas(620, 300, "Wide310x150Logo.scale-200.png");
WriteCanvas(620, 300, "SplashScreen.scale-200.png");

Step("Done");
Console.WriteLine($"  output: {assetsDir}");
return 0;

MagickImage LoadResized(uint width, uint height)
{
    var image = new MagickImage(sourcePath);
    image.BackgroundColor = MagickColors.Transparent;
    image.FilterType = FilterType.Lanczos;
    image.Resize(new MagickGeometry(width, height) { IgnoreAspectRatio = true });
    image.Format = MagickFormat.Png32;
    return image;
}

void WriteSquare(uint side, string name)
{
    using MagickImage image = LoadResized(side, side);
    WritePng(image, name);
}

void WriteCanvas(uint width, uint height, string name)
{
    uint glyph = (uint)(Math.Min(width, height) * 0.8);
    using MagickImage image = LoadResized(glyph, glyph);
    using var canvas = new MagickImage(MagickColors.Transparent, width, height);
    canvas.Composite(image, Gravity.Center, CompositeOperator.Over);
    WritePng(canvas, name);
}

void WritePng(MagickImage image, string name)
{
    image.Strip();
    image.Settings.SetDefine(MagickFormat.Png, "exclude-chunk", "date,time");
    image.Write(Path.Combine(assetsDir, name), MagickFormat.Png32);
}

static void Step(string msg)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"==> {msg}");
    Console.ResetColor();
}

static string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "TateYoko.slnx")))
            return dir.FullName;
        dir = dir.Parent;
    }
    throw new InvalidOperationException(
        "Could not locate the repository root (TateYoko.slnx not found)."
    );
}
