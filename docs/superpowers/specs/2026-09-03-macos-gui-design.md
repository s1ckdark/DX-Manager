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

### 2.3 프레임워크 선택 근거

**선택: Avalonia 11**

| 후보 | 판단 |
| :--- | :--- |
| **Avalonia 11** | 채택. macOS에 Objective-C++ 네이티브 백엔드를 사용하며 다중 창·Topmost·트레이를 지원한다. Windows/Linux로 확장 가능해 통합 목표에 부합한다. |
| .NET MAUI | 기각. macOS 지원이 Mac Catalyst 기반이라 데스크톱 다중 창 지원이 빈약하다. DX Manager는 미니 컨트롤바·설정창·전송 상태창 등 다중 창이 필수다. |
| SwiftUI + C# 브리지 | 기각. Mac UX는 우수하나 브리지 계층이 새로 필요하고 Windows 통합 목표와 상충한다. |

### 2.4 알려진 제약: Windows 7 지원

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

DexManager.Platform.Mac  (net8.0, lib)  신규: DexManager.Mac/Platform/ 이동

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
- **`IPlatformService`의 창 제어 API**: `SetForegroundWindow`, `GetWindowRect`, `IsIconic` 등이 인터페이스에 정의되어 있다. `MacPlatformService`의 실제 구현 수준은 구현 계획 단계에서 확인해야 하며, scrcpy 창 제어에 직접 영향을 준다. 미구현 항목이 있으면 해당 GUI 기능의 범위를 명시적으로 조정한다.

## 5. 검증 전략

### 5.1 기존 안전망

xUnit 95개 + 다중기기 회귀 39개(총 134개)가 통과 중이다. Phase 0(`ApplicationHost` 추출)은 동작 변경이 없어야 하므로, **134개 전부 통과**가 완료 조건이다.

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
| 0 | `Platform.Mac` 분리 + `ApplicationHost` 추출 | 기존 134개 테스트 통과, 기능 변경 없음 |
| 1 | `Desktop` 골격 + `MainWindow` (기기 목록·선택·상태) | 앱 기동, 연결 기기 표시 |
| 2 | DeX 시작/중지 + 단일창 슬롯 | 실사용 가능 |
| 3 | `SettingsWindow` (연결·값·상호작용·테마) | 설정 변경·영구 저장 |
| 4 | 무선 ADB + 파일 전송 + 전송 상태창 | |
| 5 | 진단 + 로그 + 기기 폴더 탐색 | |
| 6 | 미니 컨트롤바 + 캡처 영역 선택 오버레이 | |
| 7 | `.app` 번들 패키징 + CI 통합 | ZIP 검증 통과 |

**Phase 0만 기존 동작을 건드린다.** 여기에는 기능 추가 금지 제약을 건다.

Phase 1~7은 추가 전용이며 기존 TUI·WinForms를 변경하지 않는다. 어느 단계에서 중단해도 저장소는 정상 상태를 유지하고, TUI가 계속 존재하므로 기능 공백이 없다.

Phase 2 종료 시점부터 GUI 실사용이 가능하다.

## 8. 열린 항목

- `MacPlatformService`의 창 제어 API 실제 구현 수준 확인 (Phase 1 착수 전에 조사한다. 결과에 따라 scrcpy 창 제어 관련 GUI 기능의 범위를 조정한다.)
- Windows UI 통합 착수 여부 및 Windows 7/8.1 지원 정책 (본 설계 범위 밖. 통합을 착수하는 시점에 별도 결정한다.)
