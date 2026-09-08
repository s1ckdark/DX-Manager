using System.ComponentModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DexManager.ViewModels;

/// <summary>
/// 기기 1대의 화면/스트림 값(가상 디스플레이 크기·DPI, scrcpy 비트레이트·프레임율
/// 등) 편집 페이지. Phase 2에서는 이 값들을 JSON 파일을 직접 열어 고쳐야
/// 했다(예: 2560x1440/240, 16M) — 이 화면은 그 수작업을 대체한다.
/// </summary>
public sealed partial class DisplayStreamSettingsViewModel : ObservableObject
{
    // \d+[MK]? : 숫자 뒤에 M(메가) 또는 K(킬로) 단위가 선택적으로 붙는
    // scrcpy 비트레이트 형식. 전체 문자열이 일치해야 하므로 앵커를 둔다.
    private static readonly Regex BitRatePattern = new(
        @"^\d+[MK]?$",
        RegexOptions.Compiled);

    private readonly string _identity;
    private readonly ISettingsGateway _gateway;

    // 마지막으로 로드하거나 저장한 값의 스냅샷. HasChanges 계산 기준선이다.
    private int _baselineWidth;
    private int _baselineHeight;
    private int _baselineDpi;
    private string _baselineBitRate = string.Empty;
    private int _baselineMaxFps;
    private bool _baselineTurnScreenOff;
    private bool _baselineStayAwake;

    /// <param name="identity">편집 대상 기기의 영속 식별자. 호출자가 선택된
    /// 기기의 Identity를 넘긴다(ApplicationHost.SelectedSerial을 직접 읽지
    /// 않는다).</param>
    /// <param name="gateway">설정 읽기·쓰기 경계.</param>
    public DisplayStreamSettingsViewModel(string identity, ISettingsGateway gateway)
    {
        if (string.IsNullOrEmpty(identity))
            throw new ArgumentException("Device identity is empty.", nameof(identity));

        _identity = identity;
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));

        var profile = _gateway.GetRunProfile(_identity);
        Values = EditableRunSettings.FromProfile(profile);
        Values.PropertyChanged += OnValuesChanged;

        CaptureBaseline();
        Revalidate();
    }

    /// <summary>편집 중인 값의 사본. 저장 전까지는 프로필에 반영되지 않는다.</summary>
    public EditableRunSettings Values { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isValid;

    [ObservableProperty]
    private string _validationMessage = string.Empty;

    /// <summary>마지막으로 불러오거나 저장한 값과 비교해 편집된 내용이 있는지.</summary>
    public bool HasChanges =>
        Values.Width != _baselineWidth
        || Values.Height != _baselineHeight
        || Values.Dpi != _baselineDpi
        || Values.MaxFps != _baselineMaxFps
        || Values.TurnScreenOff != _baselineTurnScreenOff
        || Values.StayAwake != _baselineStayAwake
        || !string.Equals(Values.BitRate, _baselineBitRate, StringComparison.Ordinal);

    private void OnValuesChanged(object sender, PropertyChangedEventArgs e) => Revalidate();

    private void Revalidate()
    {
        if (Values.Width <= 0)
            ValidationMessage = "너비(Width)는 양의 정수여야 합니다.";
        else if (Values.Height <= 0)
            ValidationMessage = "높이(Height)는 양의 정수여야 합니다.";
        else if (Values.Dpi <= 0)
            ValidationMessage = "DPI는 양의 정수여야 합니다.";
        else if (Values.MaxFps <= 0)
            ValidationMessage = "최대 프레임율(MaxFps)은 양의 정수여야 합니다.";
        else if (!BitRatePattern.IsMatch(Values.BitRate ?? string.Empty))
            ValidationMessage = "비트레이트는 숫자 뒤에 M 또는 K가 붙은 형식이어야 합니다 (예: 16M).";
        else
            ValidationMessage = string.Empty;

        IsValid = string.IsNullOrEmpty(ValidationMessage);

        // HasChanges는 [ObservableProperty]가 아니라 계산 프로퍼티이므로
        // Values가 바뀔 때마다 직접 변경 통지를 올려야 바인딩이 갱신된다.
        OnPropertyChanged(nameof(HasChanges));
    }

    private void CaptureBaseline()
    {
        _baselineWidth = Values.Width;
        _baselineHeight = Values.Height;
        _baselineDpi = Values.Dpi;
        _baselineBitRate = Values.BitRate ?? string.Empty;
        _baselineMaxFps = Values.MaxFps;
        _baselineTurnScreenOff = Values.TurnScreenOff;
        _baselineStayAwake = Values.StayAwake;
    }

    private bool CanSave() => IsValid;

    /// <summary>
    /// 편집값을 기기 프로필에 저장한다. CanSave가 IsValid를 보고 바인딩된
    /// 버튼의 활성 여부를 결정하지만, 바인딩을 우회해 강제로 Execute가
    /// 호출되는 경우에도 저장이 일어나지 않도록 메서드 내부에서도
    /// IsValid를 한 번 더 확인한다.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        if (!IsValid) return;

        _gateway.Update(s => Values.ApplyTo(s.GetOrCreateDeviceRunSettings(_identity)));

        CaptureBaseline();
        OnPropertyChanged(nameof(HasChanges));
    }

    /// <summary>편집값을 전역 VirtualDisplay/Scrcpy 기본값으로 다시 채운다.
    /// 저장하지는 않으므로, 반영하려면 SaveCommand를 별도로 실행해야 한다.</summary>
    [RelayCommand]
    private void ResetToGlobalDefaults()
    {
        var globalVirtualDisplay = _gateway.Current.VirtualDisplay;
        if (globalVirtualDisplay != null)
        {
            Values.Width = globalVirtualDisplay.Width;
            Values.Height = globalVirtualDisplay.Height;
            Values.Dpi = globalVirtualDisplay.Dpi;
        }

        var globalScrcpy = _gateway.Current.Scrcpy;
        if (globalScrcpy != null)
        {
            Values.BitRate = globalScrcpy.BitRate ?? string.Empty;
            Values.MaxFps = globalScrcpy.MaxFps;
            Values.TurnScreenOff = globalScrcpy.TurnScreenOff;
            Values.StayAwake = globalScrcpy.StayAwake;
        }
    }
}
