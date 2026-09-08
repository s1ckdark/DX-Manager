using System;
using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Threading;
using DexManager.Models;

namespace DexManager.Utils
{
    /// <summary>
    /// PathService.GetRunnableCandidate의 "version" 프로브를 부하로 인한
    /// 일시적 타임아웃에 견디게 한다. 실기와 무관한 순수 재시도 정책만
    /// 다룬다 - 실제 adb 프로세스는 이 클래스 밖에서 만든다.
    ///
    /// 시작 시점(ApplicationHost 생성 중, 창이 뜨기 전)에 동기적으로
    /// 도는 경로이므로 예산은 짧게 유지한다: 후보 하나당 최대 시도
    /// <see cref="MaxAttempts"/>번, 그 사이 짧은 간격(<see cref="Gap"/>)만
    /// 둔다. 시도 횟수를 늘리면 "정말로 죽은" 후보를 판정하는 데
    /// 걸리는 시간이 그만큼 배로 늘어 시작을 눈에 띄게 늦추므로,
    /// 부하 스파이크를 넘기기에 충분하되 과하지 않은 값을 고른다.
    ///
    /// <para>
    /// 이 횟수 상한은 후보 <b>하나</b>에 대해서만 상한이다 - 후보를
    /// 순서대로 프로브하는 체인 전체로 보면 재시도가 후보 수만큼 곱해져
    /// 시작 지연이 배로 늘어난다. 그래서 체인 수준의 상한은 호출자가
    /// 넘기는 <see cref="ProbeRetryBudget"/>이 따로 맡는다: 예산이
    /// 남아 있지 않으면 <b>재시도만</b> 건너뛴다. 각 후보의 첫 시도는
    /// 예산과 무관하게 언제나 실행하므로, 앞 후보가 느렸다는 이유로
    /// 살아 있는 뒤쪽 후보를 못 찾는 일은 생기지 않는다.
    /// </para>
    ///
    /// 결과가 돌아온 경우에는 오직 <see cref="ProcessResult.TimedOut"/>만
    /// "다시 해볼 가치가 있다"는 신호로 취급한다 - 파일이 있지만 실제로는
    /// adb가 아니거나 실행 자체가 안 되는 등 타임아웃이 아닌 실패는
    /// 재시도해도 같은 결과가 나올 뿐이므로, 그런 실패는 즉시 반환해
    /// 불필요한 지연을 만들지 않는다.
    ///
    /// <para>
    /// 결과가 아예 돌아오지 않고 <b>예외</b>로 끝나는 일시 실패도 있다:
    /// 부하가 걸리면 fork/exec 자체가 EAGAIN(Win32Exception,
    /// "Resource temporarily unavailable")으로 실패할 수 있는데, 이는
    /// 타임아웃과 똑같이 잠깐 뒤에 다시 하면 되는 현상이다. 그 전에는
    /// 이 예외가 호출자의 catch에 그대로 걸려 영구 실패("실행 불가")로
    /// 기록되고 후보가 조용히 탈락했다. 재시도 대상은 <see
    /// cref="IsTransient"/>가 <b>명시적으로 열거한</b> 것뿐이다 - 집합을
    /// 넓히면 진짜 영구 실패(파일 없음, 권한 없음)를 재시도하느라 시작만
    /// 늦어진다.
    /// </para>
    /// </summary>
    public static class TransientProbeRetry
    {
        public const int MaxAttempts = 2;
        public static readonly TimeSpan Gap = TimeSpan.FromMilliseconds(300);

        /// <summary>
        /// EAGAIN("Resource temporarily unavailable") - 부하로 fork/exec가
        /// 일시 실패할 때의 errno. 값이 플랫폼마다 다르다: Linux 11,
        /// macOS 35. 이 앱은 둘 다에서 돌므로 둘 다 인정한다.
        /// </summary>
        public const int EagainLinux = 11;
        public const int EagainMac = 35;

        /// <summary>
        /// 프로브가 예외로 끝났을 때 "다시 해볼 가치가 있는" 예외인지.
        /// 열거된 것만 참이다 - ENOENT(2, 파일 없음), EACCES(13, 권한
        /// 없음) 같은 다른 Win32Exception이나 그 밖의 예외는 재시도해도
        /// 같은 결과이므로 즉시 전파해 다음 후보로 넘어가게 한다.
        /// </summary>
        public static bool IsTransient(Exception error)
        {
            var win32 = error as Win32Exception;
            if (win32 == null) return false;
            return win32.NativeErrorCode == EagainLinux ||
                   win32.NativeErrorCode == EagainMac;
        }

        /// <param name="budget">
        /// 체인 전체가 공유하는 재시도 예산. <c>null</c>이면 횟수 상한만
        /// 적용된다(예산을 공유할 체인이 없는 단독 호출).
        /// </param>
        /// <param name="retrySkipped">
        /// 예산이 없어 재시도를 건너뛸 때 한 번 호출된다. 호출자가 그
        /// 사실을 로그로 남길 수 있게 하기 위한 것이다 - 그렇지 않으면
        /// "왜 이 후보만 한 번밖에 안 해봤나"를 사용자가 알아낼 방법이
        /// 없다.
        /// </param>
        public static ProcessResult Run(
            Func<ProcessResult> probe,
            ProbeRetryBudget budget = null,
            Action<TimeSpan> delay = null,
            Action retrySkipped = null)
        {
            if (probe == null) throw new ArgumentNullException("probe");
            var wait = delay ?? Thread.Sleep;

            ProcessResult result = null;
            ExceptionDispatchInfo failure = null;
            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                // 첫 시도는 예산을 보지도, 쓰지도 않는다. 재시도만 예산의
                // 통제를 받는다.
                if (attempt > 1 && budget != null && !budget.HasRemaining)
                {
                    if (retrySkipped != null) retrySkipped();
                    break;
                }

                failure = null;
                try
                {
                    result = attempt == 1
                        ? probe()
                        : SpendOnRetry(budget, wait, probe);
                }
                catch (Exception ex)
                {
                    if (!IsTransient(ex)) throw;
                    failure = ExceptionDispatchInfo.Capture(ex);
                }

                if (failure == null && !result.TimedOut) return result;
            }

            // 재시도를 다 쓰고도 예외로 끝났다면 마지막 예외를 그대로
            // (스택을 잃지 않고) 던진다 - 호출자의 catch가 지금처럼 사유를
            // 로그하고 다음 후보로 넘어간다.
            if (failure != null) failure.Throw();
            return result;
        }

        /// <summary>
        /// 재시도 하나(간격 대기 + 프로브)를 예산에 달아 실행한다. 간격
        /// 대기도 시작을 늦추는 시간이므로 함께 청구한다.
        /// </summary>
        private static ProcessResult SpendOnRetry(
            ProbeRetryBudget budget,
            Action<TimeSpan> wait,
            Func<ProcessResult> probe)
        {
            if (budget == null)
            {
                wait(Gap);
                return probe();
            }

            return budget.Spend(delegate
            {
                wait(Gap);
                return probe();
            });
        }
    }
}
