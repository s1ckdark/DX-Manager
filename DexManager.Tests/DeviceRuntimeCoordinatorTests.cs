using DexManager.Hosting;
using DexManager.Services;

namespace DexManager.Tests;

/// <remarks>
/// 코디네이터를 거쳐 런타임을 만든 테스트는 호스트를 <c>try</c>/<c>finally</c>로
/// 감싸 <see cref="DisposeExpectingCleanupFailure"/>로 해제한다. 해제 경로가
/// 코디네이터가 아는 런타임마다 그 기기로 overlay 회수를 시도하는데, 여기서
/// 쓰는 identity·serial 뒤에는 실제 폰이 없어 회수가 실패로 보고되기
/// 때문이다 — 이는 회피할 부작용이 아니라 <see cref="ApplicationHost.Dispose"/>가
/// 실제로 던지는 <see cref="AggregateException"/>이므로, 여기서는 그 예외를
/// 명시적으로 단언해 고정한다. 그 회수 경로 자체는
/// <see cref="DexOrchestratorShutdownTests"/>가 실제 기기를 흉내 낸 adb로
/// 따로 고정한다. 여기서 확인할 것은 identity↔런타임 대응뿐이다.
/// 코디네이터를 거치지 않은 런타임(<see
/// cref="TryGetBinding_DoesNotClaimARuntimeItDidNotCreate"/> 참고)은 회수
/// 대상이 없어 정상적으로 해제되므로 <c>using</c>을 그대로 쓴다.
/// 감시자를 시작하지 않은 호스트라 남는 스레드도 없다.
/// </remarks>
public class DeviceRuntimeCoordinatorTests
{
    /// <summary>
    /// 코디네이터가 아는 런타임을 남긴 채 호스트를 해제하면 overlay 회수가
    /// 실제 기기를 찾지 못해 <see cref="AggregateException"/>이 발생한다.
    /// 이는 테스트가 피해야 할 부작용이 아니라 운영 환경에서도 그대로
    /// 나타나는 실제 동작이므로, 삼켜서 감추는 대신 여기서 명시적으로
    /// 검증해 고정한다 — 이 예외가 더 이상 나지 않게 되면 이 단언이
    /// 실패해 그 변화를 곧바로 알려준다.
    /// </summary>
    private static void DisposeExpectingCleanupFailure(ApplicationHost host)
        => Assert.Throws<AggregateException>(host.Dispose);

    [Fact]
    public void GetOrCreate_ReturnsTheSameRuntimeForTheSameIdentity()
    {
        using var root = new TempHostRoot();
        var host = root.CreateHost();
        try
        {
            var coordinator = host.RuntimeCoordinator;

            var first = coordinator.GetOrCreate("phone-a", "AAA");
            var second = coordinator.GetOrCreate("phone-a", "AAA");

            Assert.Same(first, second);
            Assert.Single(host.RuntimeFactory.CreatedInstances);
        }
        finally
        {
            DisposeExpectingCleanupFailure(host);
        }
    }

    [Fact]
    public void GetOrCreate_ReturnsDistinctRuntimesForDistinctIdentities()
    {
        using var root = new TempHostRoot();
        var host = root.CreateHost();
        try
        {
            var coordinator = host.RuntimeCoordinator;

            var a = coordinator.GetOrCreate("phone-a", "AAA");
            var b = coordinator.GetOrCreate("phone-b", "BBB");

            Assert.NotSame(a, b);
            Assert.Equal(2, host.RuntimeFactory.CreatedInstances.Count);
        }
        finally
        {
            DisposeExpectingCleanupFailure(host);
        }
    }

    [Fact]
    public void GetOrCreate_KeepsTheRuntimeWhenTheSerialChanges()
    {
        using var root = new TempHostRoot();
        var host = root.CreateHost();
        try
        {
            var coordinator = host.RuntimeCoordinator;

            // USB에서 무선으로 바뀌면 serial이 달라지지만 identity는 유지된다.
            var usb = coordinator.GetOrCreate("phone-a", "AAA");
            var wireless = coordinator.GetOrCreate(
                "phone-a",
                "192.168.0.9:5555");

            Assert.Same(usb, wireless);
            Assert.Single(host.RuntimeFactory.CreatedInstances);
        }
        finally
        {
            DisposeExpectingCleanupFailure(host);
        }
    }

    [Fact]
    public void TryGet_ReportsWhetherARuntimeExists()
    {
        using var root = new TempHostRoot();
        var host = root.CreateHost();
        try
        {
            var coordinator = host.RuntimeCoordinator;

            Assert.False(coordinator.TryGet("phone-a", out _));

            var created = coordinator.GetOrCreate("phone-a", "AAA");

            Assert.True(coordinator.TryGet("phone-a", out var found));
            Assert.Same(created, found);
        }
        finally
        {
            DisposeExpectingCleanupFailure(host);
        }
    }

    [Fact]
    public void TryGetBinding_ReportsTheIdentityAndTheLastBoundSerial()
    {
        using var root = new TempHostRoot();
        var host = root.CreateHost();
        try
        {
            var coordinator = host.RuntimeCoordinator;

            var runtime = coordinator.GetOrCreate("phone-a", "AAA");
            // transport가 바뀌면 마지막으로 결속된 serial을 따라가야 한다.
            coordinator.GetOrCreate("phone-a", "192.168.0.9:5555");

            Assert.True(
                coordinator.TryGetBinding(
                    runtime.InstanceId,
                    out var binding));
            Assert.Equal("phone-a", binding.Identity);
            Assert.Equal("192.168.0.9:5555", binding.Serial);
        }
        finally
        {
            DisposeExpectingCleanupFailure(host);
        }
    }

    [Fact]
    public void TryGetBinding_KeepsTheKnownSerialWhenCalledWithoutOne()
    {
        using var root = new TempHostRoot();
        var host = root.CreateHost();
        try
        {
            var coordinator = host.RuntimeCoordinator;

            var runtime = coordinator.GetOrCreate("phone-a", "AAA");
            // serial 없이 부른 호출이 이미 알던 결속을 지우면, 정리 경로가
            // 회수 대상을 잃는다.
            coordinator.GetOrCreate("phone-a", null);

            Assert.True(
                coordinator.TryGetBinding(
                    runtime.InstanceId,
                    out var binding));
            Assert.Equal("AAA", binding.Serial);
        }
        finally
        {
            DisposeExpectingCleanupFailure(host);
        }
    }

    [Fact]
    public void TryGetBinding_DoesNotClaimARuntimeItDidNotCreate()
    {
        using var root = new TempHostRoot();
        using var host = root.CreateHost();

        // TUI는 코디네이터를 거치지 않고 팩토리에서 곧바로 만든다. 그 런타임에
        // 대해서는 코디네이터가 모른다고 답해야 호출자가 자기 대체값을 쓴다.
        // 코디네이터가 이 런타임을 결속으로 남기지 않으므로 해제 시 overlay
        // 회수를 시도하지 않는다 — 그래서 이 호스트는 그냥 정리된다.
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
