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
    /// 편집값을 전역 설정에 저장한다.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        _gateway.Update(s =>
        {
            if (s.Paths != null)
            {
                s.Paths.ScrcpyPath = ScrcpyPath ?? string.Empty;
                s.Paths.AdbPath = AdbPath ?? string.Empty;
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
