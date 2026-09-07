using DexManager.Models;
using DexManager.Platform;

namespace DexManager.Tests.FakePlatform;

public sealed class FakeKeyboardService : IKeyboardService
{
    public bool Started { get; private set; }
    public int DisposeCallCount { get; private set; }

    // 종료 경로 테스트 전용 훅. 운영 코드에는 대응물이 없다 — 이 가짜
    // 객체만 아는 관측 지점이다.
    public Exception ThrowOnDispose { get; set; }
    public Action OnDispose { get; set; }

    public event EventHandler CaptureHotkeyPressed;
    public event EventHandler ExitHotkeyPressed;

    public void Start() => Started = true;

    public void Stop() => Started = false;

    public void ReloadConfiguration(KeyMappingSettings settings) { }

    // MacKeyboardService와 동일한 패턴이다. 이벤트를 발화하는 메서드가
    // 없으면 /warnaserror 빌드가 CS0067("event is never used")로 실패한다.
    public void TriggerCapture() =>
        CaptureHotkeyPressed?.Invoke(this, EventArgs.Empty);

    public void TriggerExit() =>
        ExitHotkeyPressed?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        Stop();
        DisposeCallCount++;
        OnDispose?.Invoke();
        if (ThrowOnDispose != null) throw ThrowOnDispose;
    }
}
