using QPdfNet;
using QPdfNet.Enums;

namespace TateYoko.Pdf;

/// <summary>
/// Rewrites a PDF as a linearized ("Fast Web View") file via qpdf, so a web viewer can render the
/// first page while the rest is still downloading. PDFsharp cannot linearize, so this runs as a
/// post-processing pass over the saved output. qpdf runs in-process through QPdfNet's P/Invoke; its
/// native binary ships x64/x86 only, so on other architectures (or any qpdf failure) linearization
/// is skipped and the caller keeps the un-linearized file.
/// </summary>
internal sealed class QpdfLinearizer
{
    /// <summary>
    /// Linearizes <paramref name="sourcePath"/> into <paramref name="destinationPath"/>.
    /// Returns <c>true</c> only when qpdf produced the destination file; on any failure (native
    /// binary missing on this architecture, qpdf error) returns <c>false</c> so the caller can fall
    /// back to the un-linearized file.
    /// </summary>
    public bool TryLinearize(string sourcePath, string destinationPath)
    {
        try
        {
            ExitCode result = new Job()
                .InputFile(sourcePath)
                .OutputFile(destinationPath)
                .Linearize()
                .Run(out _);

            // WarningsWereFoundFileProcessed still writes a valid linearized file; only
            // ErrorsFoundFileNotProcessed leaves the destination unwritten.
            return result is ExitCode.Success or ExitCode.WarningsWereFoundFileProcessed;
        }
        catch (Exception)
        {
            // Best-effort optimization: a missing native binary (non-x64/x86) or any qpdf failure
            // must not fail the conversion — the caller ships the un-linearized PDF instead.
            return false;
        }
    }
}
