# Store UI backport — 2026-10-01

> Historical scope — current documentation at [the documentation index](README.md). This dated record describes its own build/artifact/state; current2.5.0 behavior and checksums are in the user guides and verification record.
> 과거 시점의 작업 기록입니다. 현재 동작·파일 해시는 문서 목차의2.5.0 안내를 따릅니다.


## 2026-10-01 Reported UI defects

메이님이 9월 30일과 10월 1일에 지적한 UI 문제를 수정했다. 최종 크기 기준 창 중앙 배치,
연결이 끊긴 폰의 선택기 제거 및 시작 비활성화, 설정 선택기의 이름만 표시, USB/Wi-Fi
수직 중앙 정렬, 일반 글자 +2 DIP, 하단 도움말/설정 위치, 긴 옵션/버튼 공간을 조정했다.
둥근 카드/버튼에 실제 Region을 적용했고 설정 펼침은 40px 클릭 영역과 그린 꺾쇠로 바꿨다.
고급 인수 입력칸을 위로 옮기고 접을 때 스크롤 범위를 초기화한다. 메인/설정/도움말/문서
패널의 스크롤은 Win7 호환 GDI 막대를 사용한다. 페이지 전환은 redraw/layout을 묶었다.
사용 설명서/FAQ는 별도 스크롤 패널과 카드/머리말/닫기 버튼을 갖고 포터블 문서를 사용한다.
진단 하위 패널의 테마 갱신 누락을 수정하고 Store 원본의 폰별 둥근 카드(14 padding,
10 radius, 최소 높이 150)를 반영했다. Store 작업트리에는 쓰지 않았다.
회귀 Debug/Release 59/59. 격리 UI에서 중앙 배치, 연결 해제, 이름 표시, 고급 접기,
문서 스크롤 분리와 설정 저장을 확인했다. 실제 화면 캡처는 computer-use 앱 승인이
시간 초과돼 확보하지 못했다. RichTextBox 본문은 DrawToBitmap에서 빠지는 제한이 있으며
해당 빈 캡처를 실제 본문 누락으로 단정하지 않는다. Win7/실제 폰/다른 DPI 및 전환 시
체감 깜빡임은 사용자 확인 필요. 이미지마다 별도 시간 폴더를 사용한다.
빌드는 bin/Release의 검증용이며 커밋/업로드/배포는 수행하지 않았다.

## Scope and baseline

Portable branch: feature/portable-backport-v2.0.1-20260928, local v2.0.1 / 2a4aff2.
Store reference: E:\vs\dex system-store / 8b267c2, read-only throughout.
The user requires Store layout, controls, typography, messages and settings behavior.

## Implemented

- The clipped primary button was covered by overlapping status labels. Their bounds
  are now separated; the extra legacy detail line is hidden. Icon-only refresh
  buttons center their glyph and request the existing monitor's immediate poll.
- Store Pretendard Variable outlines are instantiated at weights 400 and 600 for
  Windows 7 GDI. Embedded static derivatives use DX Store UI family names to honor
  the OFL reserved font name. Fonts load privately; system installation is unnecessary.
  Source DIP roles are converted to points once: body 15, controls 14, caption 12,
  section 20, phone heading 30, settings heading 28, help heading 26.
- Store design sizes: main 1200x800, settings/help 1040x760, log 1040x680.
  The main content scales uniformly to the work area. Native window captions remain.
- Source-derived 49 setting cards/expanders cover General, Input, Connection,
  Companion, Paths and Diagnostics; About uses the Store notice/support structure
  with the portable version. Every named editor and action is mapped explicitly.
- No bulk settings Save button. Toggles and choices save their own configuration
  scope; address and target-folder drafts commit on editing/action/close boundaries.
  Hangul and Enter input modes persist independently. Hotkey editing suspends and
  restores capture hotkeys. Connection and Companion selectors synchronize and
  actions resolve the selected physical phone's authorized transport.
- Companion status ignores old target results. Phone operations retain their
  captured serial; selectors are disabled while Companion work is pending.
- Store offline help articles, six topic cards, summaries, steps, warning blocks
  and a separate guide/FAQ viewer are included in both languages.
- Info/success notices dismiss after five seconds; warnings/errors stay until closed.
  First-run notice uses a checkbox and separate onboarding.json version 2. A general
  settings reset does not remove acknowledgement. Save failure permits this launch
  and reports that acknowledgement may repeat, matching the Store policy.
- Existing USB/Wi-Fi isolation, settings, field-tuned input corrections, transfer
  protocols and Companion 2.1.3 APK remain the portable runtime foundation.

## Verification

- .NET Framework 4.6.2 reference assemblies, x64 Debug and Release build.
- 59 regression checks in each configuration, including independent acknowledgement
  state and corrupt/unwritable persistence behavior.
- UI harness with all process creation blocked: embedded font load, light/dark
  main/help/log, all seven settings pages, small-window layout, independent input
  modes, immediate toggle persistence, shared-storage root preservation,
  acknowledgement persistence/corruption and persistent error notice.
- Review image directories contain a timestamp and are never reused. Mock phone names
  in these images are fixtures, not evidence of a fresh phone test.

## Remaining real-device validation

Windows 7, non-96 DPI, connected S26/S20 FE selector changes, actual hotkey capture,
USB/Wi-Fi mode changes, Companion operations and new live UI must be checked by the
user. Automated rendering does not establish these physical results.

No commit, push, tag, GitHub upload or Store file/submission replacement was performed.
## 2026-10-01 Follow-up UI corrections

General text is now +1 DIP over the Store source sizes (the earlier +2 adjustment was reduced).
Enabled labels have a measured safe width. Setting card widths follow each page's actual client
width after scrollbar changes. The content viewport has an additional 12 DIP of footer clearance.
Device diagnostics use a right-aligned 160x40 Refresh button in the header; the summary's old
58-pixel minimum was reduced to 22, bringing the phone cards up. Help topic cards are 74 DIP
high and fit the default window without vertical scrolling.
The log buttons overlapped the description label by approximately five pixels. Their toolbar
was moved up and brought to the front, and explicit button bounds replaced the flow layout.
RichEdit keeps its native scrolling and hit testing while its non-client gutters are painted
with the current theme. The scrollbar painter was checked using real control scroll metrics.
Panel scrollbars cover the whole native gutter instead of repeatedly hiding/restoring it.
The background bitmap is cached by size/theme, and child redraw composition is enabled on
buffered panels using the Windows 7-compatible style. Actual perceived scrolling smoothness
and removal of the user's live-screen white strip still require a fresh runtime check.
Advanced options use a separate gray placeholder (examples: --no-audio --video-codec=h264).
The Text value remains empty; the placeholder cannot enter saved settings or launch arguments.
Guide/FAQ scroll offsets are now explicitly retained when switching tabs.
The isolated harness passed label/menu/button geometry, non-overlap, placeholder separation,
independent document scrolling, settings persistence and diagnostic rendering. New screenshots
are under .build-tools/ui-review/20261001-201242. They use fixture phone data.
## 2026-10-02 Settings editor geometry and consistent scrollbars

The user's live screenshots exposed a regression in StoreSettingCard.Reflow: the stacked branch
permanently overwrote every editor width, including toggles. Wider layouts did not restore their
source dimensions. Each editor's intended rectangle is now registered before adding it to the
host and restored during reflow. Only wide diagnostic flow content is stretched. The earlier
preview checks omitted the narrow-to-wide layout transition; they were insufficient UI evidence.
Wheel scrolling updated AutoScrollPosition without refreshing the custom thumb. Panel wheel
paths now refresh it, and native window observations capture other programmatic changes.
Scrollbar positioning uses WinForms ScrollProperties.Maximum-LargeChange+1 rather than
DisplayRectangle.Height-ClientSize.Height, which omitted 16 pixels of bottom padding in the
reproduced settings case (205 instead of 221). End-of-scroll checks confirm the last card fits.
A single stable thin scrollbar now covers panel and RichEdit gutters. Native log scrollbar
repainting was removed, eliminating two painters that could show different hover appearances.
Both axes, empty horizontal gutters, help/docs, environment checks, notices and license viewer
use the shared painter. Dropdown indicators use the same thin rounded thumb and theme color.
Settings content ends at the footer boundary; log buttons and description have matching bottom
bounds without overlapping. Help topic text is centered as a group; icons are 22 px and text
starts at x44. Advanced arguments moved up 6 DIP. Its preview surface starts in front of the
native editor; a native cue banner also handles the empty focused editor without changing Text.
The cue API's Vista+ support and focus behavior were checked in Microsoft documentation:
https://learn.microsoft.com/en-us/windows/win32/controls/em-setcuebanner
Validation: final Release-linked harness includes editor shrink/restore, visible toggles, log
scroll offset changes, thumb synchronization, placeholder separation, and end-of-scroll bounds.
The 20261002-011420 folder records the exact tested executable SHA-256 in verified-build.txt.
Computer-use captured an actual desktop fixture showing restored controls and a dark thin gutter.
The fixture closed before injected wheel testing; no successful real wheel gesture is claimed.
Offscreen DrawToBitmap still renders native gutters incorrectly and is not live-screen proof.
Windows 7, non-96 DPI, and connected-phone asynchronous refresh remain user validation items.
No Store worktree edits, commit, push, tag or distribution packaging were performed in this task.
## 2026-10-02 Single-window preflight and shared rendering polish

Single-window app selection is checked before ApplyRunSettings, operation-state changes,
device delay, managed-screen tracking and the try/finally wake schedule. Missing selection
shows a persistent notice and returns immediately. DeX remains app-optional. The primary
button uses an ExecutionBlocked paint state rather than Enabled=false for this condition,
retaining click feedback while preventing actual start work. App selection restores styling.
The isolated harness checked immediate task completion, unchanged log entries and wake-timer
state, DeX's optional app selection, and gray clickable styling with selection recovery.
Rounded panels and buttons no longer use binary rounded window regions that cut antialiased
edge pixels. They repaint their actual parent surface and use inset contours. Shared scrollbar
layers repaint their parent surface at the same coordinates rather than a flat color strip.
Error notices use a neutral dark rose / pale light rose palette, a matching border and opacity
230/255; labels preserve the painted background. Native title bars remain unchanged.
The two bundled executable paths now share the storage-folder field's starting edge and use
496-pixel fields. Diagnostic body layout owns its current width/height and handles empty,
one-phone and multiple-phone results without the fixed snapshot/minimum-height regression.
Environment result labels can use a full-width measured body. Companion bundle/install rows
are removed from presentation; verification logic remains, and status/refresh are consolidated.
Dark-theme mail links use a readable lavender color.
Release-linked UI harness passes the above checks plus previous settings/scroll tests. Debug
and Release regression suites each pass 59. Preview artifacts are immutable per timestamp;
20261002-201154/verified-build.txt records the exact executable hash. Notice and scrollbar
layers are separately rendered to compensate for DrawToBitmap child-order/native-gutter limits.
These are rendered control previews with fixture device data, not live phone test evidence.
Computer-use live capture was attempted but app access approval timed out. Windows 7, other
DPI, live device refresh and real single-window/DeX launching require user validation.
No Store worktree edit, commit, push, tag, GitHub upload or release packaging was performed.
## 2026-10-02 Resolution selection and stable settings width

The custom editor action now follows the three fixed presets, with a separator and saved
custom dimensions underneath. Accepting the editor stores the dimensions in memory and selects
the matching numeric entry immediately. Recent numeric entries retain custom-resolution semantics
for persistence; cancel restores the previous dimensions. Mode-switch persistence was checked.
The topmost dropdown closes before dispatching selection. Its deferred focus callback skips
disabled owners, so it cannot steal focus from a modal editor. The editor activates on Shown,
selects the width number initially and retains select-all on a click in either numeric field.
Settings reserve the native scrollbar gutter even when no scrollbar is present. The viewport
extends to the right edge while cards retain one stable width derived from outer viewport width.
Close-button right bounds match card right bounds. Expansion no longer changes card widths.
The final Release-linked harness passed ordered resolution entries, immediate numeric display,
mode-switch persistence, initial/click numeric selection, invariant expansion width, nested child
right bounds and footer alignment, plus previous checks. Debug/Release regressions each passed 59.
Control-rendered snapshots: .build-tools/ui-review/20261002-205452. Real desktop modal activation,
other DPI and Windows 7 remain user validation items. No Store edit or Git publication occurred.
## 2026-10-03 Main notice state, status copy and review onboarding

The theme-refresh path still reapplied the old opaque notice label and green error border.
It now uses the same ApplyDesktopNoticeTheme method as initial display. Notice corners restore
the underlying app-card surface, including its parent gradient, rather than the form background
alone. Missing-app notices carry their own reason, originating slot and physical identity;
selecting an app in that context closes this notice while unrelated errors remain persistent.
Idle authorized DeX says "DeX 실행 준비됨". Single-window slots distinguish "준비됨" with an
app and "대기중" without one. Actual app selection refreshes the indicator. Running, failed,
offline and unauthorized states keep their existing handling.
The display/options/start-app descriptions are hidden and section labels use a fixed-height,
vertically centered box matching their icon's center; AutoSize cannot override that alignment.
PortableOnboardingService.RepeatNoticeForUiReview is enabled at the user's request. Startup shows
the consent notice on every process launch until the user requests otherwise, without deleting
acknowledgement or general settings. A lifecycle test verifies repeated display after persisted
acceptance and unchanged acknowledgement data. This is a review-build preference, not a new UI
setting; it must not be silently disabled by later tasks.
The user's confirmed Galaxy S26 Ultra / SDK37 (Android17) / OneUI9.0 environment has a separate
UserVerifiedEnvironment informational classification. Matching is scoped to that display name,
SDK and version; other models and SDK/OneUI combinations remain unverified. This changes no
launch or permission gates and relies on the user's reported physical test.
Verification: .NET4.6.2 x64 Debug/Release, 60 regressions each. The final executable-linked
UI harness passed notice transparency after theme changes, corner pixels against the actual
underlying card, targeted dismissal versus unrelated errors, exact idle status copy, section
alignment, and scoped compatibility matching. Previews: 20261003-011841; verified-build.txt
records the executable hash. These are rendered fixtures, not fresh physical-phone evidence.
No Store edits, APK changes, commits, push, tag or release packaging were performed.