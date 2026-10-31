using DexManager.Models;
using DexManager.Services;
using Xunit;

namespace DexManager.ViewModels.Tests.Settings;

/// <summary>
/// 실제 DeviceListViewModel 을 선택 소스로 쓰는 통합 검증. adb tcpip 처럼
/// 한 번의 폴링 동안 USB transport 가 사라져도 기기별 페이지가 유지돼야 한다.
/// </summary>
public class SettingsViewModelDeviceBlipTests
{
    private static DiscoveredDeviceTransport Usb(string serial) => new DiscoveredDeviceTransport
    {
        DeviceIdentity = "phone-a",
        DisplayName = "Galaxy A",
        Serial = serial,
        Kind = DeviceTransportKind.Usb,
        Status = AdbDeviceStatus.Device,
        RawStatus = "device"
    };

    [Fact]
    public void DeviceTransportBlip_KeepsThePerDevicePagesAlive()
    {
        var registry = new PhysicalDeviceRegistry();
        var dispatcher = new ImmediateUiDispatcher();
        registry.Reconcile(new[] { Usb("USB-A") });
        using var list = new DeviceListViewModel(
            registry, new DeviceRuntimeSessionRegistry(), dispatcher, new FakeDeviceCommands());
        Assert.Equal("phone-a", list.SelectedIdentity);

        var fake = new FakeWirelessConnectionGateway();
        fake.Devices["phone-a"] = new PhysicalDeviceInfo { Identity = "phone-a" };
        using var settings = new SettingsViewModel(
            new FakeSettingsGateway(), list, new DeviceRuntimeSessionRegistry(), dispatcher, fake);
        var connection = settings.Connection;
        var displayStream = settings.DisplayStream;
        Assert.NotNull(connection);

        registry.Reconcile(Array.Empty<DiscoveredDeviceTransport>());

        Assert.Same(connection, settings.Connection);
        Assert.Same(displayStream, settings.DisplayStream);
        var summaryBefore = connection.TransportSummary;
        fake.Devices["phone-a"].Transports.Add(new DeviceTransportInfo
        {
            Kind = DeviceTransportKind.Usb,
            Serial = "USB-X",
            Status = AdbDeviceStatus.Device
        });
        fake.RaiseDevicesChanged();
        Assert.NotEqual(summaryBefore, connection.TransportSummary); // 아직 구독 중 = 해제되지 않음

        registry.Reconcile(new[] { Usb("USB-A2") });

        Assert.Same(connection, settings.Connection);
        Assert.Same(displayStream, settings.DisplayStream);
    }
}
