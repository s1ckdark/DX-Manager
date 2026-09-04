# macOS GUI Phase 1 — Desktop 골격과 MainWindow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `ApplicationHost`에 수명주기(정리·시작·중지·변경 알림)를 완성하고, Avalonia 기반 `DexManager.Desktop` 앱이 기동해 연결된 기기 목록을 표시하고 선택할 수 있게 한다.

**Architecture:** Phase 0이 조립만 구현한 `ApplicationHost`에 수명주기 절반을 채운 뒤, Avalonia 비의존 `DexManager.ViewModels` 라이브러리를 신설하고, `DexManager.Desktop`이 그 ViewModel을 Avalonia 창에 바인딩한다. Core 이벤트는 `IUiDispatcher`를 거쳐 UI 스레드로 마샬링되며, 테스트는 즉시 실행 stub을 주입해 UI 없이 ViewModel을 검증한다.

**Tech Stack:** .NET 8 (`global.json`이 SDK `8.0.130`, `rollForward: disable`로 고정), Avalonia `11.3.20`, CommunityToolkit.Mvvm `8.4.2`, xUnit `2.5.3`

**Spec:** `docs/superpowers/specs/2026-09-03-macos-gui-design.md`

## Global Constraints

- **Phase 1은 추가 전용이다.** 기존 TUI(`DexManager.Mac`)와 WinForms(`DexManager/`)의 관측 가능한 동작을 바꾸지 않는다(스펙 7절). 유일한 예외는 스펙 8절이 지시한 serial 일원화와 8.1절의 수명주기 보강이며, 둘 다 이 계획의 Task로 명시되어 있다.
- **`DexManager`(WinForms 포크)에 어떤 의존도 만들지 않는다**(스펙 2.1절). 신규 프로젝트는 `DexManager.Core`, `DexManager.Platform.Mac`만 참조한다.
- **`DexManager.ViewModels`는 Avalonia에 의존하지 않는다**(스펙 4.2절). Avalonia 패키지 참조를 이 프로젝트에 추가하면 안 된다.
- **CommunityToolkit.Mvvm은 `DexManager.ViewModels`에만 도입한다.** `DexManager.Core`에는 추가하지 않는다(스펙 4.2절).
- **`IPlatformService`의 창 제어 8개는 구현하지 않는다**(스펙 4.5절 결정 1). 기존 스텁을 그대로 둔다.
- **기기별 상태는 전역 `TargetSerial`을 암묵적으로 사용하지 않고 명시적 serial 또는 세션을 전달한다**(스펙 4.2절).
- **Core 이벤트 구독은 ViewModel `Dispose`에서 반드시 해제한다**(스펙 4.3절).
- **기존 테스트 베이스라인은 xUnit 102개 + 다중 기기 39개 = 141개다.** 모든 Task 종료 시점에 이 141개가 전부 통과해야 한다.
  - xUnit: `dotnet test DexManager.Tests/DexManager.Tests.csproj`
  - 다중 기기: `dotnet run --project DexManager.MultiDeviceTests -c Release` (콘솔 실행형이며 `dotnet test`로는 실행되지 않는다. 마지막 줄이 `All multi-device foundation tests passed: 39`여야 한다.)
- **`dotnet` 실행 경로**: 이 기기에서 `dotnet`은 PATH에 없다. 각 명령 앞에 `export PATH="$PATH:$HOME/.dotnet"`를 두거나 `$HOME/.dotnet/dotnet`을 직접 호출한다.
- **커밋 트레일러**: 커밋 메시지 끝에 `Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>`를 넣는다.

## 배경: 왜 Task 1~5가 UI보다 먼저인가

스펙 8.1절은 Phase 0 최종 리뷰가 지적한 여섯 가지 공백을 기록한다. 공통 원인은 하나다 — 계획은 `ApplicationHost`에 "조립·수명주기"를 배정했으나 Phase 0은 전반부만 구현했다. TUI 하나만 소비할 때는 드러나지 않지만, GUI가 두 번째 소비자가 되는 순간 순서대로 문제가 된다.

| # | 공백 | 이 계획의 대응 |
| :--- | :--- | :--- |
| 1 | `Dispose()`가 비어 있다 | Task 1 |
| 2 | `Start()`/`Stop()`이 없다 | Task 2 |
| 3 | 단일 사용 계약이 명시되지 않았다 | Task 1 (`IsDisposed`) |
| 4 | `SelectedSerial`에 변경 알림이 없다 | Task 3 |
| 5 | `Settings`가 저장 조율 없이 공유된다 | Task 4 |
| 6 | Core 서비스 13개가 구체 타입으로 노출된다 | Task 6 (의식적 결정을 스펙에 기록) |

스펙 8절의 `_selectedDeviceSerial` 이중 추적 해소는 Task 5다.

## File Structure

**수정:**

| 파일 | 책임 변화 |
| :--- | :--- |
| `DexManager.Core/Hosting/ApplicationHost.cs` | 조립만 → 조립 + 수명주기(정리·시작·중지·변경 알림) |
| `DexManager.Mac/Hosting/InteractiveHost.cs` | 자체 serial 필드 제거, 호스트의 `Start()`/`Stop()` 사용 |
| `DexManager.Tests/ApplicationHostTests.cs` | 수명주기 테스트 추가 |
| `DexManager.Mac.sln` | 신규 프로젝트 3개 등록 |
| `docs/superpowers/specs/2026-09-03-macos-gui-design.md` | 8.1절 6번 항목에 결정 기록 |

**신규:**

| 파일 | 책임 |
| :--- | :--- |
| `DexManager.ViewModels/DexManager.ViewModels.csproj` | net8.0 라이브러리, Avalonia 비의존 |
| `DexManager.ViewModels/IUiDispatcher.cs` | UI 스레드 마샬링 추상화 |
| `DexManager.ViewModels/DeviceViewModel.cs` | 기기 1대의 표시용 상태 |
| `DexManager.ViewModels/DeviceListViewModel.cs` | 기기 목록과 선택 |
| `DexManager.ViewModels/ShellViewModel.cs` | 앱 전역 상태, 호스트 수명주기 소유 |
| `DexManager.ViewModels.Tests/DexManager.ViewModels.Tests.csproj` | xUnit 테스트 |
| `DexManager.ViewModels.Tests/ImmediateUiDispatcher.cs` | 즉시 실행 stub |
| `DexManager.ViewModels.Tests/DeviceListViewModelTests.cs` | 목록·선택 로직 검증 |
| `DexManager.ViewModels.Tests/ShellViewModelTests.cs` | 구독 해제·수명주기 검증 |
| `DexManager.Desktop/DexManager.Desktop.csproj` | Avalonia 실행 파일 |
| `DexManager.Desktop/Program.cs` | 진입점 |
| `DexManager.Desktop/App.axaml` + `.axaml.cs` | 앱 수명주기, 테마 |
| `DexManager.Desktop/AvaloniaUiDispatcher.cs` | `IUiDispatcher`의 Avalonia 구현 |
| `DexManager.Desktop/Views/MainWindow.axaml` + `.axaml.cs` | 기기 목록 창 |

---

### Task 1: `ApplicationHost` 정리 책임과 단일 사용 계약

스펙 8.1절 공백 1·3. 현재 `Dispose()`는 `_disposed` 플래그만 세우고 아무것도 정리하지 않는다. GUI가 `using var host = new ApplicationHost(...)`를 써도 `DeviceMonitor` 타이머가 계속 ADB를 폴링하고 키보드 서비스가 핸들을 붙잡는다.

호스트가 소유한 것 중 `IDisposable`은 둘뿐이다 — `DeviceMonitorService`(Core에서 유일)와 `IKeyboardService`(`DexManager.Core/Platform/IKeyboardService.cs:6`에서 `IDisposable`을 상속). 나머지 서비스는 `IDisposable`이 아니다.

`DeviceMonitorService.Dispose()`는 `Interlocked.Exchange(ref _disposed, 1) != 0` 가드로 멱등이고(`DeviceMonitorService.cs:122`), `MacKeyboardService.Dispose()`도 멱등이다. 따라서 `InteractiveHost.ShutdownAsync()`가 이미 수행하는 기존 정리(`InteractiveHost.cs:854`, `:863`)를 그대로 두어도 이중 해제가 안전하다. **TUI 종료 순서를 건드리지 않는다** — Global Constraints의 추가 전용 원칙을 지키기 위함이다.

**Files:**
- Modify: `DexManager.Core/Hosting/ApplicationHost.cs:214-218`
- Test: `DexManager.Tests/ApplicationHostTests.cs`

**Interfaces:**
- Consumes: 없음 (첫 Task)
- Produces:
  - `ApplicationHost.IsDisposed` → `bool` (get-only)
  - `ApplicationHost.Dispose()` → `void`, 멱등. `DeviceMonitor`와 `KeyboardService`를 정리하고, 정리 중 발생한 예외를 모아 `AggregateException`으로 던진다.

- [ ] **Step 1: 기존 테스트 파일에서 헬퍼 확인**

`DexManager.Tests/ApplicationHostTests.cs`를 열어 기존 테스트가 `ApplicationHost`를 어떻게 만드는지 확인한다. Phase 0에서 `DexManager.Tests/FakePlatform/`에 `FakePlatformService`, `FakePathProvider`, `FakeCaptureService`, `FakeKeyboardService`, `FakeAutoStartService`가 이미 있다. 새 fake를 만들지 말고 이것을 쓴다.

`FakeKeyboardService`에 해제 횟수를 세는 수단이 없으면 다음을 추가한다.

```csharp
// DexManager.Tests/FakePlatform/FakeKeyboardService.cs
public int DisposeCallCount { get; private set; }

public void Dispose()
{
    DisposeCallCount++;
}
```

기존 `Dispose()` 본문이 이미 있으면 그 안에 `DisposeCallCount++;`만 추가한다.

- [ ] **Step 2: 실패하는 테스트를 쓴다**

```csharp
[Fact]
public void Dispose_DisposesKeyboardServiceAndStopsDeviceMonitor()
{
    using var temp = new TempHostRoot();
    var keyboard = new FakeKeyboardService();
    var host = temp.CreateHost(keyboard: keyboard);

    host.DeviceMonitor.Start();
    host.Dispose();

    Assert.True(host.IsDisposed);
    Assert.Equal(1, keyboard.DisposeCallCount);
    Assert.Throws<ObjectDisposedException>(() => host.DeviceMonitor.Start());
}

[Fact]
public void Dispose_IsIdempotent()
{
    using var temp = new TempHostRoot();
    var keyboard = new FakeKeyboardService();
    var host = temp.CreateHost(keyboard: keyboard);

    host.Dispose();
    host.Dispose();
    host.Dispose();

    Assert.Equal(1, keyboard.DisposeCallCount);
}
```

`TempHostRoot`와 `CreateHost`는 기존 테스트 파일의 헬퍼 이름에 맞춘다. 기존 파일이 매 테스트마다 GUID 임시 디렉터리를 직접 만드는 형태라면 그 형태를 그대로 따르고, 위 두 테스트도 같은 방식으로 작성한다. **기존 파일의 패턴을 따르는 것이 우선이다.**

`Assert.Throws<ObjectDisposedException>(() => host.DeviceMonitor.Start())`가 핵심이다. `DeviceMonitorService.Start()`가 `_disposed` 가드에서 이 예외를 던지므로(`DeviceMonitorService.cs:77-79`), 모니터가 실제로 해제되었음을 증명한다. 단순히 `Dispose()`를 호출했는지가 아니라 **결과 상태**를 검증한다.

- [ ] **Step 3: 테스트가 실패하는지 확인**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet test DexManager.Tests/DexManager.Tests.csproj --filter "FullyQualifiedName~Dispose_DisposesKeyboardService|FullyQualifiedName~Dispose_IsIdempotent"
```

Expected: FAIL. 첫 테스트는 `host.IsDisposed`가 컴파일되지 않아 빌드 실패, 또는 `DisposeCallCount`가 0이어서 단언 실패.

- [ ] **Step 4: `IsDisposed`와 실제 정리를 구현한다**

`DexManager.Core/Hosting/ApplicationHost.cs`의 `Dispose()`(214-218행)를 다음으로 교체한다.

```csharp
    /// <summary>
    /// 이 호스트가 이미 해제되었는지 여부. 해제된 호스트는 재사용할 수 없다 —
    /// <see cref="DeviceMonitor"/>가 <see cref="ObjectDisposedException"/>을 던진다.
    /// 인스턴스 하나는 한 번만 사용한다.
    /// </summary>
    public bool IsDisposed => _disposed;

    /// <summary>
    /// 호스트가 소유한 서비스를 정리한다. 멱등하다.
    /// 정리 중 발생한 예외는 모두 수집한 뒤 <see cref="AggregateException"/>으로
    /// 던진다 — 앞선 실패가 뒤의 정리를 막지 않게 하기 위함이다.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        var errors = new List<Exception>();

        try
        {
            DeviceMonitor?.Dispose();
        }
        catch (Exception ex)
        {
            errors.Add(ex);
        }

        try
        {
            _keyboardService?.Dispose();
        }
        catch (Exception ex)
        {
            errors.Add(ex);
        }

        if (errors.Count > 0)
        {
            throw new AggregateException(
                "ApplicationHost disposal did not complete cleanly.",
                errors);
        }
    }
```

`_disposed = true`를 정리 **이전에** 두는 것이 의도적이다. 정리 중 예외가 나도 두 번째 `Dispose()` 호출이 같은 정리를 반복하지 않는다.

- [ ] **Step 5: 테스트가 통과하는지 확인**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet test DexManager.Tests/DexManager.Tests.csproj --filter "FullyQualifiedName~Dispose_DisposesKeyboardService|FullyQualifiedName~Dispose_IsIdempotent"
```

Expected: PASS, 2개.

- [ ] **Step 6: 전체 테스트로 회귀가 없는지 확인**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet test DexManager.Tests/DexManager.Tests.csproj
dotnet run --project DexManager.MultiDeviceTests -c Release
```

Expected: xUnit 104개 통과(기존 102 + 신규 2), 다중 기기 `All multi-device foundation tests passed: 39`.

TUI 종료 경로가 `DeviceMonitor`와 키보드 서비스를 이중 해제하게 되지만 둘 다 멱등이므로 통과해야 한다. 여기서 실패하면 멱등성 가정이 틀린 것이므로 **구현을 바꾸지 말고 보고한다.**

- [ ] **Step 7: 커밋**

```bash
git add DexManager.Core/Hosting/ApplicationHost.cs DexManager.Tests/
git commit -m "feat(core): give ApplicationHost real disposal and a single-use contract

Dispose() only set a flag, so a GUI consumer using 'using var host'
left the DeviceMonitor timer polling ADB and the keyboard service
holding its handle. It now disposes both, collects failures into an
AggregateException so one failure cannot skip the rest, and stays
idempotent. IsDisposed makes the single-use contract inspectable.

The TUI's own teardown still disposes the same two services; both
disposals are idempotent, so its shutdown ordering is unchanged.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 2: `ApplicationHost.Start()` / `Stop()`

스펙 8.1절 공백 2. 현재 `DeviceMonitor.Start()` 호출은 소비자 몫이다(`InteractiveHost.cs:73`). 소비자가 둘이 되면 시작·중지 책임이 미정의가 된다. `Start()`는 우연히 멱등이지만(`DeviceMonitorService.cs:80`의 `if (_timer != null) return;`), `Stop()`은 소비자별이 아니어서 한 호스트가 멈추면 양쪽 감시가 함께 죽는다.

Phase 1에서는 **호스트 인스턴스 하나당 소비자 하나**를 계약으로 못 박는다(Task 1의 단일 사용 계약과 짝을 이룬다). 소비자별 참조 계수는 도입하지 않는다 — 실제로 한 프로세스에 호스트가 둘 존재하는 시나리오가 없고, YAGNI에 어긋난다.

**Files:**
- Modify: `DexManager.Core/Hosting/ApplicationHost.cs`
- Modify: `DexManager.Mac/Hosting/InteractiveHost.cs:73`
- Test: `DexManager.Tests/ApplicationHostTests.cs`

**Interfaces:**
- Consumes: `ApplicationHost.IsDisposed` (Task 1)
- Produces:
  - `ApplicationHost.Start()` → `void`. `DeviceMonitor.Start()`에 위임. 해제된 호스트에서는 `ObjectDisposedException`.
  - `ApplicationHost.Stop()` → `void`. `DeviceMonitor.Stop()`에 위임. 해제된 호스트에서는 아무 일도 하지 않는다(정리 경로에서 호출될 수 있으므로 던지지 않는다).

- [ ] **Step 1: 실패하는 테스트를 쓴다**

```csharp
[Fact]
public void Start_ThenStop_LeavesHostRestartable()
{
    using var temp = new TempHostRoot();
    using var host = temp.CreateHost();

    host.Start();
    host.Stop();
    host.Start();
    host.Stop();

    Assert.False(host.IsDisposed);
}

[Fact]
public void Start_AfterDispose_Throws()
{
    using var temp = new TempHostRoot();
    var host = temp.CreateHost();
    host.Dispose();

    Assert.Throws<ObjectDisposedException>(() => host.Start());
}

[Fact]
public void Stop_AfterDispose_DoesNotThrow()
{
    using var temp = new TempHostRoot();
    var host = temp.CreateHost();
    host.Dispose();

    host.Stop();
}
```

`Stop_AfterDispose_DoesNotThrow`는 단언이 없어 보이지만 **예외가 나면 실패한다.** 정리 경로가 `Stop()`을 호출한 뒤 `Dispose()`를 부르는 순서를 소비자가 지키지 못했을 때 앱이 죽지 않아야 한다는 계약을 고정한다.

- [ ] **Step 2: 테스트가 실패하는지 확인**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet test DexManager.Tests/DexManager.Tests.csproj --filter "FullyQualifiedName~Start_ThenStop|FullyQualifiedName~Start_AfterDispose|FullyQualifiedName~Stop_AfterDispose"
```

Expected: FAIL — `Start`/`Stop`이 없어 빌드 실패.

- [ ] **Step 3: `Start()`/`Stop()`을 구현한다**

`ApplicationHost.cs`의 `SelectedSerial` 속성 선언 바로 뒤에 추가한다.

```csharp
    /// <summary>
    /// 기기 감시를 시작한다. 호스트 인스턴스 하나는 소비자 하나가 소유한다 —
    /// 여러 소비자가 한 호스트를 공유하지 않는다.
    /// </summary>
    public void Start()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ApplicationHost));
        DeviceMonitor.Start();
    }

    /// <summary>
    /// 기기 감시를 중지한다. 해제된 호스트에서는 아무 일도 하지 않는다 —
    /// 정리 경로가 순서를 어겨 호출해도 앱이 죽지 않게 한다.
    /// </summary>
    public void Stop()
    {
        if (_disposed) return;
        DeviceMonitor.Stop();
    }
```

- [ ] **Step 4: 테스트가 통과하는지 확인**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet test DexManager.Tests/DexManager.Tests.csproj --filter "FullyQualifiedName~Start_ThenStop|FullyQualifiedName~Start_AfterDispose|FullyQualifiedName~Stop_AfterDispose"
```

Expected: PASS, 3개.

- [ ] **Step 5: TUI가 호스트 API를 쓰도록 바꾼다**

`DexManager.Mac/Hosting/InteractiveHost.cs:73`의 다음 줄을

```csharp
            _deviceMonitor.Start();
```

이것으로 바꾼다.

```csharp
            _host.Start();
```

`_deviceMonitor`는 `_host.DeviceMonitor`를 가리키는 위임 속성이므로 동작이 같다. 종료 경로(`ShutdownAsync`의 `_deviceMonitor?.Stop()`, `InteractiveHost.cs:845`)는 **바꾸지 않는다** — 그 자리는 예외를 모아 보고하는 기존 구조 안에 있고, 건드리면 TUI 종료 동작이 바뀐다.

- [ ] **Step 6: 전체 테스트**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet test DexManager.Tests/DexManager.Tests.csproj
dotnet run --project DexManager.MultiDeviceTests -c Release
```

Expected: xUnit 107개 통과, 다중 기기 39개 통과.

- [ ] **Step 7: 커밋**

```bash
git add DexManager.Core/Hosting/ApplicationHost.cs DexManager.Mac/Hosting/InteractiveHost.cs DexManager.Tests/
git commit -m "feat(core): move monitor start/stop onto ApplicationHost

Starting the device monitor was the consumer's job, which is undefined
once there are two consumers. Start()/Stop() now belong to the host,
with one host instance owned by one consumer — the same single-use
contract IsDisposed already states.

Stop() is deliberately silent on a disposed host so a teardown path
that calls it out of order cannot crash the app.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 3: `SelectedSerial` 변경 알림과 null 정규화

스펙 8.1절 공백 4. MVVM 바인딩에는 변경 알림이 필요하며, **GUI가 첫 ViewModel을 작성하기 전에 결정해야 한다** — 나중에 넣으면 모든 소비자를 수정해야 한다.

`DexManager.Core`에 CommunityToolkit.Mvvm을 넣지 않는다는 제약이 있으므로 `INotifyPropertyChanged` 대신 평범한 이벤트를 쓴다.

동시에 **setter에서 `null`을 `string.Empty`로 정규화한다.** 현재 `SelectedSerial`은 생성자에서 `string.Empty`로 시작하지만 `InteractiveHost.cs:308-309`가 `GetPrimarySerial(target)`의 반환값을 그대로 대입하는데 이 메서드는 `null`을 반환할 수 있다(`InteractiveHost.cs:225`의 `?.Serial`). 즉 현재도 `SelectedSerial`은 `null`이 될 수 있다. 정규화하면 Task 5의 일원화가 초기값 차이 없이 안전해진다.

**Files:**
- Modify: `DexManager.Core/Hosting/ApplicationHost.cs:121-125`
- Test: `DexManager.Tests/ApplicationHostTests.cs`

**Interfaces:**
- Consumes: 없음
- Produces:
  - `ApplicationHost.SelectedSerial` → `string`. **절대 `null`을 반환하지 않는다.** `null` 대입은 `string.Empty`로 정규화된다.
  - `ApplicationHost.SelectedSerialChanged` → `event EventHandler<SelectedSerialChangedEventArgs>`. 값이 실제로 바뀔 때만(`StringComparison.Ordinal`) 발생한다.
  - `DexManager.Hosting.SelectedSerialChangedEventArgs` — `Previous` → `string`, `Current` → `string`, 둘 다 non-null.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

```csharp
[Fact]
public void SelectedSerial_NormalizesNullToEmpty()
{
    using var temp = new TempHostRoot();
    using var host = temp.CreateHost();

    host.SelectedSerial = null;

    Assert.NotNull(host.SelectedSerial);
    Assert.Equal(string.Empty, host.SelectedSerial);
}

[Fact]
public void SelectedSerial_RaisesChangedOnlyWhenValueDiffers()
{
    using var temp = new TempHostRoot();
    using var host = temp.CreateHost();

    var events = new List<(string Previous, string Current)>();
    host.SelectedSerialChanged += (_, e) => events.Add((e.Previous, e.Current));

    host.SelectedSerial = "R5KLTEST";
    host.SelectedSerial = "R5KLTEST";   // 같은 값 — 발생하지 않아야 한다
    host.SelectedSerial = "OTHER";
    host.SelectedSerial = null;         // "" 로 정규화되며 변경으로 간주

    Assert.Equal(3, events.Count);
    Assert.Equal((string.Empty, "R5KLTEST"), events[0]);
    Assert.Equal(("R5KLTEST", "OTHER"), events[1]);
    Assert.Equal(("OTHER", string.Empty), events[2]);
}
```

두 번째 테스트가 중요하다. 값이 같을 때 이벤트가 발생하면 ViewModel이 불필요한 갱신 루프를 돌게 되므로, **발생 횟수와 전달된 값 양쪽**을 고정한다.

- [ ] **Step 2: 테스트가 실패하는지 확인**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet test DexManager.Tests/DexManager.Tests.csproj --filter "FullyQualifiedName~SelectedSerial_Normalizes|FullyQualifiedName~SelectedSerial_RaisesChanged"
```

Expected: FAIL — `SelectedSerialChanged`가 없어 빌드 실패.

- [ ] **Step 3: EventArgs를 만든다**

`DexManager.Core/Hosting/ApplicationHost.cs` 파일 끝, `ApplicationHost` 클래스 **바깥**에 추가한다.

```csharp
/// <summary>
/// <see cref="ApplicationHost.SelectedSerialChanged"/>가 전달하는 값.
/// 두 속성 모두 <c>null</c>이 아니다.
/// </summary>
public sealed class SelectedSerialChangedEventArgs : EventArgs
{
    public SelectedSerialChangedEventArgs(string previous, string current)
    {
        Previous = previous ?? string.Empty;
        Current = current ?? string.Empty;
    }

    public string Previous { get; }
    public string Current { get; }
}
```

- [ ] **Step 4: 속성을 백킹 필드 방식으로 바꾼다**

`ApplicationHost.cs`의 `_disposed` 필드 선언(22행) 옆에 백킹 필드를 추가한다.

```csharp
    private bool _disposed;
    private string _selectedSerial = string.Empty;
```

생성자의 `SelectedSerial = string.Empty;`(42행)는 **그대로 둔다** — 필드 초기화와 중복이지만 의도가 드러나고 무해하다.

121-125행의 속성 선언을 다음으로 교체한다.

```csharp
    /// <summary>
    /// 현재 선택된 기기의 transport serial. 진단 서비스가 이 값을 읽는다.
    /// 소비 호스트(TUI/GUI)가 갱신한다.
    /// <c>null</c>을 대입하면 <see cref="string.Empty"/>로 정규화되므로
    /// 이 속성은 절대 <c>null</c>을 반환하지 않는다.
    /// </summary>
    public string SelectedSerial
    {
        get => _selectedSerial;
        set
        {
            var next = value ?? string.Empty;
            var previous = _selectedSerial;
            if (string.Equals(previous, next, StringComparison.Ordinal)) return;
            _selectedSerial = next;
            SelectedSerialChanged?.Invoke(
                this,
                new SelectedSerialChangedEventArgs(previous, next));
        }
    }

    /// <summary>
    /// <see cref="SelectedSerial"/>이 실제로 바뀔 때 발생한다.
    /// 같은 값을 다시 대입하면 발생하지 않는다.
    /// </summary>
    public event EventHandler<SelectedSerialChangedEventArgs> SelectedSerialChanged;
```

`StringComparison.Ordinal`을 쓰는 이유: serial 비교는 대소문자를 구분하는 정확 일치여야 한다. `InteractiveHost`가 표시·조회에 `OrdinalIgnoreCase`를 쓰는 것과 다르지만, 여기서는 "값이 바뀌었는가"만 판정하므로 더 엄격한 쪽이 옳다. 대소문자만 다른 값이 들어오면 변경으로 처리되어 알림이 한 번 더 갈 뿐, 잘못된 상태가 되지는 않는다.

- [ ] **Step 5: 테스트가 통과하는지 확인**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet test DexManager.Tests/DexManager.Tests.csproj --filter "FullyQualifiedName~SelectedSerial_Normalizes|FullyQualifiedName~SelectedSerial_RaisesChanged"
```

Expected: PASS, 2개.

- [ ] **Step 6: 전체 테스트**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet test DexManager.Tests/DexManager.Tests.csproj
dotnet run --project DexManager.MultiDeviceTests -c Release
```

Expected: xUnit 109개 통과, 다중 기기 39개 통과.

`EnvironmentCheckService`가 `() => SelectedSerial ?? string.Empty`(`ApplicationHost.cs:93`)로 읽으므로 정규화 후에도 동작이 같다.

- [ ] **Step 7: 커밋**

```bash
git add DexManager.Core/Hosting/ApplicationHost.cs DexManager.Tests/
git commit -m "feat(core): notify on SelectedSerial change and normalize null

MVVM binding needs a change signal, and this has to exist before the
first ViewModel is written — adding it later means editing every
consumer. Core cannot take a dependency on CommunityToolkit.Mvvm, so
this is a plain event rather than INotifyPropertyChanged.

The setter also normalizes null to empty. The property was documented
as starting empty, but InteractiveHost assigns GetPrimarySerial()'s
result straight into it and that returns null when the preferred
transport reports no serial — so it could already be null. Normalizing
removes the null/empty split before Task 5 unifies the two fields.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 4: `Settings` 저장 조율

스펙 8.1절 공백 5. `ApplicationHost.Settings`는 공유 가변 `AppSettings` 인스턴스를 저장 조율 없이 노출한다. 소비자 둘이 편집하면 `SettingsService.Save`에서 경쟁한다.

다중 기기 회귀 테스트에 `SerializesConcurrentSettingsSaves`가 이미 존재한다 — `SettingsService` 자체는 동시 저장을 직렬화한다. 남은 공백은 **호스트 수준에서 "설정을 바꾸고 저장한다"는 동작이 원자적이지 않다**는 점이다. 두 소비자가 각각 읽고-수정하고-저장하면 나중 저장이 앞의 수정을 덮는다.

Phase 1에서는 잃어버린 갱신을 막는 최소 수단만 넣는다 — 호스트가 제공하는 단일 진입점에서 수정과 저장을 함께 잠근다. 설정 UI는 Phase 3이므로 그 이상은 YAGNI다.

**Files:**
- Modify: `DexManager.Core/Hosting/ApplicationHost.cs`
- Test: `DexManager.Tests/ApplicationHostTests.cs`

**Interfaces:**
- Consumes: 없음
- Produces:
  - `ApplicationHost.UpdateSettings(Action<AppSettings> mutate)` → `void`. `mutate`를 잠금 안에서 실행한 뒤 `SettingsService.Save(Settings)`를 호출한다. `mutate`가 `null`이면 `ArgumentNullException`.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

```csharp
[Fact]
public void UpdateSettings_AppliesMutationAndPersists()
{
    using var temp = new TempHostRoot();
    using var host = temp.CreateHost();

    host.UpdateSettings(s => s.Timing.ProcessTimeoutMs = 12345);

    Assert.Equal(12345, host.Settings.Timing.ProcessTimeoutMs);

    var reloaded = host.SettingsService.Load();
    Assert.Equal(12345, reloaded.Timing.ProcessTimeoutMs);
}

[Fact]
public void UpdateSettings_ConcurrentMutationsDoNotLoseUpdates()
{
    using var temp = new TempHostRoot();
    using var host = temp.CreateHost();

    host.UpdateSettings(s => s.Timing.ProcessTimeoutMs = 0);

    Parallel.For(0, 200, _ =>
        host.UpdateSettings(s => s.Timing.ProcessTimeoutMs += 1));

    Assert.Equal(200, host.Settings.Timing.ProcessTimeoutMs);
}

[Fact]
public void UpdateSettings_NullMutation_Throws()
{
    using var temp = new TempHostRoot();
    using var host = temp.CreateHost();

    Assert.Throws<ArgumentNullException>(() => host.UpdateSettings(null));
}
```

`UpdateSettings_ConcurrentMutationsDoNotLoseUpdates`가 이 Task의 존재 이유다. `+= 1`은 읽기-수정-쓰기이므로 잠금이 없으면 200보다 작은 값이 나온다. 잠금이 있으면 정확히 200이다.

`Timing.ProcessTimeoutMs`가 `int`가 아니거나 설정 로드 시 하한이 강제되어 0이 유지되지 않으면, 같은 성질(정수 누적)을 가진 다른 설정 필드로 바꾼다. 그 경우 어떤 필드를 왜 골랐는지 커밋 메시지에 적는다.

- [ ] **Step 2: 테스트가 실패하는지 확인**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet test DexManager.Tests/DexManager.Tests.csproj --filter "FullyQualifiedName~UpdateSettings"
```

Expected: FAIL — `UpdateSettings`가 없어 빌드 실패.

- [ ] **Step 3: 구현한다**

`ApplicationHost.cs`의 필드 선언부에 잠금 객체를 추가한다.

```csharp
    private readonly object _settingsLock = new object();
```

`Stop()` 메서드 뒤에 추가한다.

```csharp
    /// <summary>
    /// 설정을 수정하고 저장한다. 수정과 저장이 한 잠금 안에서 일어나므로
    /// 소비자 둘이 동시에 읽기-수정-쓰기를 해도 갱신이 유실되지 않는다.
    /// 설정을 바꿀 때는 <see cref="Settings"/>를 직접 수정하지 말고
    /// 이 메서드를 쓴다.
    /// </summary>
    public void UpdateSettings(Action<AppSettings> mutate)
    {
        if (mutate == null) throw new ArgumentNullException(nameof(mutate));

        lock (_settingsLock)
        {
            mutate(Settings);
            SettingsService.Save(Settings);
        }
    }
```

`Settings` 속성 문서 주석을 갱신해 직접 수정을 만류한다. 106행의

```csharp
    public AppSettings Settings { get; }
```

을 다음으로 바꾼다.

```csharp
    /// <summary>
    /// 현재 설정. 읽기 전용으로 취급한다 — 수정과 저장은
    /// <see cref="UpdateSettings"/>를 거쳐야 갱신 유실이 없다.
    /// </summary>
    public AppSettings Settings { get; }
```

`EnsureDefaultPaths()`가 생성자에서 `Settings`를 직접 수정하고 `SettingsService.Save`를 부르는 것은 **그대로 둔다.** 생성자 실행 중에는 다른 소비자가 이 인스턴스를 볼 수 없으므로 경쟁이 없다.

- [ ] **Step 4: 테스트가 통과하는지 확인**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet test DexManager.Tests/DexManager.Tests.csproj --filter "FullyQualifiedName~UpdateSettings"
```

Expected: PASS, 3개.

- [ ] **Step 5: 전체 테스트**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet test DexManager.Tests/DexManager.Tests.csproj
dotnet run --project DexManager.MultiDeviceTests -c Release
```

Expected: xUnit 112개 통과, 다중 기기 39개 통과.

- [ ] **Step 6: 커밋**

```bash
git add DexManager.Core/Hosting/ApplicationHost.cs DexManager.Tests/
git commit -m "feat(core): make settings mutation and save atomic

Settings is a shared mutable object with no write coordination, so two
consumers doing read-modify-write lose each other's changes even though
SettingsService already serializes the writes themselves.

UpdateSettings holds one lock across both the mutation and the save.
The concurrency test increments a value 200 times in parallel and
requires exactly 200 — an unlocked read-modify-write lands short.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 5: `InteractiveHost`의 serial 이중 추적 해소

스펙 8절. `InteractiveHost._selectedDeviceSerial`과 `ApplicationHost.SelectedSerial`이 같은 개념을 이중으로 추적한다. 현재 대입 4곳(`InteractiveHost.cs` 64, 308, 326, 346행)이 모두 짝지어 동기화되어 어긋날 수 없지만, 다섯 번째 대입이 동기화를 빠뜨리면 `EnvironmentCheckService`의 진단이 잘못된 기기를 대상으로 실행되며 어떤 자동 테스트도 이를 잡지 못한다.

**스펙의 경고를 이 계획이 정정한다.** 스펙 8절은 "`InteractiveHost.cs:166`의 `string.Equals(...)` 결과가 기기가 serial을 보고하지 않는 경우에 뒤집힌다"고 적었으나, 코드를 읽으면 그렇지 않다.

- 166행의 `primarySerial`은 `device.SelectPreferredTransport(null)?.Serial ?? "(no-serial)"`이므로 **절대 `null`이나 빈 문자열이 아니다.** `string.Equals("(no-serial)", null)`과 `string.Equals("(no-serial)", "")`은 둘 다 `false`다.
- 212행이 `string.IsNullOrWhiteSpace`로 조기 반환하므로 218-219행은 값이 비어 있지 않을 때만 도달한다.
- 225행의 `SelectPreferredTransport(_selectedDeviceSerial)`는 `FindTransport`로 위임되는데, 그 메서드가 `if (string.IsNullOrWhiteSpace(serial) || Transports == null) return null;`로 시작한다(`PhysicalDeviceInfo.cs:33-34`). **`null`과 `""`가 동일하게 처리된다.**

즉 Task 3의 정규화까지 마친 시점에서 이 일원화는 도달 가능한 모든 경로에서 동작 중립이다. 그럼에도 **그 등가성 자체를 테스트로 고정한 뒤에** 바꾼다 — 등가성이 `FindTransport`의 구현 세부에 의존하므로, 그 가드가 사라지면 이 Task가 깨진다는 사실을 테스트가 알려야 한다.

**Files:**
- Modify: `DexManager.Mac/Hosting/InteractiveHost.cs` (32, 62, 64, 166, 212, 218, 219, 225, 308-309, 326-327, 346-348, 835행)
- Test: `DexManager.Tests/` (신규 테스트 파일 또는 기존 모델 테스트 파일)

**Interfaces:**
- Consumes: `ApplicationHost.SelectedSerial` (Task 3의 정규화된 버전)
- Produces: 없음 (내부 정리)

- [ ] **Step 1: 등가성을 고정하는 테스트를 쓴다**

`DexManager.Tests/`에 `PhysicalDeviceInfoTests.cs`가 이미 있으면 거기에, 없으면 새로 만든다.

```csharp
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
```

`DeviceTransportInfo.IsAuthorized`는 **설정할 수 없는 계산 속성**이다 — `Status == AdbDeviceStatus.Device`로 파생된다(`DeviceTransportInfo.cs:21-24`). 그래서 위 코드가 `IsAuthorized`가 아니라 `Status`를 설정한다. `SelectPreferredTransport`가 `FindFirstAuthorized`로 transport를 고르므로 `Status`를 `AdbDeviceStatus.Device`로 두지 않으면 테스트가 의도한 경로를 타지 않는다.

`AdbDeviceStatus`는 `DexManager.Core/Models/AdbDeviceInfo.cs`에 있으므로 `using DexManager.Models;` 한 줄로 함께 들어온다.

- [ ] **Step 2: 테스트가 통과하는지 확인 (이것은 특성화 테스트다)**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet test DexManager.Tests/DexManager.Tests.csproj --filter "FullyQualifiedName~PhysicalDeviceTransportSelectionTests"
```

Expected: **PASS, 2개.** 이 테스트는 실패하는 것이 목적이 아니라 **현재 동작을 고정**하는 것이 목적이다. 여기서 실패하면 이 Task의 전제가 틀린 것이므로 구현을 진행하지 말고 보고한다.

- [ ] **Step 3: 필드를 제거하고 호스트 속성으로 대체한다**

`DexManager.Mac/Hosting/InteractiveHost.cs`에서 다음을 수행한다.

32행의 필드 선언을 **삭제**한다.

```csharp
        private string _selectedDeviceSerial;
```

그 자리에 위임 속성을 추가한다. 파일의 다른 위임 속성(`_adbService`, `_keyboardService` 등)과 같은 자리, 같은 형태로 둔다.

```csharp
        private string _selectedDeviceSerial
        {
            get => _host.SelectedSerial;
            set => _host.SelectedSerial = value;
        }
```

이렇게 하면 62, 64, 166, 212, 218, 219, 225, 308, 326, 346, 835행의 **모든 참조를 그대로 둘 수 있다.** 이중 추적은 사라지고 읽는 곳은 한 곳이 된다.

- [ ] **Step 4: 중복 대입을 제거한다**

이제 `_selectedDeviceSerial`에 대입하면 곧바로 `_host.SelectedSerial`이 갱신되므로 짝지어진 대입이 중복이다. 다음 세 곳에서 두 번째 줄을 삭제한다.

309행:
```csharp
                _selectedDeviceSerial = GetPrimarySerial(target);
                _host.SelectedSerial = _selectedDeviceSerial;   // ← 이 줄 삭제
```

327행:
```csharp
            _selectedDeviceSerial = serial;
            _host.SelectedSerial = _selectedDeviceSerial;       // ← 이 줄 삭제
```

348행:
```csharp
                _selectedDeviceSerial =
                    runtime.Dex.CurrentSession?.Serial ?? serial;
                _host.SelectedSerial = _selectedDeviceSerial;   // ← 이 줄 삭제
```

64-65행의 이벤트 핸들러도 마찬가지다.
```csharp
                    _selectedDeviceSerial = e.Current.Serial;
                    _host.SelectedSerial = e.Current.Serial;    // ← 이 줄 삭제
```

- [ ] **Step 5: 남은 참조가 없는지 확인한다**

```bash
grep -n '_host.SelectedSerial' DexManager.Mac/Hosting/InteractiveHost.cs
```

Expected: 위임 속성 안의 두 줄(`get`/`set`)만 나와야 한다. 다른 줄이 남아 있으면 Step 4를 마저 수행한다.

- [ ] **Step 6: 빌드하고 전체 테스트**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet build DexManager.Mac/DexManager.Mac.csproj
dotnet test DexManager.Tests/DexManager.Tests.csproj
dotnet run --project DexManager.MultiDeviceTests -c Release
```

Expected: 빌드 성공, xUnit 114개 통과, 다중 기기 39개 통과.

- [ ] **Step 7: 커밋**

```bash
git add DexManager.Mac/Hosting/InteractiveHost.cs DexManager.Tests/
git commit -m "refactor(mac): track the selected serial in one place

InteractiveHost kept its own _selectedDeviceSerial beside
ApplicationHost.SelectedSerial. The four assignment sites were paired
so they could not drift, but a fifth that forgot the pairing would
silently point EnvironmentCheckService's diagnostics at the wrong
device, and no test would catch it. The field is now a delegating
property over the host's value.

The spec warned this was not behavior-neutral, naming InteractiveHost
line 166. Reading the code, that line compares against a value that is
never null or empty, so null and empty both yield false there. The
real hinge is SelectPreferredTransport, which delegates to
FindTransport, which guards on IsNullOrWhiteSpace and so treats null
and empty identically. A characterization test now pins that
equivalence, so the guard cannot be removed without breaking a test
that explains why it matters.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 6: `DexManager.ViewModels` 프로젝트와 `IUiDispatcher`

스펙 4.2·4.3절. Avalonia에 의존하지 않는 ViewModel 라이브러리를 만들고 UI 스레드 마샬링 추상화를 정의한다. 스펙 8.1절 공백 6(서비스 노출 표면)에 대한 의식적 결정도 여기서 내린다 — ViewModel이 호스트를 어떻게 소비할지가 곧 그 표면을 정하기 때문이다.

**공백 6에 대한 결정**: Core 서비스 13개에 인터페이스를 새로 만들지 않는다. 대신 **소비 규칙**을 둔다.

- `ShellViewModel`만 `ApplicationHost` 전체를 받는다. 앱 수명주기(`Start`/`Stop`/`Dispose`)를 소유해야 하기 때문이다.
- 나머지 ViewModel은 **자기가 실제로 쓰는 서비스만** 생성자로 받는다. 호스트를 통째로 넘기지 않는다.

이유: 인터페이스 13개를 미리 만드는 것은 두 번째 구현이 없는 상태에서의 추측이다(YAGNI). 반면 좁은 생성자 의존성은 비용이 0이면서 결합을 실제로 제한하고, 테스트에서 fake를 주입할 지점을 자연스럽게 만든다. 인터페이스 추출이 필요해지면 그때 각 ViewModel의 생성자가 이미 그 경계를 알려준다.

**Files:**
- Create: `DexManager.ViewModels/DexManager.ViewModels.csproj`
- Create: `DexManager.ViewModels/IUiDispatcher.cs`
- Create: `DexManager.ViewModels.Tests/DexManager.ViewModels.Tests.csproj`
- Create: `DexManager.ViewModels.Tests/ImmediateUiDispatcher.cs`
- Create: `DexManager.ViewModels.Tests/ImmediateUiDispatcherTests.cs`
- Modify: `docs/superpowers/specs/2026-09-03-macos-gui-design.md` (8.1절 6번 항목)

**Interfaces:**
- Consumes: 없음
- Produces:
  - `DexManager.ViewModels.IUiDispatcher` — `bool IsOnUiThread { get; }`, `void Post(Action action)`, `Task InvokeAsync(Func<Task> action)`
  - `DexManager.ViewModels.Tests.ImmediateUiDispatcher` — `IUiDispatcher` 구현. `Post`는 즉시 동기 실행, `IsOnUiThread`는 항상 `true`. `PostCount` → `int`로 호출 횟수를 노출한다.

- [ ] **Step 1: ViewModels 프로젝트를 만든다**

`DexManager.ViewModels/DexManager.ViewModels.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <RootNamespace>DexManager.ViewModels</RootNamespace>
    <AssemblyName>DexManager.ViewModels</AssemblyName>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>disable</Nullable>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\DexManager.Core\DexManager.Core.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.2" />
  </ItemGroup>

</Project>
```

`Nullable`을 `disable`로 두는 것은 저장소의 다른 프로젝트 전부와 맞추기 위함이다. 여기만 켜면 Core 타입과 섞일 때 경고가 쏟아진다.

**Avalonia 패키지를 이 프로젝트에 추가하지 않는다** — Global Constraints.

- [ ] **Step 2: `IUiDispatcher`를 정의한다**

`DexManager.ViewModels/IUiDispatcher.cs`:

```csharp
namespace DexManager.ViewModels;

/// <summary>
/// UI 스레드로 작업을 넘기는 추상화. ViewModel이 특정 UI 프레임워크에
/// 묶이지 않게 한다.
/// </summary>
/// <remarks>
/// Core 이벤트(<c>DeviceMonitorService.DeviceConnected</c> 등)는 백그라운드
/// 스레드에서 발생한다. ViewModel이 관측 가능한 상태를 바꾸기 전에 반드시
/// 이 인터페이스를 거친다.
/// </remarks>
public interface IUiDispatcher
{
    /// <summary>현재 스레드가 UI 스레드인지 여부.</summary>
    bool IsOnUiThread { get; }

    /// <summary>
    /// UI 스레드에서 실행할 작업을 큐에 넣는다. 호출자를 막지 않는다.
    /// </summary>
    void Post(Action action);

    /// <summary>
    /// UI 스레드에서 비동기 작업을 실행하고 완료를 기다린다.
    /// </summary>
    Task InvokeAsync(Func<Task> action);
}
```

- [ ] **Step 3: 테스트 프로젝트를 만든다**

`DexManager.ViewModels.Tests/DexManager.ViewModels.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <RootNamespace>DexManager.ViewModels.Tests</RootNamespace>
    <AssemblyName>DexManager.ViewModels.Tests</AssemblyName>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>disable</Nullable>
    <LangVersion>latest</LangVersion>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.5.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.3" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\DexManager.Core\DexManager.Core.csproj" />
    <ProjectReference Include="..\DexManager.ViewModels\DexManager.ViewModels.csproj" />
  </ItemGroup>

</Project>
```

패키지 버전은 `DexManager.Tests/DexManager.Tests.csproj`와 정확히 같게 맞춘다.

- [ ] **Step 4: `ImmediateUiDispatcher`와 그 테스트를 쓴다**

`DexManager.ViewModels.Tests/ImmediateUiDispatcher.cs`:

```csharp
using DexManager.ViewModels;

namespace DexManager.ViewModels.Tests;

/// <summary>
/// 테스트용 디스패처. 모든 작업을 호출 스레드에서 즉시 실행하므로
/// ViewModel 로직을 UI 없이 동기적으로 검증할 수 있다.
/// </summary>
public sealed class ImmediateUiDispatcher : IUiDispatcher
{
    public bool IsOnUiThread => true;

    /// <summary>지금까지 <see cref="Post"/>가 호출된 횟수.</summary>
    public int PostCount { get; private set; }

    public void Post(Action action)
    {
        PostCount++;
        action?.Invoke();
    }

    public Task InvokeAsync(Func<Task> action)
    {
        PostCount++;
        return action?.Invoke() ?? Task.CompletedTask;
    }
}
```

`DexManager.ViewModels.Tests/ImmediateUiDispatcherTests.cs`:

```csharp
using Xunit;

namespace DexManager.ViewModels.Tests;

public class ImmediateUiDispatcherTests
{
    [Fact]
    public void Post_RunsSynchronouslyAndCounts()
    {
        var dispatcher = new ImmediateUiDispatcher();
        var ran = false;

        dispatcher.Post(() => ran = true);

        Assert.True(ran);
        Assert.Equal(1, dispatcher.PostCount);
    }

    [Fact]
    public async Task InvokeAsync_AwaitsTheAction()
    {
        var dispatcher = new ImmediateUiDispatcher();
        var ran = false;

        await dispatcher.InvokeAsync(async () =>
        {
            await Task.Yield();
            ran = true;
        });

        Assert.True(ran);
        Assert.Equal(1, dispatcher.PostCount);
    }

    [Fact]
    public void Post_NullAction_DoesNotThrow()
    {
        var dispatcher = new ImmediateUiDispatcher();

        dispatcher.Post(null);

        Assert.Equal(1, dispatcher.PostCount);
    }
}
```

- [ ] **Step 5: 빌드하고 테스트한다**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet test DexManager.ViewModels.Tests/DexManager.ViewModels.Tests.csproj
```

Expected: PASS, 3개.

- [ ] **Step 6: 공백 6의 결정을 스펙에 기록한다**

`docs/superpowers/specs/2026-09-03-macos-gui-design.md`의 8.1절 6번 항목

```
6. **Core 서비스 13개가 전부 구체 타입으로 노출된다.** 소비자 둘까지는 방어 가능하나 표면은 늘어나기만 한다. 누적이 아니라 의식적 결정이 필요하다.
```

을 다음으로 교체한다.

```
6. ~~**Core 서비스 13개가 전부 구체 타입으로 노출된다.**~~ — **결정됨(2026-09-05, Phase 1).** 인터페이스를 새로 만들지 않고 소비 규칙을 둔다: `ShellViewModel`만 `ApplicationHost` 전체를 받고(앱 수명주기를 소유해야 하므로), 나머지 ViewModel은 자기가 쓰는 서비스만 생성자로 받는다. 인터페이스 13개를 미리 만드는 것은 두 번째 구현이 없는 상태의 추측이다. 좁은 생성자 의존성은 비용 없이 결합을 제한하고, 추출이 필요해지는 시점에는 각 생성자가 이미 경계를 알려준다.
```

- [ ] **Step 7: 커밋**

```bash
git add DexManager.ViewModels/ DexManager.ViewModels.Tests/ docs/superpowers/specs/2026-09-03-macos-gui-design.md
git commit -m "feat(viewmodels): add the Avalonia-free ViewModel library

IUiDispatcher is the seam that keeps ViewModels off any UI framework:
Core events arrive on background threads, and every state change goes
through this interface first. Tests inject a dispatcher that runs
inline, so ViewModel logic is verified synchronously with no UI.

Also settles the spec's open question about the host's service surface.
No new interfaces for the 13 concrete services — instead ShellViewModel
takes the whole host because it owns the lifecycle, and every other
ViewModel takes only the services it uses. Writing 13 interfaces with
one implementation would be guessing at a boundary; narrow constructors
cost nothing and show where the real boundary is when extraction is
finally warranted.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 7: `DeviceViewModel`과 `DeviceListViewModel`

기기 목록의 표시 상태와 선택을 담당한다. `PhysicalDeviceRegistry.SnapshotChanged`를 구독하고, 스냅샷을 `IUiDispatcher`를 거쳐 반영한다.

**Files:**
- Create: `DexManager.ViewModels/DeviceViewModel.cs`
- Create: `DexManager.ViewModels/DeviceListViewModel.cs`
- Create: `DexManager.ViewModels.Tests/DeviceListViewModelTests.cs`

**Interfaces:**
- Consumes: `IUiDispatcher` (Task 6)
- Produces:
  - `DeviceViewModel(PhysicalDeviceInfo info)` — `Identity` → `string` (불변), `DisplayName`/`PrimarySerial`/`TransportSummary` → `string`, `IsConnected` → `bool`, `void Update(PhysicalDeviceInfo info)`
  - `DeviceListViewModel(PhysicalDeviceRegistry registry, IUiDispatcher dispatcher)` — `Devices` → `ObservableCollection<DeviceViewModel>`, `SelectedDevice` → `DeviceViewModel`, `IDisposable`

- [ ] **Step 1: `DeviceViewModel`을 쓴다**

`DexManager.ViewModels/DeviceViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using DexManager.Models;

namespace DexManager.ViewModels;

/// <summary>
/// 기기 1대의 표시용 상태. <see cref="Identity"/>는 불변이며 목록 안에서
/// 이 ViewModel을 식별하는 키다.
/// </summary>
public sealed partial class DeviceViewModel : ObservableObject
{
    public DeviceViewModel(PhysicalDeviceInfo info)
    {
        if (info == null) throw new ArgumentNullException(nameof(info));
        Identity = info.Identity ?? string.Empty;
        Update(info);
    }

    /// <summary>기기의 영속 식별자. 재연결되어도 유지된다.</summary>
    public string Identity { get; }

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _primarySerial = string.Empty;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _transportSummary = string.Empty;

    /// <summary>
    /// 새 스냅샷의 값으로 갱신한다. 기존 인스턴스를 재사용하므로
    /// 목록 바인딩과 선택 상태가 유지된다.
    /// </summary>
    public void Update(PhysicalDeviceInfo info)
    {
        if (info == null) return;

        DisplayName = info.DisplayName ?? string.Empty;
        IsConnected = info.IsConnected;

        // 현재 serial 을 선호값으로 넘겨 같은 transport 를 계속 고르게 한다.
        PrimarySerial =
            info.SelectPreferredTransport(PrimarySerial)?.Serial ?? string.Empty;

        TransportSummary = info.Transports == null
            ? string.Empty
            : string.Join(", ", info.Transports.Select(t => $"{t.Kind}: {t.Serial}"));
    }
}
```

`[ObservableProperty]`가 붙은 필드 `_displayName`에서 소스 제너레이터가 `DisplayName` 속성을 만든다. 클래스에 `partial`이 반드시 필요하다.

- [ ] **Step 2: `DeviceListViewModel`을 쓴다**

`DexManager.ViewModels/DeviceListViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DexManager.Models;
using DexManager.Services;

namespace DexManager.ViewModels;

/// <summary>
/// 연결된 기기 목록과 현재 선택. 레지스트리 스냅샷 변경을 구독한다.
/// </summary>
public sealed partial class DeviceListViewModel : ObservableObject, IDisposable
{
    private readonly PhysicalDeviceRegistry _registry;
    private readonly IUiDispatcher _dispatcher;
    private bool _disposed;

    public DeviceListViewModel(
        PhysicalDeviceRegistry registry,
        IUiDispatcher dispatcher)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

        _registry.SnapshotChanged += OnSnapshotChanged;
        Apply(_registry.Current);
    }

    public ObservableCollection<DeviceViewModel> Devices { get; } = new();

    [ObservableProperty]
    private DeviceViewModel _selectedDevice;

    private void OnSnapshotChanged(
        object sender,
        DeviceRegistrySnapshotChangedEventArgs e)
    {
        // 이 이벤트는 기기 감시 스레드에서 온다. 관측 가능한 상태를
        // 건드리기 전에 UI 스레드로 넘긴다.
        var snapshot = e?.Current;
        _dispatcher.Post(() => Apply(snapshot));
    }

    private void Apply(DeviceRegistrySnapshot snapshot)
    {
        if (_disposed) return;

        var incoming = snapshot?.Devices ?? new List<PhysicalDeviceInfo>();

        for (var i = Devices.Count - 1; i >= 0; i--)
        {
            var identity = Devices[i].Identity;
            var stillPresent = incoming.Any(d => string.Equals(
                d.Identity, identity, StringComparison.OrdinalIgnoreCase));
            if (!stillPresent) Devices.RemoveAt(i);
        }

        foreach (var info in incoming)
        {
            var existing = Devices.FirstOrDefault(v => string.Equals(
                v.Identity, info.Identity, StringComparison.OrdinalIgnoreCase));

            if (existing != null) existing.Update(info);
            else Devices.Add(new DeviceViewModel(info));
        }

        if (SelectedDevice != null && !Devices.Contains(SelectedDevice))
        {
            SelectedDevice = null;
        }

        if (SelectedDevice == null)
        {
            SelectedDevice = Devices.FirstOrDefault();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _registry.SnapshotChanged -= OnSnapshotChanged;
    }
}
```

기존 `DeviceViewModel` 인스턴스를 재사용하고 `Update`만 부르는 것이 핵심이다. 매번 새로 만들면 `SelectedDevice` 참조가 끊겨 사용자의 선택이 폴링마다 초기화된다.

- [ ] **Step 3: 실패하는 테스트를 쓴다**

`DexManager.ViewModels.Tests/DeviceListViewModelTests.cs`:

```csharp
using DexManager.Models;
using DexManager.Services;
using Xunit;

namespace DexManager.ViewModels.Tests;

public class DeviceListViewModelTests
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
    public void AddsDevicesFromSnapshotAndSelectsFirst()
    {
        var registry = new PhysicalDeviceRegistry();
        using var list = new DeviceListViewModel(registry, new ImmediateUiDispatcher());

        registry.Reconcile(new[]
        {
            Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb),
            Device("phone-b", "Galaxy B", "USB-B", DeviceTransportKind.Usb)
        });

        Assert.Equal(2, list.Devices.Count);
        Assert.NotNull(list.SelectedDevice);
        Assert.Equal("Galaxy A", list.Devices[0].DisplayName);
        Assert.Equal("USB-A", list.Devices[0].PrimarySerial);
        Assert.True(list.Devices[0].IsConnected);
    }

    [Fact]
    public void ReusesViewModelInstanceSoSelectionSurvivesPolling()
    {
        var registry = new PhysicalDeviceRegistry();
        using var list = new DeviceListViewModel(registry, new ImmediateUiDispatcher());

        registry.Reconcile(new[]
        {
            Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb),
            Device("phone-b", "Galaxy B", "USB-B", DeviceTransportKind.Usb)
        });

        var chosen = list.Devices[1];
        list.SelectedDevice = chosen;

        // 같은 기기가 다시 보고된다 — 폴링 한 바퀴
        registry.Reconcile(new[]
        {
            Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb),
            Device("phone-b", "Galaxy B", "USB-B", DeviceTransportKind.Usb)
        });

        Assert.Same(chosen, list.SelectedDevice);
    }

    [Fact]
    public void RemovesDisappearedDeviceAndMovesSelection()
    {
        var registry = new PhysicalDeviceRegistry();
        using var list = new DeviceListViewModel(registry, new ImmediateUiDispatcher());

        registry.Reconcile(new[]
        {
            Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb),
            Device("phone-b", "Galaxy B", "USB-B", DeviceTransportKind.Usb)
        });
        list.SelectedDevice = list.Devices.First(d => d.Identity == "phone-b");

        registry.Reconcile(new[]
        {
            Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb)
        });

        Assert.Single(list.Devices);
        Assert.Equal("phone-a", list.SelectedDevice.Identity);
    }

    [Fact]
    public void MarshalsSnapshotChangesThroughTheDispatcher()
    {
        var registry = new PhysicalDeviceRegistry();
        var dispatcher = new ImmediateUiDispatcher();
        using var list = new DeviceListViewModel(registry, dispatcher);

        var before = dispatcher.PostCount;
        registry.Reconcile(new[]
        {
            Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb)
        });

        Assert.True(dispatcher.PostCount > before);
    }

    [Fact]
    public void Dispose_UnsubscribesFromRegistry()
    {
        var registry = new PhysicalDeviceRegistry();
        var list = new DeviceListViewModel(registry, new ImmediateUiDispatcher());

        list.Dispose();

        registry.Reconcile(new[]
        {
            Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb)
        });

        Assert.Empty(list.Devices);
    }
}
```

`Dispose_UnsubscribesFromRegistry`가 스펙 4.3절의 "Core 이벤트 구독은 ViewModel `Dispose`에서 반드시 해제한다"를 강제한다. 해제하지 않으면 목록이 채워져 실패한다.

`MarshalsSnapshotChangesThroughTheDispatcher`는 디스패처를 우회해 직접 컬렉션을 건드리는 구현을 잡는다.

- [ ] **Step 4: 테스트를 실행한다**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet test DexManager.ViewModels.Tests/DexManager.ViewModels.Tests.csproj
```

Expected: PASS, 8개(Task 6의 3개 + 신규 5개).

`registry.Reconcile`이 `SnapshotChanged`를 발생시키지 않아 첫 테스트가 실패하면, `PhysicalDeviceRegistry.Reconcile`이 이벤트를 언제 발생시키는지 확인한다(변경이 없으면 발생시키지 않을 수 있다). 그 경우 테스트를 `registry.Current`를 직접 확인하는 형태가 아니라 실제 이벤트 발생 조건에 맞춰 조정한다. **구현을 이벤트 없이 폴링하도록 바꾸지 않는다.**

- [ ] **Step 5: 커밋**

```bash
git add DexManager.ViewModels/ DexManager.ViewModels.Tests/
git commit -m "feat(viewmodels): add the device list and its row ViewModel

DeviceListViewModel subscribes to the registry's snapshot changes and
reconciles in place, updating existing DeviceViewModel instances rather
than rebuilding the collection. Rebuilding would drop the SelectedDevice
reference on every poll, so the user's selection would reset roughly
once a second.

Snapshot events arrive on the monitor thread, so they cross IUiDispatcher
before touching anything observable, and Dispose unsubscribes — both are
pinned by tests that fail if the seam is bypassed.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 8: `ShellViewModel`

앱 전역 상태와 호스트 수명주기를 소유한다. Task 6의 결정에 따라 `ApplicationHost` 전체를 받는 **유일한** ViewModel이다.

**Files:**
- Create: `DexManager.ViewModels/ShellViewModel.cs`
- Create: `DexManager.ViewModels.Tests/ShellViewModelTests.cs`

**Interfaces:**
- Consumes: `ApplicationHost.Start()`/`Stop()`/`Dispose()`/`SelectedSerial`/`SelectedSerialChanged` (Task 1~3), `DeviceListViewModel` (Task 7)
- Produces:
  - `ShellViewModel(ApplicationHost host, IUiDispatcher dispatcher)` — `Devices` → `DeviceListViewModel`, `StatusText` → `string`, `void Start()`, `IDisposable`

- [ ] **Step 1: `ShellViewModel`을 쓴다**

`DexManager.ViewModels/ShellViewModel.cs`:

```csharp
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DexManager.Hosting;

namespace DexManager.ViewModels;

/// <summary>
/// 앱 전역 상태. <see cref="ApplicationHost"/>의 수명주기를 소유하는
/// 유일한 ViewModel이다.
/// </summary>
public sealed partial class ShellViewModel : ObservableObject, IDisposable
{
    private readonly ApplicationHost _host;
    private readonly IUiDispatcher _dispatcher;
    private bool _disposed;

    public ShellViewModel(ApplicationHost host, IUiDispatcher dispatcher)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

        Devices = new DeviceListViewModel(host.DeviceRegistry, dispatcher);
        Devices.PropertyChanged += OnDeviceListPropertyChanged;
        _host.SelectedSerialChanged += OnSelectedSerialChanged;
    }

    public DeviceListViewModel Devices { get; }

    [ObservableProperty]
    private string _statusText = "Starting…";

    /// <summary>기기 감시를 시작한다.</summary>
    public void Start()
    {
        _host.Start();
        StatusText = "Watching for devices";
    }

    private void OnDeviceListPropertyChanged(
        object sender,
        PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(DeviceListViewModel.SelectedDevice)) return;

        // 진단 서비스가 읽는 값이다. 선택이 바뀌면 호스트에 반영한다.
        _host.SelectedSerial = Devices.SelectedDevice?.PrimarySerial ?? string.Empty;
    }

    private void OnSelectedSerialChanged(
        object sender,
        SelectedSerialChangedEventArgs e)
    {
        var current = e?.Current ?? string.Empty;
        _dispatcher.Post(() =>
        {
            StatusText = string.IsNullOrEmpty(current)
                ? "No device selected"
                : $"Selected {current}";
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Devices.PropertyChanged -= OnDeviceListPropertyChanged;
        _host.SelectedSerialChanged -= OnSelectedSerialChanged;

        Devices.Dispose();
        _host.Stop();
        _host.Dispose();
    }
}
```

`Dispose`의 순서가 의도적이다 — 구독을 먼저 끊고, 자식을 정리하고, 마지막에 호스트를 멈추고 해제한다. 반대로 하면 정리 중 발생한 이벤트가 이미 해제된 객체를 건드린다.

- [ ] **Step 2: 실패하는 테스트를 쓴다**

`DexManager.ViewModels.Tests/ShellViewModelTests.cs`:

```csharp
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
}
```

`TempHostRoot`와 `CreateHost`는 `DexManager.Tests`의 헬퍼와 같은 개념이지만 **프로젝트가 다르므로 재사용할 수 없다.** `DexManager.ViewModels.Tests`에 같은 역할의 헬퍼를 만든다. 만들 때 `DexManager.Tests/FakePlatform/`의 fake 5종을 그대로 복사하지 말고, 다음 중 하나를 택한다.

- (a) `DexManager.Tests/FakePlatform/*.cs`를 `DexManager.ViewModels.Tests`에서 `<Compile Include="..\DexManager.Tests\FakePlatform\*.cs" Link="FakePlatform\%(Filename)%(Extension)" />`로 링크한다
- (b) fake 5종을 `DexManager.Tests`에서 새 공유 프로젝트로 옮긴다

**(a)를 택한다.** 파일을 물리적으로 옮기면 `DexManager.Tests`의 기존 테스트가 영향을 받고, 이는 Global Constraints의 추가 전용 원칙에서 벗어난다. 링크는 원본을 건드리지 않는다.

`DexManager.ViewModels.Tests.csproj`에 추가한다.

```xml
  <ItemGroup>
    <Compile Include="..\DexManager.Tests\FakePlatform\*.cs"
             Link="FakePlatform\%(Filename)%(Extension)" />
  </ItemGroup>
```

`TempHostRoot`는 `DexManager.Tests/ApplicationHostTests.cs`의 구현을 참고해 같은 동작으로 새로 쓴다(GUID 임시 디렉터리 생성, `Dispose`에서 삭제, `CreateHost`가 fake 5종으로 `ApplicationHost`를 만든다).

- [ ] **Step 3: 테스트를 실행한다**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet test DexManager.ViewModels.Tests/DexManager.ViewModels.Tests.csproj
```

Expected: PASS, 11개.

- [ ] **Step 4: 전체 테스트**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet test DexManager.Tests/DexManager.Tests.csproj
dotnet test DexManager.ViewModels.Tests/DexManager.ViewModels.Tests.csproj
dotnet run --project DexManager.MultiDeviceTests -c Release
```

Expected: 114 + 11 + 39 전부 통과.

- [ ] **Step 5: 커밋**

```bash
git add DexManager.ViewModels/ DexManager.ViewModels.Tests/
git commit -m "feat(viewmodels): add ShellViewModel owning the host lifecycle

ShellViewModel is the one ViewModel that takes the whole
ApplicationHost, because it owns Start/Stop/Dispose. Selection flows
one way into the host's SelectedSerial — the value EnvironmentCheck
reads — and status text flows back out through the host's change event,
so the two directions cannot disagree.

Dispose unhooks subscriptions before tearing anything down; the reverse
order lets an event raised during cleanup reach an already-disposed
object.

Platform fakes are linked from DexManager.Tests rather than moved, so
the existing test project is untouched.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 9: `DexManager.Desktop` Avalonia 골격

앱이 기동하고 빈 창이 뜨는 최소 상태를 만든다. 기기 목록 바인딩은 Task 10이다. 둘을 나누는 이유는 "앱이 안 뜬다"와 "목록이 안 보인다"를 동시에 디버깅하지 않기 위함이다.

**Files:**
- Create: `DexManager.Desktop/DexManager.Desktop.csproj`
- Create: `DexManager.Desktop/Program.cs`
- Create: `DexManager.Desktop/App.axaml`
- Create: `DexManager.Desktop/App.axaml.cs`
- Create: `DexManager.Desktop/AvaloniaUiDispatcher.cs`
- Create: `DexManager.Desktop/Views/MainWindow.axaml`
- Create: `DexManager.Desktop/Views/MainWindow.axaml.cs`

**Interfaces:**
- Consumes: `ShellViewModel` (Task 8), `IUiDispatcher` (Task 6), `DexManager.Mac.Platform`의 macOS 서비스 5종
- Produces:
  - `DexManager.Desktop.AvaloniaUiDispatcher` — `IUiDispatcher` 구현
  - `DexManager.Desktop.Views.MainWindow` — `Window`

- [ ] **Step 1: 프로젝트 파일을 만든다**

`DexManager.Desktop/DexManager.Desktop.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <RootNamespace>DexManager.Desktop</RootNamespace>
    <AssemblyName>DXManager.Desktop</AssemblyName>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>disable</Nullable>
    <LangVersion>latest</LangVersion>
    <Version>2.0.0</Version>
    <IncludeSourceRevisionInInformationalVersion>false</IncludeSourceRevisionInInformationalVersion>
    <AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Avalonia" Version="11.3.20" />
    <PackageReference Include="Avalonia.Desktop" Version="11.3.20" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="11.3.20" />
    <PackageReference Include="Avalonia.Diagnostics" Version="11.3.20"
                      Condition="'$(Configuration)' == 'Debug'" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\DexManager.Core\DexManager.Core.csproj" />
    <ProjectReference Include="..\DexManager.Platform.Mac\DexManager.Platform.Mac.csproj" />
    <ProjectReference Include="..\DexManager.ViewModels\DexManager.ViewModels.csproj" />
  </ItemGroup>

</Project>
```

`OutputType`이 `WinExe`인 것은 Windows 전용이 아니다 — 데스크톱 앱이 콘솔 창 없이 뜨게 하는 설정이며 macOS에서도 Avalonia 표준이다.

`AssemblyName`을 `DXManager.Desktop`으로 두어 기존 TUI(`DXManager.Mac`)와 구분한다.

Avalonia `11.3.20`을 쓰는 이유: 12.1.2가 최신이지만 `Avalonia.Diagnostics`(DevTools)에 12.x가 없다. 첫 GUI 단계에서 XAML 레이아웃 디버깅 수단을 잃는 것이 최신 버전의 이득보다 크다. 스펙 2.4절이 정한 "Avalonia 11"과도 일치한다.

- [ ] **Step 2: `AvaloniaUiDispatcher`를 쓴다**

`DexManager.Desktop/AvaloniaUiDispatcher.cs`:

```csharp
using Avalonia.Threading;
using DexManager.ViewModels;

namespace DexManager.Desktop;

/// <summary>
/// <see cref="IUiDispatcher"/>의 Avalonia 구현.
/// </summary>
public sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    public bool IsOnUiThread => Dispatcher.UIThread.CheckAccess();

    public void Post(Action action)
    {
        if (action == null) return;
        Dispatcher.UIThread.Post(action);
    }

    public Task InvokeAsync(Func<Task> action)
    {
        if (action == null) return Task.CompletedTask;
        return Dispatcher.UIThread.InvokeAsync(action).GetTask();
    }
}
```

`InvokeAsync(Func<Task>)`가 반환하는 타입이 `Task`가 아니어서 `.GetTask()`가 컴파일되지 않으면, 반환값을 `await`하는 형태로 바꾼다.

```csharp
    public async Task InvokeAsync(Func<Task> action)
    {
        if (action == null) return;
        await Dispatcher.UIThread.InvokeAsync(action);
    }
```

둘 중 컴파일되는 쪽을 쓴다.

- [ ] **Step 3: `Program.cs`를 쓴다**

`DexManager.Desktop/Program.cs`:

```csharp
using Avalonia;

namespace DexManager.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia 디자이너와 헤드리스 테스트가 이 메서드를 이름으로 찾는다.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
```

- [ ] **Step 4: `App.axaml`과 코드비하인드를 쓴다**

`DexManager.Desktop/App.axaml`:

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="DexManager.Desktop.App"
             RequestedThemeVariant="Default">
  <Application.Styles>
    <FluentTheme />
  </Application.Styles>
</Application>
```

`DexManager.Desktop/App.axaml.cs`:

```csharp
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DexManager.Desktop.Views;
using DexManager.Hosting;
using DexManager.Mac.Platform;
using DexManager.ViewModels;

namespace DexManager.Desktop;

public partial class App : Application
{
    private ShellViewModel _shell;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var pathProvider = new MacPathProvider();

            var host = new ApplicationHost(
                new MacPlatformService(),
                pathProvider,
                new MacCaptureService(pathProvider.DefaultScreenshotFolder),
                new MacKeyboardService(),
                new MacAutoStartService());

            _shell = new ShellViewModel(host, new AvaloniaUiDispatcher());

            desktop.MainWindow = new MainWindow { DataContext = _shell };
            desktop.ShutdownRequested += (_, _) => _shell?.Dispose();

            _shell.Start();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
```

서비스 조립은 `InteractiveHost` 생성자(`InteractiveHost.cs:41-48`)와 같은 형태다. 두 호스트가 같은 방식으로 조립하는 것이 스펙 3.3절의 목적이다.

- [ ] **Step 5: 빈 `MainWindow`를 만든다**

`DexManager.Desktop/Views/MainWindow.axaml`:

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="clr-namespace:DexManager.ViewModels;assembly=DexManager.ViewModels"
        x:Class="DexManager.Desktop.Views.MainWindow"
        x:DataType="vm:ShellViewModel"
        Title="DX Manager"
        Width="900" Height="600"
        MinWidth="640" MinHeight="400">
  <TextBlock Text="{Binding StatusText}"
             HorizontalAlignment="Center"
             VerticalAlignment="Center" />
</Window>
```

`DexManager.Desktop/Views/MainWindow.axaml.cs`:

```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DexManager.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
```

`InitializeComponent`가 소스 제너레이터에 의해 이미 생성되어 중복 정의 오류가 나면, 위 코드비하인드에서 `private void InitializeComponent()` 줄을 삭제하고 생성자만 남긴다.

- [ ] **Step 6: 빌드한다**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet build DexManager.Desktop/DexManager.Desktop.csproj
```

Expected: 빌드 성공, 경고 0개 또는 무해한 경고만.

- [ ] **Step 7: 실제로 기동하는지 확인한다**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet run --project DexManager.Desktop/DexManager.Desktop.csproj
```

Expected: "DX Manager" 제목의 창이 뜨고 가운데에 `Watching for devices` 또는 `No device selected`가 보인다. 창을 닫으면 프로세스가 정상 종료한다.

이 단계는 **사람이 눈으로 확인해야 한다.** 창이 뜨지 않거나 즉시 죽으면 콘솔 출력을 그대로 보고한다. 창이 떴다고 가정하지 않는다.

- [ ] **Step 8: 커밋**

```bash
git add DexManager.Desktop/
git commit -m "feat(desktop): add the Avalonia application skeleton

The window shows only StatusText for now — the device list lands in the
next task. Splitting them keeps 'the app will not start' and 'the list
is empty' from being debugged at the same time.

Service assembly mirrors InteractiveHost's constructor exactly, which is
the point of ApplicationHost: both hosts assemble the same way, so the
invariants only have to hold in one place.

Pinned to Avalonia 11.3.20 rather than the newer 12.1.2 because
Avalonia.Diagnostics has no 12.x release, and losing DevTools while
building the first XAML layouts costs more than the newer version gains.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 10: `MainWindow` 기기 목록과 솔루션 통합

Phase 1의 완료 조건인 "앱 기동, 연결 기기 표시"를 만족시킨다.

**Files:**
- Modify: `DexManager.Desktop/Views/MainWindow.axaml`
- Modify: `DexManager.Mac.sln`
- Create: `DexManager.ViewModels.Tests/DeviceViewModelTests.cs`

**Interfaces:**
- Consumes: `ShellViewModel.Devices` → `DeviceListViewModel` (Task 8), `DeviceViewModel`의 표시 속성 4종 (Task 7)
- Produces: 없음 (Phase 1 종료)

- [ ] **Step 1: 창 레이아웃을 쓴다**

`DexManager.Desktop/Views/MainWindow.axaml`의 내용을 다음으로 교체한다.

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="clr-namespace:DexManager.ViewModels;assembly=DexManager.ViewModels"
        x:Class="DexManager.Desktop.Views.MainWindow"
        x:DataType="vm:ShellViewModel"
        Title="DX Manager"
        Width="900" Height="600"
        MinWidth="640" MinHeight="400">

  <Grid RowDefinitions="Auto,*,Auto" Margin="16">

    <TextBlock Grid.Row="0"
               Text="Connected devices"
               FontSize="18"
               FontWeight="SemiBold"
               Margin="0,0,0,12" />

    <Border Grid.Row="1"
            BorderThickness="1"
            BorderBrush="{DynamicResource SystemControlForegroundBaseMediumLowBrush}"
            CornerRadius="4">
      <Panel>
        <TextBlock Text="No devices connected. Plug in a Galaxy device."
                   HorizontalAlignment="Center"
                   VerticalAlignment="Center"
                   Opacity="0.6"
                   IsVisible="{Binding !Devices.Devices.Count}" />

        <ListBox ItemsSource="{Binding Devices.Devices}"
                 SelectedItem="{Binding Devices.SelectedDevice}">
          <ListBox.ItemTemplate>
            <DataTemplate x:DataType="vm:DeviceViewModel">
              <StackPanel Margin="4">
                <StackPanel Orientation="Horizontal" Spacing="8">
                  <TextBlock Text="{Binding DisplayName}" FontWeight="SemiBold" />
                  <TextBlock Text="Connected"
                             Foreground="Green"
                             IsVisible="{Binding IsConnected}" />
                  <TextBlock Text="Disconnected"
                             Opacity="0.6"
                             IsVisible="{Binding !IsConnected}" />
                </StackPanel>
                <TextBlock Text="{Binding TransportSummary}"
                           Opacity="0.7"
                           FontSize="12" />
              </StackPanel>
            </DataTemplate>
          </ListBox.ItemTemplate>
        </ListBox>
      </Panel>
    </Border>

    <TextBlock Grid.Row="2"
               Text="{Binding StatusText}"
               Margin="0,12,0,0"
               Opacity="0.8" />

  </Grid>
</Window>
```

`IsVisible="{Binding !Devices.Devices.Count}"`는 Avalonia의 부정 바인딩이다. `int` 0이 `false`로 변환되므로 목록이 비었을 때만 안내 문구가 보인다. 이 변환이 동작하지 않으면 `DeviceListViewModel`에 `public bool IsEmpty => Devices.Count == 0;`를 추가하고 컬렉션 변경 시 `OnPropertyChanged(nameof(IsEmpty))`를 호출한 뒤 `IsVisible="{Binding Devices.IsEmpty}"`로 바꾼다.

- [ ] **Step 2: `DeviceViewModel` 표시 속성 테스트를 추가한다**

`DexManager.ViewModels.Tests/DeviceViewModelTests.cs`:

```csharp
using DexManager.Models;
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
        var vm = new DeviceViewModel(Device(
            Transport("USB-A", DeviceTransportKind.Usb),
            Transport("10.0.0.2:5555", DeviceTransportKind.Wireless)));

        Assert.Equal("Galaxy A", vm.DisplayName);
        Assert.Equal("phone-a", vm.Identity);
        Assert.Contains("Usb: USB-A", vm.TransportSummary);
        Assert.Contains("Wireless: 10.0.0.2:5555", vm.TransportSummary);
    }

    [Fact]
    public void UpdateRaisesPropertyChangedForChangedValuesOnly()
    {
        var vm = new DeviceViewModel(Device(Transport("USB-A", DeviceTransportKind.Usb)));

        var changed = new List<string>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.Update(Device(Transport("USB-A", DeviceTransportKind.Usb)));

        Assert.DoesNotContain(nameof(DeviceViewModel.DisplayName), changed);
        Assert.DoesNotContain(nameof(DeviceViewModel.PrimarySerial), changed);
    }

    [Fact]
    public void UnauthorizedDeviceIsNotConnected()
    {
        var vm = new DeviceViewModel(Device(
            Transport("USB-A", DeviceTransportKind.Usb, AdbDeviceStatus.Unauthorized)));

        Assert.False(vm.IsConnected);
    }
}
```

`UpdateRaisesPropertyChangedForChangedValuesOnly`가 중요하다. `[ObservableProperty]`는 값이 같으면 알림을 내지 않으므로, 폴링마다 UI가 통째로 다시 그려지지 않는다. 이 성질이 깨지면 목록이 초당 한 번 깜빡인다.

`PhysicalDeviceInfo.IsConnected`가 transport 상태에서 어떻게 파생되는지 확인하고(`PhysicalDeviceInfo.cs:17`), 세 번째 테스트의 기댓값이 실제 구현과 어긋나면 **구현이 아니라 테스트를 실제 동작에 맞춘다.** 그 경우 왜 그런지 커밋 메시지에 적는다.

- [ ] **Step 3: 테스트를 실행한다**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet test DexManager.ViewModels.Tests/DexManager.ViewModels.Tests.csproj
```

Expected: PASS, 14개.

- [ ] **Step 4: 솔루션에 신규 프로젝트 3개를 등록한다**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet sln DexManager.Mac.sln add \
  DexManager.ViewModels/DexManager.ViewModels.csproj \
  DexManager.ViewModels.Tests/DexManager.ViewModels.Tests.csproj \
  DexManager.Desktop/DexManager.Desktop.csproj
dotnet sln DexManager.Mac.sln list
```

Expected: 9개 프로젝트가 나열된다(기존 6 + 신규 3).

- [ ] **Step 5: 솔루션 전체를 빌드한다**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet build DexManager.Mac.sln
```

Expected: 빌드 성공.

- [ ] **Step 6: 전체 테스트 스위트**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet test DexManager.Tests/DexManager.Tests.csproj
dotnet test DexManager.ViewModels.Tests/DexManager.ViewModels.Tests.csproj
dotnet run --project DexManager.MultiDeviceTests -c Release
```

Expected: 114 + 14 + 39 = 167개 전부 통과.

- [ ] **Step 7: 기기를 연결하고 실기 확인한다**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet run --project DexManager.Desktop/DexManager.Desktop.csproj
```

확인 항목:
1. 창이 뜬다
2. 연결된 Galaxy 기기가 목록에 나타난다 — 이름, `Connected` 표시, transport 요약(`Usb: <serial>`)
3. 기기를 선택하면 하단 상태 줄이 `Selected <serial>`로 바뀐다
4. USB 케이블을 뽑으면 몇 초 안에 목록에서 사라진다
5. 다시 꽂으면 다시 나타난다
6. 창을 닫으면 프로세스가 종료된다

**이 항목들은 사람이 눈으로 확인해야 한다.** `AGENTS.md` 원칙에 따라 대신 성공했다고 가정하지 않는다. 확인하지 못했으면 미확인으로 명시해 보고한다.

기존 TUI가 여전히 정상 동작하는지도 확인한다.

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet run --project DexManager.Mac/DexManager.Mac.csproj -- --diag
```

Expected: 진단이 전부 PASS하고 선택된 기기가 표시된다.

- [ ] **Step 8: 커밋**

```bash
git add DexManager.Desktop/ DexManager.ViewModels.Tests/ DexManager.Mac.sln
git commit -m "feat(desktop): show connected devices in MainWindow

Completes Phase 1's exit condition — the app starts and lists connected
devices with their transports, and selecting one drives the host's
SelectedSerial.

The DeviceViewModel test asserts that re-applying an identical snapshot
raises no PropertyChanged. Without that, every monitor poll would
repaint the whole list about once a second.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Self-Review 결과

**1. 스펙 커버리지**

| 스펙 요구 | 대응 |
| :--- | :--- |
| 7절 Phase 1 "Desktop 골격 + MainWindow (기기 목록·선택·상태)" | Task 9, 10 |
| 7절 완료 조건 "앱 기동, 연결 기기 표시" | Task 10 Step 7 |
| 8.1절 공백 1 (빈 `Dispose`) | Task 1 |
| 8.1절 공백 2 (`Start`/`Stop` 없음) | Task 2 |
| 8.1절 공백 3 (단일 사용 계약) | Task 1 (`IsDisposed`) |
| 8.1절 공백 4 (변경 알림) | Task 3 |
| 8.1절 공백 5 (설정 저장 조율) | Task 4 |
| 8.1절 공백 6 (서비스 표면) | Task 6 (결정 + 스펙 기록) |
| 8절 serial 이중 추적 | Task 5 |
| 3.1절 `DexManager.ViewModels` 신설 | Task 6 |
| 3.2절 의존 방향 (WinForms 무연결) | Task 6·9의 `ProjectReference`가 Core/Platform.Mac/ViewModels만 참조 |
| 4.2절 CommunityToolkit.Mvvm, ViewModel 목록 | Task 6~8 (Phase 1 범위인 Shell/DeviceList/Device만) |
| 4.3절 `IUiDispatcher` 3-멤버 계약 | Task 6 |
| 4.3절 구독 해제 규칙 | Task 7 `Dispose_UnsubscribesFromRegistry`, Task 8 `Dispose_DisposesTheHost...` |
| 5.2절 즉시 실행 stub 주입 | Task 6 `ImmediateUiDispatcher` |
| 5.3절 실기 검증은 사용자 확인 항목 | Task 10 Step 7 |

**범위 밖으로 남긴 것** (의도적):
- 5.2절의 `Avalonia.Headless` 스모크 테스트 — 스펙이 "(선택)"으로 표기했고, Task 9 Step 7과 Task 10 Step 7의 실제 기동 확인이 같은 위험을 더 직접적으로 덮는다. Phase 2 이후 창이 여러 개가 되면 재검토한다.
- 4.4절 화면 기록 권한 안내 UI — 캡처는 Phase 6이다.
- 6절 `.app` 번들 패키징 — Phase 7이다.
- 4.2절 ViewModel 목록의 나머지 8종 — Phase 2 이후다.

**2. 플레이스홀더 스캔**

`TBD`/`TODO`/`적절히 처리한다` 류 없음. 모든 코드 단계에 실제 코드가 있다. 구현이 예상과 다를 수 있는 세 지점(`Timing.ProcessTimeoutMs`의 하한, `Dispatcher.UIThread.InvokeAsync`의 반환 타입, `InitializeComponent` 중복 정의)은 **대안 코드를 함께 제시**했고, 어느 쪽을 택할지 판단 기준도 적었다.

**3. 타입 일관성**

- `IUiDispatcher`의 3개 멤버가 Task 6 정의, Task 6 stub, Task 9 Avalonia 구현에서 동일하다.
- `ApplicationHost.SelectedSerial`(string, non-null)이 Task 3 정의 → Task 5 위임 속성 → Task 8 `ShellViewModel`에서 일관되게 쓰인다.
- `DeviceViewModel`의 `Identity`/`DisplayName`/`PrimarySerial`/`IsConnected`/`TransportSummary`가 Task 7 정의, Task 10 XAML 바인딩, Task 10 테스트에서 같은 이름이다.
- `DeviceListViewModel.Devices`(컬렉션)와 `ShellViewModel.Devices`(ViewModel)가 이름이 겹쳐 XAML에서 `Devices.Devices`가 된다. 어색하지만 각 클래스 안에서는 자연스러운 이름이고, Task 10 XAML에 실제 경로를 그대로 적어 두었으므로 혼동 위험은 없다.

**4. 스펙 정정 사항**

Task 5가 스펙 8절의 사실 오류 하나를 정정한다 — `InteractiveHost.cs:166`은 `null`/`""` 차이로 뒤집히지 않는다. 실제 등가성은 `PhysicalDeviceInfo.FindTransport`의 `IsNullOrWhiteSpace` 가드에서 나온다. Task 5가 그 등가성을 특성화 테스트로 고정한다.

**5. 테스트 수 예상**

| 시점 | xUnit(Tests) | xUnit(ViewModels.Tests) | 다중 기기 |
| :--- | ---: | ---: | ---: |
| 시작 | 102 | — | 39 |
| Task 1 후 | 104 | — | 39 |
| Task 2 후 | 107 | — | 39 |
| Task 3 후 | 109 | — | 39 |
| Task 4 후 | 112 | — | 39 |
| Task 5 후 | 114 | — | 39 |
| Task 6 후 | 114 | 3 | 39 |
| Task 7 후 | 114 | 8 | 39 |
| Task 8 후 | 114 | 11 | 39 |
| Task 10 후 | 114 | 14 | 39 |

합계 167개. 실제 수가 다르면 테스트를 지운 것이 아닌지 확인한다.
