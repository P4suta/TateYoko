namespace TateYoko.Engine.Internal;

internal static class AtomicOutput
{
    private const int MaximumUniqueNameAttempts = 10_000;
    private const int TemporaryDeleteAttempts = 4;

    internal static string CreateTemporaryPath(string requestedOutputPath)
    {
        string directory = Path.GetDirectoryName(requestedOutputPath)!;
        string fileName = Path.GetFileName(requestedOutputPath);
        return Path.Combine(directory, $".{fileName}.{Guid.NewGuid():N}.tmp");
    }

    internal static string Commit(
        string temporaryPath,
        string requestedOutputPath,
        OutputCollisionPolicy collisionPolicy
    )
    {
        Guard.Defined(collisionPolicy, nameof(collisionPolicy));
        try
        {
            if (collisionPolicy == OutputCollisionPolicy.ReplaceExisting)
            {
                File.Move(temporaryPath, requestedOutputPath, overwrite: true);
                return requestedOutputPath;
            }

            foreach (string candidate in EnumerateCandidates(requestedOutputPath))
            {
                if (!File.Exists(candidate))
                {
                    try
                    {
                        File.Move(temporaryPath, candidate, overwrite: false);
                        return candidate;
                    }
                    catch (IOException) when (File.Exists(candidate)) { }
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new PdfSpreadException(
                PdfSpreadError.WriteFailed,
                "output-commit-failed",
                exception
            );
        }

        throw new PdfSpreadException(PdfSpreadError.WriteFailed, "output-name-exhausted");
    }

    internal static void DeleteTemporary(string path)
    {
        for (int attempt = 0; attempt < TemporaryDeleteAttempts; attempt++)
        {
            try
            {
                File.Delete(path);
                return;
            }
            catch (IOException) when (attempt + 1 < TemporaryDeleteAttempts)
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(25 * (attempt + 1)));
            }
            catch (Exception exception)
                when (exception is IOException or UnauthorizedAccessException)
            {
                // Cleanup must never hide the conversion or cancellation result. A bounded retry
                // handles transient scanners; a persistent filesystem policy remains best-effort.
                return;
            }
        }
    }

    internal static IEnumerable<string> EnumerateCandidates(string requestedOutputPath)
    {
        yield return requestedOutputPath;

        string directory = Path.GetDirectoryName(requestedOutputPath)!;
        string extension = Path.GetExtension(requestedOutputPath);
        string stem = Path.GetFileNameWithoutExtension(requestedOutputPath);
        for (int suffix = 2; suffix <= MaximumUniqueNameAttempts; suffix++)
        {
            yield return Path.Combine(directory, $"{stem} ({suffix}){extension}");
        }
    }
}
