using TateYoko.Engine;

namespace TateYoko.App.Services;

internal interface IUiStrings
{
    string ProgressStarting { get; }

    string Cancelled { get; }

    string Done { get; }

    string OutputActionFailed { get; }

    string ForError(PdfSpreadError error, string? technicalDetail = null);

    string Progress(int completed, int total);
}
