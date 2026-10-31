using DexManager.Models;
using Xunit;

namespace DexManager.ViewModels.Tests;

public class WirelessConnectionGatewayTests
{
    [Fact]
    public void GetProfile_WithoutSavedProfile_ReturnsNullAndDoesNotCreateOne()
    {
        using var root = new TempHostRoot();
        var host = root.CreateHost();
        using var gateway = new WirelessConnectionGateway(host);
        Assert.Null(gateway.GetProfile("phone-a"));
        Assert.Empty(host.Settings.DeviceWirelessConnectionProfiles);
    }

    [Fact]
    public void GetProfile_WithSavedProfile_ReturnsDetachedCopy()
    {
        using var root = new TempHostRoot();
        var host = root.CreateHost();
        host.UpdateSettings(s => { var p = s.GetOrCreateDeviceWirelessConnection("phone-a", false); p.Mode = AdbConnectionMode.Wireless; p.WirelessHost = "192.168.0.5"; p.WirelessPort = 5556; });
        using var gateway = new WirelessConnectionGateway(host);
        var copy = gateway.GetProfile("phone-a");
        Assert.Equal("192.168.0.5", copy.WirelessHost);
        Assert.Equal(5556, copy.WirelessPort);
        copy.WirelessHost = "changed";
        Assert.Equal("192.168.0.5", host.Settings.FindDeviceWirelessConnection("phone-a").WirelessHost);
    }

    [Fact]
    public void FindDevice_WhenRegistryEmpty_ReturnsNull()
    {
        using var root = new TempHostRoot();
        var host = root.CreateHost();
        using var gateway = new WirelessConnectionGateway(host);
        Assert.Null(gateway.FindDevice("phone-a"));
    }

    [Fact]
    public async Task DisconnectAsync_WithEmptyIdentity_FaultsWithArgumentException()
    {
        using var root = new TempHostRoot();
        var host = root.CreateHost();
        using var gateway = new WirelessConnectionGateway(host);
        await Assert.ThrowsAsync<ArgumentException>(() => gateway.DisconnectAsync("", CancellationToken.None));
    }
}
