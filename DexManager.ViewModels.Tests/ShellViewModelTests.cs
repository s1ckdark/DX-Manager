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

    [Fact]
    public void SelectionPostedBeforeDispose_IsIgnoredWhenItRuns()
    {
        using var temp = new TempHostRoot();
        var host = temp.CreateHost();
        var dispatcher = new QueueingUiDispatcher();
        var shell = new ShellViewModel(host, dispatcher);

        var before = shell.StatusText;

        // 이벤트가 Post까지 도달한 뒤 Dispose가 끼어드는 상황이다.
        // 구독 해제는 이미 큐에 들어간 클로저를 되돌리지 못하므로,
        // 실행 시점 방어가 없으면 해제된 ViewModel의 상태가 바뀐다.
        host.SelectedSerial = "USB-A";
        Assert.Equal(1, dispatcher.PendingCount);

        shell.Dispose();
        Assert.Equal(1, dispatcher.Drain());

        Assert.Equal(before, shell.StatusText);
    }
}
