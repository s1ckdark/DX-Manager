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
    ///
    /// <paramref name="budget"/>은 실제 경과 시간(벽시계) 기준이다 -
    /// 고정된 반복 횟수가 아니다. 이전 버전은 budget/interval을 미리
    /// 나눠 반복 횟수만 고정했는데, probe 한 번(AdbService.
    /// IsDeviceLocked)이 자체적으로 최대 AppSettings.ProcessTimeoutMs
    /// (기본 15000ms)까지 걸릴 수 있다 - probe 자체가 그 자릿수로
    /// 느려지면 "2000ms 예산"이 실제로는 "최대 13번 더" 프로브를
    /// 허용해, 아무 응답이 없는 adb에서는 최대 ~14 * 15초까지 벌어질 수
    /// 있었다(기존 단발 확인의 최대 15초 대비 14배 회귀). 이제는 매
    /// 루프 진입 전에 남은 시간을 직접 재는다 - probe 자체가 오래
    /// 걸리면 그만큼 예산이 실제로 줄어들고, 다음 재확인은 시도조차
    /// 안 한다.
    ///
    /// probe 한 번의 자체 타임아웃(현재는 AdbService의 전역
    /// ProcessTimeoutMs)을 "남은 예산"에 맞춰 매번 줄이는 건 하지 않는다
    /// - 그러려면 IsDeviceLocked에 타임아웃 오버라이드를 새로 뚫어야
    /// 하는데, 이 폴 하나를 위해 공유 서비스의 공개 표면을 넓히는 건
    /// 잘못된 계층이라고 판단했다. 이 벽시계 수정만으로도 추가 노출은
    /// probe 하나의 기존 타임아웃 상한(~15초)으로 되돌아온다 - 폴로
    /// 바꾸기 전 원래 코드의 단발 확인과 같은 자릿수이지 그보다 나빠지지
    /// 않는다.
    /// </summary>
    public static class LockStatePoll
    {
        public static LockState Until(
            Func<LockState> probe,
            TimeSpan budget,
            TimeSpan interval,
            Action<TimeSpan> delay = null,
            Func<DateTime> utcNow = null)
        {
            if (probe == null) throw new ArgumentNullException("probe");
            if (interval <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException("interval");
            var wait = delay ?? Thread.Sleep;
            var clock = utcNow ?? (() => DateTime.UtcNow);

            var result = probe();
            if (result != LockState.Locked) return result;

            // 첫 확인은 이미 위에서 썼다 - 여기서부터 실제 경과 시간이
            // 예산을 넘길 때까지만 재확인한다. probe 자체가 느려지면
            // clock()이 그만큼 더 나가 있으므로 다음 재확인 기회 자체가
            // 줄어든다 - 고정 반복 횟수가 아니라 실제 남은 시간이
            // 기준이다.
            var deadline = clock() + budget;
            while (clock() < deadline)
            {
                wait(interval);
                result = probe();
                if (result != LockState.Locked) return result;
            }
            return result;
        }
    }
}
