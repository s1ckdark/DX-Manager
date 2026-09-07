namespace DexManager.ViewModels;

/// <summary>
/// 행이 부르는 런타임 명령의 경계. 실제 구현은 adb와 scrcpy 프로세스를
/// 부르므로, 테스트는 이 인터페이스에 stub을 넣어 명령의 상태 기계만
/// 기기 없이 검증한다.
/// </summary>
public interface IDeviceRuntimeCommands
{
    Task<bool> StartDexAsync(
        string identity,
        string serial,
        CancellationToken cancellationToken);

    Task<bool> StopDexAsync(
        string identity,
        string serial,
        CancellationToken cancellationToken);

    void StartSingleWindow(
        string identity,
        string serial,
        int slot,
        string appPackage);

    void StopSingleWindow(string identity, int slot);
}
