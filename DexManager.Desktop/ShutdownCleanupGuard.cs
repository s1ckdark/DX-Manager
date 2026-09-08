using System;
using System.Threading;

namespace DexManager.Desktop;

/// <summary>
/// 정상 종료(Avalonia Exit 이벤트)와 비정상 종료(SIGTERM/SIGINT/SIGHUP,
/// Program.cs 참고)가 같은 정리 로직을 정확히 한 번만 실행하도록 보장하는
/// 가드. Avalonia에 의존하지 않으므로 헤드리스로 테스트할 수 있다.
///
/// <see cref="System.Threading.Interlocked.Exchange(ref int, int)"/> 하나로
/// 승자를 가른다 - 두 경로가 서로 다른 스레드에서 동시에 들어와도(신호는
/// 스레드풀 스레드에서, Exit은 UI 스레드에서 올 수 있다) 정확히 하나만
/// <paramref name="cleanup"/>을 실행한다.
/// </summary>
public sealed class ShutdownCleanupGuard
{
    private int _started;

    /// <summary>
    /// 아직 아무도 시작하지 않았다면 <paramref name="cleanup"/>을 실행하고
    /// <c>true</c>를 돌려준다. 이미 (동시 호출 포함) 다른 호출이 먼저
    /// 시작했다면 아무 일도 하지 않고 <c>false</c>를 돌려준다.
    /// </summary>
    public bool TryRunOnce(Action cleanup)
    {
        if (cleanup == null) throw new ArgumentNullException(nameof(cleanup));
        if (Interlocked.Exchange(ref _started, 1) != 0) return false;
        cleanup();
        return true;
    }

    /// <summary>이미 (성공적으로 또는 실행 중) 시작되었는지 여부. 테스트 전용.</summary>
    public bool HasStarted => Volatile.Read(ref _started) != 0;
}
