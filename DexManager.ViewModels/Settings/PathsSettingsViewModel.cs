using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DexManager.Models;

namespace DexManager.ViewModels;

/// <summary>
/// 전역 Scrcpy·ADB 실행파일 경로 편집 페이지. 이 값들은 전역 수준이고
/// 기기별로 정하지 않는다(DisplayStreamSettingsViewModel과 달리).
/// </summary>
public sealed partial class PathsSettingsViewModel : ObservableObject
{
    private readonly ISettingsGateway _gateway;

    // 마지막으로 로드하거나 저장한 값의 스냅샷. HasChanges 계산 기준선이다.
    private string _baselineScrcpyPath = string.Empty;
    private string _baselineAdbPath = string.Empty;

    /// <param name="gateway">설정 읽기·쓰기 경계.</param>
    public PathsSettingsViewModel(ISettingsGateway gateway)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));

        var currentPaths = _gateway.Current.Paths;
        ScrcpyPath = currentPaths?.ScrcpyPath ?? string.Empty;
        AdbPath = currentPaths?.AdbPath ?? string.Empty;

        CaptureBaseline();
    }

    /// <summary>Scrcpy 실행파일 경로.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _scrcpyPath = string.Empty;

    /// <summary>ADB 실행파일 경로.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _adbPath = string.Empty;

    /// <summary>마지막으로 불러오거나 저장한 값과 비교해 편집된 내용이 있는지.</summary>
    public bool HasChanges =>
        !string.Equals(ScrcpyPath, _baselineScrcpyPath, StringComparison.Ordinal)
        || !string.Equals(AdbPath, _baselineAdbPath, StringComparison.Ordinal);

    private void CaptureBaseline()
    {
        _baselineScrcpyPath = ScrcpyPath ?? string.Empty;
        _baselineAdbPath = AdbPath ?? string.Empty;
    }

    private bool CanSave() => true;

    /// <summary>
    /// 편집값을 전역 설정에 저장한다. ADB 경로를 사용자가 실제로
    /// <b>편집했을 때만</b> 선택 모드를 바꾼다 - 채워서 편집했으면 수동
    /// (<see cref="AdbSelectionMode.Manual"/>), 비우도록 편집했으면 자동
    /// (<see cref="AdbSelectionMode.Auto"/>)로 전환한다.
    ///
    /// 왜 "값이 채워져 있으면 Manual"이 아니라 "값이 바뀌었으면"인가:
    /// 이 필드는 <see cref="ApplicationHost.EnsureDefaultPaths"/>가 자동
    /// 감지한 절대 경로로 시작부터 채워져 있는 경우가 흔하다(포터블
    /// 패키지, 또는 첫 실행). 그 상태에서 사용자가 테마 등 다른 설정만
    /// 바꾸고 저장하면 SaveAll이 이 커맨드도 무조건 실행하므로
    /// (SaveAll → Paths.SaveCommand, CanSave는 항상 true), "비어 있지
    /// 않으면 Manual"로는 사용자가 입력한 적 없는 자동 감지 경로에
    /// 조용히 Manual이 박혀 버린다 - 포터블 폴더를 옮기거나 시스템 adb를
    /// 제거하면 그 순간 시작이 막힌다. 베이스라인(<see
    /// cref="_baselineAdbPath"/>)과 비교해 실제로 바뀐 경우에만 모드를
    /// 건드리고, 바뀌지 않았으면 지금 저장돼 있는 모드를 그대로 둔다 -
    /// 이미 Manual이었다면 다시 저장해도 Manual로 남는다.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        var adbPath = AdbPath ?? string.Empty;
        var adbPathEdited = !string.Equals(
            adbPath,
            _baselineAdbPath,
            StringComparison.Ordinal);

        _gateway.Update(s =>
        {
            if (s.Paths != null)
            {
                s.Paths.ScrcpyPath = ScrcpyPath ?? string.Empty;
                s.Paths.AdbPath = adbPath;
                if (adbPathEdited)
                {
                    s.Paths.AdbSelectionMode = string.IsNullOrWhiteSpace(adbPath)
                        ? AdbSelectionMode.Auto
                        : AdbSelectionMode.Manual;
                }
            }
        });

        CaptureBaseline();
        OnPropertyChanged(nameof(HasChanges));
    }

    /// <summary>
    /// 편집값을 번들 기본값으로 다시 채운다.
    /// 저장하지는 않으므로, 반영하려면 SaveCommand를 별도로 실행해야 한다.
    /// </summary>
    [RelayCommand]
    private void ResetToBundledDefaults()
    {
        var defaultSettings = AppSettings.CreateDefault();
        var defaultPaths = defaultSettings.Paths;

        if (defaultPaths != null)
        {
            ScrcpyPath = defaultPaths.ScrcpyPath ?? string.Empty;
            AdbPath = defaultPaths.AdbPath ?? string.Empty;
        }

        OnPropertyChanged(nameof(HasChanges));
    }
}
