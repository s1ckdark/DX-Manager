using DexManager.ViewModels;

namespace DexManager.ViewModels.Tests;

/// <summary>
/// 테스트용 디스패처. 작업을 즉시 실행하지 않고 큐에 넣어 두었다가
/// <see cref="Drain"/>에서 실행한다. 실제 <c>AvaloniaUiDispatcher</c>처럼
/// Post가 비동기이므로, Post와 실행 사이에 Dispose가 끼어드는 경합을
/// 테스트에서 그대로 재현할 수 있다.
/// </summary>
/// <remarks>
/// <see cref="ImmediateUiDispatcher"/>는 호출 스레드에서 즉시 실행하므로
/// 동기 경로만 검증할 수 있다. 실행 시점 방어(<c>_disposed</c> 확인)는
/// 이 디스패처로만 관측된다.
/// </remarks>
public sealed class QueueingUiDispatcher : IUiDispatcher
{
    private readonly Queue<Action> _pending = new Queue<Action>();

    public bool IsOnUiThread => true;

    /// <summary>지금까지 <see cref="Post"/>/<see cref="InvokeAsync"/>가 호출된 횟수.</summary>
    public int PostCount { get; private set; }

    /// <summary>아직 실행되지 않고 큐에 남아 있는 작업 수.</summary>
    public int PendingCount => _pending.Count;

    public void Post(Action action)
    {
        PostCount++;
        if (action == null) return;
        _pending.Enqueue(action);
    }

    public Task InvokeAsync(Func<Task> action)
    {
        PostCount++;
        if (action == null) return Task.CompletedTask;

        var completion = new TaskCompletionSource<object>();
        _pending.Enqueue(() =>
        {
            try
            {
                action().GetAwaiter().GetResult();
                completion.SetResult(null);
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        });
        return completion.Task;
    }

    /// <summary>큐에 쌓인 작업을 모두 실행하고 실행한 개수를 반환한다.</summary>
    public int Drain()
    {
        var ran = 0;
        while (_pending.Count > 0)
        {
            _pending.Dequeue()();
            ran++;
        }
        return ran;
    }
}
