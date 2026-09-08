using System;
using System.Globalization;
using Xunit;
using DexManager.Models;
using DexManager.Services;

namespace DexManager.ViewModels.Tests.Settings;

/// <summary>
/// AppearanceSettingsViewModel의 테마·언어 선택·저장 동작을 검증한다.
/// 둘 다 전역 설정이라 기기 identity가 없다(PathsSettingsViewModel과 같은
/// 패턴). ThemeVariant 매핑은 Avalonia 쪽(ThemeApplier)의 책임이므로 여기서는
/// 다루지 않는다 — 이 뷰모델은 AppTheme(모델) 값만 다룬다.
///
/// <see cref="AppearanceSettingsViewModel.Save"/>는 언어를 건드리지 않는
/// 테스트(테마만 바꾸는 테스트 포함)에서도 매번 무조건
/// <see cref="LocalizationService.Apply"/>를 호출해 정적 필드
/// <c>LocalizationService.Culture</c> / <c>CultureInfo.CurrentUICulture</c>를
/// 바꾼다. 따라서 SaveCommand를 실행하는 모든 테스트가 이 정적 상태를
/// 오염시킬 수 있다. IDisposable로 매 테스트 인스턴스 생성 시점의 컬처를
/// 스냅샷하고, 테스트가 끝나면(xUnit은 [Fact]마다 새 클래스 인스턴스를
/// 만들고 그 인스턴스의 Dispose를 호출한다) 복원해, 개별 테스트마다
/// try/finally를 반복하지 않고도 클래스 전체를 균일하게 격리한다.
/// </summary>
public class AppearanceSettingsViewModelTests : IDisposable
{
    private readonly CultureInfo _originalCulture;

    public AppearanceSettingsViewModelTests()
    {
        _originalCulture = LocalizationService.Culture;
    }

    /// <summary>
    /// xUnit이 이 테스트 인스턴스를 버리기 직전에 호출한다. 이 인스턴스의
    /// 생성자가 찍어둔 컬처로 되돌려, 이 클래스 안의 어떤 테스트가
    /// SaveCommand를 실행해 정적 컬처를 바꾸더라도 다음 테스트(또는 같은
    /// 어셈블리의 다른 테스트 클래스)로 새어나가지 않게 한다.
    /// </summary>
    public void Dispose()
    {
        var wasKorean = string.Equals(
            _originalCulture.TwoLetterISOLanguageName,
            "ko",
            StringComparison.OrdinalIgnoreCase);
        LocalizationService.Apply(wasKorean ? AppLanguage.Korean : AppLanguage.English);
    }

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
        // 이 테스트가 남기는 정적 컬처 변경은 클래스 수준 IDisposable(생성자
        // 스냅샷 + Dispose 복원)이 정리한다 — 개별 try/finally가 필요 없다.
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

    [Fact]
    public void Dispose_RestoresCultureCapturedAtConstruction_AfterSaveMutatesIt()
    {
        // 격리 장치 자체를 직접 검증한다: 이 테스트가 아니라 별도로 만든
        // AppearanceSettingsViewModelTests 인스턴스의 Dispose가 자기
        // 생성 시점의 컬처로 정확히 되돌리는지 확인한다. xUnit 프레임워크의
        // 자동 Dispose 호출 타이밍에 기대지 않고 이 테스트 스스로 Dispose를
        // 호출해 즉시 확인하므로 "복원이 실제로 일어난다"는 계약을 직접
        // 증명한다.
        //
        // 시스템 로캘에 좌우되지 않도록, 먼저 English로 저장해 알려진
        // 상태("en")를 만든 뒤에야 격리 인스턴스를 만들어 그 상태를
        // 스냅샷하게 한다.
        var gateway = new FakeSettingsGateway();
        var viewModel = new AppearanceSettingsViewModel(gateway);

        viewModel.SelectedLanguage = AppLanguage.English;
        viewModel.SaveCommand.Execute(null);
        Assert.Equal("en", LocalizationService.Culture.TwoLetterISOLanguageName.ToLowerInvariant());

        var isolatedInstance = new AppearanceSettingsViewModelTests(); // "en"을 스냅샷

        viewModel.SelectedLanguage = AppLanguage.Korean;
        viewModel.SaveCommand.Execute(null);
        Assert.Equal("ko", LocalizationService.Culture.TwoLetterISOLanguageName.ToLowerInvariant());

        isolatedInstance.Dispose(); // 스냅샷한 "en"으로 되돌려야 한다

        Assert.Equal("en", LocalizationService.Culture.TwoLetterISOLanguageName.ToLowerInvariant());
    }
}
