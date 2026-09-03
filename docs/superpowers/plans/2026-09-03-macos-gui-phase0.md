# macOS GUI Phase 0 — 플랫폼 분리와 ApplicationHost 추출 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** macOS 플랫폼 서비스를 라이브러리로 분리하고 `InteractiveHost`의 서비스 조립 로직을 `DexManager.Core`의 `ApplicationHost`로 추출해, 장래의 Avalonia GUI가 TUI와 동일한 조립·정리 경로를 공유할 수 있게 한다.

**Architecture:** `DexManager.Mac/Platform/`의 macOS 구현 5종을 신규 라이브러리 `DexManager.Platform.Mac`으로 옮긴다. `InteractiveHost` 생성자의 18개 서비스 조립을 `DexManager.Core/Hosting/ApplicationHost.cs`로 옮기되, 플랫폼 구현은 생성자 주입으로 받아 Core의 플랫폼 중립성을 유지한다. TUI 전용 로직(AnsiConsole 출력, 메뉴 상태)은 `InteractiveHost`에 남긴다.

**Tech Stack:** .NET 8 (SDK 8.0.130 고정), C# 12, xUnit 2.5.3

**Spec:** `docs/superpowers/specs/2026-09-03-macos-gui-design.md`

## Global Constraints

- .NET SDK는 `global.json`이 `8.0.130`을 `rollForward: disable`로 고정한다. 다른 버전으로 빌드하지 않는다.
- 대상 프레임워크는 `net8.0`이다. `Nullable`은 `disable`, `LangVersion`은 `latest`, `ImplicitUsings`는 `enable`이다.
- **이 Phase는 관측 가능한 동작을 변경하지 않는다.** 기능 추가·제거·수정을 하지 않는다.
- Windows 앱(`DexManager/`)과 `DexManager.sln`은 어떤 이유로도 수정하지 않는다.
- 완료 조건은 기존 테스트 **134개(xUnit 95 + 다중기기 회귀 39) 전원 통과**다.
- Release 빌드는 경고 0개를 유지한다(`/warnaserror` 통과).
- 네임스페이스 `DexManager.Mac.Platform`은 유지한다. 파일이 옮겨져도 네임스페이스는 바꾸지 않는다.
- 커밋 메시지 본문은 한국어로 쓰고, 제목은 Conventional Commits 접두사를 사용한다.

## 검증 명령 (모든 Task에서 사용)

```bash
# Release 빌드 (경고 0)
dotnet build DexManager.Mac.sln -c Release /warnaserror

# xUnit 95개
dotnet test DexManager.Mac.sln -c Release

# 다중기기 회귀 39개
dotnet run --project DexManager.MultiDeviceTests -c Release
```

---

## File Structure

| 파일 | 책임 | 상태 |
| :--- | :--- | :--- |
| `DexManager.Platform.Mac/DexManager.Platform.Mac.csproj` | macOS 플랫폼 구현 라이브러리 | 생성 |
| `DexManager.Platform.Mac/MacPlatformService.cs` | OS 정보·경로 등록·창 제어 스텁 | 이동 |
| `DexManager.Platform.Mac/MacPathProvider.cs` | 경로 해석, 번들 도구 탐지 | 이동 + 수정 |
| `DexManager.Platform.Mac/MacCaptureService.cs` | `screencapture` 호출 | 이동 |
| `DexManager.Platform.Mac/MacKeyboardService.cs` | 키보드 서비스 | 이동 |
| `DexManager.Platform.Mac/MacAutoStartService.cs` | launchd 자동 시작 | 이동 |
| `DexManager.Core/Platform/IPathProvider.cs` | 경로 제공자 계약 | 수정 |
| `DexManager.Core/Hosting/ApplicationHost.cs` | 서비스 조립·수명주기 | 생성 |
| `DexManager.Mac/Hosting/InteractiveHost.cs` | TUI 화면·메뉴·입력 | 수정(축소) |
| `DexManager.Tests/FakePlatform/*.cs` | 테스트용 플랫폼 fake | 생성 |
| `DexManager.Tests/ApplicationHostTests.cs` | `ApplicationHost` 검증 | 생성 |
| `DexManager.Mac.sln` | 솔루션 | 수정 |

---

## Task 1: 빌드 환경 준비와 기준선 확보

이 Mac에는 .NET SDK가 설치되어 있지 않다. 기준선을 잡지 않으면 이후 실패가 내 변경 때문인지 원래부터인지 구분할 수 없다.

**Files:**
- 없음 (환경 작업, 커밋 없음)

**Interfaces:**
- Consumes: 없음
- Produces: 검증된 기준선 테스트 통과 수 (95 + 39)

- [ ] **Step 1: .NET SDK 8.0.130 설치**

`global.json`이 `rollForward: disable`로 8.0.130을 정확히 요구하므로 해당 버전을 설치한다.

```bash
curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
bash /tmp/dotnet-install.sh --version 8.0.130 --install-dir "$HOME/.dotnet"
export PATH="$HOME/.dotnet:$PATH"
```

- [ ] **Step 2: SDK 버전 확인**

Run:
```bash
cd /Users/dave/iWorks/DX-Manager
export PATH="$HOME/.dotnet:$PATH"
dotnet --version
```
Expected: `8.0.130`

버전이 다르면 진행하지 않는다. `global.json`을 수정해 회피하지 않는다.

- [ ] **Step 3: 기준선 빌드**

Run:
```bash
dotnet build DexManager.Mac.sln -c Release /warnaserror
```
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 4: 기준선 테스트 — xUnit**

Run:
```bash
dotnet test DexManager.Mac.sln -c Release
```
Expected: `Passed! - Failed: 0, Passed: 95`

숫자가 95가 아니면 그 숫자를 기준선으로 기록하고 이후 Task에서 그 수를 유지 조건으로 쓴다.

- [ ] **Step 5: 기준선 테스트 — 다중기기 회귀**

Run:
```bash
dotnet run --project DexManager.MultiDeviceTests -c Release
```
Expected: 39개 테스트 전부 통과, 종료 코드 0

- [ ] **Step 6: 기준선 기록**

세 명령의 결과를 이후 비교 기준으로 기억한다. 커밋하지 않는다(환경 작업).

---

## Task 2: `IPathProvider`에 `IsPortablePackage` 추가

`MacPathProvider.IsPortablePackage`는 현재 `internal`이라 프로젝트를 분리하면 `InteractiveHost`가 접근할 수 없다. 또한 `EnsureDefaultPaths` 로직을 Core로 옮기려면 Core가 인터페이스로 이 값을 읽어야 한다. 구현체가 `MacPathProvider` 하나뿐이라 인터페이스 확장 비용이 낮다.

**Files:**
- Modify: `DexManager.Core/Platform/IPathProvider.cs`
- Modify: `DexManager.Mac/Platform/MacPathProvider.cs:20`
- Test: `DexManager.Tests/MacPathProviderTests.cs`

**Interfaces:**
- Consumes: 없음
- Produces: `IPathProvider.IsPortablePackage` (`bool`, get-only). Task 5의 `ApplicationHost.EnsureDefaultPaths`가 사용한다.

- [ ] **Step 1: 실패하는 테스트 작성**

`DexManager.Tests/MacPathProviderTests.cs` 끝부분(클래스 닫는 중괄호 앞)에 추가한다.

```csharp
    [Fact]
    public void IsPortablePackage_IsReachableThroughInterface()
    {
        IPathProvider provider = new MacPathProvider();

        // 인터페이스 경유 접근만 검증한다. 값 자체는 번들 도구 존재 여부에
        // 따라 달라지므로 단정하지 않는다.
        var value = provider.IsPortablePackage;

        Assert.IsType<bool>(value);
    }
```

파일 상단 using에 `DexManager.Platform`이 없다면 추가한다.

```csharp
using DexManager.Platform;
using DexManager.Mac.Platform;
```

- [ ] **Step 2: 테스트가 실패하는지 확인**

Run:
```bash
dotnet build DexManager.Mac.sln -c Release
```
Expected: 컴파일 오류 — `'IPathProvider' does not contain a definition for 'IsPortablePackage'`

- [ ] **Step 3: 인터페이스에 프로퍼티 추가**

`DexManager.Core/Platform/IPathProvider.cs`의 `DefaultProxyExecutablePath` 아래에 추가한다.

```csharp
        string DefaultProxyExecutablePath { get; }

        /// <summary>
        /// 번들된 도구(포터블 패키지)를 우선 사용하는 배포 형태인지 여부.
        /// </summary>
        bool IsPortablePackage { get; }
```

- [ ] **Step 4: 구현체를 public으로 변경**

`DexManager.Mac/Platform/MacPathProvider.cs:20`을 수정한다.

변경 전:
```csharp
    internal bool IsPortablePackage => _preferBundledTools;
```

변경 후:
```csharp
    public bool IsPortablePackage => _preferBundledTools;
```

- [ ] **Step 5: 테스트 통과 확인**

Run:
```bash
dotnet build DexManager.Mac.sln -c Release /warnaserror
dotnet test DexManager.Mac.sln -c Release
```
Expected: 빌드 경고 0, `Failed: 0, Passed: 96` (기준선 95 + 신규 1)

- [ ] **Step 6: 커밋**

```bash
git add DexManager.Core/Platform/IPathProvider.cs \
        DexManager.Mac/Platform/MacPathProvider.cs \
        DexManager.Tests/MacPathProviderTests.cs
git commit -m "refactor(core): expose IsPortablePackage on IPathProvider

프로젝트 분리와 ApplicationHost 추출을 위해 MacPathProvider의 internal
프로퍼티를 인터페이스 계약으로 승격한다. 구현체는 MacPathProvider
하나뿐이므로 다른 소비자에 영향이 없다."
```

---

## Task 3: `DexManager.Platform.Mac` 라이브러리 분리

macOS 플랫폼 구현이 실행 파일 프로젝트 안에 있어 GUI가 참조할 수 없고 테스트도 `InternalsVisibleTo`에 의존한다. 라이브러리로 분리한다.

**Files:**
- Create: `DexManager.Platform.Mac/DexManager.Platform.Mac.csproj`
- Move: `DexManager.Mac/Platform/*.cs` → `DexManager.Platform.Mac/*.cs` (5개)
- Modify: `DexManager.Mac/DexManager.Mac.csproj`
- Modify: `DexManager.Tests/DexManager.Tests.csproj`
- Modify: `DexManager.Mac.sln`

**Interfaces:**
- Consumes: Task 2의 `IPathProvider.IsPortablePackage`
- Produces: 어셈블리 `DexManager.Platform.Mac`. 네임스페이스는 `DexManager.Mac.Platform`으로 **변경 없음**. 공개 타입: `MacPlatformService`, `MacPathProvider`, `MacCaptureService`, `MacKeyboardService`, `MacAutoStartService`.

- [ ] **Step 1: 새 프로젝트 파일 생성**

`DexManager.Platform.Mac/DexManager.Platform.Mac.csproj`를 만든다.

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <RootNamespace>DexManager.Mac.Platform</RootNamespace>
    <AssemblyName>DexManager.Platform.Mac</AssemblyName>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>disable</Nullable>
    <LangVersion>latest</LangVersion>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\DexManager.Core\DexManager.Core.csproj" />
  </ItemGroup>

  <!--
    MacPathProvider에는 테스트 전용 internal 생성자
    MacPathProvider(bool? preferBundledTools)가 있고 MacPathProviderTests가
    이를 직접 호출한다. 타입이 이 어셈블리로 옮겨오므로, 그 호출을 합법으로
    만들던 접근 허가도 함께 옮긴다. DexManager.Mac.csproj와 DexManager.Core.csproj가
    이미 같은 패턴을 선언한다. 노출 대상은 테스트 어셈블리 하나로 이동 전과 같다.
    DexManager.MultiDeviceTests에는 부여하지 않는다.
  -->
  <ItemGroup>
    <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo">
      <_Parameter1>DexManager.Tests</_Parameter1>
    </AssemblyAttribute>
  </ItemGroup>

</Project>
```

- [ ] **Step 2: 파일 이동**

이력 보존을 위해 `git mv`를 사용한다.

```bash
cd /Users/dave/iWorks/DX-Manager
git mv DexManager.Mac/Platform/MacPlatformService.cs  DexManager.Platform.Mac/MacPlatformService.cs
git mv DexManager.Mac/Platform/MacPathProvider.cs     DexManager.Platform.Mac/MacPathProvider.cs
git mv DexManager.Mac/Platform/MacCaptureService.cs   DexManager.Platform.Mac/MacCaptureService.cs
git mv DexManager.Mac/Platform/MacKeyboardService.cs  DexManager.Platform.Mac/MacKeyboardService.cs
git mv DexManager.Mac/Platform/MacAutoStartService.cs DexManager.Platform.Mac/MacAutoStartService.cs
rmdir DexManager.Mac/Platform
```

네임스페이스 선언(`namespace DexManager.Mac.Platform;`)은 **수정하지 않는다.** 이동한 5개 파일 안의 코드는 그대로 둔다.

- [ ] **Step 3: `DexManager.Mac`이 새 라이브러리를 참조하게 수정**

`DexManager.Mac/DexManager.Mac.csproj`의 `ItemGroup`을 수정한다.

변경 전:
```xml
  <ItemGroup>
    <ProjectReference Include="..\DexManager.Core\DexManager.Core.csproj" />
  </ItemGroup>
```

변경 후:
```xml
  <ItemGroup>
    <ProjectReference Include="..\DexManager.Core\DexManager.Core.csproj" />
    <ProjectReference Include="..\DexManager.Platform.Mac\DexManager.Platform.Mac.csproj" />
  </ItemGroup>
```

- [ ] **Step 4: 테스트 프로젝트가 새 라이브러리를 참조하게 수정**

`DexManager.Tests/DexManager.Tests.csproj`의 `ProjectReference` 그룹에 한 줄 추가한다. 기존 `DexManager.Mac` 참조는 `InteractiveHost` 테스트를 위해 유지한다.

```xml
  <ItemGroup>
    <ProjectReference Include="..\DexManager.Core\DexManager.Core.csproj" />
    <ProjectReference Include="..\DexManager.Mac\DexManager.Mac.csproj" />
    <ProjectReference Include="..\DexManager.Platform.Mac\DexManager.Platform.Mac.csproj" />
  </ItemGroup>
```

- [ ] **Step 5: 솔루션에 프로젝트 추가**

```bash
dotnet sln DexManager.Mac.sln add DexManager.Platform.Mac/DexManager.Platform.Mac.csproj
```

- [ ] **Step 6: 빌드와 테스트로 회귀 없음 확인**

Run:
```bash
dotnet build DexManager.Mac.sln -c Release /warnaserror
dotnet test DexManager.Mac.sln -c Release
dotnet run --project DexManager.MultiDeviceTests -c Release
```
Expected: 경고 0, `Failed: 0, Passed: 96`, 다중기기 39개 통과

빌드가 `InternalsVisibleTo` 관련 오류를 내면 Task 2가 누락된 것이다. 되돌아가 확인한다.

- [ ] **Step 7: 패키징 스크립트 영향 확인**

`scripts/Package-Mac-Release.sh`는 `DexManager.Mac.csproj`를 publish하므로 라이브러리 분리에 영향받지 않아야 한다. 확인만 한다.

Run:
```bash
grep -n "csproj" scripts/Package-Mac-Release.sh
```
Expected: `DexManager.Mac/DexManager.Mac.csproj`와 `DexManager.AdbProxy/DexManager.AdbProxy.csproj`만 등장. `Platform` 관련 항목이 나오면 이 Task의 범위를 넘으므로 중단하고 보고한다.

- [ ] **Step 8: 커밋**

```bash
git add -A
git commit -m "refactor(mac): extract macOS platform services into a library

DexManager.Mac/Platform/의 구현 5종을 DexManager.Platform.Mac 라이브러리로
옮긴다. 네임스페이스는 DexManager.Mac.Platform으로 유지해 기존 코드 변경을
피한다. 실행 파일 안에 있던 플랫폼 서비스를 GUI가 참조할 수 있게 되고
InternalsVisibleTo 없이 테스트할 수 있게 된다."
```

---

## Task 4: 테스트용 플랫폼 fake 작성

`ApplicationHost`는 파일시스템과 프로세스에 닿으므로, 실제 macOS 서비스 대신 제어 가능한 fake를 주입해야 테스트가 빠르고 결정적이다. 현재 테스트 프로젝트에는 fake 자산이 없다.

**Files:**
- Create: `DexManager.Tests/FakePlatform/FakePathProvider.cs`
- Create: `DexManager.Tests/FakePlatform/FakePlatformService.cs`
- Create: `DexManager.Tests/FakePlatform/FakeCaptureService.cs`
- Create: `DexManager.Tests/FakePlatform/FakeKeyboardService.cs`
- Create: `DexManager.Tests/FakePlatform/FakeAutoStartService.cs`

**Interfaces:**
- Consumes: `IPathProvider`(Task 2 확장 포함), `IPlatformService`, `ICaptureService`, `IKeyboardService`, `IAutoStartService`
- Produces: `FakePathProvider(string root)`, `FakePlatformService()`, `FakeCaptureService()`, `FakeKeyboardService()`, `FakeAutoStartService()`. Task 5·6·7의 테스트가 사용한다.

- [ ] **Step 1: `FakePathProvider` 작성**

`DexManager.Tests/FakePlatform/FakePathProvider.cs`를 만든다. ADB/scrcpy 경로는 실제로 존재하는 실행 파일(`/bin/echo`)을 가리켜, `PathService.SelectAdbPath`가 5초 타임아웃을 소모하지 않게 한다.

```csharp
using DexManager.Platform;

namespace DexManager.Tests.FakePlatform;

public sealed class FakePathProvider : IPathProvider
{
    private readonly string _root;

    public FakePathProvider(string root)
    {
        _root = root;
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(Path.Combine(_root, "config"));
    }

    public string BaseDirectory => _root;

    public string DefaultSettingsFilePath =>
        Path.Combine(_root, "config", "settings.json");

    public string DefaultScreenshotFolder => Path.Combine(_root, "screenshots");

    public string DefaultLogDirectory => Path.Combine(_root, "logs");

    public string DefaultProxyExecutablePath => Path.Combine(_root, "DXMAdbProxy");

    public bool IsPortablePackage => false;

    // 실존하는 실행 파일을 반환해 경로 탐색 타임아웃을 피한다.
    public string ResolveDefaultAdbPath() => "/bin/echo";

    public string ResolveDefaultScrcpyPath() => "/bin/echo";

    public string ResolveWin7AdbPath() => "/bin/echo";

    public string[] GetCandidateAdbPaths() => new[] { "/bin/echo" };

    public string[] GetCandidateScrcpyPaths() => new[] { "/bin/echo" };
}
```

- [ ] **Step 2: `FakePlatformService` 작성**

`DexManager.Tests/FakePlatform/FakePlatformService.cs`를 만든다. 창 제어는 스펙 4.5절 결정 1에 따라 `MacPlatformService`와 동일한 스텁 의미를 유지한다.

```csharp
using DexManager.Platform;

namespace DexManager.Tests.FakePlatform;

public sealed class FakePlatformService : IPlatformService
{
    public bool IsAdministrator() => false;

    public string GetOperatingSystemDisplayName() => "Fake macOS";

    public Version GetOperatingSystemVersion() => new Version(14, 0);

    public bool RequiresLegacyAdb => false;

    public bool IsWindow(IntPtr handle) => handle != IntPtr.Zero;

    public bool IsWindowVisible(IntPtr handle) => handle != IntPtr.Zero;

    public bool IsIconic(IntPtr handle) => false;

    public void ShowWindow(IntPtr handle, bool restore) { }

    public void SetForegroundWindow(IntPtr handle) { }

    public IntPtr GetForegroundWindow() => IntPtr.Zero;

    public bool GetClientRect(
        IntPtr handle, out int left, out int top, out int right, out int bottom)
    {
        left = 0; top = 0; right = 0; bottom = 0;
        return false;
    }

    public bool GetWindowRect(
        IntPtr handle, out int left, out int top, out int right, out int bottom)
    {
        left = 0; top = 0; right = 0; bottom = 0;
        return false;
    }

    public void SuppressNativeCrashDialogs() { }

    public bool IsDirectoryInProcessPath(string directory) => false;

    public bool IsDirectoryInSystemPath(string directory) => false;

    public bool TryRegisterDirectoryInSystemPath(string directory) => false;
}
```

- [ ] **Step 3: 나머지 fake 3종 작성**

`DexManager.Tests/FakePlatform/FakeCaptureService.cs`:

```csharp
using DexManager.Models;
using DexManager.Platform;

namespace DexManager.Tests.FakePlatform;

public sealed class FakeCaptureService : ICaptureService
{
    public CaptureResult CaptureWindow(IntPtr windowHandle, string serial) =>
        new CaptureResult(string.Empty, "fake", false);

    public CaptureResult CaptureScreenRectangle(
        int x, int y, int width, int height, string prefix, string serial) =>
        new CaptureResult(string.Empty, "fake", false);

    public Task<CaptureResult> CaptureWindowAsync(IntPtr windowHandle, string serial) =>
        Task.FromResult(CaptureWindow(windowHandle, serial));

    public Task<CaptureResult> CaptureScreenRectangleAsync(
        int x, int y, int width, int height, string prefix, string serial) =>
        Task.FromResult(CaptureScreenRectangle(x, y, width, height, prefix, serial));
}
```

`DexManager.Tests/FakePlatform/FakeKeyboardService.cs`:

```csharp
using DexManager.Models;
using DexManager.Platform;

namespace DexManager.Tests.FakePlatform;

public sealed class FakeKeyboardService : IKeyboardService
{
    public bool Started { get; private set; }

    public event EventHandler CaptureHotkeyPressed;
    public event EventHandler ExitHotkeyPressed;

    public void Start() => Started = true;

    public void Stop() => Started = false;

    public void ReloadConfiguration(KeyMappingSettings settings) { }

    // MacKeyboardService와 동일한 패턴이다. 이벤트를 발화하는 메서드가
    // 없으면 /warnaserror 빌드가 CS0067("event is never used")로 실패한다.
    public void TriggerCapture() =>
        CaptureHotkeyPressed?.Invoke(this, EventArgs.Empty);

    public void TriggerExit() =>
        ExitHotkeyPressed?.Invoke(this, EventArgs.Empty);

    public void Dispose() => Stop();
}
```

`DexManager.Tests/FakePlatform/FakeAutoStartService.cs`:

```csharp
using DexManager.Platform;

namespace DexManager.Tests.FakePlatform;

public sealed class FakeAutoStartService : IAutoStartService
{
    private bool _registered;

    public bool IsRegistered() => _registered;

    public void Apply(bool enabled) => _registered = enabled;

    public void Register() => _registered = true;

    public void Unregister() => _registered = false;
}
```

- [ ] **Step 4: 컴파일 확인**

Run:
```bash
dotnet build DexManager.Mac.sln -c Release /warnaserror
```
Expected: 경고 0, 오류 0

참고로 아래 시그니처는 이미 확인되었으므로 위 fake 코드와 일치한다.

- `IKeyboardService`: `Start()`, `Stop()`, `ReloadConfiguration(KeyMappingSettings)`, `event EventHandler CaptureHotkeyPressed`, `event EventHandler ExitHotkeyPressed`, `IDisposable`
- `CaptureResult(string localPath, string remotePath, bool transferredToDevice)`

CS0067 경고가 나면 `TriggerCapture`/`TriggerExit`가 누락된 것이다.

- [ ] **Step 5: 테스트 수 회귀 없음 확인**

Run:
```bash
dotnet test DexManager.Mac.sln -c Release
```
Expected: `Failed: 0, Passed: 96` (fake는 테스트가 아니므로 수 변화 없음)

- [ ] **Step 6: 커밋**

```bash
git add DexManager.Tests/FakePlatform
git commit -m "test: add platform service fakes for host tests

ApplicationHost 테스트가 실제 파일시스템과 프로세스에 의존하지 않도록
IPathProvider, IPlatformService, ICaptureService, IKeyboardService,
IAutoStartService의 fake를 추가한다. 경로 fake는 실존 실행 파일을 반환해
ADB 탐색 타임아웃을 피한다."
```

---

## Task 5: `ApplicationHost` 생성과 서비스 조립 이관

`InteractiveHost` 생성자의 조립 로직을 Core로 옮긴다. TUI 전용 로직은 옮기지 않는다.

**Files:**
- Create: `DexManager.Core/Hosting/ApplicationHost.cs`
- Create: `DexManager.Tests/ApplicationHostTests.cs`

**Interfaces:**
- Consumes: Task 4의 fake 5종, Task 2의 `IPathProvider.IsPortablePackage`
- Produces:
  - `ApplicationHost(IPlatformService, IPathProvider, ICaptureService, IKeyboardService, IAutoStartService)` — 생성자
  - 프로퍼티: `Settings` (`AppSettings`), `SettingsService`, `Log` (`LogService`), `ProcessRunner`, `PathService`, `Adb` (`AdbService`), `WirelessAdb` (`WirelessAdbService`), `DeviceRegistry` (`PhysicalDeviceRegistry`), `RuntimeSessions` (`DeviceRuntimeSessionRegistry`), `DeviceMonitor` (`DeviceMonitorService`), `PermissionService` (`DisplayCleanupPermissionService`), `EnvironmentCheck` (`EnvironmentCheckService`), `DiagnosticReport` (`DiagnosticReportService`), `RuntimeFactory` (`DeviceRuntimeServiceFactory`)
  - `string SelectedSerial { get; set; }` — `EnvironmentCheckService`에 전달하는 콜백의 원본. Task 7의 `InteractiveHost`가 설정한다.
  - `void Dispose()`

- [ ] **Step 1: 실패하는 테스트 작성**

`DexManager.Tests/ApplicationHostTests.cs`를 만든다.

```csharp
using DexManager.Hosting;
using DexManager.Tests.FakePlatform;

namespace DexManager.Tests;

public class ApplicationHostTests : IDisposable
{
    private readonly string _root;

    public ApplicationHostTests()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            "dxm-host-tests",
            Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }
        catch
        {
            // 임시 디렉터리 정리 실패는 테스트 결과에 영향을 주지 않는다.
        }
    }

    private ApplicationHost CreateHost() => new ApplicationHost(
        new FakePlatformService(),
        new FakePathProvider(_root),
        new FakeCaptureService(),
        new FakeKeyboardService(),
        new FakeAutoStartService());

    [Fact]
    public void Constructor_ComposesAllServices()
    {
        using var host = CreateHost();

        Assert.NotNull(host.Settings);
        Assert.NotNull(host.SettingsService);
        Assert.NotNull(host.Log);
        Assert.NotNull(host.ProcessRunner);
        Assert.NotNull(host.PathService);
        Assert.NotNull(host.Adb);
        Assert.NotNull(host.WirelessAdb);
        Assert.NotNull(host.DeviceRegistry);
        Assert.NotNull(host.RuntimeSessions);
        Assert.NotNull(host.DeviceMonitor);
        Assert.NotNull(host.PermissionService);
        Assert.NotNull(host.EnvironmentCheck);
        Assert.NotNull(host.DiagnosticReport);

        // RuntimeFactory는 Task 6에서 초기화되므로 여기서 단정하지 않는다.
        // Task 6의 Constructor_InitializesRuntimeFactory가 검증한다.
    }

    [Fact]
    public void SelectedSerial_DefaultsToEmptyAndIsSettable()
    {
        using var host = CreateHost();

        Assert.Equal(string.Empty, host.SelectedSerial ?? string.Empty);

        host.SelectedSerial = "R5CT1234567";

        Assert.Equal("R5CT1234567", host.SelectedSerial);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var host = CreateHost();

        host.Dispose();
        host.Dispose();
    }
}
```

- [ ] **Step 2: 테스트가 실패하는지 확인**

Run:
```bash
dotnet build DexManager.Mac.sln -c Release
```
Expected: 컴파일 오류 — `The type or namespace name 'Hosting' does not exist in the namespace 'DexManager'`

- [ ] **Step 3: `ApplicationHost` 구현**

`DexManager.Core/Hosting/ApplicationHost.cs`를 만든다. 조립 순서는 `InteractiveHost` 생성자와 동일하게 유지한다. 순서가 바뀌면 경로 해석과 ADB 선택 결과가 달라질 수 있다.

```csharp
using DexManager.Models;
using DexManager.Platform;
using DexManager.Services;
using DexManager.Utils;

namespace DexManager.Hosting;

/// <summary>
/// 플랫폼 중립 서비스 조립 루트. TUI와 GUI가 동일한 조립·정리 경로를
/// 공유하도록 한다. 화면 출력과 입력 처리는 포함하지 않는다.
/// </summary>
public sealed class ApplicationHost : IDisposable
{
    private const int AdbSelectionTimeoutMs = 5000;

    private readonly IPlatformService _platformService;
    private readonly IPathProvider _pathProvider;
    private readonly ICaptureService _captureService;
    private readonly IKeyboardService _keyboardService;
    private readonly IAutoStartService _autoStartService;

    private bool _disposed;

    public ApplicationHost(
        IPlatformService platformService,
        IPathProvider pathProvider,
        ICaptureService captureService,
        IKeyboardService keyboardService,
        IAutoStartService autoStartService)
    {
        _platformService = platformService
            ?? throw new ArgumentNullException(nameof(platformService));
        _pathProvider = pathProvider
            ?? throw new ArgumentNullException(nameof(pathProvider));
        _captureService = captureService
            ?? throw new ArgumentNullException(nameof(captureService));
        _keyboardService = keyboardService
            ?? throw new ArgumentNullException(nameof(keyboardService));
        _autoStartService = autoStartService
            ?? throw new ArgumentNullException(nameof(autoStartService));

        SelectedSerial = string.Empty;

        Log = new LogService();
        Log.SetLogDirectory(_pathProvider.DefaultLogDirectory);

        SettingsService = new SettingsService(Log, _pathProvider.BaseDirectory);
        Settings = SettingsService.Load();

        ProcessRunner = new ProcessRunner(Log);
        PathService = new PathService(
            SettingsService,
            Log,
            ProcessRunner,
            _pathProvider,
            _platformService);

        EnsureDefaultPaths();

        var adbPath = PathService.SelectAdbPath(Settings, AdbSelectionTimeoutMs);
        Adb = new AdbService(
            adbPath,
            Settings.Timing.ProcessTimeoutMs,
            ProcessRunner,
            Log);

        WirelessAdb = new WirelessAdbService(
            Adb,
            SettingsService,
            Settings,
            Log);

        DeviceRegistry = new PhysicalDeviceRegistry();
        RuntimeSessions = new DeviceRuntimeSessionRegistry();

        DeviceMonitor = new DeviceMonitorService(
            Adb,
            WirelessAdb,
            DeviceRegistry,
            Log,
            Settings.Timing.DeviceMonitorIntervalMs,
            Settings.Timing.DisconnectMonitorIntervalMs);

        PermissionService = new DisplayCleanupPermissionService(Adb);

        EnvironmentCheck = new EnvironmentCheckService(
            Adb,
            null,
            PathService,
            Log,
            SettingsService,
            Settings,
            () => SelectedSerial ?? string.Empty);

        DiagnosticReport = new DiagnosticReportService();

        InitializeRuntimeFactory();
    }

    public IPlatformService PlatformService => _platformService;
    public IPathProvider PathProvider => _pathProvider;
    public ICaptureService CaptureService => _captureService;
    public IKeyboardService KeyboardService => _keyboardService;
    public IAutoStartService AutoStartService => _autoStartService;

    public AppSettings Settings { get; }
    public SettingsService SettingsService { get; }
    public LogService Log { get; }
    public ProcessRunner ProcessRunner { get; }
    public PathService PathService { get; }
    public AdbService Adb { get; }
    public WirelessAdbService WirelessAdb { get; }
    public PhysicalDeviceRegistry DeviceRegistry { get; }
    public DeviceRuntimeSessionRegistry RuntimeSessions { get; }
    public DeviceMonitorService DeviceMonitor { get; }
    public DisplayCleanupPermissionService PermissionService { get; }
    public EnvironmentCheckService EnvironmentCheck { get; }
    public DiagnosticReportService DiagnosticReport { get; }
    public DeviceRuntimeServiceFactory RuntimeFactory { get; private set; }

    /// <summary>
    /// 현재 선택된 기기의 transport serial. 진단 서비스가 이 값을 읽는다.
    /// 소비 호스트(TUI/GUI)가 갱신한다.
    /// </summary>
    public string SelectedSerial { get; set; }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}
```

`EnsureDefaultPaths`와 `InitializeRuntimeFactory`는 Task 6에서 채운다. 지금은 컴파일을 통과시키기 위해 아래 두 메서드를 클래스 안에 임시로 둔다.

```csharp
    private void EnsureDefaultPaths()
    {
        // Task 6에서 InteractiveHost로부터 이관한다.
    }

    private void InitializeRuntimeFactory()
    {
        // Task 6에서 InteractiveHost로부터 이관한다.
    }
```

> 이 두 개의 빈 메서드는 Task 6에서 반드시 채워진다. Task 5만 적용한 상태로 Phase를 종료하지 않는다.

- [ ] **Step 4: 테스트 통과 확인**

Run:
```bash
dotnet build DexManager.Mac.sln -c Release /warnaserror
dotnet test DexManager.Mac.sln -c Release
```
Expected: 경고 0, `Failed: 0, Passed: 99` (96 + 신규 3)

세 테스트 모두 이 시점에 통과해야 한다. `RuntimeFactory`는 Task 6에서 초기화되므로 여기서 단정하지 않는다.

- [ ] **Step 5: 커밋**

```bash
git add DexManager.Core/Hosting/ApplicationHost.cs DexManager.Tests/ApplicationHostTests.cs
git commit -m "refactor(core): add ApplicationHost composition root

InteractiveHost 생성자의 서비스 조립을 Core로 옮기기 위한 골격을 만든다.
플랫폼 구현은 생성자 주입으로 받아 Core의 플랫폼 중립성을 유지한다.
경로 기본값 보정과 런타임 팩토리 초기화는 다음 커밋에서 이관한다."
```

---

## Task 6: `EnsureDefaultPaths`와 `InitializeRuntimeFactory` 이관

**Files:**
- Modify: `DexManager.Core/Hosting/ApplicationHost.cs`
- Modify: `DexManager.Tests/ApplicationHostTests.cs`

**Interfaces:**
- Consumes: Task 5의 `ApplicationHost`
- Produces: `ApplicationHost.RuntimeFactory`가 non-null로 초기화됨

- [ ] **Step 1: 실패하는 테스트 추가**

`DexManager.Tests/ApplicationHostTests.cs`의 클래스 안에 추가한다.

```csharp
    [Fact]
    public void Constructor_InitializesRuntimeFactory()
    {
        using var host = CreateHost();

        Assert.NotNull(host.RuntimeFactory);
    }

    [Fact]
    public void EnsureDefaultPaths_DisablesHidInputOnMac()
    {
        using var host = CreateHost();

        // macOS는 HID 키보드/마우스를 지원하지 않으므로 조립 시 꺼져야 한다.
        Assert.False(host.Settings.Scrcpy.UseHidKeyboard);
        Assert.False(host.Settings.Scrcpy.UseHidMouse);
    }
```

- [ ] **Step 2: 테스트가 실패하는지 확인**

Run:
```bash
dotnet test DexManager.Mac.sln -c Release --filter "FullyQualifiedName~ApplicationHostTests"
```
Expected: `Constructor_InitializesRuntimeFactory` FAIL (`Assert.NotNull() Failure: Value is null`)

- [ ] **Step 3: `EnsureDefaultPaths` 이관**

`ApplicationHost`의 빈 `EnsureDefaultPaths`를 아래로 교체한다. `InteractiveHost`의 원본과 동일하되 필드 참조를 프로퍼티로 바꾼다.

```csharp
    private void EnsureDefaultPaths()
    {
        var modified = false;
        var currentAdb = Settings.Paths.AdbPath ?? string.Empty;
        var forcePortableAdb = _pathProvider.IsPortablePackage &&
            Settings.Paths.AdbSelectionMode != AdbSelectionMode.Manual;
        if (forcePortableAdb ||
            string.IsNullOrWhiteSpace(currentAdb) ||
            !File.Exists(currentAdb) ||
            currentAdb.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            var adb = _pathProvider.ResolveDefaultAdbPath();
            if (File.Exists(adb))
            {
                Settings.Paths.AdbPath = adb;
                modified = true;
            }
        }

        var currentScrcpy = Settings.Paths.ScrcpyPath ?? string.Empty;
        if (_pathProvider.IsPortablePackage ||
            string.IsNullOrWhiteSpace(currentScrcpy) ||
            !File.Exists(currentScrcpy) ||
            currentScrcpy.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            var scrcpy = _pathProvider.ResolveDefaultScrcpyPath();
            if (File.Exists(scrcpy))
            {
                Settings.Paths.ScrcpyPath = scrcpy;
                modified = true;
            }
        }

        if (Settings.Scrcpy != null &&
            (Settings.Scrcpy.UseHidKeyboard || Settings.Scrcpy.UseHidMouse))
        {
            Settings.Scrcpy.UseHidKeyboard = false;
            Settings.Scrcpy.UseHidMouse = false;
            modified = true;
        }

        if (Settings.SingleWindowSlots != null)
        {
            foreach (var slot in Settings.SingleWindowSlots)
            {
                if (slot != null && (slot.UseHidKeyboard || slot.UseHidMouse))
                {
                    slot.UseHidKeyboard = false;
                    slot.UseHidMouse = false;
                    modified = true;
                }
            }
        }

        if (modified)
        {
            SettingsService.Save(Settings);
        }
    }
```

- [ ] **Step 4: `InitializeRuntimeFactory` 이관**

빈 `InitializeRuntimeFactory`를 아래로 교체한다.

```csharp
    private void InitializeRuntimeFactory()
    {
        var scrcpyPath = Settings.Paths.ScrcpyPath;
        if (string.IsNullOrWhiteSpace(scrcpyPath) ||
            !File.Exists(scrcpyPath) ||
            scrcpyPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            scrcpyPath = _pathProvider.ResolveDefaultScrcpyPath();
        }

        var adbPath = Adb.AdbPath;
        var coordinator = new ScrcpyLaunchCoordinator();

        RuntimeFactory = new DeviceRuntimeServiceFactory(
            scrcpyPath,
            adbPath,
            Settings.Timing.ProcessTimeoutMs,
            ProcessRunner,
            Adb,
            coordinator,
            SettingsService,
            Settings,
            Log,
            RuntimeSessions,
            _platformService);
    }
```

- [ ] **Step 5: 테스트 통과 확인**

Run:
```bash
dotnet build DexManager.Mac.sln -c Release /warnaserror
dotnet test DexManager.Mac.sln -c Release
```
Expected: 경고 0, `Failed: 0, Passed: 101` (99 + 신규 2)

- [ ] **Step 6: 커밋**

```bash
git add DexManager.Core/Hosting/ApplicationHost.cs DexManager.Tests/ApplicationHostTests.cs
git commit -m "refactor(core): move path defaults and runtime factory into ApplicationHost

InteractiveHost의 EnsureDefaultPaths와 InitializeRuntimeFactory를 Core로
이관한다. 조립 순서와 동작은 원본과 동일하게 유지한다."
```

---

## Task 7: `InteractiveHost`를 `ApplicationHost` 소비로 전환

TUI가 자체 조립을 버리고 `ApplicationHost`를 사용하게 한다. TUI 전용 로직(AnsiConsole 출력, 메뉴 상태)은 남긴다. 이 Task가 Phase 0의 핵심 위험 지점이다.

**Files:**
- Modify: `DexManager.Mac/Hosting/InteractiveHost.cs:9-118`

**Interfaces:**
- Consumes: Task 5·6의 `ApplicationHost` 전체
- Produces: 없음 (내부 리팩터링). `InteractiveHost`의 공개 메서드 시그니처는 변경하지 않는다: `RunAsync`, `RunDiagnosticsAsync`, `StartDexAsync`, `StopDexAsync`, `WaitForDexCleanupAsync`, `IsDexRunning`, `Dispose`.

- [ ] **Step 1: 현재 공개 표면 기록**

변경 전후 비교 기준을 만든다.

Run:
```bash
cd /Users/dave/iWorks/DX-Manager
grep -n "public .*(" DexManager.Mac/Hosting/InteractiveHost.cs | grep -v "private\|internal" > /tmp/interactive-host-before.txt
cat /tmp/interactive-host-before.txt
```

이 목록은 Step 6에서 그대로여야 한다.

- [ ] **Step 2: 필드 선언을 `ApplicationHost` 위임으로 교체**

`InteractiveHost.cs` 9-40행의 필드 블록을 교체한다.

변경 전(발췌):
```csharp
    private readonly MacPlatformService _platformService;
    private readonly MacPathProvider _pathProvider;
    private readonly MacCaptureService _captureService;
    private readonly MacKeyboardService _keyboardService;
    private readonly MacAutoStartService _autoStartService;

    private readonly SettingsService _settingsService;
    private readonly LogService _logService;
    private readonly ProcessRunner _processRunner;
    private readonly PathService _pathService;
    private readonly AdbService _adbService;
    private readonly WirelessAdbService _wirelessAdb;
    private readonly PhysicalDeviceRegistry _deviceRegistry;
    private readonly DeviceRuntimeSessionRegistry _runtimeSessions;
    private readonly DeviceMonitorService _deviceMonitor;
    private readonly DisplayCleanupPermissionService _permissionService;
    private readonly EnvironmentCheckService _envCheckService;
    private readonly DiagnosticReportService _diagnosticReportService;

    private readonly AppSettings _settings;
    private DeviceRuntimeServiceFactory _runtimeFactory;
```

변경 후:
```csharp
    private readonly ApplicationHost _host;

    // 기존 필드명을 유지해 나머지 1,000여 줄의 코드를 수정하지 않는다.
    private SettingsService _settingsService => _host.SettingsService;
    private LogService _logService => _host.Log;
    private ProcessRunner _processRunner => _host.ProcessRunner;
    private PathService _pathService => _host.PathService;
    private AdbService _adbService => _host.Adb;
    private WirelessAdbService _wirelessAdb => _host.WirelessAdb;
    private PhysicalDeviceRegistry _deviceRegistry => _host.DeviceRegistry;
    private DeviceRuntimeSessionRegistry _runtimeSessions => _host.RuntimeSessions;
    private DeviceMonitorService _deviceMonitor => _host.DeviceMonitor;
    private DisplayCleanupPermissionService _permissionService => _host.PermissionService;
    private EnvironmentCheckService _envCheckService => _host.EnvironmentCheck;
    private DiagnosticReportService _diagnosticReportService => _host.DiagnosticReport;
    private AppSettings _settings => _host.Settings;
    private DeviceRuntimeServiceFactory _runtimeFactory => _host.RuntimeFactory;
    private IKeyboardService _keyboardService => _host.KeyboardService;
```

**`_platformService`, `_pathProvider`, `_captureService`, `_autoStartService`의 위임 프로퍼티는 만들지 않는다.** 코드 조사 결과 이 넷은 생성자·`EnsureDefaultPaths`·`InitializeRuntimeFactory` 안에서만 쓰이며, 그 코드는 전부 `ApplicationHost`로 이관되어 `InteractiveHost`에서 사용처가 사라진다. 만들면 미사용 멤버가 된다.

남기는 둘의 근거:
- `_keyboardService` — 996행 `_keyboardService?.Dispose()`
- `_runtimeFactory` — 368행 `_runtimeFactory.Create()`

빌드가 위 넷 중 하나에 대해 "존재하지 않는 이름" 오류를 내면, 그 사용처는 이관되지 않은 코드다. 해당 프로퍼티 한 줄을 추가하고 왜 남았는지 보고한다.

`DeviceRuntimeServiceSet _activeRuntime`, `_selectedDeviceSerial`, `_selectedDeviceIdentity`, `_isRunning`, `_disposed`, `_shutdownStarted`, `_runtimeServicesDisposed` 필드는 TUI 상태이므로 **그대로 둔다.**

- [ ] **Step 3: 생성자를 위임으로 교체**

생성자 본문(원래 40-118행)을 교체한다. TUI 전용 이벤트 핸들러는 유지한다.

```csharp
    public InteractiveHost()
    {
        _host = new ApplicationHost(
            new MacPlatformService(),
            new MacPathProvider(),
            new MacCaptureService(new MacPathProvider().DefaultScreenshotFolder),
            new MacKeyboardService(),
            new MacAutoStartService());

        _host.DeviceMonitor.DeviceConnected += (_, e) =>
        {
            var d = e.Current;
            AnsiConsole.Success($"Device connected: {d.DisplayName} [{d.Serial}]");
        };
        _host.DeviceMonitor.DeviceDisconnected += (_, e) =>
        {
            var d = e.Current;
            AnsiConsole.Warning($"Device disconnected: {d.DisplayName} [{d.Serial}]");
        };
        _host.DeviceMonitor.StateChanged += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(_selectedDeviceSerial) && e.Current.IsConnected)
            {
                _selectedDeviceSerial = e.Current.Serial;
                _host.SelectedSerial = e.Current.Serial;
            }
        };
    }
```

`MacPathProvider`를 두 번 생성하는 것을 피하려면 지역 변수로 뽑는다.

```csharp
    public InteractiveHost()
    {
        var pathProvider = new MacPathProvider();

        _host = new ApplicationHost(
            new MacPlatformService(),
            pathProvider,
            new MacCaptureService(pathProvider.DefaultScreenshotFolder),
            new MacKeyboardService(),
            new MacAutoStartService());

        // 이하 이벤트 구독은 위와 동일
    }
```

- [ ] **Step 4: `_selectedDeviceSerial` 변경 지점을 `ApplicationHost`에 동기화**

`_selectedDeviceSerial`에 대입하는 모든 지점을 찾는다.

Run:
```bash
grep -n "_selectedDeviceSerial = " DexManager.Mac/Hosting/InteractiveHost.cs
```

각 대입 직후에 `_host.SelectedSerial = _selectedDeviceSerial;`를 추가한다. 진단 서비스가 이 값을 콜백으로 읽으므로 누락하면 진단 대상 기기가 어긋난다.

- [ ] **Step 5: 이관된 메서드 제거**

`InteractiveHost`에서 `EnsureDefaultPaths()`와 `InitializeRuntimeFactory()` 메서드 본문 전체를 삭제한다. 호출 지점도 함께 제거한다(생성자에서 이미 사라졌다).

Run:
```bash
grep -n "EnsureDefaultPaths\|InitializeRuntimeFactory" DexManager.Mac/Hosting/InteractiveHost.cs
```
Expected: 결과 없음

확인 결과 `InitializeRuntimeFactory`는 생성자(119행)에서만 호출되고 재초기화 경로가 없으므로, `ApplicationHost`에 별도 공개 재초기화 메서드를 만들 필요가 없다.

- [ ] **Step 6: 공개 표면이 동일한지 확인**

Run:
```bash
grep -n "public .*(" DexManager.Mac/Hosting/InteractiveHost.cs | grep -v "private\|internal" > /tmp/interactive-host-after.txt
diff /tmp/interactive-host-before.txt /tmp/interactive-host-after.txt
```
Expected: 차이 없음(줄 번호만 다를 수 있으므로 메서드명 기준으로 비교한다)

- [ ] **Step 7: 전체 검증**

Run:
```bash
dotnet build DexManager.Mac.sln -c Release /warnaserror
dotnet test DexManager.Mac.sln -c Release
dotnet run --project DexManager.MultiDeviceTests -c Release
```
Expected: 경고 0, `Failed: 0, Passed: 101`, 다중기기 39개 통과

- [ ] **Step 8: TUI 수동 기동 확인**

자동 테스트가 커버하지 않는 실제 기동을 확인한다.

Run:
```bash
dotnet run --project DexManager.Mac -c Release -- --version
dotnet run --project DexManager.Mac -c Release -- --help
```
Expected: 버전 문자열과 도움말이 출력되고 종료 코드 0

기기 없이 대시보드를 띄워 배너와 메뉴가 표시되는지 확인한 뒤 `Q`로 종료한다.

```bash
dotnet run --project DexManager.Mac -c Release
```

> 실제 Galaxy 기기를 이용한 DeX 시작·중지와 overlay cleanup은 이 계획의 검증 범위를 벗어난다. 사용자 확인 항목으로 보고하고 성공을 가정하지 않는다.

- [ ] **Step 9: 커밋**

```bash
git add DexManager.Mac/Hosting/InteractiveHost.cs
git commit -m "refactor(mac): consume ApplicationHost from InteractiveHost

TUI가 자체 서비스 조립을 버리고 Core의 ApplicationHost를 사용한다. 화면
출력과 메뉴 상태 등 TUI 전용 로직만 남긴다. 공개 메서드 시그니처와 관측
가능한 동작은 변경하지 않는다."
```

---

## Task 8: Phase 0 완료 검증과 문서 갱신

**Files:**
- Modify: `docs/superpowers/specs/2026-09-03-macos-gui-design.md` (Phase 0 완료 표기)
- Modify: `docs/SESSION.md`
- Modify: `docs/TODO.md`

**Interfaces:**
- Consumes: Task 1~7 전체
- Produces: 없음

- [ ] **Step 1: 전체 회귀 최종 확인**

Run:
```bash
cd /Users/dave/iWorks/DX-Manager
export PATH="$HOME/.dotnet:$PATH"
dotnet build DexManager.Mac.sln -c Release /warnaserror
dotnet test DexManager.Mac.sln -c Release
dotnet run --project DexManager.MultiDeviceTests -c Release
```
Expected: 경고 0, `Failed: 0, Passed: 101`, 다중기기 39개 통과

기준선 대비 신규 6개(Task 2에서 1, Task 5에서 3, Task 6에서 2)가 늘고 기존 95개는 모두 유지되어야 한다.

- [ ] **Step 2: 패키징 스모크 테스트**

프로젝트 구조 변경이 배포 산출물을 깨지 않았는지 확인한다.

Run:
```bash
scripts/Package-Mac-Release.sh --rid osx-arm64
```
Expected: ZIP과 `.sha256` 생성, 스크립트 자체 검증(실행 권한·아키텍처·외부 경로) 통과

실패하면 Task 3 Step 7의 확인을 다시 수행한다.

- [ ] **Step 3: 스펙에 Phase 0 완료 표기**

`docs/superpowers/specs/2026-09-03-macos-gui-design.md`의 7절 전달 단계 표에서 Phase 0 행의 완료 조건 칸을 갱신한다.

변경 전:
```
| 0 | `Platform.Mac` 분리 + `ApplicationHost` 추출 | 기존 134개 테스트 통과, 기능 변경 없음 |
```

변경 후:
```
| 0 | `Platform.Mac` 분리 + `ApplicationHost` 추출 | ✅ 완료 — 101개 xUnit + 39개 다중기기 통과, 기능 변경 없음 |
```

- [ ] **Step 4: `docs/SESSION.md` 갱신**

문서 맨 위의 "마지막 갱신" 날짜를 오늘로 바꾸고, 최상단에 절을 추가한다.

```markdown
## macOS GUI Phase 0 완료

- `DexManager.Platform.Mac` 라이브러리를 분리해 macOS 플랫폼 서비스 5종을
  실행 파일 밖으로 옮겼다. 네임스페이스는 `DexManager.Mac.Platform`을 유지한다.
- `IPathProvider`에 `IsPortablePackage`를 추가했다. 구현체는 `MacPathProvider`
  하나뿐이다.
- `DexManager.Core/Hosting/ApplicationHost.cs`가 서비스 조립, 경로 기본값 보정,
  런타임 팩토리 초기화를 담당한다. 플랫폼 구현은 생성자 주입으로 받는다.
- `InteractiveHost`는 `ApplicationHost`를 소비하며 TUI 전용 로직만 보유한다.
- xUnit 101개(기존 95 + 신규 6), 다중기기 회귀 39개 통과. Release 빌드 경고 0.
- 실제 Galaxy 기기의 DeX 실기 검증은 수행하지 않았다. 미확인 항목이다.
```

- [ ] **Step 5: `docs/TODO.md` 갱신**

"다음 작업" 절에 항목을 추가한다.

```markdown
- [x] macOS GUI Phase 0 — 플랫폼 분리와 ApplicationHost 추출
  - [x] `IPathProvider.IsPortablePackage` 승격
  - [x] `DexManager.Platform.Mac` 라이브러리 분리
  - [x] `ApplicationHost` 조립 루트 추출
  - [x] `InteractiveHost`를 `ApplicationHost` 소비로 전환
  - [ ] 실제 기기에서 TUI DeX 시작·중지 회귀 확인
- [ ] macOS GUI Phase 1 — Avalonia Desktop 골격과 MainWindow
```

- [ ] **Step 6: 커밋**

```bash
git add docs/superpowers/specs/2026-09-03-macos-gui-design.md docs/SESSION.md docs/TODO.md
git commit -m "docs: record macOS GUI Phase 0 completion

플랫폼 라이브러리 분리와 ApplicationHost 추출 결과를 스펙, SESSION,
TODO에 반영한다. 실기 검증은 미확인 항목으로 남긴다."
```

---

## Self-Review 결과

**1. 스펙 coverage**

| 스펙 요구 | 담당 Task |
| :--- | :--- |
| 3.1 프로젝트 구조 — `DexManager.Platform.Mac` 신설 | Task 3 |
| 3.2 의존 방향 — `Mac → Platform.Mac → Core` | Task 3 Step 3 |
| 3.3 `ApplicationHost` 추출 | Task 5, 6, 7 |
| 3.4 `Platform.Mac` 분리 근거(테스트 가능성) | Task 3, 4 |
| 5.1 기존 134개 안전망 유지 | Task 1(기준선), Task 8(최종) |
| 7절 Phase 0 완료 조건 | Task 8 |

Phase 1~7은 이 계획의 범위 밖이며 별도 계획으로 작성한다.

**2. Placeholder 스캔**

Task 5 Step 3의 빈 메서드 두 개는 Task 6에서 반드시 채워지며, 해당 위치에 경고를 명시했다. 그 외 "TBD", "적절히 처리", 코드 없는 코드 단계는 없다.

**3. 타입 일관성**

- `ApplicationHost` 프로퍼티명은 Task 5 Interfaces에서 정의하고 Task 6·7에서 동일하게 사용한다(`Log`, `Adb`, `RuntimeFactory`, `SelectedSerial` 등).
- Task 7의 위임 프로퍼티는 Task 5가 노출한 이름과 1:1 대응한다.
- `FakePathProvider`는 Task 2가 추가한 `IsPortablePackage`를 구현한다.

**4. 계획 작성 중 확인 완료된 사항**

- `IKeyboardService`는 `CaptureHotkeyPressed`/`ExitHotkeyPressed` 이벤트 2개를 포함한다. fake는 `MacKeyboardService`와 같이 `Trigger*` 메서드로 이를 소비해 `/warnaserror` 빌드의 CS0067을 피한다.
- `CaptureResult` 생성자는 `(string localPath, string remotePath, bool transferredToDevice)`다.
- `InitializeRuntimeFactory`는 `InteractiveHost` 생성자에서만 호출되며 재초기화 경로가 없다.

**5. 남은 실행 시 위험**

- Task 7 Step 2의 위임 프로퍼티는 기존 `readonly` 필드를 표현식 본문 프로퍼티로 바꾼다. 원본 코드에서 이 이름들에 **대입**하는 지점이 있으면 컴파일 오류가 난다. 오류가 나면 해당 지점을 `_host.<프로퍼티>` 직접 사용으로 바꾼다. `_runtimeFactory`는 원본에서 `InitializeRuntimeFactory` 안에서만 대입되며 그 메서드는 Step 5에서 제거되므로 문제되지 않는다.
- `ApplicationHost`가 `Dispose`에서 아무것도 하지 않는 것은 의도적이다. 서비스 정리는 현재 `InteractiveHost`의 종료 경로가 담당하며, 이를 옮기는 것은 동작 변경이라 Phase 0의 범위를 벗어난다.
