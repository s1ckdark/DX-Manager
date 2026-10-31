using DexManager.Hosting;
using DexManager.Models;
using DexManager.Services;

namespace DexManager.ViewModels;

/// <summary>
/// <see cref="IWirelessConnectionGateway"/>의 실제 구현. ApplicationHost의
/// 기기별 WirelessAdbService API를 감싸며, 블로킹 호출은 스레드 풀에서 실행한다.
/// </summary>
public sealed class WirelessConnectionGateway : IWirelessConnectionGateway, IDisposable
{
    private readonly ApplicationHost _host;

    public WirelessConnectionGateway(ApplicationHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _host.DeviceRegistry.SnapshotChanged += OnSnapshotChanged;
    }

    public event EventHandler DevicesChanged;

    public PhysicalDeviceInfo FindDevice(string identity) =>
        _host.DeviceRegistry.Current.FindByIdentity(identity)?.Clone();

    public DeviceWirelessConnectionProfile GetProfile(string identity)
    {
        var saved = _host.Settings.FindDeviceWirelessConnection(identity);
        if (saved == null) return null;

        return new DeviceWirelessConnectionProfile
        {
            DeviceIdentity = saved.DeviceIdentity,
            Mode = saved.Mode,
            WirelessHost = saved.WirelessHost,
            WirelessPort = saved.WirelessPort,
            AutoReconnect = saved.AutoReconnect
        };
    }

    public Task<WirelessConnectionResult> EnableFromUsbAsync(string identity, string usbSerial, string host, int port, bool autoReconnect, CancellationToken cancellationToken) =>
        Task.Run(() => _host.WirelessAdb.EnableFromUsbForDevice(identity, usbSerial, host, port, autoReconnect), cancellationToken);

    public Task<WirelessConnectionResult> ConnectAsync(string identity, string host, int port, bool autoReconnect, CancellationToken cancellationToken) =>
        Task.Run(() => _host.WirelessAdb.ConnectForDevice(identity, host, port, autoReconnect), cancellationToken);

    public Task<WirelessConnectionResult> DisconnectAsync(string identity, CancellationToken cancellationToken) =>
        Task.Run(() => _host.WirelessAdb.DisconnectForDevice(identity), cancellationToken);

    public void Dispose()
    {
        _host.DeviceRegistry.SnapshotChanged -= OnSnapshotChanged;
    }

    private void OnSnapshotChanged(object sender, DeviceRegistrySnapshotChangedEventArgs e) =>
        DevicesChanged?.Invoke(this, EventArgs.Empty);
}
