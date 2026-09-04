using DexManager.ViewModels;

namespace DexManager.ViewModels.Tests;

/// <summary>
/// 테스트용 디스패처. 모든 작업을 호출 스레드에서 즉시 실행하므로
/// ViewModel 로직을 UI 없이 동기적으로 검증할 수 있다.
/// </summary>
public sealed class ImmediateUiDispatcher : IUiDispatcher
{
    public bool IsOnUiThread => true;

    /// <summary>지금까지 <see cref="Post"/>가 호출된 횟수.</summary>
    public int PostCount { get; private set; }

    public void Post(Action action)
    {
        PostCount++;
        action?.Invoke();
    }

    public Task InvokeAsync(Func<Task> action)
    {
        PostCount++;
        return action?.Invoke() ?? Task.CompletedTask;
    }
}
