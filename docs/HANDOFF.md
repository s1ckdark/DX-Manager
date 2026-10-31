# Release handoff — Windows 2.5.0

## English — 2026-10-25

The maintainer explicitly authorized the local final commit and GitHub publication.
The product executable remains the 702-check Windows 2.5.0 build from code checkpoint `ccc10a9`.
README, EN/KO guides, 35-question FAQs, release notes and the 16 approved real screenshots are included in source.
The user chose no single-window photo, diagnostics before the environment check, and the default-size connection captures.

- Fixed asset: `DX-Manager-win-x64.zip`; one `DX Manager v2.5.0/` directory, 39 files.
- EXE SHA-256: `D3342F2A2B80DC5945D069CF408412A4DEDEBB6C0A54C78795485CB5DAD5FC19`.
- ZIP SHA-256: `EBB91A332B603C4096B1B1FB6D179F4D45B55F5A63FC657F8B989BEA8DE08764`.
- The maintainer reports that this exact final executable scanned clean. No unprovided engine/count is claimed.
- Official Companion 2.1.6/code13 and scrcpy 5.0.1 are unchanged; the private AI app tracker is excluded.
- The public `companion/` snapshot provides all matching Android source, PC constants, official APK and hashes.
  Canonical Android development remains shared with Store; no Store files or upload artifacts were changed.
- Public main's macOS implementation is preserved. Mac-specific proxy/foundation projects use separate .Mac directories
  so the Windows .NET Framework and macOS .NET 8 builds do not overwrite each other.
- Full verification/hash boundaries are in [verification](VERIFICATION_v2.5.0.md) and [publication notes](PUBLICATION_v2.5.0.md).

Use the fixed [latest Windows download](https://github.com/maze-mei/DX-Manager/releases/latest/download/DX-Manager-win-x64.zip).
Future latest releases serving this link must contain exactly that asset name.
Renaming the old 2.0.1 asset breaks stale direct links; it does not redirect them.

Do not infer future publication or product changes from scan results. Each new release still requires the maintainer's direction.

## 한국어

메이님이 이번 최종 커밋과 GitHub 게시를 명시적으로 승인했습니다.
검사한 EXE·APK·ZIP은 교체하지 않고 최신 문서·실제 사진과 함께 공개합니다.
공용 원본은 한곳에서 유지하고 해당 버전의 Companion 소스 스냅샷을 저장소에 포함합니다.
macOS 코드는 Windows 프로젝트와 분리해 보존하며 Store 작업물은 수정하지 않습니다.
다음 버전의 수정·게시 권한까지 자동으로 승인된 것은 아닙니다.

---

## macOS 포크 인수인계 (s1ckdark/DX-Manager) — 2026-10-31

- 이 저장소는 `maze-mei/DX-Manager`의 포크다. 모든 브랜치·PR·머지는 `s1ckdark/DX-Manager`
  (`origin`)만 대상으로 하며 upstream에는 push하지 않는다. `upstream` 원격의 push URL은
  `no_push`로 고정돼 있다.
- upstream v2.5.0(`f391baa`)은 2026-10-31에 포크 main에 머지했다. Windows 2.5.0 소스·문서·
  `companion/` 스냅샷은 upstream 그대로이고, 포크 고유 작업은 macOS Avalonia GUI
  (`DexManager.Desktop`, `DexManager.ViewModels`, `DexManager.Platform.Mac`)와 그 테스트다.
- macOS 솔루션은 `DexManager.Mac.sln`이다. Mac 전용 proxy는 `DexManager.AdbProxy.Mac/`, Mac
  다중 기기 회귀 테스트는 `DexManager.MultiDeviceTests.Mac/`이며, `DexManager.AdbProxy/`와
  `DexManager.MultiDeviceTests/`는 Windows .NET Framework 프로젝트라 Mac 빌드에 넣지 않는다.
- 검증 명령은 `dotnet build DexManager.Mac.sln -t:Rebuild`, `dotnet test DexManager.Mac.sln`,
  `dotnet run --project DexManager.MultiDeviceTests.Mac`이다. 상세는 `docs/AI_WORKFLOW.md`와
  `docs/MACOS_GUIDE.md`를 따른다.
- 진행 상태와 남은 작업은 `docs/SESSION.md`(상단 macOS 절)와 `docs/TODO.md`("다음 작업")를
  따른다. macOS GUI는 Phase 3(설정 창)까지 구현됐고 실기 검증 일부가 미확인이다
  (`docs/KNOWN_ISSUES.md`의 macOS 포크 절).
- 포크에는 공개 GitHub Release가 없다. macOS 포터블 ZIP은 `scripts/Package-Mac-Release.sh`와
  `.github/workflows/macos-portable.yml`로 만들며 Developer ID 서명·공증은 없다.
