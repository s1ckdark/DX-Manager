using DexManager.Models;
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
        var vm = new DeviceViewModel(Device(
            Transport("USB-A", DeviceTransportKind.Usb),
            Transport("10.0.0.2:5555", DeviceTransportKind.Wireless)));

        Assert.Equal("Galaxy A", vm.DisplayName);
        Assert.Equal("phone-a", vm.Identity);
        Assert.Contains("Usb: USB-A", vm.TransportSummary);
        Assert.Contains("Wireless: 10.0.0.2:5555", vm.TransportSummary);
    }

    [Fact]
    public void UpdateRaisesPropertyChangedForChangedValuesOnly()
    {
        var vm = new DeviceViewModel(Device(Transport("USB-A", DeviceTransportKind.Usb)));

        var changed = new List<string>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.Update(Device(Transport("USB-A", DeviceTransportKind.Usb)));

        Assert.DoesNotContain(nameof(DeviceViewModel.DisplayName), changed);
        Assert.DoesNotContain(nameof(DeviceViewModel.PrimarySerial), changed);
    }

    [Fact]
    public void UnauthorizedDeviceIsNotConnected()
    {
        var vm = new DeviceViewModel(Device(
            Transport("USB-A", DeviceTransportKind.Usb, AdbDeviceStatus.Unauthorized)));

        Assert.False(vm.IsConnected);
    }

    [Fact]
    public void Update_AfterDispose_DoesNotChangeObservableProperties()
    {
        var vm = new DeviceViewModel(Device(Transport("USB-A", DeviceTransportKind.Usb)));
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
}
