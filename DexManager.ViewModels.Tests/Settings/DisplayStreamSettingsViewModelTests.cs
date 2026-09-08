using Xunit;
using DexManager.Models;

namespace DexManager.ViewModels.Tests.Settings;

/// <summary>
/// DisplayStreamSettingsViewModel의 저장·검증·재설정 동작을 검증한다.
/// Phase 2에서 JSON을 직접 고쳐야 했던 화면 크기/DPI/비트레이트 값을
/// 화면에서 편집·저장할 수 있는지가 핵심이다.
/// </summary>
public class DisplayStreamSettingsViewModelTests
{
    private const string DeviceIdentity = "device-abc";

    [Fact]
    public void Save_WhenValid_WritesValuesIntoDeviceProfile()
    {
        // 유효한 값으로 저장하면 편집값이 기기 프로필에 실제로 대입된다.
        var gateway = new FakeSettingsGateway();
        var viewModel = new DisplayStreamSettingsViewModel(DeviceIdentity, gateway);

        viewModel.Values.Width = 2560;
        viewModel.Values.Height = 1440;
        viewModel.Values.Dpi = 240;
        viewModel.Values.BitRate = "16M";
        viewModel.Values.MaxFps = 60;

        viewModel.SaveCommand.Execute(null);

        var profile = gateway.GetRunProfile(DeviceIdentity);
        Assert.Equal(2560, profile.VirtualDisplay.Width);
        Assert.Equal(1440, profile.VirtualDisplay.Height);
        Assert.Equal(240, profile.VirtualDisplay.Dpi);
        Assert.Equal("16M", profile.Scrcpy.BitRate);
        Assert.Equal(60, profile.Scrcpy.MaxFps);
        Assert.Equal(1, gateway.UpdateCallCount);
    }

    [Fact]
    public void InvalidDpi_BlocksSaveCommandAndDoesNotWriteProfile()
    {
        // Dpi가 0 이하이면 IsValid가 false여야 하고, SaveCommand.CanExecute도
        // false여야 한다. 바인딩을 우회해 강제로 Execute를 호출해도
        // Save 메서드 내부 가드가 프로필 대입을 막아야 한다.
        var gateway = new FakeSettingsGateway();
        var viewModel = new DisplayStreamSettingsViewModel(DeviceIdentity, gateway);

        var profileBefore = gateway.GetRunProfile(DeviceIdentity);
        var widthBefore = profileBefore.VirtualDisplay.Width;

        viewModel.Values.Dpi = 0;

        Assert.False(viewModel.IsValid);
        Assert.False(string.IsNullOrEmpty(viewModel.ValidationMessage));
        Assert.False(viewModel.SaveCommand.CanExecute(null));

        // 바인딩을 우회한 강제 실행 - Save 내부 가드가 막아야 한다.
        viewModel.SaveCommand.Execute(null);

        Assert.Equal(0, gateway.UpdateCallCount);
        var profileAfter = gateway.GetRunProfile(DeviceIdentity);
        Assert.Equal(widthBefore, profileAfter.VirtualDisplay.Width);
    }

    [Fact]
    public void InvalidBitRate_BlocksSaveCommand()
    {
        // BitRate가 \d+[MK]? 형식이 아니면 저장이 막힌다.
        var gateway = new FakeSettingsGateway();
        var viewModel = new DisplayStreamSettingsViewModel(DeviceIdentity, gateway);

        viewModel.Values.BitRate = "abc";

        Assert.False(viewModel.IsValid);
        Assert.False(viewModel.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void ResetToGlobalDefaults_RepopulatesValuesFromGlobalSettings()
    {
        // 재설정은 편집값을 전역 VirtualDisplay/Scrcpy 값으로 다시 채운다.
        var gateway = new FakeSettingsGateway();
        var globalVirtualDisplay = gateway.Current.VirtualDisplay;
        var globalScrcpy = gateway.Current.Scrcpy;

        var viewModel = new DisplayStreamSettingsViewModel(DeviceIdentity, gateway);

        // 편집값을 전역 기본값과 다르게 바꿔둔다.
        viewModel.Values.Width = globalVirtualDisplay.Width + 111;
        viewModel.Values.Height = globalVirtualDisplay.Height + 222;
        viewModel.Values.Dpi = globalVirtualDisplay.Dpi + 10;
        viewModel.Values.BitRate = "1M";
        viewModel.Values.MaxFps = globalScrcpy.MaxFps + 5;
        viewModel.Values.TurnScreenOff = !globalScrcpy.TurnScreenOff;
        viewModel.Values.StayAwake = !globalScrcpy.StayAwake;

        viewModel.ResetToGlobalDefaultsCommand.Execute(null);

        Assert.Equal(globalVirtualDisplay.Width, viewModel.Values.Width);
        Assert.Equal(globalVirtualDisplay.Height, viewModel.Values.Height);
        Assert.Equal(globalVirtualDisplay.Dpi, viewModel.Values.Dpi);
        Assert.Equal(globalScrcpy.BitRate, viewModel.Values.BitRate);
        Assert.Equal(globalScrcpy.MaxFps, viewModel.Values.MaxFps);
        Assert.Equal(globalScrcpy.TurnScreenOff, viewModel.Values.TurnScreenOff);
        Assert.Equal(globalScrcpy.StayAwake, viewModel.Values.StayAwake);
    }

    [Fact]
    public void HasChanges_FalseInitially_TrueAfterEdit_FalseAfterSave()
    {
        // HasChanges는 로드 직후 false, 편집 후 true, 저장 후 다시 false다.
        var gateway = new FakeSettingsGateway();
        var viewModel = new DisplayStreamSettingsViewModel(DeviceIdentity, gateway);

        Assert.False(viewModel.HasChanges);

        viewModel.Values.MaxFps = viewModel.Values.MaxFps + 1;

        Assert.True(viewModel.HasChanges);

        viewModel.SaveCommand.Execute(null);

        Assert.False(viewModel.HasChanges);
    }
}
