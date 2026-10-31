# scrcpy 5.0 Windows 7 test package — 2026-10-06

> Historical scope — current documentation at [the documentation index](README.md). This dated record describes its own build/artifact/state; current2.5.0 behavior and checksums are in the user guides and verification record.
> 과거 시점의 작업 기록입니다. 현재 동작·파일 해시는 문서 목차의2.5.0 안내를 따릅니다.


## Scope

The user requested a test build to try at the company, then confirmed the company PC is Windows7. The frozen2.5.0 package and commit9418e2c remain intact.
This test reuses the exact reviewed DXManager2.5.0 executable and Companion2.1.4, replaces only the scrcpy runtime with the complete official5.0 win64 set, and supplies generated nonpersonal decoder presets. No new main-app behavior or UI option was added.

## Delivery

- ZIP: E:\vs\dex system\dist\DX-Manager-v2.5.0-scrcpy5-test-win-x64.zip
- Folder: E:\vs\dex system\dist\DX Manager v2.5.0 scrcpy5 test
- 01-software/DXManager.exe: --hwdec=disabled in DeX and single-window1~3 defaults. Test this first.
- 02-auto/DXManager.exe: --hwdec=auto in the same four defaults. Hardware if available, otherwise software.
- Complete-exit one before starting the other. Each uses its own config and logs; use the supplied presets initially instead of copying old config.
- Read 먼저 읽어주세요.txt in the ZIP. Extract the whole archive; do not copy the executable alone.

## Provenance and validation

- Official scrcpy5.0 ZIP SHA256:44C10D9E82F20EA67227D14D37BF9FBE3603117C5736DF3F514544A02BA20A73.
- DXManager.exe remains BACEA6027137EC0ACE770133034F258EF36F68CA977FD03FF9395F58717D3632.
- Companion remains95DAF4E734FD36FE07AD8388FE8E4C98D96D1EFA0A8E359D8C8E3E27383B6536.
- Legacy ADB remains30C68C1C1A9814A724F47CA544F273B8097263677383046DDB7A0E8C26F7DC60. The app's Windows7 selection path chooses this before probing the modern ADB; scrcpy gets the chosen path through the existing ADB/proxy environment.
- All copied scrcpy runtime files match the hash-verified official extracted files. No4.1 FFmpeg DLLs are mixed into the new runtime.
- On the current Windows11 development machine, scrcpy --version/--help and both --hwdec modes with --version completed successfully without ADB/device commands. SDL3.4.18, FFmpeg major63/63/61 and libusb1.0.30 were reported. FFmpeg's exported license reports LGPL2.1 or later.
- The original app settings model generated presets and verified inheritance into a new physical device's DeX and single-window profiles. The shipped presets have no device identity, wireless profile, app history or developer user-profile path; receive folder is relative.
-135 ZIP payload files; exactly two generated public config/settings.json seeds. No personal onboarding, logs, captures, debug symbols or signing keys are included.
- Test ZIP SHA256:9306F95A628CDD3F5D44A07176722FDC44C3B2AB5D182C5D2E90B559A1F5972E.
- Original final ZIP remains B2064404BB45B65FA863558C82898C5B1BFFAC46CB7035BA5E55CEA2892765E0.

## Company checks

1. Windows7 SP1 x64 and existing .NET4.6.2/UCRT requirements; launch software package first.
2. Note startup DLL/entry-point errors verbatim if any. This test does not guarantee Win7 compatibility from static analysis or the Windows11 smoke check.
3. Confirm the selected ADB log is tools/adb/legacy/adb.exe. Run DXManager.exe, not the modern bundled adb or direct scrcpy scripts.
4. Compare matching resolution/FPS/bitrate/app in both modes, DeX plus selected-app single windows, resize, CPU usage, Shift/AltGr/Hangul/Enter, F8, mini-bars, disconnect/reconnect and cleanup.
5. Auto can still be software on Win7. Texture (D3D11VA) indicates the hardware texture path; a software-fallback message is informational unless playback fails.
6. Send the log from the affected variant's logs folder or session-log window. No physical phone streaming or company-PC validation has been performed here.

## Reproduction

scripts/Package-Scrcpy5-Test.ps1 and Generate-Scrcpy5-TestPreset.cs prepare the package from the verified final base and the separately downloaded official runtime. Existing test outputs are preserved by refusing an overwrite; choose a fresh output root for a repeat.
No new Git commit, push, tag or upload was requested/performed for this experiment. Store code and the main2.5.0 executable are unchanged.