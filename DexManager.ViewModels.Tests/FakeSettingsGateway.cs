using DexManager.Models;

namespace DexManager.ViewModels.Tests;

/// <summary>
/// 테스트용 ISettingsGateway 구현. 파일 I/O와 정규화를 하지 않고
/// 인메모리 AppSettings를 들고 있다.
/// </summary>
public class FakeSettingsGateway : ISettingsGateway
{
    private AppSettings _settings;

    /// <summary>Update 호출 횟수.</summary>
    public int UpdateCallCount { get; private set; }

    /// <summary>마지막 Update 호출에서 적용한 mutate 함수.</summary>
    public Action<AppSettings> LastMutate { get; private set; }

    public FakeSettingsGateway()
    {
        _settings = AppSettings.CreateDefault();
        UpdateCallCount = 0;
        LastMutate = null;
    }

    public AppSettings Current => _settings;

    public DeviceRunSettingsProfile GetRunProfile(string deviceIdentity)
    {
        if (string.IsNullOrEmpty(deviceIdentity))
            throw new ArgumentException("Device identity is empty.", nameof(deviceIdentity));

        return _settings.GetOrCreateDeviceRunSettings(deviceIdentity);
    }

    public void Update(Action<AppSettings> mutate)
    {
        if (mutate == null)
            throw new ArgumentNullException(nameof(mutate));

        mutate(_settings);
        LastMutate = mutate;
        UpdateCallCount++;
    }
}
