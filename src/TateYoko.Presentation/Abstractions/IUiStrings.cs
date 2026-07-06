using TateYoko.Core.Domain;

namespace TateYoko.Presentation.Abstractions;

/// <summary>
/// Supplies the localized display strings the view model needs. Abstracts the resource loader
/// (which needs a packaged <c>.pri</c>) so the view model can be tested without one.
/// </summary>
public interface IUiStrings
{
    /// <summary>Message shown when a non-PDF file is selected.</summary>
    string NotPdf { get; }

    /// <summary>User-facing message for a conversion failure.</summary>
    string ForError(ErrorKind kind);

    /// <summary>Progress text shown before the first spread is reported.</summary>
    string ProgressStarting { get; }

    /// <summary>Progress text for <paramref name="completed"/> of <paramref name="total"/> spreads.</summary>
    string ProgressFormat(int completed, int total);
}
