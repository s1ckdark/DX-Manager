using System;
using System.Runtime.InteropServices;
using DexManager.Desktop;
using Xunit;

namespace DexManager.Desktop.Tests;

/// <summary>
/// Program.cs의 신호 처리기가 SIGINT/SIGTERM/SIGHUP에 각각 얼마나 기다릴지
/// 고르는 규칙만 고정한다. 실제 신호 전달이나 정리 자체는 다루지 않는다 -
/// 그건 BoundedExecutor/ShutdownCleanupGuard의 몫이고, 배선 검증은 실기
/// (.omc/research/2026-09-08-realdevice-lock-findings.md 11절)로 한다.
///
/// SIGINT는 터미널에서 Ctrl+C로 오는 대화형 신호다 - 사용자가 화면을 보고
/// "지금 멈춰라"라고 기대하므로 짧게 유지한다. SIGTERM/SIGHUP은 launchd,
/// pkill -TERM, 제어 터미널 닫힘처럼 아무도 실시간으로 지켜보지 않는
/// 경로이므로, 실제 정리 사슬(overlay 회수용 adb 호출 하나가 최대
/// AppSettings.ProcessTimeoutMs=15000ms까지 걸릴 수 있다)에 맞춰 더
/// 넉넉하게 잡는다.
/// </summary>
public class SignalCleanupBudgetsTests
{
    [Fact]
    public void For_Sigint_ReturnsTheShortInteractiveBudget()
    {
        var budget = SignalCleanupBudgets.For(PosixSignal.SIGINT);

        Assert.Equal(SignalCleanupBudgets.Interactive, budget);
    }

    [Fact]
    public void For_Sigterm_ReturnsTheLongerNonInteractiveBudget()
    {
        var budget = SignalCleanupBudgets.For(PosixSignal.SIGTERM);

        Assert.Equal(SignalCleanupBudgets.NonInteractive, budget);
    }

    [Fact]
    public void For_Sighup_ReturnsTheLongerNonInteractiveBudget()
    {
        // SIGHUP은 제어 터미널이 끊길 때 온다 - 그 터미널은 이미 사라지고
        // 없으니 SIGINT처럼 "지금 지켜보는 사용자"가 없다. SIGTERM과 같이
        // 취급한다.
        var budget = SignalCleanupBudgets.For(PosixSignal.SIGHUP);

        Assert.Equal(SignalCleanupBudgets.NonInteractive, budget);
    }

    [Fact]
    public void NonInteractiveBudget_IsAtLeastAsLongAsASingleAdbCallIsAllowedToTake()
    {
        // AppSettings.cs의 기본 ProcessTimeoutMs(15000ms)가 "이 앱이 단일
        // adb 호출 하나를 기다릴 가치가 있다고 보는" 상한이다. 비대화형
        // 예산이 이보다 짧으면, 신호 처리기가 이 앱의 다른 어떤 코드보다도
        // 더 성급하게 정상적인 단일 호출을 잘라버리는 셈이 된다.
        Assert.True(
            SignalCleanupBudgets.NonInteractive >= TimeSpan.FromSeconds(15));
    }

    [Fact]
    public void InteractiveBudget_IsShorterThanNonInteractiveBudget()
    {
        Assert.True(
            SignalCleanupBudgets.Interactive < SignalCleanupBudgets.NonInteractive);
    }
}
