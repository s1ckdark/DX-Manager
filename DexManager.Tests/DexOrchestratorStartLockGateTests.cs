using DexManager.Hosting;
using DexManager.Services;
using DexManager.Tests.FakePlatform;

namespace DexManager.Tests;

/// <summary>
/// DeX 시작 직전의 잠금 화면 검사를 고정한다. 근본 원인은 이미 확인됐다 —
/// DeX는 새 가상 디스플레이를 만들어 그걸 미러링하고, 잠금 화면은 항상
/// 기본 디스플레이(0)에만 그려지므로 DeX 미러로는 절대 잠금을 풀 수
/// 없다. 그래서 이 기능은 잠금을 우회하는 대신, 시작 전에 잠겨 있음을
/// 감지해 "먼저 폰을 직접 잠금 해제하라"고 명확히 알리는 쪽을 택했다.
///
/// 정책은 비대칭이다: 확신을 갖고 Locked라고 판단했을 때만 막는다.
/// Unknown(알 수 없음)이나 Unlocked는 그대로 진행한다 — 파싱이 뚫린
/// 상황에서 정상적으로 될 DeX 시작을 잘못 막는 것이 더 나쁜 실패
/// 방향이기 때문이다.
/// </summary>
public class DexOrchestratorStartLockGateTests
{
    private const string Serial = "phone-a";
    private const string Hardware = "PHONEAAA";
    private static readonly string Identity =
        FakeAdbExecutable.IdentityOf(Hardware);

    [Fact]
    public async Task StartAsync_WhenTheDumpsysOutputConfidentlyShowsLocked_AbortsWithTheLocalizedMessage()
    {
        using var root = new TempHostRoot();
        var adb = new FakeAdbExecutable(
            root.Root,
            new Dictionary<string, string> { [Serial] = Hardware },
            new Dictionary<string, string>
            {
                [Serial] = "mShowingLockscreen=true"
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
            // 잠긴 것으로 확신하면 overlay를 아예 만들지 않는다 — 시작
            // 시도 자체가 폰에 흔적을 남기면 안 된다.
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
    public async Task StartAsync_WhenTheDumpsysOutputShowsUnlocked_ProceedsPastTheLockGate()
    {
        using var root = new TempHostRoot();
        var adb = new FakeAdbExecutable(
            root.Root,
            new Dictionary<string, string> { [Serial] = Hardware },
            new Dictionary<string, string>
            {
                [Serial] = "mShowingLockscreen=false"
            });
        var host = root.CreateHost(pathProvider: adb.CreatePathProvider());
        // scrcpy와 실제 디스플레이 생성은 이 테스트 환경에 없으므로 이후
        // 단계는 결국 실패한다. 다만 그 실패는 잠금 검사와 무관한 실패여야
        // 한다 - 짧은 타임아웃으로 그 실패를 빠르게 받아낸다.
        host.UpdateSettings(settings =>
            settings.Timing.VirtualDisplayDetectionTimeoutMs = 50);
        try
        {
            var runtime = host.RuntimeCoordinator.GetOrCreate(Identity, Serial);

            var ex = await Record.ExceptionAsync(
                () => runtime.Dex.StartAsync(Serial, Identity, CancellationToken.None));

            Assert.NotNull(ex);
            Assert.NotEqual(
                LocalizationService.Get("Error.Dex.DeviceLocked"),
                ex.Message);
        }
        finally
        {
            host.Dispose();
        }
    }

    [Fact]
    public async Task StartAsync_WhenTheDumpsysOutputIsUnrecognized_FailsOpenAndProceedsPastTheLockGate()
    {
        using var root = new TempHostRoot();
        var adb = new FakeAdbExecutable(
            root.Root,
            new Dictionary<string, string> { [Serial] = Hardware },
            new Dictionary<string, string>
            {
                // 알려진 필드가 하나도 없는, 예상 밖 버전의 출력을 흉내낸다.
                [Serial] = "SOME_UNRECOGNIZED_DUMP_SHAPE"
            });
        var host = root.CreateHost(pathProvider: adb.CreatePathProvider());
        host.UpdateSettings(settings =>
            settings.Timing.VirtualDisplayDetectionTimeoutMs = 50);
        try
        {
            var runtime = host.RuntimeCoordinator.GetOrCreate(Identity, Serial);

            var ex = await Record.ExceptionAsync(
                () => runtime.Dex.StartAsync(Serial, Identity, CancellationToken.None));

            Assert.NotNull(ex);
            Assert.NotEqual(
                LocalizationService.Get("Error.Dex.DeviceLocked"),
                ex.Message);
        }
        finally
        {
            host.Dispose();
        }
    }
}
