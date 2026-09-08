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

    /// <summary>
    /// 단일창 슬롯 1~3의 편집 가능한 사본. 항상 슬롯 번호 1, 2, 3 순서로
    /// 정확히 3개를 담는다 - SingleWindowService.Start가 범위 밖 슬롯을
    /// 거부하므로 이 화면도 3개 고정으로만 다룬다.
    /// </summary>
    public IReadOnlyList<EditableSingleWindowSlot> Slots { get; private set; }
        = Array.Empty<EditableSingleWindowSlot>();

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

        // SingleWindowSlotSettings 필드 복사 - 슬롯 1~3을 번호로 찾아
        // 각각의 값을 복사한다. 원본에 슬롯이 비어 있어도(예: 정규화
        // 이전 프로필) 기본값(0/빈 문자열)으로 채운 사본을 만든다.
        var slots = new List<EditableSingleWindowSlot>(3);
        for (var slotNumber = 1; slotNumber <= 3; slotNumber++)
        {
            var sourceSlot = profile.SingleWindowSlots?
                .FirstOrDefault(s => s != null && s.Slot == slotNumber);
            slots.Add(EditableSingleWindowSlot.FromSlot(slotNumber, sourceSlot));
        }

        editable.Slots = slots;

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

    /// <summary>
    /// 슬롯 1~3의 편집값만 프로필의 SingleWindowSlots에 되돌려 쓴다.
    /// VirtualDisplay/Scrcpy는 건드리지 않는다 - 슬롯 설정 화면은 그
    /// 값들을 편집하지 않으므로, 이 메서드가 그 값까지 대입하면 다른
    /// 화면(DisplayStreamSettingsViewModel)에서 아직 저장하지 않은
    /// 편집이 이 저장으로 덮여 사라질 위험이 있다. 그래서 ApplyTo(전체)와
    /// 별도로 슬롯만 쓰는 이 메서드를 둔다.
    /// </summary>
    /// <param name="target">대상 프로필.</param>
    public void ApplySlotsTo(DeviceRunSettingsProfile target)
    {
        if (target == null)
            throw new ArgumentNullException(nameof(target));

        if (target.SingleWindowSlots == null)
            target.SingleWindowSlots = new List<SingleWindowSlotSettings>();

        foreach (var editableSlot in Slots)
        {
            var targetSlot = target.SingleWindowSlots
                .FirstOrDefault(s => s != null && s.Slot == editableSlot.Slot);
            if (targetSlot == null)
            {
                targetSlot = new SingleWindowSlotSettings();
                target.SingleWindowSlots.Add(targetSlot);
            }

            editableSlot.ApplyTo(targetSlot);
        }
    }
}

/// <summary>
/// 단일창 슬롯 하나(1~3)의 편집 가능한 사본. EditableRunSettings의
/// 스칼라 값들과 같은 이유로 원본 SingleWindowSlotSettings를 복사해
/// 쓴다 - 저장 시 EnsureDefaults 정규화가 편집 중에 실행되어 화면이
/// 바뀌어 버리는 것을 막기 위해서다.
/// </summary>
public sealed partial class EditableSingleWindowSlot : ObservableObject
{
    /// <summary>슬롯 번호 (1~3 고정). 편집 대상이 아니라 식별자다.</summary>
    public int Slot { get; }

    /// <summary>가상 디스플레이 너비 override (px).</summary>
    [ObservableProperty]
    private int _width;

    /// <summary>가상 디스플레이 높이 override (px).</summary>
    [ObservableProperty]
    private int _height;

    /// <summary>가상 디스플레이 DPI override.</summary>
    [ObservableProperty]
    private int _dpi;

    /// <summary>scrcpy 비트레이트 override (예: "8M").</summary>
    [ObservableProperty]
    private string _bitRate;

    /// <summary>scrcpy 최대 프레임율 override.</summary>
    [ObservableProperty]
    private int _maxFps;

    /// <summary>scrcpy 화면 끄기 플래그.</summary>
    [ObservableProperty]
    private bool _turnScreenOff;

    /// <summary>scrcpy 화면 켜기 유지 플래그.</summary>
    [ObservableProperty]
    private bool _stayAwake;

    /// <summary>HID 키보드 사용 여부.</summary>
    [ObservableProperty]
    private bool _useHidKeyboard;

    /// <summary>HID 마우스 사용 여부.</summary>
    [ObservableProperty]
    private bool _useHidMouse;

    /// <summary>시작 전 앱을 강제 종료할지 여부.</summary>
    [ObservableProperty]
    private bool _forceStopStartApp;

    /// <summary>이 슬롯에서 실행할 Android 앱 패키지.</summary>
    [ObservableProperty]
    private string _startAppPackage;

    /// <summary>화면에 표시할 앱 이름.</summary>
    [ObservableProperty]
    private string _startAppName;

    /// <summary>scrcpy에 추가로 전달할 인자.</summary>
    [ObservableProperty]
    private string _additionalArguments;

    /// <summary>사용자 지정 너비 (px).</summary>
    [ObservableProperty]
    private int _customWidth;

    /// <summary>사용자 지정 높이 (px).</summary>
    [ObservableProperty]
    private int _customHeight;

    /// <summary>Flex Display(-x) 사용 여부.</summary>
    [ObservableProperty]
    private bool _flexDisplay;

    private EditableSingleWindowSlot(int slot)
    {
        Slot = slot;
    }

    /// <summary>
    /// 슬롯 번호와 원본 SingleWindowSlotSettings의 값을 복사하여 새
    /// EditableSingleWindowSlot을 만든다. source가 null이면(정규화 이전
    /// 프로필 등) 기본값(0/빈 문자열)으로 채운다. 복사 이후 원본을
    /// 변경해도 이 사본에는 영향이 없다.
    /// </summary>
    /// <param name="slotNumber">슬롯 번호 (1~3).</param>
    /// <param name="source">원본 슬롯 설정. null이면 기본값 사용.</param>
    /// <returns>값이 복사된 새 EditableSingleWindowSlot.</returns>
    public static EditableSingleWindowSlot FromSlot(
        int slotNumber,
        SingleWindowSlotSettings source)
    {
        var editable = new EditableSingleWindowSlot(slotNumber);

        if (source != null)
        {
            editable.Width = source.Width;
            editable.Height = source.Height;
            editable.Dpi = source.Dpi;
            editable.BitRate = source.BitRate ?? string.Empty;
            editable.MaxFps = source.MaxFps;
            editable.TurnScreenOff = source.TurnScreenOff;
            editable.StayAwake = source.StayAwake;
            editable.UseHidKeyboard = source.UseHidKeyboard;
            editable.UseHidMouse = source.UseHidMouse;
            editable.ForceStopStartApp = source.ForceStopStartApp;
            editable.StartAppPackage = source.StartAppPackage ?? string.Empty;
            editable.StartAppName = source.StartAppName ?? string.Empty;
            editable.AdditionalArguments = source.AdditionalArguments ?? string.Empty;
            editable.CustomWidth = source.CustomWidth;
            editable.CustomHeight = source.CustomHeight;
            editable.FlexDisplay = source.FlexDisplay;
        }
        else
        {
            editable.BitRate = string.Empty;
            editable.StartAppPackage = string.Empty;
            editable.StartAppName = string.Empty;
            editable.AdditionalArguments = string.Empty;
        }

        return editable;
    }

    /// <summary>
    /// 이 사본의 편집값을 대상 SingleWindowSlotSettings에 되돌려 쓴다.
    /// 슬롯 번호도 함께 대입해 새로 만들어진 대상이라도 올바른 번호를
    /// 갖도록 한다.
    /// </summary>
    /// <param name="target">대상 슬롯 설정.</param>
    public void ApplyTo(SingleWindowSlotSettings target)
    {
        if (target == null)
            throw new ArgumentNullException(nameof(target));

        target.Slot = Slot;
        target.Width = Width;
        target.Height = Height;
        target.Dpi = Dpi;
        target.BitRate = BitRate ?? string.Empty;
        target.MaxFps = MaxFps;
        target.TurnScreenOff = TurnScreenOff;
        target.StayAwake = StayAwake;
        target.UseHidKeyboard = UseHidKeyboard;
        target.UseHidMouse = UseHidMouse;
        target.ForceStopStartApp = ForceStopStartApp;
        target.StartAppPackage = StartAppPackage ?? string.Empty;
        target.StartAppName = StartAppName ?? string.Empty;
        target.AdditionalArguments = AdditionalArguments ?? string.Empty;
        target.CustomWidth = CustomWidth;
        target.CustomHeight = CustomHeight;
        target.FlexDisplay = FlexDisplay;
    }
}
