# Official DX Manager 2.5.0 update — 2026-10-15

> Historical scope — current documentation at [the documentation index](README.md). This dated record describes its own build/artifact/state; current2.5.0 behavior and checksums are in the user guides and verification record.
> 과거 시점의 작업 기록입니다. 현재 동작·파일 해시는 문서 목차의2.5.0 안내를 따릅니다.


## Scope

The user accepted the private preview and authorized updating the official portable2.5.0.
Worktree: E:\vs\dex system-portable-backport.
Branch: feature/portable-backport-v2.0.1-20260928; starting HEAD8b19906.
Existing shared-Companion migration and other uncommitted changes are preserved.
Store and private AI sources/artifacts are not edited. No commit or publication is authorized.

## Included

- Complete official scrcpy5.0.1 x64 runtime, with all16 source/copied files pinned by hash.
  DeX and all3 single-window slots explicitly include --hwdec=auto on supported versions.
  Explicit user decoder overrides remain single and unchanged; legacy ADB and .NET4.6.2 remain.
- Latest accepted F2 rendering in both themes, dark text contrast, both Store-style transfer
  windows, per-device/slot missing-app notices, EXE relaunch restoration, full auxiliary-window
  closure on tray transitions and wireless responsiveness fixes.
- Companion management selects authorized USB first, otherwise the selected phone's live
  wireless/TLS transport. The shared official2.1.5/code12 APK is bundled and verified.
- Automatic update checking on the first launch of each local calendar day. Persist the
  attempt before networking; an offline failure still consumes that day's automatic attempt.
  No interval polling or automatic retry loop. Manual checking remains available in Settings/About.
- Overall5-second deadline, bounded API response, one logical request at a time and at most
  one outstanding physical HTTP request. Late results cannot update UI. Shutdown cancellation
  runs away from the UI thread. Network/JSON work never executes on the UI thread.
- Stable GitHub release and numeric version comparison. Release links are restricted to this
  repository's HTTPS tag pages. Same-version notification and user-dismissal state is retained.
  State is separate at config/update-check.json; settings.json is not migrated or overwritten.
- Main-window themed update banner, tray notification when hidden/minimized, and themed
  owner-centered results for manual checking. Pending dialogs close with Main-to-tray.
- Environment inspection reads the selected scrcpy executable's complete version, including
  patch5.0.1. An unreadable file reports failure rather than the probe's compatibility fallback.

## Explicitly excluded

No accessibility foreground tracking, app/window snapshots, automatic AI Enter rules,
Gemini WebAPK selectors, private package IDs, test ports or protocol3.
Official Guardian protocol remains1. Existing Scroll Lock input correction is retained.
Android source/APK are not rebuilt or modified; common official source management remains canonical.

Shared APK SHA256:
F458D0E644443C8C5D266AE7947D449AD7A9116138A67C1A4A932C57CE98231B

scrcpy official archive SHA256:
B12A2C4EE8BE317422451FC7DCF8EE20A71B5EA7EF9AD73DDD825A25316227A5

## Build and evidence

scripts/Verify-OfficialBuild.ps1 performs the build and actual-assembly checks.
scripts/Build-PortablePreview.ps1 updates the single existing official fixed preview;
normal builds run verification, while -SkipBuild packages already verified outputs.
No new dist version folder or ZIP is produced. Managed output lists protect existing config,
onboarding, logs, captures and receive data. Runtime identity is in official-build-identity.json.

Evidence directory: .build-tools/official250.
Mock release tests do not create or publish a GitHub release.
The independent update-ui-preview/UpdateNoticePreview.exe opens the real themed dialogs with
example versions2.0.0/2.0.1, without an Internet check or user settings changes.
Its release link uses the real existing upstream2.0.1 page.

Native window captures are used for the main banner: WinForms DrawToBitmap omits an
overlapping late-added panel in this test host. The banner itself and native Main captures
show its actual buttons and text. Ordinary dialog/transfer/setting captures use DrawToBitmap.
The fixtures never inspect another application's window or contact a phone.

Final build verification, identity and preservation results are recorded below after packaging.

## Validation boundary

The user reported no observed problems in the private test. That is accepted for that scope.
Development verification does not claim fresh Galaxy streaming, actual Windows shutdown or all
Windows7/DPI combinations. Physical shutdown recovery remains a final field check.
The old frozen2.5.0 folder is historical and preserved; the current review candidate is the
single DX Manager Preview path. GitHub upload follows virus scanning and explicit user instruction.

## 한국어 요약

정식2.5.0에scrcpy5.0.1·명시적 자동 디코딩과 테스트판의 UI/트레이/무선/전송 수정을 반영한다.
APK는 공용 정식2.1.5를 사용하고 앱 감지·AI 키맵·테스트 패키지/통신은 제외한다.
기존Scroll Lock 수동 Enter 보정은 유지한다.

업데이트는 매일 첫 실행 때 한 번 자동 확인한다. 실패도 시도로 저장하므로 오프라인에서
재시작해도 반복 접속하지 않는다. 수동 확인 버튼은 설정→정보에 배치한다.
백그라운드5초 전체 제한·중복 방지·늦은 응답 무시를 적용하고 안내 UI는 테마를 따른다.
환경검사에는 실제 선택된scrcpy의 전체 버전을 표시한다.

새 버전 안내는 메인 배너/트레이 알림이며 수동 결과는 설정창 가운데에 뜬다.
인터넷 없이 알림창을 보는 개발용 미리보기를 따로 준비한다.
기존 설정·보관한 테스트판·공용 APK와 Store는 보존하며 고정 공식 프리뷰만 덮어쓴다.

## Final completion record

- x64 .NET Framework4.6.2 Release:0 warnings,0 errors.
- Fresh checks: foundation62 + updates48 + actual transfer17 + transfer UI/management149
  + wireless10 + actual runtime13 + desktop/update/settings UI176 =475 passed.
- The timeout and shutdown checks include slow network-abort callbacks. Neither path waits
  for the abort on the UI/result path; late responses remain ignored.
- Actual public GitHub query through the new client: Current,373ms in this environment.
- Main update banner, available/current/offline/busy/checking dialogs, About button and
  expanded full-version diagnostics captured in both themes and visually inspected.
- Source, build output, clean staging and fixed preview match all16 pinned runtime files.
- All11 pre-task preservation records match; Store status is unchanged.
- Preview contains only the official Companion APK and font license in tools/companion.
- Official Guardian1, shared Companion2.1.5/code12 and absence of AI types verified in the assembly.
- Current EXE SHA256: 49A409F2255971FBDF25252A5A1AA7A1BB4D915E5C8B3FA29E8B626407D2FBB2
- Current APK SHA256: F458D0E644443C8C5D266AE7947D449AD7A9116138A67C1A4A932C57CE98231B
- Current preview: E:\vs\dex system\dist\DX Manager Preview\DXManager.exe
- Offline dialog preview: E:\vs\dex system-portable-backport\.build-tools\official250\update-ui-preview\UpdateNoticePreview.exe
- No commit, tag, push, ZIP or GitHub/Store publication performed.
