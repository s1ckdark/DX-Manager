using DexManager.Hosting;
using DexManager.Services;
using DexManager.Tests.FakePlatform;

namespace DexManager.Tests;

/// <summary>
/// DeX 시작 시 자동 화면 깨우기 + 키가드 해제를 고정한다. 실기(SM-F971N,
/// One UI) 확인 - fix round 1: 깨우기(<c>input keyevent 224</c>)와 키가드
/// 해제(<c>wm dismiss-keyguard</c>)는 별개의 두 단계이며 반드시 그 순서로
/// 실행해야 한다. <c>wm dismiss-keyguard</c>만으로는 기기가 잠들어 있는
/// 동안 아무 효과가 없었고, 깨우기만으로는 키가드가 풀리지 않았다(첫
/// 관찰에서 풀린 것처럼 보였던 건 Smart Lock 신뢰가 마침 그 시점에 막
/// 재승인된 우연이었다). 그래서 잠금 게이트
/// (<see cref="DexOrchestratorStartLockGateTests"/>)가 판단하는 상태는
/// 반드시 "깨우고 해제까지 시도한 뒤"의 상태여야 한다.
/// </summary>
public class DexOrchestratorWakeTests
{
    private const string Serial = "phone-a";
    private const string Hardware = "PHONEAAA";
    private static readonly string Identity =
        FakeAdbExecutable.IdentityOf(Hardware);

    [Fact]
    public async Task StartAsync_SendsWakeThenDismissKeyguardThenProbesTheLockState()
    {
        using var root = new TempHostRoot();
        var adb = new FakeAdbExecutable(
            root.Root,
            new Dictionary<string, string> { [Serial] = Hardware },
            dumpsysTrustOutputByTransport: new Dictionary<string, string>
            {
                [Serial] =
                    " User \"Owner\" (id=0, flags=0x4c13) (current): " +
                    "trustState=TRUSTED, trustManaged=1, deviceLocked=0, " +
                    "isActiveUnlockRunning=0, strongAuthRequired=0x0"
            });
        var host = root.CreateHost(pathProvider: adb.CreatePathProvider());
        // 잠금 게이트를 통과한 뒤의 단계(가상 디스플레이)는 이 테스트
        // 환경에 실제 scrcpy/디스플레이가 없어 결국 실패한다. 그 실패는
        // 깨우기·해제·잠금 검사와 무관하므로 짧은 타임아웃으로 빠르게
        // 받는다.
        host.UpdateSettings(settings =>
            settings.Timing.VirtualDisplayDetectionTimeoutMs = 50);
        try
        {
            var runtime = host.RuntimeCoordinator.GetOrCreate(Identity, Serial);

            await Record.ExceptionAsync(
                () => runtime.Dex.StartAsync(Serial, Identity, CancellationToken.None));

            var invocations = adb.Invocations;
            var wakeIndex = IndexOfFirst(invocations, "input keyevent 224");
            var dismissIndex = IndexOfFirst(invocations, "wm dismiss-keyguard");
            var trustIndex = IndexOfFirst(invocations, "dumpsys trust");

            Assert.True(
                wakeIndex >= 0,
                "The wake command (input keyevent 224) was never sent.");
            Assert.True(
                dismissIndex >= 0,
                "The dismiss-keyguard command (wm dismiss-keyguard) was never sent.");
            Assert.True(
                trustIndex >= 0,
                "The lock probe (dumpsys trust) was never sent.");
            Assert.True(
                wakeIndex < dismissIndex,
                "The wake command must be sent before dismiss-keyguard - on real " +
                "hardware, dismiss-keyguard has no effect while the device is " +
                "still asleep.");
            Assert.True(
                dismissIndex < trustIndex,
                "Dismiss-keyguard must be sent before the lock probe, so the " +
                "gate judges the device's state after the dismiss attempt, not " +
                "before.");
        }
        finally
        {
            host.Dispose();
        }
    }

    [Fact]
    public async Task StartAsync_SendsTheWakeCommandToTheCorrectDevice()
    {
        using var root = new TempHostRoot();
        var adb = new FakeAdbExecutable(
            root.Root,
            new Dictionary<string, string> { [Serial] = Hardware });
        var host = root.CreateHost(pathProvider: adb.CreatePathProvider());
        host.UpdateSettings(settings =>
            settings.Timing.VirtualDisplayDetectionTimeoutMs = 50);
        try
        {
            var runtime = host.RuntimeCoordinator.GetOrCreate(Identity, Serial);

            await Record.ExceptionAsync(
                () => runtime.Dex.StartAsync(Serial, Identity, CancellationToken.None));

            Assert.Contains(
                adb.Invocations,
                line => line.Contains("-s " + Serial, StringComparison.Ordinal) &&
                    line.Contains("input keyevent 224", StringComparison.Ordinal));
        }
        finally
        {
            host.Dispose();
        }
    }

    [Fact]
    public async Task StartAsync_SendsTheDismissKeyguardCommandToTheCorrectDevice()
    {
        using var root = new TempHostRoot();
        var adb = new FakeAdbExecutable(
            root.Root,
            new Dictionary<string, string> { [Serial] = Hardware });
        var host = root.CreateHost(pathProvider: adb.CreatePathProvider());
        host.UpdateSettings(settings =>
            settings.Timing.VirtualDisplayDetectionTimeoutMs = 50);
        try
        {
            var runtime = host.RuntimeCoordinator.GetOrCreate(Identity, Serial);

            await Record.ExceptionAsync(
                () => runtime.Dex.StartAsync(Serial, Identity, CancellationToken.None));

            Assert.Contains(
                adb.Invocations,
                line => line.Contains("-s " + Serial, StringComparison.Ordinal) &&
                    line.Contains("wm dismiss-keyguard", StringComparison.Ordinal));
        }
        finally
        {
            host.Dispose();
        }
    }

    private static int IndexOfFirst(
        IReadOnlyList<string> invocations,
        string fragment)
    {
        for (var i = 0; i < invocations.Count; i++)
        {
            if (invocations[i].Contains(fragment, StringComparison.Ordinal))
                return i;
        }

        return -1;
    }
}
