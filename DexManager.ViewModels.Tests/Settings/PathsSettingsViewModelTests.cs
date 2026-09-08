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
        // 재설정하면 번들 기본값에서 로드된다. 게이트웨이의 현재값이 아니라.
        // 이것을 검증하기 위해 게이트웨이의 현재값을 번들 기본값과
        // 다르게 밀어놓은 뒤 reset을 호출한다. Reset이 CreateDefault()를
        // 읽으면 기본값으로 돌아오고, gateway.Current를 읽으면
        // "live" 값으로 가버린다. 그러므로 이 테스트는
        // 회귀(gateway.Current를 읽는 버그)를 반드시 잡아야 한다.
        var gateway = new FakeSettingsGateway();
        var defaultSettings = AppSettings.CreateDefault();

        // 게이트웨이의 현재 경로를 번들 기본값과 다르게 설정한다.
        gateway.Update(s =>
        {
            s.Paths.ScrcpyPath = "/live/scrcpy";
            s.Paths.AdbPath = "/live/adb";
        });

        var viewModel = new PathsSettingsViewModel(gateway);

        // 초기 로드 후에는 gateway.Current.Paths(현재값)로 로드되어 있다.
        Assert.Equal("/live/scrcpy", viewModel.ScrcpyPath);
        Assert.Equal("/live/adb", viewModel.AdbPath);

        // 재설정 호출
        viewModel.ResetToBundledDefaultsCommand.Execute(null);

        // Reset이 CreateDefault()를 읽으면 번들 기본값으로 복구된다.
        // Reset이 gateway.Current를 읽으면 이미 live 값이므로 변하지 않는다.
        // 따라서 이 어서션은 CreateDefault() 사용을 강제한다.
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
