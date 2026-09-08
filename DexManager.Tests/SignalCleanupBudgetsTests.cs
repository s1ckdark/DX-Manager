using System;
using System.Runtime.InteropServices;
using DexManager.Models;
using DexManager.Utils;
using Xunit;

namespace DexManager.Tests;

/// <summary>
/// 각 프론트엔드 Program.cs의 신호 처리기가 SIGINT/SIGTERM/SIGHUP에 얼마나
/// 기다릴지 고르는 규칙만 고정한다. 실제 신호 전달이나 정리 자체는
/// 다루지 않는다 - 그건 BoundedExecutor/ShutdownCleanupGuard의 몫이고,
/// 배선 검증은 실기(.omc/research/2026-09-08-realdevice-lock-findings.md
/// 11절, 12절 / mac-fix-review-report.md fix round 2)로 한다.
///
/// 예전에는 SIGINT(대화형)만 짧은 예산, SIGTERM/SIGHUP(비대화형)은 실제
/// adb 정리 사슬에 맞춘 긴 예산으로 나뉘어 있었다. 코드 리뷰가 그 구분을
/// 반증했다: SIGINT도 이제 SIGTERM/SIGHUP과 같은 guard 경로로
/// host.Shutdown()을 직접 실행하고, "짧게" 잡았던 5초는 부하가 걸린
/// 정리 사슬(8~18초로 관측됨)에는 부족해 --dex 실행 중 Ctrl+C를 누르면
/// overlay가 흘렀다. 지금은 세 신호 모두 같은 예산(SignalCleanupBudgets.
/// Budget, ProcessTimeoutMs와 맞춘 15초)을 쓴다.
/// </summary>
public class SignalCleanupBudgetsTests
{
    [Fact]
    public void For_Sigint_ReturnsTheSharedBudget()
    {
        var budget = SignalCleanupBudgets.For(PosixSignal.SIGINT);

        Assert.Equal(SignalCleanupBudgets.Budget, budget);
    }

    [Fact]
    public void For_Sigterm_ReturnsTheSharedBudget()
    {
        var budget = SignalCleanupBudgets.For(PosixSignal.SIGTERM);

        Assert.Equal(SignalCleanupBudgets.Budget, budget);
    }

    [Fact]
    public void For_Sighup_ReturnsTheSharedBudget()
    {
        var budget = SignalCleanupBudgets.For(PosixSignal.SIGHUP);

        Assert.Equal(SignalCleanupBudgets.Budget, budget);
    }

    [Fact]
    public void For_AllThreeSignalsReturnTheIdenticalBudget()
    {
        // 이 성질이 이번 수정의 핵심이다 - SIGINT가 더 이상 SIGTERM/SIGHUP보다
        // 짧은 별도 예산을 받지 않는다는 것을 직접 고정한다. 예전에는
        // 여기서 SIGINT < SIGTERM/SIGHUP을 확인했다(InteractiveBudget_
        // IsShorterThanNonInteractiveBudget) - 그 비대칭 자체가 결함이었다.
        var sigint = SignalCleanupBudgets.For(PosixSignal.SIGINT);
        var sigterm = SignalCleanupBudgets.For(PosixSignal.SIGTERM);
        var sighup = SignalCleanupBudgets.For(PosixSignal.SIGHUP);

        Assert.Equal(sigterm, sigint);
        Assert.Equal(sigterm, sighup);
    }

    [Fact]
    public void Budget_IsAtLeastAsLongAsASingleAdbCallIsAllowedToTake()
    {
        // AppSettings.Timing.ProcessTimeoutMs가 "이 앱이 단일 adb 호출 하나를
        // 기다릴 가치가 있다고 보는" 상한이다. 여기서 그 상수를 하드코딩된
        // 15초로 베껴 적으면, 누군가 ProcessTimeoutMs를 나중에 올려도 이
        // 테스트는 그 사실을 모른 채 계속 통과한다 - 이름이 주장하는 관계를
        // 실제로는 지키지 못하게 된다. 그래서 상수를 직접 읽는다.
        var singleAdbCall = TimeSpan.FromMilliseconds(
            AppSettings.CreateDefault().Timing.ProcessTimeoutMs);

        Assert.True(SignalCleanupBudgets.Budget >= singleAdbCall);
    }
}
