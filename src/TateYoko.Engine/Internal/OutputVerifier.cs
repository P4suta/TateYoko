using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace TateYoko.Engine.Internal;

internal static class OutputVerifier
{
    internal const double DimensionTolerancePoints = 1d / 128d;

    internal static void Verify(
        string path,
        IReadOnlyList<PageSize> expectedPageSizes,
        string? password
    )
    {
        try
        {
            using PdfDocument document = password is null
                ? PdfReader.Open(path, PdfDocumentOpenMode.Import)
                : PdfReader.Open(path, password, PdfDocumentOpenMode.Import);
            if (document.PageCount != expectedPageSizes.Count)
            {
                throw new PdfSpreadException(
                    PdfSpreadError.WriteFailed,
                    "output-page-count-mismatch"
                );
            }

            for (int index = 0; index < expectedPageSizes.Count; index++)
            {
                PageSize expected = expectedPageSizes[index];
                PdfPage actual = document.Pages[index];
                if (
                    Math.Abs(actual.Width.Point - expected.Width) > DimensionTolerancePoints
                    || Math.Abs(actual.Height.Point - expected.Height) > DimensionTolerancePoints
                )
                {
                    throw new PdfSpreadException(
                        PdfSpreadError.WriteFailed,
                        "output-page-size-mismatch"
                    );
                }
            }
        }
        catch (PdfSpreadException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new PdfSpreadException(
                PdfSpreadError.WriteFailed,
                "output-validation-failed",
                exception
            );
        }
    }
}
