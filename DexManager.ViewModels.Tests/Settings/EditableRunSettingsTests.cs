using Xunit;
using DexManager.Models;

namespace DexManager.ViewModels.Tests.Settings;

/// <summary>
/// EditableRunSettings 의 값 복사 및 되돌리기 기능을 검증한다.
/// 편집 중 원본 프로필이 정규화되지 않도록 사본과 원본이
/// 격리되어야 한다.
/// </summary>
public class EditableRunSettingsTests
{
    [Fact]
    public void FromProfile_CopiesVirtualDisplayValues()
    {
        // FromProfile이 VirtualDisplaySettings 필드들을 정확히 복사한다.
        var profile = CreateProfileWithCustomValues();
        profile.VirtualDisplay.Width = 1280;
        profile.VirtualDisplay.Height = 720;
        profile.VirtualDisplay.Dpi = 240;

        var editable = EditableRunSettings.FromProfile(profile);

        Assert.Equal(1280, editable.Width);
        Assert.Equal(720, editable.Height);
        Assert.Equal(240, editable.Dpi);
    }

    [Fact]
    public void FromProfile_CopiesScrcpyValues()
    {
        // FromProfile이 ScrcpySettings 필드들을 정확히 복사한다.
        var profile = CreateProfileWithCustomValues();
        profile.Scrcpy.BitRate = "8M";
        profile.Scrcpy.MaxFps = 60;
        profile.Scrcpy.TurnScreenOff = true;
        profile.Scrcpy.StayAwake = true;

        var editable = EditableRunSettings.FromProfile(profile);

        Assert.Equal("8M", editable.BitRate);
        Assert.Equal(60, editable.MaxFps);
        Assert.True(editable.TurnScreenOff);
        Assert.True(editable.StayAwake);
    }

    [Fact]
    public void FromProfile_IsolatesFromSourceMutation_DisplayValues()
    {
        // FromProfile 후 원본 프로필의 VirtualDisplaySettings를 바꿔도
        // 사본에는 영향이 없다. 참조 공유가 아니라 값 복사여야 한다.
        var profile = CreateProfileWithCustomValues();
        profile.VirtualDisplay.Width = 1280;
        profile.VirtualDisplay.Height = 720;
        profile.VirtualDisplay.Dpi = 240;

        var editable = EditableRunSettings.FromProfile(profile);

        // 원본 프로필을 수정한다.
        profile.VirtualDisplay.Width = 1920;
        profile.VirtualDisplay.Height = 1080;
        profile.VirtualDisplay.Dpi = 420;

        // 사본은 여전히 원래 값을 가지고 있어야 한다.
        Assert.Equal(1280, editable.Width);
        Assert.Equal(720, editable.Height);
        Assert.Equal(240, editable.Dpi);
    }

    [Fact]
    public void FromProfile_IsolatesFromSourceMutation_ScrcpyValues()
    {
        // FromProfile 후 원본 프로필의 ScrcpySettings를 바꿔도
        // 사본에는 영향이 없다.
        var profile = CreateProfileWithCustomValues();
        profile.Scrcpy.BitRate = "8M";
        profile.Scrcpy.MaxFps = 60;
        profile.Scrcpy.TurnScreenOff = true;
        profile.Scrcpy.StayAwake = true;

        var editable = EditableRunSettings.FromProfile(profile);

        // 원본 프로필을 수정한다.
        profile.Scrcpy.BitRate = "4M";
        profile.Scrcpy.MaxFps = 30;
        profile.Scrcpy.TurnScreenOff = false;
        profile.Scrcpy.StayAwake = false;

        // 사본은 여전히 원래 값을 가지고 있어야 한다.
        Assert.Equal("8M", editable.BitRate);
        Assert.Equal(60, editable.MaxFps);
        Assert.True(editable.TurnScreenOff);
        Assert.True(editable.StayAwake);
    }

    [Fact]
    public void ApplyTo_WritesBackVirtualDisplayValues()
    {
        // ApplyTo가 편집값을 프로필에 되돌려 쓴다 (Display).
        var profile = CreateProfileWithCustomValues();
        var editable = EditableRunSettings.FromProfile(profile);

        // 사본을 수정한다.
        editable.Width = 1920;
        editable.Height = 1080;
        editable.Dpi = 420;

        // ApplyTo로 되돌려 쓴다.
        editable.ApplyTo(profile);

        // 프로필이 변경되었는지 확인한다.
        Assert.Equal(1920, profile.VirtualDisplay.Width);
        Assert.Equal(1080, profile.VirtualDisplay.Height);
        Assert.Equal(420, profile.VirtualDisplay.Dpi);
    }

    [Fact]
    public void ApplyTo_WritesBackScrcpyValues()
    {
        // ApplyTo가 편집값을 프로필에 되돌려 쓴다 (Scrcpy).
        var profile = CreateProfileWithCustomValues();
        var editable = EditableRunSettings.FromProfile(profile);

        // 사본을 수정한다.
        editable.BitRate = "4M";
        editable.MaxFps = 30;
        editable.TurnScreenOff = false;
        editable.StayAwake = false;

        // ApplyTo로 되돌려 쓴다.
        editable.ApplyTo(profile);

        // 프로필이 변경되었는지 확인한다.
        Assert.Equal("4M", profile.Scrcpy.BitRate);
        Assert.Equal(30, profile.Scrcpy.MaxFps);
        Assert.False(profile.Scrcpy.TurnScreenOff);
        Assert.False(profile.Scrcpy.StayAwake);
    }

    private DeviceRunSettingsProfile CreateProfileWithCustomValues()
    {
        var profile = new DeviceRunSettingsProfile
        {
            DeviceIdentity = "test-device",
            VirtualDisplay = new VirtualDisplaySettings
            {
                Width = 800,
                Height = 600,
                Dpi = 120,
                Suffix = string.Empty,
                ReuseExistingDisplay = false,
                CustomWidth = 0,
                CustomHeight = 0
            },
            Scrcpy = new ScrcpySettings
            {
                BitRate = "2M",
                MaxFps = 30,
                WindowTitle = string.Empty,
                TurnScreenOff = false,
                UseHidKeyboard = false,
                UseHidMouse = false,
                ForceStopStartApp = false,
                StartAppPackage = string.Empty,
                AdditionalArguments = string.Empty,
                StayAwake = false,
                StartAppName = string.Empty
            },
            LastSuccess = new LastSuccessSettings(),
            SingleWindowSlots = new List<SingleWindowSlotSettings>(),
            SingleWindowAppProfiles = new List<SingleWindowAppProfile>()
        };
        return profile;
    }
}
