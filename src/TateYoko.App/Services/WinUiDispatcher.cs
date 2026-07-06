using Microsoft.UI.Dispatching;
using TateYoko.Presentation.Abstractions;

namespace TateYoko.App.Services;

/// <summary><see cref="IUiDispatcher"/> backed by the WinUI <see cref="DispatcherQueue"/> captured on the UI thread.</summary>
public sealed class WinUiDispatcher : IUiDispatcher
{
    private readonly DispatcherQueue _queue;

    public WinUiDispatcher(DispatcherQueue queue) => _queue = queue;

    public void Post(Action action) => _queue.TryEnqueue(() => action());
}
