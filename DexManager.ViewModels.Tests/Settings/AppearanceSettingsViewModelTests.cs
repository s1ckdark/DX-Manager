using Xunit;
using DexManager.Models;

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
}
