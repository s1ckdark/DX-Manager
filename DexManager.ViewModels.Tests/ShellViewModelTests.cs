using System.Reflection;
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
    public void OpenSettingsCommand_RaisesSettingsRequestedWithAReadySettingsViewModel()
    {
        using var temp = new TempHostRoot();
        var host = temp.CreateHost();
        using var shell = new ShellViewModel(host, new ImmediateUiDispatcher());

        host.DeviceRegistry.Reconcile(new[]
        {
            Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb)
        });
        shell.Devices.SelectedDevice =
            shell.Devices.Devices.First(d => d.Identity == "phone-a");

        SettingsViewModel raised = null;
        shell.SettingsRequested += (_, settings) => raised = settings;

        shell.OpenSettingsCommand.Execute(null);

        Assert.NotNull(raised);
        Assert.NotNull(raised.DisplayStream);
        Assert.NotNull(raised.Slot);
    }

    [Fact]
    public void OpenSettingsCommand_WithNoDeviceSelected_StillProducesGlobalPagesOnly()
    {
        using var temp = new TempHostRoot();
        var host = temp.CreateHost();
        using var shell = new ShellViewModel(host, new ImmediateUiDispatcher());

        SettingsViewModel raised = null;
        shell.SettingsRequested += (_, settings) => raised = settings;

        shell.OpenSettingsCommand.Execute(null);

        Assert.NotNull(raised);
        Assert.NotNull(raised.Paths);
        Assert.Null(raised.DisplayStream);
        Assert.Null(raised.Slot);
    }

    [Fact]
    public void OpenSettingsCommand_WithASubscriber_DeliversAnUndisposedSettingsViewModel()
    {
        using var temp = new TempHostRoot();
        var host = temp.CreateHost();
        using var shell = new ShellViewModel(host, new ImmediateUiDispatcher());

        SettingsViewModel raised = null;
        shell.SettingsRequested += (_, settings) => raised = settings;

        shell.OpenSettingsCommand.Execute(null);

        Assert.NotNull(raised);
        Assert.False(raised.IsDisposed);
    }

    [Fact]
    public void OpenSettingsCommand_WithNoSubscriber_DisposesTheUnconsumedSettingsViewModel()
    {
        using var temp = new TempHostRoot();
        var host = temp.CreateHost();
        using var shell = new ShellViewModel(host, new ImmediateUiDispatcher());

        // 일부러 SettingsRequested를 구독하지 않는다 - Task 12(App)가
        // 아직 붙지 않았거나 이미 떨어져 나간 상황을 흉내낸다. 이 경로에서
        // 만들어진 SettingsViewModel이 Dispose되지 않으면, 그 생성자가
        // 구독한 host.RuntimeSessions.Changed에 핸들러가 그대로 남는다 -
        // SettingsViewModel은 이 구독을 Dispose에서만 해제하므로, 구독자
        // 수가 호출 전후로 그대로인지를 보면 Dispose가 실제로 불렸는지
        // 관측할 수 있다.
        var before = RuntimeSessionsChangedSubscriberCount(host.RuntimeSessions);

        shell.OpenSettingsCommand.Execute(null);

        var after = RuntimeSessionsChangedSubscriberCount(host.RuntimeSessions);
        Assert.Equal(before, after);
    }

    private static int RuntimeSessionsChangedSubscriberCount(
        DeviceRuntimeSessionRegistry registry)
    {
        var field = typeof(DeviceRuntimeSessionRegistry).GetField(
            "Changed",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var handler = field.GetValue(registry) as Delegate;
        return handler?.GetInvocationList().Length ?? 0;
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
