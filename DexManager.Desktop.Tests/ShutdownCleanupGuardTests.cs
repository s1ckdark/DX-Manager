using System.Threading;
using System.Threading.Tasks;
using DexManager.Desktop;
using Xunit;

namespace DexManager.Desktop.Tests;

/// <summary>
/// ShutdownCleanupGuard는 App.axaml.cs의 OnExit(정상 종료)과
/// Program.cs의 HandleTerminationSignal(SIGTERM/SIGINT/SIGHUP)이 같은
/// DisposeQuietly를 정확히 한 번만 실행하도록 공유하는 가드다. 실제
/// 신호 전달 자체는 이 테스트로 검증할 수 없다 - 여기서는 그 신호가
/// 오든 안 오든 상관없이 참이어야 하는 성질(최대 한 번 실행, 동시
/// 호출에도 안전)만 고정한다.
/// </summary>
public class ShutdownCleanupGuardTests
{
    [Fact]
    public void TryRunOnce_FirstCall_RunsCleanupAndReturnsTrue()
    {
        var guard = new ShutdownCleanupGuard();
        var runCount = 0;

        var result = guard.TryRunOnce(() => runCount++);

        Assert.True(result);
        Assert.Equal(1, runCount);
        Assert.True(guard.HasStarted);
    }

    [Fact]
    public void TryRunOnce_SecondSequentialCall_DoesNotRunAgain()
    {
        // Exit이 신호 뒤에 오는 경우(또는 그 반대)를 흉내낸다: 두 경로
        // 모두 같은 가드로 TryRunOnce를 부르지만, 정리는 한 번만 실행돼야
        // 한다.
        var guard = new ShutdownCleanupGuard();
        var runCount = 0;

        var first = guard.TryRunOnce(() => runCount++);
        var second = guard.TryRunOnce(() => runCount++);

        Assert.True(first);
        Assert.False(second);
        Assert.Equal(1, runCount);
    }

    [Fact]
    public async Task TryRunOnce_ConcurrentCalls_RunsCleanupExactlyOnce()
    {
        // 신호는 스레드풀 스레드에서, Exit은 UI 스레드에서 동시에 들어올
        // 수 있다 - Interlocked 가드가 경쟁 상태에서도 정확히 한 번만
        // 실행을 허용하는지 확인한다.
        var guard = new ShutdownCleanupGuard();
        var runCount = 0;
        var successCount = 0;
        const int concurrency = 50;
        using var start = new ManualResetEventSlim(false);

        var tasks = new Task[concurrency];
        for (var i = 0; i < concurrency; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                start.Wait();
                if (guard.TryRunOnce(() => Interlocked.Increment(ref runCount)))
                {
                    Interlocked.Increment(ref successCount);
                }
            });
        }

        start.Set();
        await Task.WhenAll(tasks);

        Assert.Equal(1, runCount);
        Assert.Equal(1, successCount);
    }

    [Fact]
    public void TryRunOnce_NullCleanup_Throws()
    {
        var guard = new ShutdownCleanupGuard();

        Assert.Throws<System.ArgumentNullException>(() => guard.TryRunOnce(null));
    }
}
