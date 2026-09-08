using System;
using DexManager.Models;
using DexManager.Utils;
using Xunit;

namespace DexManager.Tests
{
    /// <summary>
    /// 후보 프로브 재시도가 체인 전체에서 쓸 수 있는 시간 예산
    /// (<see cref="ProbeRetryBudget"/>)의 성질만 고정한다. "누가 언제
    /// 재시도하는가"는 TransientProbeRetryTests가, "PathService가 체인
    /// 하나당 예산 하나를 쓰는가"는 PathServiceProbeBudgetTests가 맡는다.
    ///
    /// 시간은 <see cref="ManualClock"/>으로만 흘린다 - 실제로 기다리는
    /// 테스트는 느린 데다 부하가 걸린 CI에서 그 자체가 새 flake가 된다
    /// (이 브랜치가 고치려는 결함이 정확히 "부하 때문에 시간이 예상보다
    /// 오래 걸린다"는 것이므로 더더욱).
    /// </summary>
    public class ProbeRetryBudgetTests
    {
        [Fact]
        public void Default_IsExactlyWhatASingleAdbCallIsAllowedToTake()
        {
            // 이 예산이 주장하는 성질은 "체인 전체의 재시도를 다 합쳐도
            // adb 호출 한 번보다 더 기다리지는 않는다"이다. 여기서
            // 15초를 그대로 베껴 적으면 누가 ProcessTimeoutMs를 올려도
            // 이 테스트는 그 사실을 모른 채 계속 통과하고, 이름이
            // 주장하는 관계만 조용히 깨진다 - 그래서 값이 아니라 상수를
            // 읽어 관계를 고정한다(SignalCleanupBudgetsTests와 같은 이유).
            var singleAdbCall = TimeSpan.FromMilliseconds(
                AppSettings.CreateDefault().Timing.ProcessTimeoutMs);

            Assert.Equal(singleAdbCall, ProbeRetryBudget.Default);
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
