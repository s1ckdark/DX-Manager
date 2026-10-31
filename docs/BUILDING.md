# Building Windows 2.5.0

## English

Windows uses WinForms, .NET Framework 4.6.2 and x64. Store WinUI and the .NET 8 macOS implementation have separate builds.

### Requirements

- Visual Studio 2019/MSBuild with .NET Framework 4.6.2 reference assemblies.
- The repository's pinned scrcpy 5.0.1 Windows runtime, described in `scripts/scrcpy-runtime.json`.
- The included [official Companion snapshot](../companion/README.md): source, PC constants, release metadata, APK and license.
- No external NuGet packages are required for the Windows application.

### Build a public source checkout

The repository includes everything needed for the Windows build once MSBuild and the reference assemblies are installed.
The developer's external shared folder is optional:

~~~powershell
./scripts/Verify-CompanionSnapshot.ps1
MSBuild.exe DexManager.sln /t:Build /p:Configuration=Release /m:1
~~~

Application output: `DexManager/bin/Release/DXManager.exe` (2.5.0.0, x64).
If needed, pass an installed framework reference root using `/p:TargetFrameworkRootPath=...`.

### Shared source and release pinning

The GitHub and Store workrooms maintain Android only in the canonical shared component.
This repository's `companion/` directory is a generated public release snapshot.
It pins 105 original source files and the exact official 2.1.6/code13 APK; it contains no signing inputs.
Maintainers export from the canonical component with `scripts/Export-CompanionSnapshot.ps1`.
Do not edit this snapshot as another independently maintained Android tree.

Resolution order is an explicit `CompanionSharedRoot`, the maintainer's sibling `DX Companion Shared` directory,
then the included `companion/` snapshot. The MSBuild property and package helper support explicit selection:

~~~powershell
MSBuild.exe DexManager.sln /t:Build /p:Configuration=Release /p:CompanionSharedRoot="X:/shared/DX Companion Shared"
./scripts/Package-Release.ps1 -SkipBuild -Version "2.5.0" -CompanionSharedRoot "X:/shared/DX Companion Shared" -OutputRoot "X:/output" -PackageDirectoryName "DX Manager v2.5.0"
~~~

### Verification and packaging

The maintainer's fixed-workspace verifier is `scripts/Verify-OfficialBuild.ps1`; it uses local test/toolchain fixtures.
Its 702-check record is described in [verification](VERIFICATION_v2.5.0.md).
It is not a claim that every real phone, GPU or OS was tested.
A public checkout can build and run the Windows foundation project without these private fixtures.

After verifying the build, use the generic package helper:

~~~powershell
./scripts/Package-Release.ps1 -SkipBuild -Version "2.5.0" -OutputRoot "X:/output" -PackageDirectoryName "DX Manager v2.5.0"
~~~

The ZIP name is `DX-Manager-win-x64.zip`; the inner directory is `DX Manager v2.5.0`.
Only the explicit 39-file runtime/license payload is copied. User settings, consent/update state, logs, captures,
full source docs/screenshots, PDBs, signing inputs and test programs are excluded.
The two vendor scrcpy PNGs are runtime assets. Verify extracted hashes and fresh first-run consent state.

The maintainer's fixed preview/final wrappers use their approved workspace paths.
They overwrite one preview while preserving user state and back up the previous final before replacing it.
They do not publish to GitHub.

### Android and macOS

The [Companion source guide](../companion/README.md) documents personal debug builds with JDK 17, SDK 36 and min API 33.
Official signed builds require the existing private signing identity and canonical shared build procedure.
Do not replace the verified release APK with an unsigned or debug build.

The [macOS guide](MACOS_GUIDE.md) covers its separate .NET 8 build and scrcpy 4.1 packaging.
Mac-specific proxy/foundation projects are kept separate from the Windows .NET Framework projects.
Windows 2.5.0 features and test counts do not assert macOS validation.

## 한국어

Windows 소스는 MSBuild·.NET Framework 4.6.2 참조 어셈블리·x64 환경을 사용합니다.
공용 Companion 원본에서 내보낸 `companion/` 스냅샷이 포함되어 공개 저장소만 받아도 빌드할 수 있습니다.
원본은 한곳에서 유지하며 동봉 소스·APK의 해시를 검증합니다. 서명 비밀은 포함하지 않습니다.

배포 ZIP 이름은 고정하고 내부는 버전 폴더로 구분합니다. 개인정보·로그·캡처·전체 설명서·사진·디버그 파일을 제외합니다.
메이님이 검사한 배포 파일은 문서·공개 소스 연결 작업 때문에 교체하지 않습니다.
macOS와 Store의 코드·런타임·검증 범위는 Windows와 구분합니다.
