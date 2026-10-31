# DX Companion

## Store follow-up 2.1.5 (2026-10-07)

The Store bundle uses versionCode 12 with the unchanged official certificate,
package ID, permissions and wire protocol. Sharing keeps one request ID across
Activity recreation and rejects callbacks from destroyed/finishing owners.
Service enqueue deduplicates that ID, not the content; intentionally sharing
the same file again remains supported. Queue cancellation, timeout and URI
grant cleanup are unchanged. Android 28 unit tests and signed Release lint/build
passed; actual Galaxy sharing remains a separate verification step.

Builds may use `scripts/Build-AndroidCleanup.ps1 -SigningPropertiesPath <path>`
to read an existing trusted signing environment without copying its private
files into this checkout. Never commit or package those private files.

Private Android companion utility for DX Manager. It can inspect and clean up:

- Android's simulated-secondary-display setting (`overlay_display_devices`)
- Developer options **Stay awake** (`stay_on_while_plugged_in`)

DX Companion supports Android 13 (API 33) and later. Android 13 and later can
use DX Manager single-window sessions, while New DeX requires the Android 16 /
One UI 8 generation. Android 12 and earlier are outside the product support
range.

The phone UI uses the same violet/cyan glass-inspired visual language and
bundled Pretendard typeface as DX Manager Store Edition. Home, Cleanup,
Transfer, and Settings share one 176 dp hero-card height and automatically
follow the system light or dark theme.

Home shows the active USB or Wi-Fi connection and its elapsed session time.
Transfer keeps two separate histories: the latest 10 PC-to-phone results and
the latest 10 phone-to-PC results. Each list can be cleared independently.

The app provides separate actions for both settings and a combined action. Its
Quick Settings tile and compact 2 × 1 home-screen widget clean both by default;
their targets can be changed inside the app.

DX Companion 2.1.2 can also keep an authenticated loopback session with a
verified DX Manager instance. When Windows announces a real session shutdown,
DX Manager sends the cleanup request over that already-open session instead of
starting a new ADB process. A broken USB/Wi-Fi session does not clean up
immediately: the default grace period is five minutes, and the user can select
immediate cleanup, one, five, ten or thirty minutes, or disable delayed cleanup.

## Safety boundary

- Uses `WRITE_SECURE_SETTINGS` only for the two documented recovery settings.
- Does not provide a shell, execute arbitrary commands, contact cloud services,
  or collect data. Its network use is limited to authenticated local sessions
  created through an ADB reverse tunnel by a verified DX Manager instance.
- The permission is granted only through DX Manager after the exact package and
  official signing certificate are verified.
- Every cleanup operation is verified by reading the affected setting again.

## Local build

Run the repository build helper from PowerShell:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
    -File .\scripts\Build-AndroidCleanup.ps1
```

The helper uses `JAVA_HOME` and `ANDROID_HOME` when set, or the ignored
repository-local `.build-tools\android` toolchain. JDK 17 and Android SDK
platform 36 are required. Never commit `signing.properties`, a keystore, or
passwords. Back up the release keystore separately; it is required for future
updates.

Package ID: `io.github.mazemei.dxdisplaycleanup`

The public signing certificate fingerprint is recorded in [SIGNING.md](SIGNING.md).
