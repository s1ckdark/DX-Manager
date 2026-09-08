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
        /// 체인 하나가 재시도에 쓸 수 있는 기본 예산. 값은 이 앱이 단일
        /// adb 호출 하나를 기다릴 가치가 있다고 이미 인정한 상한
        /// (<c>AppSettings.Timing.ProcessTimeoutMs</c>, 기본 15000ms)과
        /// 맞춘다 - 즉 "체인 전체의 재시도를 다 합쳐도 adb 호출 한 번보다
        /// 더 기다리지는 않는다"가 여기서 주장하는 성질이다.
        /// <c>ProcessTimeoutMs</c>를 직접 읽지 않고 값을 적어 두는 것은
        /// SignalCleanupBudgets.Budget과 같은 이유다: 그래야 누가
        /// <c>ProcessTimeoutMs</c>를 바꿨을 때 관계를 고정한 테스트가
        /// 깨지면서 이 예산도 함께 다시 판단하게 된다(자동으로 따라가면
        /// 아무도 모르게 시작 지연 상한이 같이 늘어난다).
        /// </summary>
        public static readonly TimeSpan Default = TimeSpan.FromSeconds(15);

        private readonly Func<DateTime> _utcNow;
        private readonly TimeSpan _total;
        private TimeSpan _spent;

        public ProbeRetryBudget(TimeSpan total, Func<DateTime> utcNow = null)
        {
            _total = total > TimeSpan.Zero ? total : TimeSpan.Zero;
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        public ProbeRetryBudget(Func<DateTime> utcNow = null)
            : this(Default, utcNow)
        {
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
        /// 그래서 실제 초과분은 마지막 재시도 하나만큼이다.
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
