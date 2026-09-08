# macOS GUI Phase 3 — SettingsWindow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Avalonia GUI에서 선택된 기기의 실행 설정(화면·스트림·경로)과 앱 전역 설정(테마·언어·키매핑)을 편집하고 영구 저장할 수 있게 한다. 스펙 7절 Phase 3의 완료 조건인 "설정 변경·영구 저장"에 도달한다.

**Architecture:** 설정은 두 계층이다 — **기기별 프로필**(`DeviceRunSettingsProfiles`, DeX 시작 시 `DexOrchestrator.GetDeviceRunSettings`가 읽음)과 **앱 전역**(테마·언어·키매핑·경로). 설정 화면은 선택된 기기의 프로필을 편집하되(없으면 `GetOrCreateDeviceRunSettings`로 전역 기본에서 파생), 전역 전용 항목은 별도 영역에 둔다. 모든 쓰기는 `ApplicationHost.UpdateSettings`를 거쳐 한 잠금 안에서 mutate + save 한다. 폼은 `Settings` 객체에 직접 양방향 바인딩하지 않고 ViewModel의 사본에 바인딩한 뒤 명시적 저장 시 `UpdateSettings`로 흘려보낸다 — 저장 경로의 `EnsureDefaults()` 정규화가 살아있는 객체를 건드리기 때문이다.

**Tech Stack:** .NET 8 (`global.json` SDK `8.0.130`, `rollForward: disable`), Avalonia `11.3.20`, CommunityToolkit.Mvvm `8.4.2`, xUnit `2.5.3`

**Spec:** `docs/superpowers/specs/2026-09-03-macos-gui-design.md`

**선행 계획:** `docs/superpowers/plans/2026-09-05-macos-gui-phase1.md`, `docs/superpowers/plans/2026-09-07-macos-gui-phase2.md`

## Global Constraints

- **Phase 3은 추가 전용이다.** 기존 TUI(`DexManager.Mac`)와 WinForms 포크(`DexManager/`)의 관측 가능한 동작을 바꾸지 않는다(스펙 7절).
- **`DexManager.ViewModels`는 Avalonia에 의존하지 않는다**(스펙 4.2절). 테마 적용처럼 Avalonia가 필요한 부분은 `DexManager.Desktop`에만 둔다.
- **모든 설정 쓰기는 `ApplicationHost.UpdateSettings(Action<AppSettings>)`를 거친다.** `Settings`를 직접 수정하지 않는다. 이 메서드는 mutate + save를 한 잠금 안에서 하고, 저장 경로가 `EnsureDefaults()`로 살아있는 `Settings` 객체를 정규화한다.
- **폼을 `Settings`(또는 그 하위 객체)에 직접 양방향 바인딩하지 않는다.** 저장 시 정규화가 화면 값을 덮어쓴다. ViewModel은 편집용 사본을 들고, 저장 시 그 값을 `UpdateSettings` 안에서 프로필/전역에 대입한다. 이것이 Phase 2 이연 항목 "`UpdateSettings` 정규화 문서화"가 경고한 바로 그 함정이다.
- **기기별 프로필은 `AppSettings.GetOrCreateDeviceRunSettings(identity)`로 얻는다.** 이것이 프로필을 조회하거나 전역 기본에서 파생 생성하는 유일한 API다. 프로필 목록을 직접 순회해 만들지 않는다.
- **DeX가 실행 중인 기기의 설정 변경은 다음 시작부터 적용된다.** `DexOrchestrator`는 시작 시점에 프로필을 읽는다. 실행 중 실시간 반영은 이 Phase의 비목표다 — 저장은 되지만 현재 세션에는 반영되지 않음을 UI가 알린다.
- **`ApplicationHost.SelectedSerial`은 진단 전용이다**(Phase 2 설계 주의). 설정 화면이 대상 기기를 아는 경로는 `DeviceListViewModel.SelectedDevice`의 `Identity`다. `SelectedSerial`을 읽지 않는다.
- **`dotnet`은 PATH에 없다.** 각 명령 앞에 `export PATH="$PATH:$HOME/.dotnet"`.
- **테스트 베이스라인은 병합 시점 기준 192 xUnit(`DexManager.ViewModels.Tests` 50 + `DexManager.Tests` 142) + 다중기기 39다.** 각 Task 종료 시 이 수 + 추가분이 전부 통과해야 한다. 브리프의 개수는 낡을 수 있으니 **관측한 수를 보고한다.**
  - `dotnet test DexManager.Mac.sln`
  - `dotnet run --project DexManager.MultiDeviceTests -c Release` — 마지막 줄 `All multi-device foundation tests passed: 39`
- **커밋 컨벤션:** conventional commits, 소문자 명령형. `Co-Authored-By` 트레일러는 각 세션의 지시를 따른다.
- **코드 스타일:** 한국어 XML doc 주석(`///`)과 한국어 근거 주석. xUnit `[Fact]`만.
- **실기 검증은 대신 성공했다고 가정하지 않는다**(스펙 5.3절, `AGENTS.md`). 테마·언어·키매핑의 실제 화면 반영은 하드웨어/실행 확인 항목으로 명시한다.

## 배경: 두 계층과 세 함정

Phase 2 실사용에서 드러난 세 가지가 이 Phase의 설계를 결정한다.

| # | 사실 | 대응 |
| :--- | :--- | :--- |
| 1 | 전역값을 바꿔도 이미 프로필이 있는 기기엔 안 먹는다 — `DexOrchestrator.GetDeviceRunSettings`가 프로필을 읽기 때문 | 화면은 **선택된 기기의 프로필**을 편집한다(Task 3~5) |
| 2 | `UpdateSettings` 저장 경로의 `EnsureDefaults()`가 살아있는 `Settings`를 정규화한다 | 폼은 사본에 바인딩, 저장 시에만 흘려보낸다(Task 2) |
| 3 | 테마·언어가 모델에 있으나 GUI가 적용하지 않는다(`RequestedThemeVariant="Default"` 하드코딩) | 테마 배선(Task 7), 언어 배선(Task 8) |

### 범주별 난이도와 배치 근거

사용자가 네 범주(값·경로·테마/언어·상호작용)를 모두 요청했다. 위험이 크게 다르므로 위험 순으로 배치한다.

| 범주 | 규모 | 위험 | Task |
| :--- | :--- | :--- | :--- |
| 화면/스트림 값 | 중 | 낮음 — Phase 2에서 JSON으로 검증됨 | 3~5 |
| 경로(Scrcpy/ADB) | 소 | 낮음 | 6 |
| 테마 | 소 | 중 — Avalonia 배선, 실행 확인 필요 | 7 |
| 언어 | 중 | 높음 — Avalonia resx 핫리로드 불가, 재시작 의미 | 8 |
| 상호작용(키매핑) | 대(130 멤버) | 높음 — macOS 검증 부담, 실기 필요 | 9~10 |

Task 8·9·10은 앞선 Task가 그린 후 착수하며, 각자 실기/실행 확인을 미확인으로 명시할 수 있다.

## File Structure

**신규:**

| 파일 | 책임 |
| :--- | :--- |
| `DexManager.ViewModels/Settings/EditableRunSettings.cs` | 기기 프로필의 편집용 사본 — 폼 바인딩 대상, 저장 시 프로필로 흘려보냄 |
| `DexManager.ViewModels/Settings/DisplayStreamSettingsViewModel.cs` | 화면·스트림 값 페이지 |
| `DexManager.ViewModels/Settings/PathsSettingsViewModel.cs` | Scrcpy·ADB 경로 페이지 |
| `DexManager.ViewModels/Settings/AppearanceSettingsViewModel.cs` | 테마·언어 페이지 |
| `DexManager.ViewModels/Settings/InteractionSettingsViewModel.cs` | 키매핑 페이지 |
| `DexManager.ViewModels/Settings/SettingsViewModel.cs` | 페이지 묶음 + 저장/취소 조율 + 대상 기기 |
| `DexManager.ViewModels/ISettingsGateway.cs` | ViewModel이 설정을 읽고 쓰는 경계(테스트가 stub) |
| `DexManager.ViewModels/SettingsGateway.cs` | `ApplicationHost.UpdateSettings` 기반 실제 구현 |
| `DexManager.Desktop/Views/SettingsWindow.axaml` + `.axaml.cs` | 설정 창(페이지 탭) |
| `DexManager.Desktop/ThemeApplier.cs` | `AppTheme` → `RequestedThemeVariant` 배선 |
| `DexManager.ViewModels.Tests/Settings/*.cs` | 페이지별 ViewModel 테스트 |
| `DexManager.ViewModels.Tests/FakeSettingsGateway.cs` | 게이트웨이 stub |

**수정:**

| 파일 | 변화 |
| :--- | :--- |
| `DexManager.ViewModels/ShellViewModel.cs` | 설정 창 열기 명령, `SettingsViewModel` 소유 |
| `DexManager.Desktop/Views/MainWindow.axaml` | 설정 열기 버튼 |
| `DexManager.Desktop/App.axaml.cs` | 시작 시 저장된 테마 적용 |
| `docs/TODO.md` | Phase 2 이연 항목 중 이 Phase가 닫는 것 체크, Phase 3 완료 기록 |

---

### Task 1: `ISettingsGateway` 경계와 stub

**Files:**
- Create: `DexManager.ViewModels/ISettingsGateway.cs`, `DexManager.ViewModels/SettingsGateway.cs`
- Create: `DexManager.ViewModels.Tests/FakeSettingsGateway.cs`
- Test: `DexManager.ViewModels.Tests/Settings/SettingsGatewayContractTests.cs`

**Interfaces:**
- Consumes: `ApplicationHost.UpdateSettings`, `ApplicationHost.Settings`, `AppSettings.GetOrCreateDeviceRunSettings(string identity)`
- Produces:
  - `AppSettings ISettingsGateway.Current { get; }` — 읽기 전용 스냅 (편집 사본을 만들 원본)
  - `DeviceRunSettingsProfile ISettingsGateway.GetRunProfile(string identity)`
  - `void ISettingsGateway.Update(Action<AppSettings> mutate)`
  - Task 2~10의 모든 저장이 이 경계를 통한다.

설정 편집·저장은 실제 파일 I/O와 `EnsureDefaults` 정규화를 유발한다. ViewModel 테스트가 이것을 부를 수 없다. 경계를 인터페이스로 끊어 페이지 로직을 파일 없이 검증한다.

`ISettingsGateway`:

```csharp
namespace DexManager.ViewModels;

/// <summary>
/// 설정 읽기·쓰기의 경계. 실제 구현은 ApplicationHost.UpdateSettings를 거쳐
/// 파일 저장과 EnsureDefaults 정규화를 유발하므로, 테스트는 stub을 넣어
/// 페이지 로직만 검증한다.
/// </summary>
public interface ISettingsGateway
{
    /// <summary>현재 설정. 편집용 사본을 만들 원본으로만 읽는다.</summary>
    AppSettings Current { get; }

    /// <summary>이 기기의 실행 프로필을 얻는다. 없으면 전역 기본에서 파생 생성.</summary>
    DeviceRunSettingsProfile GetRunProfile(string deviceIdentity);

    /// <summary>한 잠금 안에서 설정을 수정하고 저장한다.</summary>
    void Update(Action<AppSettings> mutate);
}
```

`SettingsGateway`는 `ApplicationHost`를 감싸 `Current => host.Settings`, `GetRunProfile(id) => host.Settings.GetOrCreateDeviceRunSettings(id)`, `Update(m) => host.UpdateSettings(m)`로 위임한다.

`FakeSettingsGateway`는 인메모리 `AppSettings`를 들고 `Update`가 mutate만 적용(정규화 없음)하며, `Update` 호출 횟수와 마지막 mutate 결과를 관측 가능하게 노출한다.

- [ ] Step 1: `SettingsGatewayContractTests` — `GetRunProfile`이 같은 identity에 같은 프로필을 돌려주고, `Update`가 mutate를 적용하는지 (RED)
- [ ] Step 2: 실패 확인
- [ ] Step 3: 세 파일 구현
- [ ] Step 4: 통과 확인, 전체 스위트
- [ ] Step 5: 커밋 `feat(viewmodels): add the settings gateway boundary`

---

### Task 2: `EditableRunSettings` — 정규화 함정을 막는 편집 사본

**Files:**
- Create: `DexManager.ViewModels/Settings/EditableRunSettings.cs`
- Test: `DexManager.ViewModels.Tests/Settings/EditableRunSettingsTests.cs`

**Interfaces:**
- Consumes: `DeviceRunSettingsProfile`, `VirtualDisplaySettings`, `ScrcpySettings`
- Produces:
  - `EditableRunSettings.FromProfile(DeviceRunSettingsProfile)` — 값 복사
  - `void EditableRunSettings.ApplyTo(DeviceRunSettingsProfile target)` — 편집값을 프로필에 대입
  - `[ObservableProperty]` 필드: `Width`, `Height`, `Dpi`, `BitRate`, `MaxFps`, `TurnScreenOff`, `StayAwake` (그리고 Task 3에서 쓰는 검증 상태)
  - Task 3의 페이지가 이 타입에 바인딩한다.

**핵심:** 이 타입이 Global Constraint의 "폼을 Settings에 직접 바인딩하지 않는다"를 구현한다. 프로필의 값을 **복사**해 들고, 저장 시 `ApplyTo`로 되돌려 넣는다. 그 대입은 `Update`의 잠금 안에서 일어나므로, 정규화는 대입 뒤에 실행되고 편집 중 화면은 정규화의 영향을 받지 않는다.

- [ ] Step 1: `FromProfile`이 값을 복사(참조 공유 아님)하고 `ApplyTo`가 되돌려 넣는지, 그리고 `FromProfile` 후 원본 프로필을 바꿔도 사본이 안 바뀌는지 검증 (RED)
- [ ] Step 2~4: 구현·통과
- [ ] Step 5: 커밋 `feat(viewmodels): add an editable copy of run settings`

---

### Task 3: 화면/스트림 값 페이지 + 검증

**Files:**
- Create: `DexManager.ViewModels/Settings/DisplayStreamSettingsViewModel.cs`
- Test: `DexManager.ViewModels.Tests/Settings/DisplayStreamSettingsViewModelTests.cs`

**Interfaces:**
- Consumes: `EditableRunSettings`(Task 2), `ISettingsGateway`(Task 1)
- Produces:
  - `DisplayStreamSettingsViewModel(string identity, ISettingsGateway gateway)`
  - `EditableRunSettings Values { get; }`
  - `bool HasChanges { get; }`, `bool IsValid { get; }`, `string ValidationMessage { get; }`
  - `SaveCommand`, `ResetToGlobalDefaultsCommand`
  - Task 11의 `SettingsViewModel`이 이 페이지를 담는다.

Phase 2에서 JSON으로 직접 고쳤던 값들(`2560×1440/240, 16M`)을 화면에서 편집한다. `SaveCommand`는 `gateway.Update(s => Values.ApplyTo(s.GetOrCreateDeviceRunSettings(identity)))`를 부른다. `ResetToGlobalDefaultsCommand`는 전역 `VirtualDisplay`/`Scrcpy`에서 값을 다시 채운다.

**검증:** Width/Height/Dpi/MaxFps는 양의 정수, BitRate는 `\d+[MK]?` 형식. 잘못된 값이면 `IsValid == false`, `SaveCommand.CanExecute == false`. 검증이 실제로 저장을 막는지 변이 검증한다.

- [ ] Step 1: 저장이 프로필에 값을 대입하는지, 잘못된 DPI가 저장을 막는지, 재설정이 전역값을 채우는지 (RED, 각 [Fact])
- [ ] Step 2~4: 구현·통과·변이 검증
- [ ] Step 5: 커밋 `feat(viewmodels): edit display and stream values per device`

---

### Task 4: 단일창 슬롯 설정 페이지

**Files:**
- Modify: `DexManager.ViewModels/Settings/EditableRunSettings.cs` (슬롯 편집 추가)
- Create: `DexManager.ViewModels/Settings/SlotSettingsViewModel.cs`
- Test: `DexManager.ViewModels.Tests/Settings/SlotSettingsViewModelTests.cs`

**Interfaces:**
- Consumes: `SingleWindowSlotSettings`(프로필의 `SingleWindowSlots`), `ISettingsGateway`
- Produces: 슬롯 1~3 각각의 편집(앱 패키지, 해상도 override, StayAwake 등), 저장 시 프로필의 `SingleWindowSlots`에 대입.

Phase 2의 `SingleWindowSlotViewModel`은 **실행**용(패키지 입력 + 시작), 이건 **설정**용(슬롯 기본값 영구 저장)이다. 둘을 혼동하지 않는다 — 다른 타입, 다른 화면.

- [ ] Step 1~5: 위 패턴 동일. 커밋 `feat(viewmodels): edit single window slot defaults per device`

---

### Task 5: 실행 중 기기 경고

**Files:**
- Modify: `DexManager.ViewModels/Settings/SettingsViewModel.cs`(Task 11에서 생성되므로 순서 주의 — 이 Task는 11 뒤로 갈 수 있음), 또는 각 페이지 VM
- Test: 해당 테스트

**Interfaces:**
- Consumes: `DeviceRuntimeSessionRegistry`(Phase 2), 대상 기기 identity
- Produces: `bool IsTargetDexRunning { get; }` — 대상 기기에 DeX가 도는 동안 true. 저장은 허용하되 "다음 시작부터 적용됨" 문구를 노출.

Global Constraint의 "실행 중 변경은 다음 시작부터"를 UI로 정직하게 알린다. `DeviceRuntimeSessionRegistry.Changed`를 구독하고 `IUiDispatcher`로 마샬링, `Dispose`에서 해제 — Phase 2의 `DeviceViewModel`과 같은 패턴.

- [ ] Step 1~5. 커밋 `feat(viewmodels): warn when settings change while DeX is running`

---

### Task 6: 경로 설정 페이지 (전역)

**Files:**
- Create: `DexManager.ViewModels/Settings/PathsSettingsViewModel.cs`
- Test: `DexManager.ViewModels.Tests/Settings/PathsSettingsViewModelTests.cs`

**Interfaces:**
- Consumes: `PathSettings`(`Settings.Paths`), `ISettingsGateway`
- Produces: Scrcpy·ADB 경로 편집, "번들 기본값으로 재설정", 존재하지 않는 경로 경고. 저장 시 `s.Paths`에 대입.

경로는 전역 전용이다(기기별 아님). 파일 존재 확인은 순수 함수로 두어 테스트가 stub 파일 시스템 없이 검증할 수 있게 한다 — 또는 존재 확인은 실행 확인으로 미루고 ViewModel은 문자열만 다룬다.

- [ ] Step 1~5. 커밋 `feat(viewmodels): edit scrcpy and adb paths`

---

### Task 7: 테마 배선

**Files:**
- Create: `DexManager.ViewModels/Settings/AppearanceSettingsViewModel.cs`(테마 부분), `DexManager.Desktop/ThemeApplier.cs`
- Modify: `DexManager.Desktop/App.axaml.cs`
- Test: `DexManager.ViewModels.Tests/Settings/AppearanceSettingsViewModelTests.cs`

**Interfaces:**
- Consumes: `AppTheme { Auto, Light, Dark }`, `ISettingsGateway`
- Produces:
  - `AppearanceSettingsViewModel.Theme` 선택 + 저장
  - `ThemeApplier.Apply(AppTheme)` — `Application.Current.RequestedThemeVariant`를 설정 (`Auto → Default`, `Light → Light`, `Dark → Dark`)
  - `App.axaml.cs`가 시작 시 저장된 테마를 적용, 저장 시 즉시 재적용

**미확인 항목:** 실제 화면이 라이트/다크로 바뀌는지는 실행 확인이다. GUI를 띄울 수 없는 환경에서는 미확인으로 명시한다. ViewModel 로직(선택·저장)과 `ThemeApplier`의 매핑은 단위 테스트로 고정하되, `Application.Current` 접근부는 `DexManager.Desktop`에만 두어 ViewModel 테스트가 Avalonia 없이 돈다.

- [ ] Step 1: 테마 선택·저장 로직 테스트, 매핑 테스트 (RED)
- [ ] Step 2~4: 구현. `App.axaml`의 `RequestedThemeVariant="Default"` 하드코딩을 시작 시 저장값 적용으로 대체
- [ ] Step 5: 커밋 `feat(desktop): apply the saved theme`
- [ ] Step 6: 실행 확인 시도, 결과(또는 미확인)를 보고에 명시

---

### Task 8: 언어 배선 (재시작 의미 명시)

**Files:**
- Modify: `AppearanceSettingsViewModel.cs`(언어 부분)
- Modify: `DexManager.Desktop/App.axaml.cs` 또는 `Program.cs`(시작 시 언어 적용)
- Test: 해당 테스트

**Interfaces:**
- Consumes: `AppLanguage`, `LocalizationService`, `ISettingsGateway`
- Produces: 언어 선택 + 저장. 시작 시 저장된 언어를 `LocalizationService`에 적용.

**높은 위험 — 정직하게 다룬다.** Avalonia는 이미 로드된 XAML의 resx 문자열을 핫스왑하지 못한다. 언어 변경은 **다음 실행부터** 반영된다. UI가 "재시작 후 적용됨"을 명시하고, 실행 중 UI 전체를 다시 그리려 시도하지 않는다. `LocalizationService`가 `CultureInfo`를 바꾸는 방식이면 새로 생성되는 문자열에는 반영되나 이미 그려진 창에는 반영되지 않는다 — 이 경계를 UI 문구로 정확히 표현한다.

만약 조사 결과 `LocalizationService`가 런타임 언어 전환을 지원하지 않으면(문자열이 시작 시 한 번만 로드), 저장만 하고 "재시작 필요"만 노출한다. 구현자는 착수 시 `LocalizationService`의 실제 전환 지원 여부를 확인하고, 지원 범위를 보고에 명시한다.

- [ ] Step 1~5. 커밋 `feat(desktop): persist the language selection`
- [ ] Step 6: 재시작 의미와 지원 범위를 보고에 명시

---

### Task 9: 키매핑 모델 조사와 뷰모델 골격

**Files:**
- Create: `DexManager.ViewModels/Settings/InteractionSettingsViewModel.cs`
- Test: `DexManager.ViewModels.Tests/Settings/InteractionSettingsViewModelTests.cs`

**Interfaces:**
- Consumes: `KeyMappingSettings`(130 멤버), `ISettingsGateway`
- Produces: 키매핑 편집의 ViewModel 표면. **전역 전용.**

**착수 전 필수 조사.** `KeyMappingSettings`는 130개 public 멤버다. 무엇을 사용자가 편집해야 하고 무엇이 파생/내부값인지 구분한다 — WinForms `SettingsForm.Interaction.cs`(2,082바이트로 작음)와 `SettingsForm.cs`의 키매핑 UI가 실제로 노출한 항목만 대상으로 삼는다. 130개 전부를 화면에 올리지 않는다. 구현자는 조사 결과(편집 대상 목록)를 먼저 보고하고, 컨트롤러의 확인을 받은 뒤 골격을 구현한다.

이 Task는 **골격만** — 편집 대상 필드 집합의 로드/저장 왕복과 검증. 실제 키 캡처 UX는 Task 10.

- [ ] Step 0: `KeyMappingSettings` 편집 대상 조사 → 보고 → 확인
- [ ] Step 1~5: 확인된 필드 집합의 왕복 저장 테스트·구현. 커밋 `feat(viewmodels): add the interaction settings skeleton`

---

### Task 10: 키 캡처 UX와 macOS 검증

**Files:**
- Modify: `InteractionSettingsViewModel.cs`, `DexManager.Desktop/Views/SettingsWindow.axaml`(키 입력 필드)
- Test: 해당 테스트

**Interfaces:**
- Consumes: Task 9의 골격
- Produces: 키 조합 입력·표시. macOS 키 이벤트 → 저장 표현 매핑.

**높은 위험 — macOS 검증 부담.** `AGENTS.md`가 지적한 대로 키 보정은 scan code / SDL3 오른쪽 Shift 등 플랫폼 특수성이 있다. macOS에서 실제 키 캡처가 올바른 값을 저장하는지는 **실기/실행 확인**이다. ViewModel의 매핑 로직은 단위 테스트로 고정하되, 실제 키 이벤트 경로는 미확인으로 명시한다. 이 Task가 blocked되면 Phase 3의 나머지(1~8)는 이미 완성이므로 독립적으로 병합 가능하다.

- [ ] Step 1~5. 커밋 `feat(desktop): capture key combinations in settings`
- [ ] Step 6: macOS 키 캡처 실기 확인 시도, 결과/미확인 명시

---

### Task 11: `SettingsViewModel` 조립과 저장/취소 조율

**Files:**
- Create: `DexManager.ViewModels/Settings/SettingsViewModel.cs`
- Modify: `DexManager.ViewModels/ShellViewModel.cs`
- Test: `DexManager.ViewModels.Tests/Settings/SettingsViewModelTests.cs`

**Interfaces:**
- Consumes: 페이지 ViewModel 전부(Task 3~10), `ISettingsGateway`, `DeviceListViewModel.SelectedDevice`
- Produces:
  - `SettingsViewModel` — 페이지 묶음, 대상 기기 identity, 전역 `SaveAll`/`Cancel`, 페이지별 `HasChanges` 집계
  - `ShellViewModel.OpenSettingsCommand`

대상 기기는 `Devices.SelectedDevice?.Identity`에서 온다(`SelectedSerial` 아님). 선택이 바뀌면 기기별 페이지가 새 프로필로 다시 로드된다. `Cancel`은 편집 사본을 버린다(원본 미변경). `SaveAll`은 각 페이지의 저장을 한 번씩 흘려보낸다.

- [ ] Step 1~5. 커밋 `feat(viewmodels): assemble the settings view model`

---

### Task 12: `SettingsWindow` 뷰와 MainWindow 진입점

**Files:**
- Create: `DexManager.Desktop/Views/SettingsWindow.axaml` + `.axaml.cs`
- Modify: `DexManager.Desktop/Views/MainWindow.axaml`, `DexManager.Desktop/App.axaml.cs`(창 열기 배선)
- Test: 빌드(컴파일 바인딩) + 실행 확인

**Interfaces:**
- Consumes: `SettingsViewModel`, 페이지 ViewModel의 표면
- Produces: 탭 페이지로 구성된 설정 창. 스펙 7절 완료 조건.

Phase 2의 Task 12와 같은 성격 — 자동 테스트 없음, 빌드(컴파일 바인딩)가 바인딩 유효성을 잡고 나머지는 실행 확인. `TextBox.Text`의 양방향 바인딩(값 편집)을 특히 확인한다.

- [ ] Step 1~3: XAML 작성·빌드·커밋 `feat(desktop): add the settings window`
- [ ] Step 4: 실행 확인 시도, 미확인 항목 명시

---

### Task 13: 문서 갱신과 Phase 3 종료

**Files:**
- Modify: `docs/TODO.md`, `docs/superpowers/specs/2026-09-03-macos-gui-design.md`(7절 Phase 3 완료), `docs/KNOWN_ISSUES.md`

**Interfaces:**
- Consumes: Task 1~12
- Produces: Phase 3 종료 기록. Phase 2 이연 항목 중 이 Phase가 닫은 것("`UpdateSettings` 정규화 문서화") 체크. 미확인 실행/실기 항목(테마·언어·키매핑) 기록.

- [ ] Step 1~2. 커밋 `docs: close out macOS GUI Phase 3`

---

## Self-Review 결과

### 1. 스펙 커버리지

| 스펙 항목 | 대응 |
| :--- | :--- |
| 7절 Phase 3 "SettingsWindow (연결·값·상호작용·테마)" | Task 3~10, 12 |
| 7절 완료 조건 "설정 변경·영구 저장" | Task 1(게이트웨이) + Task 12(창) |
| 4.2절 "SettingsViewModel + 페이지별 하위 ViewModel" | Task 11 + 페이지 VM |
| 4.2절 "전역 TargetSerial 암묵 사용 금지" | 대상은 `SelectedDevice.Identity`(Global Constraint) |
| 8.1절 이연 "UpdateSettings 정규화 문서화" | Task 2(사본 바인딩) + Task 13(문서) |

**의도적 범위 결정:**
- 스펙 4.1절이 `SettingsForm`에 포함했던 **진단·디스플레이 클리너·기기 폴더 탐색**은 Phase 3에 넣지 않는다 — 스펙 7절이 각각 Phase 5·Phase 5로 배정했다. "연결" 설정(무선 ADB)은 스펙이 Phase 4로 배정했으므로 이 Phase의 "연결"은 경로/값에 한정한다.
- 키매핑(Task 9~10)은 규모(130 멤버)와 macOS 검증 부담이 커, 앞 Task와 독립적으로 병합 가능하게 마지막에 둔다. blocked돼도 Task 1~8·11~12는 완성 상태다.

### 2. Placeholder 점검
Task 9의 키매핑 편집 대상은 조사 후 확정하는 것이 의도된 설계다(130개 전부를 미리 나열하지 않음). 그 외 Task는 인터페이스와 접근이 확정돼 있다. 각 Task의 verbatim 코드는 dispatch 시점에 구현자가 채운다 — 이 계획은 Phase 2 계획과 달리 페이지별 상세 코드를 착수 시 조사와 함께 확정하는 항목(테마 매핑, 언어 전환 지원, 키매핑 필드)을 포함하므로, 해당 Task는 Step 0 조사를 명시했다.

### 3. 타입 일관성
- `ISettingsGateway`의 세 멤버(`Current`/`GetRunProfile`/`Update`)가 Task 1에서 정의되고 Task 3~11 전부가 소비한다.
- `EditableRunSettings.FromProfile`/`ApplyTo`(Task 2)를 Task 3·4의 페이지가 쓴다.
- 대상 기기 identity는 어디서나 `SelectedDevice.Identity`이며 `SelectedSerial`을 읽지 않는다(Global Constraint + Task 11).
