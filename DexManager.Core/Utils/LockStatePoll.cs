using System;
using System.Threading;
using DexManager.Models;

namespace DexManager.Utils
{
    /// <summary>
    /// DexOrchestrator.StartCore가 DismissKeyguard 직후 쓰던 고정 대기
    /// (DismissKeyguardSettleDelayMs)를 대신한다. 고정 대기의 문제는
    /// fail-*closed*였다: dismiss가 그 대기보다 느리게 반영되면 프로브가
    /// "아직 해제 중"인 기기를 Locked로 읽어 시작을 막았다 - 이미
    /// 풀리고 있는 폰에게 "먼저 잠금을 해제하라"고 잘못 안내하는 것과
    /// 같다.
    ///
    /// 이 폴은 판단 "시점"만 바꾼다 - 어느 쪽으로 판단할지는 절대 바꾸지
    /// 않는다(fail-open 규율은 호출자인 DexOrchestrator가 그대로 쥔다).
    /// <see cref="LockState.Locked"/>만 "다시 볼 가치가 있는" 신호다 -
    /// 계속 잠긴 채면 아직 해제 중일 수 있으니 예산 안에서 다시 확인한다.
    /// <see cref="LockState.Unlocked"/>와 <see cref="LockState.Unknown"/>은
    /// 둘 다 "이미 판단이 끝났다"는 뜻이라(하나는 확실히 풀림, 하나는
    /// 애매해서 fail-open) 즉시 멈춘다 - Unknown을 계속 재확인하면 판단이
    /// 애매한 기기의 시작만 불필요하게 늦춘다.
    /// </summary>
    public static class LockStatePoll
    {
        public static LockState Until(
            Func<LockState> probe,
            TimeSpan budget,
            TimeSpan interval,
            Action<TimeSpan> delay = null)
        {
            if (probe == null) throw new ArgumentNullException("probe");
            if (interval <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException("interval");
            var wait = delay ?? Thread.Sleep;

            var result = probe();
            if (result != LockState.Locked) return result;

            // 첫 확인은 이미 위에서 썼다 - 남은 예산만큼만 더 재확인한다.
            // 매 확인이 adb 왕복이므로(dumpsys trust, 필요하면 dumpsys
            // window까지) 무한정 도는 대신 정확히 이만큼만 돈다.
            var additionalPolls = (int)(budget.Ticks / interval.Ticks);
            for (var i = 0; i < additionalPolls; i++)
            {
                wait(interval);
                result = probe();
                if (result != LockState.Locked) return result;
            }
            return result;
        }
    }
}
