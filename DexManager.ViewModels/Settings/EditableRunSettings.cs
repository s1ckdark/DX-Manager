using CommunityToolkit.Mvvm.ComponentModel;
using DexManager.Models;

namespace DexManager.ViewModels;

/// <summary>
/// 기기 실행 설정의 편집 가능한 사본. 원본 프로필에 직접 바인딩하면
/// 저장 시 EnsureDefaults() 정규화가 편집 중에 실행되어 화면이 변해버린다.
/// 이를 방지하기 위해 원본 프로필의 값을 **복사**하고, 저장 시 ApplyTo로
/// 편집값을 되돌려 넣는다. 되돌리기는 UpdateSettings 잠금 안에서 일어나므로
/// 정규화는 대입 뒤에 실행된다.
/// </summary>
public sealed partial class EditableRunSettings : ObservableObject
{
    /// <summary>
    /// 가상 디스플레이 너비 (px).
    /// </summary>
    [ObservableProperty]
    private int _width;

    /// <summary>
    /// 가상 디스플레이 높이 (px).
    /// </summary>
    [ObservableProperty]
    private int _height;

    /// <summary>
    /// 가상 디스플레이 DPI.
    /// </summary>
    [ObservableProperty]
    private int _dpi;

    /// <summary>
    /// scrcpy 비트레이트 (예: "8M").
    /// </summary>
    [ObservableProperty]
    private string _bitRate;

    /// <summary>
    /// scrcpy 최대 프레임율.
    /// </summary>
    [ObservableProperty]
    private int _maxFps;

    /// <summary>
    /// scrcpy 화면 끄기 플래그.
    /// </summary>
    [ObservableProperty]
    private bool _turnScreenOff;

    /// <summary>
    /// scrcpy 화면 켜기 유지 플래그.
    /// </summary>
    [ObservableProperty]
    private bool _stayAwake;

    private EditableRunSettings()
    {
    }

    /// <summary>
    /// 프로필의 값을 복사하여 새 EditableRunSettings를 만든다.
    /// 복사 이후 원본 프로필을 변경해도 이 사본에는 영향이 없다.
    /// </summary>
    /// <param name="profile">원본 프로필.</param>
    /// <returns>값이 복사된 새 EditableRunSettings.</returns>
    public static EditableRunSettings FromProfile(DeviceRunSettingsProfile profile)
    {
        if (profile == null)
            throw new ArgumentNullException(nameof(profile));

        var editable = new EditableRunSettings();

        // VirtualDisplaySettings 필드 복사
        if (profile.VirtualDisplay != null)
        {
            editable.Width = profile.VirtualDisplay.Width;
            editable.Height = profile.VirtualDisplay.Height;
            editable.Dpi = profile.VirtualDisplay.Dpi;
        }

        // ScrcpySettings 필드 복사
        if (profile.Scrcpy != null)
        {
            editable.BitRate = profile.Scrcpy.BitRate ?? string.Empty;
            editable.MaxFps = profile.Scrcpy.MaxFps;
            editable.TurnScreenOff = profile.Scrcpy.TurnScreenOff;
            editable.StayAwake = profile.Scrcpy.StayAwake;
        }

        return editable;
    }

    /// <summary>
    /// 이 사본의 편집값을 프로필에 되돌려 쓴다.
    /// </summary>
    /// <param name="target">대상 프로필.</param>
    public void ApplyTo(DeviceRunSettingsProfile target)
    {
        if (target == null)
            throw new ArgumentNullException(nameof(target));

        // VirtualDisplaySettings 필드 대입
        if (target.VirtualDisplay == null)
            target.VirtualDisplay = new VirtualDisplaySettings();

        target.VirtualDisplay.Width = Width;
        target.VirtualDisplay.Height = Height;
        target.VirtualDisplay.Dpi = Dpi;

        // ScrcpySettings 필드 대입
        if (target.Scrcpy == null)
            target.Scrcpy = new ScrcpySettings();

        target.Scrcpy.BitRate = BitRate ?? string.Empty;
        target.Scrcpy.MaxFps = MaxFps;
        target.Scrcpy.TurnScreenOff = TurnScreenOff;
        target.Scrcpy.StayAwake = StayAwake;
    }
}
