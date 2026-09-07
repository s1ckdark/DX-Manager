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
    public async Task StartDexCommand_ReportsBusyAndBlocksCanExecuteWhileRunning()
    {
        // 주의: 이 테스트의 이름과 실제로 검증하는 것을 정확히 구분해야
        // 한다. CommunityToolkit.Mvvm의 [RelayCommand]가 만드는
        // AsyncRelayCommand는 기본적으로 재진입을 막는다(명령 인스턴스
        // 하나 안에서). 즉 CanExecute(null)이 false인데도 ExecuteAsync를
        // 다시 부르면(버튼이 아니라 코드로 직접) 실제로 동시 실행될 수
        // 있다는 것이 확인되었다 — 그래서 여기서는 두 번째 ExecuteAsync를
        // 직접 부르지 않는다. 이 테스트가 실제로 검증하는 것은 "실행
        // 중에는 IsBusy가 true이고 CanExecute가 false를 보고한다"는
        // 것뿐이다. 아래 Assert.Single은 실행을 한 번만 걸었으니 호출도
        // 한 번뿐이라는 당연한 사실만 확인한다 — 재진입 방지 자체의
        // 증거는 아니다. 명령 사이의 실제 교차 잠금(Start 진행 중에
        // Stop이 막히는 것)은 StopDexCommand_IsNotExecutableWhileStartIsRunning이
        // 검증한다 — IsBusy가 두 명령의 CanExecute에 공유되기 때문에
        // 가능한 보장이며, 이는 AsyncRelayCommand의 재진입 방지가 명령
        // 인스턴스별로만 적용되어 주지 못하는 것이다.
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
    public async Task StopDexCommand_IsNotExecutableWhileStartIsRunning()
    {
        // StartDexCommand와 StopDexCommand는 서로 다른 AsyncRelayCommand
        // 인스턴스다. 툴킷의 재진입 방지는 명령 인스턴스 안에서만
        // 작동하므로, Start가 진행 중일 때 Stop이 실행 가능한지 여부는
        // 툴킷이 아니라 두 명령이 공유하는 IsBusy → CanRunCommand에
        // 달려 있다. 이 테스트가 그 교차 잠금을 직접 검증한다.
        var commands = new FakeDeviceCommands
        {
            StartGate = new TaskCompletionSource<bool>()
        };
        using var vm = CreateRow("phone-a", "AAA", commands, out _);

        var first = vm.StartDexCommand.ExecuteAsync(null);

        Assert.False(vm.StopDexCommand.CanExecute(null));

        commands.StartGate.SetResult(true);
        await first;

        Assert.True(vm.StopDexCommand.CanExecute(null));
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
