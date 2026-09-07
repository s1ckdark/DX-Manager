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
        using var list = new DeviceListViewModel(registry, new ImmediateUiDispatcher());

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
        using var list = new DeviceListViewModel(registry, new ImmediateUiDispatcher());

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
        using var list = new DeviceListViewModel(registry, new ImmediateUiDispatcher());

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
        using var list = new DeviceListViewModel(registry, dispatcher);

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
        var list = new DeviceListViewModel(registry, dispatcher);

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
        var list = new DeviceListViewModel(registry, new ImmediateUiDispatcher());

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
        using var list = new DeviceListViewModel(registry, new ImmediateUiDispatcher());

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
        using var list = new DeviceListViewModel(registry, new ImmediateUiDispatcher());

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
    public void ConstructionAppliesTheCurrentGenerationAndIgnoresOlderOnes()
    {
        var registry = new PhysicalDeviceRegistry();
        registry.Reconcile(new[]
        {
            Device("phone-a", "Galaxy A", "AAA", DeviceTransportKind.Usb),
            Device("phone-b", "Galaxy B", "BBB", DeviceTransportKind.Usb)
        });

        var dispatcher = new QueueingUiDispatcher();
        using var vm = new DeviceListViewModel(registry, dispatcher);

        // 생성자가 최신 스냅샷을 즉시 반영했다.
        Assert.Equal(2, vm.Devices.Count);

        // 한 기기가 빠진 새 스냅샷이 오면 세대가 높으므로 반영된다.
        registry.Reconcile(new[] { Device("phone-a", "Galaxy A", "AAA", DeviceTransportKind.Usb) });
        dispatcher.Drain();

        Assert.Single(vm.Devices);

        // 큐에 남아 있던 예전 세대가 뒤늦게 실행돼도 되돌리지 않는다.
        dispatcher.Drain();
        Assert.Single(vm.Devices);
    }
}
