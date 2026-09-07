using DexManager.Services;

namespace DexManager.Tests;

/// <remarks>
/// 코디네이터를 거쳐 런타임을 만든 테스트는 호스트를 <c>using</c>으로 해제하지
/// 않는다. 해제 경로가 이제 코디네이터가 아는 런타임마다 그 기기로 overlay
/// 회수를 시도하는데, 여기서 쓰는 identity·serial 뒤에는 실제 폰이 없어
/// 회수가 실패로 보고되기 때문이다. 그 회수 경로 자체는
/// <see cref="DexOrchestratorShutdownTests"/>가 실제 기기를 흉내 낸 adb로
/// 따로 고정한다. 여기서 확인할 것은 identity↔런타임 대응뿐이다.
/// 감시자를 시작하지 않은 호스트라 남는 스레드도 없다.
/// </remarks>
public class DeviceRuntimeCoordinatorTests
{
    [Fact]
    public void GetOrCreate_ReturnsTheSameRuntimeForTheSameIdentity()
    {
        using var root = new TempHostRoot();
        var host = root.CreateHost();
        var coordinator = host.RuntimeCoordinator;

        var first = coordinator.GetOrCreate("phone-a", "AAA");
        var second = coordinator.GetOrCreate("phone-a", "AAA");

        Assert.Same(first, second);
        Assert.Single(host.RuntimeFactory.CreatedInstances);
    }

    [Fact]
    public void GetOrCreate_ReturnsDistinctRuntimesForDistinctIdentities()
    {
        using var root = new TempHostRoot();
        var host = root.CreateHost();
        var coordinator = host.RuntimeCoordinator;

        var a = coordinator.GetOrCreate("phone-a", "AAA");
        var b = coordinator.GetOrCreate("phone-b", "BBB");

        Assert.NotSame(a, b);
        Assert.Equal(2, host.RuntimeFactory.CreatedInstances.Count);
    }

    [Fact]
    public void GetOrCreate_KeepsTheRuntimeWhenTheSerialChanges()
    {
        using var root = new TempHostRoot();
        var host = root.CreateHost();
        var coordinator = host.RuntimeCoordinator;

        // USB에서 무선으로 바뀌면 serial이 달라지지만 identity는 유지된다.
        var usb = coordinator.GetOrCreate("phone-a", "AAA");
        var wireless = coordinator.GetOrCreate("phone-a", "192.168.0.9:5555");

        Assert.Same(usb, wireless);
        Assert.Single(host.RuntimeFactory.CreatedInstances);
    }

    [Fact]
    public void TryGet_ReportsWhetherARuntimeExists()
    {
        using var root = new TempHostRoot();
        var host = root.CreateHost();
        var coordinator = host.RuntimeCoordinator;

        Assert.False(coordinator.TryGet("phone-a", out _));

        var created = coordinator.GetOrCreate("phone-a", "AAA");

        Assert.True(coordinator.TryGet("phone-a", out var found));
        Assert.Same(created, found);
    }

    [Fact]
    public void TryGetBinding_ReportsTheIdentityAndTheLastBoundSerial()
    {
        using var root = new TempHostRoot();
        var host = root.CreateHost();
        var coordinator = host.RuntimeCoordinator;

        var runtime = coordinator.GetOrCreate("phone-a", "AAA");
        // transport가 바뀌면 마지막으로 결속된 serial을 따라가야 한다.
        coordinator.GetOrCreate("phone-a", "192.168.0.9:5555");

        Assert.True(
            coordinator.TryGetBinding(runtime.InstanceId, out var binding));
        Assert.Equal("phone-a", binding.Identity);
        Assert.Equal("192.168.0.9:5555", binding.Serial);
    }

    [Fact]
    public void TryGetBinding_KeepsTheKnownSerialWhenCalledWithoutOne()
    {
        using var root = new TempHostRoot();
        var host = root.CreateHost();
        var coordinator = host.RuntimeCoordinator;

        var runtime = coordinator.GetOrCreate("phone-a", "AAA");
        // serial 없이 부른 호출이 이미 알던 결속을 지우면, 정리 경로가
        // 회수 대상을 잃는다.
        coordinator.GetOrCreate("phone-a", null);

        Assert.True(
            coordinator.TryGetBinding(runtime.InstanceId, out var binding));
        Assert.Equal("AAA", binding.Serial);
    }

    [Fact]
    public void TryGetBinding_DoesNotClaimARuntimeItDidNotCreate()
    {
        using var root = new TempHostRoot();
        var host = root.CreateHost();

        // TUI는 코디네이터를 거치지 않고 팩토리에서 곧바로 만든다. 그 런타임에
        // 대해서는 코디네이터가 모른다고 답해야 호출자가 자기 대체값을 쓴다.
        var direct = host.RuntimeFactory.Create();

        Assert.False(
            host.RuntimeCoordinator.TryGetBinding(direct.InstanceId, out _));
        Assert.False(
            host.RuntimeCoordinator.TryGetBinding(Guid.Empty, out _));
    }

    [Fact]
    public void GetOrCreate_RejectsAnEmptyIdentity()
    {
        using var root = new TempHostRoot();
        using var host = root.CreateHost();

        Assert.Throws<ArgumentException>(
            () => host.RuntimeCoordinator.GetOrCreate("  ", "AAA"));
    }
}
