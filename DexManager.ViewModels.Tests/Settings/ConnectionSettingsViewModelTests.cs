using DexManager.Models;
using DexManager.Services;
using Xunit;

namespace DexManager.ViewModels.Tests.Settings;

public class ConnectionSettingsViewModelTests
{
    private const string Id = "phone-a";

    private static PhysicalDeviceInfo MakeDevice(string identity, string usbSerial = null, string wirelessEndpoint = null)
    {
        var d = new PhysicalDeviceInfo { Identity = identity, DisplayName = identity };
        if (usbSerial != null)
            d.Transports.Add(new DeviceTransportInfo { Kind = DeviceTransportKind.Usb, Serial = usbSerial, Status = AdbDeviceStatus.Device });
        if (wirelessEndpoint != null)
            d.Transports.Add(new DeviceTransportInfo { Kind = DeviceTransportKind.Wireless, Serial = wirelessEndpoint, Status = AdbDeviceStatus.Device });
        return d;
    }

    private static ConnectionSettingsViewModel Create(FakeWirelessConnectionGateway gw, IUiDispatcher d = null) =>
        new ConnectionSettingsViewModel(Id, gw, d ?? new ImmediateUiDispatcher());

    private static DeviceWirelessConnectionProfile Wireless(string host, int port, bool auto = true) =>
        new DeviceWirelessConnectionProfile { DeviceIdentity = Id, Mode = AdbConnectionMode.Wireless, WirelessHost = host, WirelessPort = port, AutoReconnect = auto };

    [Fact]
    public void Constructor_WithSavedPortOutOfRange_ShowsPortMessage()
    {
        var gw = new FakeWirelessConnectionGateway();
        gw.Profiles[Id] = Wireless("192.168.0.5", 70000);

        var vm = Create(gw);

        Assert.Equal(LocalizationService.Get("Settings.Connection.PortOutOfRange"), vm.StatusMessage);
        Assert.False(vm.ConnectCommand.CanExecute(null));
    }

    [Fact]
    public void Constructor_WithoutSavedProfile_UsesDefaults()
    {
        var vm = Create(new FakeWirelessConnectionGateway());

        Assert.Equal("", vm.Host);
        Assert.Equal(5555, vm.Port);
        Assert.True(vm.AutoReconnect);
        Assert.Contains(LocalizationService.Get("Settings.Connection.ModeUsb"), vm.SavedModeText);
        Assert.Equal(LocalizationService.Get("Settings.Connection.NoTransport"), vm.TransportSummary);
        Assert.False(vm.HasUsbTransport);
        Assert.False(vm.CanDisconnect);
    }

    [Fact]
    public void Constructor_WithSavedWirelessProfile_LoadsFieldsAndAllowsDisconnect()
    {
        var gw = new FakeWirelessConnectionGateway();
        gw.Profiles[Id] = Wireless("192.168.0.5", 5556, false);

        var vm = Create(gw);

        Assert.Equal("192.168.0.5", vm.Host);
        Assert.Equal(5556, vm.Port);
        Assert.False(vm.AutoReconnect);
        Assert.True(vm.CanDisconnect);
        Assert.Contains("192.168.0.5:5556", vm.SavedModeText);
    }

    [Fact]
    public void PrepareWirelessCommand_WithoutUsbTransport_CannotExecute()
    {
        var vm = Create(new FakeWirelessConnectionGateway());
        Assert.False(vm.PrepareWirelessCommand.CanExecute(null));
    }

    [Fact]
    public async Task PrepareWirelessCommand_WithUsbTransport_CallsEnableFromUsbWithThatSerialAndShowsEndpoint()
    {
        var gw = new FakeWirelessConnectionGateway();
        gw.Devices[Id] = MakeDevice(Id, usbSerial: "R5K");
        var vm = Create(gw);
        Assert.True(vm.PrepareWirelessCommand.CanExecute(null));

        await vm.PrepareWirelessCommand.ExecuteAsync(null);

        Assert.StartsWith("EnableFromUsb:phone-a:R5K:", gw.Calls[0]);
        Assert.Equal("ok (192.168.0.5:5555)", vm.StatusMessage);
        Assert.Equal("192.168.0.5", gw.Profiles[Id].WirelessHost);
        Assert.True(vm.CanDisconnect);
    }

    [Fact]
    public async Task ConnectCommand_CallsConnectWithHostPortAutoReconnect()
    {
        var gw = new FakeWirelessConnectionGateway();
        var vm = Create(gw);
        vm.Host = " 192.168.0.9 ";
        vm.Port = 5557;
        vm.AutoReconnect = false;

        await vm.ConnectCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "Connect:phone-a:192.168.0.9:5557:False" }, gw.Calls);
    }

    [Fact]
    public void ConnectCommand_WhenHostEmpty_CannotExecute()
    {
        var vm = Create(new FakeWirelessConnectionGateway());
        Assert.False(vm.ConnectCommand.CanExecute(null));
        vm.Host = "   ";
        Assert.False(vm.ConnectCommand.CanExecute(null));
        vm.Host = "10.0.0.1";
        Assert.True(vm.ConnectCommand.CanExecute(null));
    }

    [Fact]
    public void ConnectCommand_WhenPortOutOfRange_CannotExecute()
    {
        var vm = Create(new FakeWirelessConnectionGateway());
        vm.Host = "10.0.0.1";

        vm.Port = 70000;

        Assert.False(vm.IsPortValid);
        Assert.False(vm.ConnectCommand.CanExecute(null));
        Assert.Equal(LocalizationService.Get("Settings.Connection.PortOutOfRange"), vm.StatusMessage);

        vm.Port = 5555;
        Assert.True(vm.ConnectCommand.CanExecute(null));
        Assert.Equal("", vm.StatusMessage);
    }

    [Fact]
    public async Task DisconnectCommand_OnSuccess_RefreshesProfileToUsb()
    {
        var gw = new FakeWirelessConnectionGateway();
        gw.Profiles[Id] = Wireless("192.168.0.5", 5555);
        var vm = Create(gw);
        Assert.True(vm.DisconnectCommand.CanExecute(null));

        await vm.DisconnectCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "Disconnect:phone-a" }, gw.Calls);
        Assert.False(vm.CanDisconnect);
        Assert.Contains(LocalizationService.Get("Settings.Connection.ModeUsb"), vm.SavedModeText);
    }

    [Fact]
    public async Task Command_WhenGatewayThrows_ShowsLocalizedFailureAndClearsBusy()
    {
        var gw = new FakeWirelessConnectionGateway { ExceptionToThrow = new InvalidOperationException("boom") };
        var vm = Create(gw);
        vm.Host = "10.0.0.1";

        await vm.ConnectCommand.ExecuteAsync(null);

        Assert.Equal(LocalizationService.Format("Settings.WirelessOperationFailed", "boom"), vm.StatusMessage);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Command_WhileRunning_IsBusyAndOtherCommandsCannotExecute()
    {
        var gw = new FakeWirelessConnectionGateway { Gate = new TaskCompletionSource<WirelessConnectionResult>() };
        gw.Devices[Id] = MakeDevice(Id, usbSerial: "R5K");
        gw.Profiles[Id] = Wireless("10.0.0.1", 5555);
        var vm = Create(gw);

        var task = vm.ConnectCommand.ExecuteAsync(null);

        Assert.True(vm.IsBusy);
        Assert.False(vm.PrepareWirelessCommand.CanExecute(null));
        Assert.False(vm.ConnectCommand.CanExecute(null));
        Assert.False(vm.DisconnectCommand.CanExecute(null));

        gw.Gate.SetResult(WirelessConnectionResult.Succeeded("10.0.0.1:5555", "ok"));
        await task;

        Assert.False(vm.IsBusy);
        Assert.True(vm.PrepareWirelessCommand.CanExecute(null));
    }

    [Fact]
    public async Task Dispose_DuringPendingOperation_IgnoresLateResult()
    {
        var gw = new FakeWirelessConnectionGateway { Gate = new TaskCompletionSource<WirelessConnectionResult>() };
        var vm = Create(gw);
        vm.Host = "10.0.0.1";
        var task = vm.ConnectCommand.ExecuteAsync(null);
        var before = vm.StatusMessage;

        vm.Dispose();
        gw.Gate.SetResult(WirelessConnectionResult.Succeeded("10.0.0.1:5555", "ok"));
        await task;

        Assert.Equal(before, vm.StatusMessage);
    }

    [Fact]
    public void DevicesChanged_RefreshesTransportSummaryThroughDispatcher()
    {
        var gw = new FakeWirelessConnectionGateway();
        var dispatcher = new QueueingUiDispatcher();
        var vm = Create(gw, dispatcher);
        var before = vm.TransportSummary;

        gw.Devices[Id] = MakeDevice(Id, usbSerial: "R5K");
        gw.RaiseDevicesChanged();

        Assert.Equal(before, vm.TransportSummary);
        dispatcher.Drain();
        Assert.Contains("R5K", vm.TransportSummary);
        Assert.True(vm.HasUsbTransport);
    }

    [Fact]
    public void DevicesChanged_AfterDispose_DoesNothing()
    {
        var gw = new FakeWirelessConnectionGateway();
        var dispatcher = new QueueingUiDispatcher();
        var vm = Create(gw, dispatcher);
        var before = vm.TransportSummary;

        vm.Dispose();
        gw.Devices[Id] = MakeDevice(Id, usbSerial: "R5K");
        gw.RaiseDevicesChanged();
        dispatcher.Drain();

        Assert.Equal(0, dispatcher.PostCount);
        Assert.Equal(before, vm.TransportSummary);
    }
}
