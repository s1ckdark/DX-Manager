using Avalonia;
using Avalonia.Styling;
using DexManager.Models;

namespace DexManager.Desktop;

/// <summary>
/// AppTheme(모델, Avalonia 미참조)을 Avalonia의 ThemeVariant로 매핑해
/// 실행 중인 Application에 적용한다. Application.Current 접근은
/// DexManager.Desktop에만 있어야 하므로, 이 클래스가 그 유일한 지점이다
/// — DexManager.ViewModels의 AppearanceSettingsViewModel은 이 타입을
/// 전혀 모른다.
/// </summary>
public static class ThemeApplier
{
    /// <summary>
    /// AppTheme을 ThemeVariant로 매핑하는 순수 함수. Application.Current에
    /// 의존하지 않으므로 Avalonia Application을 구성하지 않고도 테스트할
    /// 수 있다.
    /// </summary>
    public static ThemeVariant MapToVariant(AppTheme theme)
    {
        switch (theme)
        {
            case AppTheme.Light:
                return ThemeVariant.Light;
            case AppTheme.Dark:
                return ThemeVariant.Dark;
            case AppTheme.Auto:
            default:
                // Auto와 정의되지 않은 값은 시스템 기본(Default)을 따른다.
                return ThemeVariant.Default;
        }
    }

    /// <summary>
    /// 저장된 테마를 현재 실행 중인 Application에 즉시 적용한다.
    /// Application.Current가 아직 없으면(예: 초기화 타이밍 문제) 아무 것도
    /// 하지 않는다.
    /// </summary>
    public static void Apply(AppTheme theme)
    {
        var application = Application.Current;
        if (application == null) return;

        application.RequestedThemeVariant = MapToVariant(theme);
    }
}
