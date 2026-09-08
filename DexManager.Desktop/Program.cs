using System;
using System.Runtime.InteropServices;
using Avalonia;

namespace DexManager.Desktop;

internal static class Program
{
    private static PosixSignalRegistration[] _signalRegistrations;

    [STAThread]
    public static void Main(string[] args)
    {
        // StartWithClassicDesktopLifetime 이전에 등록해야, 앱 초기화
        // 도중(App 생성자~OnFrameworkInitializationCompleted 사이)에
        // 신호가 와도 처리기가 이미 걸려 있다.
        RegisterTerminationSignalHandlers();
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            DisposeTerminationSignalHandlers();
        }
    }

    // Avalonia 디자이너와 헤드리스 테스트가 이 메서드를 이름으로 찾는다.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

    /// <summary>
    /// SIGTERM/SIGINT/SIGHUP으로 죽을 때 Avalonia의 Exit 이벤트는 발생하지
    /// 않는다 - App.axaml.cs:45의 desktop.Exit은 정상 종료(Stop DeX,
    /// 창 닫기)에서만 올라온다. 이 신호들을 그대로 두면 App.OnExit ->
    /// DisposeQuietly가 실행되지 않아 overlay_display_devices 회수와
    /// scrcpy 종료가 통째로 샌다 - 오늘 실기기(SM-F971N)에서
    /// `pkill -TERM`으로 확인한 버그다.
    ///
    /// SIGINT도 같은 부류다: 터미널에서 `dotnet run`으로 띄운 경우
    /// Ctrl+C가 SIGINT를 보내는데, 지금까지 Console.CancelKeyPress 등
    /// 아무 것도 걸려 있지 않아 SIGTERM과 동일하게 샌다. SIGHUP은
    /// 제어 터미널이 닫힐 때(예: nohup 없이 백그라운드로 띄운 세션이
    /// 끊길 때) 오는데, 발생 빈도는 낮지만 같은 정리를 걸어 두는 비용이
    /// 거의 없어 함께 등록한다.
    /// </summary>
    private static void RegisterTerminationSignalHandlers()
    {
        _signalRegistrations = new[]
        {
            PosixSignalRegistration.Create(PosixSignal.SIGTERM, HandleTerminationSignal),
            PosixSignalRegistration.Create(PosixSignal.SIGINT, HandleTerminationSignal),
            PosixSignalRegistration.Create(PosixSignal.SIGHUP, HandleTerminationSignal),
        };
    }

    private static void DisposeTerminationSignalHandlers()
    {
        if (_signalRegistrations == null) return;
        foreach (var registration in _signalRegistrations)
        {
            registration.Dispose();
        }
        _signalRegistrations = null;
    }

    /// <summary>
    /// 기본 종료 동작을 취소하지 않는다(<c>ctx.Cancel = true</c>를 하지
    /// 않는다). 정리를 예산 안에서 동기적으로 기다린 뒤 그대로 반환하면,
    /// 이 메서드가 마지막으로 등록된 처리기라 런타임이 이어서 해당 신호의
    /// 기본 동작(프로세스 종료)을 수행한다 - 예산을 넘겨도 이 메서드는
    /// 결국 반환하므로 정리가 끝났든 아니든 프로세스는 죽는다. 직접
    /// Environment.Exit을 부르지 않는 이유도 이것이다 - 그럴 필요가 없고,
    /// 우리가 직접 종료 코드를 고르는 것보다 신호별 기본 동작에 맡기는
    /// 편이 더 예측 가능하다.
    ///
    /// 예산은 신호 종류에 따라 다르다(<see cref="SignalCleanupBudgets"/>) -
    /// SIGINT(대화형 Ctrl+C)는 짧게, SIGTERM/SIGHUP(비대화형)은 실제 adb
    /// 정리 사슬이 필요로 하는 시간에 맞춰 더 길게 잡는다.
    /// </summary>
    private static void HandleTerminationSignal(PosixSignalContext ctx)
    {
        if (Application.Current is not App app) return;

        try
        {
            BoundedExecutor.RunWithBudget(
                () => app.TryRunShutdownCleanup(),
                SignalCleanupBudgets.For(ctx.Signal));
        }
        catch
        {
            // 신호 처리기에서 예외가 새어 나가면 CLR이 fail-fast로 프로세스를
            // 죽인다 - 의도한 "정리하고 기본 종료"가 크래시로 바뀐다.
            // DisposeQuietly 안에서 실제로 던질 만한 곳(_shell?.Dispose()의
            // adb/프로세스 정리)은 이미 자체 try/catch가 있지만, 그렇지 않은
            // 경로(_openSettings.Dispose() 등)가 언젠가 던질 수 있다.
            // 어차피 프로세스는 곧 종료되므로 여기서는 삼키고 기본 종료 처리에
            // 맡기는 것이 크래시보다 낫다.
        }
    }
}
