# Portable backport audit — 2026-09-30

> Historical scope — current documentation at [the documentation index](README.md). This dated record describes its own build/artifact/state; current2.5.0 behavior and checksums are in the user guides and verification record.
> 과거 시점의 작업 기록입니다. 현재 동작·파일 해시는 문서 목차의2.5.0 안내를 따릅니다.


## Scope

Baseline: local v2.0.1 / 2a4aff2. Worktree: feature/portable-backport-v2.0.1-20260928.
Review covered the tracked diff and new files from this backport, plus the newly
reported app-list bug. Existing product code was read only at affected integration
boundaries. Store code was used as a read-only reference.

The configuration temporary-file errors observed in Codex-launched test copies
were excluded at the user's request. They were not reproduced in the user's
normal Release execution and the settings file I/O code was not changed here.

## Findings and corrections

| Finding | Evidence and resulting behavior |
| --- | --- |
| App catalogues shared across phones | MainForm had one app selector and no per-phone catalogue. DeviceAppCatalog now stores responses by physical identity; selection and mode changes rebuild that phone's projection. A late A response is cached for A without replacing B. |
| Shared recent apps contaminated another phone | Recent apps now live inside DeviceRunSettingsProfile (schema 27). Existing global legacy records remain stored but are not guessed to belong to every phone. Saved per-phone selections remain available as placeholders. |
| Missing app-list completion feedback | A non-focusable child overlay displays the number of loaded apps for five seconds. A late response names its source phone. A newer message replaces the previous one and restarts the timer. |
| Stale wireless ownership snapshot | Reconnect previously treated an old authorized registry entry as live evidence. The current ADB roster and queried physical identity are now required before updating endpoints. |
| mDNS discovery overwrote newer connection policy | Discovered endpoint updates now compare the current mode and endpoint with the request snapshot. A USB selection, edited endpoint or switch to another current connection survives an earlier discovery. Pairing is serialized with reconnect operations. |
| Incomplete diagnostic redaction | Raw/scoped IPv6 addresses and known device names/identity values in warnings and query errors are now masked. Invalid advertised host characters are rejected by the mDNS parser. |
| Diagnostic card lifetime | Repeated refreshes now dispose removed cards and their generated fonts. Report generation stops after an awaited query if the settings form has closed. |
| Companion source and signed binary drift | Two earlier portable-only Android modifications were absent from the reused APK. The planner was aligned with Store; unshipped collision guarantees were removed from the completed checklist. |
| Companion queued-request shutdown | Store confirmed that shutdownNow could discard jobs without their finally cleanup, retain URI grants/queued histories and allow later jobs to reset a shared cancellation flag. Source now tracks requests individually, claims terminal cleanup once, rejects starts after shutdown and cancels queued histories/URI grants even when jobs never execute. Normal queue completion retains grants until the queue drains. |
| Missing imported font license | The Pretendard OFL text is now included beside the Companion sources and in both packaging scripts. Scripts require the license before replacing package output. |
| Real identifiers in new test fixtures | Added mDNS/report tests had used a real phone serial and private network addresses. They now use TESTPHONEA and RFC 5737 documentation addresses. |

## Validation

- .NET Framework 4.6.2 reference assemblies; Windows x64 Debug/Release builds.
- 57 regression cases, including cross-phone catalogue projections and persisted
  recents, stale/reassigned wireless transports, changed connection policy,
  scoped IPv6 masking, existing transfer paths and bounded process drains.
- Android source: 13 unit tests, lintDebug and assembleDebug. The new request
  tracker is exercised independently of Android with active/queued shutdown,
  shared URI lifetime, repeat cleanup and persistent request cancellation cases.
- Android lint completed with no errors and 70 inherited warnings (including
  old SDK-guard branches now redundant at minSdk 33); no new tracker warning.
- Existing signed Companion 2.1.2/versionCode 9/minSdk 33 binary is unchanged:
  SHA-256 D5B09491B2DDEE0358EC0A678994256E814A9434250650E11052A930A21D9854.
- Resource XML/project references and packaging script syntax checked.
- Store working tree remained unchanged.

## Evidence limits and remaining release work

The user reported normal Release DeX startup, file transfer (including the
shared-storage root), capture and independent mini-bars working on Windows 11.
These reports do not establish long-name/cancellation/disconnect coverage,
capture immediately after Scrcpy exit, Windows 7 or real Windows shutdown tests.
The corrected app-list cache/notice still needs a fresh A/B phone check.

The new Android request-lifetime fix exists in source and Debug validation only.
The reused signed 2.1.2 APK still has the queued-timeout issue. Delivering that fix
requires an explicitly approved new signed Companion version and updating the
PC's exact version/hash contract. No new signed APK or release package was made.
Document-provider names that collapse during Android trim/slash sanitization
also remain a separate Companion release limitation.

Actual mDNS port changes, blocked multicast and multiple wireless phone
combinations remain unverified. Direct transfer ADB Kill failure still requires
its dedicated reproduction; no live-process FIFO/cleanup policy was changed.
No commit, push, tag, GitHub upload or Windows UI redesign was performed.

## Approved signed APK follow-up

The user subsequently approved Companion 2.1.3/versionCode 10. The queued-request fix is now in the signed APK, using the unchanged official certificate. SHA-256: 790A0F830BA182A74D27628072ABB709CB77904690E3AFA8797DEAF480392A62. Release lint/build and 13 Android tests pass, as do Windows Release and 57 regression tests. The pinned portable bundle and PC verification values were updated. Earlier source-only statements above describe the initial audit state. Real-device timeout/update installation remains unverified. Store submission files are untouched.
