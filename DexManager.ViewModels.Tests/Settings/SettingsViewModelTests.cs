using System.IO;
using Xunit;
using DexManager.Models;
using DexManager.Services;

namespace DexManager.ViewModels.Tests.Settings;

/// <summary>
/// SettingsViewModel의 조립(다섯 페이지), 기기 선택 추적, SaveAll/Cancel
/// 조율, 집계 HasChanges를 검증한다.
/// </summary>
public class SettingsViewModelTests
{
    [Fact]
    public void Constructor_WithNoDeviceSelected_LeavesPerDevicePagesNull()
    {
        // 선택된 기기가 없으면(identity == null) 기기별 페이지는 null이어야
        // 한다 - 전역 페이지 3개는 항상 준비되어 있어야 한다.
        var gateway = new FakeSettingsGateway();
        var deviceSelection = new FakeDeviceSelectionSource();

        var settings = CreateSettings(gateway, deviceSelection);

        Assert.Null(settings.DisplayStream);
        Assert.Null(settings.Slot);
        Assert.NotNull(settings.Paths);
        Assert.NotNull(settings.Appearance);
        Assert.NotNull(settings.Interaction);
    }

    [Fact]
    public void Constructor_WithDeviceSelected_LoadsPerDevicePagesForThatIdentity()
    {
        var gateway = new FakeSettingsGateway();
        var deviceSelection = new FakeDeviceSelectionSource { SelectedIdentity = "device-a" };

        var settings = CreateSettings(gateway, deviceSelection);

        Assert.NotNull(settings.DisplayStream);
        Assert.NotNull(settings.Slot);
    }

    [Fact]
    public void DeviceSelectionChanged_ReloadsPerDevicePagesWithTheNewProfile()
    {
        // device-a의 프로필 너비를 1111로 바꿔둔다. device-b는 기본값을
        // 유지한다. 선택을 device-a -> device-b로 바꾸면 DisplayStream이
        // device-b의 (기본) 값을 보여줘야 한다 - device-a의 값이 새 페이지에
        // 새어 들어오면 이 검증이 실패한다.
        var gateway = new FakeSettingsGateway();
        gateway.Update(s => s.GetOrCreateDeviceRunSettings("device-a").VirtualDisplay.Width = 1111);
        var defaultWidth = AppSettings.CreateDefault().VirtualDisplay.Width;

        var deviceSelection = new FakeDeviceSelectionSource { SelectedIdentity = "device-a" };
        var settings = CreateSettings(gateway, deviceSelection);

        Assert.Equal(1111, settings.DisplayStream.Values.Width);
        var deviceAPage = settings.DisplayStream;

        deviceSelection.SelectedIdentity = "device-b";

        Assert.NotSame(deviceAPage, settings.DisplayStream);
        Assert.Equal(defaultWidth, settings.DisplayStream.Values.Width);
    }

    [Fact]
    public void DeviceSelectionChanged_ToNoDevice_ClearsPerDevicePages()
    {
        var gateway = new FakeSettingsGateway();
        var deviceSelection = new FakeDeviceSelectionSource { SelectedIdentity = "device-a" };
        var settings = CreateSettings(gateway, deviceSelection);

        Assert.NotNull(settings.DisplayStream);

        deviceSelection.SelectedIdentity = null;

        Assert.Null(settings.DisplayStream);
        Assert.Null(settings.Slot);
    }

    [Fact]
    public void Cancel_DiscardsUnsavedEditsWithoutWritingTheGateway()
    {
        var gateway = new FakeSettingsGateway();
        var deviceSelection = new FakeDeviceSelectionSource();
        var settings = CreateSettings(gateway, deviceSelection);

        var originalScrcpyPath = gateway.Current.Paths.ScrcpyPath;
        settings.Paths.ScrcpyPath = "/edited/scrcpy";
        Assert.True(settings.Paths.HasChanges);

        var updateCallsBeforeCancel = gateway.UpdateCallCount;

        settings.CancelCommand.Execute(null);

        Assert.Equal(updateCallsBeforeCancel, gateway.UpdateCallCount);
        Assert.Equal(originalScrcpyPath, gateway.Current.Paths.ScrcpyPath);
        // Cancel이 재로드한 새 Paths 인스턴스는 게이트웨이의 원본값을 반영한다.
        Assert.Equal(originalScrcpyPath, settings.Paths.ScrcpyPath);
        Assert.False(settings.HasChanges);
    }

    [Fact]
    public void SaveAll_FlowsEachPageOnceAndClearsAggregateHasChanges()
    {
        var gateway = new FakeSettingsGateway();
        var deviceSelection = new FakeDeviceSelectionSource { SelectedIdentity = "device-a" };
        var settings = CreateSettings(gateway, deviceSelection);

        settings.Paths.ScrcpyPath = "/new/scrcpy";
        settings.DisplayStream.Values.Width = 1280;
        Assert.True(settings.HasChanges);

        var updateCallsBeforeSave = gateway.UpdateCallCount;

        settings.SaveAllCommand.Execute(null);

        // 다섯 페이지 각각 한 번씩만 Update를 호출한다.
        Assert.Equal(updateCallsBeforeSave + 5, gateway.UpdateCallCount);
        Assert.Equal("/new/scrcpy", gateway.Current.Paths.ScrcpyPath);
        Assert.Equal(1280, gateway.GetRunProfile("device-a").VirtualDisplay.Width);
        Assert.False(settings.HasChanges);
    }

    [Fact]
    public void SaveAll_DoesNotForceSaveAnInvalidPage()
    {
        // Interaction 페이지를 캡처/종료 단축키가 같은 잘못된 상태로
        // 만든다 - IsValid가 false가 되어 SaveCommand.CanExecute가 false다.
        // SaveAll은 이 페이지를 건너뛰어야 한다.
        var gateway = new FakeSettingsGateway();
        var originalCaptureHotkey = gateway.Current.KeyMappings.CaptureHotkey;
        var deviceSelection = new FakeDeviceSelectionSource();
        var settings = CreateSettings(gateway, deviceSelection);

        settings.Interaction.CaptureHotkey = "F1";
        settings.Interaction.ExitHotkey = "F1";
        Assert.False(settings.Interaction.IsValid);
        Assert.False(settings.Interaction.SaveCommand.CanExecute(null));

        settings.Paths.ScrcpyPath = "/valid/edit";

        settings.SaveAllCommand.Execute(null);

        // 유효한 Paths는 저장됐지만, 유효하지 않은 Interaction의 편집은
        // 게이트웨이에 반영되지 않는다.
        Assert.Equal("/valid/edit", gateway.Current.Paths.ScrcpyPath);
        Assert.Equal(originalCaptureHotkey, gateway.Current.KeyMappings.CaptureHotkey);

        // Interaction은 여전히 저장되지 않은 편집을 갖고 있으므로 집계도 true.
        Assert.True(settings.Interaction.HasChanges);
        Assert.True(settings.HasChanges);
    }

    [Fact]
    public void HasChanges_AggregatesAcrossAllFivePagesAndRecomputesOnPageChange()
    {
        var gateway = new FakeSettingsGateway();
        var deviceSelection = new FakeDeviceSelectionSource { SelectedIdentity = "device-a" };
        var settings = CreateSettings(gateway, deviceSelection);

        Assert.False(settings.HasChanges);

        settings.Slot.Values.Slots[0].Width = 999;
        Assert.True(settings.Slot.HasChanges);
        Assert.True(settings.HasChanges);

        settings.Slot.SaveCommand.Execute(null);
        Assert.False(settings.Slot.HasChanges);
        Assert.False(settings.HasChanges);

        settings.Appearance.SelectedTheme = settings.Appearance.SelectedTheme == AppTheme.Dark
            ? AppTheme.Light
            : AppTheme.Dark;
        Assert.True(settings.HasChanges);

        settings.Appearance.SaveCommand.Execute(null);
        Assert.False(settings.Appearance.HasChanges);
        Assert.False(settings.HasChanges);

        // Paths in isolation: nothing else is dirty here (Slot/Appearance were just
        // saved above). If Paths' PropertyChanged subscription or its "Paths.HasChanges
        // ||" term dropped out of RecomputeHasChanges, this would be the only assertion
        // in the suite to notice - every other test that touches Paths either checks
        // only settings.Paths.HasChanges directly, or co-dirties another page whose
        // notification would mask a Paths-wiring regression.
        settings.Paths.ScrcpyPath = "/isolated/paths/only";
        Assert.True(settings.Paths.HasChanges);
        Assert.True(settings.HasChanges);
    }

    // --- 창 닫기 (UI-1: Cancel/Save 둘 다 창을 닫아야 한다) ---

    [Fact]
    public void SaveAll_RaisesCloseRequestedExactlyOnce()
    {
        var gateway = new FakeSettingsGateway();
        var deviceSelection = new FakeDeviceSelectionSource();
        var settings = CreateSettings(gateway, deviceSelection);

        var raiseCount = 0;
        settings.CloseRequested += (_, _) => raiseCount++;

        settings.SaveAllCommand.Execute(null);

        Assert.Equal(1, raiseCount);
    }

    [Fact]
    public void Cancel_RaisesCloseRequestedExactlyOnce()
    {
        var gateway = new FakeSettingsGateway();
        var deviceSelection = new FakeDeviceSelectionSource();
        var settings = CreateSettings(gateway, deviceSelection);

        var raiseCount = 0;
        settings.CloseRequested += (_, _) => raiseCount++;

        settings.CancelCommand.Execute(null);

        Assert.Equal(1, raiseCount);
    }

    [Fact]
    public void SaveAll_PersistsEditsBeforeRaisingCloseRequested()
    {
        // CloseRequested가 저장을 가로막거나 순서를 흐트러뜨리지 않는지
        // 확인한다 - 이벤트 핸들러 안에서 게이트웨이를 들여다봐도 이미
        // 저장이 끝나 있어야 한다.
        var gateway = new FakeSettingsGateway();
        var deviceSelection = new FakeDeviceSelectionSource();
        var settings = CreateSettings(gateway, deviceSelection);

        settings.Paths.ScrcpyPath = "/new/scrcpy";

        string scrcpyPathWhenClosed = null;
        settings.CloseRequested += (_, _) => scrcpyPathWhenClosed = gateway.Current.Paths.ScrcpyPath;

        settings.SaveAllCommand.Execute(null);

        Assert.Equal("/new/scrcpy", scrcpyPathWhenClosed);
        Assert.Equal("/new/scrcpy", gateway.Current.Paths.ScrcpyPath);
    }

    [Fact]
    public void Cancel_DiscardsEditsBeforeRaisingCloseRequested()
    {
        var gateway = new FakeSettingsGateway();
        var deviceSelection = new FakeDeviceSelectionSource();
        var settings = CreateSettings(gateway, deviceSelection);

        var originalScrcpyPath = gateway.Current.Paths.ScrcpyPath;
        settings.Paths.ScrcpyPath = "/edited/scrcpy";

        string scrcpyPathWhenClosed = null;
        settings.CloseRequested += (_, _) => scrcpyPathWhenClosed = settings.Paths.ScrcpyPath;

        settings.CancelCommand.Execute(null);

        Assert.Equal(originalScrcpyPath, scrcpyPathWhenClosed);
        Assert.False(settings.HasChanges);
    }

    // --- 실행 중 기기 경고 (task-5) ---

    [Fact]
    public void Constructor_TargetDeviceHasDexRunning_IsTargetDexRunningIsTrue()
    {
        var sessions = CreateRuntimeRegistryWithDexRunning("device-a", "USB-A");
        var gateway = new FakeSettingsGateway();
        var deviceSelection = new FakeDeviceSelectionSource { SelectedIdentity = "device-a" };

        var settings = CreateSettings(gateway, deviceSelection, sessions);

        Assert.True(settings.IsTargetDexRunning);
    }

    [Fact]
    public void Constructor_ADifferentDeviceHasDexRunning_IsTargetDexRunningIsFalse()
    {
        // device-b의 DeX가 도는 동안, 대상은 device-a다 - 플래그는 대상
        // identity만 봐야 하며 레지스트리 전역 상태에 휘둘리면 안 된다.
        var sessions = CreateRuntimeRegistryWithDexRunning("device-b", "USB-B");
        var gateway = new FakeSettingsGateway();
        var deviceSelection = new FakeDeviceSelectionSource { SelectedIdentity = "device-a" };

        var settings = CreateSettings(gateway, deviceSelection, sessions);

        Assert.False(settings.IsTargetDexRunning);
    }

    [Fact]
    public void RegistryChangedWhileSettingsOpen_FlipsTheFlagLiveThroughTheDispatcher()
    {
        var sessions = CreateRuntimeRegistry("device-a", "USB-A");
        var gateway = new FakeSettingsGateway();
        var deviceSelection = new FakeDeviceSelectionSource { SelectedIdentity = "device-a" };
        var dispatcher = new QueueingUiDispatcher();

        var settings = CreateSettings(gateway, deviceSelection, sessions, dispatcher);
        Assert.False(settings.IsTargetDexRunning);

        sessions.SetDexSession("USB-A", new ManagedDisplaySession
        {
            Serial = "USB-A",
            DeviceIdentity = "device-a"
        });

        // Post는 비동기다 - 큐를 비우기 전까지는 아직 반영되지 않는다.
        Assert.False(settings.IsTargetDexRunning);
        dispatcher.Drain();
        Assert.True(settings.IsTargetDexRunning);

        sessions.SetDexSession("USB-A", null);
        dispatcher.Drain();
        Assert.False(settings.IsTargetDexRunning);
    }

    [Fact]
    public void SelectedIdentityChangedToADeviceWithDexRunning_FlipsTheFlagTrue()
    {
        var sessions = CreateRuntimeRegistryWithDexRunning("device-b", "USB-B");
        // device-a도 레지스트리에 등록해 둔다 - identity가 존재해야 전환이
        // "알 수 없는 기기"가 아니라 실제 device-a 세션을 찾는 경로를 탄다.
        var physical = new PhysicalDeviceRegistry();
        physical.Reconcile(new[] { Discovered("device-a", "Galaxy A", "USB-A") });
        sessions.Reconcile(physical.Current);

        var gateway = new FakeSettingsGateway();
        var deviceSelection = new FakeDeviceSelectionSource { SelectedIdentity = "device-a" };
        var settings = CreateSettings(gateway, deviceSelection, sessions);
        Assert.False(settings.IsTargetDexRunning);

        deviceSelection.SelectedIdentity = "device-b";

        Assert.True(settings.IsTargetDexRunning);
    }

    [Fact]
    public void SelectedIdentityChangedToADeviceWithoutDexRunning_FlipsTheFlagFalse()
    {
        var sessions = CreateRuntimeRegistryWithDexRunning("device-a", "USB-A");
        var physical = new PhysicalDeviceRegistry();
        physical.Reconcile(new[] { Discovered("device-b", "Galaxy B", "USB-B") });
        sessions.Reconcile(physical.Current);

        var gateway = new FakeSettingsGateway();
        var deviceSelection = new FakeDeviceSelectionSource { SelectedIdentity = "device-a" };
        var settings = CreateSettings(gateway, deviceSelection, sessions);
        Assert.True(settings.IsTargetDexRunning);

        deviceSelection.SelectedIdentity = "device-b";

        Assert.False(settings.IsTargetDexRunning);
    }

    [Fact]
    public void AfterDispose_ASubsequentRegistryChanged_DoesNotTouchTheFlag()
    {
        // Post는 비동기다: 이벤트가 디스패처 큐까지 들어간 뒤에 Dispose가
        // 끼어드는 경합을 재현해야 한다 - Dispose를 먼저 부르면 구독이
        // 곧장 해제되어 OnSessionsChanged 자체가 걸리지 않으므로,
        // ApplyRuntime의 실행 시점 _disposed 재확인은 전혀 검증되지 않는다.
        // 그래서 여기서는 DeviceViewModelTests.DisposedRowStopsFollowingTheRegistry와
        // 같은 순서로 만든다: 먼저 Changed를 큐에 넣고(SetDexSession),
        // 그 다음 Dispose, 마지막에 Drain해서 이미 큐에 들어간 클로저가
        // 실행 시점에 스스로를 막는지 본다.
        var sessions = CreateRuntimeRegistry("device-a", "USB-A");
        var gateway = new FakeSettingsGateway();
        var deviceSelection = new FakeDeviceSelectionSource { SelectedIdentity = "device-a" };
        var dispatcher = new QueueingUiDispatcher();
        var settings = CreateSettings(gateway, deviceSelection, sessions, dispatcher);

        sessions.SetDexSession("USB-A", new ManagedDisplaySession
        {
            Serial = "USB-A",
            DeviceIdentity = "device-a"
        });
        Assert.Equal(1, dispatcher.PendingCount);

        settings.Dispose();
        dispatcher.Drain();

        Assert.False(settings.IsTargetDexRunning);
    }

    // --- F-1: 같은 폰의 행 인스턴스 교체는 편집을 버리면 안 된다 ---

    [Fact]
    public void SelectedIdentityNotificationForTheSameIdentity_KeepsThePagesAndTheUnsavedEdits()
    {
        // DeviceListViewModel.Apply는 스냅샷에 없는 행을 Dispose+제거하고,
        // 같은 폰이 돌아오면 "새 DeviceViewModel 인스턴스"를 만들어 다시
        // 선택한다. SelectedIdentity 통지는 그 참조 변경에서 올라오므로
        // identity 문자열은 그대로인데도 통지가 뜬다 - USB 흔들림이나
        // 전송 방식 전환 한 번에 사용자의 미저장 편집이 조용히 사라졌다.
        var gateway = new FakeSettingsGateway();
        var deviceSelection = new FakeDeviceSelectionSource { SelectedIdentity = "device-a" };
        var settings = CreateSettings(gateway, deviceSelection);

        var displayStreamBefore = settings.DisplayStream;
        var slotBefore = settings.Slot;
        settings.DisplayStream.Values.Width = 1920;
        settings.Slot.Values.Slots[0].Width = 1600;
        Assert.True(settings.HasChanges);

        deviceSelection.RaiseSelectedIdentityChangedWithoutChangingValue();

        Assert.Same(displayStreamBefore, settings.DisplayStream);
        Assert.Same(slotBefore, settings.Slot);
        Assert.Equal(1920, settings.DisplayStream.Values.Width);
        Assert.Equal(1600, settings.Slot.Values.Slots[0].Width);
        Assert.True(settings.HasChanges);
    }

    [Fact]
    public void SelectedIdentityNotificationForTheSameIdentity_StillRecomputesTheRunningFlag()
    {
        // identity 가드는 기기별 페이지 재생성만 막는다 - 런타임 플래그
        // 재계산까지 막아서는 안 된다.
        // 큐잉 디스패처를 쓰는 이유: 즉시 실행 디스패처라면 SetDexSession의
        // Changed가 그 자리에서 ApplyRuntime을 돌려 플래그를 이미 뒤집어
        // 놓으므로, 통지 핸들러가 재계산을 하든 말든 검증이 통과해버린다.
        // 큐에 넣어두고 비우지 않으면 "통지 핸들러가 스스로 다시 계산하는가"
        // 만 남는다.
        var sessions = CreateRuntimeRegistry("device-a", "USB-A");
        var gateway = new FakeSettingsGateway();
        var deviceSelection = new FakeDeviceSelectionSource { SelectedIdentity = "device-a" };
        var dispatcher = new QueueingUiDispatcher();
        var settings = CreateSettings(gateway, deviceSelection, sessions, dispatcher);
        Assert.False(settings.IsTargetDexRunning);

        sessions.SetDexSession("USB-A", new ManagedDisplaySession
        {
            Serial = "USB-A",
            DeviceIdentity = "device-a"
        });
        Assert.Equal(1, dispatcher.PendingCount);
        Assert.False(settings.IsTargetDexRunning);

        deviceSelection.RaiseSelectedIdentityChangedWithoutChangingValue();

        Assert.True(settings.IsTargetDexRunning);
    }

    [Fact]
    public void Cancel_StillRebuildsThePerDevicePagesEvenThoughTheIdentityDidNotChange()
    {
        // F-1의 identity 가드가 Cancel의 "편집 버리기"까지 무력화하면
        // 안 된다 - Cancel은 같은 identity로도 강제 재로드해야 한다.
        var gateway = new FakeSettingsGateway();
        var deviceSelection = new FakeDeviceSelectionSource { SelectedIdentity = "device-a" };
        var settings = CreateSettings(gateway, deviceSelection);

        var originalWidth = settings.DisplayStream.Values.Width;
        var pageBefore = settings.DisplayStream;
        settings.DisplayStream.Values.Width = 1920;
        Assert.True(settings.HasChanges);

        settings.CancelCommand.Execute(null);

        Assert.NotSame(pageBefore, settings.DisplayStream);
        Assert.Equal(originalWidth, settings.DisplayStream.Values.Width);
        Assert.False(settings.HasChanges);
    }

    // --- F-2: SaveAll은 예외를 커맨드 밖으로 흘리지 않는다 ---

    [Fact]
    public void SaveAll_WhenAPageSaveThrows_DoesNotEscapeTheCommandAndKeepsTheWindowOpen()
    {
        // SettingsService.SaveCore는 디스크 가득참·저장 잠금 타임아웃·
        // 상위 버전 설정 파일에서 예외를 던진다. 그 예외가 RelayCommand를
        // 뚫고 나가면 Avalonia UI 스레드에 처리기가 없어 앱이 죽는다.
        var gateway = new FakeSettingsGateway
        {
            UpdateExceptionToThrow = new IOException("There is not enough space on the disk."),
            UpdateExceptionOnCall = 2
        };
        var deviceSelection = new FakeDeviceSelectionSource();
        var settings = CreateSettings(gateway, deviceSelection);

        settings.Paths.ScrcpyPath = "/new/scrcpy";

        var closeRequestedCount = 0;
        settings.CloseRequested += (_, _) => closeRequestedCount++;

        var thrown = Record.Exception(() => settings.SaveAllCommand.Execute(null));

        Assert.Null(thrown);
        // 저장이 실패했으니 창은 열려 있어야 한다.
        Assert.Equal(0, closeRequestedCount);
        Assert.False(string.IsNullOrEmpty(settings.SaveErrorMessage));
        Assert.Contains("There is not enough space on the disk.", settings.SaveErrorMessage);
    }

    [Fact]
    public void SaveAll_WhenEverySaveSucceeds_ClearsAPreviousErrorMessageAndCloses()
    {
        var gateway = new FakeSettingsGateway
        {
            UpdateExceptionToThrow = new IOException("boom"),
            UpdateExceptionOnCall = 1
        };
        var deviceSelection = new FakeDeviceSelectionSource();
        var settings = CreateSettings(gateway, deviceSelection);

        settings.Paths.ScrcpyPath = "/new/scrcpy";
        settings.SaveAllCommand.Execute(null);
        Assert.False(string.IsNullOrEmpty(settings.SaveErrorMessage));

        // 이제 게이트웨이가 정상이다 - 다시 저장하면 메시지가 지워지고
        // 창이 닫혀야 한다.
        gateway.UpdateExceptionToThrow = null;

        var closeRequestedCount = 0;
        settings.CloseRequested += (_, _) => closeRequestedCount++;

        settings.SaveAllCommand.Execute(null);

        Assert.Equal(string.Empty, settings.SaveErrorMessage);
        Assert.Equal(1, closeRequestedCount);
        Assert.Equal("/new/scrcpy", gateway.Current.Paths.ScrcpyPath);
    }

    // --- F-5: 무효 페이지 위에서 Save가 닫히지 않게 하는 집계 유효성 ---

    [Fact]
    public void AreAllPagesValid_GoesFalseWhileAPageIsInvalidAndBackTrueOnceFixed()
    {
        var gateway = new FakeSettingsGateway();
        var deviceSelection = new FakeDeviceSelectionSource { SelectedIdentity = "device-a" };
        var settings = CreateSettings(gateway, deviceSelection);

        Assert.True(settings.AreAllPagesValid);

        // 참고: 리뷰 보고서의 예시("8"은 M/K 접미사가 없어 무효)는 실제
        // 규칙과 다르다 - 두 페이지의 패턴 ^\d+[MK]?$ 모두 접미사를
        // 선택으로 두므로 "8"은 유효하다. 무효 사례로는 접미사가 아닌
        // 꼬리표가 붙은 값을 쓴다.
        settings.Slot.Values.Slots[0].BitRate = "8Mbps";
        Assert.False(settings.Slot.IsValid);
        Assert.True(settings.HasChanges);
        // HasChanges만 보고 Save를 열어두면 이 슬롯 편집이 조용히 버려진 채
        // 창이 닫힌다 - Save 게이트는 유효성도 함께 봐야 한다.
        Assert.False(settings.AreAllPagesValid);

        settings.Slot.Values.Slots[0].BitRate = "8M";
        Assert.True(settings.Slot.IsValid);
        Assert.True(settings.AreAllPagesValid);
    }

    [Fact]
    public void AreAllPagesValid_TracksTheDisplayStreamAndInteractionPagesToo()
    {
        var gateway = new FakeSettingsGateway();
        var deviceSelection = new FakeDeviceSelectionSource { SelectedIdentity = "device-a" };
        var settings = CreateSettings(gateway, deviceSelection);

        settings.DisplayStream.Values.Width = 0;
        Assert.False(settings.DisplayStream.IsValid);
        Assert.False(settings.AreAllPagesValid);

        settings.DisplayStream.Values.Width = 1920;
        Assert.True(settings.AreAllPagesValid);

        settings.Interaction.ExitHotkey = settings.Interaction.CaptureHotkey;
        Assert.False(settings.Interaction.IsValid);
        Assert.False(settings.AreAllPagesValid);
    }

    [Fact]
    public void AreAllPagesValid_RecomputesWhenThePerDevicePagesAreReplaced()
    {
        // 무효한 기기별 페이지를 놔둔 채 다른 기기로 옮기면, 새 페이지는
        // 게이트웨이에서 새로 읽어 유효하다 - 집계도 따라와야 한다.
        var gateway = new FakeSettingsGateway();
        var deviceSelection = new FakeDeviceSelectionSource { SelectedIdentity = "device-a" };
        var settings = CreateSettings(gateway, deviceSelection);

        settings.DisplayStream.Values.Height = -1;
        Assert.False(settings.AreAllPagesValid);

        deviceSelection.SelectedIdentity = "device-b";

        Assert.True(settings.AreAllPagesValid);
    }

    private static SettingsViewModel CreateSettings(
        ISettingsGateway gateway,
        IDeviceSelectionSource deviceSelection,
        DeviceRuntimeSessionRegistry sessions = null,
        IUiDispatcher dispatcher = null)
    {
        return new SettingsViewModel(
            gateway,
            deviceSelection,
            sessions ?? new DeviceRuntimeSessionRegistry(),
            dispatcher ?? new ImmediateUiDispatcher());
    }

    private static DeviceRuntimeSessionRegistry CreateRuntimeRegistry(
        string identity,
        string serial)
    {
        var physical = new PhysicalDeviceRegistry();
        physical.Reconcile(new[] { Discovered(identity, "Galaxy", serial) });
        var sessions = new DeviceRuntimeSessionRegistry();
        sessions.Reconcile(physical.Current);
        return sessions;
    }

    private static DeviceRuntimeSessionRegistry CreateRuntimeRegistryWithDexRunning(
        string identity,
        string serial)
    {
        var sessions = CreateRuntimeRegistry(identity, serial);
        sessions.SetDexSession(serial, new ManagedDisplaySession
        {
            Serial = serial,
            DeviceIdentity = identity
        });
        return sessions;
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
}
