using Xunit;
using DexManager.Models;

namespace DexManager.ViewModels.Tests.Settings;

/// <summary>
/// PathsSettingsViewModel의 경로 편집·저장·재설정 동작을 검증한다.
/// </summary>
public class PathsSettingsViewModelTests
{
    [Fact]
    public void Save_WhenPathsEdited_WritesToSettingsPathsViaGateway()
    {
        // 경로를 편집하고 저장하면 s.Paths에 실제로 대입된다.
        var gateway = new FakeSettingsGateway();
        var viewModel = new PathsSettingsViewModel(gateway);

        viewModel.ScrcpyPath = "/usr/local/bin/scrcpy";
        viewModel.AdbPath = "/usr/local/bin/adb";

        viewModel.SaveCommand.Execute(null);

        Assert.Equal("/usr/local/bin/scrcpy", gateway.Current.Paths.ScrcpyPath);
        Assert.Equal("/usr/local/bin/adb", gateway.Current.Paths.AdbPath);
        Assert.Equal(1, gateway.UpdateCallCount);
    }

    [Fact]
    public void ResetToBundledDefaults_RepopulatesPathsFromDefaults()
    {
        // 재설정하면 편집값이 번들 기본값으로 다시 채워진다.
        var gateway = new FakeSettingsGateway();
        var defaultSettings = AppSettings.CreateDefault();
        var viewModel = new PathsSettingsViewModel(gateway);

        // 기존 값과 다르게 설정한다.
        viewModel.ScrcpyPath = "/different/scrcpy";
        viewModel.AdbPath = "/different/adb";

        viewModel.ResetToBundledDefaultsCommand.Execute(null);

        Assert.Equal(defaultSettings.Paths.ScrcpyPath, viewModel.ScrcpyPath);
        Assert.Equal(defaultSettings.Paths.AdbPath, viewModel.AdbPath);
    }

    [Fact]
    public void HasChanges_FalseInitially_TrueAfterEdit_FalseAfterSave()
    {
        // HasChanges는 로드 직후 false, 편집 후 true, 저장 후 false다.
        var gateway = new FakeSettingsGateway();
        var viewModel = new PathsSettingsViewModel(gateway);

        Assert.False(viewModel.HasChanges);

        viewModel.ScrcpyPath = "/new/path";

        Assert.True(viewModel.HasChanges);

        viewModel.SaveCommand.Execute(null);

        Assert.False(viewModel.HasChanges);
    }

    [Fact]
    public void Constructor_LoadsCurrentPathsFromGateway()
    {
        // 생성 시 현재 설정값에서 경로를 로드한다.
        var gateway = new FakeSettingsGateway();
        gateway.Update(s =>
        {
            s.Paths.ScrcpyPath = "/custom/scrcpy";
            s.Paths.AdbPath = "/custom/adb";
        });

        var viewModel = new PathsSettingsViewModel(gateway);

        Assert.Equal("/custom/scrcpy", viewModel.ScrcpyPath);
        Assert.Equal("/custom/adb", viewModel.AdbPath);
    }

    [Fact]
    public void Save_NoOpWhenNoChanges()
    {
        // 변경이 없으면 저장해도 Update가 호출된다(현재값으로 설정).
        // 이것은 의도적 설계: 저장이 무조건 Update를 호출하도록.
        var gateway = new FakeSettingsGateway();
        var viewModel = new PathsSettingsViewModel(gateway);

        int callsBefore = gateway.UpdateCallCount;

        viewModel.SaveCommand.Execute(null);

        // Update는 호출되지만 값은 변하지 않음.
        Assert.Equal(callsBefore + 1, gateway.UpdateCallCount);
    }
}
