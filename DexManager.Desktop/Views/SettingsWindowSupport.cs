using System;
using DexManager.Models;
using DexManager.Services;

namespace DexManager.Desktop.Views;

/// <summary>
/// SettingsWindow.axaml의 ComboBox가 x:Static으로 참조하는 항목 소스.
/// 뷰모델(DexManager.ViewModels)은 Avalonia를 몰라야 하므로, 열거형 값을
/// UI 항목 목록으로 나열하는 책임은 여기(Desktop 레이어)에 둔다 -
/// AppearanceSettingsViewModel/InteractionSettingsViewModel은 손대지 않는다.
/// </summary>
public static class SettingsEnumSource
{
    public static AppTheme[] AppThemes { get; } = (AppTheme[])Enum.GetValues(typeof(AppTheme));

    public static AppLanguage[] AppLanguages { get; } = (AppLanguage[])Enum.GetValues(typeof(AppLanguage));

    public static KeyInputMode[] KeyInputModes { get; } = (KeyInputMode[])Enum.GetValues(typeof(KeyInputMode));
}

/// <summary>
/// SettingsWindow.axaml과 MainWindow.axaml이 x:Static으로 참조하는 지역화
/// 문자열. x:Static 값은 XAML 로드(창 생성) 시점에 한 번 평가되므로, 설정
/// 창 안에서 언어를 바꿔 저장해도 이미 그려진 이 문자열들은 갱신되지
/// 않는다 - AppearanceSettingsViewModel.LanguageRestartNotice가 이미
/// 알리는 "재시작 후 완전히 반영" 정책과 같은 제약이다.
/// </summary>
public static class SettingsWindowStrings
{
    public static string SettingsButtonLabel => LocalizationService.Get("Main.Settings");

    public static string DexRunningNotice => LocalizationService.Get("Settings.DexRunningNotice");

    /// <summary>Task 10: Interaction 탭 단축키 필드 아래에 표시하는 정직한
    /// 고지. macOS에는 실제 전역 단축키 리스너가 아직 없다(MacKeyboardService가
    /// 완전한 스텁) - 캡처/표시/저장은 되지만 앱이 그 값을 실제로 감지해
    /// 반응하지는 않는다.</summary>
    public static string HotkeyMacNotice => LocalizationService.Get("Settings.HotkeyMacNotice");

    /// <summary>UI-3: Slots 탭 상단에 표시하는 설명 - 슬롯 하나가 실제로
    /// 무엇을 하는지(별도 가상 디스플레이 위 scrcpy 창 하나에서 안드로이드
    /// 앱 하나를 실행) 한두 문장으로 요약한다. 필드 하나하나의 툴팁만으로는
    /// "슬롯"이라는 개념 자체가 불명확하다는 사용자 피드백에 대응한다.</summary>
    public static string SlotsHeader => LocalizationService.Get("Settings.Slots.Header");

    public static string SlotsWidthTip => LocalizationService.Get("Settings.Slots.WidthTip");

    public static string SlotsHeightTip => LocalizationService.Get("Settings.Slots.HeightTip");

    public static string SlotsDpiTip => LocalizationService.Get("Settings.Slots.DpiTip");

    public static string SlotsMaxFpsTip => LocalizationService.Get("Settings.Slots.MaxFpsTip");

    public static string SlotsBitRateTip => LocalizationService.Get("Settings.Slots.BitRateTip");

    public static string SlotsAppPackageTip => LocalizationService.Get("Settings.Slots.AppPackageTip");

    public static string SlotsAppNameTip => LocalizationService.Get("Settings.Slots.AppNameTip");

    public static string SlotsExtraArgsTip => LocalizationService.Get("Settings.Slots.ExtraArgsTip");

    public static string SlotsTurnScreenOffTip => LocalizationService.Get("Settings.Slots.TurnScreenOffTip");

    public static string SlotsStayAwakeTip => LocalizationService.Get("Settings.Slots.StayAwakeTip");

    /// <summary>HID 키보드/마우스는 ScrcpyService/SingleWindowService에서
    /// `OperatingSystem.IsWindows()`로 게이트되어 macOS에서는 -K/-M 인자가
    /// 아예 추가되지 않는다 - 이 툴팁은 그 사실을 정직하게 밝힌다.</summary>
    public static string SlotsHidKeyboardTip => LocalizationService.Get("Settings.Slots.HidKeyboardTip");

    public static string SlotsHidMouseTip => LocalizationService.Get("Settings.Slots.HidMouseTip");

    public static string SlotsForceStopTip => LocalizationService.Get("Settings.Slots.ForceStopTip");

    public static string SlotsFlexDisplayTip => LocalizationService.Get("Settings.Slots.FlexDisplayTip");
}
