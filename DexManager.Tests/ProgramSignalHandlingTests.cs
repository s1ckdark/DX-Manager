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
/// - SIGINT는 ctx.Cancel = true로 기본 종료를 막고 cts만 취소한다 - 정리
///   콜백은 절대 부르지 않는다(기존 Console.CancelKeyPress가 하던
///   협조적 취소 흐름을 그대로 승계하고, host.Shutdown()과 경쟁하지
///   않는다).
/// - SIGTERM/SIGHUP은 ctx.Cancel을 건드리지 않고(기본 종료가 그대로
///   진행되게 둔다) guard를 통해 정리 콜백을 정확히 한 번 부른다.
/// - 정리 콜백이 던지는 예외는 신호 처리기 밖으로 새지 않는다(CLR
///   fail-fast를 피하기 위해).
///
/// 실기 검증(overlay/scrcpy 실제 회수)은
/// .omc/research/mac-fix-report.md에 기록한 하드웨어 명령으로 별도 확인한다.
/// </summary>
public class ProgramSignalHandlingTests
{
    [Fact]
    public void HandleSignalCore_Sigint_SuppressesDefaultTerminationAndCancelsToken_WithoutRunningCleanup()
    {
        using var cts = new CancellationTokenSource();
        var guard = new ShutdownCleanupGuard();
        var cleanupRuns = 0;
        var ctx = new PosixSignalContext(PosixSignal.SIGINT);

        Program.HandleSignalCore(ctx, cts, guard, () => cleanupRuns++);

        Assert.True(ctx.Cancel);
        Assert.True(cts.IsCancellationRequested);
        Assert.Equal(0, cleanupRuns);
        Assert.False(guard.HasStarted);
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
    }

    [Fact]
    public void HandleSignalCore_OverlappingTerminationSignalsShareOneGuard_CleanupRunsExactlyOnce()
    {
        // SIGTERM 뒤 SIGHUP처럼 서로 다른 신호가 겹쳐 들어오는 경우
        // (또는 SIGTERM이 중복 전달되는 경우)를 흉내낸다 - 같은 guard를
        // 공유하는 한 정리는 정확히 한 번만 실행돼야 한다.
        using var cts = new CancellationTokenSource();
        var guard = new ShutdownCleanupGuard();
        var cleanupRuns = 0;
        Action cleanup = () => Interlocked.Increment(ref cleanupRuns);

        Program.HandleSignalCore(new PosixSignalContext(PosixSignal.SIGTERM), cts, guard, cleanup);
        Program.HandleSignalCore(new PosixSignalContext(PosixSignal.SIGHUP), cts, guard, cleanup);

        Assert.Equal(1, cleanupRuns);
    }

    [Fact]
    public void HandleSignalCore_NullCleanupAction_TerminationSignalDoesNothingAndDoesNotThrow()
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
    public void HandleSignalCore_NullCancellationTokenSource_SigintStillSuppressesDefaultTermination()
    {
        var guard = new ShutdownCleanupGuard();
        var ctx = new PosixSignalContext(PosixSignal.SIGINT);

        var ex = Record.Exception(() => Program.HandleSignalCore(ctx, null, guard, () => { }));

        Assert.Null(ex);
        Assert.True(ctx.Cancel);
    }
}
