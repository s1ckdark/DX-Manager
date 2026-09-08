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
}
