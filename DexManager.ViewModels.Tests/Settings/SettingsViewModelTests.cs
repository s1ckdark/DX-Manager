using Xunit;
using DexManager.Models;

namespace DexManager.ViewModels.Tests.Settings;

/// <summary>
/// SettingsViewModel의 조립(다섯 페이지), 기기 선택 추적, SaveAll/Cancel
/// 조율, 집계 HasChanges를 검증한다.
/// </summary>
public class SettingsViewModelTests
{
    [Fact]
    public void Constructor_WithNoDeviceSelected_LeavesPerDevicePagesNull()
    {
        // 선택된 기기가 없으면(identity == null) 기기별 페이지는 null이어야
        // 한다 - 전역 페이지 3개는 항상 준비되어 있어야 한다.
        var gateway = new FakeSettingsGateway();
        var deviceSelection = new FakeDeviceSelectionSource();

        var settings = new SettingsViewModel(gateway, deviceSelection);

        Assert.Null(settings.DisplayStream);
        Assert.Null(settings.Slot);
        Assert.NotNull(settings.Paths);
        Assert.NotNull(settings.Appearance);
        Assert.NotNull(settings.Interaction);
    }

    [Fact]
    public void Constructor_WithDeviceSelected_LoadsPerDevicePagesForThatIdentity()
    {
        var gateway = new FakeSettingsGateway();
        var deviceSelection = new FakeDeviceSelectionSource { SelectedIdentity = "device-a" };

        var settings = new SettingsViewModel(gateway, deviceSelection);

        Assert.NotNull(settings.DisplayStream);
        Assert.NotNull(settings.Slot);
    }

    [Fact]
    public void DeviceSelectionChanged_ReloadsPerDevicePagesWithTheNewProfile()
    {
        // device-a의 프로필 너비를 1111로 바꿔둔다. device-b는 기본값을
        // 유지한다. 선택을 device-a -> device-b로 바꾸면 DisplayStream이
        // device-b의 (기본) 값을 보여줘야 한다 - device-a의 값이 새 페이지에
        // 새어 들어오면 이 검증이 실패한다.
        var gateway = new FakeSettingsGateway();
        gateway.Update(s => s.GetOrCreateDeviceRunSettings("device-a").VirtualDisplay.Width = 1111);
        var defaultWidth = AppSettings.CreateDefault().VirtualDisplay.Width;

        var deviceSelection = new FakeDeviceSelectionSource { SelectedIdentity = "device-a" };
        var settings = new SettingsViewModel(gateway, deviceSelection);

        Assert.Equal(1111, settings.DisplayStream.Values.Width);
        var deviceAPage = settings.DisplayStream;

        deviceSelection.SelectedIdentity = "device-b";

        Assert.NotSame(deviceAPage, settings.DisplayStream);
        Assert.Equal(defaultWidth, settings.DisplayStream.Values.Width);
    }

    [Fact]
    public void DeviceSelectionChanged_ToNoDevice_ClearsPerDevicePages()
    {
        var gateway = new FakeSettingsGateway();
        var deviceSelection = new FakeDeviceSelectionSource { SelectedIdentity = "device-a" };
        var settings = new SettingsViewModel(gateway, deviceSelection);

        Assert.NotNull(settings.DisplayStream);

        deviceSelection.SelectedIdentity = null;

        Assert.Null(settings.DisplayStream);
        Assert.Null(settings.Slot);
    }

    [Fact]
    public void Cancel_DiscardsUnsavedEditsWithoutWritingTheGateway()
    {
        var gateway = new FakeSettingsGateway();
        var deviceSelection = new FakeDeviceSelectionSource();
        var settings = new SettingsViewModel(gateway, deviceSelection);

        var originalScrcpyPath = gateway.Current.Paths.ScrcpyPath;
        settings.Paths.ScrcpyPath = "/edited/scrcpy";
        Assert.True(settings.Paths.HasChanges);

        var updateCallsBeforeCancel = gateway.UpdateCallCount;

        settings.CancelCommand.Execute(null);

        Assert.Equal(updateCallsBeforeCancel, gateway.UpdateCallCount);
        Assert.Equal(originalScrcpyPath, gateway.Current.Paths.ScrcpyPath);
        // Cancel이 재로드한 새 Paths 인스턴스는 게이트웨이의 원본값을 반영한다.
        Assert.Equal(originalScrcpyPath, settings.Paths.ScrcpyPath);
        Assert.False(settings.HasChanges);
    }

    [Fact]
    public void SaveAll_FlowsEachPageOnceAndClearsAggregateHasChanges()
    {
        var gateway = new FakeSettingsGateway();
        var deviceSelection = new FakeDeviceSelectionSource { SelectedIdentity = "device-a" };
        var settings = new SettingsViewModel(gateway, deviceSelection);

        settings.Paths.ScrcpyPath = "/new/scrcpy";
        settings.DisplayStream.Values.Width = 1280;
        Assert.True(settings.HasChanges);

        var updateCallsBeforeSave = gateway.UpdateCallCount;

        settings.SaveAllCommand.Execute(null);

        // 다섯 페이지 각각 한 번씩만 Update를 호출한다.
        Assert.Equal(updateCallsBeforeSave + 5, gateway.UpdateCallCount);
        Assert.Equal("/new/scrcpy", gateway.Current.Paths.ScrcpyPath);
        Assert.Equal(1280, gateway.GetRunProfile("device-a").VirtualDisplay.Width);
        Assert.False(settings.HasChanges);
    }

    [Fact]
    public void SaveAll_DoesNotForceSaveAnInvalidPage()
    {
        // Interaction 페이지를 캡처/종료 단축키가 같은 잘못된 상태로
        // 만든다 - IsValid가 false가 되어 SaveCommand.CanExecute가 false다.
        // SaveAll은 이 페이지를 건너뛰어야 한다.
        var gateway = new FakeSettingsGateway();
        var originalCaptureHotkey = gateway.Current.KeyMappings.CaptureHotkey;
        var deviceSelection = new FakeDeviceSelectionSource();
        var settings = new SettingsViewModel(gateway, deviceSelection);

        settings.Interaction.CaptureHotkey = "F1";
        settings.Interaction.ExitHotkey = "F1";
        Assert.False(settings.Interaction.IsValid);
        Assert.False(settings.Interaction.SaveCommand.CanExecute(null));

        settings.Paths.ScrcpyPath = "/valid/edit";

        settings.SaveAllCommand.Execute(null);

        // 유효한 Paths는 저장됐지만, 유효하지 않은 Interaction의 편집은
        // 게이트웨이에 반영되지 않는다.
        Assert.Equal("/valid/edit", gateway.Current.Paths.ScrcpyPath);
        Assert.Equal(originalCaptureHotkey, gateway.Current.KeyMappings.CaptureHotkey);

        // Interaction은 여전히 저장되지 않은 편집을 갖고 있으므로 집계도 true.
        Assert.True(settings.Interaction.HasChanges);
        Assert.True(settings.HasChanges);
    }

    [Fact]
    public void HasChanges_AggregatesAcrossAllFivePagesAndRecomputesOnPageChange()
    {
        var gateway = new FakeSettingsGateway();
        var deviceSelection = new FakeDeviceSelectionSource { SelectedIdentity = "device-a" };
        var settings = new SettingsViewModel(gateway, deviceSelection);

        Assert.False(settings.HasChanges);

        settings.Slot.Values.Slots[0].Width = 999;
        Assert.True(settings.Slot.HasChanges);
        Assert.True(settings.HasChanges);

        settings.Slot.SaveCommand.Execute(null);
        Assert.False(settings.Slot.HasChanges);
        Assert.False(settings.HasChanges);

        settings.Appearance.SelectedTheme = settings.Appearance.SelectedTheme == AppTheme.Dark
            ? AppTheme.Light
            : AppTheme.Dark;
        Assert.True(settings.HasChanges);
    }
}
