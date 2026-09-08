using DexManager.Hosting;
using DexManager.Services;
using DexManager.Tests.FakePlatform;

namespace DexManager.Tests;

/// <summary>
/// DeX 시작 시 자동 화면 깨우기(auto-wake)를 고정한다. 실기(SM-F971N,
/// One UI) 확인: <c>adb shell input keyevent 224</c>(KEYCODE_WAKEUP)만으로
/// 신뢰할 수 있는/자격증명이 필요 없는 기기의 키가드가 스스로 해제됐다 -
/// <c>wm dismiss-keyguard</c>는 기기가 잠들어 있는 동안 아무 효과가
/// 없었다. 그래서 잠금 게이트(<see cref="DexOrchestratorStartLockGateTests"/>)가
/// 판단하는 상태는 반드시 "깨운 뒤"의 상태여야 한다.
/// </summary>
public class DexOrchestratorWakeTests
{
    private const string Serial = "phone-a";
    private const string Hardware = "PHONEAAA";
    private static readonly string Identity =
        FakeAdbExecutable.IdentityOf(Hardware);

    [Fact]
    public async Task StartAsync_SendsTheWakeCommandBeforeProbingTheLockState()
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
        // 깨우기·잠금 검사와 무관하므로 짧은 타임아웃으로 빠르게 받는다.
        host.UpdateSettings(settings =>
            settings.Timing.VirtualDisplayDetectionTimeoutMs = 50);
        try
        {
            var runtime = host.RuntimeCoordinator.GetOrCreate(Identity, Serial);

            await Record.ExceptionAsync(
                () => runtime.Dex.StartAsync(Serial, Identity, CancellationToken.None));

            var invocations = adb.Invocations;
            var wakeIndex = IndexOfFirst(invocations, "input keyevent 224");
            var trustIndex = IndexOfFirst(invocations, "dumpsys trust");

            Assert.True(
                wakeIndex >= 0,
                "The wake command (input keyevent 224) was never sent.");
            Assert.True(
                trustIndex >= 0,
                "The lock probe (dumpsys trust) was never sent.");
            Assert.True(
                wakeIndex < trustIndex,
                "The wake command must be sent before the lock probe, so the " +
                "gate judges the device's state after waking, not before.");
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
