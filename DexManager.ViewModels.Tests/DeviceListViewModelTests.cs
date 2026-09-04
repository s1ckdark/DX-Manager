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
        var dispatcher = new ImmediateUiDispatcher();
        using var list = new DeviceListViewModel(registry, dispatcher);

        var before = dispatcher.PostCount;
        registry.Reconcile(new[]
        {
            Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb)
        });

        Assert.True(dispatcher.PostCount > before);
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
}
