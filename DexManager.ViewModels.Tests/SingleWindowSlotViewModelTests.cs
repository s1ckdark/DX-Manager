using DexManager.Models;
using DexManager.ViewModels;
using Xunit;

namespace DexManager.ViewModels.Tests;

public class SingleWindowSlotViewModelTests
{
    [Fact]
    public async Task StartCommand_PassesTheSlotAndTheCurrentSerial()
    {
        var commands = new FakeDeviceCommands();
        var serial = "AAA";
        var vm = new SingleWindowSlotViewModel(
            2, "phone-a", () => serial, commands)
        {
            AppPackage = "com.sec.android.app.sbrowser"
        };

        await vm.StartCommand.ExecuteAsync(null);

        Assert.Equal(
            new[] { "start-slot:phone-a:AAA:2:com.sec.android.app.sbrowser" },
            commands.Calls);
    }

    [Fact]
    public async Task StartCommand_UsesTheSerialAtInvocationTimeNotAtConstruction()
    {
        var commands = new FakeDeviceCommands();
        var serial = "AAA";
        var vm = new SingleWindowSlotViewModel(
            1, "phone-a", () => serial, commands)
        {
            AppPackage = "com.example.app"
        };

        // USB에서 무선으로 전환되면 serial이 바뀐다.
        serial = "192.168.0.9:5555";
        await vm.StartCommand.ExecuteAsync(null);

        Assert.Equal(
            new[] { "start-slot:phone-a:192.168.0.9:5555:1:com.example.app" },
            commands.Calls);
    }

    [Fact]
    public void StartCommand_IsDisabledWithoutAnAppPackage()
    {
        var commands = new FakeDeviceCommands();
        var vm = new SingleWindowSlotViewModel(1, "phone-a", () => "AAA", commands);

        // 단일창은 실행할 앱을 지정해야 의미가 있다.
        Assert.False(vm.StartCommand.CanExecute(null));

        vm.AppPackage = "com.example.app";

        Assert.True(vm.StartCommand.CanExecute(null));
    }

    [Fact]
    public async Task StopCommand_PassesTheSlot()
    {
        var commands = new FakeDeviceCommands();
        var vm = new SingleWindowSlotViewModel(3, "phone-a", () => "AAA", commands);

        await vm.StopCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "stop-slot:phone-a:3" }, commands.Calls);
    }

    [Fact]
    public void ApplyRuntime_ReflectsOnlyThisSlot()
    {
        var commands = new FakeDeviceCommands();
        var vm = new SingleWindowSlotViewModel(2, "phone-a", () => "AAA", commands);

        vm.ApplyRuntime(new DeviceRuntimeSessionSnapshot
        {
            Identity = "phone-a",
            SingleWindows = new List<SingleWindowRuntimeSnapshot>
            {
                new SingleWindowRuntimeSnapshot { Slot = 1, IsRunning = true },
                new SingleWindowRuntimeSnapshot { Slot = 2, IsRunning = false }
            }
        });

        Assert.False(vm.IsRunning);

        vm.ApplyRuntime(new DeviceRuntimeSessionSnapshot
        {
            Identity = "phone-a",
            SingleWindows = new List<SingleWindowRuntimeSnapshot>
            {
                new SingleWindowRuntimeSnapshot { Slot = 2, IsRunning = true }
            }
        });

        Assert.True(vm.IsRunning);
    }

    [Fact]
    public void ApplyRuntime_TreatsAMissingSessionAsStopped()
    {
        var commands = new FakeDeviceCommands();
        var vm = new SingleWindowSlotViewModel(1, "phone-a", () => "AAA", commands);

        vm.ApplyRuntime(null);

        Assert.False(vm.IsRunning);
    }

    [Fact]
    public void Constructor_RejectsSlotsOutsideOneToThree()
    {
        var commands = new FakeDeviceCommands();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SingleWindowSlotViewModel(0, "phone-a", () => "AAA", commands));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SingleWindowSlotViewModel(4, "phone-a", () => "AAA", commands));
    }

    // --- 아래는 브리프를 넘어선 추가 테스트다 (task-10 지시사항 3, 4) ---

    [Fact]
    public async Task StartCommand_DoesNotBlockTheCallingThread()
    {
        // IDeviceRuntimeCommands.StartSingleWindow는
        // DeviceRuntimeCoordinator.GetOrCreate를 부르는데, 이 메서드는
        // scrcpy 버전 프로브 동안 최대 ~3초 자물쇠를 쥔다. 슬롯의 시작
        // 명령이 이걸 스레드 풀로 넘기지 않으면 호출자(=UI) 스레드가
        // 그대로 막힌다.
        //
        // 호출 자체를 별도 워커 스레드에서 걸어, 오프로딩이 빠져
        // 동기적으로 막히더라도 이 테스트의 실행 스레드가 아니라 그
        // 워커 스레드가 막히게 한다 — 그래야 "호출이 곧바로
        // 반환됐는가"를 외부 timeout 래퍼 없이 유한 시간 안에 판정할
        // 수 있다. 오프로딩이 없으면 이 테스트는 행이 아니라 아래
        // Assert.True에서 곧바로 실패한다.
        var commands = new FakeDeviceCommands
        {
            StartSingleWindowGate = new ManualResetEventSlim(false)
        };
        var vm = new SingleWindowSlotViewModel(1, "phone-a", () => "AAA", commands)
        {
            AppPackage = "com.example.app"
        };

        var (returnedPromptly, invocation) = InvokeOnBackgroundThread(
            () => vm.StartCommand.ExecuteAsync(null),
            TimeSpan.FromSeconds(2));

        Assert.True(
            returnedPromptly,
            "StartCommand.ExecuteAsync did not return within 2s — " +
            "the calling thread was blocked (offload missing).");

        commands.StartSingleWindowGate.Set();
        await invocation;

        Assert.Equal(
            new[] { "start-slot:phone-a:AAA:1:com.example.app" },
            commands.Calls);
    }

    [Fact]
    public async Task StartCommand_CatchesFailuresAndReportsThemWithoutThrowing()
    {
        // SingleWindowService.Start는 예외를 던질 수 있다. 명령 핸들러가
        // 그걸 그대로 흘리면 창이 죽는다 — DeX 명령과 같은 방식으로
        // 잡아서 문구로만 남겨야 한다.
        var commands = new FakeDeviceCommands
        {
            StartSingleWindowException = new InvalidOperationException("scrcpy missing")
        };
        var vm = new SingleWindowSlotViewModel(1, "phone-a", () => "AAA", commands)
        {
            AppPackage = "com.example.app"
        };

        await vm.StartCommand.ExecuteAsync(null);

        Assert.Contains("scrcpy missing", vm.LastCommandMessage);
        // 실패 후에도 다시 시작을 시도할 수 있어야 한다(멈춰 있지 않음).
        Assert.True(vm.StartCommand.CanExecute(null));
    }

    [Fact]
    public async Task StopCommand_CatchesFailuresAndReportsThemWithoutThrowing()
    {
        var commands = new FakeDeviceCommands
        {
            StopSingleWindowException = new InvalidOperationException("no such slot")
        };
        var vm = new SingleWindowSlotViewModel(1, "phone-a", () => "AAA", commands);

        await vm.StopCommand.ExecuteAsync(null);

        Assert.Contains("no such slot", vm.LastCommandMessage);
    }

    [Fact]
    public async Task StopCommand_DoesNotBlockTheCallingThread()
    {
        // IDeviceRuntimeCommands.StopSingleWindow는 TryGet을 부르는데,
        // TryGet은 GetOrCreate와 같은 자물쇠를 공유한다 — 다른 기기가
        // scrcpy를 프로브하는 동안 몇 초씩 막힐 수 있다. StopAsync도
        // Start와 같은 이유로 스레드 풀에 넘겨야 한다.
        var commands = new FakeDeviceCommands
        {
            StopSingleWindowGate = new ManualResetEventSlim(false)
        };
        var vm = new SingleWindowSlotViewModel(3, "phone-a", () => "AAA", commands);

        var (returnedPromptly, invocation) = InvokeOnBackgroundThread(
            () => vm.StopCommand.ExecuteAsync(null),
            TimeSpan.FromSeconds(2));

        Assert.True(
            returnedPromptly,
            "StopCommand.ExecuteAsync did not return within 2s — " +
            "the calling thread was blocked (offload missing).");

        commands.StopSingleWindowGate.Set();
        await invocation;

        Assert.Equal(new[] { "stop-slot:phone-a:3" }, commands.Calls);
    }

    /// <summary>
    /// 주어진 호출을 별도 워커 스레드에서 실행해, 그 호출 자체가
    /// 곧바로 반환되는지 확인한다. 오프로딩이 빠지면 호출이 그
    /// 워커 스레드에서 동기적으로 막히므로, 여기서 시간 제한을 두어
    /// (외부 timeout 래퍼 없이) 테스트 스스로 유한 시간 안에 실패할
    /// 수 있게 한다 — 테스트 자신의 호출 스레드는 절대 막히지 않는다.
    /// </summary>
    private static (bool ReturnedPromptly, Task Invocation) InvokeOnBackgroundThread(
        Func<Task> invoke, TimeSpan bound)
    {
        Task invocation = null;
        var invoked = new ManualResetEventSlim(false);
        var worker = new Thread(() =>
        {
            invocation = invoke();
            invoked.Set();
        })
        {
            IsBackground = true
        };
        worker.Start();

        var returnedPromptly = invoked.Wait(bound);
        return (returnedPromptly, invocation);
    }
}
