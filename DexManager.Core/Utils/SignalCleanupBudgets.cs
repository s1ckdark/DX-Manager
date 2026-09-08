using System;
using System.Runtime.InteropServices;

namespace DexManager.Utils
{
    /// <summary>
    /// SIGINT/SIGTERM/SIGHUP 신호 처리기(각 프론트엔드의 Program.cs)가
    /// 정리(DisposeQuietly/Shutdown)를 얼마나 기다릴지 고른다. 하나의
    /// 예산으로는 두 성격이 다른 요구를 동시에 만족시킬 수 없다.
    ///
    /// SIGINT는 터미널에서 Ctrl+C로 오는 대화형 신호다 - 사용자가 화면을 보고
    /// 있고 "지금 멈춰라"라는 기대가 있으므로 짧게 유지한다.
    ///
    /// SIGTERM/SIGHUP은 launchd, `pkill -TERM`, 제어 터미널이 끊길 때(SIGHUP)처럼
    /// 아무도 실시간으로 지켜보지 않는 경로다. 실제 정리 사슬은 adb 호출을
    /// 포함하는데, 단일 adb 호출 하나가 <c>AppSettings.ProcessTimeoutMs</c>
    /// (기본 15000ms)까지 걸릴 수 있다고 이 앱의 다른 모든 곳이 이미 인정하고
    /// 있다 - 신호 처리기만 그보다 짧게 잘라내면, 정상적인 단일 호출조차
    /// 인위적으로 실패시켜 overlay를 흘리게 된다(부하가 걸린 실행에서 재현:
    /// .omc/research/settle-poll-report.md). 그래서 비대화형 예산은 그 상한과
    /// 맞춘다.
    ///
    /// macOS는 SIGTERM을 SIGKILL로 자동 승격하지 않는다(그 결정은 신호를 보낸
    /// 쪽의 몫이다) - 그래서 "더 오래 기다리면 더 거칠게 죽는다"는 근거로
    /// 예산을 짧게 유지할 필요는 없다. 반대로 지나치게 길게 기다릴 이유도
    /// 없다: overlay 회수가 이 예산을 넘겨 실패해도 다음 DeX 시작이 남은
    /// overlay를 그대로 발견해 재사용하므로(VirtualDisplayService.
    /// EnsureVirtualDisplay), 유일한 비용은 그 사이 창이 하나 남아 있는
    /// 것뿐이다 - 병적으로 참을성 있게 기다릴 이유가 되지는 못한다.
    /// </summary>
    public static class SignalCleanupBudgets
    {
        public static readonly TimeSpan Interactive = TimeSpan.FromSeconds(5);
        public static readonly TimeSpan NonInteractive = TimeSpan.FromSeconds(15);

        public static TimeSpan For(PosixSignal signal) =>
            signal == PosixSignal.SIGINT ? Interactive : NonInteractive;
    }
}
