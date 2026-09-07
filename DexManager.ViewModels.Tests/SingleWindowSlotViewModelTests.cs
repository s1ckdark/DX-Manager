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
    public void StopCommand_PassesTheSlot()
    {
        var commands = new FakeDeviceCommands();
        var vm = new SingleWindowSlotViewModel(3, "phone-a", () => "AAA", commands);

        vm.StopCommand.Execute(null);

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
        // 그대로 막힌다. 게이트를 걸어 시작 호출이 스레드 풀에서
        // 대기하는 동안, 명령을 건 호출은 즉시 반환되는지 확인한다.
        var commands = new FakeDeviceCommands
        {
            StartSingleWindowGate = new ManualResetEventSlim(false)
        };
        var vm = new SingleWindowSlotViewModel(1, "phone-a", () => "AAA", commands)
        {
            AppPackage = "com.example.app"
        };

        var task = vm.StartCommand.ExecuteAsync(null);

        // 오프로딩되지 않았다면 ExecuteAsync 자체가 게이트에서 막혀
        // 이 줄에 도달하지 못했을 것이다.
        Assert.False(task.IsCompleted);

        commands.StartSingleWindowGate.Set();
        await task;

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
    public void StopCommand_CatchesFailuresAndReportsThemWithoutThrowing()
    {
        var commands = new FakeDeviceCommands
        {
            StopSingleWindowException = new InvalidOperationException("no such slot")
        };
        var vm = new SingleWindowSlotViewModel(1, "phone-a", () => "AAA", commands);

        vm.StopCommand.Execute(null);

        Assert.Contains("no such slot", vm.LastCommandMessage);
    }
}
