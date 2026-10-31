# F2 UI preview — 2026-10-08

> Historical scope — current documentation at [the documentation index](README.md). This dated record describes its own build/artifact/state; current2.5.0 behavior and checksums are in the user guides and verification record.
> 과거 시점의 작업 기록입니다. 현재 동작·파일 해시는 문서 목차의2.5.0 안내를 따릅니다.


User accepted F2 after company Windows7 SP1/96DPI testing and requested product
integration, Store-matched run-button caption size/placement and compact help
warning boxes. Only the portable worktree is edited. Store was read-only for
comparison and remained clean. No commit, push, tag or upload.

## Changes

- New Forms/UiText.cs paints/measures with the unchanged embedded Pretendard,
  Graphics.DrawString and TextRenderingHint.AntiAlias (F2; no gasp/font edit).
- Native Label creation uses UiLabel with matching GDI+ preferred-size measurement;
  custom buttons, switches, selectors, dropdown rows, path display and numeric/
  hotkey paint/selection/caret measurement use the same text engine. Links and
  standard radio/checkbox controls retain compatible GDI+ native interaction.
- The native path editor remains a TextBox to preserve IME, drag selection and
  native undo. While editing it still uses Windows native text painting. RichTextBox
  log/docs/license bodies also retain native rendering; this preview does not
  claim those native surfaces now use GDI+. Log body Consolas is unchanged.
- Main run/stop caption is explicitly17px SemiBold (12.75pt before main font fit),
  with a20px icon and10px gap, scaled/centered as one measured group. Source:
  Store MainPage.xaml:324-338 and Styles/Controls.xaml:134. Button bounds, enabled/
  blocked behavior and actions are unchanged.
- Help warning inner FlowLayoutPanel no longer has the default100px GrowOnly
  minimum. Both stack and card use GrowAndShrink with fixed content widths and
  an initially1px-high stack. Resulting bottom padding is12px for all checked
  Korean/English warning topics, instead of a fixed blank tail.

## Verification

- .NET Framework4.6.2 x64 Release build: passed, no warnings/errors.
- Existing foundation regressions:62/62 passed.
- Actual product UI fixture with ProcessRunner.BlockNewProcessesForWindowsShutdown:
  DeX and all3single modes, all7settings pages, all5help topics in both themes;
  Korean/English help. Store button size/group marker and warning text containment/
  bottom padding checked.53 PNG files decoded.
- Actual numeric select/replace/value parsing, native path select-all and undo,
  and actual custom dropdown opening/rendering passed on Windows11.
- The first undo fixture used AppendText, which does not create the user-edit undo
  entry. It was corrected to EM_REPLACESEL with undo enabled; no editing logic was
  modified to make that fixture pass.
- Evidence: .build-tools/f2-preview-20261008/Verify-F2Ui.cs, verification.txt, images.
- Fixture source fills the sample phone header and blocks physical phone commands.
  This is actual-control evidence, not a fresh phone/session test.
- Win7 quality for the integrated app, focused native editor/IME and native long
  text bodies remains a field check.125%/150% DPI integration is not independently
  verified in this request. No unsupported rendering success is asserted.

## Delivery

Folder: E:\vs\dex system\dist\DX Manager v2.5.0 F2 preview 20261008
EXE SHA256: 0D2CD7F6CD6FBC52667674CBF8DE6443D2BE8EF2D05E8914B05D55E2E3CA94D7
ZIP: E:\vs\dex system\dist\DX-Manager-v2.5.0-F2-preview-20261008-win-x64.zip
ZIP SHA256: 8BA587DCD9B9146A5BD67AE99C220EB4A15290A54345C1FC458A806D80E944A0
ZIP size:25,623,118 bytes;67 entries. Executable ZIP hash matches disk. Public
config/README.txt is included; no personal settings/onboarding/logs/captures/PDB.
Version remains2.5.0; bundled Scrcpy remains the product's4.1 and Companion2.1.4.
The scrcpy5 decoder trial remains a separate, unchanged package.

Original final2.5.0 EXE and ZIP are preserved, hashes BACEA602... and B2064404... .

## 한국어 확인

ZIP을 새 폴더에 풀고 DXManager.exe를 실행한다. 기존 앱이 실행 중이면 트레이의
종료로 끝낸 뒤 프리뷰를 실행한다. 본체의 DeX/단일창 실행·중지 버튼과 도움말
강조 상자 여백을 확인한다. 회사Win7에서는 정적 화면뿐 아니라 경로 입력 중
한글/드래그 선택, 숫자 편집과 실제 드롭다운도 확인할 필요가 있다.