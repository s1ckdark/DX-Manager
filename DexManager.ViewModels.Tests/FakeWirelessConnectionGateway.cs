using DexManager.Models;
using DexManager.Services;

namespace DexManager.ViewModels.Tests;

/// <summary>
/// 테스트용 <see cref="IWirelessConnectionGateway"/>. 호출을 기록하고,
/// 실제 서비스의 프로필 부작용(성공 시 Wireless/USB 모드 저장)을 흉내낸다.
/// </summary>
public sealed class FakeWirelessConnectionGateway : IWirelessConnectionGateway, IDisposable
{
    /// <summary>"메서드:identity:인자..." 형식의 호출 기록.</summary>
    public List<string> Calls { get; } = new List<string>();

    public WirelessConnectionResult NextResult { get; set; } =
        WirelessConnectionResult.Succeeded("192.168.0.5:5555", "ok");

    /// <summary>설정되면 호출을 기록한 뒤 이 예외로 Task가 실패한다.</summary>
    public Exception ExceptionToThrow { get; set; }

    /// <summary>설정되면 이 Task를 돌려주고, 성공 완료 시에만 프로필을 갱신한다.</summary>
    public TaskCompletionSource<WirelessConnectionResult> Gate { get; set; }

    public Dictionary<string, PhysicalDeviceInfo> Devices { get; } = new Dictionary<string, PhysicalDeviceInfo>();

    public Dictionary<string, DeviceWirelessConnectionProfile> Profiles { get; } = new Dictionary<string, DeviceWirelessConnectionProfile>();

    public bool Disposed { get; private set; }

    public event EventHandler DevicesChanged;

    public void RaiseDevicesChanged() => DevicesChanged?.Invoke(this, EventArgs.Empty);

    public PhysicalDeviceInfo FindDevice(string identity) =>
        Devices.TryGetValue(identity, out var device) ? device.Clone() : null;

    public DeviceWirelessConnectionProfile GetProfile(string identity) =>
        Profiles.TryGetValue(identity, out var p)
            ? new DeviceWirelessConnectionProfile
            {
                DeviceIdentity = p.DeviceIdentity,
                Mode = p.Mode,
                WirelessHost = p.WirelessHost,
                WirelessPort = p.WirelessPort,
                AutoReconnect = p.AutoReconnect
            }
            : null;

    public Task<WirelessConnectionResult> EnableFromUsbAsync(string identity, string usbSerial, string host, int port, bool autoReconnect, CancellationToken cancellationToken) =>
        Run($"EnableFromUsb:{identity}:{usbSerial}:{host}:{port}:{autoReconnect}",
            r => ApplyWireless(identity, host, port, autoReconnect, r));

    public Task<WirelessConnectionResult> ConnectAsync(string identity, string host, int port, bool autoReconnect, CancellationToken cancellationToken) =>
        Run($"Connect:{identity}:{host}:{port}:{autoReconnect}",
            r => ApplyWireless(identity, host, port, autoReconnect, r));

    public Task<WirelessConnectionResult> DisconnectAsync(string identity, CancellationToken cancellationToken) =>
        Run($"Disconnect:{identity}", r =>
        {
            if (!r.Success) return;
            if (!Profiles.TryGetValue(identity, out var p))
            {
                p = new DeviceWirelessConnectionProfile { DeviceIdentity = identity, WirelessPort = 5555 };
                Profiles[identity] = p;
            }
            p.Mode = AdbConnectionMode.Usb;
        });

    public void Dispose() => Disposed = true;

    private Task<WirelessConnectionResult> Run(string call, Action<WirelessConnectionResult> applySideEffects)
    {
        Calls.Add(call);
        if (ExceptionToThrow != null) return Task.FromException<WirelessConnectionResult>(ExceptionToThrow);

        if (Gate != null)
        {
            return Gate.Task.ContinueWith(t =>
            {
                var result = t.GetAwaiter().GetResult();
                applySideEffects(result);
                return result;
            }, TaskContinuationOptions.ExecuteSynchronously);
        }

        var result = NextResult;
        applySideEffects(result);
        return Task.FromResult(result);
    }

    private void ApplyWireless(string identity, string host, int port, bool autoReconnect, WirelessConnectionResult result)
    {
        if (!result.Success) return;

        var resolvedHost = host;
        if (string.IsNullOrEmpty(resolvedHost) && !string.IsNullOrEmpty(result.Endpoint))
        {
            var idx = result.Endpoint.LastIndexOf(':');
            resolvedHost = idx >= 0 ? result.Endpoint.Substring(0, idx) : result.Endpoint;
        }

        Profiles[identity] = new DeviceWirelessConnectionProfile
        {
            DeviceIdentity = identity,
            Mode = AdbConnectionMode.Wireless,
            WirelessHost = resolvedHost,
            WirelessPort = port,
            AutoReconnect = autoReconnect
        };
    }
}
