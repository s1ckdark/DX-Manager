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
