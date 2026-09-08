# DX Manager for macOS — 네이티브 GUI 설계

- 작성일: 2026-09-03
- 상태: 설계 승인됨, 구현 계획 대기
- 대상: `DexManager.Desktop` (신규 Avalonia GUI)

## 1. 배경과 목표

DX Manager는 현재 두 개의 사용자 인터페이스를 가진다.

| 플랫폼 | UI | 런타임 | 위치 |
| :--- | :--- | :--- | :--- |
| Windows | WinForms GUI (약 14,122줄) | .NET Framework 4.6.2 | `DexManager/Forms/` |
| macOS | ANSI 터미널 TUI (1,104줄) | .NET 8 | `DexManager.Mac/Hosting/InteractiveHost.cs` |

macOS 사용자는 터미널 대시보드만 사용할 수 있다. 이 설계의 목표는 **Windows GUI와 기능적으로 동등한 네이티브 macOS GUI**를 추가하는 것이다.

부차 목표로, 장기적으로 Windows UI까지 같은 코드베이스로 흡수할 수 있는 구조를 남긴다. 통합을 실제로 수행할지는 이 설계의 범위 밖이며, 통합하지 않더라도 손해가 없는 구조를 택한다.

### 비목표 (Non-goals)

- Windows WinForms 앱(`DexManager/`)의 수정 또는 마이그레이션
- 기존 macOS TUI(`DexManager.Mac`)의 제거 — 유지한다
- Apple Developer ID 서명·notarization 도입 (별도 과제, `docs/KNOWN_ISSUES.md` 참조)
- 신규 기능 추가 — 기존 기능의 GUI 제공에 한정한다
- **전송 상태창을 scrcpy 창 옆에 배치하는 UX** — 근거는 2.3절, 결정은 4.5절
- **`IPlatformService`의 창 제어 API 구현** — 근거는 2.3절

## 2. 현황 분석

### 2.1 코드 포크 상태

`DexManager`(WinForms)는 `DexManager.Core`를 **참조하지 않는다.** `DexManager.csproj`의 유일한 `ProjectReference`는 `DexManager.AdbProxy`이며, Core와 동일한 이름의 소스가 양쪽에 복제되어 있다.

| 상태 | 파일 수 |
| :--- | ---: |
| Windows == Core (동일) | 45 |
| Windows != Core (갈라짐) | 20 |
| Core에만 존재 | 9 |

갈라진 20개에는 `DexOrchestrator`, `SettingsService`, `ScrcpyService`, `FileTransferCoordinator`, `DeviceMonitorService`, `SingleWindowService`, `EnvironmentCheckService` 등 핵심 서비스가 포함된다.

**원인**: Core는 `net8.0`, Windows는 `net462`로 타깃 런타임이 다르다. C# 12 문법과 .NET 8 전용 API가 Windows 측에서 사용 불가하다.

**이 설계의 대응**: 포크를 해소하지 않는다. 대신 신규 GUI가 `DexManager.Core`만 참조하고 WinForms 포크에는 어떤 의존도 만들지 않는 **격리선**을 유지한다. 포크 해소(Core 멀티타깃)는 별도 과제로 남긴다.

### 2.2 재사용 가능한 자산

- `DexManager.Core/Platform/`에 플랫폼 추상화 5종이 이미 존재한다: `IPlatformService`, `IPathProvider`, `ICaptureService`, `IKeyboardService`, `IAutoStartService`
- `DexManager.Mac/Platform/`에 위 5종의 macOS 구현이 모두 존재한다
- `InteractiveHost` 생성자가 18개 서비스를 조립하는 사실상의 컴포지션 루트 역할을 한다
- xUnit 95개 + 다중기기 회귀 39개 테스트가 통과 중이다

### 2.3 창 제어 API 구현 실태 조사 (2026-09-03)

`MacPlatformService`의 창 제어 8개는 모두 스텁이다.

| 메서드 | Mac 구현 | 결과 |
| :--- | :--- | :--- |
| `IsWindow` | `handle != IntPtr.Zero` | 항상 true |
| `IsWindowVisible` | `handle != IntPtr.Zero` | 항상 true |
| `IsIconic` | `false` 고정 | 최소화 감지 불가 |
| `ShowWindow` | no-op | 주석만 존재 |
| `SetForegroundWindow` | no-op | 주석만 존재 |
| `GetForegroundWindow` | `IntPtr.Zero` | 포그라운드 창 판별 불가 |
| `GetClientRect` | 1920×1080 하드코딩 + `return false` | 명시적 실패 신호 |
| `GetWindowRect` | 1920×1080 하드코딩 + `return false` | 명시적 실패 신호 |

`ShowWindow`/`SetForegroundWindow`의 주석은 "Handled via macOS process / AppleScript activation when needed"라고 기술하나, 코드베이스에 `osascript`/AppleScript 호출은 존재하지 않는다.

**실사용 범위**: 창 제어 API 중 실제로 호출되는 것은 `IsWindow` 하나뿐이며, 호출 지점은 `ScrcpyService.cs:1070`과 `SingleWindowService.cs:1123` 두 곳(모두 `QueueWindowMonitor` 내부)이다. TUI는 창 제어 API를 전혀 호출하지 않는다. WinForms는 `IPlatformService`를 사용하지 않고 `NativeMethods`로 직접 P/Invoke한다. 나머지 7개는 인터페이스를 경유해 호출되는 지점이 없다.

**`QueueWindowMonitor`의 macOS 동작**: macOS에서 `Process.MainWindowHandle`은 항상 0이므로 Core는 `handle = (IntPtr)process.Id`(PID)로 대체한다(`ScrcpyService.cs:872-878`). 따라서 `IsWindow(handle)`는 항상 true가 되어 창 닫힘을 감지하지 못한다.

그러나 주 정리 경로는 별도로 존재한다. 두 서비스 모두 `process.EnableRaisingEvents = true; process.Exited += Process_Exited`를 등록하며(`ScrcpyService.cs:604`, `SingleWindowService.cs:412`), `Process_Exited`(`ScrcpyService.cs:821`)가 세션 정리·전송 세션 종료·상태 리셋·`RunningChanged` 발화를 수행한다. 이 경로는 크로스플랫폼으로 동작한다. scrcpy 창을 닫으면 프로세스가 종료되므로 정리가 이루어진다.

스레드 누수도 없다. `Process_Exited`가 `_process = null`로 설정하면 감시 루프가 다음 순회(100ms)에 스스로 반환한다.

**결론**: `QueueWindowMonitor`는 Windows 전용 보조 안전망이며, macOS는 `Process.Exited` 주 경로만으로 충분하다. 창 제어 미구현은 현재 기능 결함을 일으키지 않는다.

**WinForms의 창 제어 실사용처**: `MainForm.TransferWindows.cs` 한 곳뿐이며, 기능은 전송 상태창을 scrcpy 창 옆에 배치하고 소유 창으로 결속하는 것이다(`ResolveTransferTarget` 106행, `TryPositionBesideTarget` 136행). 모든 경로가 실패 시 `window.Show()` 기본 배치로 폴백하므로 부가 UX에 해당한다.

### 2.4 프레임워크 선택 근거

**선택: Avalonia 11**

| 후보 | 판단 |
| :--- | :--- |
| **Avalonia 11** | 채택. macOS에 Objective-C++ 네이티브 백엔드를 사용하며 다중 창·Topmost·트레이를 지원한다. Windows/Linux로 확장 가능해 통합 목표에 부합한다. |
| .NET MAUI | 기각. macOS 지원이 Mac Catalyst 기반이라 데스크톱 다중 창 지원이 빈약하다. DX Manager는 미니 컨트롤바·설정창·전송 상태창 등 다중 창이 필수다. |
| SwiftUI + C# 브리지 | 기각. Mac UX는 우수하나 브리지 계층이 새로 필요하고 Windows 통합 목표와 상충한다. |

### 2.5 알려진 제약: Windows 7 지원

Avalonia를 포함한 크로스플랫폼 .NET UI는 데스크톱에서 **.NET 8.0 이상**을 요구한다. .NET 7부터 Windows 7/8.1 지원이 중단되었고, Windows 7을 지원한 마지막 릴리스는 .NET 6(2024-11-12 지원 종료)이다.

`AGENTS.md`는 "대상은 64비트 Windows 7 SP1, 8.1, 10과 11"을 불변 조건으로 명시한다. 따라서 **장래의 Windows UI 통합은 Windows 7/8.1 지원 포기를 전제로 한다.**

이 결정은 통합을 실제로 착수하는 시점에 내리며, 본 설계(macOS GUI 추가)는 Windows 지원 범위에 아무 영향을 주지 않는다.

참고:
- [Avalonia — Supported Platforms](https://github.com/avaloniaui/avalonia-docs/blob/main/docs/supported-platforms.mdx)
- [.NET 8.0 — Supported OS versions](https://github.com/dotnet/core/blob/main/release-notes/8.0/supported-os.md)
- [.NET support for Windows 7 and 8.1 will end in January 2023](https://github.com/dotnet/core/issues/7556)

## 3. 아키텍처

### 3.1 프로젝트 구조

```
DexManager.Core          (net8.0, lib)  기존 엔진
  └ Platform/I*.cs                      기존 추상화 5종 — 재사용
  └ Hosting/ApplicationHost.cs          신규: 서비스 조립

DexManager.Platform.Mac  (net8.0, lib)  완료(Phase 0): DexManager.Mac/Platform/에서 이동

DexManager.ViewModels    (net8.0, lib)  신규: UI 로직·상태, Avalonia 비의존

DexManager.Mac           (net8.0, exe)  기존 TUI — 유지, 참조만 교체
DexManager.Desktop       (net8.0, exe)  신규: Avalonia GUI
```

### 3.2 의존 방향

```
DexManager.Desktop ──┬──> DexManager.ViewModels ──> DexManager.Core
                     └──> DexManager.Platform.Mac ─┘

DexManager.Mac ─────────> DexManager.Platform.Mac ─┘

DexManager (WinForms 포크) ── 연결 없음
```

순환 의존이 없으며, WinForms 포크는 어떤 신규 프로젝트와도 연결되지 않는다.

### 3.3 `ApplicationHost` 추출

`InteractiveHost` 생성자의 서비스 조립 로직을 `DexManager.Core/Hosting/ApplicationHost.cs`로 이동한다. 플랫폼 구현은 생성자 주입으로 받으므로 Core는 플랫폼 중립을 유지한다.

```csharp
public sealed class ApplicationHost : IDisposable
{
    public ApplicationHost(
        IPlatformService platform,
        IPathProvider paths,
        ICaptureService capture,
        IKeyboardService keyboard,
        IAutoStartService autoStart);

    public AdbService Adb { get; }
    public WirelessAdbService WirelessAdb { get; }
    public DeviceMonitorService DeviceMonitor { get; }
    public PhysicalDeviceRegistry DeviceRegistry { get; }
    public DeviceRuntimeSessionRegistry RuntimeSessions { get; }
    public SettingsService Settings { get; }
    public LogService Log { get; }
    // ... 조립된 서비스 노출
}
```

**효과**: TUI와 GUI가 동일한 조립·수명주기·정리 경로를 공유한다. Scrcpy 시작 직렬화, 프로세스 정리, overlay cleanup 등 `AGENTS.md`의 불변 조건이 단일 지점에서만 보장되면 되고, 두 호스트가 갈라질 위험이 사라진다.

`InteractiveHost`는 `ApplicationHost`를 소비하는 형태로 축소한다. 관측 가능한 동작은 변경하지 않는다.

### 3.4 `DexManager.Platform.Mac` 분리 근거

현재 macOS 플랫폼 서비스는 실행 파일 프로젝트 안에 있어 다른 프로젝트가 정상적으로 참조할 수 없고, `DexManager.Tests`가 `InternalsVisibleTo`로만 접근한다. 라이브러리로 분리하면 GUI 참조와 정상적인 테스트가 모두 가능해진다.

네임스페이스(`DexManager.Mac.Platform`)는 유지해 기존 TUI 코드 변경을 최소화한다.

## 4. UI 설계

### 4.1 화면 인벤토리

| Windows 자산 | 줄 수 | macOS GUI 대응 |
| :--- | ---: | :--- |
| `MainForm.*` (13 partial) | 6,016 | `MainWindow` + 기능별 ViewModel |
| `SettingsForm.*` (9 partial) | 3,490 | `SettingsWindow` + 페이지별 ViewModel |
| 커스텀 컨트롤 인프라 | 2,224 | **불필요** — Avalonia 스타일 시스템으로 대체 |
| 전송 상태창 2종 | 666 | `TransferStatusWindow` |
| `MiniControlBarForm` | 491 | `MiniControlBarWindow` (`Topmost`) |
| `EnvironmentCheckForm` | 392 | `DiagnosticsWindow` |
| 캡처 영역선택 2종 | 232 | 투명 전체화면 오버레이 창 |
| `DeviceFolderBrowserForm` | 228 | `DeviceFolderBrowserWindow` |
| `ThirdPartyLicensesForm` | 205 | About 페이지 |
| `LogForm` | 178 | `LogWindow` |
| **합계** | **14,122** | |

커스텀 컨트롤 인프라 2,224줄(`CustomInputControls` 984, `ThemedControls` 512, `CustomValueInputControls` 330, `CustomDropDownControls`, `ThemeColors`, `UiFonts`, `UiWindowStyle`)은 WinForms에 테마·커스텀 입력 컨트롤이 없어 직접 구현한 코드다. Avalonia는 스타일 시스템·테마·다크모드를 기본 제공하므로 XAML 스타일 정의로 대체한다.

`MainForm.Layout.cs`(438줄) 같은 좌표 계산 코드도 Avalonia의 `Grid`/`StackPanel` 선언으로 대체된다.

### 4.2 ViewModel 레이어

`CommunityToolkit.Mvvm`(소스 제너레이터 기반)을 사용한다. Avalonia에 의존하지 않으므로 Windows 통합 시 그대로 재사용 가능하다.

`AGENTS.md`는 불필요한 새 의존성 추가를 금하나, 이 패키지는 UI 프레임워크 도입에 수반되는 표준 의존성으로 판단해 채택한다. 직접 `INotifyPropertyChanged`를 구현하는 대안 대비 보일러플레이트가 크게 줄고, `netstandard2.0`을 지원해 장래 Windows 통합 시에도 사용 가능하다. Core 엔진에는 도입하지 않고 `DexManager.ViewModels`에만 한정한다.

```
DexManager.ViewModels/
  ShellViewModel             앱 전역 상태, 활성 기기 선택
  DeviceListViewModel        연결된 기기 목록
  DeviceViewModel            기기 1대의 런타임 상태
  DexModeViewModel           DeX 시작/중지
  SingleWindowViewModel      단일창 슬롯 1~3
  WirelessAdbViewModel       무선 ADB 연결 관리
  FileTransferViewModel      전송 큐·진행률
  DiagnosticsViewModel       환경 점검·진단 리포트
  SettingsViewModel          + 페이지별 하위 ViewModel
  LogViewModel
  MiniControlBarViewModel
```

ViewModel은 `ApplicationHost`가 노출한 Core 서비스를 소비한다. 기기별 상태는 `AGENTS.md`의 복수 기기 불변 조건에 따라 **전역 `TargetSerial`을 암묵적으로 사용하지 않고** 명시적 세션 또는 serial을 전달한다.

### 4.3 스레딩 모델

`DeviceMonitorService.DeviceConnected` 등 Core 이벤트는 백그라운드 스레드에서 발생한다. `AGENTS.md`의 불변 조건은 다음과 같다.

> 백그라운드 작업 결과로 UI를 갱신할 때 WinForms UI 스레드 규칙과 폼 종료 경합을 고려한다.

ViewModel이 특정 UI 프레임워크에 묶이지 않도록 추상화를 둔다.

```csharp
// DexManager.ViewModels/IUiDispatcher.cs
public interface IUiDispatcher
{
    bool IsOnUiThread { get; }
    void Post(Action action);
    Task InvokeAsync(Func<Task> action);
}
```

| 소비자 | 구현 |
| :--- | :--- |
| `DexManager.Desktop` | `AvaloniaUiDispatcher` (`Dispatcher.UIThread`) |
| 테스트 | 즉시 실행 stub |
| 장래 Windows | `WinFormsUiDispatcher` (`Control.Invoke`) |

**창 종료 경합 방지 규칙**:
- Core 이벤트 구독은 ViewModel `Dispose`에서 반드시 해제한다
- `Post` 실행 시점에 대상 창이 이미 닫혔는지 확인한다

### 4.4 macOS 고유 고려사항

- **화면 기록 권한**: `MacCaptureService`가 `screencapture` CLI를 호출하므로 macOS 화면 기록 권한이 필요하다. 영역 선택 오버레이도 동일하다. 첫 실행 시 권한 안내 UI가 필요하다.
- **권한 주체 변경**: 권한은 `.app` 번들 식별자에 귀속된다. 기존 CLI 바이너리와 주체가 달라지므로 GUI 첫 실행 시 권한 재승인이 필요하다. 사용자 문서에 명시한다.
- **미니 컨트롤바**: Avalonia `Window.Topmost` + `ShowInTaskbar = false`로 구현한다.

### 4.5 창 제어 의존 기능의 범위 결정

2.3절 조사 결과에 따라 다음을 확정한다.

**결정 1 — `IPlatformService` 창 제어 8개는 구현하지 않는다.**

실사용이 `IsWindow` 하나뿐이고, 그마저 `Process.Exited` 주 경로가 정리를 담당하므로 구현 이득이 없다. macOS에서 타 앱 창을 제어하려면 접근성 권한(`AXUIElement`)이 필요한데, 얻는 것에 비해 사용자 부담이 크다. 기존 스텁을 그대로 유지한다.

`GetClientRect`/`GetWindowRect`가 하드코딩 값을 채우면서도 `false`를 반환하는 현재 방식은 호출자가 스텁임을 감지할 수 있게 하므로 유지한다.

**결정 2 — 전송 상태창은 화면 기준 기본 배치로 구현한다.**

WinForms의 "scrcpy 창 옆 배치 + 소유 창 결속"은 비목표로 둔다(1절). macOS에서 scrcpy 창 좌표를 얻으려면 `CGWindowListCopyWindowInfo` 또는 접근성 권한이 필요한데, 부가 UX를 위해 화면 기록 권한에 더해 접근성 권한까지 요구하는 것은 과하다. WinForms도 창 좌표 획득 실패 시 기본 배치로 폴백하므로, macOS는 항상 폴백 경로를 사용하는 것과 동등하다.

Phase 4에서 전송 상태창은 주 창 기준 또는 화면 중앙 기준으로 배치한다.

**결정 3 — Phase 6 착수 전 `MacCaptureService.CaptureWindow`를 선행 수정한다.**

미검증 잠재 결함이 존재한다. `CaptureWindow`는 `screencapture -x -l <handle>`을 호출하는데, `man screencapture` 기준 `-l`은 CoreGraphics window ID를 받는다. 그러나 Core가 전달하는 handle은 macOS에서 PID이므로(2.3절) 창 캡처가 실패할 것으로 판단된다.

추가로 `RunScreencapture`는 예외를 삼키고 `CaptureWindow`는 성공 여부와 무관하게 파일 경로를 반환하므로, 실패가 성공으로 보고된다.

현재 `MacCaptureService`는 `InteractiveHost`에서 생성만 되고 호출 지점이 없어 도달 불가 상태이며, 실행 검증은 수행하지 않았다. 따라서 확인된 버그가 아닌 **미검증 잠재 결함**으로 분류한다. Phase 6에서 캡처를 GUI에 노출하는 시점에 다음 중 하나를 선택해 해소한다.

- (a) 창 캡처를 제공하지 않고 전체화면·영역 캡처만 노출한다
- (b) `CGWindowListCopyWindowInfo`로 PID → CGWindowID 변환을 구현한다

어느 쪽이든 `CaptureResult`가 실패를 반영하도록 함께 수정한다.

## 5. 검증 전략

### 5.1 기존 안전망

xUnit 284개(`DexManager.ViewModels.Tests` 112개 + `DexManager.Tests` 155개 +
`DexManager.Desktop.Tests` 17개) + 다중기기 회귀 39개(총 323개)가 통과 중이다
(2026-09-08, Phase 3 종료 시점 실측). Phase 0(`ApplicationHost` 추출)은 동작 변경이
없어야 하므로, **전부 통과**가 완료 조건이다.

- `dotnet test DexManager.Mac.sln` — xUnit 284개
- `dotnet run --project DexManager.MultiDeviceTests -c Release` — 39개

`DexManager.Desktop.Tests`는 Phase 3 Task 7에서 신설됐다 — `ThemeApplier`처럼 Avalonia
타입을 다루는 순수 매핑 함수를, Avalonia를 코어 테스트 프로젝트로 전이 유입시키지 않고
검증하기 위한 전용 프로젝트다(`.sln`에 등록됨).

빌드는 `DexManager.Tests/ApplicationHostTests.cs`에서 `xUnit1031`(블로킹 `.GetAwaiter().GetResult()`) 경고 1건을 낸다. Phase 0 Task 3에서 브리프 verbatim 코드로 발생했으며 의도적으로 미해결 상태로 남아 있다 — `docs/TODO.md`의 지연 항목 목록 참조.

### 5.2 신규 테스트

- `DexManager.ViewModels.Tests` — `IUiDispatcher`에 즉시 실행 stub을 주입해 UI 없이 ViewModel 로직을 검증한다. 대상: 기기 선택 전환, 세션 상태 전이, 설정 우선순위(공통 기본값 / 기기별 / 앱 프로필), 이벤트 구독 해제.
- `Avalonia.Headless` 기반 창 생성 스모크 테스트(선택).

### 5.3 실기 검증

실제 Galaxy 기기의 DeX 시작·중지, overlay cleanup, 파일 전송은 사용자 확인 항목이다. `AGENTS.md` 원칙에 따라 대신 성공했다고 가정하지 않고 미확인으로 명시한다.

## 6. 패키징

기존 `scripts/Package-Mac-Release.sh`는 self-contained single-file publish → ad-hoc `codesign` → ZIP 재검증까지 수행한다. GUI는 여기에 `.app` 번들 생성을 추가한다.

```
DX Manager.app/Contents/
  Info.plist        CFBundleIdentifier, CFBundleVersion,
                    NSHighResolutionCapable, LSMinimumSystemVersion
  MacOS/DXManager   Avalonia 실행 파일
  Resources/        아이콘 (.icns)
```

- ad-hoc 서명을 유지한다. Developer ID 서명·notarization은 여전히 미보유이므로 Gatekeeper 최초 승인 요구는 변하지 않는다.
- `.github/workflows/macos-portable.yml`의 arm64/x64 매트릭스에 GUI 빌드·패키지 검증을 추가한다.
- 패키징 스크립트는 quarantine 속성을 삭제하거나 macOS 보안 기능을 우회하지 않는다(기존 원칙 유지).

## 7. 전달 단계

| Phase | 내용 | 완료 조건 |
| :--- | :--- | :--- |
| 0 | `Platform.Mac` 분리 + `ApplicationHost` 추출 | ✅ 완료 — 101개 xUnit + 39개 다중기기 통과, 기능 변경 없음 |
| 1 | `Desktop` 골격 + `MainWindow` (기기 목록·선택·상태) | 앱 기동, 연결 기기 표시 |
| 2 | DeX 시작/중지 + 단일창 슬롯 | ✅ 완료 — 실사용 가능. 실기 검증은 KNOWN_ISSUES 참조 |
| 3 | `SettingsWindow` (연결·값·상호작용·테마) | ✅ 완료 — 설정 변경·영구 저장. 실기 검증은 KNOWN_ISSUES 참조 |
| 4 | 무선 ADB + 파일 전송 + 전송 상태창 (화면 기준 배치, 4.5절 결정 2) | |
| 5 | 진단 + 로그 + 기기 폴더 탐색 | |
| 6 | `MacCaptureService` 선행 수정(4.5절 결정 3) → 미니 컨트롤바 + 캡처 영역 선택 오버레이 | 캡처 실패가 `CaptureResult`에 반영됨 |
| 7 | `.app` 번들 패키징 + CI 통합 | ZIP 검증 통과 |

**Phase 0만 기존 동작을 건드린다.** 여기에는 기능 추가 금지 제약을 건다.

Phase 1~7은 추가 전용이며 기존 TUI·WinForms를 변경하지 않는다. 어느 단계에서 중단해도 저장소는 정상 상태를 유지하고, TUI가 계속 존재하므로 기능 공백이 없다.

Phase 2 종료 시점부터 GUI 실사용이 가능하다. Phase 3 종료 시점부터 설정 창에서 값을
바꿔 영구 저장할 수 있다.

## 8. 열린 항목

- ~~`MacPlatformService`의 창 제어 API 실제 구현 수준 확인~~ — **해결됨(2026-09-03).** 조사 결과는 2.3절, 범위 결정은 4.5절 참조.
- Windows UI 통합 착수 여부 및 Windows 7/8.1 지원 정책 (본 설계 범위 밖. 통합을 착수하는 시점에 별도 결정한다.)
- `MacCaptureService.CaptureWindow`의 PID/CGWindowID 불일치 실행 검증 (Phase 6 착수 시. 현재는 미검증 잠재 결함.)
- `ApplicationHost`가 `IKeyboardService`를 비롯한 플랫폼 서비스를 소유하지만 `Dispose()`는 아무것도 하지 않고, 실제 정리는 `InteractiveHost`의 종료 경로가 수행한다. 현재 `MacKeyboardService.Dispose()`가 멱등이라 런타임 차이는 없다. GUI가 두 번째 소비자가 되는 Phase 1에서는 소유권과 정리 책임을 `ApplicationHost`로 일원화해야 한다. (Phase 0에서 이관하지 않은 이유: 서비스 teardown 이동은 동작 변경이라 범위 밖이다.)
- `InteractiveHost._selectedDeviceSerial`과 `ApplicationHost.SelectedSerial`이 같은 개념을 이중으로 추적한다. 현재는 대입 4곳(`InteractiveHost.cs` 64, 308, 326, 346행)이 모두 짝지어 동기화되어 어긋날 수 없지만, 향후 다섯 번째 대입이 동기화를 빠뜨리면 `EnvironmentCheckService`의 진단이 잘못된 기기를 대상으로 실행되며 어떤 자동 테스트도 이를 잡지 못한다. Phase 1 착수 시 `InteractiveHost`가 자체 필드를 버리고 `ApplicationHost.SelectedSerial`만 사용하도록 일원화한다.

  ⚠️ **이 수정은 동작 중립이 아니다.** 필드를 전달로 바꾸면 초기값이 `null`에서 `""`로 바뀌고, `InteractiveHost.cs:166`의 `string.Equals(...)` 결과가 기기가 serial을 보고하지 않는 경우에 뒤집힌다. 회귀 테스트를 먼저 추가한 뒤 의도적으로 변경한다.

  영향 범위는 진단에 한정된다(Phase 0 최종 리뷰에서 확인). `ApplicationHost.SelectedSerial`의 저장소 전체 reader는 테스트와 `ApplicationHost.cs:93`의 클로저 둘뿐이며, 그 클로저의 호출 지점은 `EnvironmentCheckService.cs:112`(`AddDeviceScreenshotFolderCheck`) 하나다. DeX·scrcpy 실행, `DeviceRuntimeSessionRegistry` 변경, 세션 상태 접근 경로는 없다.

### 8.1 Phase 1 착수 전 해결할 `ApplicationHost` 수명주기 공백

Phase 0 최종 리뷰가 지적한 사항이다. 공통 원인은 하나다 — 계획은 `ApplicationHost`에 **"조립·수명주기"** 를 배정했으나 Phase 0에서는 전반부만 구현했다. GUI가 두 번째 소비자가 되는 순간 아래가 순서대로 문제가 된다.

1. **`Dispose()`가 실질적으로 비어 있다.** GUI가 `using var host = new ApplicationHost(...)`를 써도 아무것도 정리되지 않는다. `DeviceMonitor` 타이머가 계속 ADB를 폴링하고 키보드 서비스가 핸들을 붙잡는다. Phase 1 최우선 과제다. (Phase 0에서 `InteractiveHost.Dispose()`가 `_host?.Dispose()`를 호출하도록 배선만 해 두었다.)
2. **`Start()`/`Stop()`이 없다.** `DeviceMonitor.Start()` 호출은 현재 소비자 몫이다(`InteractiveHost.cs:72`). 소비자가 둘이면 시작·중지 책임이 미정의다. `Start()`는 우연히 멱등이지만(`if (_timer != null) return;`) `Stop()`은 소비자별이 아니어서 한 호스트가 멈추면 양쪽 감시가 함께 죽는다.
3. **인스턴스가 단일 사용인데 계약에 없다.** `InteractiveHost.cs:854`가 `DeviceMonitor`를 dispose한 뒤 재시작하면 `DeviceMonitorService`가 `ObjectDisposedException`을 던진다(`DeviceMonitorService.cs:73-84`). `IsDisposed` 노출이든 문서화된 단일 사용 계약이든, 명시가 필요하다.
4. **`SelectedSerial`에 변경 알림이 없다.** MVVM 바인딩에는 `INotifyPropertyChanged`나 이벤트가 필요하다. **GUI가 첫 ViewModel을 작성하기 전에 결정해야 한다** — 나중에 넣으면 모든 소비자를 수정해야 한다.
5. ~~**`Settings`가 공유 가변 `AppSettings`를 저장 조율 없이 노출한다.**~~ — **해결됨(2026-09-07, Phase 2 Task 4).**
   TUI 설정 메뉴(`InteractiveHost.cs:792`)가 `_settings`를 직접 수정하는 대신
   `ApplicationHost.UpdateSettings`를 거친다. 입력은 잠금 밖에서 받아 프롬프트가
   잠금을 붙잡지 않게 하고, 해제된 호스트에 대한 호출은 `ObjectDisposedException`으로
   막는다.
6. ~~**Core 서비스 13개가 전부 구체 타입으로 노출된다.**~~ — **결정됨(2026-09-05, Phase 1).** 인터페이스를 새로 만들지 않고 소비 규칙을 둔다: `ShellViewModel`만 `ApplicationHost` 전체를 받고(앱 수명주기를 소유해야 하므로), 나머지 ViewModel은 자기가 쓰는 서비스만 생성자로 받는다. 인터페이스 13개를 미리 만드는 것은 두 번째 구현이 없는 상태의 추측이다. 좁은 생성자 의존성은 비용 없이 결합을 제한하고, 추출이 필요해지는 시점에는 각 생성자가 이미 경계를 알려준다.

### 8.2 Phase 0에서 지연 처리한 기타 항목

- `ApplicationHost.cs`의 `EnsureDefaultPaths`가 macOS 정책(HID 강제 비활성화, `.exe` 경로 거부)을 플랫폼 중립 Core에 담고 있다. 제약의 문언은 지키나 취지에는 어긋난다. Windows 통합 시 재검토 대상이다.
- 죽은 참조 둘: `DexManager.Mac.csproj`의 `InternalsVisibleTo`와 `DexManager.Tests.csproj`가 참조하는 Mac 실행 파일(현재 어떤 테스트도 그 어셈블리의 타입을 쓰지 않는다).
- `InteractiveHost.cs:60-66`이 두 serial을 모니터 스레드에서 비휘발성 저장 두 번으로 쓴다. 변수 하나일 때는 불가능하던 불일치가 가능해졌다. 영향은 위의 진단 한정 범위와 같다.
- 브랜치의 커밋 co-author 트레일러가 일관되지 않다(모델 3종, 트레일러 없는 커밋 2개, 서로 다른 세션 URL 4개). 메타데이터 문제이며 12개 커밋을 다시 쓰는 것이 더 나쁜 거래라고 판단해 그대로 두었다.

### 8.3 Phase 0에서 정적으로 검증할 수 없었던 것

- 실기 Galaxy 기기의 DeX 시작·중지·overlay 정리·재연결. 작업 전 기간 동안 기기를 연결한 적이 없다. **유일한 진짜 미지수다.**
- 배포된 single-file 패키지가 클린 머신에서 `DexManager.Platform.Mac`을 실제로 로드하는지. 패키징 스크립트는 0으로 종료하고 ZIP과 체크섬을 생성했으나, 패키징된 바이너리를 실행해 보지는 않았다.
- `SelectedSerial` 쓰기의 메모리 가시성. 참조 저장은 원자적이라 torn read는 없으나 가시성은 소스만으로 증명할 수 없다.
- `ApplicationHostTests`의 병렬 실행 상호작용. GUID 임시 루트로 격리되어 현재 통과하나, `LogService`/`SettingsService`/`AdbService` 내부의 정적 상태는 부하 상황에서 flake로만 드러난다.
