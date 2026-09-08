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
    /// 동작하는지)은 DexOrchestratorDismissKeyguardPollTests 쪽 통합
    /// 테스트가 맡는다.
    ///
    /// fail-open이 이 정책의 핵심 불변식이다: <see cref="LockState.Unknown"/>
    /// 은 "아직 모른다"가 아니라 "이미 판단이 끝났다"로 취급해 즉시
    /// 멈춰야 한다 - Locked만 "다시 확인할 가치가 있다"는 신호다.
    ///
    /// <see cref="FakeClock"/>을 쓰는 테스트들은 "실제 벽시계 경과 시간"이
    /// 멈추는 기준이라는 것 자체를 고정한다 - delay 콜백이 시계를
    /// 간격만큼 앞당기므로, 진짜 Thread.Sleep 없이도 결정적이고 빠르게
    /// 검증할 수 있다.
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
            var clock = new FakeClock();

            var result = LockStatePoll.Until(
                delegate
                {
                    callCount++;
                    return callCount <= 2 ? LockState.Locked : LockState.Unlocked;
                },
                TimeSpan.FromMilliseconds(300),
                TimeSpan.FromMilliseconds(100),
                delay: delegate(TimeSpan ts)
                {
                    delayCalls.Add(ts);
                    clock.Advance(ts);
                },
                utcNow: clock.Now);

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
            var clock = new FakeClock();

            var result = LockStatePoll.Until(
                delegate { callCount++; return LockState.Locked; },
                TimeSpan.FromMilliseconds(300),
                TimeSpan.FromMilliseconds(100),
                delay: delegate(TimeSpan ts)
                {
                    delayCalls.Add(ts);
                    clock.Advance(ts);
                },
                utcNow: clock.Now);

            // fail-open은 "판단 시점"만 바꾼다 - 끝까지 Locked만 관측됐다면
            // (판단 자체가 애매하지 않았다면) 그 판정은 그대로 존중돼
            // Locked로 돌아와야 한다. 예산을 넘겼다고 Unknown으로
            // 바뀌지 않는다.
            Assert.Equal(LockState.Locked, result);
            // 300ms 예산 / 100ms 간격 = 첫 확인 이후 3번 더(시계가 정확히
            // 간격만큼만 앞당겨지는 경우) - 무한정 재시도하지 않고
            // 예산만큼만 재확인했다는 증거.
            Assert.Equal(4, callCount);
            Assert.Equal(3, delayCalls.Count);
        }

        [Fact]
        public void Until_ProbeItselfTakesLongerThanTheInterval_StopsOnElapsedBudgetNotIterationCount()
        {
            // 이전 버전의 결함: budget/interval을 미리 나눠 "반복 횟수"로만
            // 썼다 - probe 자체가 느려지는 건 전혀 반영하지 않았다. 여기서
            // probe 한 번이 간격(100ms)보다 훨씬 오래(400ms) 걸린다고
            // 흉내낸다. 반복 횟수 기준이었다면 예산(1000ms)/간격(100ms)=10
            // 번 더, 총 11번 불렸을 것이다. 실제 경과 시간 기준이라면
            // probe 자체가 시간을 다 태우므로 훨씬 적게 불려야 한다.
            var callCount = 0;
            var clock = new FakeClock();

            var result = LockStatePoll.Until(
                delegate
                {
                    callCount++;
                    // probe 자체의 소요 시간을 흉내낸다 - 간격 대기와
                    // 별개로 시계를 직접 앞당긴다.
                    clock.Advance(TimeSpan.FromMilliseconds(400));
                    return LockState.Locked;
                },
                TimeSpan.FromMilliseconds(1000),
                TimeSpan.FromMilliseconds(100),
                delay: delegate { },
                utcNow: clock.Now);

            Assert.Equal(LockState.Locked, result);
            // 첫 확인(0ms 시점, probe가 400ms 소모) + 루프 진입 전 확인
            // (400 < 1000, 재확인 - probe가 800ms까지 소모) + 재확인
            // (800 < 1000, 재확인 - probe가 1200ms까지 소모) + 재확인
            // (1200 < 1000? 거짓, 종료) = 총 4번. "반복 횟수" 방식이었다면
            // 11번이 나왔을 것이다 - 그 차이가 이 테스트의 핵심 증거다.
            Assert.Equal(4, callCount);
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

        private sealed class FakeClock
        {
            private DateTime _current = DateTime.UtcNow;

            public DateTime Now() => _current;

            public void Advance(TimeSpan by) => _current += by;
        }
    }
}
