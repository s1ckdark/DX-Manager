using DexManager.Hosting;
using DexManager.Services;

namespace DexManager.Tests;

public class DeviceRuntimeServiceFactoryTests
{
    [Fact]
    public void CreatedInstances_ReturnsEverySetInCreationOrder()
    {
        using var root = new TempHostRoot();
        using var host = root.CreateHost();
        var factory = host.RuntimeFactory;

        var first = factory.Create();
        var second = factory.Create();

        var created = factory.CreatedInstances;

        Assert.Equal(2, created.Count);
        Assert.Same(first, created[0]);
        Assert.Same(second, created[1]);
    }

    [Fact]
    public void CreatedInstances_IsSnapshotAndDoesNotObserveLaterCreations()
    {
        using var root = new TempHostRoot();
        using var host = root.CreateHost();
        var factory = host.RuntimeFactory;

        factory.Create();
        var snapshot = factory.CreatedInstances;
        factory.Create();

        // 정리 루프가 도는 도중 새 런타임이 생겨도 컬렉션이 변경되면 안 된다.
        Assert.Single(snapshot);
    }
}
