using System.Globalization;
using TateYoko.App.Services;

namespace TateYoko.App.Tests;

public sealed class DiagnosticLogTests
{
    [Fact]
    public void DefaultLogLocationCanBeResolved()
    {
        var log = new DiagnosticLog();

        Assert.NotNull(log);
    }

    [Fact]
    public void WritesOnlyTimestampTypeAndHResult()
    {
        string directory = CreateDirectory();
        try
        {
            var log = new DiagnosticLog(directory, 4_096);
            var exception = new InvalidOperationException(
                @"secret C:\Users\someone\private.pdf password=hunter2"
            );

            log.Write(exception);

            string contents = File.ReadAllText(Path.Combine(directory, "diagnostics.log"));
            Assert.Contains(
                typeof(InvalidOperationException).FullName!,
                contents,
                StringComparison.Ordinal
            );
            Assert.Contains($"0x{exception.HResult:X8}", contents, StringComparison.Ordinal);
            Assert.DoesNotContain("private.pdf", contents, StringComparison.Ordinal);
            Assert.DoesNotContain("hunter2", contents, StringComparison.Ordinal);
            Assert.DoesNotContain("secret", contents, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RotatesToOneBoundedPreviousFile()
    {
        string directory = CreateDirectory();
        try
        {
            var log = new DiagnosticLog(directory, 100);
            for (int index = 0; index < 20; index++)
            {
                log.Write(
                    new InvalidOperationException(index.ToString(CultureInfo.InvariantCulture))
                );
            }

            string[] files = Directory.GetFiles(directory);
            Assert.Equal(2, files.Length);
            Assert.Contains(Path.Combine(directory, "diagnostics.log"), files);
            Assert.Contains(Path.Combine(directory, "diagnostics.previous.log"), files);
            Assert.InRange(new FileInfo(Path.Combine(directory, "diagnostics.log")).Length, 1, 100);
            Assert.InRange(
                new FileInfo(Path.Combine(directory, "diagnostics.previous.log")).Length,
                1,
                100
            );
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void LoggingFailureNeverEscapes()
    {
        string directory = CreateDirectory();
        try
        {
            string fileInsteadOfDirectory = Path.Combine(directory, "not-a-directory");
            File.WriteAllText(fileInsteadOfDirectory, "sentinel");
            var log = new DiagnosticLog(fileInsteadOfDirectory, 4_096);

            log.Write(new InvalidOperationException("must not escape"));

            Assert.Equal("sentinel", File.ReadAllText(fileInsteadOfDirectory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "TateYoko.App.Tests",
            Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(path);
        return path;
    }
}
