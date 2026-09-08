using Xunit;
using DexManager.Models;

namespace DexManager.ViewModels.Tests.Settings;

/// <summary>
/// SlotSettingsViewModel의 저장·검증·복사 격리 동작을 검증한다.
/// 단일창 슬롯(1~3) 기본값 - 앱 패키지, 해상도 override, StayAwake 등 -
/// 을 화면에서 편집하고 기기 프로필의 SingleWindowSlots에 영구 저장할
/// 수 있는지가 핵심이다. Phase 2의 SingleWindowSlotViewModel(실행용)과는
/// 다른 타입이며, 이 테스트는 그것을 건드리지 않는다.
/// </summary>
public class SlotSettingsViewModelTests
{
    private const string DeviceIdentity = "device-slot-test";

    [Fact]
    public void Constructor_ExposesThreeSlotsInOrder()
    {
        // 슬롯은 1~3 고정이며, Slots는 항상 그 순서대로 3개를 노출한다.
        var gateway = new FakeSettingsGateway();
        var viewModel = new SlotSettingsViewModel(DeviceIdentity, gateway);

        Assert.Equal(3, viewModel.Values.Slots.Count);
        Assert.Equal(1, viewModel.Values.Slots[0].Slot);
        Assert.Equal(2, viewModel.Values.Slots[1].Slot);
        Assert.Equal(3, viewModel.Values.Slots[2].Slot);
    }

    [Fact]
    public void Save_WhenValid_WritesSlotValuesIntoDeviceProfile()
    {
        // 유효한 값으로 저장하면 편집한 슬롯 값이 기기 프로필의
        // SingleWindowSlots에 실제로 대입된다.
        var gateway = new FakeSettingsGateway();
        var viewModel = new SlotSettingsViewModel(DeviceIdentity, gateway);

        var slot2 = viewModel.Values.Slots[1];
        slot2.Width = 1920;
        slot2.Height = 1080;
        slot2.Dpi = 320;
        slot2.BitRate = "12M";
        slot2.MaxFps = 90;
        slot2.TurnScreenOff = false;
        slot2.StayAwake = false;
        slot2.StartAppPackage = "com.example.app";
        slot2.StartAppName = "Example App";

        viewModel.SaveCommand.Execute(null);

        var profile = gateway.GetRunProfile(DeviceIdentity);
        var savedSlot2 = profile.SingleWindowSlots.First(s => s.Slot == 2);
        Assert.Equal(1920, savedSlot2.Width);
        Assert.Equal(1080, savedSlot2.Height);
        Assert.Equal(320, savedSlot2.Dpi);
        Assert.Equal("12M", savedSlot2.BitRate);
        Assert.Equal(90, savedSlot2.MaxFps);
        Assert.False(savedSlot2.TurnScreenOff);
        Assert.False(savedSlot2.StayAwake);
        Assert.Equal("com.example.app", savedSlot2.StartAppPackage);
        Assert.Equal("Example App", savedSlot2.StartAppName);
        Assert.Equal(1, gateway.UpdateCallCount);
    }

    [Fact]
    public void Save_DoesNotModifyDisplayStreamValuesOnProfile()
    {
        // 슬롯 저장은 슬롯 데이터만 대입해야 한다 - VirtualDisplay/Scrcpy
        // 값은 이 페이지의 책임 범위 밖이므로 건드리지 않는다.
        var gateway = new FakeSettingsGateway();
        var profileBefore = gateway.GetRunProfile(DeviceIdentity);
        var widthBefore = profileBefore.VirtualDisplay.Width;
        var bitRateBefore = profileBefore.Scrcpy.BitRate;

        var viewModel = new SlotSettingsViewModel(DeviceIdentity, gateway);
        viewModel.Values.Slots[0].Width = 1234;
        viewModel.SaveCommand.Execute(null);

        var profileAfter = gateway.GetRunProfile(DeviceIdentity);
        Assert.Equal(widthBefore, profileAfter.VirtualDisplay.Width);
        Assert.Equal(bitRateBefore, profileAfter.Scrcpy.BitRate);
    }

    [Fact]
    public void InvalidWidthOnASlot_BlocksSaveCommandAndDoesNotWriteProfile()
    {
        // 슬롯 하나라도 Width가 0 이하면 IsValid가 false여야 하고
        // SaveCommand.CanExecute도 false여야 한다. 바인딩을 우회해 강제로
        // Execute해도 Save 내부 가드가 프로필 대입을 막아야 한다.
        var gateway = new FakeSettingsGateway();
        var viewModel = new SlotSettingsViewModel(DeviceIdentity, gateway);

        var profileBefore = gateway.GetRunProfile(DeviceIdentity);
        var slot1WidthBefore = profileBefore.SingleWindowSlots
            .First(s => s.Slot == 1).Width;

        viewModel.Values.Slots[0].Width = 0;

        Assert.False(viewModel.IsValid);
        Assert.False(string.IsNullOrEmpty(viewModel.ValidationMessage));
        Assert.False(viewModel.SaveCommand.CanExecute(null));

        // 바인딩을 우회한 강제 실행 - Save 내부 가드가 막아야 한다.
        viewModel.SaveCommand.Execute(null);

        Assert.Equal(0, gateway.UpdateCallCount);
        var profileAfter = gateway.GetRunProfile(DeviceIdentity);
        Assert.Equal(
            slot1WidthBefore,
            profileAfter.SingleWindowSlots.First(s => s.Slot == 1).Width);
    }

    [Fact]
    public void InvalidBitRateOnASlot_BlocksSaveCommand()
    {
        // 슬롯 하나라도 BitRate가 \d+[MK]? 형식이 아니면 저장이 막힌다.
        var gateway = new FakeSettingsGateway();
        var viewModel = new SlotSettingsViewModel(DeviceIdentity, gateway);

        viewModel.Values.Slots[2].BitRate = "abc";

        Assert.False(viewModel.IsValid);
        Assert.False(viewModel.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void FromProfile_IsolatesFromSourceMutation_SlotValues()
    {
        // 로드 후 원본 프로필의 슬롯을 바꿔도 편집 사본에는 영향이 없어야
        // 한다 - 참조 공유가 아니라 값 복사여야 한다.
        var gateway = new FakeSettingsGateway();
        var viewModel = new SlotSettingsViewModel(DeviceIdentity, gateway);

        var profile = gateway.GetRunProfile(DeviceIdentity);
        var sourceSlot1 = profile.SingleWindowSlots.First(s => s.Slot == 1);
        var originalWidth = viewModel.Values.Slots[0].Width;

        sourceSlot1.Width = originalWidth + 999;
        sourceSlot1.StartAppPackage = "mutated.after.load";

        Assert.Equal(originalWidth, viewModel.Values.Slots[0].Width);
        Assert.NotEqual(
            "mutated.after.load",
            viewModel.Values.Slots[0].StartAppPackage);
    }

    [Fact]
    public void HasChanges_FalseInitially_TrueAfterEdit_FalseAfterSave()
    {
        // HasChanges는 로드 직후 false, 편집 후 true, 저장 후 다시 false다.
        var gateway = new FakeSettingsGateway();
        var viewModel = new SlotSettingsViewModel(DeviceIdentity, gateway);

        Assert.False(viewModel.HasChanges);

        viewModel.Values.Slots[0].MaxFps = viewModel.Values.Slots[0].MaxFps + 1;

        Assert.True(viewModel.HasChanges);

        viewModel.SaveCommand.Execute(null);

        Assert.False(viewModel.HasChanges);
    }
}
