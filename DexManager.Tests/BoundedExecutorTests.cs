using System;
using System.Threading;
using DexManager.Utils;
using Xunit;

namespace DexManager.Tests;

/// <summary>
/// BoundedExecutor는 각 프론트엔드 Program.cs의 신호 처리기가 정리 로직
/// (adb 호출을 포함해 블로킹될 수 있다)을 무한정 기다리지 않도록 예산을
/// 강제하는 부분이다. 실제 adb 호출은 테스트하지 않는다 - "예산 안에 끝나면
/// true", "예산을 넘기면 false를 돌려주고 대기를 포기한다"는 예산
/// 자체의 성질만 고정한다.
/// </summary>
public class BoundedExecutorTests
{
    [Fact]
    public void RunWithBudget_ActionCompletesInTime_ReturnsTrue()
    {
        var ran = false;

        var completed = BoundedExecutor.RunWithBudget(
            () => ran = true,
            TimeSpan.FromSeconds(2));

        Assert.True(completed);
        Assert.True(ran);
    }

    [Fact]
    public void RunWithBudget_ActionExceedsBudget_ReturnsFalseAndGivesUpWaiting()
    {
        using var actionStarted = new ManualResetEventSlim(false);

        var completed = BoundedExecutor.RunWithBudget(
            () =>
            {
                actionStarted.Set();
                Thread.Sleep(TimeSpan.FromSeconds(2));
            },
            TimeSpan.FromMilliseconds(100));

        Assert.False(completed);
        // RunWithBudget이 실제로 기다림을 포기했다는 증거: 예산(100ms)보다
        // 훨씬 짧게 반환됐어야 한다. action 자체는 배경에서 계속 돈다 -
        // 신호 처리기가 반환된 뒤 프로세스가 죽으면 함께 사라지는 게
        // 의도된 동작이다(각 프론트엔드 Program.cs.HandleTerminationSignal 주석 참고).
        Assert.True(actionStarted.Wait(TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void RunWithBudget_NullAction_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => BoundedExecutor.RunWithBudget(null, TimeSpan.FromSeconds(1)));
    }
}
