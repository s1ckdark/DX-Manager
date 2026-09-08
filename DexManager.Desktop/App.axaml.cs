using System.ComponentModel;
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

    // 설정 창은 한 번에 하나만 띄운다 - ShellViewModel.OpenSettingsCommand는
    // 호출될 때마다 새 SettingsViewModel을 만들므로, 창이 이미 열려 있으면
    // 새로 만든 뷰모델은 즉시 Dispose하고 기존 창을 포커스한다(Task 12).
    private SettingsWindow _settingsWindow;
    private SettingsViewModel _openSettings;

    // SettingsViewModel.Appearance는 Cancel에서 교체된다(클래스 문서 참고) -
    // 그때마다 ThemeSaved 재구독 대상을 갱신하기 위해 현재 구독 중인
    // 인스턴스를 별도로 기억한다.
    private AppearanceSettingsViewModel _openSettingsAppearance;

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

            _shell.SettingsRequested += OnSettingsRequested;

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
    /// 즉시 재적용한다. Avalonia 타입 매핑은 ThemeApplier가 맡는다. 시작
    /// 시점의 _appearance와 설정 창의 SettingsViewModel.Appearance 양쪽 모두
    /// 이 핸들러 하나를 공유한다 - 상태가 없는 순수 재적용이라 안전하다.</summary>
    private void OnThemeSaved(object sender, AppTheme theme)
        => ThemeApplier.Apply(theme);

    /// <summary>
    /// ShellViewModel.OpenSettingsCommand가 새 SettingsViewModel을 준비할
    /// 때마다 발생한다. 이미 열린 설정 창이 있으면 방금 만들어진 뷰모델은
    /// 화면에 쓰이지 않으므로 즉시 Dispose하고(레지스트리 구독을 새지
    /// 않게) 기존 창을 앞으로 가져오는 데 그친다 - 창은 한 번에 하나만
    /// 띄운다.
    ///
    /// 새로 여는 경우: 창을 모덜리스(Show)로 띄운다. SettingsViewModel은
    /// 대상 기기 선택이 바뀔 때마다 DisplayStream/Slot 페이지를 스스로
    /// 다시 로드하므로(SettingsViewModel.OnDeviceSelectionPropertyChanged),
    /// 설정 창을 열어 둔 채로 MainWindow에서 다른 기기를 선택하면 그
    /// 반응성을 그대로 볼 수 있어야 한다 - ShowDialog로 MainWindow를
    /// 막으면 이 설계 의도가 무의미해진다.
    /// </summary>
    private void OnSettingsRequested(object sender, SettingsViewModel settings)
    {
        if (_settingsWindow != null)
        {
            settings.Dispose();
            _settingsWindow.Activate();
            return;
        }

        _openSettings = settings;
        HookAppearanceThemeSaved(settings.Appearance);
        settings.PropertyChanged += OnOpenSettingsPropertyChanged;
        settings.CloseRequested += OnSettingsCloseRequested;

        var window = new SettingsWindow { DataContext = settings };
        _settingsWindow = window;
        window.Closed += OnSettingsWindowClosed;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow != null)
        {
            window.Show(desktop.MainWindow);
        }
        else
        {
            window.Show();
        }
    }

    /// <summary>
    /// SettingsViewModel.Cancel은 Appearance 인스턴스를 통째로 교체한다
    /// (SettingsViewModel 클래스 문서 참고) - 창을 열 때 구독한 ThemeSaved는
    /// 옛 인스턴스에 걸려 있으므로, Appearance 프로퍼티 변경 통지를 계기로
    /// 새 인스턴스에 다시 구독한다. 기기 선택 변경은 Appearance를 건드리지
    /// 않으므로 이 경로는 Cancel에서만 발생한다.
    /// </summary>
    private void OnOpenSettingsPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SettingsViewModel.Appearance)) return;

        UnhookAppearanceThemeSaved();
        HookAppearanceThemeSaved(_openSettings?.Appearance);
    }

    /// <summary>
    /// SettingsViewModel.SaveAll/Cancel이 각자의 기존 동작(저장 또는 편집
    /// 버리기)을 끝낸 뒤 올리는 신호다 - 이 타입은 Avalonia에 의존하지
    /// 않으므로 창을 직접 닫지 못한다. 여기서 실제로 창을 닫는다. Close()는
    /// SettingsWindow.Closed를 동기적으로 발생시키므로 OnSettingsWindowClosed가
    /// 바로 뒤이어 구독 해제와 Dispose를 맡는다 - 이 핸들러에서 별도로
    /// SettingsViewModel을 정리할 필요가 없다.
    /// </summary>
    private void OnSettingsCloseRequested(object sender, EventArgs e)
        => _settingsWindow?.Close();

    private void HookAppearanceThemeSaved(AppearanceSettingsViewModel appearance)
    {
        _openSettingsAppearance = appearance;
        if (_openSettingsAppearance != null)
            _openSettingsAppearance.ThemeSaved += OnThemeSaved;
    }

    private void UnhookAppearanceThemeSaved()
    {
        if (_openSettingsAppearance != null)
            _openSettingsAppearance.ThemeSaved -= OnThemeSaved;
        _openSettingsAppearance = null;
    }

    /// <summary>
    /// 설정 창이 닫힐 때(취소/저장 후 닫기 모두) 구독을 해제하고
    /// SettingsViewModel을 Dispose한다 - IDisposable이 소유한 레지스트리
    /// 구독이 새지 않도록.
    /// </summary>
    private void OnSettingsWindowClosed(object sender, EventArgs e)
    {
        if (sender is SettingsWindow window)
            window.Closed -= OnSettingsWindowClosed;

        if (_openSettings != null)
        {
            _openSettings.PropertyChanged -= OnOpenSettingsPropertyChanged;
            _openSettings.CloseRequested -= OnSettingsCloseRequested;
        }

        UnhookAppearanceThemeSaved();

        _openSettings?.Dispose();
        _openSettings = null;
        _settingsWindow = null;
    }

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

        // 설정 창이 아직 열려 있으면(예: 확인 없이 앱이 종료되는 경로)
        // Closed 핸들러를 먼저 떼어 이중 Dispose를 피하고, 여기서 직접
        // 한 번만 정리한다. SettingsViewModel.Dispose 자체도 _disposed
        // 가드가 있어 이중 호출에 안전하지만, 굳이 기대지 않는다.
        if (_settingsWindow != null)
        {
            _settingsWindow.Closed -= OnSettingsWindowClosed;
            _settingsWindow = null;
        }

        if (_openSettings != null)
        {
            _openSettings.PropertyChanged -= OnOpenSettingsPropertyChanged;
            _openSettings.CloseRequested -= OnSettingsCloseRequested;
            UnhookAppearanceThemeSaved();
            _openSettings.Dispose();
            _openSettings = null;
        }

        if (_shell != null)
            _shell.SettingsRequested -= OnSettingsRequested;

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
