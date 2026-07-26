using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace TateYoko.Engine.Tests;

public sealed class PopplerRenderedGoldenTests
{
    private static readonly Rgb Red = new(255, 0, 0);
    private static readonly Rgb Green = new(0, 255, 0);
    private static readonly Rgb Blue = new(0, 0, 255);
    private static readonly Rgb White = new(255, 255, 255);
    private readonly PdfSpreadConverter _converter = new();

    [Fact]
    public async Task StandardModeRendersPageOneOnTheRight()
    {
        using var temp = new TempDirectory();
        string output = Convert(
            temp,
            FirstPageMode.Standard,
            (255, 0, 0, 100, 150),
            (0, 0, 255, 100, 150)
        );

        PpmImage image = await RenderedPdf.RenderPageAsync(
            output,
            1,
            temp.File("standard"),
            TestContext.Current.CancellationToken
        );

        Assert.Equal((200, 150), (image.Width, image.Height));
        image.AssertColor(50, 75, Blue);
        image.AssertColor(150, 75, Red);
    }

    [Fact]
    public async Task CoverModeKeepsTheCoverRightAndThenPairsPages()
    {
        using var temp = new TempDirectory();
        string output = Convert(
            temp,
            FirstPageMode.Cover,
            (255, 0, 0, 100, 150),
            (0, 0, 255, 100, 150),
            (0, 255, 0, 100, 150)
        );

        PpmImage cover = await RenderedPdf.RenderPageAsync(
            output,
            1,
            temp.File("cover"),
            TestContext.Current.CancellationToken
        );
        PpmImage pages = await RenderedPdf.RenderPageAsync(
            output,
            2,
            temp.File("cover-pair"),
            TestContext.Current.CancellationToken
        );

        cover.AssertColor(50, 75, White);
        cover.AssertColor(150, 75, Red);
        pages.AssertColor(50, 75, Green);
        pages.AssertColor(150, 75, Blue);
    }

    [Fact]
    public async Task LeadingBlankModeRendersTheBlankRightOfPageOne()
    {
        using var temp = new TempDirectory();
        string output = Convert(temp, FirstPageMode.LeadingBlank, (255, 0, 0, 100, 150));

        PpmImage image = await RenderedPdf.RenderPageAsync(
            output,
            1,
            temp.File("leading-blank"),
            TestContext.Current.CancellationToken
        );

        image.AssertColor(50, 75, Red);
        image.AssertColor(150, 75, White);
    }

    [Fact]
    public async Task MixedPageSizesAreCenteredWithoutScaling()
    {
        using var temp = new TempDirectory();
        string output = Convert(
            temp,
            FirstPageMode.Standard,
            (255, 0, 0, 100, 200),
            (0, 0, 255, 200, 100)
        );

        PpmImage image = await RenderedPdf.RenderPageAsync(
            output,
            1,
            temp.File("mixed"),
            TestContext.Current.CancellationToken
        );

        Assert.Equal((400, 200), (image.Width, image.Height));
        image.AssertColor(25, 25, White);
        image.AssertColor(100, 100, Blue);
        image.AssertColor(225, 100, White);
        image.AssertColor(300, 100, Red);
        image.AssertColor(375, 100, White);
    }

    private string Convert(
        TempDirectory temp,
        FirstPageMode mode,
        params (byte Red, byte Green, byte Blue, double Width, double Height)[] pages
    )
    {
        string input = temp.File("input.pdf");
        string output = temp.File("output.pdf");
        SamplePdf.CreateColored(input, pages);
        _converter.Convert(
            new PdfSpreadRequest(input, output, mode),
            cancellationToken: TestContext.Current.CancellationToken
        );
        return output;
    }
}

internal static class RenderedPdf
{
    internal static async Task<PpmImage> RenderPageAsync(
        string pdfPath,
        int page,
        string outputPrefix,
        CancellationToken cancellationToken
    )
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ResolvePdfToPpm(),
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add(page.ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("-l");
        startInfo.ArgumentList.Add(page.ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("-r");
        startInfo.ArgumentList.Add("72");
        startInfo.ArgumentList.Add("-singlefile");
        startInfo.ArgumentList.Add(pdfPath);
        startInfo.ArgumentList.Add(outputPrefix);

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException(
                "The pinned Poppler renderer could not be started. Run `just setup-poppler` "
                    + "or set TATEYOKO_PDFTOPPM to a trusted pdftoppm executable.",
                exception
            );
        }

        Task<string> readError = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        string standardError = await readError;
        Assert.True(
            process.ExitCode == 0,
            $"pdftoppm exited with {process.ExitCode}: {standardError}"
        );

        return await PpmImage.ReadAsync($"{outputPrefix}.ppm", cancellationToken);
    }

    private static string ResolvePdfToPpm()
    {
        string? configured = Environment.GetEnvironmentVariable("TATEYOKO_PDFTOPPM");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            string fullPath = Path.GetFullPath(configured);
            if (!File.Exists(fullPath))
            {
                throw new InvalidOperationException(
                    $"TATEYOKO_PDFTOPPM does not identify a file: {fullPath}"
                );
            }

            return fullPath;
        }

        for (
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            directory is not null;
            directory = directory.Parent
        )
        {
            if (!File.Exists(Path.Combine(directory.FullName, "TateYoko.slnx")))
            {
                continue;
            }

            string toolRoot = Path.Combine(directory.FullName, "build", "tools", "poppler");
            if (!Directory.Exists(toolRoot))
            {
                break;
            }

            string[] candidates =
            [
                .. Directory
                    .EnumerateFiles(toolRoot, "pdftoppm.exe", SearchOption.AllDirectories)
                    .Take(2),
            ];
            if (candidates.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Expected one pinned pdftoppm.exe beneath {toolRoot}; "
                        + $"found {candidates.Length}."
                );
            }

            return candidates[0];
        }

        return "pdftoppm";
    }
}

internal readonly record struct Rgb(byte Red, byte Green, byte Blue);

internal sealed class PpmImage
{
    private readonly byte[] _pixels;

    private PpmImage(int width, int height, byte[] pixels)
    {
        Width = width;
        Height = height;
        _pixels = pixels;
    }

    internal int Width { get; }

    internal int Height { get; }

    internal static async Task<PpmImage> ReadAsync(string path, CancellationToken cancellationToken)
    {
        byte[] contents = await File.ReadAllBytesAsync(path, cancellationToken);
        var parser = new PpmHeaderParser(contents);
        Assert.Equal("P6", parser.ReadToken());
        int width = int.Parse(parser.ReadToken(), CultureInfo.InvariantCulture);
        int height = int.Parse(parser.ReadToken(), CultureInfo.InvariantCulture);
        Assert.Equal("255", parser.ReadToken());
        int pixelOffset = parser.ConsumeHeaderTerminator();
        int expectedBytes = checked(width * height * 3);
        Assert.Equal(expectedBytes, contents.Length - pixelOffset);
        return new PpmImage(width, height, contents[pixelOffset..]);
    }

    internal void AssertColor(int x, int y, Rgb expected)
    {
        Assert.InRange(x, 0, Width - 1);
        Assert.InRange(y, 0, Height - 1);
        int offset = checked(((y * Width) + x) * 3);
        var actual = new Rgb(_pixels[offset], _pixels[offset + 1], _pixels[offset + 2]);
        Assert.InRange(Math.Abs(actual.Red - expected.Red), 0, 3);
        Assert.InRange(Math.Abs(actual.Green - expected.Green), 0, 3);
        Assert.InRange(Math.Abs(actual.Blue - expected.Blue), 0, 3);
    }

    private sealed class PpmHeaderParser(byte[] contents)
    {
        private int _offset;

        internal string ReadToken()
        {
            SkipWhitespaceAndComments();
            int start = _offset;
            while (_offset < contents.Length && !char.IsWhiteSpace((char)contents[_offset]))
            {
                _offset++;
            }

            return System.Text.Encoding.ASCII.GetString(contents, start, _offset - start);
        }

        internal int ConsumeHeaderTerminator()
        {
            if (_offset >= contents.Length || !char.IsWhiteSpace((char)contents[_offset]))
            {
                throw new InvalidDataException("PPM header has no terminator.");
            }

            byte first = contents[_offset++];
            if (first == '\r' && _offset < contents.Length && contents[_offset] == '\n')
            {
                _offset++;
            }

            return _offset;
        }

        private void SkipWhitespaceAndComments()
        {
            while (_offset < contents.Length)
            {
                if (char.IsWhiteSpace((char)contents[_offset]))
                {
                    _offset++;
                    continue;
                }

                if (contents[_offset] != '#')
                {
                    return;
                }

                while (
                    _offset < contents.Length
                    && contents[_offset] is not (byte)'\r' and not (byte)'\n'
                )
                {
                    _offset++;
                }
            }
        }
    }
}
