using TateYoko.Engine;

namespace TateYoko.App.Services;

internal sealed class ResourceUiStrings : IUiStrings
{
    public string ForError(PdfSpreadError error, string? technicalDetail = null) =>
        ErrorMessages.For(error, technicalDetail);

    public string ProgressStarting => Localized.Get("ProgressStarting");

    public string Progress(int completed, int total) =>
        Localized.Get("ProgressFormat", completed, total);

    public string Cancelled => Localized.Get("Cancelled");

    public string Done => Localized.Get("DoneAnnouncement");

    public string OutputActionFailed => Localized.Get("OutputActionFailed");
}
