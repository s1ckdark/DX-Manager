using System;
using System.Runtime.InteropServices;

namespace DexManager.Utils
{
    /// <summary>
    /// SIGINT/SIGTERM/SIGHUP 신호 처리기(각 프론트엔드의 Program.cs)가
    /// 정리(DisposeQuietly/Shutdown)를 얼마나 기다릴지 고른다. 세 신호
    /// 모두 <b>같은</b> 예산을 쓴다.
    ///
    /// <para>
    /// 예전에는 SIGINT(대화형)만 5초로 짧게, SIGTERM/SIGHUP(비대화형)은
    /// 실제 adb 정리 사슬에 맞춰 15초로 길게 뒀다 - "화면 앞에 사용자가
    /// 있으니 SIGINT는 짧게"라는 근거였다. 그 구분의 전제 둘 다 이제
    /// 깨졌다:
    /// </para>
    /// <list type="number">
    /// <item>5초라는 SIGINT 예산은 SIGINT가 신호 처리기 안에서 실제로
    /// 정리를 실행하지 않던 시절(<c>ctx.Cancel = true</c>로 기본 종료를
    /// 막고 토큰만 취소하던 시절, DexManager.Mac에서는 이 guard 경로
    /// 자체가 도달 불가능했다)에 세운 값이었다. 지금은 SIGINT도
    /// SIGTERM/SIGHUP과 똑같이 이 예산 안에서 <c>host.Shutdown()</c>을
    /// 직접, 동기적으로 실행한다.</item>
    /// <item>5초라는 숫자 자체가 유휴 상태에서 측정한 ~2초 정리 시간에
    /// 여유를 더한 것이었다. 부하가 걸린 상태의 실제 정리 사슬
    /// (DeviceMonitor 정지/해제 → keyboard → StopAll →
    /// Dex.ShutdownAsync = scrcpy 종료 + <c>settings put global
    /// overlay_display_devices null</c> + 화면 전원 복구 + settle)은
    /// 6초 이상, 관측된 범위로는 8~18초까지 걸릴 수 있다 - 애초에
    /// SIGTERM/SIGHUP 쪽 예산이 (같은 이유로) 15초까지 올라간 것과 같은
    /// 근거다. 5초로 자르면 <c>--dex</c> 실행 중 사용자가 가장 흔히
    /// 보내는 신호(Ctrl+C)에서 정리가 중간에 끊기고
    /// <c>overlay_display_devices</c>가 그대로 남는다 - overlay 누수를
    /// 막으려고 신호 처리기를 도입한 커밋(SIGTERM/SIGHUP 배선)이 막으려던
    /// 바로 그 결함이 SIGINT에서 재발한 셈이었다.</item>
    /// </list>
    /// <para>
    /// 단일 adb 호출 하나가 <c>AppSettings.Timing.ProcessTimeoutMs</c>
    /// (기본 15000ms)까지 걸릴 수 있다고 이 앱의 다른 모든 곳이 이미
    /// 인정하고 있다 - 신호 처리기만 그보다 짧게 잘라내면, 정상적인 단일
    /// 호출조차 인위적으로 실패시켜 overlay를 흘리게 된다(부하가 걸린
    /// 실행에서 재현: .omc/research/settle-poll-report.md). SIGINT라고
    /// 예외를 둘 이유가 없다 - 화면 앞에 사용자가 있다는 사실이 정리
    /// 사슬이 실제로 걸리는 시간을 줄여주지는 않는다. 몇 초의 체감
    /// 응답성을 아끼는 이득은 유휴 기기에서는 아무도 못 느끼고(정리는
    /// 어차피 ~2초에 끝난다), 정리가 오래 걸리는 바로 그 순간(누수가
    /// 실제로 발생하는 순간)에만 값을 치르게 되는 나쁜 트레이드오프다.
    /// </para>
    /// <para>
    /// macOS는 SIGTERM을 SIGKILL로 자동 승격하지 않는다(그 결정은 신호를
    /// 보낸 쪽의 몫이다) - 그래서 "더 오래 기다리면 더 거칠게 죽는다"는
    /// 근거로 예산을 짧게 유지할 필요는 없다. 반대로 지나치게 길게 기다릴
    /// 이유도 없다: overlay 회수가 이 예산을 넘겨 실패해도 다음 DeX
    /// 시작이 남은 overlay를 그대로 발견해 재사용하므로
    /// (VirtualDisplayService.EnsureVirtualDisplay), 유일한 비용은 그
    /// 사이 창이 하나 남아 있는 것뿐이다 - 병적으로 참을성 있게 기다릴
    /// 이유가 되지는 못한다.
    /// </para>
    /// <para>
    /// 이 클래스는 DexManager.Core에 있어 GUI(DexManager.Desktop)와
    /// TUI(DexManager.Mac) 양쪽에 적용된다 - GUI의 SIGINT도 같은 잠재
    /// 누수를 안고 있었으므로 이 수정은 양쪽 모두를 고친다.
    /// </para>
    /// </summary>
    public static class SignalCleanupBudgets
    {
        /// <summary>
        /// SIGINT/SIGTERM/SIGHUP 공통 정리 예산. 단일 adb 호출이 걸릴 수
        /// 있는 상한(<c>AppSettings.Timing.ProcessTimeoutMs</c>, 기본
        /// 15000ms)과 맞춘다.
        /// </summary>
        public static readonly TimeSpan Budget = TimeSpan.FromSeconds(15);

        /// <summary>
        /// 신호별 조회 형태는 유지한다 - 호출부(각 Program.cs)를 굳이
        /// <c>SignalCleanupBudgets.Budget</c>으로 바꾸지 않아도 되고,
        /// 나중에 정말로 신호별 차등이 다시 필요해지면 이 진입점 하나만
        /// 고치면 된다. 지금은 신호와 무관하게 항상 같은 값을 돌려준다.
        /// </summary>
        public static TimeSpan For(PosixSignal signal) => Budget;
    }
}
