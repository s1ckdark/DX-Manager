using System.Reflection;
using DexManager.Models;
using DexManager.Services;
using DexManager.Tests.FakePlatform;

namespace DexManager.Tests;

/// <summary>
/// DeX overlay 회수의 종료 경로를 고정한다. AGENTS.md가 요구하는 불변식은
/// 하나다 — overlay는 반드시 폰에서 회수된다. 회수하지 못한 overlay는
/// <c>settings global</c> 값으로 남아 재부팅해도 살아남는다.
/// </summary>
public class DexOrchestratorShutdownTests
{
    private const string SerialA = "phone-a";
    private const string HardwareA = "PHONEAAA";
    private const string SerialB = "phone-b";
    private const string HardwareB = "PHONEBBB";
    private static readonly string IdentityA =
        FakeAdbExecutable.IdentityOf(HardwareA);
    private static readonly string IdentityB =
        FakeAdbExecutable.IdentityOf(HardwareB);
    private const string OverlayResetCommand =
        "settings delete global overlay_display_devices";

    [Fact]
    public async Task ShutdownAsync_WithNoSessionAndNoTarget_NeitherThrowsNorTouchesTheDevice()
    {
        // 이 가드는 지금까지 어떤 테스트도 고정하지 않고 있었다.
        // 세션도 없고 대상 serial·identity도 없으면 회수할 overlay도,
        // 명령을 보낼 기기도 없다. 이때 던지는 예외는 정리 실패가 아니라
        // 대상 부재를 실패로 잘못 보고하는 것이므로 시도하지 않는다.
        using var root = new TempHostRoot();
        var adb = new FakeAdbExecutable(
            root.Root,
            new Dictionary<string, string>());
        var host = root.CreateHost(pathProvider: adb.CreatePathProvider());
        var runtime = host.RuntimeFactory.Create();
        // 호스트를 조립하는 동안 경로 탐색이 실행한 `adb version`을 걷어낸다.
        adb.ClearInvocations();

        await runtime.Dex.ShutdownAsync(null, null);

        Assert.True(runtime.Dex.IsCleanupComplete);
        // 회수 시도 자체가 없어야 한다. 다만 이 단언만으로는 가드를 고정하지
        // 못한다 — 가드를 없애도 이 상태에서는 명령이 나가기 전에 대상 탐색이
        // 먼저 실패하기 때문이다. 가드를 고정하는 것은 위의 "던지지 않는다"다.
        Assert.Empty(adb.Invocations);
    }

    [Fact]
    public async Task HostShutdown_ReclaimsADeferredOverlayForACoordinatorRuntimeWithoutFallbacks()
    {
        // GUI 종료 경로(ApplicationHost.Dispose)는 언제나 (null, null)로
        // 부른다. 세션이 이미 끝나고 회수만 밀린 런타임이 이때 통째로
        // 건너뛰어지면 overlay가 폰에 남는다.
        using var root = new TempHostRoot();
        var adb = new FakeAdbExecutable(
            root.Root,
            new Dictionary<string, string> { [SerialA] = HardwareA });
        var host = root.CreateHost(pathProvider: adb.CreatePathProvider());

        var runtime = host.RuntimeCoordinator.GetOrCreate(IdentityA, SerialA);
        DeferDisplayCleanup(runtime.Dex, SerialA, IdentityA);
        Assert.True(runtime.Dex.HasDeferredDisplayCleanup);
        adb.ClearInvocations();

        var errors = await host.ShutdownAsync(null, null);

        Assert.False(runtime.Dex.HasDeferredDisplayCleanup);
        Assert.Contains(adb.Invocations, IsOverlayResetFor(SerialA));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task HostShutdown_AimsEachCoordinatorRuntimeAtItsOwnDevice()
    {
        // 런타임이 둘 이상일 때 호출자가 준 대체값 하나를 모든 런타임에
        // 뿌리면, 한 기기의 serial·identity가 다른 기기의 런타임을 겨눈다.
        // 여기서는 대체값이 A를 가리키는데도 B가 자기 기기로 회수되어야 한다.
        using var root = new TempHostRoot();
        var adb = new FakeAdbExecutable(
            root.Root,
            new Dictionary<string, string>
            {
                [SerialA] = HardwareA,
                [SerialB] = HardwareB
            });
        var host = root.CreateHost(pathProvider: adb.CreatePathProvider());

        var runtimeA = host.RuntimeCoordinator.GetOrCreate(IdentityA, SerialA);
        var runtimeB = host.RuntimeCoordinator.GetOrCreate(IdentityB, SerialB);
        DeferDisplayCleanup(runtimeA.Dex, SerialA, IdentityA);
        DeferDisplayCleanup(runtimeB.Dex, SerialB, IdentityB);
        adb.ClearInvocations();

        var errors = await host.ShutdownAsync(SerialA, IdentityA);

        Assert.False(runtimeA.Dex.HasDeferredDisplayCleanup);
        Assert.False(runtimeB.Dex.HasDeferredDisplayCleanup);
        Assert.Contains(adb.Invocations, IsOverlayResetFor(SerialA));
        Assert.Contains(adb.Invocations, IsOverlayResetFor(SerialB));
        Assert.Empty(errors);
    }

    private static Predicate<string> IsOverlayResetFor(string serial)
    {
        return line =>
            line.Contains("-s " + serial, StringComparison.Ordinal) &&
            line.Contains(OverlayResetCommand, StringComparison.Ordinal);
    }

    /// <summary>
    /// 회수가 밀린 상태를 만든다. DeX 세션이 끝났는데 폰이 응답하지 않아
    /// lease를 놓지 못했을 때 프로덕션 코드가 실제로 부르는 그 메서드를
    /// 그대로 부른다 — 실제 흐름과의 차이는 lease 해제를 실패시킬 실물
    /// 기기가 없다는 것뿐이다.
    /// </summary>
    private static void DeferDisplayCleanup(
        DexOrchestrator dex,
        string serial,
        string identity)
    {
        var method = typeof(DexOrchestrator).GetMethod(
            "DeferDisplayCleanup",
            BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            new[] { typeof(VirtualDisplayLease), typeof(string) },
            null);
        Assert.NotNull(method);
        method.Invoke(
            dex,
            new object[]
            {
                new VirtualDisplayLease
                {
                    Serial = serial,
                    DisplayId = 2,
                    AppliedOverlaySetting = "1600x900/150,hdmi",
                    OwnsOverlaySetting = true
                },
                identity
            });
    }
}
