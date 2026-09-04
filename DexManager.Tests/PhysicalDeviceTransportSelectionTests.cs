using DexManager.Models;
using Xunit;

namespace DexManager.Tests;

public class PhysicalDeviceTransportSelectionTests
{
    private static PhysicalDeviceInfo TwoTransportDevice() => new PhysicalDeviceInfo
    {
        Identity = "identity-1",
        DisplayName = "Test Device",
        Transports = new List<DeviceTransportInfo>
        {
            new DeviceTransportInfo
            {
                Serial = "USB-SERIAL",
                Kind = DeviceTransportKind.Usb,
                Status = AdbDeviceStatus.Device
            },
            new DeviceTransportInfo
            {
                Serial = "1.2.3.4:5555",
                Kind = DeviceTransportKind.Wireless,
                Status = AdbDeviceStatus.Device
            }
        }
    };

    // InteractiveHost의 serial 일원화는 null과 "" 가 같은 transport를 고르는 데
    // 의존한다. FindTransport의 IsNullOrWhiteSpace 가드가 사라지면 이 테스트가
    // 먼저 깨져야 한다.
    [Fact]
    public void SelectPreferredTransport_TreatsNullAndEmptyAlike()
    {
        var device = TwoTransportDevice();

        var fromNull = device.SelectPreferredTransport(null);
        var fromEmpty = device.SelectPreferredTransport(string.Empty);
        var fromWhitespace = device.SelectPreferredTransport("   ");

        Assert.NotNull(fromNull);
        Assert.Equal(fromNull.Serial, fromEmpty.Serial);
        Assert.Equal(fromNull.Serial, fromWhitespace.Serial);
    }

    [Fact]
    public void FindTransport_TreatsNullAndEmptyAlike()
    {
        var device = TwoTransportDevice();

        Assert.Null(device.FindTransport(null));
        Assert.Null(device.FindTransport(string.Empty));
        Assert.Null(device.FindTransport("   "));
    }
}
