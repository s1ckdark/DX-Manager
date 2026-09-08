using DexManager.Hosting;
using DexManager.Models;

namespace DexManager.ViewModels;

/// <summary>
/// 실제 환경에서의 ISettingsGateway 구현. ApplicationHost의 설정
/// 읽기·쓰기 기능을 래핑한다.
/// </summary>
public sealed class SettingsGateway : ISettingsGateway
{
    private readonly ApplicationHost _host;

    public SettingsGateway(ApplicationHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
    }

    public AppSettings Current => _host.Settings;

    public DeviceRunSettingsProfile GetRunProfile(string deviceIdentity)
    {
        if (string.IsNullOrEmpty(deviceIdentity))
            throw new ArgumentException("Device identity is empty.", nameof(deviceIdentity));

        return _host.Settings.GetOrCreateDeviceRunSettings(deviceIdentity);
    }

    public void Update(Action<AppSettings> mutate)
    {
        if (mutate == null)
            throw new ArgumentNullException(nameof(mutate));

        _host.UpdateSettings(mutate);
    }
}
