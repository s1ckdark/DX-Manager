using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DexManager.ViewModels;

namespace DexManager.Desktop.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();

        // Tunnel(하향) 단계에서 먼저 가로챈다. RoutingStrategies.Bubble로
        // 등록했다면 대상(TextBox) 자신의 내부 KeyDown 처리(문자 삽입,
        // 캐럿 이동 등)가 우리 핸들러보다 먼저 실행될 수 있어 e.Handled를
        // 아무리 빨리 세팅해도 이미 늦을 수 있다. Tunnel은 루트에서
        // 대상으로 내려가는 단계라 TextBox 자신의 처리보다 반드시 먼저
        // 실행되므로, IsReadOnly 설정 여부와 무관하게 원문 텍스트 입력을
        // 확실히 막을 수 있다 - Task 10 Step-0에서 확인한 것처럼 이
        // 순서는 Avalonia 문서만으로는 보장되지 않아 실기에서 최종
        // 확인이 필요하다(보고서의 UNVERIFIED 체크리스트 참고).
        CaptureHotkeyBox.AddHandler(
            KeyDownEvent, OnCaptureHotkeyKeyDown, RoutingStrategies.Tunnel);
        ExitHotkeyBox.AddHandler(
            KeyDownEvent, OnExitHotkeyKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnCaptureHotkeyKeyDown(object sender, KeyEventArgs e)
        => HandleHotkeyKeyDown(e, isCapture: true);

    private void OnExitHotkeyKeyDown(object sender, KeyEventArgs e)
        => HandleHotkeyKeyDown(e, isCapture: false);

    /// <summary>
    /// 두 단축키 입력란이 공유하는 캡처 로직. 항상 e.Handled = true를
    /// 먼저 세팅해 원문 텍스트가 절대 삽입되지 않게 한 뒤,
    /// HotkeyFormatter가 유효한 조합으로 판단한 경우에만(수정자 단독
    /// 입력이나 Key.None이 아닌 경우) 바인딩된 값을 갱신한다.
    /// </summary>
    private void HandleHotkeyKeyDown(KeyEventArgs e, bool isCapture)
    {
        e.Handled = true;

        var formatted = HotkeyFormatter.Format(e.Key, e.KeyModifiers);
        if (formatted == null) return;

        if (DataContext is not SettingsViewModel settings) return;

        if (isCapture)
            settings.Interaction.CaptureHotkey = formatted;
        else
            settings.Interaction.ExitHotkey = formatted;
    }
}
