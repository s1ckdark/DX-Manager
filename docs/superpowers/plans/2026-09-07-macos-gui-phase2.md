# macOS GUI Phase 2 — DeX 시작/중지와 단일창 슬롯 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `ApplicationHost`가 런타임 서비스의 정리까지 소유하게 만든 뒤, Avalonia GUI에서 기기별로 DeX를 시작·중지하고 단일창 슬롯 1~3을 띄울 수 있게 한다. 스펙 7절의 Phase 2 완료 조건인 "실사용 가능"에 도달한다.

**Architecture:** Phase 1이 남긴 수명주기 공백(정리 이관·`Interlocked`·설정 조율)을 먼저 닫는다. 그다음 identity별로 `DeviceRuntimeServiceSet`을 하나만 만들도록 보장하는 `DeviceRuntimeCoordinator`를 Core에 추가하고, GUI는 이것을 통해서만 런타임을 얻는다. 상태 표시는 `DeviceRuntimeSessionRegistry.Changed`를 유일한 관측 채널로 삼는다 — `DexOrchestrator`에는 public 이벤트가 없으므로 이 경로 외에는 실시간 구독이 불가능하다. 명령은 `ShellViewModel`이 아니라 `DeviceViewModel`에 두어 행에서 serial을 가져온다.

**Tech Stack:** .NET 8 (`global.json`이 SDK `8.0.130`, `rollForward: disable`로 고정), Avalonia `11.3.20`, CommunityToolkit.Mvvm `8.4.2`, xUnit `2.5.3`

**Spec:** `docs/superpowers/specs/2026-09-03-macos-gui-design.md`

**선행 계획:** `docs/superpowers/plans/2026-09-05-macos-gui-phase1.md`

## Global Constraints

- **Phase 2는 추가 전용이다.** 기존 TUI(`DexManager.Mac`)와 WinForms(`DexManager/`)의 **관측 가능한 동작**을 바꾸지 않는다(스펙 7절). 유일한 예외는 `docs/TODO.md`가 "Phase 2의 첫 작업"으로 지정한 정리 책임 이관(Task 3)과 설정 조율 연결(Task 4)이며, 둘 다 관측 가능한 동작이 동일함을 테스트로 고정한 뒤에 수행한다.
- **TUI의 단일 런타임 동작은 이번 Phase에서 바꾸지 않는다.** `InteractiveHost.GetOrCreateRuntime()`은 기기와 무관하게 `_activeRuntime` 하나를 재사용한다. 이것을 기기별 런타임으로 바꾸는 것은 관측 가능한 동작 변경이므로 범위 밖이다. Task 7의 `DeviceRuntimeCoordinator`는 **GUI만 소비한다.**
- **`DexManager`(WinForms 포크)에 어떤 의존도 만들지 않는다**(스펙 2.1절).
- **`DexManager.ViewModels`는 Avalonia에 의존하지 않는다**(스펙 4.2절). Avalonia 패키지 참조를 이 프로젝트에 추가하면 안 된다.
- **`IPlatformService`의 창 제어 8개는 구현하지 않는다**(스펙 4.5절 결정 1). 기존 스텁을 그대로 둔다. 따라서 `SingleWindowService.MainWindowHandle(slot)` 등 창 핸들 기반 API는 GUI에서 사용하지 않는다.
- **기기별 상태는 전역 `TargetSerial`을 암묵적으로 사용하지 않고 명시적 serial 또는 세션을 전달한다**(스펙 4.2절).
- **`ApplicationHost.SelectedSerial`은 진단 전용으로 유지한다**(`docs/TODO.md`의 Phase 2 설계 주의). DeX·단일창 명령은 이 값을 읽지 않는다. 명령 대상 serial은 명령이 달린 `DeviceViewModel` 자신의 `PrimarySerial`에서 가져온다.
- **Core 이벤트 구독은 ViewModel `Dispose`에서 반드시 해제한다**(스펙 4.3절). `Post` 실행 시점에도 `_disposed`를 다시 확인한다.
- **단일창 슬롯은 1~3 고정이다.** `SingleWindowService.Start()`가 범위 밖 값에 `ArgumentOutOfRangeException`을 던진다.
- **기존 테스트 베이스라인은 183개다.** 모든 Task 종료 시점에 전부 통과해야 한다.
  - `dotnet test DexManager.Tests/DexManager.Tests.csproj` — 122개
  - `dotnet test DexManager.ViewModels.Tests/DexManager.ViewModels.Tests.csproj` — 22개
  - `dotnet run --project DexManager.MultiDeviceTests -c Release` — 39개. 콘솔 실행형이며 `dotnet test`로는 실행되지 않는다. 마지막 줄이 `All multi-device foundation tests passed: 39`여야 한다.
  - 솔루션 전체는 `dotnet test DexManager.Mac.sln`으로 xUnit 144개를 한 번에 돌린다.
- **`dotnet` 실행 경로**: 이 기기에서 `dotnet`은 PATH에 없다. 각 명령 앞에 `export PATH="$PATH:$HOME/.dotnet"`를 두거나 `$HOME/.dotnet/dotnet`을 직접 호출한다.
- **커밋 트레일러**: 커밋 메시지 끝에 `Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>`를 넣는다.
- **커밋 컨벤션**: 이 저장소는 conventional commits를 쓴다 — `feat(core):`, `fix(viewmodels):`, `refactor(mac):`, `test(core):`, `docs:`, `chore:`. 제목은 소문자 명령형.
- **실기 검증은 대신 성공했다고 가정하지 않는다**(스펙 5.3절, `AGENTS.md`). 실제 Galaxy 기기의 DeX 시작·중지와 overlay 회수는 Task 13에서 사용자 확인 항목으로 명시한다.

## 배경: 왜 Task 1~6이 기능보다 먼저인가

`docs/TODO.md`의 "macOS GUI Phase 2 착수 전 선행 정리"는 Phase 1 최종 리뷰가 남긴 지적이다. 공통 원인은 Phase 1과 같다 — GUI가 두 번째 소비자가 되면서 "하나의 수명주기를 두 곳이 추적"하는 구조가 드러난다.

| # | 지적 | 이 계획의 대응 |
| :--- | :--- | :--- |
| 1 | 정리는 `InteractiveHost.ShutdownAsync`에만 있다 | Task 2 + Task 3 |
| 2 | `UpdateSettings`에 프로덕션 호출자가 없다 | Task 4 |
| 3 | `ApplicationHost._disposed`가 `Interlocked`가 아니다 | Task 1 |
| 4 | `DeviceListViewModel` 생성자의 구독-적용 순서 경합 | Task 5 |
| 5 | `DeviceViewModel`에 `IDisposable`이 없다 | Task 6 |
| 6 | `new ShellViewModel(...)`이 던지면 호스트가 샌다 | Task 11 |
| 7 | 워크플로 `paths:`에 `DexManager.Platform.Mac/**`가 없다 | Task 13 |

### 조사로 확정한 사실

계획 수립 중 코드에서 직접 확인한 것들이다. 구현자는 이 전제를 다시 검증할 필요가 없다.

- **`DexOrchestrator`에는 public event가 하나도 없다.** 상태 변화를 실시간으로 관측하려면 `DeviceRuntimeSessionRegistry.Changed`를 구독하는 수밖에 없다. 이 이벤트가 실어 나르는 `DeviceRuntimeSessionSnapshot`에는 이미 `Dex.IsRunning`과 슬롯별 `SingleWindows[i].IsRunning`이 들어 있으므로, **상태 노출을 위해 Core를 수정할 필요가 없다.**
- **`DeviceRuntimeServiceFactory.Create()`는 기기별로 캐시하지 않는다.** 호출할 때마다 새 `InstanceId`로 새 세트를 만들고 내부 `_created` 딕셔너리에 넣는다. 기기↔인스턴스 매핑은 `DeviceRuntimeSessionRegistry.BindServiceInstance(serial, instanceId)`가 따로 담당한다. 중복 `Create()`를 막는 책임은 호출자에게 있다 — Task 7이 이것을 맡는다.
- **`SingleWindowService.Dispose()`는 이미 멱등이다**(`Interlocked.Exchange(ref _disposed, 1)`). 따라서 Task 3에서 정리 경로가 둘로 남아도 이중 해제가 사고를 만들지 않는다.
- **`DeviceRegistrySnapshot.Generation`은 `long`으로 이미 존재한다.** Task 5의 경합 판별에 그대로 쓴다.
- **`InteractiveHost.GetOrCreateRuntime()`은 `_activeRuntime ??= _runtimeFactory.Create()` 한 줄이다.** 기기를 보지 않는다.
- **`InteractiveHost` 생성자와 `App.axaml.cs`의 `CreateMainWindow()`가 동일한 `ApplicationHost` 조립 코드를 중복 보유한다.** Task 11에서 공유 팩토리로 추출한다.

## File Structure

**수정:**

| 파일 | 책임 변화 |
| :--- | :--- |
| `DexManager.Core/Hosting/ApplicationHost.cs` | 수명주기에 런타임 정리 추가, `_disposed` 원자화, `UpdateSettings` 가드, 코디네이터 소유 |
| `DexManager.Core/Services/DeviceRuntimeServiceFactory.cs` | 생성된 인스턴스 열거 노출 |
| `DexManager.Mac/Hosting/InteractiveHost.cs` | 정리를 호스트에 위임, 설정 메뉴를 `UpdateSettings` 경유로 전환 |
| `DexManager.ViewModels/DeviceViewModel.cs` | 표시 전용 → 표시 + 런타임 상태 + DeX·단일창 명령, `IDisposable` |
| `DexManager.ViewModels/DeviceListViewModel.cs` | 세대 기반 경합 방어, 제거 시 자식 `Dispose` |
| `DexManager.ViewModels/ShellViewModel.cs` | 코디네이터를 자식 ViewModel에 전달 |
| `DexManager.Desktop/Views/MainWindow.axaml` | 기기 목록 → 목록 + DeX 제어 + 슬롯 제어 |
| `DexManager.Desktop/App.axaml.cs` | 조립을 공유 팩토리로 위임, 셸 생성 실패 시 호스트 정리 |
| `DexManager.Tests/ApplicationHostTests.cs` | 정리·가드 테스트 추가 |
| `DexManager.ViewModels.Tests/DeviceListViewModelTests.cs` | 경합·Dispose 전파 테스트 추가 |
| `DexManager.ViewModels.Tests/DeviceViewModelTests.cs` | 런타임 상태·명령 테스트 추가 |
| `.github/workflows/macos-portable.yml` | `paths:` 두 블록에 `DexManager.Platform.Mac/**` 추가 |
| `docs/superpowers/specs/2026-09-03-macos-gui-design.md` | 5.1절 베이스라인 갱신, 8.1절 항목 종결 기록 |
| `docs/TODO.md` | Phase 2 항목 체크, 실기 검증 미확인 항목 명시 |

**신규:**

| 파일 | 책임 |
| :--- | :--- |
| `DexManager.Core/Services/DeviceRuntimeCoordinator.cs` | identity별 런타임 1개 보장, 생성된 런타임 열거 |
| `DexManager.Platform.Mac/MacApplicationHostFactory.cs` | TUI/GUI 공용 `ApplicationHost` 조립 |
| `DexManager.ViewModels/SingleWindowSlotViewModel.cs` | 슬롯 1개의 상태와 시작·중지 명령 |
| `DexManager.Tests/DeviceRuntimeCoordinatorTests.cs` | 코디네이터 단위 테스트 |
| `DexManager.ViewModels.Tests/SingleWindowSlotViewModelTests.cs` | 슬롯 ViewModel 테스트 |
| `DexManager.ViewModels.Tests/FakeDeviceCommands.cs` | 명령 테스트용 런타임 경계 stub |

---

### Task 1: `ApplicationHost._disposed`를 `Interlocked`로 전환

**Files:**
- Modify: `DexManager.Core/Hosting/ApplicationHost.cs:24`, `:159-174`, `:285`, `:292-323`
- Test: `DexManager.Tests/ApplicationHostTests.cs`

**Interfaces:**
- Consumes: 없음 (첫 Task)
- Produces: `ApplicationHost.IsDisposed`의 의미가 "원자적으로 한 번만 true로 전이"로 강화된다. Task 3이 이 보장 위에서 정리를 한 번만 실행한다.

Task 2에서 창 닫기 경로와 프로세스 종료 경로가 동시에 `Dispose()`에 도달할 수 있다. 현재 `bool _disposed`는 검사와 대입이 분리돼 있어 두 스레드가 모두 정리 본문에 들어갈 수 있다. `DeviceMonitorService`가 이미 쓰는 패턴(`Interlocked.Exchange`)으로 맞춘다.

- [ ] **Step 1: 동시 Dispose가 정리를 한 번만 실행하는지 검증하는 실패 테스트를 쓴다**

`DexManager.Tests/ApplicationHostTests.cs` 끝에 추가한다.

```csharp
    [Fact]
    public void Dispose_ConcurrentCalls_DisposeKeyboardServiceOnce()
    {
        using var root = new TempHostRoot();
        var keyboard = new FakeKeyboardService();
        var host = CreateHost(root, keyboard);

        // 창 닫기 경로와 프로세스 종료 경로가 동시에 도달하는 상황이다.
        // bool 플래그는 검사와 대입 사이에 다른 스레드를 들여보낸다.
        var start = new ManualResetEventSlim(false);
        var threads = new Thread[8];
        for (var i = 0; i < threads.Length; i++)
        {
            threads[i] = new Thread(() =>
            {
                start.Wait();
                try { host.Dispose(); }
                catch (AggregateException) { }
            });
            threads[i].Start();
        }

        start.Set();
        foreach (var thread in threads) thread.Join();

        Assert.Equal(1, keyboard.DisposeCount);
    }
```

`FakeKeyboardService`에 `DisposeCount`가 없으면 `DexManager.Tests/FakePlatform/FakeKeyboardService.cs`에 추가한다.

```csharp
    public int DisposeCount { get; private set; }

    public void Dispose() => DisposeCount++;
```

- [ ] **Step 2: 테스트가 실패하는지 확인한다**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet test DexManager.Tests/DexManager.Tests.csproj \
  --filter "FullyQualifiedName~Dispose_ConcurrentCalls_DisposeKeyboardServiceOnce" -v n
```

Expected: FAIL. `DisposeCount`가 1보다 큰 값으로 관측된다. 경합이라 항상 재현되지는 않으므로 5회 반복해 최소 1회 실패를 확인한다. 5회 모두 통과하면 스레드 수를 32로 올려 다시 확인한다.

- [ ] **Step 3: `_disposed`를 `int`로 바꾸고 `Interlocked`로 전이시킨다**

`ApplicationHost.cs:24`

```csharp
    private int _disposed;
```

`ApplicationHost.cs:285`

```csharp
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;
```

`Start()` / `Stop()` / `UpdateSettings`가 읽는 자리는 `IsDisposed`를 쓰도록 바꾼다.

```csharp
    public void Start()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(ApplicationHost));
        DeviceMonitor.Start();
    }

    public void Stop()
    {
        if (IsDisposed) return;
        DeviceMonitor.Stop();
    }
```

`Dispose()` 진입부.

```csharp
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        var errors = new List<Exception>();
        // ... 이하 기존 본문 그대로
```

파일 상단에 `using System.Threading;`이 없으면 추가한다.

- [ ] **Step 4: 테스트가 통과하는지 확인한다**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet test DexManager.Tests/DexManager.Tests.csproj -v n
```

Expected: PASS, 123개(기존 122 + 신규 1).

- [ ] **Step 5: 커밋한다**

```bash
git add DexManager.Core/Hosting/ApplicationHost.cs \
        DexManager.Tests/ApplicationHostTests.cs \
        DexManager.Tests/FakePlatform/FakeKeyboardService.cs
git commit -m "$(cat <<'EOF'
fix(core): make ApplicationHost disposal atomic

창 닫기 경로와 프로세스 종료 경로가 Phase 2에서 함께 Dispose에 도달한다.
bool 플래그는 검사와 대입 사이에 두 번째 스레드를 정리 본문으로 들여보내
키보드 서비스를 두 번 해제한다. DeviceMonitorService가 이미 쓰는
Interlocked.Exchange 패턴으로 맞춘다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: `DeviceRuntimeServiceFactory`가 만든 런타임 열거 노출

**Files:**
- Modify: `DexManager.Core/Services/DeviceRuntimeServiceFactory.cs:22-23`, `:120`, `:130`
- Test: `DexManager.Tests/DeviceRuntimeServiceFactoryTests.cs` (없으면 생성)

**Interfaces:**
- Consumes: 없음
- Produces: `IReadOnlyList<DeviceRuntimeServiceSet> DeviceRuntimeServiceFactory.CreatedInstances { get; }` — 생성 순서대로의 스냅샷. Task 3이 정리 대상을 여기서 가져온다.

`ApplicationHost`가 런타임 정리를 맡으려면 어떤 런타임이 만들어졌는지 알아야 한다. 팩토리는 이미 `_created` 딕셔너리에 전부 들고 있으나 밖에서 볼 수 없다. TUI가 만든 것이든 GUI가 만든 것이든 한 곳에서 회수하려면 이 열거가 필요하다.

- [ ] **Step 1: 실패 테스트를 쓴다**

`DexManager.Tests/DeviceRuntimeServiceFactoryTests.cs`

```csharp
using DexManager.Hosting;
using DexManager.Services;

namespace DexManager.Tests;

public class DeviceRuntimeServiceFactoryTests
{
    [Fact]
    public void CreatedInstances_ReturnsEverySetInCreationOrder()
    {
        using var root = new TempHostRoot();
        using var host = root.CreateHost();
        var factory = host.RuntimeFactory;

        var first = factory.Create();
        var second = factory.Create();

        var created = factory.CreatedInstances;

        Assert.Equal(2, created.Count);
        Assert.Same(first, created[0]);
        Assert.Same(second, created[1]);
    }

    [Fact]
    public void CreatedInstances_IsSnapshotAndDoesNotObserveLaterCreations()
    {
        using var root = new TempHostRoot();
        using var host = root.CreateHost();
        var factory = host.RuntimeFactory;

        factory.Create();
        var snapshot = factory.CreatedInstances;
        factory.Create();

        // 정리 루프가 도는 도중 새 런타임이 생겨도 컬렉션이 변경되면 안 된다.
        Assert.Single(snapshot);
    }
}
```

`DexManager.Tests`에 `TempHostRoot`가 없으면 `DexManager.ViewModels.Tests/TempHostRoot.cs`와 동일한 내용으로 `DexManager.Tests/TempHostRoot.cs`를 만든다. 네임스페이스만 `DexManager.Tests`로 바꾼다.

- [ ] **Step 2: 테스트가 실패하는지 확인한다**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet test DexManager.Tests/DexManager.Tests.csproj \
  --filter "FullyQualifiedName~DeviceRuntimeServiceFactoryTests" -v n
```

Expected: 컴파일 실패 — `'DeviceRuntimeServiceFactory' does not contain a definition for 'CreatedInstances'`

- [ ] **Step 3: 열거를 노출한다**

`DeviceRuntimeServiceFactory.cs`의 `Find` 메서드 바로 아래에 추가한다.

```csharp
        /// <summary>
        /// 이 팩토리가 지금까지 만든 런타임 세트를 생성 순서대로 돌려준다.
        /// 호출 시점의 스냅샷이므로 순회 중 새 런타임이 생겨도 영향이 없다.
        /// 정리 경로가 회수 대상을 찾는 데 쓴다.
        /// </summary>
        public IReadOnlyList<DeviceRuntimeServiceSet> CreatedInstances
        {
            get
            {
                lock (_sync) return _created.Values.ToList();
            }
        }
```

`_created`가 `Dictionary<Guid, ...>`이므로 삽입 순서가 보장되지 않는 것처럼 보이나, .NET의 `Dictionary`는 제거가 없으면 삽입 순서로 열거한다. 이 팩토리는 제거를 하지 않으므로 순서가 안정적이다. 그럼에도 계약을 코드로 고정하기 위해 삽입 순서를 별도 리스트로 관리한다.

`_created` 선언 옆에 추가한다.

```csharp
        private readonly List<DeviceRuntimeServiceSet> _createdOrder =
            new List<DeviceRuntimeServiceSet>();
```

`Create()`의 등록 지점(`DeviceRuntimeServiceFactory.cs:120`)을 바꾼다.

```csharp
            lock (_sync)
            {
                _created.Add(services.InstanceId, services);
                _createdOrder.Add(services);
            }
```

그리고 `CreatedInstances`가 이 리스트를 복사하게 한다.

```csharp
        public IReadOnlyList<DeviceRuntimeServiceSet> CreatedInstances
        {
            get
            {
                lock (_sync) return _createdOrder.ToList();
            }
        }
```

파일 상단에 `using System.Linq;`가 없으면 추가한다.

- [ ] **Step 4: 테스트가 통과하는지 확인한다**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet test DexManager.Tests/DexManager.Tests.csproj -v n
```

Expected: PASS, 125개.

- [ ] **Step 5: 커밋한다**

```bash
git add DexManager.Core/Services/DeviceRuntimeServiceFactory.cs \
        DexManager.Tests/DeviceRuntimeServiceFactoryTests.cs \
        DexManager.Tests/TempHostRoot.cs
git commit -m "$(cat <<'EOF'
feat(core): expose the runtime sets a factory has created

정리 책임을 ApplicationHost로 옮기려면 어떤 런타임이 만들어졌는지
호스트가 알아야 한다. 팩토리는 이미 전부 들고 있으나 밖에서 볼 수 없었다.
순회 중 생성이 끼어들어도 안전하도록 스냅샷을 돌려준다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: 런타임 정리 책임을 `ApplicationHost`로 이관

**Files:**
- Modify: `DexManager.Core/Hosting/ApplicationHost.cs:292-323`
- Modify: `DexManager.Mac/Hosting/InteractiveHost.cs:828-971`
- Test: `DexManager.Tests/ApplicationHostTests.cs`

**Interfaces:**
- Consumes: `DeviceRuntimeServiceFactory.CreatedInstances`(Task 2), `ApplicationHost.IsDisposed`(Task 1)
- Produces:
  - `Task<IReadOnlyList<Exception>> ApplicationHost.ShutdownAsync(string fallbackSerial, string fallbackIdentity)` — 정리를 수행하고 수집된 예외를 돌려준다. 던지지 않는다. 멱등이며 두 번째 호출부터는 빈 목록을 돌려준다.
  - `ApplicationHost.Dispose()`는 내부적으로 `ShutdownAsync(null, null)`를 실행한 뒤 예외가 있으면 `AggregateException`을 던진다(기존 계약 유지).

`docs/TODO.md`가 "Phase 2의 첫 작업"으로 지정한 항목이다. 현재 `Dex.ShutdownAsync`, `SingleWindows.StopAll()`, `HasDeferredDisplayCleanup` 확인, `DisposeRuntimeServices` — `AGENTS.md`의 프로세스·overlay 불변식을 강제하는 코드 전부가 `InteractiveHost.ShutdownAsync`에만 있다. GUI가 이것을 복제하면 두 벌이 갈라진다. **옮기는 쪽을 먼저 한다.**

**관측 가능한 동작 보존:** TUI의 콘솔 출력(`Shutting down...`, 오류별 `Error during shutdown:`, 성공/경고 마무리 문구)은 `InteractiveHost`에 그대로 남긴다. `ApplicationHost`는 출력하지 않고 예외 목록만 돌려준다. 순서도 현재 `InteractiveHost.ShutdownAsync`와 동일하게 유지한다.

**UI 스레드 교착 주의:** `Dispose()`는 동기 메서드인데 정리는 비동기다. GUI의 `App.OnExit`은 UI 스레드에서 실행되므로 `ShutdownAsync().GetAwaiter().GetResult()`를 그대로 부르면 Core의 `await`가 UI `SynchronizationContext`를 잡아 교착할 수 있다. `Task.Run`으로 스레드 풀에 넘겨 컨텍스트를 끊는다. TUI는 `SynchronizationContext`가 없어 지금까지 문제가 없었을 뿐이다.

- [ ] **Step 1: 호스트가 런타임을 정리하는지 검증하는 실패 테스트를 쓴다**

`DexManager.Tests/ApplicationHostTests.cs` 끝에 추가한다.

```csharp
    [Fact]
    public async Task ShutdownAsync_DisposesRuntimeServicesCreatedByTheFactory()
    {
        using var root = new TempHostRoot();
        var host = root.CreateHost();
        var runtime = host.RuntimeFactory.Create();

        var errors = await host.ShutdownAsync(null, null);

        Assert.Empty(errors);
        // SingleWindowService.Dispose는 멱등이며 두 번째 호출이 조용히 반환한다.
        // 이미 해제됐다면 StopAll이 새 프로세스를 만들지 않는다.
        Assert.Equal(0, runtime.SingleWindows.RunningCount);
        Assert.True(host.IsDisposed);
    }

    [Fact]
    public async Task ShutdownAsync_IsIdempotentAndReportsNoErrorsOnSecondCall()
    {
        using var root = new TempHostRoot();
        var host = root.CreateHost();
        host.RuntimeFactory.Create();

        var first = await host.ShutdownAsync(null, null);
        var second = await host.ShutdownAsync(null, null);

        Assert.Empty(first);
        Assert.Empty(second);
    }

    [Fact]
    public void Dispose_AfterShutdownAsync_DoesNotThrow()
    {
        using var root = new TempHostRoot();
        var host = root.CreateHost();
        host.RuntimeFactory.Create();

        host.ShutdownAsync(null, null).GetAwaiter().GetResult();

        // 종료 훅이 ShutdownAsync를 부른 뒤 using이 Dispose를 또 부른다.
        host.Dispose();
    }
```

- [ ] **Step 2: 테스트가 실패하는지 확인한다**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet test DexManager.Tests/DexManager.Tests.csproj \
  --filter "FullyQualifiedName~ShutdownAsync" -v n
```

Expected: 컴파일 실패 — `'ApplicationHost' does not contain a definition for 'ShutdownAsync'`

- [ ] **Step 3: `ApplicationHost`에 정리를 구현한다**

`ApplicationHost.cs`의 `Dispose()`를 통째로 아래로 교체한다.

```csharp
    private int _shutdownStarted;

    /// <summary>
    /// 호스트가 소유한 런타임과 서비스를 정리한다. 멱등하다 — 두 번째
    /// 호출부터는 아무 일도 하지 않고 빈 목록을 돌려준다.
    /// 예외를 던지지 않고 수집해 돌려주므로, 호출자가 자기 방식으로
    /// 보고할 수 있다. 앞선 실패가 뒤의 정리를 막지 않는다.
    /// </summary>
    /// <param name="fallbackSerial">
    /// DeX 세션이 자기 serial을 모를 때 쓸 대체값. 없으면 <c>null</c>.
    /// </param>
    /// <param name="fallbackIdentity">
    /// 같은 용도의 물리 기기 identity 대체값. 없으면 <c>null</c>.
    /// </param>
    public async Task<IReadOnlyList<Exception>> ShutdownAsync(
        string fallbackSerial,
        string fallbackIdentity)
    {
        if (Interlocked.Exchange(ref _shutdownStarted, 1) != 0)
            return Array.Empty<Exception>();

        var errors = new List<Exception>();

        try
        {
            DeviceMonitor?.Stop();
        }
        catch (Exception ex)
        {
            errors.Add(ex);
        }

        foreach (var runtime in RuntimeFactory.CreatedInstances)
        {
            await ShutdownRuntimeAsync(
                runtime,
                fallbackSerial,
                fallbackIdentity,
                errors);
        }

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

        Interlocked.Exchange(ref _disposed, 1);
        return errors;
    }

    /// <summary>
    /// 런타임 하나를 정리한다. 순서는 TUI가 검증해 온 순서를 그대로 따른다 —
    /// 먼저 모든 서비스에 종료를 알려 새 작업을 막고, 단일창을 내리고,
    /// DeX overlay를 회수한 뒤, 마지막에 서비스를 해제한다.
    /// </summary>
    private static async Task ShutdownRuntimeAsync(
        DeviceRuntimeServiceSet runtime,
        string fallbackSerial,
        string fallbackIdentity,
        ICollection<Exception> errors)
    {
        if (runtime == null) return;

        try
        {
            runtime.FileTransfers.RequestShutdown();
            runtime.PhoneTransfers.RequestShutdown();
            runtime.CompanionGuardian.RequestShutdown();
            runtime.ScreenOff.RequestShutdown();
            runtime.SingleWindows.RequestShutdown();
            runtime.Dex.RequestShutdown();
        }
        catch (Exception ex)
        {
            errors.Add(ex);
        }

        try
        {
            runtime.SingleWindows.StopAll();
        }
        catch (Exception ex)
        {
            errors.Add(ex);
        }

        try
        {
            var serial = runtime.Dex.CurrentSession?.Serial ?? fallbackSerial;
            var identity = runtime.Dex.CurrentSession?.DeviceIdentity
                ?? fallbackIdentity
                ?? string.Empty;

            await runtime.Dex.ShutdownAsync(serial, identity);

            if (runtime.Dex.HasDeferredDisplayCleanup)
            {
                errors.Add(new InvalidOperationException(
                    "DeX display cleanup was deferred because the " +
                    "target device was unavailable."));
            }
        }
        catch (Exception ex)
        {
            errors.Add(ex);
        }

        var disposables = new IDisposable[]
        {
            runtime.SingleWindows,
            runtime.Scrcpy,
            runtime.ScreenOff,
            runtime.PhoneTransfers,
            runtime.CompanionGuardian,
            runtime.FileTransfers
        };

        foreach (var disposable in disposables)
        {
            try
            {
                disposable.Dispose();
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
        }
    }

    /// <summary>
    /// 호스트가 소유한 서비스를 정리한다. 멱등하다.
    /// 정리 중 발생한 예외는 모두 수집한 뒤 <see cref="AggregateException"/>으로
    /// 던진다 — 앞선 실패가 뒤의 정리를 막지 않기 위함이다.
    /// </summary>
    public void Dispose()
    {
        // ShutdownAsync 안의 await가 호출자의 SynchronizationContext를 잡으면
        // UI 스레드에서 부를 때 교착한다. 스레드 풀로 넘겨 컨텍스트를 끊는다.
        var errors = Task
            .Run(() => ShutdownAsync(null, null))
            .GetAwaiter()
            .GetResult();

        if (errors.Count > 0)
        {
            throw new AggregateException(
                "ApplicationHost disposal did not complete cleanly.",
                errors);
        }
    }
```

`ApplicationHost.cs` 상단에 필요한 `using`을 확인한다.

```csharp
using System.Threading;
using System.Threading.Tasks;
```

- [ ] **Step 4: 테스트가 통과하는지 확인한다**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet test DexManager.Tests/DexManager.Tests.csproj -v n
```

Expected: PASS, 128개. 특히 기존 `Dispose_IsIdempotent`와 `Dispose_DisposesKeyboardServiceAndStopsDeviceMonitor`가 그대로 통과해야 한다.

- [ ] **Step 5: `InteractiveHost`가 호스트에 위임하게 바꾼다**

`InteractiveHost.cs:828-928`의 `ShutdownAsync`를 아래로 교체한다. 콘솔 출력과 순서는 그대로 두고, 정리 본문만 호스트에 넘긴다.

```csharp
        public async Task ShutdownAsync()
        {
            if (Interlocked.Exchange(ref _shutdownStarted, 1) != 0) return;

            _isRunning = false;
            var device = GetSelectedDevice();
            var fallbackSerial = GetPrimarySerial(device) ??
                _selectedDeviceSerial;
            var fallbackIdentity = device?.Identity ??
                _activeRuntime?.Dex.CurrentSession?.DeviceIdentity ??
                _selectedDeviceIdentity ??
                string.Empty;

            AnsiConsole.Info("Shutting down DX Manager and cleaning up active sessions...");

            var errors = await _host.ShutdownAsync(
                fallbackSerial,
                fallbackIdentity);

            foreach (var error in errors)
            {
                AnsiConsole.Error($"Error during shutdown: {error.Message}");
            }

            if (errors.Count == 0)
            {
                AnsiConsole.Success("DX Manager stopped cleanly. Goodbye!");
            }
            else
            {
                AnsiConsole.Warning("DX Manager stopped, but one or more cleanup steps could not be confirmed.");
            }
        }
```

`DisposeRuntimeServices` 메서드(`:943-971`)와 `_runtimeServicesDisposed` 필드(`:41`)를 삭제한다. 호출 지점이 사라졌다.

`Dispose()`(`:935-941`)는 그대로 둔다 — `Shutdown()`이 `ShutdownAsync`를 돌리고, 그 뒤 `_host.Dispose()`가 멱등하게 반환한다.

- [ ] **Step 6: TUI 종료 동작이 보존됐는지 확인한다**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet build DexManager.Mac.sln --nologo
dotnet test DexManager.Mac.sln --nologo
dotnet run --project DexManager.MultiDeviceTests -c Release 2>&1 | tail -3
```

Expected: 빌드 성공, xUnit 150개 통과, 마지막 줄 `All multi-device foundation tests passed: 39`.

기기를 연결한 상태에서 진단 경로가 정상 종료되는지도 확인한다.

```bash
cd DexManager.Mac/bin/Debug/net8.0 && ./DXManager.Mac --diag 2>&1 | tail -3
```

Expected: 마지막 줄이 `DX Manager stopped cleanly. Goodbye!`

- [ ] **Step 7: 커밋한다**

```bash
git add DexManager.Core/Hosting/ApplicationHost.cs \
        DexManager.Mac/Hosting/InteractiveHost.cs \
        DexManager.Tests/ApplicationHostTests.cs
git commit -m "$(cat <<'EOF'
refactor(core): move runtime teardown into ApplicationHost

DeX overlay 회수와 단일창 종료, 런타임 서비스 해제가 InteractiveHost에만
있었다. GUI가 두 번째 소비자가 되면 이 코드가 복제되고 두 벌이 갈라진다.
AGENTS.md의 프로세스·overlay 불변식을 강제하는 경로이므로 옮긴다.

ShutdownAsync는 예외를 던지지 않고 수집해 돌려준다. TUI는 기존 콘솔 출력과
순서를 그대로 유지한 채 본문만 위임한다. Dispose는 Task.Run으로 정리를
스레드 풀에 넘긴다 — UI 스레드에서 부를 때 await가 SynchronizationContext를
잡아 교착하는 것을 막는다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: `UpdateSettings`에 가드와 프로덕션 호출자를 붙인다

**Files:**
- Modify: `DexManager.Core/Hosting/ApplicationHost.cs:176-191`
- Modify: `DexManager.Mac/Hosting/InteractiveHost.cs:740-791`
- Test: `DexManager.Tests/ApplicationHostTests.cs`

**Interfaces:**
- Consumes: `ApplicationHost.IsDisposed`(Task 1)
- Produces: `UpdateSettings`가 해제된 호스트에서 `ObjectDisposedException`을 던진다. Task 10의 슬롯 설정 저장이 이 계약 위에 선다.

스펙 8.1절 공백 5는 "`Settings`가 저장 조율 없이 공유된다"였다. Phase 1이 `UpdateSettings`를 만들었으나 **프로덕션 호출자가 없다.** TUI 설정 메뉴가 여전히 `_settings`를 직접 수정하고 잠금 밖에서 저장한다 — 스펙이 지목한 바로 그 메뉴다. GUI가 두 번째 소비자가 되기 전에 닫는다.

**잠금 안에서 입력을 기다리지 않는다.** 현재 메뉴는 `Console.ReadLine()`이 `switch` 안에 있다. 그대로 `UpdateSettings`로 감싸면 사용자가 프롬프트를 보는 동안 `_settingsLock`을 잡고 있게 되어, GUI 소비자가 그 시간만큼 막힌다. 입력을 먼저 받아 mutation으로 포장한 뒤 넘긴다.

- [ ] **Step 1: 실패 테스트를 쓴다**

`DexManager.Tests/ApplicationHostTests.cs` 끝에 추가한다.

```csharp
    [Fact]
    public void UpdateSettings_AfterDispose_Throws()
    {
        using var root = new TempHostRoot();
        var host = root.CreateHost();
        host.Dispose();

        // Start/Stop이 세운 패턴과 같아야 한다 — 해제된 호스트에 쓰기를
        // 허용하면 디스크에 남는 마지막 값이 종료 순서에 좌우된다.
        Assert.Throws<ObjectDisposedException>(
            () => host.UpdateSettings(s => s.VirtualDisplay.Width = 1280));
    }
```

- [ ] **Step 2: 테스트가 실패하는지 확인한다**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet test DexManager.Tests/DexManager.Tests.csproj \
  --filter "FullyQualifiedName~UpdateSettings_AfterDispose_Throws" -v n
```

Expected: FAIL — 예외가 던져지지 않고 통과한다.

- [ ] **Step 3: 가드를 추가하고 정규화 부작용을 문서화한다**

`ApplicationHost.cs`의 `UpdateSettings`를 교체한다.

```csharp
    /// <summary>
    /// 설정을 수정하고 저장한다. 수정과 저장이 한 잠금 안에서 일어나므로
    /// 소비자 둘이 동시에 읽기-수정-쓰기를 해도 갱신이 유실되지 않는다.
    /// 설정을 바꿀 때는 <see cref="Settings"/>를 직접 수정하지 말고
    /// 이 메서드를 쓴다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <paramref name="mutate"/>가 건드리지 않은 필드도 값이 바뀔 수 있다.
    /// 저장 경로가 <c>Save</c> → <c>SaveCore</c> → <c>EnsureDefaults()</c>로
    /// 이어지며, <c>EnsureDefaults</c>는 호출자가 들고 있는 살아있는
    /// <see cref="Settings"/> 객체 자체를 정규화한다. 폼을 이 객체에
    /// 양방향 바인딩하면 저장 직후 화면 값이 정규화된 값으로 바뀐다.
    /// </para>
    /// <para>
    /// 잠금 안에서 사용자 입력을 기다리지 않는다. 입력을 먼저 받아
    /// mutation으로 포장한 뒤 넘긴다 — 그러지 않으면 다른 소비자가
    /// 프롬프트가 닫힐 때까지 막힌다.
    /// </para>
    /// </remarks>
    /// <exception cref="ObjectDisposedException">
    /// 호스트가 이미 해제된 경우.
    /// </exception>
    public void UpdateSettings(Action<AppSettings> mutate)
    {
        if (mutate == null) throw new ArgumentNullException(nameof(mutate));
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(ApplicationHost));

        lock (_settingsLock)
        {
            mutate(Settings);
            SettingsService.Save(Settings);
        }
    }
```

- [ ] **Step 4: 테스트가 통과하는지 확인한다**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet test DexManager.Tests/DexManager.Tests.csproj -v n
```

Expected: PASS, 129개.

- [ ] **Step 5: TUI 설정 메뉴를 `UpdateSettings` 경유로 바꾼다**

`InteractiveHost.cs:753-790`을 교체한다. 화면 출력과 프롬프트 문구는 한 글자도 바꾸지 않는다.

```csharp
            Console.Write("\nEnter setting # to modify (or Enter to go back): ");
            var opt = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(opt)) return;

            // 입력을 먼저 받아 mutation으로 포장한다. UpdateSettings는
            // 잠금을 잡으므로 그 안에서 사용자 입력을 기다리면 다른
            // 소비자가 프롬프트가 닫힐 때까지 막힌다.
            Action<AppSettings> mutate = null;
            switch (opt)
            {
                case "1":
                    Console.Write("Enter Width (e.g. 1920, 2560): ");
                    if (int.TryParse(Console.ReadLine(), out var w))
                        mutate = s => s.VirtualDisplay.Width = w;
                    break;
                case "2":
                    Console.Write("Enter Height (e.g. 1080, 1440): ");
                    if (int.TryParse(Console.ReadLine(), out var h))
                        mutate = s => s.VirtualDisplay.Height = h;
                    break;
                case "3":
                    Console.Write("Enter DPI (e.g. 160, 200, 240): ");
                    if (int.TryParse(Console.ReadLine(), out var dpi))
                        mutate = s => s.VirtualDisplay.Dpi = dpi;
                    break;
                case "4":
                    Console.Write("Enter Bitrate (e.g. 16M, 24M, 32M): ");
                    var br = Console.ReadLine()?.Trim();
                    if (!string.IsNullOrWhiteSpace(br))
                        mutate = s => s.Scrcpy.BitRate = br;
                    break;
                case "5":
                    Console.Write("Enter Max FPS (e.g. 60, 120): ");
                    if (int.TryParse(Console.ReadLine(), out var fps))
                        mutate = s => s.Scrcpy.MaxFps = fps;
                    break;
                case "6":
                    mutate = s => s.Scrcpy.TurnScreenOff = !s.Scrcpy.TurnScreenOff;
                    break;
                case "7":
                    mutate = s => s.Scrcpy.StayAwake = !s.Scrcpy.StayAwake;
                    break;
            }

            // 기존 동작 보존: 인식되지 않은 항목이나 잘못된 입력에도
            // 저장이 일어나고 같은 문구가 나왔다. 정규화 부작용을 위해
            // 저장 자체는 유지한다.
            _host.UpdateSettings(mutate ?? (_ => { }));
            AnsiConsole.Success("Settings updated and saved.");
            Thread.Sleep(800);
```

`_settingsService.Save(_settings)` 호출은 사라진다. `InteractiveHost.cs` 상단에 `using DexManager.Models;`가 이미 있으므로 `AppSettings` 참조는 추가 `using` 없이 해결된다.

- [ ] **Step 6: 전체 검증**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet test DexManager.Mac.sln --nologo
dotnet run --project DexManager.MultiDeviceTests -c Release 2>&1 | tail -3
```

Expected: xUnit 151개 통과, `All multi-device foundation tests passed: 39`.

- [ ] **Step 7: 커밋한다**

```bash
git add DexManager.Core/Hosting/ApplicationHost.cs \
        DexManager.Mac/Hosting/InteractiveHost.cs \
        DexManager.Tests/ApplicationHostTests.cs
git commit -m "$(cat <<'EOF'
fix(core): route the TUI settings menu through UpdateSettings

스펙 8.1절 공백 5가 지목한 메뉴다. Phase 1이 UpdateSettings를 만들었으나
프로덕션 호출자가 없어 공백이 절반만 닫혀 있었다. 메뉴는 여전히 Settings를
직접 수정하고 잠금 밖에서 저장했다.

입력은 잠금 밖에서 먼저 받아 mutation으로 포장한다. 그러지 않으면 사용자가
프롬프트를 보는 동안 두 번째 소비자가 막힌다. 해제된 호스트에 쓰기를
막는 가드도 Start/Stop과 같은 패턴으로 추가한다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 5: `DeviceListViewModel` 생성자의 구독-적용 경합을 닫는다

**Files:**
- Modify: `DexManager.ViewModels/DeviceListViewModel.cs:17-26`, `:36-44`, `:46-48`
- Test: `DexManager.ViewModels.Tests/DeviceListViewModelTests.cs`

**Interfaces:**
- Consumes: `DeviceRegistrySnapshot.Generation`(기존 `long` 속성)
- Produces: `DeviceListViewModel`이 자기가 반영한 마지막 세대보다 낮은 스냅샷을 무시한다. Task 6·8이 이 위에서 자식 ViewModel을 안전하게 만든다.

생성자는 `SnapshotChanged`를 먼저 구독하고 그다음 `Apply(_registry.Current)`를 부른다. 그 사이에 감시 스레드가 `Reconcile`을 돌리면 새 스냅샷이 디스패처 큐에 들어가고, 생성자의 `Apply`가 나중에 실행되면서 **오래된 스냅샷이 이긴다.** 기기가 목록에서 사라지거나 유령으로 남는다.

- [ ] **Step 1: 실패 테스트를 쓴다**

`DexManager.ViewModels.Tests/DeviceListViewModelTests.cs` 끝에 추가한다.

```csharp
    [Fact]
    public void StaleSnapshotArrivingAfterConstruction_DoesNotWin()
    {
        var registry = new PhysicalDeviceRegistry();
        registry.Reconcile(new[] { Device("phone-a", "Galaxy A", "AAA", DeviceTransportKind.Usb) });
        var stale = registry.Current;

        registry.Reconcile(new[]
        {
            Device("phone-a", "Galaxy A", "AAA", DeviceTransportKind.Usb),
            Device("phone-b", "Galaxy B", "BBB", DeviceTransportKind.Usb)
        });

        var dispatcher = new ImmediateUiDispatcher();
        using var vm = new DeviceListViewModel(registry, dispatcher);

        // 구독과 생성자 Apply 사이에 끼어든 이벤트를 재현한다.
        // 세대가 낮으므로 무시되어야 한다.
        registry.RaiseSnapshotChangedForTest(stale);

        Assert.Equal(2, vm.Devices.Count);
    }
```

`PhysicalDeviceRegistry`에 테스트용 이벤트 발생 수단이 없다면 stale 스냅샷을 직접 만들어 넣는 대신, `Apply`를 세대로 보호했는지를 관측 가능한 경로로 검증한다. 아래 대안 테스트를 쓴다(레지스트리를 수정하지 않아도 된다).

```csharp
    [Fact]
    public void ConstructionAppliesTheCurrentGenerationAndIgnoresOlderOnes()
    {
        var registry = new PhysicalDeviceRegistry();
        registry.Reconcile(new[]
        {
            Device("phone-a", "Galaxy A", "AAA", DeviceTransportKind.Usb),
            Device("phone-b", "Galaxy B", "BBB", DeviceTransportKind.Usb)
        });

        var dispatcher = new QueueingUiDispatcher();
        using var vm = new DeviceListViewModel(registry, dispatcher);

        // 생성자가 최신 스냅샷을 즉시 반영했다.
        Assert.Equal(2, vm.Devices.Count);

        // 한 기기가 빠진 새 스냅샷이 오면 세대가 높으므로 반영된다.
        registry.Reconcile(new[] { Device("phone-a", "Galaxy A", "AAA", DeviceTransportKind.Usb) });
        dispatcher.Drain();

        Assert.Single(vm.Devices);

        // 큐에 남아 있던 예전 세대가 뒤늦게 실행돼도 되돌리지 않는다.
        dispatcher.Drain();
        Assert.Single(vm.Devices);
    }
```

- [ ] **Step 2: 테스트가 실패하는지 확인한다**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet test DexManager.ViewModels.Tests/DexManager.ViewModels.Tests.csproj \
  --filter "FullyQualifiedName~Generation" -v n
```

Expected: FAIL 또는 컴파일 실패.

- [ ] **Step 3: 세대 가드를 구현한다**

`DeviceListViewModel.cs`에 필드를 추가한다.

```csharp
    private long _appliedGeneration = -1;
```

`Apply`의 첫머리를 바꾼다.

```csharp
    private void Apply(DeviceRegistrySnapshot snapshot)
    {
        if (_disposed) return;

        // 구독과 생성자의 첫 Apply 사이에 감시 스레드가 Reconcile을 돌리면
        // 새 스냅샷이 먼저 큐에 들어가고 생성자의 오래된 스냅샷이 나중에
        // 실행된다. 세대가 뒤로 가는 반영은 버린다.
        var generation = snapshot?.Generation ?? 0;
        if (generation <= _appliedGeneration) return;
        _appliedGeneration = generation;

        var incoming = snapshot?.Devices ?? new List<PhysicalDeviceInfo>();
        // ... 이하 기존 본문 그대로
```

생성자에서 구독보다 `Apply`를 먼저 하도록 순서를 바꾸지 않는다 — 그러면 그 사이의 변경을 통째로 놓친다. 세대 비교가 올바른 해법이다.

- [ ] **Step 4: 테스트가 통과하는지 확인한다**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet test DexManager.ViewModels.Tests/DexManager.ViewModels.Tests.csproj -v n
```

Expected: PASS, 23개.

- [ ] **Step 5: 커밋한다**

```bash
git add DexManager.ViewModels/DeviceListViewModel.cs \
        DexManager.ViewModels.Tests/DeviceListViewModelTests.cs
git commit -m "$(cat <<'EOF'
fix(viewmodels): ignore device snapshots older than the applied one

생성자는 SnapshotChanged를 먼저 구독하고 그다음 현재 스냅샷을 반영한다.
그 사이에 감시 스레드가 Reconcile을 돌리면 새 스냅샷이 큐에 먼저 들어가고
생성자의 오래된 스냅샷이 나중에 실행되면서 이긴다. 기기가 목록에서
사라지거나 유령으로 남는다.

DeviceRegistrySnapshot.Generation이 이미 있으므로 뒤로 가는 반영만 버린다.
구독보다 Apply를 먼저 하는 방식은 그 사이의 변경을 통째로 놓친다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 6: `DeviceViewModel`에 `IDisposable`을 붙이고 목록이 해제하게 한다

**Files:**
- Modify: `DexManager.ViewModels/DeviceViewModel.cs`
- Modify: `DexManager.ViewModels/DeviceListViewModel.cs:52-58`, `:99-104`
- Test: `DexManager.ViewModels.Tests/DeviceViewModelTests.cs`, `DeviceListViewModelTests.cs`

**Interfaces:**
- Consumes: Task 5의 세대 가드
- Produces: `DeviceViewModel : ObservableObject, IDisposable`. `DeviceListViewModel`이 목록에서 뽑을 때와 자신이 해제될 때 자식을 `Dispose`한다. Task 8이 여기에 레지스트리 구독을 단다.

`docs/TODO.md`의 Phase 2 설계 주의: "행에 세션 상태 구독을 다는 순간 `Apply`의 `Devices.RemoveAt(i)`가 기기를 뽑을 때마다 하나씩 샌다. **지금은 공짜다.**" 구독을 달기 전에 해제 경로를 먼저 만든다.

- [ ] **Step 1: 실패 테스트를 쓴다**

`DexManager.ViewModels.Tests/DeviceListViewModelTests.cs` 끝에 추가한다.

```csharp
    [Fact]
    public void RemovingADeviceDisposesItsViewModel()
    {
        var registry = new PhysicalDeviceRegistry();
        registry.Reconcile(new[]
        {
            Device("phone-a", "Galaxy A", "AAA", DeviceTransportKind.Usb),
            Device("phone-b", "Galaxy B", "BBB", DeviceTransportKind.Usb)
        });

        var dispatcher = new ImmediateUiDispatcher();
        using var vm = new DeviceListViewModel(registry, dispatcher);
        var removed = vm.Devices.Single(d => d.Identity == "phone-b");

        registry.Reconcile(new[] { Device("phone-a", "Galaxy A", "AAA", DeviceTransportKind.Usb) });

        Assert.True(removed.IsDisposed);
    }

    [Fact]
    public void DisposingTheListDisposesEveryRow()
    {
        var registry = new PhysicalDeviceRegistry();
        registry.Reconcile(new[] { Device("phone-a", "Galaxy A", "AAA", DeviceTransportKind.Usb) });

        var dispatcher = new ImmediateUiDispatcher();
        var vm = new DeviceListViewModel(registry, dispatcher);
        var row = vm.Devices.Single();

        vm.Dispose();

        Assert.True(row.IsDisposed);
    }
```

- [ ] **Step 2: 테스트가 실패하는지 확인한다**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet test DexManager.ViewModels.Tests/DexManager.ViewModels.Tests.csproj \
  --filter "FullyQualifiedName~Dispose" -v n
```

Expected: 컴파일 실패 — `'DeviceViewModel' does not contain a definition for 'IsDisposed'`

- [ ] **Step 3: `DeviceViewModel`에 해제를 구현한다**

`DeviceViewModel.cs`의 클래스 선언과 끝부분을 바꾼다.

```csharp
public sealed partial class DeviceViewModel : ObservableObject, IDisposable
{
    private bool _disposed;
```

`Update` 아래에 추가한다.

```csharp
    /// <summary>이 행이 이미 해제되었는지 여부.</summary>
    public bool IsDisposed => _disposed;

    /// <summary>
    /// 이 행이 건 구독을 해제한다. 멱등하다.
    /// 목록에서 제거될 때와 목록 자체가 해제될 때 호출된다.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
```

`Update`가 해제 후에 호출돼도 조용히 반환하게 한다.

```csharp
    public void Update(PhysicalDeviceInfo info)
    {
        if (info == null || _disposed) return;
        // ... 이하 기존 본문 그대로
```

- [ ] **Step 4: 목록이 자식을 해제하게 한다**

`DeviceListViewModel.cs`의 제거 루프를 바꾼다.

```csharp
        for (var i = Devices.Count - 1; i >= 0; i--)
        {
            var identity = Devices[i].Identity;
            var stillPresent = incoming.Any(d => string.Equals(
                d.Identity, identity, StringComparison.OrdinalIgnoreCase));
            if (!stillPresent)
            {
                // 행이 건 구독을 끊고 뽑는다. 순서를 바꾸면 뽑힌 행이
                // 계속 이벤트를 받는다.
                Devices[i].Dispose();
                Devices.RemoveAt(i);
            }
        }
```

`Dispose`를 바꾼다.

```csharp
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _registry.SnapshotChanged -= OnSnapshotChanged;

        foreach (var device in Devices) device.Dispose();
        Devices.Clear();
    }
```

- [ ] **Step 5: 테스트가 통과하는지 확인한다**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet test DexManager.ViewModels.Tests/DexManager.ViewModels.Tests.csproj -v n
```

Expected: PASS, 25개.

- [ ] **Step 6: 커밋한다**

```bash
git add DexManager.ViewModels/DeviceViewModel.cs \
        DexManager.ViewModels/DeviceListViewModel.cs \
        DexManager.ViewModels.Tests/DeviceListViewModelTests.cs
git commit -m "$(cat <<'EOF'
feat(viewmodels): give device rows a disposal path

다음 Task가 행마다 런타임 세션 구독을 단다. 그 순간부터 Apply의
RemoveAt은 기기를 뽑을 때마다 구독을 하나씩 흘린다. 구독을 달기 전에
해제 경로를 먼저 만든다 — 지금은 비용이 없다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 7: `DeviceRuntimeCoordinator` — identity별 런타임 1개를 보장한다

**Files:**
- Create: `DexManager.Core/Services/DeviceRuntimeCoordinator.cs`
- Modify: `DexManager.Core/Hosting/ApplicationHost.cs` (속성 추가)
- Test: `DexManager.Tests/DeviceRuntimeCoordinatorTests.cs`

**Interfaces:**
- Consumes: `DeviceRuntimeServiceFactory.Create()`, `DeviceRuntimeSessionRegistry.BindServiceInstance(string serial, Guid instanceId)`
- Produces:
  - `DeviceRuntimeServiceSet DeviceRuntimeCoordinator.GetOrCreate(string identity, string serial)`
  - `bool DeviceRuntimeCoordinator.TryGet(string identity, out DeviceRuntimeServiceSet runtime)`
  - `ApplicationHost.RuntimeCoordinator { get; }`
  - Task 9·10의 명령이 런타임을 얻는 유일한 경로다.

`DeviceRuntimeServiceFactory.Create()`는 부를 때마다 새 세트를 만든다. 기기별 캐시가 아니다. GUI에서 같은 기기에 두 번 `Create()`를 하면 DeX 세션 둘이 서로를 모르는 채 겹치고, `AGENTS.md`의 복수 기기 불변 조건이 조용히 깨진다. 어떤 자동 테스트도 이것을 잡지 못한다.

**identity를 키로 쓴다.** serial은 USB↔무선 전환으로 바뀌지만 identity는 유지된다(`docs/DECISIONS.md`의 "연결 방식이 USB에서 Wi-Fi로 바뀌어도 물리 identity와 서비스 instance ID는 유지한다"). serial은 `BindServiceInstance`에 넘길 값으로만 쓴다.

- [ ] **Step 1: 실패 테스트를 쓴다**

`DexManager.Tests/DeviceRuntimeCoordinatorTests.cs`

```csharp
using DexManager.Services;

namespace DexManager.Tests;

public class DeviceRuntimeCoordinatorTests
{
    [Fact]
    public void GetOrCreate_ReturnsTheSameRuntimeForTheSameIdentity()
    {
        using var root = new TempHostRoot();
        using var host = root.CreateHost();
        var coordinator = host.RuntimeCoordinator;

        var first = coordinator.GetOrCreate("phone-a", "AAA");
        var second = coordinator.GetOrCreate("phone-a", "AAA");

        Assert.Same(first, second);
        Assert.Single(host.RuntimeFactory.CreatedInstances);
    }

    [Fact]
    public void GetOrCreate_ReturnsDistinctRuntimesForDistinctIdentities()
    {
        using var root = new TempHostRoot();
        using var host = root.CreateHost();
        var coordinator = host.RuntimeCoordinator;

        var a = coordinator.GetOrCreate("phone-a", "AAA");
        var b = coordinator.GetOrCreate("phone-b", "BBB");

        Assert.NotSame(a, b);
        Assert.Equal(2, host.RuntimeFactory.CreatedInstances.Count);
    }

    [Fact]
    public void GetOrCreate_KeepsTheRuntimeWhenTheSerialChanges()
    {
        using var root = new TempHostRoot();
        using var host = root.CreateHost();
        var coordinator = host.RuntimeCoordinator;

        // USB에서 무선으로 바뀌면 serial이 달라지지만 identity는 유지된다.
        var usb = coordinator.GetOrCreate("phone-a", "AAA");
        var wireless = coordinator.GetOrCreate("phone-a", "192.168.0.9:5555");

        Assert.Same(usb, wireless);
        Assert.Single(host.RuntimeFactory.CreatedInstances);
    }

    [Fact]
    public void TryGet_ReportsWhetherARuntimeExists()
    {
        using var root = new TempHostRoot();
        using var host = root.CreateHost();
        var coordinator = host.RuntimeCoordinator;

        Assert.False(coordinator.TryGet("phone-a", out _));

        var created = coordinator.GetOrCreate("phone-a", "AAA");

        Assert.True(coordinator.TryGet("phone-a", out var found));
        Assert.Same(created, found);
    }

    [Fact]
    public void GetOrCreate_RejectsAnEmptyIdentity()
    {
        using var root = new TempHostRoot();
        using var host = root.CreateHost();

        Assert.Throws<ArgumentException>(
            () => host.RuntimeCoordinator.GetOrCreate("  ", "AAA"));
    }
}
```

- [ ] **Step 2: 테스트가 실패하는지 확인한다**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet test DexManager.Tests/DexManager.Tests.csproj \
  --filter "FullyQualifiedName~DeviceRuntimeCoordinatorTests" -v n
```

Expected: 컴파일 실패 — `'ApplicationHost' does not contain a definition for 'RuntimeCoordinator'`

- [ ] **Step 3: 코디네이터를 구현한다**

`DexManager.Core/Services/DeviceRuntimeCoordinator.cs`

```csharp
using System;
using System.Collections.Generic;

namespace DexManager.Services
{
    /// <summary>
    /// 물리 기기 identity 하나에 <see cref="DeviceRuntimeServiceSet"/> 하나를
    /// 보장한다. 팩토리는 부를 때마다 새 세트를 만들므로, 중복 생성을 막는
    /// 책임이 호출자에게 있다 — 그 책임을 한 곳에 모은다.
    /// </summary>
    /// <remarks>
    /// 키가 serial이 아니라 identity인 이유: 연결 방식이 USB에서 무선으로
    /// 바뀌면 serial이 달라지지만 물리 기기와 서비스 instance ID는 유지된다.
    /// serial로 키를 잡으면 전환 때마다 런타임이 하나씩 더 생긴다.
    /// </remarks>
    public sealed class DeviceRuntimeCoordinator
    {
        private readonly DeviceRuntimeServiceFactory _factory;
        private readonly DeviceRuntimeSessionRegistry _sessions;
        private readonly object _sync = new object();

        private readonly Dictionary<string, DeviceRuntimeServiceSet> _byIdentity =
            new Dictionary<string, DeviceRuntimeServiceSet>(
                StringComparer.OrdinalIgnoreCase);

        public DeviceRuntimeCoordinator(
            DeviceRuntimeServiceFactory factory,
            DeviceRuntimeSessionRegistry sessions)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        }

        /// <summary>
        /// 이 identity의 런타임을 돌려준다. 없으면 만든다.
        /// <paramref name="serial"/>이 비어 있지 않으면 레지스트리에
        /// 기기↔인스턴스 결속을 갱신한다 — transport가 바뀌어도 같은
        /// 런타임이 새 serial로 다시 결속된다.
        /// </summary>
        public DeviceRuntimeServiceSet GetOrCreate(string identity, string serial)
        {
            if (string.IsNullOrWhiteSpace(identity))
            {
                throw new ArgumentException(
                    "A physical device identity is required.",
                    nameof(identity));
            }

            DeviceRuntimeServiceSet runtime;
            lock (_sync)
            {
                if (!_byIdentity.TryGetValue(identity, out runtime))
                {
                    runtime = _factory.Create();
                    _byIdentity.Add(identity, runtime);
                }
            }

            if (!string.IsNullOrWhiteSpace(serial))
            {
                _sessions.BindServiceInstance(serial, runtime.InstanceId);
            }

            return runtime;
        }

        /// <summary>
        /// 이 identity의 런타임이 이미 있으면 돌려준다. 만들지 않는다.
        /// 아직 아무것도 시작하지 않은 기기에 중지 명령이 들어왔을 때
        /// 빈 런타임을 만들지 않기 위해 쓴다.
        /// </summary>
        public bool TryGet(string identity, out DeviceRuntimeServiceSet runtime)
        {
            runtime = null;
            if (string.IsNullOrWhiteSpace(identity)) return false;
            lock (_sync) return _byIdentity.TryGetValue(identity, out runtime);
        }
    }
}
```

- [ ] **Step 4: `ApplicationHost`가 코디네이터를 소유하게 한다**

`ApplicationHost.cs`의 `InitializeRuntimeFactory()` 마지막에 추가한다.

```csharp
            RuntimeCoordinator = new DeviceRuntimeCoordinator(
                RuntimeFactory,
                RuntimeSessions);
```

속성 선언을 `RuntimeFactory` 옆에 추가한다.

```csharp
    /// <summary>
    /// 물리 기기별 런타임을 하나로 유지하는 코디네이터.
    /// GUI는 이것을 통해서만 런타임을 얻는다. TUI는 단일 런타임 동작을
    /// 유지하므로 사용하지 않는다.
    /// </summary>
    public DeviceRuntimeCoordinator RuntimeCoordinator { get; private set; }
```

- [ ] **Step 5: 테스트가 통과하는지 확인한다**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet test DexManager.Tests/DexManager.Tests.csproj -v n
```

Expected: PASS, 134개.

- [ ] **Step 6: 커밋한다**

```bash
git add DexManager.Core/Services/DeviceRuntimeCoordinator.cs \
        DexManager.Core/Hosting/ApplicationHost.cs \
        DexManager.Tests/DeviceRuntimeCoordinatorTests.cs
git commit -m "$(cat <<'EOF'
feat(core): guarantee one runtime per physical device

DeviceRuntimeServiceFactory.Create()는 부를 때마다 새 세트를 만든다.
GUI가 같은 기기에 두 번 부르면 DeX 세션 둘이 서로를 모르는 채 겹치고
AGENTS.md의 복수 기기 불변 조건이 조용히 깨진다.

키는 serial이 아니라 identity다. USB에서 무선으로 바뀌면 serial은 달라지고
identity는 유지되므로, serial로 키를 잡으면 전환마다 런타임이 하나씩 는다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 8: 행이 런타임 세션 상태를 반영한다

**Files:**
- Modify: `DexManager.ViewModels/DeviceViewModel.cs`
- Modify: `DexManager.ViewModels/DeviceListViewModel.cs` (생성자 시그니처)
- Modify: `DexManager.ViewModels/ShellViewModel.cs` (레지스트리 전달)
- Test: `DexManager.ViewModels.Tests/DeviceViewModelTests.cs`

**Interfaces:**
- Consumes: `DeviceRuntimeSessionRegistry.Changed`, `DeviceRuntimeRegistrySnapshot.FindByIdentity(string)`, Task 6의 `Dispose`
- Produces:
  - `DeviceViewModel(PhysicalDeviceInfo info, DeviceRuntimeSessionRegistry sessions, IUiDispatcher dispatcher)`
  - `bool DeviceViewModel.IsDexRunning { get; }`
  - `IReadOnlyList<SingleWindowSlotViewModel> DeviceViewModel.Slots { get; }` — Task 10에서 채운다. 이 Task에서는 빈 목록으로 둔다.
  - Task 9의 명령이 `IsDexRunning`으로 버튼 활성 여부를 정한다.

`DexOrchestrator`에는 public event가 없다. 상태를 실시간으로 받으려면 `DeviceRuntimeSessionRegistry.Changed`가 유일한 경로다. 이 이벤트는 감시·런타임 스레드에서 오므로 `IUiDispatcher`를 거친다.

- [ ] **Step 1: 실패 테스트를 쓴다**

`DexManager.ViewModels.Tests/DeviceViewModelTests.cs` 끝에 추가한다.

```csharp
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
            dispatcher);

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
            dispatcher);

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
```

`Discovered` 헬퍼를 이 파일에 추가한다(`DeviceListViewModelTests`의 `Device`와 같은 형태이나 이름이 겹치므로 구분한다).

```csharp
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
```

- [ ] **Step 2: 테스트가 실패하는지 확인한다**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet test DexManager.ViewModels.Tests/DexManager.ViewModels.Tests.csproj \
  --filter "FullyQualifiedName~DexRunningState" -v n
```

Expected: 컴파일 실패 — 3인자 생성자가 없다.

- [ ] **Step 3: 행에 구독을 단다**

`DeviceViewModel.cs`를 바꾼다. 기존 1인자 생성자는 남기지 않는다 — 목록만이 이 타입을 만들고, 목록은 Task 8 이후 항상 3인자를 쓴다.

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DexManager.Models;
using DexManager.Services;

namespace DexManager.ViewModels;

public sealed partial class DeviceViewModel : ObservableObject, IDisposable
{
    private readonly DeviceRuntimeSessionRegistry _sessions;
    private readonly IUiDispatcher _dispatcher;
    private bool _disposed;

    public DeviceViewModel(
        PhysicalDeviceInfo info,
        DeviceRuntimeSessionRegistry sessions,
        IUiDispatcher dispatcher)
    {
        if (info == null) throw new ArgumentNullException(nameof(info));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

        Identity = info.Identity ?? string.Empty;
        Update(info);

        _sessions.Changed += OnSessionsChanged;
        ApplyRuntime(_sessions.Current);
    }

    public string Identity { get; }

    [ObservableProperty]
    private bool _isDexRunning;

    /// <summary>단일창 슬롯 1~3. Task 10에서 채운다.</summary>
    public ObservableCollection<SingleWindowSlotViewModel> Slots { get; } = new();

    private void OnSessionsChanged(
        object sender,
        DeviceRuntimeRegistryChangedEventArgs e)
    {
        // 런타임 스레드에서 온다. 관측 가능한 상태를 건드리기 전에
        // UI 스레드로 넘긴다.
        var snapshot = e?.Snapshot;
        _dispatcher.Post(() => ApplyRuntime(snapshot));
    }

    private void ApplyRuntime(DeviceRuntimeRegistrySnapshot snapshot)
    {
        if (_disposed) return;

        var session = snapshot?.FindByIdentity(Identity);
        IsDexRunning = session?.Dex?.IsRunning == true;

        foreach (var slot in Slots) slot.ApplyRuntime(session);
    }
```

`Dispose`를 바꾼다.

```csharp
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _sessions.Changed -= OnSessionsChanged;

        foreach (var slot in Slots) slot.Dispose();
        Slots.Clear();
    }
```

`Slots`가 비어 있으므로 이 Task에서는 두 루프가 아무 일도 하지 않는다. Task 10이 채운다.

- [ ] **Step 4: 목록과 셸이 레지스트리를 전달하게 한다**

`DeviceListViewModel.cs`의 생성자를 바꾼다.

```csharp
    private readonly PhysicalDeviceRegistry _registry;
    private readonly DeviceRuntimeSessionRegistry _sessions;
    private readonly IUiDispatcher _dispatcher;

    public DeviceListViewModel(
        PhysicalDeviceRegistry registry,
        DeviceRuntimeSessionRegistry sessions,
        IUiDispatcher dispatcher)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

        _registry.SnapshotChanged += OnSnapshotChanged;
        Apply(_registry.Current);
    }
```

행 생성 지점을 바꾼다.

```csharp
            else Devices.Add(new DeviceViewModel(info, _sessions, _dispatcher));
```

`ShellViewModel.cs`의 생성자를 바꾼다.

```csharp
        Devices = new DeviceListViewModel(
            host.DeviceRegistry,
            host.RuntimeSessions,
            dispatcher);
```

기존 `DeviceListViewModelTests`의 `new DeviceListViewModel(registry, dispatcher)` 호출을 전부 `new DeviceListViewModel(registry, new DeviceRuntimeSessionRegistry(), dispatcher)`로 바꾼다.

- [ ] **Step 5: 테스트가 통과하는지 확인한다**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet test DexManager.ViewModels.Tests/DexManager.ViewModels.Tests.csproj -v n
```

Expected: PASS, 27개.

- [ ] **Step 6: 커밋한다**

```bash
git add DexManager.ViewModels/ DexManager.ViewModels.Tests/
git commit -m "$(cat <<'EOF'
feat(viewmodels): follow DeX session state from the runtime registry

DexOrchestrator에는 public event가 없다. 상태 변화를 실시간으로 관측하는
경로는 DeviceRuntimeSessionRegistry.Changed 하나뿐이며, 그 스냅샷에는
Dex.IsRunning과 슬롯별 IsRunning이 이미 들어 있다. Core를 건드리지 않고
행이 자기 기기의 상태만 골라 읽는다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 9: DeX 시작·중지 명령을 행에 단다

**Files:**
- Create: `DexManager.ViewModels/IDeviceRuntimeCommands.cs`
- Create: `DexManager.ViewModels/DeviceRuntimeCommands.cs`
- Create: `DexManager.ViewModels.Tests/FakeDeviceCommands.cs`
- Modify: `DexManager.ViewModels/DeviceViewModel.cs`, `DeviceListViewModel.cs`, `ShellViewModel.cs`
- Test: `DexManager.ViewModels.Tests/DeviceViewModelTests.cs`

**Interfaces:**
- Consumes: `DeviceRuntimeCoordinator.GetOrCreate/TryGet`(Task 7), `DeviceViewModel.IsDexRunning`(Task 8)
- Produces:
  - `IDeviceRuntimeCommands` — 4개 메서드. Task 10의 슬롯 명령이 같은 인터페이스를 쓴다.
  - `DeviceViewModel.StartDexCommand` / `StopDexCommand` (`IAsyncRelayCommand`)
  - `bool DeviceViewModel.IsBusy { get; }`

`docs/TODO.md`의 Phase 2 설계 주의를 그대로 따른다 — **명령을 `ShellViewModel`에 두지 않는다.** 거기 두면 `host.SelectedSerial`을 읽는 것이 최소 저항 경로가 되고, 복수 기기 불변식이 조용히 무너지며 어떤 테스트도 잡지 못한다. 명령은 행에 두고 serial은 행에서 가져온다.

**경계를 인터페이스로 끊는 이유:** DeX 시작은 실제 adb와 scrcpy 프로세스를 부른다. ViewModel 테스트가 이것을 부를 수 없다. 경계를 인터페이스로 두면 명령의 상태 기계(중복 실행 방지, 실패 처리)를 기기 없이 검증할 수 있다.

- [ ] **Step 1: 명령 경계와 stub을 만든다**

`DexManager.ViewModels/IDeviceRuntimeCommands.cs`

```csharp
namespace DexManager.ViewModels;

/// <summary>
/// 행이 부르는 런타임 명령의 경계. 실제 구현은 adb와 scrcpy 프로세스를
/// 부르므로, 테스트는 이 인터페이스에 stub을 넣어 명령의 상태 기계만
/// 기기 없이 검증한다.
/// </summary>
public interface IDeviceRuntimeCommands
{
    Task<bool> StartDexAsync(
        string identity,
        string serial,
        CancellationToken cancellationToken);

    Task<bool> StopDexAsync(
        string identity,
        string serial,
        CancellationToken cancellationToken);

    void StartSingleWindow(
        string identity,
        string serial,
        int slot,
        string appPackage);

    void StopSingleWindow(string identity, int slot);
}
```

`DexManager.ViewModels.Tests/FakeDeviceCommands.cs`

```csharp
using DexManager.ViewModels;

namespace DexManager.ViewModels.Tests;

/// <summary>
/// 테스트용 런타임 명령 stub. 호출 인자를 기록하고 미리 정한 결과를
/// 돌려준다. <see cref="StartGate"/>를 걸면 시작이 끝나지 않은 상태를
/// 만들 수 있어 중복 실행 방지를 검증할 수 있다.
/// </summary>
public sealed class FakeDeviceCommands : IDeviceRuntimeCommands
{
    public List<string> Calls { get; } = new();
    public bool StartResult { get; set; } = true;
    public bool StopResult { get; set; } = true;
    public TaskCompletionSource<bool> StartGate { get; set; }

    public async Task<bool> StartDexAsync(
        string identity,
        string serial,
        CancellationToken cancellationToken)
    {
        Calls.Add($"start-dex:{identity}:{serial}");
        if (StartGate != null) await StartGate.Task;
        return StartResult;
    }

    public Task<bool> StopDexAsync(
        string identity,
        string serial,
        CancellationToken cancellationToken)
    {
        Calls.Add($"stop-dex:{identity}:{serial}");
        return Task.FromResult(StopResult);
    }

    public void StartSingleWindow(
        string identity,
        string serial,
        int slot,
        string appPackage)
        => Calls.Add($"start-slot:{identity}:{serial}:{slot}:{appPackage}");

    public void StopSingleWindow(string identity, int slot)
        => Calls.Add($"stop-slot:{identity}:{slot}");
}
```

- [ ] **Step 2: 실패 테스트를 쓴다**

`DexManager.ViewModels.Tests/DeviceViewModelTests.cs` 끝에 추가한다.

```csharp
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
```

행 생성 헬퍼를 같은 파일에 추가한다.

```csharp
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
```

- [ ] **Step 3: 테스트가 실패하는지 확인한다**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet test DexManager.ViewModels.Tests/DexManager.ViewModels.Tests.csproj \
  --filter "FullyQualifiedName~DexCommand" -v n
```

Expected: 컴파일 실패 — 4인자 생성자와 `StartDexCommand`가 없다.

- [ ] **Step 4: 행에 명령을 구현한다**

`DeviceViewModel.cs`에 `using CommunityToolkit.Mvvm.Input;`을 추가하고, 생성자에 4번째 인자를 받는다.

```csharp
    private readonly IDeviceRuntimeCommands _commands;

    public DeviceViewModel(
        PhysicalDeviceInfo info,
        DeviceRuntimeSessionRegistry sessions,
        IUiDispatcher dispatcher,
        IDeviceRuntimeCommands commands)
    {
        if (info == null) throw new ArgumentNullException(nameof(info));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));

        Identity = info.Identity ?? string.Empty;
        Update(info);

        _sessions.Changed += OnSessionsChanged;
        ApplyRuntime(_sessions.Current);
    }
```

명령과 상태를 추가한다.

```csharp
    /// <summary>
    /// 이 행의 명령이 하나 진행 중인지 여부. scrcpy 시작은 느리므로
    /// 버튼을 두 번 눌러 세션이 겹치는 것을 막는다.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartDexCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopDexCommand))]
    private bool _isBusy;

    /// <summary>마지막 명령의 결과 문구. 실패해도 예외를 올리지 않는다.</summary>
    [ObservableProperty]
    private string _lastCommandMessage = string.Empty;

    private bool CanRunCommand() => !_disposed && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanRunCommand))]
    private async Task StartDexAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            var started = await _commands.StartDexAsync(
                Identity,
                PrimarySerial,
                cancellationToken);
            LastCommandMessage = started
                ? "DeX started."
                : "DeX did not start.";
        }
        catch (OperationCanceledException)
        {
            LastCommandMessage = "DeX start was cancelled.";
        }
        catch (Exception ex)
        {
            // 명령 실패가 창을 죽이면 안 된다. 행에 문구로만 남긴다.
            LastCommandMessage = $"DeX start failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunCommand))]
    private async Task StopDexAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            var stopped = await _commands.StopDexAsync(
                Identity,
                PrimarySerial,
                cancellationToken);
            LastCommandMessage = stopped
                ? "DeX stopped and the display overlay was cleaned up."
                : "DeX stopped, but display cleanup was deferred.";
        }
        catch (Exception ex)
        {
            LastCommandMessage = $"DeX stop failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
```

- [ ] **Step 5: 프로덕션 구현을 만든다**

`DexManager.ViewModels/DeviceRuntimeCommands.cs`

```csharp
using DexManager.Hosting;
using DexManager.Models;

namespace DexManager.ViewModels;

/// <summary>
/// <see cref="IDeviceRuntimeCommands"/>의 실제 구현. 코디네이터에서 이
/// 기기의 런타임을 얻어 명령을 넘긴다. 런타임을 직접 만들지 않는다 —
/// 중복 생성 방지는 코디네이터의 책임이다.
/// </summary>
public sealed class DeviceRuntimeCommands : IDeviceRuntimeCommands
{
    private readonly ApplicationHost _host;

    public DeviceRuntimeCommands(ApplicationHost host)
        => _host = host ?? throw new ArgumentNullException(nameof(host));

    public Task<bool> StartDexAsync(
        string identity,
        string serial,
        CancellationToken cancellationToken)
    {
        var runtime = _host.RuntimeCoordinator.GetOrCreate(identity, serial);
        return runtime.Dex.StartAsync(serial, identity, cancellationToken);
    }

    public Task<bool> StopDexAsync(
        string identity,
        string serial,
        CancellationToken cancellationToken)
    {
        // 아직 아무것도 시작하지 않았으면 빈 런타임을 만들지 않는다.
        if (!_host.RuntimeCoordinator.TryGet(identity, out var runtime))
            return Task.FromResult(true);

        return runtime.Dex.StopOrConfirmCleanupAsync();
    }

    public void StartSingleWindow(
        string identity,
        string serial,
        int slot,
        string appPackage)
    {
        var runtime = _host.RuntimeCoordinator.GetOrCreate(identity, serial);
        var settings = _host.Settings;

        runtime.SingleWindows.Start(
            slot,
            new SingleWindowSlotSettings
            {
                Slot = slot,
                Width = settings.VirtualDisplay.Width,
                Height = settings.VirtualDisplay.Height,
                Dpi = settings.VirtualDisplay.Dpi,
                BitRate = settings.Scrcpy.BitRate,
                MaxFps = settings.Scrcpy.MaxFps,
                StayAwake = settings.Scrcpy.StayAwake,
                TurnScreenOff = settings.Scrcpy.TurnScreenOff,
                StartAppPackage = appPackage,
                AdditionalArguments = settings.Scrcpy.AdditionalArguments
            },
            serial);
    }

    public void StopSingleWindow(string identity, int slot)
    {
        if (!_host.RuntimeCoordinator.TryGet(identity, out var runtime)) return;
        runtime.SingleWindows.Stop(slot);
    }
}
```

`DeviceListViewModel`과 `ShellViewModel`이 이것을 전달하도록 생성자를 넓힌다. `ShellViewModel` 생성자에서 만든다.

```csharp
        Devices = new DeviceListViewModel(
            host.DeviceRegistry,
            host.RuntimeSessions,
            dispatcher,
            new DeviceRuntimeCommands(host));
```

`DeviceListViewModel`은 받은 `IDeviceRuntimeCommands`를 필드에 두고 행 생성에 넘긴다.

```csharp
            else Devices.Add(new DeviceViewModel(info, _sessions, _dispatcher, _commands));
```

- [ ] **Step 6: 테스트가 통과하는지 확인한다**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet test DexManager.Mac.sln --nologo
```

Expected: PASS, xUnit 165개.

- [ ] **Step 7: 커밋한다**

```bash
git add DexManager.ViewModels/ DexManager.ViewModels.Tests/
git commit -m "$(cat <<'EOF'
feat(viewmodels): start and stop DeX from the device row

명령을 ShellViewModel에 두면 host.SelectedSerial을 읽는 것이 최소 저항
경로가 되고, 복수 기기 불변식이 조용히 무너지며 어떤 테스트도 잡지 못한다.
명령은 행에 두고 serial은 행에서 가져온다. SelectedSerial은 문서대로
진단 전용으로 남는다.

명령 경계를 인터페이스로 끊어 adb·scrcpy 없이 상태 기계를 검증한다.
scrcpy 시작은 느리므로 중복 실행을 CanExecute로 막는다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 10: 단일창 슬롯 1~3

**Files:**
- Create: `DexManager.ViewModels/SingleWindowSlotViewModel.cs`
- Modify: `DexManager.ViewModels/DeviceViewModel.cs` (`Slots` 채우기)
- Test: `DexManager.ViewModels.Tests/SingleWindowSlotViewModelTests.cs`

**Interfaces:**
- Consumes: `IDeviceRuntimeCommands.StartSingleWindow/StopSingleWindow`(Task 9), `DeviceRuntimeSessionSnapshot.SingleWindows`(Task 8)
- Produces:
  - `SingleWindowSlotViewModel(int slot, string identity, Func<string> serialProvider, IDeviceRuntimeCommands commands)`
  - `void SingleWindowSlotViewModel.ApplyRuntime(DeviceRuntimeSessionSnapshot session)`
  - `StartCommand` / `StopCommand`, `IsRunning`, `AppPackage`

슬롯은 1~3 고정이다. `SingleWindowService.Start()`가 범위 밖에 `ArgumentOutOfRangeException`을 던진다. 상태는 Task 8의 스냅샷 경로를 그대로 탄다 — 슬롯 ViewModel은 자기 이벤트를 구독하지 않고 부모가 넘겨주는 세션 스냅샷만 읽는다. 구독 지점을 하나로 유지해 해제 누락을 없앤다.

serial은 `Func<string>`로 받는다. 행의 `PrimarySerial`은 transport 전환으로 바뀌므로 생성 시점 값을 붙잡으면 무선 전환 뒤 옛 serial로 시작하게 된다.

- [ ] **Step 1: 실패 테스트를 쓴다**

`DexManager.ViewModels.Tests/SingleWindowSlotViewModelTests.cs`

```csharp
using DexManager.Models;
using DexManager.ViewModels;

namespace DexManager.ViewModels.Tests;

public class SingleWindowSlotViewModelTests
{
    [Fact]
    public void StartCommand_PassesTheSlotAndTheCurrentSerial()
    {
        var commands = new FakeDeviceCommands();
        var serial = "AAA";
        var vm = new SingleWindowSlotViewModel(
            2, "phone-a", () => serial, commands)
        {
            AppPackage = "com.sec.android.app.sbrowser"
        };

        vm.StartCommand.Execute(null);

        Assert.Equal(
            new[] { "start-slot:phone-a:AAA:2:com.sec.android.app.sbrowser" },
            commands.Calls);
    }

    [Fact]
    public void StartCommand_UsesTheSerialAtInvocationTimeNotAtConstruction()
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
        vm.StartCommand.Execute(null);

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
}
```

- [ ] **Step 2: 테스트가 실패하는지 확인한다**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet test DexManager.ViewModels.Tests/DexManager.ViewModels.Tests.csproj \
  --filter "FullyQualifiedName~SingleWindowSlotViewModelTests" -v n
```

Expected: 컴파일 실패 — `SingleWindowSlotViewModel` 타입이 없다.

- [ ] **Step 3: 슬롯 ViewModel을 구현한다**

`DexManager.ViewModels/SingleWindowSlotViewModel.cs`

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DexManager.Models;

namespace DexManager.ViewModels;

/// <summary>
/// 단일창 슬롯 하나의 상태와 명령. 슬롯은 1~3 고정이다 —
/// <c>SingleWindowService.Start</c>가 범위 밖 값을 거부한다.
/// </summary>
/// <remarks>
/// 이 ViewModel은 레지스트리를 직접 구독하지 않는다. 부모
/// <see cref="DeviceViewModel"/>이 자기 구독에서 받은 세션 스냅샷을
/// <see cref="ApplyRuntime"/>으로 넘긴다. 구독 지점을 하나로 유지해
/// 해제 누락을 없앤다.
/// </remarks>
public sealed partial class SingleWindowSlotViewModel : ObservableObject, IDisposable
{
    private readonly string _identity;
    private readonly Func<string> _serialProvider;
    private readonly IDeviceRuntimeCommands _commands;
    private bool _disposed;

    public SingleWindowSlotViewModel(
        int slot,
        string identity,
        Func<string> serialProvider,
        IDeviceRuntimeCommands commands)
    {
        if (slot < 1 || slot > 3)
        {
            throw new ArgumentOutOfRangeException(
                nameof(slot),
                slot,
                "Single window slots are 1 through 3.");
        }

        Slot = slot;
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
        _serialProvider = serialProvider
            ?? throw new ArgumentNullException(nameof(serialProvider));
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
    }

    public int Slot { get; }

    /// <summary>화면에 표시할 슬롯 이름.</summary>
    public string Title => $"Window #{Slot}";

    [ObservableProperty]
    private bool _isRunning;

    /// <summary>실행할 Android 앱 패키지. 비어 있으면 시작할 수 없다.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private string _appPackage = string.Empty;

    /// <summary>
    /// 부모가 넘긴 세션 스냅샷에서 이 슬롯의 상태만 골라 반영한다.
    /// 세션이 없으면 중지 상태로 본다.
    /// </summary>
    public void ApplyRuntime(DeviceRuntimeSessionSnapshot session)
    {
        if (_disposed) return;

        var slot = session?.SingleWindows?
            .FirstOrDefault(s => s != null && s.Slot == Slot);
        IsRunning = slot?.IsRunning == true;
    }

    private bool CanStart()
        => !_disposed && !string.IsNullOrWhiteSpace(AppPackage);

    [RelayCommand(CanExecute = nameof(CanStart))]
    private void Start()
    {
        // serial은 호출 시점에 읽는다. USB에서 무선으로 전환되면
        // 생성 시점 값은 이미 죽은 transport다.
        _commands.StartSingleWindow(
            _identity,
            _serialProvider(),
            Slot,
            AppPackage.Trim());
    }

    [RelayCommand]
    private void Stop() => _commands.StopSingleWindow(_identity, Slot);

    public void Dispose() => _disposed = true;
}
```

`using System.Linq;`가 암시적으로 들어오지 않으면 파일 상단에 추가한다.

- [ ] **Step 4: 행이 슬롯 3개를 만들게 한다**

`DeviceViewModel.cs`의 생성자에서 `_sessions.Changed` 구독 **직전에** 슬롯을 채운다. 구독보다 먼저 만들어야 첫 `ApplyRuntime`이 슬롯까지 반영한다.

```csharp
        for (var slot = 1; slot <= 3; slot++)
        {
            Slots.Add(new SingleWindowSlotViewModel(
                slot,
                Identity,
                () => PrimarySerial,
                _commands));
        }

        _sessions.Changed += OnSessionsChanged;
        ApplyRuntime(_sessions.Current);
```

Task 8에서 넣어 둔 `foreach (var slot in Slots) slot.ApplyRuntime(session);`과 `Dispose`의 슬롯 정리 루프가 이제 실제로 동작한다.

- [ ] **Step 5: 테스트가 통과하는지 확인한다**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet test DexManager.Mac.sln --nologo
```

Expected: PASS, xUnit 172개.

- [ ] **Step 6: 커밋한다**

```bash
git add DexManager.ViewModels/ DexManager.ViewModels.Tests/
git commit -m "$(cat <<'EOF'
feat(viewmodels): add single window slots 1 to 3

슬롯 ViewModel은 레지스트리를 직접 구독하지 않는다. 부모가 자기 구독에서
받은 세션 스냅샷을 넘긴다 — 구독 지점을 하나로 유지해 해제 누락을 없앤다.

serial은 Func으로 받아 호출 시점에 읽는다. USB에서 무선으로 전환되면
생성 시점 값은 이미 죽은 transport다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 11: `ApplicationHost` 조립을 공유하고 셸 생성 실패 시 누수를 막는다

**Files:**
- Create: `DexManager.Platform.Mac/MacApplicationHostFactory.cs`
- Modify: `DexManager.Mac/Hosting/InteractiveHost.cs:43-52`
- Modify: `DexManager.Desktop/App.axaml.cs`
- Test: `DexManager.Tests/` (조립 팩토리는 실제 경로에 의존하므로 단위 테스트 대신 빌드와 실행으로 검증한다)

**Interfaces:**
- Consumes: 없음
- Produces: `static ApplicationHost MacApplicationHostFactory.Create()`

`docs/TODO.md`: "`new ShellViewModel(...)`가 던질 때 이미 만들어진 `ApplicationHost`가 새지 않게 한다 — 현재는 `_shell`이 null이라 종료 훅의 `DisposeQuietly()`가 no-op이 된다." 그리고 `InteractiveHost` 생성자와 `App.CreateMainWindow()`가 같은 조립 코드를 중복 보유한다. 둘을 함께 고친다.

- [ ] **Step 1: 공유 팩토리를 만든다**

`DexManager.Platform.Mac/MacApplicationHostFactory.cs`

```csharp
using DexManager.Hosting;

namespace DexManager.Mac.Platform;

/// <summary>
/// macOS 플랫폼 서비스로 <see cref="ApplicationHost"/>를 조립한다.
/// TUI와 GUI가 같은 조립을 쓰게 해, 한쪽만 바뀌어 두 실행 경로가
/// 갈라지는 것을 막는다.
/// </summary>
public static class MacApplicationHostFactory
{
    public static ApplicationHost Create()
    {
        var pathProvider = new MacPathProvider();

        return new ApplicationHost(
            new MacPlatformService(),
            pathProvider,
            new MacCaptureService(pathProvider.DefaultScreenshotFolder),
            new MacKeyboardService(),
            new MacAutoStartService());
    }
}
```

- [ ] **Step 2: TUI가 팩토리를 쓰게 한다**

`InteractiveHost.cs`의 생성자 앞부분을 바꾼다.

```csharp
    public InteractiveHost()
    {
        _host = MacApplicationHostFactory.Create();
```

`var pathProvider = new MacPathProvider();`와 뒤따르는 `new ApplicationHost(...)` 블록을 지운다.

- [ ] **Step 3: GUI가 팩토리를 쓰고 누수를 막게 한다**

`App.axaml.cs`의 `CreateMainWindow()`를 바꾼다.

```csharp
    private Window CreateMainWindow()
    {
        ApplicationHost host = null;
        try
        {
            host = MacApplicationHostFactory.Create();

            _shell = new ShellViewModel(host, new AvaloniaUiDispatcher());
            // 셸이 호스트를 넘겨받았다. 이제부터 정리는 셸의 몫이다.
            host = null;

            var window = new MainWindow { DataContext = _shell };

            _shell.Start();
            return window;
        }
        catch (Exception ex)
        {
            // ApplicationHost 생성자는 adb를 찾지 못하면 FileNotFoundException을
            // 던진다. 여기서 막지 않으면 창도 대화상자도 없이 앱이 죽는다.
            //
            // ShellViewModel 생성이 던지면 _shell은 null이라 DisposeQuietly가
            // 아무것도 하지 않는다. 이미 만들어진 호스트를 직접 회수한다.
            DisposeQuietly();
            DisposeHostQuietly(host);
            Report(ex);
            return StartupErrorWindow.Create(ex);
        }
    }

    /// <summary>
    /// 셸이 넘겨받지 못한 호스트를 회수한다. 정리 실패는 보고만 하고
    /// 삼키지 않는다 — 이 경로는 이미 오류 처리 중이다.
    /// </summary>
    private static void DisposeHostQuietly(ApplicationHost host)
    {
        if (host == null) return;
        try
        {
            host.Dispose();
        }
        catch (Exception ex)
        {
            Report(ex);
        }
    }
```

`_shell.Start()`가 던지는 경우도 같은 경로로 들어온다. 그때는 `host`가 이미 `null`이고 `_shell`이 살아 있으므로 `DisposeQuietly()`가 호스트까지 회수한다.

- [ ] **Step 4: 두 실행 경로를 확인한다**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet build DexManager.Mac.sln --nologo
dotnet test DexManager.Mac.sln --nologo
cd DexManager.Mac/bin/Debug/net8.0 && ./DXManager.Mac --diag 2>&1 | tail -3
```

Expected: 빌드 성공, xUnit 172개 통과, TUI 진단이 `DX Manager stopped cleanly. Goodbye!`로 끝난다.

- [ ] **Step 5: 커밋한다**

```bash
git add DexManager.Platform.Mac/MacApplicationHostFactory.cs \
        DexManager.Mac/Hosting/InteractiveHost.cs \
        DexManager.Desktop/App.axaml.cs
git commit -m "$(cat <<'EOF'
refactor(mac): share host assembly between the TUI and the GUI

InteractiveHost 생성자와 App.CreateMainWindow()가 같은 조립 코드를 두 벌
들고 있었다. 한쪽만 바뀌면 두 실행 경로가 조용히 갈라진다.

ShellViewModel 생성이 던지면 _shell이 null이라 종료 훅의 DisposeQuietly가
no-op이 되고 이미 만들어진 호스트가 샌다. 소유권이 셸로 넘어간 시점을
명시하고, 넘어가기 전에 실패하면 호스트를 직접 회수한다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 12: `MainWindow`에 DeX 제어와 슬롯을 붙인다

**Files:**
- Modify: `DexManager.Desktop/Views/MainWindow.axaml`
- Test: 실행 확인 (Avalonia 뷰는 자동 테스트 대상이 아니다 — 스펙 5.2절이 헤드리스 스모크를 "선택"으로 둔다)

**Interfaces:**
- Consumes: `DeviceListViewModel.SelectedDevice`, `DeviceViewModel.IsDexRunning/IsBusy/LastCommandMessage/StartDexCommand/StopDexCommand/Slots`, `SingleWindowSlotViewModel.Title/AppPackage/IsRunning/StartCommand/StopCommand`
- Produces: 실사용 가능한 창. 스펙 7절 Phase 2 완료 조건.

목록은 왼쪽에 두고 선택된 기기의 제어를 오른쪽에 둔다. 제어는 `SelectedDevice`에 바인딩하므로 명령이 항상 화면에 보이는 그 기기로 간다.

**Phase 1에서 관측된 결함도 함께 고친다.** 선택된 행의 파란 배경 위에서 `Foreground="Green"` 하드코딩 때문에 `Connected` 라벨이 거의 읽히지 않는다. 테마 브러시로 바꾼다.

- [ ] **Step 1: `MainWindow.axaml`을 교체한다**

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="clr-namespace:DexManager.ViewModels;assembly=DexManager.ViewModels"
        x:Class="DexManager.Desktop.Views.MainWindow"
        x:DataType="vm:ShellViewModel"
        Title="DX Manager"
        Width="980" Height="640"
        MinWidth="820" MinHeight="480">
  <Grid RowDefinitions="Auto,*,Auto" ColumnDefinitions="320,*" Margin="16">

    <TextBlock Grid.Row="0" Grid.Column="0"
               Text="Connected devices"
               FontSize="18"
               FontWeight="SemiBold"
               Margin="0,0,0,12" />

    <Border Grid.Row="1" Grid.Column="0"
            BorderThickness="1"
            BorderBrush="{DynamicResource SystemControlForegroundBaseMediumLowBrush}"
            CornerRadius="4"
            Margin="0,0,12,0">
      <Panel>
        <TextBlock Text="No devices connected. Plug in a Galaxy device."
                   HorizontalAlignment="Center"
                   VerticalAlignment="Center"
                   TextWrapping="Wrap"
                   TextAlignment="Center"
                   Margin="16"
                   Opacity="0.6"
                   IsVisible="{Binding Devices.IsEmpty}" />

        <!-- 두 자식 중 하나만 그려지게 한다. ListBox 기본 배경이 투명한지에
             빈 목록 안내의 가시성을 맡기지 않는다. -->
        <ListBox ItemsSource="{Binding Devices.Devices}"
                 SelectedItem="{Binding Devices.SelectedDevice}"
                 IsVisible="{Binding !Devices.IsEmpty}">
          <ListBox.ItemTemplate>
            <DataTemplate x:DataType="vm:DeviceViewModel">
              <StackPanel Margin="4">
                <StackPanel Orientation="Horizontal" Spacing="8">
                  <TextBlock Text="{Binding DisplayName}" FontWeight="SemiBold" />
                  <!-- Foreground를 고정하지 않는다. 선택된 행의 강조 배경
                       위에서 하드코딩된 Green은 거의 읽히지 않는다. -->
                  <TextBlock Text="Connected"
                             Opacity="0.85"
                             IsVisible="{Binding IsConnected}" />
                  <TextBlock Text="Disconnected"
                             Opacity="0.6"
                             IsVisible="{Binding !IsConnected}" />
                  <TextBlock Text="DeX"
                             FontWeight="SemiBold"
                             Opacity="0.85"
                             IsVisible="{Binding IsDexRunning}" />
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

    <ScrollViewer Grid.Row="1" Grid.Column="1"
                  IsVisible="{Binding Devices.SelectedDevice, Converter={x:Static ObjectConverters.IsNotNull}}">
      <StackPanel DataContext="{Binding Devices.SelectedDevice}"
                  x:DataType="vm:DeviceViewModel"
                  Spacing="16">

        <TextBlock Text="{Binding DisplayName}"
                   FontSize="18"
                   FontWeight="SemiBold" />

        <StackPanel Orientation="Horizontal" Spacing="8">
          <Button Content="Start DeX"
                  Command="{Binding StartDexCommand}"
                  IsVisible="{Binding !IsDexRunning}" />
          <Button Content="Stop DeX"
                  Command="{Binding StopDexCommand}"
                  IsVisible="{Binding IsDexRunning}" />
          <TextBlock Text="Working…"
                     VerticalAlignment="Center"
                     Opacity="0.7"
                     IsVisible="{Binding IsBusy}" />
        </StackPanel>

        <TextBlock Text="{Binding LastCommandMessage}"
                   Opacity="0.75"
                   TextWrapping="Wrap"
                   IsVisible="{Binding LastCommandMessage, Converter={x:Static StringConverters.IsNotNullOrEmpty}}" />

        <TextBlock Text="Single app windows"
                   FontWeight="SemiBold"
                   Margin="0,8,0,0" />

        <ItemsControl ItemsSource="{Binding Slots}">
          <ItemsControl.ItemTemplate>
            <DataTemplate x:DataType="vm:SingleWindowSlotViewModel">
              <Border BorderThickness="1"
                      BorderBrush="{DynamicResource SystemControlForegroundBaseMediumLowBrush}"
                      CornerRadius="4"
                      Padding="12"
                      Margin="0,0,0,8">
                <Grid ColumnDefinitions="Auto,*,Auto,Auto" ColumnSpacing="8">
                  <TextBlock Grid.Column="0"
                             Text="{Binding Title}"
                             FontWeight="SemiBold"
                             VerticalAlignment="Center"
                             Width="96" />
                  <TextBox Grid.Column="1"
                           Text="{Binding AppPackage}"
                           Watermark="com.sec.android.app.sbrowser"
                           VerticalAlignment="Center" />
                  <Button Grid.Column="2"
                          Content="Start"
                          Command="{Binding StartCommand}"
                          IsVisible="{Binding !IsRunning}" />
                  <Button Grid.Column="3"
                          Content="Stop"
                          Command="{Binding StopCommand}"
                          IsVisible="{Binding IsRunning}" />
                </Grid>
              </Border>
            </DataTemplate>
          </ItemsControl.ItemTemplate>
        </ItemsControl>
      </StackPanel>
    </ScrollViewer>

    <TextBlock Grid.Row="2" Grid.Column="0" Grid.ColumnSpan="2"
               Text="{Binding StatusText}"
               Margin="0,12,0,0"
               Opacity="0.8" />

  </Grid>
</Window>
```

- [ ] **Step 2: 창이 뜨는지 확인한다**

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet build DexManager.Mac.sln --nologo
dotnet run --project DexManager.Desktop
```

Expected: 창이 뜨고, 연결된 기기가 왼쪽 목록에, 오른쪽에 `Start DeX`와 슬롯 3개가 보인다. 콘솔에 바인딩 오류가 없어야 한다. `AvaloniaUseCompiledBindingsByDefault`가 켜져 있으므로 `x:DataType` 불일치는 빌드 시점에 잡힌다.

기기 없이도 확인한다 — 왼쪽에 빈 목록 안내가 나오고 오른쪽 패널 전체가 숨겨져야 한다.

- [ ] **Step 3: 커밋한다**

```bash
git add DexManager.Desktop/Views/MainWindow.axaml
git commit -m "$(cat <<'EOF'
feat(desktop): add DeX and single window controls to MainWindow

제어는 SelectedDevice에 바인딩한다. 명령이 항상 화면에 보이는 그 기기로
간다.

행의 Connected 라벨에서 Foreground="Green" 하드코딩을 걷어낸다. 선택된
행의 강조 배경 위에서 어두운 초록은 거의 읽히지 않았다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 13: CI 필터, 문서 갱신, 실기 검증

**Files:**
- Modify: `.github/workflows/macos-portable.yml:5-23`, `:29-47`
- Modify: `docs/superpowers/specs/2026-09-03-macos-gui-design.md` (5.1절, 7절, 8.1절)
- Modify: `docs/TODO.md`
- Modify: `docs/KNOWN_ISSUES.md` (실기 미확인 항목)

**Interfaces:**
- Consumes: Task 1~12 전부
- Produces: Phase 2 종결 기록. Phase 3 착수 시점의 출발점.

- [ ] **Step 1: 워크플로 `paths:` 두 블록에 `DexManager.Platform.Mac/**`를 추가한다**

`pull_request`와 `push` 양쪽 모두에 넣는다. 알파벳 순서상 `DexManager.MultiDeviceTests/**` 다음, `DexManager.Tests/**` 앞이다.

```yaml
      - "DexManager.MultiDeviceTests/**"
      - "DexManager.Platform.Mac/**"
      - "DexManager.Tests/**"
```

빠져 있으면 그 프로젝트만 건드린 PR이 macOS 워크플로를 트리거하지 않는다. main에 이미 있던 공백이다.

- [ ] **Step 2: 스펙 5.1절의 베이스라인을 갱신한다**

현재 문장은 "xUnit 95개 + 다중기기 회귀 39개(총 134개)"다. Phase 2 완료 시점 숫자로 바꾼다.

```markdown
### 5.1 기존 안전망

xUnit 172개 + 다중기기 회귀 39개(총 211개)가 통과 중이다. 각 Phase는
**전부 통과**를 완료 조건으로 한다.

- `dotnet test DexManager.Mac.sln` — xUnit 172개
- `dotnet run --project DexManager.MultiDeviceTests -c Release` — 39개
```

실제 숫자는 Task 12 종료 시점의 실행 결과로 채운다. 위 숫자는 이 계획이 예측한 값이므로 **반드시 실행 결과로 대체한다.**

- [ ] **Step 3: 스펙 7절 표에서 Phase 2를 완료로 표시한다**

```markdown
| 2 | DeX 시작/중지 + 단일창 슬롯 | ✅ 완료 — 실사용 가능. 실기 검증은 KNOWN_ISSUES 참조 |
```

- [ ] **Step 4: 스펙 8.1절의 공백 5를 종결한다**

"`Settings`가 저장 조율 없이 공유된다" 항목에 결과를 적는다.

```markdown
- ~~`Settings`가 저장 조율 없이 공유된다~~ — **해결됨(2026-09-07, Phase 2 Task 4).**
  TUI 설정 메뉴가 `UpdateSettings`를 거치며, 해제된 호스트에 대한 쓰기는
  `ObjectDisposedException`으로 막는다. 입력은 잠금 밖에서 받는다.
```

- [ ] **Step 5: `docs/TODO.md`를 갱신한다**

"macOS GUI Phase 2 착수 전 선행 정리"의 모든 하위 항목을 `[x]`로 바꾸고, "macOS GUI Phase 2 설계 시 주의"의 항목도 처리 결과를 적는다. 그리고 Phase 2 완료 항목을 추가한다.

```markdown
- [x] macOS GUI Phase 2 — DeX 시작/중지와 단일창 슬롯
  - [x] 런타임 정리 책임을 `ApplicationHost`로 이관
  - [x] `UpdateSettings`에 프로덕션 호출자 연결과 `_disposed` 가드
  - [x] `ApplicationHost._disposed`를 `Interlocked`로 전환
  - [x] `DeviceListViewModel` 생성자의 구독-적용 경합 해소
  - [x] `DeviceViewModel`에 `IDisposable`
  - [x] identity별 런타임 1개를 보장하는 `DeviceRuntimeCoordinator`
  - [x] 명령을 `DeviceViewModel`에 배치 (`SelectedSerial`은 진단 전용 유지)
  - [x] 워크플로 `paths:`에 `DexManager.Platform.Mac/**` 추가
  - [ ] 실기 검증 — 아래 항목은 사용자 확인 전까지 미확인이다
```

- [ ] **Step 6: 실기 검증을 수행하고 결과를 기록한다**

`AGENTS.md`와 스펙 5.3절에 따라 **대신 성공했다고 가정하지 않는다.** 실제 Galaxy 기기를 연결하고 아래를 수행한 뒤, 관측한 결과만 적는다.

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet run --project DexManager.Desktop
```

확인 항목:

1. 기기 목록에 연결된 기기가 나타난다
2. `Start DeX`를 누르면 scrcpy 창이 뜨고, 목록의 행에 `DeX` 표시가 붙는다
3. `Stop DeX`를 누르면 창이 닫히고 `DeX` 표시가 사라진다
4. 중지 뒤 `adb shell settings get global overlay_display_devices`가 `null`을 돌려준다 (overlay 회수 확인)
5. 슬롯 1에 앱 패키지를 넣고 `Start`를 누르면 그 앱 창이 뜬다
6. 슬롯 `Stop`으로 창이 닫힌다
7. 창을 닫으면 남은 scrcpy 프로세스가 없다 — `pgrep -fl scrcpy`가 비어야 한다
8. DeX 실행 중 창을 닫아도 overlay가 회수된다

4번과 8번이 `AGENTS.md`의 overlay 불변식이다. 실패하면 Task 3의 정리 경로를 다시 본다.

미확인으로 남는 항목은 `docs/KNOWN_ISSUES.md`에 적는다.

```markdown
## macOS GUI Phase 2 실기 검증 범위

Phase 2의 GUI DeX 시작·중지와 단일창 슬롯은 SM-F971N(One UI 9.0) 한 대에서
확인했다. 복수 기기 동시 DeX, USB에서 무선으로의 전환 중 명령, 슬롯 3개
동시 실행은 자동 테스트로만 덮여 있고 실기 확인이 남아 있다.
```

- [ ] **Step 7: 커밋한다**

```bash
git add .github/workflows/macos-portable.yml \
        docs/superpowers/specs/2026-09-03-macos-gui-design.md \
        docs/TODO.md docs/KNOWN_ISSUES.md
git commit -m "$(cat <<'EOF'
docs: close out macOS GUI Phase 2

워크플로 paths에 DexManager.Platform.Mac이 빠져 있어 그 프로젝트만 건드린
PR이 macOS 워크플로를 트리거하지 않았다. main에 있던 공백이다.

스펙 5.1절 베이스라인과 7절 Phase 표를 실행 결과로 갱신하고, 8.1절 공백 5를
종결한다. 실기 검증에서 확인하지 못한 범위는 KNOWN_ISSUES에 남긴다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Self-Review 결과

### 1. 스펙 커버리지

| 스펙 항목 | 대응 Task |
| :--- | :--- |
| 7절 Phase 2 "DeX 시작/중지" | Task 9 |
| 7절 Phase 2 "단일창 슬롯" | Task 10 |
| 7절 "실사용 가능" 완료 조건 | Task 12 + Task 13 Step 6 |
| 4.2절 ViewModel 레이어 (`DexModeViewModel`, `SingleWindowViewModel`) | Task 9·10에서 이름을 바꿔 반영 — 아래 이탈 기록 참조 |
| 4.2절 "전역 `TargetSerial`을 암묵적으로 쓰지 않는다" | Task 9 (행에서 serial을 가져온다) |
| 4.3절 스레딩 모델 (`IUiDispatcher` 경유, `Dispose`에서 구독 해제) | Task 8 (`Post` + 실행 시점 `_disposed` 재확인) |
| 4.5절 결정 1 (창 제어 미구현) | Global Constraints — 창 핸들 API 미사용 |
| 5.1절 안전망 | Global Constraints + Task 13 Step 2 |
| 5.2절 신규 테스트 (stub 주입, 세션 상태 전이) | Task 8·9·10 |
| 5.3절 실기 검증은 가정하지 않는다 | Task 13 Step 6 |
| 8.1절 공백 5 (`Settings` 조율) | Task 4 |
| `docs/TODO.md` 선행 정리 4건 | Task 1·3·4·5 |
| `docs/TODO.md` 설계 주의 3건 | Task 6·9·11 |
| `docs/TODO.md` 워크플로 필터 | Task 13 |

**스펙에서 의도적으로 이탈한 것:**

스펙 4.2절은 ViewModel 목록에 `DexModeViewModel`과 `SingleWindowViewModel`을 별도 타입으로 적었다. 이 계획은 DeX 명령을 `DeviceViewModel`에 직접 두고 슬롯만 `SingleWindowSlotViewModel`로 뺐다. 이유는 `docs/TODO.md`의 Phase 2 설계 주의가 더 구체적이기 때문이다 — "명령은 `DeviceViewModel`에 두고 serial을 행에서 가져온다." `DexModeViewModel`을 따로 만들면 그 ViewModel이 대상 기기를 어디선가 받아야 하고, 가장 쉬운 경로가 `host.SelectedSerial`이 되어 리뷰가 경고한 함정에 그대로 빠진다. Task 13에서 스펙 4.2절에 이 결정을 각주로 남긴다.

**Phase 2에서 다루지 않는 것(범위 밖임을 명시):**

- TUI를 기기별 런타임으로 바꾸는 것 — 관측 가능한 동작 변경이라 Global Constraints가 금지한다. `InteractiveHost`는 `_activeRuntime` 하나를 계속 쓴다. 코디네이터와 두 메커니즘이 공존하는 상태가 남으며, 이것은 Phase 3 이후의 정리 대상이다. Task 13에서 `docs/TODO.md`에 항목으로 남긴다.
- 설정 화면(Phase 3), 무선 ADB·파일 전송(Phase 4), 진단 창(Phase 5).
- 슬롯 설정의 영구 저장 — 이 Phase의 슬롯은 `AppSettings.VirtualDisplay`/`Scrcpy` 공통값을 쓰고 앱 패키지만 화면에서 받는다. 슬롯별 설정 저장은 Phase 3의 설정 화면과 함께 다룬다.

### 2. Placeholder 점검

"TBD", "적절한 오류 처리 추가", "Task N과 유사", 코드 없는 코드 단계 — 없음. 각 Task의 모든 코드 단계에 실제 코드가 들어 있다.

Task 12만 자동 테스트 없이 실행 확인으로 검증한다. 스펙 5.2절이 헤드리스 창 테스트를 "선택"으로 두었고, `AvaloniaUseCompiledBindingsByDefault`가 켜져 있어 `x:DataType` 불일치는 빌드가 잡는다.

Task 13 Step 2의 테스트 개수는 이 계획의 예측값이다. 해당 단계가 "반드시 실행 결과로 대체한다"고 명시한다.

### 3. 타입 일관성

- `DeviceViewModel` 생성자는 Task 8에서 3인자, Task 9에서 4인자로 두 번 넓어진다. Task 9 Step 4가 최종형을 전부 보여준다.
- `DeviceListViewModel` 생성자도 같은 순서로 넓어진다(Task 8: 3인자, Task 9: 4인자). Task 8 Step 4에서 기존 테스트 호출부를 고치라고 명시했고, Task 9 Step 5에서 다시 넓힌다.
- `IDeviceRuntimeCommands`의 4개 메서드 이름은 Task 9(정의) → Task 9 Step 5(구현) → Task 10(슬롯 소비) → `FakeDeviceCommands`(stub)에서 모두 동일하다.
- `SingleWindowSlotViewModel.ApplyRuntime(DeviceRuntimeSessionSnapshot)`은 Task 8의 `DeviceViewModel.ApplyRuntime`이 호출하는 시그니처와 일치한다. Task 8에서는 `Slots`가 비어 있어 호출이 no-op이고, Task 10이 채운다.
- `ApplicationHost.ShutdownAsync(string, string)`는 Task 3에서 정의되고 `InteractiveHost.ShutdownAsync()`가 소비한다. 반환형 `Task<IReadOnlyList<Exception>>`이 양쪽에서 같다.
- `DeviceRuntimeCoordinator.GetOrCreate(string identity, string serial)`의 인자 순서는 Task 7(정의)와 Task 9 Step 5(소비)에서 같다.
