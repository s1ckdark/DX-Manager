using System;
using System.Runtime.InteropServices;
using System.Threading;
using DexManager.Mac;
using DexManager.Utils;
using Xunit;

namespace DexManager.Tests;

/// <summary>
/// DexManager.Mac.Program.HandleSignalCore는 실제 신호 처리기(HandleSignal)의
/// 로직을 정적 필드(_cts/_host) 없이 순수하게 뽑아낸 것이다 - 실제 신호
/// 전달 자체는 이 테스트로 검증할 수 없다(PosixSignalRegistration이 실제
/// OS 신호를 요구한다). 여기서는 그 신호가 오든 안 오든 참이어야 하는
/// 배선 규칙만 고정한다:
///
/// - SIGINT/SIGTERM/SIGHUP 모두 ctx.Cancel을 건드리지 않는다(기본 종료가
///   그대로 진행되게 둔다) - 정리는 신호 처리기 자신이 guard를 통해
///   정확히 한 번, 신호별 예산 안에서 동기적으로 한다.
/// - SIGINT는 cts.Cancel()도 함께 호출한다(협조적으로 관측하는 코드가
///   있다면 최선의 노력으로 기회를 준다) - 이전 버전은 여기서 그치고
///   ctx.Cancel = true로 기본 종료까지 막았는데, 코드 리뷰가 실기 pty로
///   반증했다: Console.ReadLine()에 블로킹된 대시보드 프롬프트는 .NET
///   Unix StdInReader가 EINTR에서 재시도하기 때문에 cts가 취소돼도
///   반환하지 않고, 두 번째 Ctrl+C도 효과가 없었다(선재 결함, 옛
///   Console.CancelKeyPress 방식도 동일하게 재현됨 - 이 파일이 새로
///   쓴 주석만 사실과 달랐다). 그래서 지금은 SIGINT도 SIGTERM/SIGHUP과
///   같은 guard 경로로 정리한다 - 어떤 코드가 무엇에 블로킹돼 있든
///   상관없이 정리와 프로세스 종료가 보장된다.
/// - 정리 콜백이 던지는 예외는 신호 처리기 밖으로 새지 않는다(CLR
///   fail-fast를 피하기 위해).
///
/// 실기 검증(overlay/scrcpy 실제 회수, Ctrl+C 실제 종료)은
/// .omc/research/mac-fix-report.md에 기록한 하드웨어 명령으로 별도 확인한다.
/// </summary>
public class ProgramSignalHandlingTests
{
    [Fact]
    public void HandleSignalCore_Sigint_CancelsTokenAndRunsCleanupThroughGuard()
    {
        using var cts = new CancellationTokenSource();
        var guard = new ShutdownCleanupGuard();
        var cleanupRuns = 0;
        var ctx = new PosixSignalContext(PosixSignal.SIGINT);

        Program.HandleSignalCore(ctx, cts, guard, () => cleanupRuns++);

        Assert.False(ctx.Cancel);
        Assert.True(cts.IsCancellationRequested);
        Assert.Equal(1, cleanupRuns);
        Assert.True(guard.HasStarted);
    }

    [Fact]
    public void HandleSignalCore_Sigterm_LeavesDefaultTerminationInPlaceAndRunsCleanupOnce()
    {
        using var cts = new CancellationTokenSource();
        var guard = new ShutdownCleanupGuard();
        var cleanupRuns = 0;
        var ctx = new PosixSignalContext(PosixSignal.SIGTERM);

        Program.HandleSignalCore(ctx, cts, guard, () => cleanupRuns++);

        Assert.False(ctx.Cancel);
        Assert.Equal(1, cleanupRuns);
        // SIGTERM은 SIGINT와 달리 협조적으로 기다리는 코드가 없다고
        // 가정하므로 cts는 건드리지 않는다.
        Assert.False(cts.IsCancellationRequested);
    }

    [Fact]
    public void HandleSignalCore_Sighup_BehavesLikeSigterm()
    {
        using var cts = new CancellationTokenSource();
        var guard = new ShutdownCleanupGuard();
        var cleanupRuns = 0;
        var ctx = new PosixSignalContext(PosixSignal.SIGHUP);

        Program.HandleSignalCore(ctx, cts, guard, () => cleanupRuns++);

        Assert.False(ctx.Cancel);
        Assert.Equal(1, cleanupRuns);
        Assert.False(cts.IsCancellationRequested);
    }

    [Fact]
    public void HandleSignalCore_OverlappingSignalsShareOneGuard_CleanupRunsExactlyOnce()
    {
        // SIGINT 뒤 SIGTERM처럼 서로 다른 신호가 겹쳐 들어오는 경우(또는
        // 같은 신호가 중복 전달되는 경우)를 흉내낸다 - 같은 guard를
        // 공유하는 한 정리는 정확히 한 번만 실행돼야 한다. 이제 SIGINT도
        // 같은 guard 경로를 타므로 이 성질이 세 신호 전부에 적용된다.
        using var cts = new CancellationTokenSource();
        var guard = new ShutdownCleanupGuard();
        var cleanupRuns = 0;
        Action cleanup = () => Interlocked.Increment(ref cleanupRuns);

        Program.HandleSignalCore(new PosixSignalContext(PosixSignal.SIGINT), cts, guard, cleanup);
        Program.HandleSignalCore(new PosixSignalContext(PosixSignal.SIGTERM), cts, guard, cleanup);

        Assert.Equal(1, cleanupRuns);
    }

    [Fact]
    public void HandleSignalCore_NormalDisposeAfterSignal_SharesGuardSoCleanupRunsOnce()
    {
        // 신호가 먼저 오고 뒤이어 정상 종료 경로(예: using host의 Dispose가
        // 어쩌다 실행되는 드문 창)가 같은 guard로 같은 정리를 부르는 경우를
        // 흉내낸다 - InteractiveHost.Shutdown 자신도 내부 가드를 갖고
        // 있지만, 이 바깥 guard만으로도 이미 중복을 막는다는 것을 고정한다.
        using var cts = new CancellationTokenSource();
        var guard = new ShutdownCleanupGuard();
        var cleanupRuns = 0;
        Action cleanup = () => Interlocked.Increment(ref cleanupRuns);

        Program.HandleSignalCore(new PosixSignalContext(PosixSignal.SIGTERM), cts, guard, cleanup);
        // "정상 경로"를 흉내낸 두 번째 직접 호출 - 실제 정상 경로는
        // guard.TryRunOnce를 직접 부르지 않지만, 같은 guard 인스턴스가
        // 공유되는 한 어떤 경로로 두 번째 시도가 오든 무시된다는 성질을
        // 여기서 직접 고정한다.
        var secondAttemptRan = guard.TryRunOnce(cleanup);

        Assert.Equal(1, cleanupRuns);
        Assert.False(secondAttemptRan);
    }

    [Fact]
    public void HandleSignalCore_NullCleanupAction_DoesNothingAndDoesNotThrow()
    {
        // host가 아직 만들어지지 않았거나 이미 정리된 창(finally에서
        // _host = null) 사이에 신호가 도착하는 경우를 흉내낸다.
        using var cts = new CancellationTokenSource();
        var guard = new ShutdownCleanupGuard();
        var ctx = new PosixSignalContext(PosixSignal.SIGTERM);

        var ex = Record.Exception(() => Program.HandleSignalCore(ctx, cts, guard, null));

        Assert.Null(ex);
        Assert.False(guard.HasStarted);
    }

    [Fact]
    public void HandleSignalCore_CleanupThrows_ExceptionIsSwallowedNotPropagated()
    {
        // 신호 처리기에서 예외가 새어 나가면 CLR이 fail-fast로 프로세스를
        // 죽인다 - HandleSignalCore가 이를 삼키는지 직접 고정한다.
        using var cts = new CancellationTokenSource();
        var guard = new ShutdownCleanupGuard();
        var ctx = new PosixSignalContext(PosixSignal.SIGTERM);

        var ex = Record.Exception(() =>
            Program.HandleSignalCore(ctx, cts, guard, () => throw new InvalidOperationException("boom")));

        Assert.Null(ex);
    }

    [Fact]
    public void HandleSignalCore_NullCancellationTokenSource_SigintDoesNotThrow()
    {
        var guard = new ShutdownCleanupGuard();
        var ctx = new PosixSignalContext(PosixSignal.SIGINT);
        var cleanupRuns = 0;

        var ex = Record.Exception(
            () => Program.HandleSignalCore(ctx, null, guard, () => cleanupRuns++));

        Assert.Null(ex);
        Assert.Equal(1, cleanupRuns);
    }
}
