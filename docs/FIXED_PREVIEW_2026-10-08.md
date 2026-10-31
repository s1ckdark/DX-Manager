# Fixed portable preview and contrast — 2026-10-08

> Historical scope — current documentation at [the documentation index](README.md). This dated record describes its own build/artifact/state; current2.5.0 behavior and checksums are in the user guides and verification record.
> 과거 시점의 작업 기록입니다. 현재 동작·파일 해시는 문서 목차의2.5.0 안내를 따릅니다.


User authorized the agreed contrast/icon changes and dist cleanup. Mini-bar is
explicitly excluded: its source and icon geometry remain unchanged in this turn.

## UI

- Keep font files, point sizes and main font-fit behavior. Main field labels now
  use TextSecondary instead of TextTertiary; navigation captions use TextPrimary.
- Windows10/11 use GDI+ ClearTypeGridFit, including matching measurement. Windows7/
  8.x retain F2 AntiAlias. Product/harness manifests declare Windows10 so OS version
  detection follows the actual supported host. The first manifest-less fixture
  reported the compatibility version; it was corrected to use the product manifest.
- Main/settings ordinary icons use20px: sidebar/action glyphs, option glyphs,
  help menu and SymbolView. Main option icons use TextPrimary and consistent spacing.
  Main connection badge gets20px glyphs with measured spacing. The large custom
  phone artwork and original logo are preserved. Mini-bar geometry/resources stay.
- Settings capture-hotkey card now uses Capture/Screenshot and wireless ADB group
  uses Wifi/WiFi1, as in Store. No Store product files were changed.
- Release build/62 foundation regressions passed. Real main/all settings/help UI,
  Korean/English warning sizing, actual number editing, native path select/undo,
  dropdowns, icon mappings and modern-Windows/Win7 rendering-policy checks passed.
 53 PNGs decoded. Current fixture: .build-tools/preview-validation/verification.txt.
- Integrated Win7 native edit/IME and native long-text quality are still field checks.
  No claim is added for all DPI configurations. No commit/push/tag/upload.

## Fixed preview

Run scripts/Build-PortablePreview.ps1 (or -SkipBuild after a verified build).
It stages the public release payload with Package-Release.ps1 -NoArchive and then
updates only managed files in E:\vs\dex system\dist\DX Manager Preview. It preserves
user configuration, acknowledgement, logs and captures. A manifest removes only
obsolete managed files; absolute/traversal/user-data/reparse-point paths are rejected.
The fixed folder itself is never recursively removed. No ZIP is generated.
A second overwrite preserved4 existing user config files and matched62 payload files.

EXE: E:\vs\dex system\dist\DX Manager Preview\DXManager.exe
SHA256: 17D9DE7FE8B0AE053129F8C2146CB0017F5617E2782B5E34DF88E1E355E9142A
Runtime remains Scrcpy4.1 and Companion2.1.4. Version remains2.5.0.

## Authorized dist cleanup

The user named DX Companion and DX Manager Store as protected and authorized
removing other folders and all root archives, with judgment to preserve essentials.
Before deleting, each resolved direct-child path was checked against the exact dist
root, and nested reparse points were checked.34 direct entries were removed.

Remaining root folders:
- DX Companion (original protected files untouched; GitHub2.1.4 saved inside it)
- DX Manager Store (protected, unchanged)
- DX Manager v2.5.0 (original upload-candidate EXE/payload preserved)
- DX Manager Preview (single overwrite location)

DX Companion/GitHub2.1.4 APK hash equals the pinned95DAF4E7... official artifact.
Original2.5.0 EXE hash remains BACEA602... . All root ZIP/checksum clutter, old release/
font-test/scrcpy5-test folders were deleted by user request. Historical documents'
old paths/checksums describe prior artifacts, not currently available dist files.
Source/build caches remain available for rebuilding when needed.

Old user settings/capture data were copied to:
.build-tools/dist-cleanup-20261008/user-data
The prior F2 preview's config was migrated into the fixed preview. File contents were
not printed or committed. Deletion plan: .build-tools/dist-cleanup-20261008/delete-plan.json.

## 한국어

프리뷰는 앞으로 DX Manager Preview의 DXManager.exe를 실행한다. 폴더를 덮어써도
기존 설정/동의/로그/캡처는 유지한다. 앱 실행 중에는 트레이 종료 후 갱신한다.
ZIP을 매번 만들지 않는다. 미니바는 이번 요청에서 수정하지 않았다.
## 2026-10-08 ClearType regression correction

메이님이 현대 Windows에서 큰 한글 제목의 획이 거칠어진 실제 화면을 보고했다. 이전 요청에서 추가한 ClearTypeGridFit 분기가 원인 후보이며, 안티앨리어싱을 끈 변경은 아니었다. 해당 픽셀 맞춤 분기를 제거하고 모든 지원 OS의 UiText 표시/측정을 검증된 F2 AntiAlias로 복원했다. 아이콘20px·색 대비·Store 캡처/Wifi 매핑·버튼 정렬·도움말 높이는 유지한다. Release와 실제 UI/경로 선택·undo/드롭다운/경고 상자 검증 통과. 고정 Preview 폴더만 갱신했고 ZIP은 만들지 않았다. 최신 EXE SHA256: 56D2D1AB00A4A2372F09E4E8CE4793528C4E60B655930AF8826ABF9F20AE1555. 이전 FIXED_PREVIEW 문서의 OS별 분기는 이 기록으로 대체한다. 집 PC에서 실제 제목 모양 재확인 필요. 커밋/업로드 없음.