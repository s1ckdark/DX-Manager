# Work, documentation and release workflow

## English — 2026-10-24

1. Read AGENTS/current handoff/TODO, then inspect the active branch and changes. Preserve user work.
2. Windows portable, Store WinUI and macOS are separate implementation scopes. Store stays read-only in this workroom.
3. Diagnosis/review does not authorize product edits; documentation changes do not authorize publishing or replacing verified binaries.
4. A clean antivirus result is evidence for that exact file, not commit/push/tag/release approval.

### Windows code verification

Use the approved .NET Framework 4.6.2 x64 Release environment and `scripts/Verify-OfficialBuild.ps1`.
Its current fixtures cover702 checks, including48 update,196 desktop and 36 English-localization checks.
The English fixture scans application-owned resource/control text, all Settings pages and initial/completed diagnostics;
it checks English Help fit and unchanged Korean size. Native OS/vendor/user data are separate.

The maintainer verifier uses fixed paths and test-only roots. See [Building](BUILDING.md) for prerequisites.
Repeat relevant code gates when code/embedded resources change. For packaging an identical verified EXE, confirm byte identity
and payload/runtime/hash/privacy checks instead of claiming a new full feature test.
Distinguish automated/fixture, maintainer real-use and fresh physical-device results.

### Shared Android component

Maintain official Android source in the one shared component; read its own instructions first.
Use the existing official identity/signature and wrapper scripts. Record workroom/chat attribution and version/change history.
Signing inputs never enter Git or a public ZIP. Never substitute the private AI test APK.
A documentation-only pass does not rebuild/install Android.

### Artifacts and personal state

- Fixed preview: overwrite `E:/vs/dex system/dist/DX Manager Preview`, preserving config/logs/captures/receive data.
- Final: `scripts/Build-FinalRelease.ps1` backs up the previous final, then packages
  `E:/vs/dex system/dist/DX-Manager-win-x64.zip` with one versioned root folder.
- Final39-file contents are explicit: executable/config, Korean resources, proxy, legacy ADB, pinned scrcpy,
  verified official APK, required licenses, compact README/config README.
- Exclude settings, onboarding/update state, private paths/serials/tokens, logs/captures, source guides/images, PDBs,
  signing material and test programs. Vendor runtime images are not guide screenshots.
- Check extracted ZIP hashes against final files, version identity, fresh-consent state and original APK/runtime pins.

Keep the Windows asset name `DX-Manager-win-x64.zip` identical across releases for the fixed latest-download URL.
Every release selected as latest for that URL must include it. A specific old-tag URL does not redirect to a new file.
Preserve superseded local upload archives outside dist before removing duplicate delivery names.

### GitHub documents and images

English precedes Korean in bilingual pages. Keep historical release facts/license text unchanged.
Use reviewed real portable photos with accurate state/language captions; never present fixture/Store/private-test captures as the portable release.
Current selection omits single-window photos and uses diagnostics before checking plus default-size connection pages.
Do not regenerate/edit original screenshot pixels without the user's instruction.

Inventory current public main before replacing its README. Preserve macOS documentation/source/workflow;
do not erase remote-only work by force-pushing this Windows branch.
Check links/anchors/HTML image paths, matching EN/KO question numbers, version/filename/menu facts and final artifact preservation.
Embedded help is a product-resource snapshot; web-document changes alone do not alter the final executable.

### Publication

Before explicit publication: complete docs/photos, public shared-component source availability and main/macOS reconciliation.
Commit only on request. Push/tag/release creation and old GitHub-asset renaming require the maintainer's publication direction.
Never treat “all scans clean” as upload approval. Report remaining gates without pretending a local ZIP is already published.

## 한국어

코드·문서·프리뷰·검증된 배포 파일·공개 GitHub 상태를 구분합니다.
현재 고정 ZIP·버전 폴더 규칙과 개인정보 제외를 지키며, 같은 EXE의 문서 작업 때문에 기능 검증을 반복하지 않습니다.
영어 초기 상태·한글 혼용·잘림과 링크·사진 상태를 점검하고, 사용자 원본과 정식 APK·서명·스토어 작업물을 보존합니다.
공개 소스 의존성과 macOS 보존을 마친 뒤 메이님이 게시를 지시해야 GitHub를 변경합니다.

## macOS fork workflow (s1ckdark/DX-Manager)

The sections above describe the upstream Windows 2.5.0 workroom. In this fork, macOS work follows
`docs/MACOS_GUIDE.md` and these commands from the repository root with the .NET 8 SDK pinned by `global.json`:

```bash
dotnet build DexManager.Mac.sln -c Release -t:Rebuild
dotnet test DexManager.Mac.sln -c Release
dotnet run --project DexManager.MultiDeviceTests.Mac -c Release
scripts/Package-Mac-Release.sh --rid osx-arm64   # or --rid osx-x64
```

`DexManager.AdbProxy/` and `DexManager.MultiDeviceTests/` are the Windows .NET Framework projects; the Mac
builds use `DexManager.AdbProxy.Mac/` and `DexManager.MultiDeviceTests.Mac/`. Branches, PRs and merges target
`s1ckdark/DX-Manager` only; nothing is pushed to `maze-mei/DX-Manager`.

위 절은 upstream Windows 2.5.0 작업 흐름이다. 이 포크의 macOS 작업은 `docs/MACOS_GUIDE.md`와 위 명령을
기준으로 빌드·검증·패키징하며, 모든 브랜치·PR·머지는 `s1ckdark/DX-Manager`만 대상으로 한다.
