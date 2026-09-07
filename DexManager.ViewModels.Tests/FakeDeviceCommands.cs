using DexManager.ViewModels;

namespace DexManager.ViewModels.Tests;

/// <summary>
/// 테스트용 런타임 명령 stub. 호출 인자를 기록하고 미리 정한 결과를
/// 돌려준다. <see cref="StartGate"/>를 걸면 시작이 끝나지 않은 상태를
/// 만들 수 있어 중복 실행 방지를 검증할 수 있다.
/// </summary>
public sealed class FakeDeviceCommands : IDeviceRuntimeCommands
{
    public List<string> Calls { get; } = new();
    public bool StartResult { get; set; } = true;
    public bool StopResult { get; set; } = true;
    public TaskCompletionSource<bool> StartGate { get; set; }

    /// <summary>설정하면 StartSingleWindow가 이 게이트가 풀릴 때까지
    /// 동기적으로 대기한다. 슬롯 시작이 호출자 스레드를 막지 않고
    /// 스레드 풀로 넘어갔는지 확인하는 데 쓴다.</summary>
    public ManualResetEventSlim StartSingleWindowGate { get; set; }

    /// <summary>설정하면 StopSingleWindow가 이 게이트가 풀릴 때까지
    /// 동기적으로 대기한다. 슬롯 중지가 호출자 스레드를 막지 않고
    /// 스레드 풀로 넘어갔는지 확인하는 데 쓴다.</summary>
    public ManualResetEventSlim StopSingleWindowGate { get; set; }

    /// <summary>설정하면 StartSingleWindow가 이 예외를 던진다.</summary>
    public Exception StartSingleWindowException { get; set; }

    /// <summary>설정하면 StopSingleWindow가 이 예외를 던진다.</summary>
    public Exception StopSingleWindowException { get; set; }

    public async Task<bool> StartDexAsync(
        string identity,
        string serial,
        CancellationToken cancellationToken)
    {
        Calls.Add($"start-dex:{identity}:{serial}");
        if (StartGate != null) await StartGate.Task;
        return StartResult;
    }

    public Task<bool> StopDexAsync(
        string identity,
        string serial,
        CancellationToken cancellationToken)
    {
        Calls.Add($"stop-dex:{identity}:{serial}");
        return Task.FromResult(StopResult);
    }

    public void StartSingleWindow(
        string identity,
        string serial,
        int slot,
        string appPackage)
    {
        StartSingleWindowGate?.Wait();
        if (StartSingleWindowException != null) throw StartSingleWindowException;
        Calls.Add($"start-slot:{identity}:{serial}:{slot}:{appPackage}");
    }

    public void StopSingleWindow(string identity, int slot)
    {
        StopSingleWindowGate?.Wait();
        if (StopSingleWindowException != null) throw StopSingleWindowException;
        Calls.Add($"stop-slot:{identity}:{slot}");
    }
}
