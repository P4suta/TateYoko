using TateYoko.Core.Domain;
using TateYoko.Presentation.Abstractions;

namespace TateYoko.App.Services;

/// <summary><see cref="IUiStrings"/> backed by the app's resource loader (<see cref="Localized"/> / <see cref="ErrorMessages"/>).</summary>
public sealed class ResourceUiStrings : IUiStrings
{
    public string NotPdf => ErrorMessages.NotPdf;

    public string ForError(ErrorKind kind) => ErrorMessages.ForKind(kind);

    public string ProgressStarting => Localized.Get("ProgressStarting");

    public string ProgressFormat(int completed, int total) => Localized.Get("ProgressFormat", completed, total);
}
