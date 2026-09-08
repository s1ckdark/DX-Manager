using System;
using DexManager.Hosting;
using DexManager.Utils;
using Xunit;

namespace DexManager.Tests
{
    /// <summary>
    /// 후보 프로브 재시도가 체인 전체에서 쓸 수 있는 시간 예산
    /// (<see cref="ProbeRetryBudget"/>)의 성질만 고정한다. "누가 언제
    /// 재시도하는가"는 TransientProbeRetryTests가, "PathService가 체인
    /// 하나당 예산 하나를 쓰는가"는 PathServiceCandidateRetryTests가 맡는다.
    ///
    /// 시간은 <see cref="ManualClock"/>으로만 흘린다 - 실제로 기다리는
    /// 테스트는 느린 데다 부하가 걸린 CI에서 그 자체가 새 flake가 된다
    /// (이 브랜치가 고치려는 결함이 정확히 "부하 때문에 시간이 예상보다
    /// 오래 걸린다"는 것이므로 더더욱).
    /// </summary>
    public class ProbeRetryBudgetTests
    {
        [Theory]
        [InlineData(5000, 5000)]   // ApplicationHost가 실제로 넘기는 값
        [InlineData(15000, 15000)] // AppSettings.Timing.ProcessTimeoutMs
        [InlineData(3000, 3000)]
        [InlineData(1000, 3000)]   // 바닥에 걸린다
        [InlineData(0, 3000)]
        public void EffectiveProbeTimeout_IsTheCallersTimeoutRaisedToTheFloor(
            int timeoutMs,
            int expectedMs)
        {
            Assert.Equal(
                expectedMs,
                ProbeRetryBudget.EffectiveProbeTimeoutMs(timeoutMs));
            Assert.Equal(
                TimeSpan.FromMilliseconds(expectedMs),
                ProbeRetryBudget.EffectiveProbeTimeout(timeoutMs));
        }

        [Theory]
        [InlineData(5000, 5000)]
        [InlineData(15000, 15000)]
        [InlineData(1000, 3000)]
        public void For_TotalIsExactlyOneProbeTimeoutOfTheChainThatAsksForIt(
            int timeoutMs,
            int expectedTotalMs)
        {
            // 이 예산이 고정하는 관계는 "총액 = 이 체인이 프로브 하나에
            // 실제로 허용하는 시간"이다. 예전에는 총액을
            // AppSettings.Timing.ProcessTimeoutMs(15초)에 못 박아 뒀는데,
            // 정작 시작 경로는 5초를 넘기므로 예산이 재시도를 세 번이나
            // 허용해 아무것도 막지 못했다. 그래서 값을 다른 상수에 다시
            // 못 박으면(예: ProcessTimeoutMs) 이 [InlineData(5000, 5000)]이
            // 즉시 깨진다.
            var budget = ProbeRetryBudget.For(timeoutMs);

            Assert.Equal(TimeSpan.FromMilliseconds(expectedTotalMs), budget.Total);
            Assert.Equal(TimeSpan.Zero, budget.Spent);
            Assert.True(budget.HasRemaining);
        }

        [Fact]
        public void For_TotalTracksTheTimeoutTheStartupPathActuallyPasses()
        {
            // ApplicationHost가 넘기는 값이 바뀌면 예산도 따라가야 한다 -
            // 그 연결이 끊기는 순간(예산을 상수에 다시 못 박는 순간)이
            // 바로 리뷰가 잡아낸 결함이다.
            Assert.Equal(
                ProbeRetryBudget.EffectiveProbeTimeout(
                    ApplicationHost.AdbSelectionTimeoutMs),
                ProbeRetryBudget.For(ApplicationHost.AdbSelectionTimeoutMs).Total);
        }

        [Fact]
        public void Spend_ChargesTheWallClockTimeTheRetryActuallyTook()
        {
            var clock = new ManualClock();
            var budget = new ProbeRetryBudget(
                TimeSpan.FromSeconds(15),
                clock.Now);

            var returned = budget.Spend(delegate
            {
                clock.Advance(TimeSpan.FromSeconds(4));
                return "probe result";
            });

            Assert.Equal("probe result", returned);
            // 핵심: 청구 기준은 "재시도를 몇 번 했나"가 아니라 그 재시도가
            // 실제로 잡아먹은 벽시계 시간이다 - 부하가 걸리면 같은 횟수도
            // 훨씬 오래 걸리므로 횟수로 세면 상한이 무의미해진다.
            Assert.Equal(TimeSpan.FromSeconds(4), budget.Spent);
            Assert.Equal(TimeSpan.FromSeconds(11), budget.Remaining);
            Assert.True(budget.HasRemaining);
        }

        [Fact]
        public void Spend_UsesUpTheWholeBudget_LeavesNothingForAnotherRetry()
        {
            var clock = new ManualClock();
            var budget = new ProbeRetryBudget(
                TimeSpan.FromSeconds(15),
                clock.Now);

            budget.Spend(delegate
            {
                clock.Advance(TimeSpan.FromSeconds(15));
                return 0;
            });

            Assert.False(budget.HasRemaining);
            // 초과분이 음수 Remaining으로 새어 나가지 않아야 한다 -
            // 호출자는 Remaining을 "앞으로 쓸 수 있는 시간"으로 읽는다.
            Assert.Equal(TimeSpan.Zero, budget.Remaining);
        }

        [Fact]
        public void Spend_RetryThrows_StillChargesTheTimeItBurned()
        {
            var clock = new ManualClock();
            var budget = new ProbeRetryBudget(
                TimeSpan.FromSeconds(15),
                clock.Now);

            Assert.Throws<InvalidOperationException>(() => budget.Spend<int>(delegate
            {
                clock.Advance(TimeSpan.FromSeconds(15));
                throw new InvalidOperationException("probe blew up");
            }));

            // 예외로 끝난 재시도도 시작을 늦춘 것은 마찬가지다. 여기서
            // 청구를 건너뛰면 프로브가 예외로 끝날수록 예산이 새어 나가
            // 상한이 무너진다.
            Assert.Equal(TimeSpan.FromSeconds(15), budget.Spent);
            Assert.False(budget.HasRemaining);
        }

        [Fact]
        public void Spend_ClockGoesBackwards_DoesNotRefundTheBudget()
        {
            var clock = new ManualClock();
            var budget = new ProbeRetryBudget(
                TimeSpan.FromSeconds(15),
                clock.Now);

            budget.Spend(delegate
            {
                clock.Advance(TimeSpan.FromSeconds(15));
                return 0;
            });
            // NTP 보정 등으로 시스템 시계가 뒤로 갈 수 있다. 음수 경과를
            // 그대로 더하면 이미 다 쓴 예산이 되살아나 상한이 무의미해진다.
            budget.Spend(delegate
            {
                clock.Advance(TimeSpan.FromSeconds(-10));
                return 0;
            });

            Assert.Equal(TimeSpan.FromSeconds(15), budget.Spent);
            Assert.False(budget.HasRemaining);
        }
    }

    /// <summary>
    /// 테스트가 직접 앞당기는 시계. 느린 프로브는 "probe 델리게이트 안에서
    /// 시계를 앞당긴다"로 흉내 낸다 - LockStatePollTests가 delay 콜백에서
    /// 쓰는 방식과 같다.
    /// </summary>
    internal sealed class ManualClock
    {
        private DateTime _current = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public DateTime Now() => _current;

        public void Advance(TimeSpan by) => _current += by;
    }
}
