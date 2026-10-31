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
            // 이 테스트가 고정하는 건 첫(짧은) 폴의 반복 재확인이다. 그 뒤의
            // 사용자 잠금 해제 대기(기본 15초)는 아래 전용 테스트들이
            // 고정하므로, 여기서는 짧게 줄여 스위트를 느리게 만들지 않는다.
            runtime.Dex.UserUnlockWaitBudget = TimeSpan.FromMilliseconds(600);

            // 두 번째 단계의 확인이 섞이지 않도록, 대기 로그가 남는 시점(첫
            // 폴이 끝난 직후)의 확인 횟수만 센다.
            var waitMessage = LocalizationService.Get(
                "Log.Dex.WaitingForUserUnlock");
            var firstPhaseProbeCount = -1;
            host.Log.EntryWritten += (_, e) =>
            {
                if (e.Message.Contains(waitMessage, StringComparison.Ordinal))
                    firstPhaseProbeCount = CountTrustProbes(adb);
            };

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => runtime.Dex.StartAsync(Serial, Identity, CancellationToken.None));

            Assert.Equal(
                LocalizationService.Get("Error.Dex.DeviceLocked"),
                ex.Message);
            // "예산을 다 쓰고 포기했다"를 "한 번만 보고 바로 포기했다"와
            // 구분하는 핵심 단언 - 진짜로 여러 번 재확인했다는 증거가
            // 없으면, 이 테스트는 폴로 바꾼 것과 예전 단발 확인을 구분하지
            // 못한다.
            Assert.True(
                firstPhaseProbeCount >= 5,
                "expected several re-checks before giving up (budget " +
                $"2000ms / interval 150ms), but saw only {firstPhaseProbeCount}");
        }
        finally
        {
            host.Dispose();
        }
    }

    // 실기 버그: PIN·지문·얼굴 인증이 필요한 폰은 사용자가 2초(첫 폴
    // 예산) 안에 잠금을 풀지 못해, 시작 버튼을 한 번 누르면 잠금만 풀리고
    // 두 번째 눌러야 연결됐다. 첫 폴이 Locked로 끝나도 바로 포기하지 않고
    // 사용자 잠금 해제를 더 기다리는 두 번째 단계를 고정한다.
    [Fact]
    public async Task StartAsync_DeviceUnlocksDuringTheUserUnlockWait_ProceedsWithoutTheLockedError()
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
            // 예산은 넉넉히 둔다 - 이 테스트에서는 예산을 다 쓰기 전에
            // 풀려야 하고, 다 썼다면 Locked 오류로 실패한다.
            runtime.Dex.UserUnlockWaitBudget = TimeSpan.FromSeconds(10);

            var waitMessage = LocalizationService.Get(
                "Log.Dex.WaitingForUserUnlock");
            var probesWhenWaitStarted = -1;
            Task unlockAfterDelay = Task.CompletedTask;
            host.Log.EntryWritten += (_, e) =>
            {
                if (!e.Message.Contains(waitMessage, StringComparison.Ordinal))
                    return;
                probesWhenWaitStarted = CountTrustProbes(adb);
                // 첫 폴은 이미 Locked로 끝났다. 두 번째 단계가 적어도 한 번
                // 더 Locked를 본 뒤 사용자가 잠금을 푼 상황을 흉내낸다.
                // 시간이 아니라 확인 횟수로 전환한다 - 부하가 큰 머신에서
                // 가짜 adb 프로세스 기동이 느려도, 두 번째 단계의 첫 확인이
                // 끝나고 다음 확인이 시작된 뒤에만 풀어 준다. 상한(10초)은
                // 시작이 예산 소진으로 끝난 경우 이 작업이 매달리지 않게 한다.
                var startedAt = probesWhenWaitStarted;
                unlockAfterDelay = Task.Run(delegate
                {
                    var giveUpAt = DateTime.UtcNow + TimeSpan.FromSeconds(10);
                    while (CountTrustProbes(adb) <= startedAt + 1 &&
                           DateTime.UtcNow < giveUpAt)
                        Thread.Sleep(20);
                    adb.UpdateDumpsysTrustOutput(Serial, UnlockedTrustOutput);
                });
            };

            var ex = await Record.ExceptionAsync(
                () => runtime.Dex.StartAsync(Serial, Identity, CancellationToken.None));
            await unlockAfterDelay;

            Assert.NotEqual(
                LocalizationService.Get("Error.Dex.DeviceLocked"),
                ex?.Message);
            Assert.True(
                probesWhenWaitStarted >= 0,
                "expected the start to enter the user-unlock wait after the short poll stayed locked");
            Assert.True(
                CountTrustProbes(adb) - probesWhenWaitStarted >= 2,
                "expected the user-unlock wait to re-probe until the device unlocked");
        }
        finally
        {
            host.Dispose();
        }
    }

    [Fact]
    public async Task StartAsync_DeviceStaysLockedThroughTheUserUnlockWait_ReprobesThenAbortsWithTheLockedError()
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
            runtime.Dex.UserUnlockWaitBudget = TimeSpan.FromMilliseconds(1200);

            var waitMessage = LocalizationService.Get(
                "Log.Dex.WaitingForUserUnlock");
            var probesWhenWaitStarted = -1;
            host.Log.EntryWritten += (_, e) =>
            {
                if (e.Message.Contains(waitMessage, StringComparison.Ordinal))
                    probesWhenWaitStarted = CountTrustProbes(adb);
            };

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => runtime.Dex.StartAsync(Serial, Identity, CancellationToken.None));

            Assert.Equal(
                LocalizationService.Get("Error.Dex.DeviceLocked"),
                ex.Message);
            Assert.True(
                probesWhenWaitStarted >= 0,
                "expected the start to enter the user-unlock wait before giving up");
            // 두 번째 단계가 실제로 다시 확인했다는 증거 - 로그만 남기고
            // 바로 던졌다면 첫 폴의 판단을 그대로 쓴 것과 구분되지 않는다.
            var reprobes = CountTrustProbes(adb) - probesWhenWaitStarted;
            Assert.True(
                reprobes >= 2,
                $"expected the user-unlock wait to re-probe, but saw {reprobes}");
            Assert.DoesNotContain(
                adb.Invocations,
                line => line.Contains(
                    "overlay_display_devices",
                    StringComparison.Ordinal) &&
                    !line.Contains("delete", StringComparison.Ordinal));
        }
        finally
        {
            host.Dispose();
        }
    }

    [Fact]
    public async Task StartAsync_ShutdownRequestedDuringTheUserUnlockWait_StopsWaitingPromptly()
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
            // 예산을 일부러 길게 둔다 - 종료 신호를 무시하고 예산을 다
            // 쓴다면 아래 시간 상한을 확실히 넘긴다.
            runtime.Dex.UserUnlockWaitBudget = TimeSpan.FromSeconds(30);

            var waitMessage = LocalizationService.Get(
                "Log.Dex.WaitingForUserUnlock");
            var probesAtShutdown = -1;
            Task shutdownAfterDelay = Task.CompletedTask;
            host.Log.EntryWritten += (_, e) =>
            {
                if (!e.Message.Contains(waitMessage, StringComparison.Ordinal))
                    return;
                shutdownAfterDelay = Task.Run(delegate
                {
                    Thread.Sleep(700);
                    probesAtShutdown = CountTrustProbes(adb);
                    runtime.Dex.RequestShutdown();
                });
            };

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var start = runtime.Dex.StartAsync(Serial, Identity, CancellationToken.None);
            var finished = await Task.WhenAny(start, Task.Delay(TimeSpan.FromSeconds(15)));
            stopwatch.Stop();

            Assert.Same(start, finished);
            await shutdownAfterDelay;
            Assert.True(
                probesAtShutdown >= 0,
                "expected the shutdown to land inside the user-unlock wait");
            // 종료 요청은 잠금 오류가 아니다 - 다른 종료 경로처럼 조용히
            // 시작을 포기한다.
            Assert.False(await start);
            Assert.True(
                stopwatch.Elapsed < TimeSpan.FromSeconds(15),
                $"expected the wait to stop promptly on shutdown, but took {stopwatch.Elapsed}");
            // 신호가 켜진 뒤 남은 확인을 빠르게 다 돌리지 않는다 - 진행 중이던
            // 확인 하나 이상은 나가지 않아야 한다.
            var probesAfterShutdown = CountTrustProbes(adb) - probesAtShutdown;
            Assert.True(
                probesAfterShutdown <= 1,
                $"expected no further lock probes after shutdown, but saw {probesAfterShutdown}");
        }
        finally
        {
            host.Dispose();
        }
    }

    private static int CountTrustProbes(FakeAdbExecutable adb) =>
        adb.Invocations.Count(
            line => line.Contains("dumpsys trust", StringComparison.Ordinal));
}
