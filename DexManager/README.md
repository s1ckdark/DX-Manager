# DX Manager Windows source notes

[Overview](../README.md) · [Building](../docs/BUILDING.md) ·
[English guide](../docs/USER_GUIDE_EN.md) · [한국어 설명서](../docs/USER_GUIDE_KO.md)

## English

Current Windows documentation targets 2.5.0, C# WinForms, .NET Framework 4.6.2 and x64 Windows 7 SP1,
8.1, 10 and 11. There are no external NuGet dependencies for the Windows app.
The bundled runtime is scrcpy 5.0.1; its full file hashes are pinned in `scripts/scrcpy-runtime.json`.
Hardware decoding is selected automatically unless advanced arguments explicitly select it.

Open `DexManager.sln` at the repository root. The included [Companion snapshot](../companion/README.md)
provides the constants and official APK when the canonical shared component is absent.
`CompanionSharedRoot` can select a verified component explicitly; see [Building](../docs/BUILDING.md).

The approved workspace maintains one canonical Companion source/APK outside the two PC products.
`DXDisplayCleanup` is a migration notice. The official Android package is 2.1.6/code13, min API 33,
using the original official certificate and protocol1. Test packages are separate.

Build outputs under `bin/Debug` and `bin/Release` are development outputs, not the final user folder.
The package copies an explicit runtime/license list plus a short README. The fixed ZIP name is `DX-Manager-win-x64.zip`, with a `DX Manager v2.5.0` inner folder. Full guides/FAQ are embedded
in the app and maintained on GitHub; source screenshots are not copied into the binary ZIP.
`config/settings.json`, `config/onboarding.json`, `config/update-check.json`, `config/update-notice.json`, logs, captures and received
files are runtime data. First-run consent is not bundled and is saved separately from settings.

ADB is selected by absolute path: an explicit manual configuration first, otherwise legacy-compatible
ADB on Windows 7/8.1 and the selected scrcpy folder's ADB on newer Windows, with the documented fallback.
Its displayed build comes from `adb version`'s `Version ...` line, not the common1.0.41 banner.

`DexManager.AdbProxy` builds `tools/adb-proxy/DXMAdbProxy.exe`. Only managed scrcpy file drops use it;
DX Manager's device, wireless, screen and recovery commands use the selected real ADB.
Session/phone isolation, Unicode names, collision-safe commits and cancellation must be preserved.

Experimental120 FPS confirms each start, keeps the phone panel on and restores the earlier screen-off preference at30/60. The current verifier has702 Windows checks including English UI/resource coverage.

Every device command captures an explicit serial. New launches prefer authorized USB; live sessions
retain the serial used at launch. Desktop global input is routed only to the focused eligible session.

## 한국어

Windows 2.5.0은 WinForms/.NET Framework 4.6.2 x64 작업선입니다. scrcpy 5.0.1과 공용 정식
Companion 2.1.6/code13을 사용합니다. [빌드 안내](../docs/BUILDING.md)의 공용 컴포넌트 준비 조건을
확인하세요. 예전 Android 프로젝트를 복원해 별도로 유지하거나 테스트 APK로 대체하지 않습니다.

개발 출력과 사용자 배포 폴더는 구분합니다. 배포본에는 실행용 파일·필수 라이선스·짧은 README만 넣고
저장소 스크린샷·개인 설정·동의 기록·로그·디버그 파일을 넣지 않습니다. 도움말과 FAQ는 앱에 내장돼 있습니다.
단, 앱에 내장된 문서 변경은 EXE 재빌드를 필요로 하므로 GitHub 문서 수정과 구분합니다.
