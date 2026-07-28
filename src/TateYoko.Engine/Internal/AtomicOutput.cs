namespace TateYoko.Engine.Internal;

internal static class AtomicOutput
{
    internal static string CreateTemporaryPath(string requestedOutputPath)
    {
        string directory = Path.GetDirectoryName(requestedOutputPath)!;
        string fileName = Path.GetFileName(requestedOutputPath);
        return Path.Combine(directory, $".{fileName}.{Guid.NewGuid():N}.tmp");
    }

    internal static string Commit(string temporaryPath, string requestedOutputPath)
    {
        try
        {
            if (File.Exists(requestedOutputPath))
            {
                File.Replace(temporaryPath, requestedOutputPath, destinationBackupFileName: null);
            }
            else
            {
                File.Move(temporaryPath, requestedOutputPath);
            }

            return requestedOutputPath;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new PdfSpreadException(
                PdfSpreadError.WriteFailed,
                "output-commit-failed",
                exception
            );
        }
    }

    internal static void DeleteTemporary(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new PdfSpreadException(
                PdfSpreadError.WriteFailed,
                "temporary-cleanup-failed",
                exception
            );
        }
    }
}
