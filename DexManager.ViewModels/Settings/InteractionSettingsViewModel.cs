using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DexManager.Models;

namespace DexManager.ViewModels;

/// <summary>
/// 키매핑(단축키·입력 보정) 설정 편집 페이지의 골격. <see cref="KeyMappingSettings"/>는
/// 기기별이 아니라 전역 설정(<see cref="AppSettings.KeyMappings"/>)이므로
/// identity를 받지 않는다(PathsSettingsViewModel/AppearanceSettingsViewModel과
/// 같은 전역 편집 패턴).
///
/// Step 0 조사 결과 <see cref="KeyMappingSettings"/>는 실제로는 (브리프가
/// 가정한 130개가 아니라) 10개의 스칼라 <c>DataMember</c> 속성뿐이고,
/// WinForms 참조 UI(SettingsForm.Pages.cs의 BuildKeyboardPage +
/// SettingsForm.Values.cs)가 10개 전부를 편집 가능한 값으로 노출한다 —
/// 그래서 이 골격은 10개 필드 전부를 다룬다.
///
/// 검증 범위는 최소 구조적 검증뿐이다: "캡처/종료 단축키가 비어 있지
/// 않고 서로 (서수 비교로) 달라야 함". 그 이상은 이 브랜치 어디에도
/// 없다 - Task 10은 캡처한 키를 저장 문자열로 바꾸는 포매터
/// (DexManager.Desktop의 HotkeyFormatter)만 냈고, 문법 파서나 충돌
/// 검출기를 내지 않았다. 즉 다음 두 가지는 여기서도, 다른 어디서도
/// 검사하지 않는다:
///   - 단축키 문법 자체의 유효성(WinForms의
///     HotkeyService.IsValidShortcut에 해당). 캡처 UI로 입력하면 항상
///     유효한 형식이 나오지만, 이전 버전이 남긴 값이나 손으로 고친
///     settings.json은 그대로 통과한다.
///   - 의미론적 충돌(WinForms의 HotkeyService.ShortcutsConflict에 해당).
///     "Alt+F8"과 "LeftAlt+F8"은 서수 비교로는 서로 다르지만
///     KeyShortcut.Matches에게는 같은 단축키다.
/// 둘 다 후속 과제다.
/// </summary>
public sealed partial class InteractionSettingsViewModel : ObservableObject
{
    private readonly ISettingsGateway _gateway;

    // 마지막으로 로드하거나 저장한 값의 스냅샷. HasChanges 계산 기준선이다.
    private string _baselineCaptureHotkey = string.Empty;
    private string _baselineExitHotkey = string.Empty;
    private bool _baselineUseLowLevelHotkeys;
    private bool _baselineLogKeyboardDiagnostics;
    private bool _baselineConvertKoreanEnglishKey;
    private KeyInputMode _baselineKoreanEnglishInputMode;
    private bool _baselineHandleRightWindowsKey;
    private bool _baselineConvertEnterToShiftEnter;
    private KeyInputMode _baselineEnterInputMode;
    private bool _baselineIgnoreShiftSpace;

    /// <param name="gateway">설정 읽기·쓰기 경계.</param>
    public InteractionSettingsViewModel(ISettingsGateway gateway)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));

        var current = _gateway.Current.KeyMappings;
        CaptureHotkey = current?.CaptureHotkey ?? string.Empty;
        ExitHotkey = current?.ExitHotkey ?? string.Empty;
        UseLowLevelHotkeys = current?.UseLowLevelHotkeys ?? false;
        LogKeyboardDiagnostics = current?.LogKeyboardDiagnostics ?? false;
        ConvertKoreanEnglishKey = current?.ConvertKoreanEnglishKey ?? false;
        KoreanEnglishInputMode = current?.KoreanEnglishInputMode ?? KeyInputMode.SendInputVirtualKey;
        HandleRightWindowsKey = current?.HandleRightWindowsKey ?? false;
        ConvertEnterToShiftEnter = current?.ConvertEnterToShiftEnter ?? false;
        EnterInputMode = current?.EnterInputMode ?? KeyInputMode.SendInputVirtualKey;
        IgnoreShiftSpace = current?.IgnoreShiftSpace ?? false;

        CaptureBaseline();
        Revalidate();
    }

    /// <summary>캡처(화면 캡처 실행) 단축키.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _captureHotkey = string.Empty;

    /// <summary>종료 단축키.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _exitHotkey = string.Empty;

    /// <summary>저수준 키보드 후크로 단축키를 처리할지.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _useLowLevelHotkeys;

    /// <summary>키보드 진단 로그를 남길지.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _logKeyboardDiagnostics;

    /// <summary>한/영 전환 키를 보정할지.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _convertKoreanEnglishKey;

    /// <summary>한/영 전환 키를 전달하는 입력 모드.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private KeyInputMode _koreanEnglishInputMode;

    /// <summary>오른쪽 윈도우 키를 별도로 처리할지.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _handleRightWindowsKey;

    /// <summary>Enter를 Shift+Enter로 변환할지.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _convertEnterToShiftEnter;

    /// <summary>Enter 키를 전달하는 입력 모드.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private KeyInputMode _enterInputMode;

    /// <summary>Shift+Space를 무시할지.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _ignoreShiftSpace;

    /// <summary>현재 편집값이 저장 가능한 상태인지.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isValid;

    /// <summary>검증 실패 사유. 유효하면 빈 문자열.</summary>
    [ObservableProperty]
    private string _validationMessage = string.Empty;

    /// <summary>마지막으로 불러오거나 저장한 값과 비교해 편집된 내용이 있는지.</summary>
    public bool HasChanges =>
        !string.Equals(CaptureHotkey, _baselineCaptureHotkey, StringComparison.Ordinal)
        || !string.Equals(ExitHotkey, _baselineExitHotkey, StringComparison.Ordinal)
        || UseLowLevelHotkeys != _baselineUseLowLevelHotkeys
        || LogKeyboardDiagnostics != _baselineLogKeyboardDiagnostics
        || ConvertKoreanEnglishKey != _baselineConvertKoreanEnglishKey
        || KoreanEnglishInputMode != _baselineKoreanEnglishInputMode
        || HandleRightWindowsKey != _baselineHandleRightWindowsKey
        || ConvertEnterToShiftEnter != _baselineConvertEnterToShiftEnter
        || EnterInputMode != _baselineEnterInputMode
        || IgnoreShiftSpace != _baselineIgnoreShiftSpace;

    partial void OnCaptureHotkeyChanged(string value) => Revalidate();

    partial void OnExitHotkeyChanged(string value) => Revalidate();

    /// <summary>
    /// 캡처/종료 단축키에 대한 최소 구조적 검증 - 비어 있지 않을 것,
    /// 그리고 서로 서수 비교로 다를 것. 이게 전부다. 단축키 문법 파싱과
    /// 의미론적 충돌 감지는 이 브랜치의 어디에도 구현돼 있지 않다
    /// (클래스 문서 참고).
    ///
    /// 비어 있음을 막는 이유는 취향이 아니라 안전장치다:
    /// AppSettings.EnsureDefaults()는 빈 CaptureHotkey/ExitHotkey를 조용히
    /// 기본값으로 되돌린다(AppSettings.cs 181행 부근). 이 검증이 빈 값을
    /// Save에 도달하지 못하게 막아 두면, 다음 로드 때 그 되돌림이 사용자의
    /// 의도적 편집을 말없이 뒤집는 일이 없다.
    /// </summary>
    private void Revalidate()
    {
        var captureHotkey = (CaptureHotkey ?? string.Empty).Trim();
        var exitHotkey = (ExitHotkey ?? string.Empty).Trim();

        if (string.IsNullOrEmpty(captureHotkey))
            ValidationMessage = "캡처 단축키(CaptureHotkey)는 비어 있을 수 없습니다.";
        else if (string.IsNullOrEmpty(exitHotkey))
            ValidationMessage = "종료 단축키(ExitHotkey)는 비어 있을 수 없습니다.";
        else if (string.Equals(captureHotkey, exitHotkey, StringComparison.Ordinal))
            ValidationMessage = "캡처 단축키와 종료 단축키는 서로 달라야 합니다.";
        else
            ValidationMessage = string.Empty;

        IsValid = string.IsNullOrEmpty(ValidationMessage);

        // HasChanges는 [ObservableProperty]가 아니라 계산 프로퍼티이므로
        // Values가 바뀔 때마다 직접 변경 통지를 올려야 바인딩이 갱신된다.
        OnPropertyChanged(nameof(HasChanges));
    }

    private void CaptureBaseline()
    {
        _baselineCaptureHotkey = CaptureHotkey ?? string.Empty;
        _baselineExitHotkey = ExitHotkey ?? string.Empty;
        _baselineUseLowLevelHotkeys = UseLowLevelHotkeys;
        _baselineLogKeyboardDiagnostics = LogKeyboardDiagnostics;
        _baselineConvertKoreanEnglishKey = ConvertKoreanEnglishKey;
        _baselineKoreanEnglishInputMode = KoreanEnglishInputMode;
        _baselineHandleRightWindowsKey = HandleRightWindowsKey;
        _baselineConvertEnterToShiftEnter = ConvertEnterToShiftEnter;
        _baselineEnterInputMode = EnterInputMode;
        _baselineIgnoreShiftSpace = IgnoreShiftSpace;
    }

    private bool CanSave() => IsValid;

    /// <summary>
    /// 편집값을 전역 설정(<see cref="AppSettings.KeyMappings"/>)에 저장한다.
    /// CanSave가 IsValid를 보고 바인딩된 버튼의 활성 여부를 결정하지만,
    /// 바인딩을 우회해 강제로 Execute가 호출되는 경우에도 저장이 일어나지
    /// 않도록 메서드 내부에서도 IsValid를 한 번 더 확인한다.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        if (!IsValid) return;

        _gateway.Update(s =>
        {
            if (s.KeyMappings == null) s.KeyMappings = new KeyMappingSettings();
            s.KeyMappings.CaptureHotkey = CaptureHotkey ?? string.Empty;
            s.KeyMappings.ExitHotkey = ExitHotkey ?? string.Empty;
            s.KeyMappings.UseLowLevelHotkeys = UseLowLevelHotkeys;
            s.KeyMappings.LogKeyboardDiagnostics = LogKeyboardDiagnostics;
            s.KeyMappings.ConvertKoreanEnglishKey = ConvertKoreanEnglishKey;
            s.KeyMappings.KoreanEnglishInputMode = KoreanEnglishInputMode;
            s.KeyMappings.HandleRightWindowsKey = HandleRightWindowsKey;
            s.KeyMappings.ConvertEnterToShiftEnter = ConvertEnterToShiftEnter;
            s.KeyMappings.EnterInputMode = EnterInputMode;
            s.KeyMappings.IgnoreShiftSpace = IgnoreShiftSpace;
        });

        CaptureBaseline();
        OnPropertyChanged(nameof(HasChanges));
    }

    /// <summary>
    /// 편집값을 번들 기본값(<see cref="AppSettings.CreateDefault"/>)으로
    /// 다시 채운다. 저장하지는 않으므로, 반영하려면 SaveCommand를 별도로
    /// 실행해야 한다.
    /// </summary>
    [RelayCommand]
    private void ResetToBundledDefaults()
    {
        var defaults = AppSettings.CreateDefault().KeyMappings;

        CaptureHotkey = defaults.CaptureHotkey ?? string.Empty;
        ExitHotkey = defaults.ExitHotkey ?? string.Empty;
        UseLowLevelHotkeys = defaults.UseLowLevelHotkeys;
        LogKeyboardDiagnostics = defaults.LogKeyboardDiagnostics;
        ConvertKoreanEnglishKey = defaults.ConvertKoreanEnglishKey;
        KoreanEnglishInputMode = defaults.KoreanEnglishInputMode;
        HandleRightWindowsKey = defaults.HandleRightWindowsKey;
        ConvertEnterToShiftEnter = defaults.ConvertEnterToShiftEnter;
        EnterInputMode = defaults.EnterInputMode;
        IgnoreShiftSpace = defaults.IgnoreShiftSpace;

        OnPropertyChanged(nameof(HasChanges));
    }
}
