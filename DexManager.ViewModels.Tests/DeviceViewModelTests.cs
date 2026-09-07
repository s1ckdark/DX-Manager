using DexManager.Models;
using DexManager.Services;
using Xunit;

namespace DexManager.ViewModels.Tests;

public class DeviceViewModelTests
{
    private static PhysicalDeviceInfo Device(params DeviceTransportInfo[] transports)
        => new PhysicalDeviceInfo
        {
            Identity = "phone-a",
            DisplayName = "Galaxy A",
            Transports = transports.ToList()
        };

    private static DeviceTransportInfo Transport(
        string serial,
        DeviceTransportKind kind,
        AdbDeviceStatus status = AdbDeviceStatus.Device)
        => new DeviceTransportInfo
        {
            Serial = serial,
            Kind = kind,
            Status = status,
            RawStatus = status.ToString().ToLowerInvariant()
        };

    [Fact]
    public void SummarizesEveryTransport()
    {
        var vm = new DeviceViewModel(
            Device(
                Transport("USB-A", DeviceTransportKind.Usb),
                Transport("10.0.0.2:5555", DeviceTransportKind.Wireless)),
            new DeviceRuntimeSessionRegistry(),
            new ImmediateUiDispatcher());

        Assert.Equal("Galaxy A", vm.DisplayName);
        Assert.Equal("phone-a", vm.Identity);
        Assert.Contains("Usb: USB-A", vm.TransportSummary);
        Assert.Contains("Wireless: 10.0.0.2:5555", vm.TransportSummary);
    }

    [Fact]
    public void UpdateRaisesPropertyChangedForChangedValuesOnly()
    {
        var vm = new DeviceViewModel(
            Device(Transport("USB-A", DeviceTransportKind.Usb)),
            new DeviceRuntimeSessionRegistry(),
            new ImmediateUiDispatcher());

        var changed = new List<string>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.Update(Device(Transport("USB-A", DeviceTransportKind.Usb)));

        Assert.DoesNotContain(nameof(DeviceViewModel.DisplayName), changed);
        Assert.DoesNotContain(nameof(DeviceViewModel.PrimarySerial), changed);
    }

    [Fact]
    public void UnauthorizedDeviceIsNotConnected()
    {
        var vm = new DeviceViewModel(
            Device(Transport(
                "USB-A", DeviceTransportKind.Usb, AdbDeviceStatus.Unauthorized)),
            new DeviceRuntimeSessionRegistry(),
            new ImmediateUiDispatcher());

        Assert.False(vm.IsConnected);
    }

    [Fact]
    public void Update_AfterDispose_DoesNotChangeObservableProperties()
    {
        var vm = new DeviceViewModel(
            Device(Transport("USB-A", DeviceTransportKind.Usb)),
            new DeviceRuntimeSessionRegistry(),
            new ImmediateUiDispatcher());
        var originalName = vm.DisplayName;
        var originalSerial = vm.PrimarySerial;
        var originalConnected = vm.IsConnected;

        vm.Dispose();

        // 변경된 정보로 갱신하려 시도한다.
        vm.Update(Device(
            Transport("USB-B", DeviceTransportKind.Wireless, AdbDeviceStatus.Offline)));

        // 해제된 행은 갱신되지 않아야 한다.
        Assert.Equal(originalName, vm.DisplayName);
        Assert.Equal(originalSerial, vm.PrimarySerial);
        Assert.Equal(originalConnected, vm.IsConnected);
    }

    [Fact]
    public void DexRunningStateFollowsTheRuntimeSessionRegistry()
    {
        var sessions = new DeviceRuntimeSessionRegistry();
        var devices = new PhysicalDeviceRegistry();
        devices.Reconcile(new[] { Discovered("phone-a", "Galaxy A", "AAA") });
        sessions.Reconcile(devices.Current);

        var dispatcher = new ImmediateUiDispatcher();
        using var vm = new DeviceViewModel(
            devices.Current.Devices.Single(),
            sessions,
            dispatcher);

        Assert.False(vm.IsDexRunning);

        sessions.SetDexSession("AAA", new ManagedDisplaySession
        {
            Serial = "AAA",
            DeviceIdentity = "phone-a",
            DisplayId = 47,
            ScrcpyProcessId = 1234
        });

        Assert.True(vm.IsDexRunning);

        sessions.SetDexSession("AAA", null);

        Assert.False(vm.IsDexRunning);
    }

    [Fact]
    public void DisposedRowStopsFollowingTheRegistry()
    {
        var sessions = new DeviceRuntimeSessionRegistry();
        var devices = new PhysicalDeviceRegistry();
        devices.Reconcile(new[] { Discovered("phone-a", "Galaxy A", "AAA") });
        sessions.Reconcile(devices.Current);

        var dispatcher = new QueueingUiDispatcher();
        var vm = new DeviceViewModel(
            devices.Current.Devices.Single(),
            sessions,
            dispatcher);

        sessions.SetDexSession("AAA", new ManagedDisplaySession
        {
            Serial = "AAA",
            DeviceIdentity = "phone-a"
        });

        vm.Dispose();

        // Post는 비동기다. 구독을 해제해도 이미 큐에 들어간 클로저는
        // 되돌릴 수 없으므로 실행 시점에 다시 확인해야 한다.
        dispatcher.Drain();

        Assert.False(vm.IsDexRunning);
    }

    private static DiscoveredDeviceTransport Discovered(
        string identity,
        string name,
        string serial,
        DeviceTransportKind kind = DeviceTransportKind.Usb,
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
}
