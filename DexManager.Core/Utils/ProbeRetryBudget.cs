using System;

namespace DexManager.Utils
{
    /// <summary>
    /// adb 후보 프로브의 <b>재시도</b>가 체인 전체에서 쓸 수 있는 시간
    /// 예산. <see cref="TransientProbeRetry"/>의 재시도 횟수 상한은 후보
    /// <i>하나</i>에 대해서만 참이다 - PathService의 자동 선택은 후보를
    /// 순서대로(scrcpy 옆 adb → IPathProvider 후보 전부 → Win7 → PATH)
    /// 프로브하므로, 후보 N개가 모두 부하로 타임아웃하면 재시도까지
    /// 곱해져 시작이 창도 못 띄운 채 2N번의 타임아웃만큼 멈춘다. 그
    /// 곱셈을 끊는 것이 이 예산의 유일한 목적이다.
    ///
    /// <para>
    /// 총액은 <b>이 선택 호출이 후보 프로브 하나에 실제로 허용하는 시간</b>
    /// (<see cref="EffectiveProbeTimeout"/>)과 같다. 프로브를 거는 쪽과
    /// 예산을 만드는 쪽이 같은 함수를 쓰므로 둘이 갈라질 수 없다. 예전에는
    /// 예산을 <c>AppSettings.Timing.ProcessTimeoutMs</c>(15초)에 고정했는데,
    /// 정작 시작 경로인 <c>ApplicationHost</c>는 5초를 넘긴다 - 재시도 한
    /// 번이 5.3초만 깎으니 15초 예산은 재시도 세 번을 허용했고, 후보 셋짜리
    /// 체인에서는 예산이 아예 걸리지 않아 고친 것이 아무 효과가 없었다.
    /// 그래서 값을 고정하지 않고 <b>넘어온 timeoutMs와의 관계</b>로 정한다.
    /// </para>
    /// <para>
    /// 여기서 보장하는 것: <b>체인 전체의 재시도가 쓰는 시간은 프로브
    /// 타임아웃 하나 + 마지막 재시도 하나를 넘지 않는다.</b> 예산이 ε만
    /// 남았을 때도 재시도 하나는 시작되고 그것이 프로브 타임아웃을 꽉 채울
    /// 수 있으므로 최악은 <c>2 × 예산 − ε</c>이다. 남은 예산이 이번 재시도를
    /// 끝까지 감당할지는 프로브 소요를 미리 아는 것과 같아 알 수 없다 -
    /// 초과가 O(1)로(재시도 하나만큼) 묶인다는 것이 보장의 전부이고,
    /// "다 합쳐도 프로브 하나보다 덜 기다린다"는 더 센 주장은 하지 않는다.
    /// </para>
    /// <para>
    /// 예산은 <b>재시도만</b> 제한한다. 각 후보의 <b>첫 시도는 예산과
    /// 무관하게 항상 실행</b>한다 - 첫 시도를 예산으로 자르면 "앞 후보가
    /// 느려서 예산을 다 썼다"는 이유로 정말 살아 있는 뒤쪽 후보를 아예
    /// 시도조차 못 하게 되어, 후보를 여러 개 두는 fail-open 설계 자체가
    /// 무너진다. 그래서 최악은 (N × 단일 프로브 타임아웃) + (이 예산만큼의
    /// 재시도)로 줄어든다. 앞의 N배 항까지 없애려면 후보를 병렬로
    /// 프로브해야 하는데 그건 별개의 변경이다 - 여기서 주장하는 상한은
    /// 딱 뒤쪽 항까지다.
    /// </para>
    /// <para>
    /// 시계는 반복 횟수가 아니라 <b>wall-clock</b>이다(LockStatePoll
    /// 회귀에서 얻은 교훈: "몇 번 돌았나"는 부하가 걸리면 실제 경과
    /// 시간과 아무 관계가 없다). 테스트에서 결정적으로 만들 수 있도록
    /// <c>utcNow</c>를 주입받는다 - 시간을 실제로 흘려보내는 테스트는
    /// 쓰지 않는다.
    /// </para>
    /// </summary>
    public sealed class ProbeRetryBudget
    {
        /// <summary>
        /// 후보 프로브 하나에 허용하는 최소 시간. 호출자가 이보다 짧은
        /// 타임아웃을 넘겨도 부하가 걸린 기기에서 adb가 뜨는 시간조차
        /// 안 되므로 여기까지 올려 잡는다.
        /// </summary>
        public const int MinimumProbeTimeoutMs = 3000;

        /// <summary>
        /// 이 선택 호출에서 후보 프로브 하나가 실제로 쓸 수 있는 시간.
        /// <b>프로브를 거는 쪽(PathService.GetRunnableCandidate)과 예산을
        /// 만드는 쪽이 반드시 이 함수 하나만 쓴다</b> - 두 값이 갈라지면
        /// 예산이 실제 프로브보다 커져 재시도를 여러 번 허용하고, 그러면
        /// 예산이 아무것도 막지 못한다(정확히 그 일이 예산을 15초로
        /// 고정했을 때 일어났다).
        /// </summary>
        public static int EffectiveProbeTimeoutMs(int timeoutMs)
        {
            return Math.Max(timeoutMs, MinimumProbeTimeoutMs);
        }

        /// <inheritdoc cref="EffectiveProbeTimeoutMs"/>
        public static TimeSpan EffectiveProbeTimeout(int timeoutMs)
        {
            return TimeSpan.FromMilliseconds(EffectiveProbeTimeoutMs(timeoutMs));
        }

        /// <summary>
        /// 후보 체인 하나가 재시도에 쓸 예산을 만든다. 총액은 그 체인의
        /// 프로브 타임아웃 하나와 같다 - 호출자가 어떤 timeoutMs를 넘기든
        /// 이 관계는 유지되므로, 시작 경로가 넘기는 값이 바뀌어도 예산은
        /// 저절로 따라간다.
        /// </summary>
        public static ProbeRetryBudget For(
            int timeoutMs,
            Func<DateTime> utcNow = null)
        {
            return new ProbeRetryBudget(EffectiveProbeTimeout(timeoutMs), utcNow);
        }

        private readonly Func<DateTime> _utcNow;
        private readonly TimeSpan _total;
        private TimeSpan _spent;

        public ProbeRetryBudget(TimeSpan total, Func<DateTime> utcNow = null)
        {
            _total = total > TimeSpan.Zero ? total : TimeSpan.Zero;
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        public TimeSpan Total { get { return _total; } }

        public TimeSpan Spent { get { return _spent; } }

        public TimeSpan Remaining
        {
            get
            {
                var remaining = _total - _spent;
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
        }

        /// <summary>
        /// 재시도를 <i>시작</i>해도 되는지. 남은 예산이 이번 재시도를
        /// 끝까지 감당하는지는 묻지 않는다 - 그건 프로브가 얼마나 걸릴지
        /// 미리 아는 것과 같아서 알 수 없다. 대신 "예산이 남아 있으면 한
        /// 번 더 해본다, 그 대가는 다음 후보가 치른다"로 단순화한다.
        /// 클래스 주석이 말하는 <c>2 × 예산 − ε</c> 상한이 여기서 나온다.
        /// </summary>
        public bool HasRemaining { get { return Remaining > TimeSpan.Zero; } }

        /// <summary>
        /// 재시도 하나를 예산에 달아 실행한다. 재시도가 예외로 끝나도
        /// 소비한 시간은 그대로 청구한다 - 예외로 끝난 재시도도 시작을
        /// 늦춘 것은 마찬가지이므로 청구를 건너뛰면 예산이 새어 나간다.
        /// </summary>
        public T Spend<T>(Func<T> retry)
        {
            if (retry == null) throw new ArgumentNullException("retry");

            var startedAt = _utcNow();
            try
            {
                return retry();
            }
            finally
            {
                // 시스템 시계가 뒤로 갈 수 있으므로(NTP 보정) 음수는
                // 버린다 - 예산을 되돌려주는 쪽이 아니라 0으로 본다.
                var elapsed = _utcNow() - startedAt;
                if (elapsed > TimeSpan.Zero) _spent += elapsed;
            }
        }
    }
}
