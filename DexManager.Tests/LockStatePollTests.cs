using System;
using System.Collections.Generic;
using DexManager.Models;
using DexManager.Utils;
using Xunit;

namespace DexManager.Tests
{
    /// <summary>
    /// DexOrchestrator.StartCore가 DismissKeyguard 직후 쓰던 고정 대기
    /// (DismissKeyguardSettleDelayMs) 대신 쓰는 재확인 정책만 고정한다.
    /// 실제 adb 프로브는 다루지 않는다 - 가짜 델리게이트로 결과와 호출
    /// 횟수만 검증한다. 실제 배선(DexOrchestrator가 이 정책을 실제로
    /// 쓰는지, FakeAdbExecutable로 잠김->해제 전환을 흉내낼 때도
    /// 동작하는지)은 DexOrchestratorWakeTests 쪽 통합 테스트가 맡는다.
    ///
    /// fail-open이 이 정책의 핵심 불변식이다: <see cref="LockState.Unknown"/>
    /// 은 "아직 모른다"가 아니라 "이미 판단이 끝났다"로 취급해 즉시
    /// 멈춰야 한다 - Locked만 "다시 확인할 가치가 있다"는 신호다.
    /// </summary>
    public class LockStatePollTests
    {
        [Fact]
        public void Until_FirstProbeReportsUnlocked_ReturnsImmediatelyWithoutWaiting()
        {
            var callCount = 0;
            var delayCalls = new List<TimeSpan>();

            var result = LockStatePoll.Until(
                delegate { callCount++; return LockState.Unlocked; },
                TimeSpan.FromMilliseconds(300),
                TimeSpan.FromMilliseconds(100),
                delayCalls.Add);

            Assert.Equal(LockState.Unlocked, result);
            Assert.Equal(1, callCount);
            Assert.Empty(delayCalls);
        }

        [Fact]
        public void Until_FirstProbeReportsUnknown_ReturnsImmediatelyWithoutWaiting()
        {
            // fail-open의 핵심: Unknown은 Locked와 달리 "다시 볼 가치가
            // 있는" 신호가 아니다 - 판정 자체가 애매하다는 뜻이므로 즉시
            // 통과 판단으로 넘어가야 한다. 계속 재확인하면 판단이 애매한
            // 기기의 시작을 불필요하게 늦추게 된다.
            var callCount = 0;

            var result = LockStatePoll.Until(
                delegate { callCount++; return LockState.Unknown; },
                TimeSpan.FromMilliseconds(300),
                TimeSpan.FromMilliseconds(100));

            Assert.Equal(LockState.Unknown, result);
            Assert.Equal(1, callCount);
        }

        [Fact]
        public void Until_LockedTwiceThenUnlocked_PollsUntilUnlockedAndReturnsIt()
        {
            var callCount = 0;
            var delayCalls = new List<TimeSpan>();

            var result = LockStatePoll.Until(
                delegate
                {
                    callCount++;
                    return callCount <= 2 ? LockState.Locked : LockState.Unlocked;
                },
                TimeSpan.FromMilliseconds(300),
                TimeSpan.FromMilliseconds(100),
                delayCalls.Add);

            Assert.Equal(LockState.Unlocked, result);
            // 핵심 단언: "끝까지 잠긴 채 예산을 다 썼다"와 "재확인 끝에
            // 풀린 걸 관측했다"는 최종 결과값만 보면 구분되지 않을 수
            // 있다(둘 다 Locked가 아닌 값이 나올 수도 있으므로). 호출
            // 횟수와 그 사이 지연 횟수로 실제로 두 번 다시 확인했다는
            // 것을 증명한다.
            Assert.Equal(3, callCount);
            Assert.Equal(2, delayCalls.Count);
            Assert.All(
                delayCalls,
                delay => Assert.Equal(TimeSpan.FromMilliseconds(100), delay));
        }

        [Fact]
        public void Until_StaysLockedForTheWholeBudget_GivesUpAndReturnsLocked()
        {
            var callCount = 0;
            var delayCalls = new List<TimeSpan>();

            var result = LockStatePoll.Until(
                delegate { callCount++; return LockState.Locked; },
                TimeSpan.FromMilliseconds(300),
                TimeSpan.FromMilliseconds(100),
                delayCalls.Add);

            // fail-open은 "판단 시점"만 바꾼다 - 끝까지 Locked만 관측됐다면
            // (판단 자체가 애매하지 않았다면) 그 판정은 그대로 존중돼
            // Locked로 돌아와야 한다. 예산을 넘겼다고 Unknown으로
            // 바뀌지 않는다.
            Assert.Equal(LockState.Locked, result);
            // 300ms 예산 / 100ms 간격 = 첫 확인 이후 3번 더 - 무한정
            // 재시도하지 않고 정확히 예산만큼만 재확인했다는 증거.
            Assert.Equal(4, callCount);
            Assert.Equal(3, delayCalls.Count);
        }

        [Fact]
        public void Until_NullProbe_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => LockStatePoll.Until(
                null,
                TimeSpan.FromMilliseconds(300),
                TimeSpan.FromMilliseconds(100)));
        }

        [Fact]
        public void Until_NonPositiveInterval_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => LockStatePoll.Until(
                () => LockState.Locked,
                TimeSpan.FromMilliseconds(300),
                TimeSpan.Zero));
        }
    }
}
