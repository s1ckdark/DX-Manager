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
            new ImmediateUiDispatcher(),
            new FakeDeviceCommands());

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
            new ImmediateUiDispatcher(),
            new FakeDeviceCommands());

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
            new ImmediateUiDispatcher(),
            new FakeDeviceCommands());

        Assert.False(vm.IsConnected);
    }

    [Fact]
    public void Update_AfterDispose_DoesNotChangeObservableProperties()
    {
        var vm = new DeviceViewModel(
            Device(Transport("USB-A", DeviceTransportKind.Usb)),
            new DeviceRuntimeSessionRegistry(),
            new ImmediateUiDispatcher(),
            new FakeDeviceCommands());
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
            dispatcher,
            new FakeDeviceCommands());

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
            dispatcher,
            new FakeDeviceCommands());

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

    private static DeviceViewModel CreateRow(
        string identity,
        string serial,
        IDeviceRuntimeCommands commands,
        out DeviceRuntimeSessionRegistry sessions)
    {
        var devices = new PhysicalDeviceRegistry();
        devices.Reconcile(new[] { Discovered(identity, "Galaxy", serial) });
        sessions = new DeviceRuntimeSessionRegistry();
        sessions.Reconcile(devices.Current);

        return new DeviceViewModel(
            devices.Current.Devices.Single(),
            sessions,
            new ImmediateUiDispatcher(),
            commands);
    }

    [Fact]
    public async Task StartDexCommand_PassesTheRowsOwnIdentityAndSerial()
    {
        var commands = new FakeDeviceCommands();
        using var vm = CreateRow("phone-a", "AAA", commands, out _);

        await vm.StartDexCommand.ExecuteAsync(null);

        // 전역 SelectedSerial이 아니라 행 자신의 값이어야 한다.
        Assert.Equal(new[] { "start-dex:phone-a:AAA" }, commands.Calls);
    }

    [Fact]
    public async Task StopDexCommand_PassesTheRowsOwnIdentityAndSerial()
    {
        var commands = new FakeDeviceCommands();
        using var vm = CreateRow("phone-a", "AAA", commands, out _);

        await vm.StopDexCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "stop-dex:phone-a:AAA" }, commands.Calls);
    }

    [Fact]
    public async Task StartDexCommand_DoesNotRunTwiceWhileTheFirstIsStillRunning()
    {
        var commands = new FakeDeviceCommands
        {
            StartGate = new TaskCompletionSource<bool>()
        };
        using var vm = CreateRow("phone-a", "AAA", commands, out _);

        var first = vm.StartDexCommand.ExecuteAsync(null);
        Assert.True(vm.IsBusy);

        // 사용자가 버튼을 두 번 누른 상황이다. scrcpy 시작은 느리다.
        Assert.False(vm.StartDexCommand.CanExecute(null));

        commands.StartGate.SetResult(true);
        await first;

        Assert.False(vm.IsBusy);
        Assert.Single(commands.Calls);
    }

    [Fact]
    public async Task StartDexCommand_ReportsFailureWithoutThrowing()
    {
        var commands = new FakeDeviceCommands { StartResult = false };
        using var vm = CreateRow("phone-a", "AAA", commands, out _);

        await vm.StartDexCommand.ExecuteAsync(null);

        Assert.False(vm.IsBusy);
        Assert.Equal("DeX did not start.", vm.LastCommandMessage);
    }
}
