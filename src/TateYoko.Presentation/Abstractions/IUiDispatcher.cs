namespace TateYoko.Presentation.Abstractions;

/// <summary>
/// Marshals an action onto the UI thread. Abstracts the platform dispatcher so the view model can be
/// tested off the UI thread (the WinUI adapter wraps <c>DispatcherQueue</c>).
/// </summary>
public interface IUiDispatcher
{
    /// <summary>Runs <paramref name="action"/> on the UI thread (may be asynchronous).</summary>
    void Post(Action action);
}
