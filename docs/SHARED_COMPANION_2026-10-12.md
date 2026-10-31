# Shared Companion handoff / 공용 Companion 인계 (2026-10-12)

> Historical scope — current documentation at [the documentation index](README.md). This dated record describes its own build/artifact/state; current2.5.0 behavior and checksums are in the user guides and verification record.
> 과거 시점의 작업 기록입니다. 현재 동작·파일 해시는 문서 목차의2.5.0 안내를 따릅니다.


Performed by GitHub workroom dx manager 깃헙판 ui 업데이트,
thread01a0e7aa-4ff2-76b3-a282-8125eab39e62, explicitly requested by 메이님.
Receiver: DX Manager v2.0.0 이후 스토어버전, thread01a0298a-8dcc-79c0-a981-5ac0e30679d5.

Canonical folder: E:\vs\DX Companion Shared.
- Official source: source; verified APK: release/DX-Companion.apk,2.1.5/code12.
- Official package/certificate unchanged. Exact APK SHA256 F458D0E644443C8C5D266AE7947D449AD7A9116138A67C1A4A932C57CE98231B.
- release.json and integration/CompanionRelease.cs are consumed by both PC products.
- README.md/AGENTS.md/CHANGELOG.md require GitHub vs Store actor, exact title/ID, version,
  changed files, hashes and verification. Original duplicate-share2.1.5 fix is attributed to Store.
- Both product Android build/package wrappers now delegate to the common folder.
- Old tracked Android sources are in each product .build-tools/companion-before-shared/DXDisplayCleanup;
  old DXDisplayCleanup/README.md redirects here. Signing properties/keys were preserved at their paths.
- Store project content and its tests compile/link the shared metadata; portable build/package uses
  the same official APK. No Store UI, MSIX, published package or production preview was replaced.

Private AI test stays under E:\vs\dex system-ai-keymap-test, package
io.github.mazemei.dxdisplaycleanup.aikeymaptest, separate signing identity and37133/37134 ports.
It is rebased on the exact latest2.1.5 source plus existing event-driven app/window tracking,
private Guardian3 protocol and native cache-only Enter profiles. It uses code3 and versionName
2.1.5-ai-keymap-test; the private APK must never be copied to official release.
Private share target labels explicitly say AI Test/테스트판. Store duplicate request/owner guards
are present. User must update only the private Companion after this rebuild.

The portable private Companion status lookup now selects authorized USB first, otherwise actual
wireless/TLS on the selected physical device, independently from the saved launch mode.
Private Windows transfer UI follows Store styling in both themes; official UI patch is prepared
for later user acceptance. This handoff is information only, not a request to run more work.

Fresh verification:
- official portable Release0 errors/warnings,62 regressions, copied APK hash equals common release.
- Store Debug build0 errors (existing offline NU1900), Store137 + multi-device39 tests pass.
- private Android39 tests/lint/signed Release, Windows85 and authenticated transfer17 checks pass.
- actual private settings/UI149 checks and12 rendered transfer screenshots pass.
- Store Release and final integrity checks are recorded in the shared changelog after completion.
- No phone install, commit, tag, push, GitHub release or Store submission.
### Migration validation completed — GitHub workroom, 2026-10-12

- Portable .NET4.6.2 Release:0 warnings/errors,62 regressions. Store Debug/Release:0 errors,
  existing offline NU1900 only;137 Store and39 multi-device regressions.
- Both fresh PC build outputs contain exactly the common official F458D0... APK.
- Canonical103 source hashes match the original Store2.1.5 input; no signing material moved.
- Private latest-base APK:2.1.5-ai-keymap-test/code3, same private certificate, separate package/ports.
- Private Android39, Windows85, transfer17, actual management/UI149 checks and12 screenshots pass.
- Private APK dex contains ShareTransferRequestState, TransferStartRegistry and AI accessibility classes.
- Existing production dist/preview unchanged. Private preview config timestamps predate this task;
  packaging copies only managed payload and keeps config/logs/captures/received files.
- UI remains in the private branch; standalone official UI patch is prepared for acceptance.
- Physical new-APK behavior remains for the user; no install/commit/push/submission/upload performed.
- Full hashes/results: VALIDATION_2026-10-12.json. Handoff: HANDOFF_2026-10-12.md.