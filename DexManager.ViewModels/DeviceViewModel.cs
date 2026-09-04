using CommunityToolkit.Mvvm.ComponentModel;
using DexManager.Models;

namespace DexManager.ViewModels;

/// <summary>
/// 기기 1대의 표시용 상태. <see cref="Identity"/>는 불변이며 목록 안에서
/// 이 ViewModel을 식별하는 키다.
/// </summary>
public sealed partial class DeviceViewModel : ObservableObject
{
    public DeviceViewModel(PhysicalDeviceInfo info)
    {
        if (info == null) throw new ArgumentNullException(nameof(info));
        Identity = info.Identity ?? string.Empty;
        Update(info);
    }

    /// <summary>기기의 영속 식별자. 재연결되어도 유지된다.</summary>
    public string Identity { get; }

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _primarySerial = string.Empty;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _transportSummary = string.Empty;

    /// <summary>
    /// 새 스냅샷의 값으로 갱신한다. 기존 인스턴스를 재사용하므로
    /// 목록 바인딩과 선택 상태가 유지된다.
    /// </summary>
    public void Update(PhysicalDeviceInfo info)
    {
        if (info == null) return;

        DisplayName = info.DisplayName ?? string.Empty;
        IsConnected = info.IsConnected;

        // 현재 serial 을 선호값으로 넘겨 같은 transport 를 계속 고르게 한다.
        PrimarySerial =
            info.SelectPreferredTransport(PrimarySerial)?.Serial ?? string.Empty;

        TransportSummary = info.Transports == null
            ? string.Empty
            : string.Join(", ", info.Transports.Select(t => $"{t.Kind}: {t.Serial}"));
    }
}
