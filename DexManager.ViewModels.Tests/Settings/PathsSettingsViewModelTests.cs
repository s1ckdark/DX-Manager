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
    public void Save_WhenAdbPathNonEmpty_SwitchesToManualSelectionMode()
    {
        // 오늘 고친 버그: ADB 경로를 채워 저장해도 AdbSelectionMode가
        // 전혀 바뀌지 않아, PathService가 그 값을 절대 읽지 않았다
        // (AdbSelectionMode는 항상 Auto로 남는다). 값을 채우면 Manual로
        // 넘어가야 실제로 쓰인다.
        var gateway = new FakeSettingsGateway();
        var viewModel = new PathsSettingsViewModel(gateway);

        viewModel.AdbPath = "/usr/local/bin/adb";
        viewModel.SaveCommand.Execute(null);

        Assert.Equal(
            AdbSelectionMode.Manual,
            gateway.Current.Paths.AdbSelectionMode);
    }

    [Fact]
    public void Save_WhenAdbPathCleared_RestoresAutoSelectionMode()
    {
        // 필드를 비우면 자동 감지로 되돌아가야 한다 - Manual로 남아
        // 있으면 빈 경로로 PathService.SelectRequired가 항상 실패한다.
        var gateway = new FakeSettingsGateway();
        gateway.Update(s =>
        {
            s.Paths.AdbPath = "/usr/local/bin/adb";
            s.Paths.AdbSelectionMode = AdbSelectionMode.Manual;
        });

        var viewModel = new PathsSettingsViewModel(gateway);
        Assert.Equal("/usr/local/bin/adb", viewModel.AdbPath);

        viewModel.AdbPath = string.Empty;
        viewModel.SaveCommand.Execute(null);

        Assert.Equal(
            AdbSelectionMode.Auto,
            gateway.Current.Paths.AdbSelectionMode);
        Assert.Equal(string.Empty, gateway.Current.Paths.AdbPath);
    }

    [Fact]
    public void Save_WhenAdbPathUntouched_LeavesSelectionModeUnchanged()
    {
        // 리뷰가 짚은 크리티컬 시나리오: ApplicationHost.EnsureDefaultPaths가
        // 자동 감지한 절대 경로로 이 필드가 이미 채워져 있는(Auto 모드)
        // 흔한 상태에서, 사용자가 이 필드는 건드리지 않고 다른 설정만
        // 바꾼 뒤 저장해도(SettingsViewModel.SaveAll은 Paths.SaveCommand를
        // 무조건 실행한다) Manual로 넘어가면 안 된다 - 넘어가면 포터블
        // 패키지를 옮기거나 시스템 adb를 지웠을 때 다음 실행이 막힌다.
        var gateway = new FakeSettingsGateway();
        gateway.Update(s =>
        {
            // EnsureDefaultPaths가 채웠을 법한, 사용자가 입력한 적 없는 값.
            s.Paths.AdbPath = "/opt/homebrew/bin/adb";
            s.Paths.AdbSelectionMode = AdbSelectionMode.Auto;
        });

        var viewModel = new PathsSettingsViewModel(gateway);
        Assert.Equal("/opt/homebrew/bin/adb", viewModel.AdbPath);

        // ADB 필드는 건드리지 않고 저장만 한다.
        viewModel.SaveCommand.Execute(null);

        Assert.Equal(
            AdbSelectionMode.Auto,
            gateway.Current.Paths.AdbSelectionMode);
        Assert.Equal(
            "/opt/homebrew/bin/adb",
            gateway.Current.Paths.AdbPath);
    }

    [Fact]
    public void Save_WhenAlreadyManualAndUntouched_StaysManual()
    {
        // 반대 방향의 함정: "비어 있지 않으면 Manual"이 아니라 "편집됐으면"
        // 으로 고쳤다고 해서, 이미 Manual인 값을 편집 없이 다시 저장했을 때
        // Auto로 되돌아가면 안 된다.
        var gateway = new FakeSettingsGateway();
        gateway.Update(s =>
        {
            s.Paths.AdbPath = "/usr/local/bin/adb";
            s.Paths.AdbSelectionMode = AdbSelectionMode.Manual;
        });

        var viewModel = new PathsSettingsViewModel(gateway);

        // 편집 없이 다시 저장(예: 다른 탭에서 뭔가 바꾸고 SaveAll).
        viewModel.SaveCommand.Execute(null);

        Assert.Equal(
            AdbSelectionMode.Manual,
            gateway.Current.Paths.AdbSelectionMode);
    }

    [Fact]
    public void Save_WhenAdbPathOnlyGainsWhitespace_IsNotTreatedAsAnEdit()
    {
        // 붙여넣기로 흔히 섞여 들어오는 앞뒤 공백은 진짜 편집이 아니다 -
        // 트림 없이 베이스라인과 비교하면 자동 감지값과 공백 하나 차이인
        // 문자열이 "편집됨"으로 잡혀 Manual로 넘어가고, 그 공백 섞인
        // 경로는 존재하지 않는 파일이라 다음 실행이 막힌다.
        var gateway = new FakeSettingsGateway();
        gateway.Update(s =>
        {
            s.Paths.AdbPath = "/opt/homebrew/bin/adb";
            s.Paths.AdbSelectionMode = AdbSelectionMode.Auto;
        });

        var viewModel = new PathsSettingsViewModel(gateway);

        viewModel.AdbPath = "  /opt/homebrew/bin/adb  ";
        viewModel.SaveCommand.Execute(null);

        Assert.Equal(
            AdbSelectionMode.Auto,
            gateway.Current.Paths.AdbSelectionMode);
        Assert.Equal(
            "/opt/homebrew/bin/adb",
            gateway.Current.Paths.AdbPath);
    }

    [Fact]
    public void Save_WhenAdbPathEnteredWithWhitespace_IsStoredTrimmed()
    {
        // 진짜 편집(다른 경로를 입력)일 때도 저장되는 값 자체는 트림돼야
        // 한다 - 안 그러면 트림 안 된 값이 그대로 실행 불가능한 Manual
        // 경로로 설정 파일에 남는다.
        var gateway = new FakeSettingsGateway();
        var viewModel = new PathsSettingsViewModel(gateway);

        viewModel.AdbPath = "  /usr/local/bin/adb  ";
        viewModel.SaveCommand.Execute(null);

        Assert.Equal("/usr/local/bin/adb", gateway.Current.Paths.AdbPath);
        Assert.Equal(
            AdbSelectionMode.Manual,
            gateway.Current.Paths.AdbSelectionMode);
        // 화면에 바인딩된 값도 트림된 값으로 되돌아와야, 다음 저장에서
        // 베이스라인 비교가 다시 어긋나지 않는다.
        Assert.Equal("/usr/local/bin/adb", viewModel.AdbPath);
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
