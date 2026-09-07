using DexManager.Models;
using DexManager.Services;
using Xunit;

namespace DexManager.ViewModels.Tests;

public class DeviceListViewModelTests
{
    private static DiscoveredDeviceTransport Device(
        string identity,
        string name,
        string serial,
        DeviceTransportKind kind,
        AdbDeviceStatus status = AdbDeviceStatus.Device)
    {
        return new DiscoveredDeviceTransport
        {
            DeviceIdentity = identity,
            DisplayName = name,
            Serial = serial,
            Kind = kind,
            Status = status,
            RawStatus = status.ToString().ToLowerInvariant()
        };
    }

    [Fact]
    public void AddsDevicesFromSnapshotAndSelectsFirst()
    {
        var registry = new PhysicalDeviceRegistry();
        using var list = new DeviceListViewModel(registry, new DeviceRuntimeSessionRegistry(), new ImmediateUiDispatcher());

        registry.Reconcile(new[]
        {
            Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb),
            Device("phone-b", "Galaxy B", "USB-B", DeviceTransportKind.Usb)
        });

        Assert.Equal(2, list.Devices.Count);
        Assert.NotNull(list.SelectedDevice);
        Assert.Equal("Galaxy A", list.Devices[0].DisplayName);
        Assert.Equal("USB-A", list.Devices[0].PrimarySerial);
        Assert.True(list.Devices[0].IsConnected);
    }

    [Fact]
    public void ReusesViewModelInstanceWhenADeviceChanges()
    {
        var registry = new PhysicalDeviceRegistry();
        using var list = new DeviceListViewModel(registry, new DeviceRuntimeSessionRegistry(), new ImmediateUiDispatcher());

        registry.Reconcile(new[]
        {
            Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb),
            Device("phone-b", "Galaxy B", "USB-B", DeviceTransportKind.Usb)
        });

        var chosen = list.Devices.First(d => d.Identity == "phone-b");
        list.SelectedDevice = chosen;

        // phone-b 에 무선 transport 가 추가된다 — 스냅샷이 실제로 달라지므로
        // SnapshotChanged 가 발생하고 Apply 가 돌아간다.
        registry.Reconcile(new[]
        {
            Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb),
            Device("phone-b", "Galaxy B", "USB-B", DeviceTransportKind.Usb),
            Device("phone-b", "Galaxy B", "10.0.0.2:5555", DeviceTransportKind.Wireless)
        });

        Assert.Same(chosen, list.SelectedDevice);
        Assert.Contains("Wireless: 10.0.0.2:5555", chosen.TransportSummary);
    }

    [Fact]
    public void RemovesDisappearedDeviceAndMovesSelection()
    {
        var registry = new PhysicalDeviceRegistry();
        using var list = new DeviceListViewModel(registry, new DeviceRuntimeSessionRegistry(), new ImmediateUiDispatcher());

        registry.Reconcile(new[]
        {
            Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb),
            Device("phone-b", "Galaxy B", "USB-B", DeviceTransportKind.Usb)
        });
        list.SelectedDevice = list.Devices.First(d => d.Identity == "phone-b");

        registry.Reconcile(new[]
        {
            Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb)
        });

        Assert.Single(list.Devices);
        Assert.Equal("phone-a", list.SelectedDevice.Identity);
    }

    [Fact]
    public void MarshalsSnapshotChangesThroughTheDispatcher()
    {
        var registry = new PhysicalDeviceRegistry();
        var dispatcher = new QueueingUiDispatcher();
        using var list = new DeviceListViewModel(registry, new DeviceRuntimeSessionRegistry(), dispatcher);

        registry.Reconcile(new[]
        {
            Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb)
        });

        // 아직 디스패처를 비우지 않았다 — 스냅샷이 목록에 반영되면
        // 레지스트리 스레드에서 직접 건드렸다는 뜻이다.
        Assert.True(list.IsEmpty);
        Assert.Empty(list.Devices);

        dispatcher.Drain();

        // 큐에 들어 있던 작업이 실제로 스냅샷을 반영해야 한다.
        Assert.Equal(new[] { "phone-a" }, list.Devices.Select(d => d.Identity));
        Assert.False(list.IsEmpty);
    }

    [Fact]
    public void SnapshotPostedBeforeDispose_IsIgnoredWhenItRuns()
    {
        var registry = new PhysicalDeviceRegistry();
        var dispatcher = new QueueingUiDispatcher();
        var list = new DeviceListViewModel(registry, new DeviceRuntimeSessionRegistry(), dispatcher);

        // 스냅샷 변경이 Post까지 도달한 뒤 Dispose가 끼어든다.
        registry.Reconcile(new[]
        {
            Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb)
        });
        Assert.Equal(1, dispatcher.PendingCount);

        list.Dispose();
        Assert.Equal(1, dispatcher.Drain());

        Assert.Empty(list.Devices);
    }

    [Fact]
    public void Dispose_UnsubscribesFromRegistry()
    {
        var registry = new PhysicalDeviceRegistry();
        var list = new DeviceListViewModel(registry, new DeviceRuntimeSessionRegistry(), new ImmediateUiDispatcher());

        list.Dispose();

        registry.Reconcile(new[]
        {
            Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb)
        });

        Assert.Empty(list.Devices);
    }

    [Fact]
    public void ReordersDevicesToMatchTheRegistrysSortOrder()
    {
        var registry = new PhysicalDeviceRegistry();
        using var list = new DeviceListViewModel(registry, new DeviceRuntimeSessionRegistry(), new ImmediateUiDispatcher());

        registry.Reconcile(new[]
        {
            Device("phone-b", "Galaxy B", "USB-B", DeviceTransportKind.Usb),
            Device("phone-z", "Galaxy Z", "USB-Z", DeviceTransportKind.Usb)
        });

        var selected = list.Devices.First(d => d.Identity == "phone-z");
        list.SelectedDevice = selected;

        // 정렬 순서상 맨 앞에 와야 하는 기기가 새로 연결된다.
        registry.Reconcile(new[]
        {
            Device("phone-b", "Galaxy B", "USB-B", DeviceTransportKind.Usb),
            Device("phone-z", "Galaxy Z", "USB-Z", DeviceTransportKind.Usb),
            Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb)
        });

        Assert.Equal(
            new[] { "Galaxy A", "Galaxy B", "Galaxy Z" },
            list.Devices.Select(d => d.DisplayName));
        Assert.Same(selected, list.SelectedDevice);
    }

    [Fact]
    public void IsEmpty_TracksWhetherTheListHasDevices()
    {
        var registry = new PhysicalDeviceRegistry();
        using var list = new DeviceListViewModel(registry, new DeviceRuntimeSessionRegistry(), new ImmediateUiDispatcher());

        Assert.True(list.IsEmpty);

        registry.Reconcile(new[]
        {
            Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb)
        });
        Assert.False(list.IsEmpty);

        registry.Reconcile(Array.Empty<DiscoveredDeviceTransport>());
        Assert.True(list.IsEmpty);
    }

    [Fact]
    public void ConstructionAppliesThePreExistingSnapshotSynchronously()
    {
        // 주의: 이 테스트 이름이 말하는 것과 검증하는 것을 정확히 구분해야
        // 한다. 이 테스트는 "생성자의 첫 Apply가 디스패처를 거치지 않고
        // 동기로 실행된다"는 것만 검증한다. DeviceListViewModel.Apply의
        // 세대 가드(_appliedGeneration)는 검증하지 않는다 — 가드를
        // 완전히 제거해도 이 테스트는 그대로 통과한다(수동으로 확인함).
        // 가드가 막는 경합(구독-Apply 사이에 Reconcile이 여러 번 끼는 것)은
        // 주입할 수 있는 프로덕션 이음새가 없어 테스트로 재현할 수 없다.
        var registry = new PhysicalDeviceRegistry();
        registry.Reconcile(new[]
        {
            Device("phone-a", "Galaxy A", "AAA", DeviceTransportKind.Usb),
            Device("phone-b", "Galaxy B", "BBB", DeviceTransportKind.Usb)
        });

        var dispatcher = new QueueingUiDispatcher();
        using var vm = new DeviceListViewModel(registry, new DeviceRuntimeSessionRegistry(), dispatcher);

        // QueueingUiDispatcher를 썼는데도 Drain()을 한 번도 부르지 않은
        // 시점에 이미 두 기기가 보인다 — 생성자의 첫 Apply가 디스패처
        // 큐를 거치지 않고 직접 실행되었다는 증거다.
        Assert.Equal(2, vm.Devices.Count);

        // 생성 이후의 변경은 정상적으로 디스패처 큐를 거친다.
        registry.Reconcile(new[] { Device("phone-a", "Galaxy A", "AAA", DeviceTransportKind.Usb) });
        Assert.Equal(1, dispatcher.PendingCount);

        dispatcher.Drain();
        Assert.Single(vm.Devices);
    }

    [Fact]
    public void RemovingADeviceDisposesItsViewModel()
    {
        var registry = new PhysicalDeviceRegistry();
        registry.Reconcile(new[]
        {
            Device("phone-a", "Galaxy A", "AAA", DeviceTransportKind.Usb),
            Device("phone-b", "Galaxy B", "BBB", DeviceTransportKind.Usb)
        });

        var dispatcher = new ImmediateUiDispatcher();
        using var vm = new DeviceListViewModel(registry, new DeviceRuntimeSessionRegistry(), dispatcher);
        var removed = vm.Devices.Single(d => d.Identity == "phone-b");

        registry.Reconcile(new[] { Device("phone-a", "Galaxy A", "AAA", DeviceTransportKind.Usb) });

        Assert.True(removed.IsDisposed);
    }

    [Fact]
    public void DisposingTheListDisposesEveryRow()
    {
        var registry = new PhysicalDeviceRegistry();
        registry.Reconcile(new[] { Device("phone-a", "Galaxy A", "AAA", DeviceTransportKind.Usb) });

        var dispatcher = new ImmediateUiDispatcher();
        var vm = new DeviceListViewModel(registry, new DeviceRuntimeSessionRegistry(), dispatcher);
        var row = vm.Devices.Single();

        vm.Dispose();

        Assert.True(row.IsDisposed);
    }

    [Fact]
    public void RemovedRowStopsReactingToRuntimeSessionChanges()
    {
        // Apply가 행을 Dispose한 뒤 RemoveAt하는 순서를 지켜야 한다 — 순서가
        // 바뀌면 목록에서 빠진 행이 여전히 DeviceRuntimeSessionRegistry.Changed를
        // 구독한 채로 남아 IsDexRunning이 계속 움직인다.
        var registry = new PhysicalDeviceRegistry();
        var sessions = new DeviceRuntimeSessionRegistry();
        registry.Reconcile(new[]
        {
            Device("phone-a", "Galaxy A", "AAA", DeviceTransportKind.Usb),
            Device("phone-b", "Galaxy B", "BBB", DeviceTransportKind.Usb)
        });
        sessions.Reconcile(registry.Current);

        var dispatcher = new ImmediateUiDispatcher();
        using var vm = new DeviceListViewModel(registry, sessions, dispatcher);
        var removed = vm.Devices.Single(d => d.Identity == "phone-b");

        sessions.SetDexSession("BBB", new ManagedDisplaySession
        {
            Serial = "BBB",
            DeviceIdentity = "phone-b"
        });
        Assert.True(removed.IsDexRunning);

        // phone-b 를 목록에서 뺀다 — Apply가 removed.Dispose()를 먼저
        // 호출해 구독을 끊어야 한다.
        registry.Reconcile(new[] { Device("phone-a", "Galaxy A", "AAA", DeviceTransportKind.Usb) });
        Assert.True(removed.IsDisposed);

        // 제거된 뒤에도 레지스트리 변경을 계속 구독하고 있었다면 여기서
        // IsDexRunning이 false로 바뀐다 — 구독 해제가 실제로 관측된다.
        sessions.SetDexSession("BBB", null);

        Assert.True(removed.IsDexRunning);
    }
}
