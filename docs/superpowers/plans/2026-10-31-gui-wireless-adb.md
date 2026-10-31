# GUI 무선 ADB 연결 탭 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Avalonia 설정 창에 기기별 "Connection" 탭을 추가해 TUI처럼 선택한 휴대폰을 USB→무선으로 전환하고, IP:포트로 직접 연결하고, 무선을 해제하며 현재 transport 상태를 보여 준다.

**Architecture:** `DexManager.ViewModels`에 `IWirelessConnectionGateway` 경계를 두고 실제 구현이 `ApplicationHost.WirelessAdb`의 기기별 API(`EnableFromUsbForDevice`/`ConnectForDevice`/`DisconnectForDevice`)와 `ApplicationHost.DeviceRegistry`를 `Task.Run`으로 감싼다. 새 `ConnectionSettingsViewModel`은 다른 기기별 페이지처럼 `SettingsViewModel.LoadDevicePages`가 identity별로 만들고 폐기한다. 이 페이지는 Save/Cancel에 참여하지 않는다. 연결 동작은 즉시 실행되고 `WirelessAdbService`가 성공 시 프로필을 스스로 저장하기 때문이다.

**Tech Stack:** .NET 8, Avalonia 11, CommunityToolkit.Mvvm 8.4.2, xUnit 2.5.3. 솔루션 `DexManager.Mac.sln`.

**Spec:** 별도 스펙 없음(사용자가 질문 없이 진행 요청). 설계 결정은 아래 "Design decisions"에 있다.

## Design decisions

- 기기별 API만 쓴다. TUI `InteractiveHost.cs:537`의 `EnableFromUsb(device.Identity, 5555)`는 identity를 호스트 자리에 넣는 버그라 복사하지 않는다(TODO에 기록).
- USB serial은 `PhysicalDeviceInfo.SelectAuthorizedTransport(DeviceTransportKind.Usb, null)?.Serial`에서 얻는다. 설정 창은 identity만 알므로 게이트웨이가 `FindDevice(identity)`로 레지스트리 스냅샷을 돌려준다.
- 페이지에는 `HasChanges`/`SaveCommand`가 없다. 대신 `IsBusy` 중에는 창의 Save 버튼을 비활성화한다(`WirelessAdbService`의 `SettingsService.UpdateAndSave`와 `ApplicationHost.UpdateSettings`가 서로 다른 잠금으로 같은 `AppSettings`를 쓰므로 동시 저장을 피한다).
- Android 11+ 6자리 페어링은 범위 밖(Core의 `Pair`는 기기 프로필을 건드리지 않음).
- 호스트 입력이 비어 있으면 `EnableFromUsbForDevice`가 `ip route`로 Wi-Fi IP를 감지한다. 직접 연결(`ConnectForDevice`)은 호스트가 필수다.

## Global Constraints

- 뷰모델 프로젝트는 Avalonia를 참조하지 않는다. UI 문자열은 `LocalizationService` 키로 두고 XAML은 `SettingsWindowStrings`의 x:Static으로 읽는다.
- 모든 기기 명령은 명시적 identity/serial로 실행한다. 전역 `WirelessAdbService.Connect/EnableFromUsb/Disconnect`는 쓰지 않는다.
- Core 호출은 모두 블로킹이므로 UI 스레드에서 직접 부르지 않는다. 뷰모델의 `await`는 UI 컨텍스트로 돌아오고, 다른 스레드 이벤트는 `IUiDispatcher.Post`로 넘기며 `_disposed`를 다시 확인한다.
- 새 지역화 키는 `Strings.resx`와 `Strings.ko.resx` 양쪽에 넣는다.
- 빌드는 `export PATH="$PATH:$HOME/.dotnet"` 뒤 `dotnet build DexManager.Mac.sln -c Release --warnaserror`로 경고 0을 유지한다.
- 이 개발 환경은 GUI를 띄우지 못한다(`KNOWN_ISSUES.md` RenderTimer -6661). 실기 확인 항목은 미확인으로 기록한다.

## Review Focus

1. 동작 중 기기가 사라짐(USB 분리) — 명령은 실패 메시지를 보여 주고 `IsBusy`가 풀려야 하며, 창이 닫힌 뒤 도착한 결과로 예외가 나면 안 된다. → Task 2 `Dispose_DuringPendingOperation_IgnoresLateResult`.
2. 포트 입력 범위 밖(0, 70000) — `BuildEndpoint`가 던지므로 명령을 비활성화하고 상태에 안내한다. → Task 2 `ConnectCommand_WhenPortOutOfRange_CannotExecute`.
3. 설정 창이 열린 채 기기 선택이 바뀜 — 이전 identity의 페이지가 폐기되고 진행 중 결과가 새 페이지에 섞이지 않는다. → Task 3 `SelectionChange_ReplacesConnectionPageAndDisposesOld`.
4. 저장된 프로필이 없는 새 기기 — 기본값 포트 5555, 자동 재연결 켬, 모드 USB로 표시된다. → Task 2 `Constructor_WithoutSavedProfile_UsesDefaults`.
5. 무선 동작 도중 Save 클릭 — Save 버튼이 비활성화돼야 한다. → Task 3 `IsWirelessIdle_FalseWhileConnectionBusy`.

---

### Task 1: 게이트웨이 경계와 실제 구현, 테스트용 가짜

**Files:**
- Create: `DexManager.ViewModels/IWirelessConnectionGateway.cs`
- Create: `DexManager.ViewModels/WirelessConnectionGateway.cs`
- Create: `DexManager.ViewModels.Tests/FakeWirelessConnectionGateway.cs`
- Test: `DexManager.ViewModels.Tests/WirelessConnectionGatewayTests.cs`

**Interfaces:**
- Consumes: `ApplicationHost.WirelessAdb` (`WirelessAdbService`), `ApplicationHost.DeviceRegistry` (`PhysicalDeviceRegistry.Current`, `SnapshotChanged`), `ApplicationHost.Settings.FindDeviceWirelessConnection(string)`, `WirelessConnectionResult { bool Success; string Endpoint; string Message }`, `DeviceWirelessConnectionProfile { DeviceIdentity, Mode, WirelessHost, WirelessPort, AutoReconnect }`.
- Produces:
  ```csharp
  namespace DexManager.ViewModels;
  public interface IWirelessConnectionGateway
  {
      /// 레지스트리 스냅샷이 바뀔 때 임의 스레드에서 발생한다.
      event EventHandler DevicesChanged;
      /// 현재 스냅샷의 기기 복사본. 없으면 null.
      PhysicalDeviceInfo FindDevice(string identity);
      /// 저장된 프로필의 복사본. 저장된 것이 없으면 null (만들지 않는다).
      DeviceWirelessConnectionProfile GetProfile(string identity);
      Task<WirelessConnectionResult> EnableFromUsbAsync(string identity, string usbSerial, string host, int port, bool autoReconnect, CancellationToken cancellationToken);
      Task<WirelessConnectionResult> ConnectAsync(string identity, string host, int port, bool autoReconnect, CancellationToken cancellationToken);
      Task<WirelessConnectionResult> DisconnectAsync(string identity, CancellationToken cancellationToken);
  }
  public sealed class WirelessConnectionGateway : IWirelessConnectionGateway, IDisposable
  {
      public WirelessConnectionGateway(ApplicationHost host);
  }
  ```
  가짜: `FakeWirelessConnectionGateway` — `List<string> Calls`(예: `"EnableFromUsb:phone-a:R5K:…:5555:True"`), `WirelessConnectionResult NextResult`(기본 `Succeeded("192.168.0.5:5555", "ok")`), `Exception ExceptionToThrow`, `TaskCompletionSource<WirelessConnectionResult> Gate`(null이 아니면 그 Task를 돌려준다), `Dictionary<string, PhysicalDeviceInfo> Devices`, `Dictionary<string, DeviceWirelessConnectionProfile> Profiles`, `void RaiseDevicesChanged()`.

- [ ] **Step 1: 실제 게이트웨이의 읽기 경로 테스트 작성**

```csharp
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
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test DexManager.ViewModels.Tests -c Release --filter "FullyQualifiedName~WirelessConnectionGatewayTests"`
Expected: 컴파일 오류(`WirelessConnectionGateway` 없음).

- [ ] **Step 3: 인터페이스·실제 구현·가짜 작성**

`WirelessConnectionGateway`: 생성자에서 `host.DeviceRegistry.SnapshotChanged`를 구독해 `DevicesChanged`로 전달하고 `Dispose`에서 해제한다. `FindDevice`는 `host.DeviceRegistry.Current.FindByIdentity(identity)?.Clone()`. `GetProfile`은 `host.Settings.FindDeviceWirelessConnection(identity)`를 읽어 새 `DeviceWirelessConnectionProfile`에 다섯 필드를 복사한다. 세 비동기 메서드는 `Task.Run(() => host.WirelessAdb.XxxForDevice(...), cancellationToken)`이다. 인자 검증 예외는 그대로 Task fault로 전파한다.

- [ ] **Step 4: 통과 확인**

Run: 위 명령. Expected: 4개 PASS.

- [ ] **Step 5: Commit**

```bash
git add DexManager.ViewModels/IWirelessConnectionGateway.cs DexManager.ViewModels/WirelessConnectionGateway.cs DexManager.ViewModels.Tests/FakeWirelessConnectionGateway.cs DexManager.ViewModels.Tests/WirelessConnectionGatewayTests.cs
git commit -m "feat(viewmodels): add a wireless connection gateway over the per-device WirelessAdbService API"
```

---

### Task 2: `ConnectionSettingsViewModel`

**Files:**
- Create: `DexManager.ViewModels/Settings/ConnectionSettingsViewModel.cs`
- Modify: `DexManager.Core/Resources/Strings.resx`, `DexManager.Core/Resources/Strings.ko.resx` (끝부분 `Settings.Slots.*` 다음)
- Test: `DexManager.ViewModels.Tests/Settings/ConnectionSettingsViewModelTests.cs`

**Interfaces:**
- Consumes: Task 1의 `IWirelessConnectionGateway`, `FakeWirelessConnectionGateway`; `IUiDispatcher`, `ImmediateUiDispatcher`, `QueueingUiDispatcher`; `PhysicalDeviceInfo.SelectAuthorizedTransport(DeviceTransportKind.Usb, null)`; `LocalizationService.Get/Format`.
- Produces:
  ```csharp
  namespace DexManager.ViewModels.Settings;
  public sealed partial class ConnectionSettingsViewModel : ObservableObject, IDisposable
  {
      public ConnectionSettingsViewModel(string identity, IWirelessConnectionGateway wireless, IUiDispatcher dispatcher);
      public string Identity { get; }
      [ObservableProperty] string _host;            // 프로필 WirelessHost 또는 ""
      [ObservableProperty] int _port;               // 프로필 WirelessPort 또는 5555
      [ObservableProperty] bool _autoReconnect;     // 프로필 AutoReconnect 또는 true
      [ObservableProperty] bool _isBusy;
      [ObservableProperty] string _statusMessage;   // 마지막 결과 또는 ""
      [ObservableProperty] string _transportSummary;// "USB: R5K…, Wireless: 192.168.0.5:5555" 또는 Settings.Connection.NoTransport
      [ObservableProperty] string _savedModeText;   // Format(Settings.Connection.SavedMode, "USB" | "Wireless 192.168.0.5:5555")
      [ObservableProperty] bool _hasUsbTransport;
      [ObservableProperty] bool _canDisconnect;     // 저장 모드 Wireless 이거나 Wireless transport 존재
      public bool IsPortValid => Port is >= 1 and <= 65535;
      public IAsyncRelayCommand PrepareWirelessCommand; // CanExecute: !IsBusy && HasUsbTransport && IsPortValid
      public IAsyncRelayCommand ConnectCommand;         // CanExecute: !IsBusy && Host.Trim() != "" && IsPortValid
      public IAsyncRelayCommand DisconnectCommand;      // CanExecute: !IsBusy && CanDisconnect
      public void Refresh();                            // FindDevice + GetProfile 재조회, UI 스레드에서만
  }
  ```
  새 지역화 키(영/한): `Settings.Connection.NoTransport` = "No ADB transport is connected for this device." / "이 기기에 연결된 ADB transport가 없습니다."; `Settings.Connection.SavedMode` = "Saved connection: {0}" / "저장된 연결 방식: {0}"; `Settings.Connection.AutoDetectIp` = "Leave empty to detect the phone's Wi-Fi IP over USB" / "비워 두면 USB로 휴대폰의 Wi-Fi IP를 감지합니다"; `Settings.Connection.NoUsbForPrepare` = "Connect the phone over USB first." / "먼저 휴대폰을 USB로 연결하세요."; `Settings.Connection.ActionsApplyImmediately` = "Connection actions run immediately and are saved when they succeed. Save and Cancel do not affect them." / "연결 동작은 즉시 실행되고 성공하면 저장됩니다. 저장·취소 버튼과는 무관합니다."; `Settings.Connection.PortOutOfRange` = "Port must be between 1 and 65535." / "포트는 1에서 65535 사이여야 합니다."

- [ ] **Step 1: 테스트 작성** (헬퍼 `MakeDevice(identity, usbSerial = null, wirelessEndpoint = null)`는 `PhysicalDeviceInfo`에 `DeviceTransportInfo { Kind, Serial, Status = AdbDeviceStatus.Device }`를 채운다)

```csharp
[Fact] public void Constructor_WithoutSavedProfile_UsesDefaults()
  // Host == "", Port == 5555, AutoReconnect, SavedModeText contains Get("Settings.Usb"), TransportSummary == Get("Settings.Connection.NoTransport"), !HasUsbTransport, !CanDisconnect
[Fact] public void Constructor_WithSavedWirelessProfile_LoadsFieldsAndAllowsDisconnect()
  // Profiles["phone-a"] = Wireless 192.168.0.5:5556 → Host, Port, CanDisconnect, SavedModeText contains "192.168.0.5:5556"
[Fact] public void PrepareWirelessCommand_WithoutUsbTransport_CannotExecute()
[Fact] public async Task PrepareWirelessCommand_WithUsbTransport_CallsEnableFromUsbWithThatSerialAndShowsEndpoint()
  // Devices["phone-a"] = MakeDevice("phone-a", usbSerial: "R5K"); Host = "" → Calls[0] starts with "EnableFromUsb:phone-a:R5K:" and StatusMessage == "ok (192.168.0.5:5555)"
[Fact] public async Task ConnectCommand_CallsConnectWithHostPortAutoReconnect()
[Fact] public void ConnectCommand_WhenHostEmpty_CannotExecute()
[Fact] public void ConnectCommand_WhenPortOutOfRange_CannotExecute()
  // Port = 70000 → !CanExecute, StatusMessage == Get("Settings.Connection.PortOutOfRange")
[Fact] public async Task DisconnectCommand_OnSuccess_RefreshesProfileToUsb()
  // Profiles wireless → fake DisconnectAsync also sets Profiles["phone-a"].Mode = Usb → after command CanDisconnect false (no wireless transport)
[Fact] public async Task Command_WhenGatewayThrows_ShowsLocalizedFailureAndClearsBusy()
  // ExceptionToThrow = new InvalidOperationException("boom") → StatusMessage == Format("Settings.WirelessOperationFailed", "boom"), !IsBusy
[Fact] public async Task Command_WhileRunning_IsBusyAndOtherCommandsCannotExecute()
  // Gate = new TCS; start ConnectCommand; IsBusy && !PrepareWirelessCommand.CanExecute(null); Gate.SetResult(...) ; !IsBusy
[Fact] public async Task Dispose_DuringPendingOperation_IgnoresLateResult()
  // Gate; start; Dispose(); Gate.SetResult(Succeeded(...)); await task → no throw, StatusMessage unchanged
[Fact] public void DevicesChanged_RefreshesTransportSummaryThroughDispatcher()
  // QueueingUiDispatcher; Devices updated; RaiseDevicesChanged(); summary unchanged until dispatcher.RunAll(); then contains "R5K"
[Fact] public void DevicesChanged_AfterDispose_DoesNothing()
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test DexManager.ViewModels.Tests -c Release --filter "FullyQualifiedName~ConnectionSettingsViewModelTests"`
Expected: 컴파일 오류.

- [ ] **Step 3: 뷰모델과 지역화 키 구현**

명령 본문 공통 흐름: `IsBusy = true; try { var result = await op; if (_disposed) return; StatusMessage = result.Endpoint == "" ? result.Message : $"{result.Message} ({result.Endpoint})"; Refresh(); } catch (OperationCanceledException) { StatusMessage = ""; } catch (Exception ex) { StatusMessage = LocalizationService.Format("Settings.WirelessOperationFailed", ex.Message); } finally { if (!_disposed) IsBusy = false; }`. 포트가 범위 밖이면 `StatusMessage`를 `Settings.Connection.PortOutOfRange`로 바꾸고 세 명령의 `NotifyCanExecuteChanged`를 호출한다. `TransportSummary`는 승인된 transport만 `"{Kind}: {Serial}"`로 나열한다(`DeviceViewModel.TransportSummary`와 같은 형식).

- [ ] **Step 4: 통과 확인**

Run: 위 명령. Expected: 전부 PASS. 추가로 `dotnet build DexManager.Mac.sln -c Release --warnaserror` 경고 0.

- [ ] **Step 5: Commit**

```bash
git add DexManager.ViewModels/Settings/ConnectionSettingsViewModel.cs DexManager.ViewModels.Tests/Settings/ConnectionSettingsViewModelTests.cs DexManager.Core/Resources/Strings.resx DexManager.Core/Resources/Strings.ko.resx
git commit -m "feat(viewmodels): add the per-device Connection settings page"
```

---

### Task 3: `SettingsViewModel`에 Connection 페이지 연결

**Files:**
- Modify: `DexManager.ViewModels/Settings/SettingsViewModel.cs` (생성자 :44, 필드 :86-90, `LoadDevicePages` :192-218, `Attach/DetachDevicePageHandlers` :234-244, `RecomputeHasChanges` :259, `Dispose` :377)
- Modify: `DexManager.ViewModels/ShellViewModel.cs:70-71`
- Modify: `DexManager.ViewModels.Tests/SettingsViewModelTests.cs:589-600` (`CreateSettings` 헬퍼)
- Test: `DexManager.ViewModels.Tests/SettingsViewModelTests.cs`

**Interfaces:**
- Consumes: Task 1 `IWirelessConnectionGateway`/`WirelessConnectionGateway`, Task 2 `ConnectionSettingsViewModel`.
- Produces: `SettingsViewModel(ISettingsGateway gateway, IDeviceSelectionSource deviceSelection, DeviceRuntimeSessionRegistry sessions, IUiDispatcher dispatcher, IWirelessConnectionGateway wireless)`; `[ObservableProperty] ConnectionSettingsViewModel _connection;` `[ObservableProperty] bool _isWirelessIdle = true;` (Connection이 null이거나 `!Connection.IsBusy`). `SettingsViewModel.Dispose`는 `Connection`을 폐기하고 `wireless`가 `IDisposable`이면 함께 폐기한다(생성자가 소유권을 넘겨받는다).

- [ ] **Step 1: 테스트 작성**

```csharp
[Fact] public void SelectingDevice_CreatesConnectionPageForThatIdentity()
  // deviceSelection.SelectedIdentity = "phone-a" → settings.Connection?.Identity == "phone-a"
[Fact] public void ClearingSelection_SetsConnectionPageNull()
[Fact] public void SelectionChange_ReplacesConnectionPageAndDisposesOld()
  // 이전 페이지를 잡아 두고 identity 변경 → ReferenceEquals false; 이전 페이지에 fake.RaiseDevicesChanged() 후에도 이전 페이지 TransportSummary가 바뀌지 않는다
[Fact] public async Task IsWirelessIdle_FalseWhileConnectionBusy()
  // fake.Gate; Connection.Host="1.2.3.4"; var t = Connection.ConnectCommand.ExecuteAsync(null); Assert.False(settings.IsWirelessIdle); Gate.SetResult(...); await t; Assert.True(settings.IsWirelessIdle)
[Fact] public void HasChanges_IgnoresConnectionPage()
  // Connection.Host 편집 → settings.HasChanges false
[Fact] public void Dispose_DisposesWirelessGatewayWhenDisposable()
  // FakeWirelessConnectionGateway에 IDisposable 구현과 Disposed 플래그 추가
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test DexManager.ViewModels.Tests -c Release --filter "FullyQualifiedName~SettingsViewModelTests"`
Expected: 컴파일 오류(생성자 인자 수).

- [ ] **Step 3: 구현**

`LoadDevicePages`에서 `Connection = identity == "" ? null : new ConnectionSettingsViewModel(identity, _wireless, _dispatcher)`를 Display/Slot과 같은 자리에서 만들고, 교체·폐기 전 이전 인스턴스를 `Dispose`한다. `AttachDevicePageHandlers`에서 `Connection.PropertyChanged`를 구독해 `IsBusy` 변경 시 `IsWirelessIdle`을 다시 계산한다. `RecomputeHasChanges`와 `SaveAll`에는 Connection을 넣지 않는다. `ShellViewModel.OpenSettings`는 `new WirelessConnectionGateway(_host)`를 만들어 다섯 번째 인자로 넘긴다. `CreateSettings` 헬퍼에 `IWirelessConnectionGateway wireless = null` 매개변수를 추가하고 기본값은 `new FakeWirelessConnectionGateway()`.

- [ ] **Step 4: 통과 확인**

Run: `dotnet test DexManager.ViewModels.Tests -c Release` Expected: 전부 PASS.

- [ ] **Step 5: Commit**

```bash
git add DexManager.ViewModels/Settings/SettingsViewModel.cs DexManager.ViewModels/ShellViewModel.cs DexManager.ViewModels.Tests/SettingsViewModelTests.cs DexManager.ViewModels.Tests/FakeWirelessConnectionGateway.cs
git commit -m "feat(viewmodels): host the Connection page in the settings window and gate Save while it is busy"
```

---

### Task 4: Avalonia 탭과 문자열 배선

**Files:**
- Modify: `DexManager.Desktop/Views/SettingsWindow.axaml` (Slots 탭 :72-193 다음에 새 TabItem; Save 버튼 MultiBinding :339-342)
- Modify: `DexManager.Desktop/Views/SettingsWindowSupport.cs` (`SettingsWindowStrings`에 속성 추가)
- Test: `DexManager.Desktop.Tests`에 기존 XAML 로드/컴파일 테스트가 있으면 그 패턴으로 `SettingsWindow` 생성 확인; 없으면 빌드와 아래 수동 체크리스트로 대신한다.

**Interfaces:**
- Consumes: Task 3 `SettingsViewModel.Connection`, `IsWirelessIdle`; Task 2 뷰모델 속성·명령; 지역화 키 `Settings.Connection`, `Settings.CurrentStatus`, `Settings.PhoneIp`, `Settings.ConnectPort`, `Settings.AutoReconnect`, `Settings.PrepareWireless`, `Settings.ConnectWireless`, `Settings.Disconnect`, `Settings.Connection.*`.

- [ ] **Step 1: `SettingsWindowStrings`에 속성 추가** — `Connection`, `CurrentStatus`, `PhoneIp`, `ConnectPort`, `AutoReconnect`, `PrepareWireless`, `ConnectWireless`, `Disconnect`, `ConnectionAutoDetectIp`, `ConnectionActionsApplyImmediately`, `ConnectionNoUsbForPrepare`(각각 `LocalizationService.Get(키)`).

- [ ] **Step 2: 탭 추가** — Display/Stream 탭과 같은 null 게이트 패턴. `Header="{x:Static local:SettingsWindowStrings.Connection}"`. 본문(`StackPanel DataContext="{Binding Connection}" x:DataType="vm:ConnectionSettingsViewModel" Spacing="10" Margin="16"`): `CurrentStatus` 제목 + `TransportSummary` + `SavedModeText`; `PhoneIp` 라벨 + `TextBox Text="{Binding Host, Mode=TwoWay}" Watermark="{x:Static local:SettingsWindowStrings.ConnectionAutoDetectIp}"`; `ConnectPort` 라벨 + `NumericUpDown Value="{Binding Port, Mode=TwoWay}" Minimum="1" Maximum="65535" FormatString="0"`; `CheckBox IsChecked="{Binding AutoReconnect}" Content=AutoReconnect`; 가로 `StackPanel`에 버튼 셋(`PrepareWirelessCommand`, `ConnectCommand`, `DisconnectCommand`); `ProgressBar IsIndeterminate="True" IsVisible="{Binding IsBusy}"`; `TextBlock Text="{Binding StatusMessage}" TextWrapping="Wrap"`; `TextBlock Text=ConnectionActionsApplyImmediately Opacity="0.7" TextWrapping="Wrap"`; `TextBlock Text=ConnectionNoUsbForPrepare IsVisible="{Binding !HasUsbTransport}" Opacity="0.7"`.

- [ ] **Step 3: Save 버튼 게이트** — MultiBinding에 `<Binding Path="IsWirelessIdle" />` 추가.

- [ ] **Step 4: 빌드 확인**

Run: `dotnet build DexManager.Mac.sln -c Release --warnaserror` Expected: 오류 0, 경고 0 (XAML 컴파일 포함). `dotnet test DexManager.Mac.sln -c Release` 전부 PASS.

- [ ] **Step 5: Commit**

```bash
git add DexManager.Desktop/Views/SettingsWindow.axaml DexManager.Desktop/Views/SettingsWindowSupport.cs
git commit -m "feat(desktop): add the Connection tab for wireless ADB to the settings window"
```

---

### Task 5: 문서

**Files:**
- Modify: `docs/TODO.md` ("다음 작업" 상단), `docs/KNOWN_ISSUES.md` (macOS 포크 현행 기록 절), `docs/SESSION.md` (상단), `docs/MACOS_GUIDE.md` (GUI 설명이 있으면 그 절)

- [ ] **Step 1: TODO** — `- [x] macOS GUI Phase 4 — 설정 창 Connection 탭(기기별 USB→무선 전환·IP 직접 연결·해제·transport 상태)` 하위에 구현 항목과 실기 미확인 체크리스트: USB 연결 상태에서 Prepare → `Transports`에 Wireless 항목 추가 표시; 케이블 분리 후 기기 행이 유지되고 DeX 시작 가능; Disconnect 뒤 저장 모드 USB; IP 오입력 시 실패 메시지와 버튼 복귀; 동작 중 Save 비활성. 별도 항목: `- [ ] TUI [5]-[1]이 EnableFromUsb(device.Identity, 5555)로 identity를 호스트 자리에 넘긴다(InteractiveHost.cs:537) — EnableFromUsbForDevice로 교체`.
- [ ] **Step 2: KNOWN_ISSUES** — "macOS GUI Connection 탭 실기 검증 범위" 절: 이 환경은 GUI를 띄우지 못하므로 전부 미확인; 6자리 페어링 미지원; 재부팅 시 tcpip 해제; `WirelessAdbService`와 설정 저장의 잠금이 달라 동작 중 Save를 막은 이유.
- [ ] **Step 3: SESSION** — 날짜 절에 구현 요약, 테스트 수, 미확인 항목.
- [ ] **Step 4: Commit**

```bash
git add docs/TODO.md docs/KNOWN_ISSUES.md docs/SESSION.md docs/MACOS_GUIDE.md
git commit -m "docs: record the GUI Connection tab, its unverified device checks and the TUI EnableFromUsb bug"
```
