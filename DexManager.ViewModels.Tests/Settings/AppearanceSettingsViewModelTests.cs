using System;
using Xunit;
using DexManager.Models;
using DexManager.Services;

namespace DexManager.ViewModels.Tests.Settings;

/// <summary>
/// AppearanceSettingsViewModel의 테마 선택·저장 동작을 검증한다.
/// 테마는 전역 설정이라 기기 identity가 없다(PathsSettingsViewModel과 같은
/// 패턴). ThemeVariant 매핑은 Avalonia 쪽(ThemeApplier)의 책임이므로 여기서는
/// 다루지 않는다 — 이 뷰모델은 AppTheme(모델) 값만 다룬다.
/// </summary>
public class AppearanceSettingsViewModelTests
{
    [Fact]
    public void Constructor_LoadsCurrentThemeFromGateway()
    {
        // 생성 시 현재 설정값에서 테마를 로드한다.
        var gateway = new FakeSettingsGateway();
        gateway.Update(s => s.Theme = AppTheme.Dark);

        var viewModel = new AppearanceSettingsViewModel(gateway);

        Assert.Equal(AppTheme.Dark, viewModel.SelectedTheme);
    }

    [Fact]
    public void Save_WhenThemeSelected_WritesThemeIntoSettingsViaGateway()
    {
        // 테마를 고르고 저장하면 s.Theme에 실제로 대입된다.
        var gateway = new FakeSettingsGateway();
        var viewModel = new AppearanceSettingsViewModel(gateway);

        viewModel.SelectedTheme = AppTheme.Light;

        viewModel.SaveCommand.Execute(null);

        Assert.Equal(AppTheme.Light, gateway.Current.Theme);
        Assert.Equal(1, gateway.UpdateCallCount);
    }

    [Fact]
    public void Save_RaisesThemeSavedEvent_WithSelectedTheme()
    {
        // 저장 시 ThemeSaved 이벤트가 선택한 테마 값과 함께 발생해야
        // Desktop의 ThemeApplier가 즉시 재적용할 수 있다.
        var gateway = new FakeSettingsGateway();
        var viewModel = new AppearanceSettingsViewModel(gateway);

        AppTheme? raised = null;
        viewModel.ThemeSaved += (_, theme) => raised = theme;

        viewModel.SelectedTheme = AppTheme.Dark;
        viewModel.SaveCommand.Execute(null);

        Assert.Equal(AppTheme.Dark, raised);
    }

    [Fact]
    public void HasChanges_FalseInitially_TrueAfterEdit_FalseAfterSave()
    {
        // HasChanges는 로드 직후 false, 편집 후 true, 저장 후 다시 false다.
        var gateway = new FakeSettingsGateway();
        var viewModel = new AppearanceSettingsViewModel(gateway);

        Assert.False(viewModel.HasChanges);

        viewModel.SelectedTheme = AppTheme.Dark;

        Assert.True(viewModel.HasChanges);

        viewModel.SaveCommand.Execute(null);

        Assert.False(viewModel.HasChanges);
    }

    // --- 언어(Task 8) ---
    // 테마와 다른 점: LocalizationService는 Avalonia를 참조하지 않는
    // DexManager.Core에 있으므로, 뷰모델이 저장과 동시에 직접 Apply를
    // 호출할 수 있다(테마처럼 Desktop 쪽 구독자가 따로 필요 없다).

    [Fact]
    public void Constructor_LoadsCurrentLanguageFromGateway()
    {
        // 생성 시 현재 설정값에서 언어를 로드한다.
        var gateway = new FakeSettingsGateway();
        gateway.Update(s => s.Language = AppLanguage.English);

        var viewModel = new AppearanceSettingsViewModel(gateway);

        Assert.Equal(AppLanguage.English, viewModel.SelectedLanguage);
    }

    [Fact]
    public void Save_WhenLanguageSelected_WritesLanguageIntoSettingsViaGateway()
    {
        // 언어를 고르고 저장하면 s.Language에 실제로 대입된다.
        var gateway = new FakeSettingsGateway();
        var viewModel = new AppearanceSettingsViewModel(gateway);

        viewModel.SelectedLanguage = AppLanguage.Korean;

        viewModel.SaveCommand.Execute(null);

        Assert.Equal(AppLanguage.Korean, gateway.Current.Language);
    }

    [Fact]
    public void HasChanges_TrueAfterLanguageEdit_FalseAfterSave()
    {
        // 언어만 바꿔도 HasChanges가 true가 되고, 저장하면 다시 false가 된다.
        var gateway = new FakeSettingsGateway();
        var viewModel = new AppearanceSettingsViewModel(gateway);

        Assert.False(viewModel.HasChanges);

        viewModel.SelectedLanguage = AppLanguage.English;

        Assert.True(viewModel.HasChanges);

        viewModel.SaveCommand.Execute(null);

        Assert.False(viewModel.HasChanges);
    }

    [Fact]
    public void Save_AppliesSelectedLanguageToLocalizationService_SoNewStringsSwitch()
    {
        // LocalizationService.Get은 호출마다 정적 _culture를 다시 읽으므로,
        // 저장 시 Apply가 실제로 호출됐는지를 새로 생성되는 문자열로 검증할
        // 수 있다. 두 리소스 파일에서 값이 실제로 다른 키(Environment.AdbVersion)
        // 로 en/ko 전환을 증명한다.
        //
        // LocalizationService._culture는 정적 상태라 이 테스트가 다른
        // 테스트를 오염시키지 않도록 원래 컬처를 저장했다가 finally에서
        // 복원한다.
        var originalCulture = LocalizationService.Culture;
        try
        {
            var gateway = new FakeSettingsGateway();
            var viewModel = new AppearanceSettingsViewModel(gateway);

            viewModel.SelectedLanguage = AppLanguage.English;
            viewModel.SaveCommand.Execute(null);
            var english = LocalizationService.Get("Environment.AdbVersion");

            viewModel.SelectedLanguage = AppLanguage.Korean;
            viewModel.SaveCommand.Execute(null);
            var korean = LocalizationService.Get("Environment.AdbVersion");

            Assert.Equal("ADB version", english);
            Assert.Equal("ADB 버전", korean);
        }
        finally
        {
            var wasKorean = string.Equals(
                originalCulture.TwoLetterISOLanguageName,
                "ko",
                StringComparison.OrdinalIgnoreCase);
            LocalizationService.Apply(wasKorean ? AppLanguage.Korean : AppLanguage.English);
        }
    }
}
