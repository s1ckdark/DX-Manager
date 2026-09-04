namespace DexManager.ViewModels;

/// <summary>
/// UI 스레드로 작업을 넘기는 추상화. ViewModel이 특정 UI 프레임워크에
/// 묶이지 않게 한다.
/// </summary>
/// <remarks>
/// Core 이벤트(<c>DeviceMonitorService.DeviceConnected</c> 등)는 백그라운드
/// 스레드에서 발생한다. ViewModel이 관측 가능한 상태를 바꾸기 전에 반드시
/// 이 인터페이스를 거친다.
/// </remarks>
public interface IUiDispatcher
{
    /// <summary>현재 스레드가 UI 스레드인지 여부.</summary>
    bool IsOnUiThread { get; }

    /// <summary>
    /// UI 스레드에서 실행할 작업을 큐에 넣는다. 호출자를 막지 않는다.
    /// </summary>
    void Post(Action action);

    /// <summary>
    /// UI 스레드에서 비동기 작업을 실행하고 완료를 기다린다.
    /// </summary>
    Task InvokeAsync(Func<Task> action);
}
