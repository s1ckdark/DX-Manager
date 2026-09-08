using System;
using System.Collections.Generic;
using DexManager.Models;
using DexManager.Utils;
using Xunit;

namespace DexManager.Tests
{
    /// <summary>
    /// PathService.GetRunnableCandidate의 "version" 프로브가 부하로 인한
    /// 일시적 타임아웃 때문에 정상 후보를 죽었다고 오판하는 문제
    /// (.omc/research/settle-poll-report.md)의 재시도 정책만 고정한다.
    /// 실제 adb 프로세스는 다루지 않는다 - probe 대신 가짜 델리게이트를
    /// 넣어 결과와 호출 횟수만 검증한다.
    ///
    /// 타임아웃이 아닌 실패(파일이 있지만 실제로는 adb가 아니다, 버전
    /// 파싱이 깨졌다 등)는 재시도해도 같은 결과가 나올 뿐이므로 재시도
    /// 하지 않는다 - 오직 TimedOut만 "다시 해볼 가치가 있는" 신호로
    /// 취급한다.
    ///
    /// 여기에 더해, 횟수 상한이 후보 <b>하나</b>에 대한 상한일 뿐이라
    /// 후보를 줄줄이 프로브하는 체인에서는 재시도가 후보 수만큼 곱해진다.
    /// 그 곱셈을 끊는 공유 예산(ProbeRetryBudget)의 소비 규칙도 여기서
    /// 함께 고정한다 - 예산 자체의 성질은 ProbeRetryBudgetTests가 맡는다.
    /// </summary>
    public class TransientProbeRetryTests
    {
        [Fact]
        public void Run_FirstAttemptSucceeds_ReturnsItWithoutRetrying()
        {
            var callCount = 0;
            var success = SuccessResult();

            var result = TransientProbeRetry.Run(delegate
            {
                callCount++;
                return success;
            });

            Assert.Same(success, result);
            // "처음부터 성공"과 "재시도 끝에 성공"을 구분하는 핵심 단언 -
            // 결과값만 보면 둘 다 성공으로 보이므로, 호출 횟수로 실제로
            // 재시도가 없었음을 증명해야 한다.
            Assert.Equal(1, callCount);
        }

        [Fact]
        public void Run_FirstAttemptTimesOutThenSucceeds_RetriesExactlyOnceAndReturnsTheSuccess()
        {
            var callCount = 0;
            var timedOut = TimedOutResult();
            var success = SuccessResult();
            var delayCalls = new List<TimeSpan>();

            var result = TransientProbeRetry.Run(
                delegate
                {
                    callCount++;
                    return callCount == 1 ? timedOut : success;
                },
                delay: delayCalls.Add);

            Assert.Same(success, result);
            // 핵심 단언: 두 번째 호출까지 실제로 갔다는 증거. 이게 없으면
            // "결과가 성공이다"만으로는 재시도가 실제로 일어났는지, 아니면
            // 원래도 한 번에 성공했는지 구분할 수 없다.
            Assert.Equal(2, callCount);
            Assert.Single(delayCalls);
        }

        [Fact]
        public void Run_AllAttemptsTimeOut_StopsAtTheAttemptCapAndReturnsTheLastTimeout()
        {
            var callCount = 0;
            var timedOut = TimedOutResult();

            var result = TransientProbeRetry.Run(delegate
            {
                callCount++;
                return timedOut;
            });

            Assert.Same(timedOut, result);
            Assert.Equal(TransientProbeRetry.MaxAttempts, callCount);
        }

        [Fact]
        public void Run_FirstAttemptFailsWithoutTimingOut_ReturnsImmediatelyWithoutRetrying()
        {
            var callCount = 0;
            var nonZeroExit = NonZeroExitResult();

            var result = TransientProbeRetry.Run(delegate
            {
                callCount++;
                return nonZeroExit;
            });

            Assert.Same(nonZeroExit, result);
            // 타임아웃이 아닌 실패는 다시 해봤자 같은 결과다 - 재시도로
            // 시작 지연만 늘리고 얻는 게 없다.
            Assert.Equal(1, callCount);
        }

        [Fact]
        public void Run_NullProbe_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => TransientProbeRetry.Run(null));
        }

        [Fact]
        public void Run_SharedBudgetIsAlreadyUsedUp_StillRunsTheFirstAttemptButSkipsTheRetry()
        {
            var callCount = 0;
            var timedOut = TimedOutResult();
            var skipped = 0;
            var exhausted = new ProbeRetryBudget(TimeSpan.Zero);

            var result = TransientProbeRetry.Run(
                delegate
                {
                    callCount++;
                    return timedOut;
                },
                exhausted,
                delay: delegate { },
                retrySkipped: delegate { skipped++; });

            Assert.Same(timedOut, result);
            // 핵심: 예산이 없어도 첫 시도는 반드시 돈다. 첫 시도까지
            // 예산으로 자르면 앞 후보가 느렸다는 이유만으로 정말 살아
            // 있는 뒤쪽 후보를 아예 시도조차 못 하게 되어, 후보를 여러 개
            // 두는 fail-open 설계가 무너진다.
            Assert.Equal(1, callCount);
            // 건너뛴 사실은 호출자에게 알려야 한다 - 그래야 "왜 이 후보만
            // 한 번만 시도했나"가 로그로 남는다.
            Assert.Equal(1, skipped);
        }

        [Fact]
        public void Run_RetryChargesTheSharedBudget_SoTheNextCandidateOnlyGetsItsFirstAttempt()
        {
            // 체인을 흉내 낸다: 예산 하나를 후보 둘이 나눠 쓴다. 첫 후보의
            // 재시도가 예산을 다 쓰면 둘째 후보는 첫 시도만 받는다 -
            // 이것이 "후보 N개 × 재시도"라는 곱셈을 끊는 성질이다.
            var clock = new ManualClock();
            var budget = new ProbeRetryBudget(
                TimeSpan.FromSeconds(15),
                clock.Now);

            var firstCandidateCalls = 0;
            var firstResult = TransientProbeRetry.Run(
                delegate
                {
                    firstCandidateCalls++;
                    // 부하가 걸린 프로브 하나가 타임아웃 상한을 꽉 채운다.
                    clock.Advance(TimeSpan.FromSeconds(15));
                    return TimedOutResult();
                },
                budget,
                delay: delegate { });

            Assert.True(firstResult.TimedOut);
            // 첫 후보는 예산이 남아 있었으므로 재시도까지 받는다.
            Assert.Equal(2, firstCandidateCalls);

            var secondCandidateCalls = 0;
            var skipped = 0;
            TransientProbeRetry.Run(
                delegate
                {
                    secondCandidateCalls++;
                    clock.Advance(TimeSpan.FromSeconds(15));
                    return TimedOutResult();
                },
                budget,
                delay: delegate { },
                retrySkipped: delegate { skipped++; });

            Assert.Equal(1, secondCandidateCalls);
            Assert.Equal(1, skipped);
        }

        private static ProcessResult SuccessResult()
        {
            return new ProcessResult
            {
                ExitCode = 0,
                TimedOut = false,
                Canceled = false,
                StandardOutput = "1.0.41",
                StandardError = string.Empty
            };
        }

        private static ProcessResult TimedOutResult()
        {
            return new ProcessResult
            {
                ExitCode = -1,
                TimedOut = true,
                Canceled = false,
                StandardOutput = string.Empty,
                StandardError = string.Empty
            };
        }

        private static ProcessResult NonZeroExitResult()
        {
            return new ProcessResult
            {
                ExitCode = 1,
                TimedOut = false,
                Canceled = false,
                StandardOutput = string.Empty,
                StandardError = "not adb"
            };
        }
    }
}
