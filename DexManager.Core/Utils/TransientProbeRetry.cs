using System;
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
    /// 도는 경로이므로 예산은 짧게 유지한다: 최대 시도 <see
    /// cref="MaxAttempts"/>번, 그 사이 짧은 간격(<see cref="Gap"/>)만
    /// 둔다. 시도 횟수를 늘리면 "정말로 죽은" 후보를 판정하는 데
    /// 걸리는 시간이 그만큼 배로 늘어 시작을 눈에 띄게 늦추므로,
    /// 부하 스파이크를 넘기기에 충분하되 과하지 않은 값을 고른다.
    ///
    /// 오직 <see cref="ProcessResult.TimedOut"/>만 "다시 해볼 가치가
    /// 있다"는 신호로 취급한다 - 파일이 있지만 실제로는 adb가 아니거나
    /// 실행 자체가 안 되는 등 타임아웃이 아닌 실패는 재시도해도 같은
    /// 결과가 나올 뿐이므로, 그런 실패는 즉시 반환해 불필요한 지연을
    /// 만들지 않는다.
    /// </summary>
    public static class TransientProbeRetry
    {
        public const int MaxAttempts = 2;
        public static readonly TimeSpan Gap = TimeSpan.FromMilliseconds(300);

        public static ProcessResult Run(
            Func<ProcessResult> probe,
            Action<TimeSpan> delay = null)
        {
            if (probe == null) throw new ArgumentNullException("probe");
            var wait = delay ?? Thread.Sleep;

            ProcessResult result = null;
            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                result = probe();
                if (!result.TimedOut) return result;
                if (attempt < MaxAttempts) wait(Gap);
            }
            return result;
        }
    }
}
