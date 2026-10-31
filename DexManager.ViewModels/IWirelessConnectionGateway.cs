using DexManager.Models;
using DexManager.Services;

namespace DexManager.ViewModels;

/// <summary>
/// 뷰모델이 무선 ADB 연결 기능에 접근하는 경계. 모든 동작은 물리 기기
/// identity 단위로 수행되며 전역 연결 상태를 사용하지 않는다.
/// </summary>
public interface IWirelessConnectionGateway
{
    /// <summary>레지스트리 스냅샷이 바뀔 때 임의 스레드에서 발생한다.</summary>
    event EventHandler DevicesChanged;

    /// <summary>현재 스냅샷의 기기 복사본. 없으면 null.</summary>
    PhysicalDeviceInfo FindDevice(string identity);

    /// <summary>저장된 프로필의 복사본. 저장된 것이 없으면 null (만들지 않는다).</summary>
    DeviceWirelessConnectionProfile GetProfile(string identity);

    Task<WirelessConnectionResult> EnableFromUsbAsync(string identity, string usbSerial, string host, int port, bool autoReconnect, CancellationToken cancellationToken);

    Task<WirelessConnectionResult> ConnectAsync(string identity, string host, int port, bool autoReconnect, CancellationToken cancellationToken);

    Task<WirelessConnectionResult> DisconnectAsync(string identity, CancellationToken cancellationToken);
}
