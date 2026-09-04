using DexManager.Models;
using DexManager.Services;
using Xunit;

namespace DexManager.ViewModels.Tests;

public class ShellViewModelTests
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
    public void SelectingADevicePushesItsSerialOntoTheHost()
    {
        using var temp = new TempHostRoot();
        var host = temp.CreateHost();
        using var shell = new ShellViewModel(host, new ImmediateUiDispatcher());

        host.DeviceRegistry.Reconcile(new[]
        {
            Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb),
            Device("phone-b", "Galaxy B", "USB-B", DeviceTransportKind.Usb)
        });

        shell.Devices.SelectedDevice =
            shell.Devices.Devices.First(d => d.Identity == "phone-b");

        Assert.Equal("USB-B", host.SelectedSerial);
    }

    [Fact]
    public void StatusTextFollowsTheHostSelection()
    {
        using var temp = new TempHostRoot();
        var host = temp.CreateHost();
        using var shell = new ShellViewModel(host, new ImmediateUiDispatcher());

        host.SelectedSerial = "USB-A";
        Assert.Equal("Selected USB-A", shell.StatusText);

        host.SelectedSerial = string.Empty;
        Assert.Equal("No device selected", shell.StatusText);
    }

    [Fact]
    public void Dispose_DisposesTheHostAndStopsResponding()
    {
        using var temp = new TempHostRoot();
        var host = temp.CreateHost();
        var shell = new ShellViewModel(host, new ImmediateUiDispatcher());

        shell.Dispose();

        Assert.True(host.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() => host.Start());
    }
}
