using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DexManager.Desktop.Views;
using DexManager.Hosting;
using DexManager.Mac.Platform;
using DexManager.ViewModels;

namespace DexManager.Desktop;

public partial class App : Application
{
    private ShellViewModel _shell;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 정리는 ShutdownRequested가 아니라 Exit에 건다. ShutdownRequested는
            // 취소할 수 있고 명시적 Shutdown() 호출에서는 아예 발생하지 않는다 —
            // 종료 확인 대화상자가 생기면 호스트만 먼저 죽는 상태가 된다.
            desktop.Exit += OnExit;
            desktop.MainWindow = CreateMainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// 호스트를 조립하고 주 창을 만든다. 조립이 실패하면 안내 창을 대신 낸다.
    /// </summary>
    private Window CreateMainWindow()
    {
        try
        {
            var pathProvider = new MacPathProvider();

            var host = new ApplicationHost(
                new MacPlatformService(),
                pathProvider,
                new MacCaptureService(pathProvider.DefaultScreenshotFolder),
                new MacKeyboardService(),
                new MacAutoStartService());

            _shell = new ShellViewModel(host, new AvaloniaUiDispatcher());
            var window = new MainWindow { DataContext = _shell };

            _shell.Start();
            return window;
        }
        catch (Exception ex)
        {
            // ApplicationHost 생성자는 adb를 찾지 못하면 FileNotFoundException을
            // 던진다. 여기서 막지 않으면 창도 대화상자도 없이 앱이 죽는다.
            DisposeQuietly();
            Report(ex);
            return StartupErrorWindow.Create(ex);
        }
    }

    private void OnExit(object sender, ControlledApplicationLifetimeExitEventArgs e)
        => DisposeQuietly();

    /// <summary>
    /// 셸을 해제한다. <see cref="ApplicationHost.Dispose"/>는 정리 실패를
    /// <see cref="AggregateException"/>으로 던지는데, 종료 이벤트 처리기에서
    /// 그대로 새어 나가면 처리되지 않은 예외가 된다. 삼키지는 않고 보고한다.
    /// </summary>
    private void DisposeQuietly()
    {
        try
        {
            _shell?.Dispose();
        }
        catch (Exception ex)
        {
            Report(ex);
        }
        finally
        {
            _shell = null;
        }
    }

    // 터미널 UI(DexManager.Mac/Program.cs)의 "Fatal error: {message}"와 같은
    // 형식으로 남긴다. .app으로 실행하면 이 출력은 시스템 로그에 기록된다.
    private static void Report(Exception ex)
        => Console.Error.WriteLine($"Fatal error: {ex.Message}");
}
