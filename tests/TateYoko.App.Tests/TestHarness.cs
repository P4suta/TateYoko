using TateYoko.App.Services;
using TateYoko.App.ViewModels;
using TateYoko.Engine;

namespace TateYoko.App.Tests;

internal sealed partial class TestHarness : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "TateYoko.App.Tests",
        Guid.NewGuid().ToString("N")
    );

    internal TestHarness()
    {
        Directory.CreateDirectory(_directory);
        Converter = new FakeConverter();
        Strings = new FakeStrings();
        Shell = new FakeShell();
        Log = new FakeLog();
        ViewModel = new MainViewModel(Converter, Strings, Shell, Log);
    }

    internal FakeConverter Converter { get; }

    internal FakeStrings Strings { get; }

    internal FakeShell Shell { get; }

    internal FakeLog Log { get; }

    internal MainViewModel ViewModel { get; }

    internal string CreatePdf(string name = "book.pdf")
    {
        string path = Path.Combine(_directory, name);
        File.WriteAllBytes(path, "%PDF-1.7"u8.ToArray());
        return path;
    }

    internal string PathFor(string name) => Path.Combine(_directory, name);

    public void Dispose()
    {
        ViewModel.Dispose();
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

internal sealed class FakeConverter : IPdfSpreadConverter
{
    internal Func<
        PdfSpreadRequest,
        IProgress<PdfSpreadProgress>?,
        CancellationToken,
        Task<PdfSpreadResult>
    >? Behavior { get; set; }

    internal List<PdfSpreadRequest> Requests { get; } = [];

    public async Task<PdfSpreadResult> ConvertAsync(
        PdfSpreadRequest request,
        IProgress<PdfSpreadProgress>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        Requests.Add(request);
        if (Behavior is not null)
        {
            return await Behavior(request, progress, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(new PdfSpreadProgress(1, 1));
        await File.WriteAllBytesAsync(
            request.OutputPath,
            "%PDF-1.7"u8.ToArray(),
            cancellationToken
        );
        return new PdfSpreadResult(request.OutputPath, 2, 1);
    }
}

internal sealed class FakeStrings : IUiStrings
{
    public string ProgressStarting => "starting";

    public string Cancelled => "cancelled";

    public string Done => "done";

    public string OutputActionFailed => "output-action-failed";

    public string ForError(PdfSpreadError error, string? technicalDetail = null) =>
        technicalDetail is null ? $"error:{error}" : $"error:{error}:{technicalDetail}";

    public string Progress(int completed, int total) => $"{completed}/{total}";
}

internal sealed class FakeShell : IShellLauncher
{
    internal Exception? Failure { get; set; }

    internal string? OpenedPath { get; private set; }

    internal string? ShownPath { get; private set; }

    public void Open(string filePath)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        OpenedPath = filePath;
    }

    public void ShowInFolder(string filePath)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        ShownPath = filePath;
    }
}

internal sealed class FakeLog : IDiagnosticLog
{
    internal Exception? LastException { get; private set; }

    public void Write(Exception exception) => LastException = exception;
}
