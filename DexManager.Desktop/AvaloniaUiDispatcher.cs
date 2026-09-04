using Avalonia.Threading;
using DexManager.ViewModels;

namespace DexManager.Desktop;

/// <summary>
/// <see cref="IUiDispatcher"/>의 Avalonia 구현.
/// </summary>
public sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    public bool IsOnUiThread => Dispatcher.UIThread.CheckAccess();

    public void Post(Action action)
    {
        if (action == null) return;
        Dispatcher.UIThread.Post(action);
    }

    public Task InvokeAsync(Func<Task> action)
    {
        if (action == null) return Task.CompletedTask;
        return Dispatcher.UIThread.InvokeAsync(action);
    }
}
