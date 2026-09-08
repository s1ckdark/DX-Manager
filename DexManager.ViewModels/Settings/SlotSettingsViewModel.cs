using System.ComponentModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DexManager.ViewModels;

/// <summary>
/// 기기 1대의 단일창 슬롯(1~3) 기본값 편집 페이지 - 앱 패키지, 해상도
/// override, StayAwake 등을 편집해 기기 프로필에 영구 저장한다.
/// Phase 2의 SingleWindowSlotViewModel은 이미 떠 있는 슬롯을 시작/중지
/// 하는 **실행**용이고, 이 화면은 그 슬롯이 시작될 때 쓸 **기본값**을
/// 미리 편집·저장하는 **설정**용이다 - 서로 다른 타입, 다른 화면이며
/// 이 타입은 Phase 2의 것을 대체하거나 변경하지 않는다.
/// </summary>
public sealed partial class SlotSettingsViewModel : ObservableObject
{
    // \d+[MK]? : 숫자 뒤에 M(메가) 또는 K(킬로) 단위가 선택적으로 붙는
    // scrcpy 비트레이트 형식. DisplayStreamSettingsViewModel과 동일한
    // 형식을 슬롯별로 검증한다.
    private static readonly Regex BitRatePattern = new(
        @"^\d+[MK]?$",
        RegexOptions.Compiled);

    private readonly string _identity;
    private readonly ISettingsGateway _gateway;

    // 마지막으로 로드하거나 저장한 슬롯 값의 스냅샷. HasChanges 계산
    // 기준선이다. 슬롯 순서(1~3)와 나란히 저장한다.
    private readonly List<SlotBaseline> _baselines = new();

    /// <param name="identity">편집 대상 기기의 영속 식별자. 호출자가
    /// 선택된 기기의 Identity를 넘긴다(ApplicationHost.SelectedSerial을
    /// 직접 읽지 않는다).</param>
    /// <param name="gateway">설정 읽기·쓰기 경계.</param>
    public SlotSettingsViewModel(string identity, ISettingsGateway gateway)
    {
        if (string.IsNullOrEmpty(identity))
            throw new ArgumentException("Device identity is empty.", nameof(identity));

        _identity = identity;
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));

        var profile = _gateway.GetRunProfile(_identity);
        Values = EditableRunSettings.FromProfile(profile);

        foreach (var slot in Values.Slots)
            slot.PropertyChanged += OnSlotChanged;

        CaptureBaseline();
        Revalidate();
    }

    /// <summary>편집 중인 슬롯 1~3의 사본. 저장 전까지는 프로필에
    /// 반영되지 않는다.</summary>
    public EditableRunSettings Values { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isValid;

    [ObservableProperty]
    private string _validationMessage = string.Empty;

    /// <summary>마지막으로 불러오거나 저장한 값과 비교해 슬롯 중 하나라도
    /// 편집된 내용이 있는지.</summary>
    public bool HasChanges
    {
        get
        {
            for (var i = 0; i < Values.Slots.Count; i++)
            {
                if (_baselines[i].DiffersFrom(Values.Slots[i]))
                    return true;
            }

            return false;
        }
    }

    private void OnSlotChanged(object sender, PropertyChangedEventArgs e) => Revalidate();

    private void Revalidate()
    {
        var message = string.Empty;

        foreach (var slot in Values.Slots)
        {
            message = ValidateSlot(slot);
            if (!string.IsNullOrEmpty(message))
                break;
        }

        ValidationMessage = message;
        IsValid = string.IsNullOrEmpty(ValidationMessage);

        // HasChanges는 [ObservableProperty]가 아니라 계산 프로퍼티이므로
        // 슬롯 값이 바뀔 때마다 직접 변경 통지를 올려야 바인딩이 갱신된다.
        OnPropertyChanged(nameof(HasChanges));
    }

    private static string ValidateSlot(EditableSingleWindowSlot slot)
    {
        if (slot.Width <= 0)
            return $"슬롯 {slot.Slot}: 너비(Width)는 양의 정수여야 합니다.";
        if (slot.Height <= 0)
            return $"슬롯 {slot.Slot}: 높이(Height)는 양의 정수여야 합니다.";
        if (slot.Dpi <= 0)
            return $"슬롯 {slot.Slot}: DPI는 양의 정수여야 합니다.";
        if (slot.MaxFps <= 0)
            return $"슬롯 {slot.Slot}: 최대 프레임율(MaxFps)은 양의 정수여야 합니다.";
        if (!BitRatePattern.IsMatch(slot.BitRate ?? string.Empty))
            return $"슬롯 {slot.Slot}: 비트레이트는 숫자 뒤에 M 또는 K가 붙은 형식이어야 합니다 (예: 8M).";

        return string.Empty;
    }

    private void CaptureBaseline()
    {
        _baselines.Clear();
        foreach (var slot in Values.Slots)
            _baselines.Add(SlotBaseline.Capture(slot));
    }

    private bool CanSave() => IsValid;

    /// <summary>
    /// 편집한 슬롯 1~3의 값을 기기 프로필의 SingleWindowSlots에 저장한다.
    /// CanSave가 IsValid를 보고 바인딩된 버튼의 활성 여부를 결정하지만,
    /// 바인딩을 우회해 강제로 Execute가 호출되는 경우에도 저장이
    /// 일어나지 않도록 메서드 내부에서도 IsValid를 한 번 더 확인한다.
    /// VirtualDisplay/Scrcpy는 이 화면의 편집 대상이 아니므로
    /// ApplySlotsTo로 슬롯만 대입한다(EditableRunSettings.ApplyTo 참고).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        if (!IsValid) return;

        _gateway.Update(s => Values.ApplySlotsTo(s.GetOrCreateDeviceRunSettings(_identity)));

        CaptureBaseline();
        OnPropertyChanged(nameof(HasChanges));
    }

    /// <summary>
    /// 슬롯 값의 변경 여부를 판단하기 위한 스냅샷. HasChanges 계산에만
    /// 쓰이며 프로필에는 쓰이지 않는다.
    /// </summary>
    private sealed class SlotBaseline
    {
        private int _width;
        private int _height;
        private int _dpi;
        private string _bitRate = string.Empty;
        private int _maxFps;
        private bool _turnScreenOff;
        private bool _stayAwake;
        private bool _useHidKeyboard;
        private bool _useHidMouse;
        private bool _forceStopStartApp;
        private string _startAppPackage = string.Empty;
        private string _startAppName = string.Empty;
        private string _additionalArguments = string.Empty;
        private int _customWidth;
        private int _customHeight;
        private bool _flexDisplay;

        public static SlotBaseline Capture(EditableSingleWindowSlot slot)
        {
            return new SlotBaseline
            {
                _width = slot.Width,
                _height = slot.Height,
                _dpi = slot.Dpi,
                _bitRate = slot.BitRate ?? string.Empty,
                _maxFps = slot.MaxFps,
                _turnScreenOff = slot.TurnScreenOff,
                _stayAwake = slot.StayAwake,
                _useHidKeyboard = slot.UseHidKeyboard,
                _useHidMouse = slot.UseHidMouse,
                _forceStopStartApp = slot.ForceStopStartApp,
                _startAppPackage = slot.StartAppPackage ?? string.Empty,
                _startAppName = slot.StartAppName ?? string.Empty,
                _additionalArguments = slot.AdditionalArguments ?? string.Empty,
                _customWidth = slot.CustomWidth,
                _customHeight = slot.CustomHeight,
                _flexDisplay = slot.FlexDisplay
            };
        }

        public bool DiffersFrom(EditableSingleWindowSlot slot)
        {
            return _width != slot.Width
                || _height != slot.Height
                || _dpi != slot.Dpi
                || !string.Equals(_bitRate, slot.BitRate ?? string.Empty, StringComparison.Ordinal)
                || _maxFps != slot.MaxFps
                || _turnScreenOff != slot.TurnScreenOff
                || _stayAwake != slot.StayAwake
                || _useHidKeyboard != slot.UseHidKeyboard
                || _useHidMouse != slot.UseHidMouse
                || _forceStopStartApp != slot.ForceStopStartApp
                || !string.Equals(_startAppPackage, slot.StartAppPackage ?? string.Empty, StringComparison.Ordinal)
                || !string.Equals(_startAppName, slot.StartAppName ?? string.Empty, StringComparison.Ordinal)
                || !string.Equals(_additionalArguments, slot.AdditionalArguments ?? string.Empty, StringComparison.Ordinal)
                || _customWidth != slot.CustomWidth
                || _customHeight != slot.CustomHeight
                || _flexDisplay != slot.FlexDisplay;
        }
    }
}
