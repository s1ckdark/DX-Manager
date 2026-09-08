using DexManager.Hosting;
using DexManager.Services;
using DexManager.Tests.FakePlatform;

namespace DexManager.Tests;

/// <summary>
/// DismissKeyguard 직후의 잠금 프로브가 고정 대기(150ms) + 단발 확인 대신
/// LockStatePoll을 쓰는 배선을 고정한다. LockStatePollTests가 이미 그
/// 정책(언제 재확인하고, 언제 멈추는지) 자체를 가짜 델리게이트로 빈틈없이
/// 고정했으므로, 여기서는 진짜 DexOrchestrator.StartAsync + FakeAdbExecutable
/// 조합으로 "이음매"만 검증한다 - 정책을 다시 반복하지 않는다.
///
/// FakeAdbExecutable.UpdateDumpsysTrustOutput으로 잠김 -&gt; 해제 전환을
/// 흉내낸다 - 스크립트가 매 호출마다 파일을 다시 읽으므로 폴링 도중
/// 값을 바꿔도 다음 확인부터 바로 반영된다.
/// </summary>
public class DexOrchestratorDismissKeyguardPollTests
{
    private const string Serial = "phone-a";
    private const string Hardware = "PHONEAAA";
    private static readonly string Identity =
        FakeAdbExecutable.IdentityOf(Hardware);

    private const string LockedTrustOutput =
        " User \"Owner\" (id=0, flags=0x4c13) (current): " +
        "trustState=UNTRUSTED, trustManaged=0, deviceLocked=1, " +
        "isActiveUnlockRunning=0, strongAuthRequired=0x1";

    private const string UnlockedTrustOutput =
        " User \"Owner\" (id=0, flags=0x4c13) (current): " +
        "trustState=TRUSTED, trustManaged=1, deviceLocked=0, " +
        "isActiveUnlockRunning=0, strongAuthRequired=0x0";

    [Fact]
    public async Task StartAsync_DeviceUnlocksPartwayThroughTheBudget_StopsPollingEarlyAndProceeds()
    {
        using var root = new TempHostRoot();
        var adb = new FakeAdbExecutable(
            root.Root,
            new Dictionary<string, string> { [Serial] = Hardware },
            dumpsysTrustOutputByTransport: new Dictionary<string, string>
            {
                [Serial] = LockedTrustOutput
            });
        var host = root.CreateHost(pathProvider: adb.CreatePathProvider());
        host.UpdateSettings(settings =>
            settings.Timing.VirtualDisplayDetectionTimeoutMs = 50);
        try
        {
            var runtime = host.RuntimeCoordinator.GetOrCreate(Identity, Serial);

            // 첫 확인은 Locked를 읽어야 폴 루프가 실제로 도는지 검증할 수
            // 있다. WakeScreen -> 300ms 대기 -> DismissKeyguard까지가
            // 이미 첫 프로브 전에 걸리므로(WakeSettleDelayMs), 그보다
            // 확실히 뒤(간격 150ms를 최소 두 번 넘긴 시점)에 풀어 줘야
            // "이미 첫 확인부터 풀려 있었다"와 구분된다.
            var unlockAfterDelay = Task.Run(delegate
            {
                Thread.Sleep(700);
                adb.UpdateDumpsysTrustOutput(Serial, UnlockedTrustOutput);
            });

            var ex = await Record.ExceptionAsync(
                () => runtime.Dex.StartAsync(Serial, Identity, CancellationToken.None));
            await unlockAfterDelay;

            Assert.NotEqual(
                LocalizationService.Get("Error.Dex.DeviceLocked"),
                ex?.Message);

            var trustProbeCount = adb.Invocations
                .Count(line => line.Contains("dumpsys trust", StringComparison.Ordinal));
            // 예산(2000ms)/간격(150ms)을 다 썼다면 확인이 14번 안팎까지
            // 갔을 것이다. 350ms 만에 풀렸는데도 그만큼 돌았다면 폴이
            // 조기 종료하지 못하고 있다는 뜻이다 - 넉넉한 여유를 두고도
            // 뚜렷이 구분되는 낮은 상한으로 "일찍 멈췄다"를 증명한다.
            Assert.True(
                trustProbeCount <= 8,
                $"expected the poll to exit early (<=8 checks), but saw {trustProbeCount}");
            Assert.True(
                trustProbeCount >= 2,
                "expected at least one re-check before the device unlocked, " +
                $"but saw {trustProbeCount}");
        }
        finally
        {
            host.Dispose();
        }
    }

    [Fact]
    public async Task StartAsync_DeviceStaysLockedForTheWholeBudget_PollsRepeatedlyThenAbortsWithTheLockedError()
    {
        using var root = new TempHostRoot();
        var adb = new FakeAdbExecutable(
            root.Root,
            new Dictionary<string, string> { [Serial] = Hardware },
            dumpsysTrustOutputByTransport: new Dictionary<string, string>
            {
                [Serial] = LockedTrustOutput
            });
        var host = root.CreateHost(pathProvider: adb.CreatePathProvider());
        try
        {
            var runtime = host.RuntimeCoordinator.GetOrCreate(Identity, Serial);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => runtime.Dex.StartAsync(Serial, Identity, CancellationToken.None));

            Assert.Equal(
                LocalizationService.Get("Error.Dex.DeviceLocked"),
                ex.Message);
            // "예산을 다 쓰고 포기했다"를 "한 번만 보고 바로 포기했다"와
            // 구분하는 핵심 단언 - 진짜로 여러 번 재확인했다는 증거가
            // 없으면, 이 테스트는 폴로 바꾼 것과 예전 단발 확인을 구분하지
            // 못한다.
            var trustProbeCount = adb.Invocations
                .Count(line => line.Contains("dumpsys trust", StringComparison.Ordinal));
            Assert.True(
                trustProbeCount >= 5,
                "expected several re-checks before giving up (budget " +
                $"2000ms / interval 150ms), but saw only {trustProbeCount}");
        }
        finally
        {
            host.Dispose();
        }
    }
}
