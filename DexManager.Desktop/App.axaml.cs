using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DexManager.Desktop.Views;
using DexManager.Hosting;
using DexManager.Mac.Platform;
using DexManager.Models;
using DexManager.Services;
using DexManager.ViewModels;

namespace DexManager.Desktop;

public partial class App : Application
{
    private ShellViewModel _shell;

    // 외관 설정 뷰모델을 앱 수명 동안 들고 있는다. 아직 이 값을 편집하는
    // 화면이 없더라도, 저장 시 즉시 재적용(ThemeSaved 구독)이 동작하려면
    // 인스턴스가 GC되지 않고 살아 있어야 한다. 나중에 설정 화면이 이
    // 인스턴스를 재사용한다.
    private AppearanceSettingsViewModel _appearance;

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
        ApplicationHost host = null;
        try
        {
            host = MacApplicationHostFactory.Create();

            // 저장된 테마를 시작 시 적용한다. App.axaml의 하드코딩된
            // RequestedThemeVariant="Default"를 대신한다.
            var gateway = new SettingsGateway(host);
            _appearance = new AppearanceSettingsViewModel(gateway);
            _appearance.ThemeSaved += OnThemeSaved;
            ThemeApplier.Apply(_appearance.SelectedTheme);

            // 저장된 언어를 시작 시 적용한다. LocalizationService는
            // DexManager.Core에 있어 Avalonia를 참조하지 않으므로 여기서
            // 직접 호출한다(테마처럼 Desktop 전용 어댑터가 필요 없다).
            // 이 호출은 이후 LocalizationService.Get이 반환할 문자열의
            // 기준 컬처를 세팅할 뿐, 이미 만들어진 MainWindow의 XAML
            // 문자열은 다시 그리지 않는다 — 그건 재시작이 필요하다.
            LocalizationService.Apply(_appearance.SelectedLanguage);

            _shell = new ShellViewModel(host, new AvaloniaUiDispatcher());
            // 셸이 호스트를 넘겨받았다. 이제부터 정리는 셸의 몫이다.
            host = null;

            var window = new MainWindow { DataContext = _shell };

            _shell.Start();
            return window;
        }
        catch (Exception ex)
        {
            // ApplicationHost 생성자는 adb를 찾지 못하면 FileNotFoundException을
            // 던진다. 여기서 막지 않으면 창도 대화상자도 없이 앱이 죽는다.
            //
            // ShellViewModel 생성이 던지면 _shell은 null이라 DisposeQuietly가
            // 아무것도 하지 않는다. 이미 만들어진 호스트를 직접 회수한다.
            DisposeQuietly();
            DisposeHostQuietly(host);
            Report(ex);
            return StartupErrorWindow.Create(ex);
        }
    }

    /// <summary>
    /// 셸이 넘겨받지 못한 호스트를 회수한다. 정리 실패는 보고만 하고
    /// 삼키지 않는다 — 이 경로는 이미 오류 처리 중이다.
    /// </summary>
    private static void DisposeHostQuietly(ApplicationHost host)
    {
        if (host == null) return;
        try
        {
            host.Dispose();
        }
        catch (Exception ex)
        {
            Report(ex);
        }
    }

    private void OnExit(object sender, ControlledApplicationLifetimeExitEventArgs e)
        => DisposeQuietly();

    /// <summary>테마가 저장되면(AppearanceSettingsViewModel.SaveCommand)
    /// 즉시 재적용한다. Avalonia 타입 매핑은 ThemeApplier가 맡는다.</summary>
    private void OnThemeSaved(object sender, AppTheme theme)
        => ThemeApplier.Apply(theme);

    /// <summary>
    /// 셸을 해제한다. <see cref="ApplicationHost.Dispose"/>는 정리 실패를
    /// <see cref="AggregateException"/>으로 던지는데, 종료 이벤트 처리기에서
    /// 그대로 새어 나가면 처리되지 않은 예외가 된다. 삼키지는 않고 보고한다.
    /// </summary>
    private void DisposeQuietly()
    {
        if (_appearance != null)
        {
            _appearance.ThemeSaved -= OnThemeSaved;
            _appearance = null;
        }

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
