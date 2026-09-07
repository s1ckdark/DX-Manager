using DexManager.Services;

namespace DexManager.Tests;

public class DeviceRuntimeCoordinatorTests
{
    [Fact]
    public void GetOrCreate_ReturnsTheSameRuntimeForTheSameIdentity()
    {
        using var root = new TempHostRoot();
        using var host = root.CreateHost();
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
        using var host = root.CreateHost();
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
        using var host = root.CreateHost();
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
        using var host = root.CreateHost();
        var coordinator = host.RuntimeCoordinator;

        Assert.False(coordinator.TryGet("phone-a", out _));

        var created = coordinator.GetOrCreate("phone-a", "AAA");

        Assert.True(coordinator.TryGet("phone-a", out var found));
        Assert.Same(created, found);
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
