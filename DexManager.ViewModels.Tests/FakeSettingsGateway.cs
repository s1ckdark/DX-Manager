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

    /// <summary>Update 시도 횟수(예외로 끝난 시도도 센다).</summary>
    private int _updateAttempts;

    /// <summary>
    /// 지정한 회차(1부터)의 Update 호출에서 던질 예외. null이면 던지지
    /// 않는다. SettingsService.SaveCore가 디스크 가득참·잠금 파일 타임아웃·
    /// 상위 버전 설정 파일에서 실제로 던지는 상황을 흉내낸다.
    /// </summary>
    public Exception UpdateExceptionToThrow { get; set; }

    /// <summary>몇 번째 Update 호출에서 예외를 던질지(1부터).</summary>
    public int UpdateExceptionOnCall { get; set; } = 1;

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

        _updateAttempts++;
        if (UpdateExceptionToThrow != null && _updateAttempts == UpdateExceptionOnCall)
            throw UpdateExceptionToThrow;

        mutate(_settings);
        LastMutate = mutate;
        UpdateCallCount++;
    }
}
