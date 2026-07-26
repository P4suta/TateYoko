using System.Globalization;
using System.Text;

namespace TateYoko.App.Services;

internal sealed class DiagnosticLog : IDiagnosticLog
{
    private const int DefaultMaximumBytes = 256 * 1024;
    private static readonly Lock Sync = new();
    private readonly string _directory;
    private readonly int _maximumBytes;

    internal DiagnosticLog()
        : this(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TateYoko",
                "Logs"
            ),
            DefaultMaximumBytes
        ) { }

    internal DiagnosticLog(string directory, int maximumBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        _directory = Path.GetFullPath(directory);
        _maximumBytes = maximumBytes;
    }

    public void Write(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        try
        {
            Directory.CreateDirectory(_directory);
            string line = string.Create(
                CultureInfo.InvariantCulture,
                $"{DateTimeOffset.UtcNow:O}\t{exception.GetType().FullName}\t0x{exception.HResult:X8}{Environment.NewLine}"
            );
            string currentPath = Path.Combine(_directory, "diagnostics.log");
            string previousPath = Path.Combine(_directory, "diagnostics.previous.log");
            lock (Sync)
            {
                long currentLength = File.Exists(currentPath)
                    ? new FileInfo(currentPath).Length
                    : 0;
                if (currentLength + Encoding.UTF8.GetByteCount(line) > _maximumBytes)
                {
                    File.Move(currentPath, previousPath, overwrite: true);
                }

                File.AppendAllText(currentPath, line, Encoding.UTF8);
            }
        }
        catch (Exception loggingException)
            when (loggingException is IOException or UnauthorizedAccessException) { }
    }
}
