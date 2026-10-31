# DX Manager 2.5.0 Frequently Asked Questions

Windows 2.5.0 documentation, reviewed 2026-10-24. Q1–24 retain their established numbering; Q25–35 cover updates, Companion, consent, input, experimental FPS and package changes.

[English user guide](USER_GUIDE_EN.md) · [Korean FAQ](FAQ_KO.md)

## Q1. A small screen (secondary display) remains on the phone.

Android's virtual-display setting may remain if DX Manager is terminated
forcibly, or if the USB or wireless connection is completely lost before the
cleanup command can be sent. DX Manager removes this setting automatically
when it exits normally.

For a normal exit, keep the phone connected and use **Stop DeX**, press
`Left Alt+F8`, or right-click the DX Manager tray icon and select **Exit**.
Wait for cleanup before disconnecting USB or wireless ADB.

If the screen remains, remove it on the phone as follows:

1. Open **Developer options**.
2. Select **Simulate secondary displays**.
3. Select any resolution once, even if **None** is already selected.
4. Open **Simulate secondary displays** again and select **None**.

Selecting **None** first may not clear a stale display. The resolution-then-None
sequence forces Android to refresh the overlay setting, after which the
remaining virtual screen disappears.

If the bundled official **DX Companion** is installed and verified under
**Settings > Companion**, the leftover display can also be
removed from the main app, Quick Settings tile, or home-screen widget. DX
Companion can also
turn off Developer options **Stay awake** after an interrupted session. See the
[English user guide](USER_GUIDE_EN.md#12-dx-companion-optional) for full
installation and usage details.

<p align="center">
  <img src="images/en/simulate-secondary-display-en.png" width="45%" alt="Simulate secondary displays in Developer options">
  <img src="images/en/simulate-secondary-display-options-en.png" width="45%" alt="Simulate secondary displays selection menu">
</p>
<p align="center"><small>Android settings examples; device and firmware layouts can differ.</small></p>


## Q2. Why use Force-stop selected app when launching Samsung Internet automatically?

On some systems, Korean/English input switching may stop working if Samsung
Internet is already running on the phone and is reopened on the DeX display.
When **Force-stop selected app** is enabled, DX Manager first stops the
existing Samsung Internet process on the phone and then launches it again on
the DeX display, avoiding this issue.

This does not delete browser data or bookmarks, but unsaved text and work in
progress may be lost. Enable it for other apps only when needed.

## Q3. Which device is used when two or more phones are connected?

Choose the phone in the device selector. DeX, all three single-window slots, app profiles, connection
preferences and transfers are stored and run per physical phone. Choosing another phone does not
stop the first one's sessions. USB/Wi-Fi connections belonging to one phone appear as one entry.

The badge shows the observed USB, Wi-Fi or both state. For a new launch, an authorized USB connection
takes priority; without one, an authorized wireless connection can be used. Existing windows retain
the serial they started with and are not moved to another transport. Phone-to-PC files are grouped
under a phone-name subfolder beneath the configured receive directory.

## Q4. Parts of the DeX interface are clipped below 1600×900.

In the tested Samsung DeX layouts, 1600×900 or higher is recommended for the complete desktop. DX Manager can create smaller virtual resolutions, but some DeX elements may not adapt to the smaller screen. For example, the top of the app drawer may be clipped.

Apps themselves may still work, but 1600×900 or higher is recommended for the
complete DeX interface. This is a Samsung DeX limitation at low resolutions;
DX Manager is not cropping the image.

## Q5. The desktop icons and wallpaper changed after adjusting resolution or DPI.

Samsung DeX may choose different scaling and layout profiles for different
resolution and DPI combinations. Even at the same resolution, changing DPI
can alter icon size, widget placement, and whether desktop items can be
edited. The wallpaper may also be stored separately for each profile.

Therefore, a different desktop after changing settings does not necessarily
mean that DX Manager selected the wrong virtual display. For the most stable
layout, choose the resolution and DPI combination you normally use and arrange
the desktop under that combination.

## Q6. The Recents screen looks wrong and I cannot return to Desktop 1.

With some resolution and DPI combinations, the desktop selector in Recents
may be misaligned after DeX starts for the first time. To recover:

1. On the Recents screen, select the **+** button inside the monitor image to
   create a new desktop.
2. Select **Desktop 1**, which may be only partly visible at the left edge.

Once the normal screen returns, the problem generally does not recur during
the same DeX session. The layout and names may differ by Samsung firmware and
One UI version.

## Q7. Why can DPI not be set below 120?

On the tested Samsung DeX environment, 120 is the lowest DPI at which a
virtual display can be created. At 119 or below, the overlay is not created
and no new display ID can be found.

DX Manager restores the value that was present before editing and displays a
notice when a value below 120 is entered. Very high DPI values may create a
display successfully, but text and interface elements can become too large
for practical use.

## Q8. The scrcpy-server transfer speed in the log is lower than usual.

A line such as the following reports the momentary rate calculated while
scrcpy sends the small `scrcpy-server` file to the phone:

```text
scrcpy-server: 1 file pushed, 0 skipped. 42.9 MB/s
```

Because the file is small and the transfer completes very quickly, the value
can vary significantly. It is not an exact measurement of the USB link or the
subsequent video-streaming speed. A low number by itself does not indicate a
connection failure.

If the DeX display also stutters, or direct scrcpy runs consistently show low
values, try another data-capable USB cable and another USB port on the PC. A
charging-oriented or poor-quality cable may be the cause.

## Q9. Wireless ADB connection or automatic reconnection fails.

Check the following in order:

- Confirm that the PC and phone are on a local network where they can
  communicate directly.
- Check guest Wi-Fi, AP/client isolation, VLAN rules, and corporate firewalls.
- Make sure the pairing port is not being confused with the connection port.
- Check whether the phone's IP address has changed.
- If automatic mDNS discovery is blocked, enter the current connection port
  shown on the Wireless debugging screen; do not reuse the pairing port.
- After restarting the phone, prepare wireless ADB over USB again.
- Pair Android 11+ wireless debugging again when necessary.

If the wireless address appears in `adb devices` as `offline`, disconnect and
reconnect it. Using the same Wi-Fi name does not by itself guarantee that
devices can communicate with each other.

## Q10. The device status is unauthorized or offline.

`unauthorized` means that the phone has not approved the PC's ADB key. Turn on
the phone screen and approve the RSA debugging prompt. If the prompt does not
appear, revoke USB debugging authorizations and reconnect the cable, or turn
USB debugging off and on again.

`offline` means that the device is listed but cannot accept ADB commands.
Check the cable, USB port, or wireless network and reconnect. If a USB device
continues to be missing, also check the Samsung USB driver and the phone's
**Auto Blocker** setting.

## Q11. Why is right Shift corrected to left Shift?

The physical right Shift delivery problem was reproduced with scrcpy 4.0/SDL3
for Windows: Windows detected the key, but Android did not receive it. The same
behavior was not observed with scrcpy 3.3.4/SDL2.

For compatibility with SDL3-based scrcpy clients, including the bundled 5.0.1, DX Manager converts
right Shift to left Shift only while such a scrcpy window is active. Normal
Shift input remains available, but Android apps cannot distinguish the two
Shift sides during that session. The correction is not applied to other
Windows applications or SDL2-based scrcpy versions.

## Q12. What is the difference between device start delay and process timeout?

**Device start delay** is an intentional pause after DX Manager confirms the
phone's connection state and device name, but before it sends the actual DeX
or single-window start command. Its range is 0–60 seconds and the default is
1 second. A value of 0 starts immediately after connection confirmation.

**Process timeout** is the maximum time DX Manager allows an ADB or scrcpy
helper process to respond or create its window. A command that finishes
normally does not wait for the full timeout. The advanced default normally
does not need to be changed unless timeouts repeatedly occur on a slow PC or
device. A managed drag-and-drop file transfer is not limited to this duration;
large ADB pushes continue until they finish, fail, are canceled, or the session
ends.

Start delay is exposed under **Settings > General > Start DeX when a phone connects**. Other timeout values are stored configuration and are not all exposed as controls on the current Diagnostics page.

## Q13. What happens if USB is unexpectedly disconnected during use?

Windows using that USB transport stop and DX Manager attempts the applicable cleanup. If the phone
is already unreachable, the PC cannot confirm that its virtual display or power settings were restored.
A remaining Wi-Fi connection does not automatically inherit or restart those windows.

Reconnect the phone. The saved automatic DeX-start setting can apply on an actual reconnection;
otherwise start manually. Installed, verified Companion can provide the configured fallback recovery.
See Q1 for manual cleanup and Q26 for the 2.1.6 session-protection notification behavior.

## Q14. What is the difference between Turn phone screen off and Stay awake?

**Turn phone screen off (`-S`)** turns off the phone's physical panel while a
scrcpy session is running. The DeX virtual display and scrcpy video continue
to operate while the phone screen is off.

**Stay awake** keeps Android active so that it does not sleep or interrupt the
connection during a session. If any DeX or single-window session requests
screen-off or stay-awake behavior, DX Manager manages it using the combined
state of all sessions. When all related sessions stop, or DX Manager exits
normally, it attempts to restore the phone screen and stay-awake state.

At experimental120 FPS, screen-off is disabled automatically. Returning to 30/60 restores its prior value; see [Q30](#q30-why-is-turn-phone-screen-off-disabled-at-120-fps).

## Q15. The phone screen stays on while it is charging.

DX Manager restores the original **Stay awake (`-w`)** setting when
the DeX or single-window session, or the program itself, is closed normally.

If DX Manager exits abnormally, or exits while the phone is disconnected, it
may be unable to send the ADB command that restores this setting. Use either
of the following methods to recover:

- On the phone, open **Developer options** and turn off **Stay awake**.
- Reconnect the same phone to DX Manager, and then close the session or
  program normally.

This setting does not force the screen to remain on while the phone is running
only on battery power, but it may prevent the screen from turning off while
the phone is connected to USB or a charger.

## Q16. What is required on Windows 7, and why can DXManager.exe not be copied by itself?

DX Manager supports 64-bit Windows 7 SP1 with .NET Framework 4.6.2 or later.
32-bit Windows is not supported. On Windows 7/8.1, the Universal CRT update
required by the bundled legacy ADB and the Samsung USB driver may also be
needed.

.NET Framework 4.6.2 is not included with Windows 7 SP1 by default. It is the
intentional minimum target used to preserve compatibility with Windows 7 SP1
and offline or closed-network PCs. If it is missing, obtain the runtime from
the [official Microsoft download page](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net462).
The page also provides an offline installer that can be downloaded on another
computer and transferred to the target PC.

.NET Framework 4.x releases are in-place updates. If 4.7.2 or 4.8 is already
installed, do not downgrade it or attempt to install 4.6.2 beside it. The same
DX Manager build runs using the newer installed 4.x runtime.

`DXManager.exe` depends on the following adjacent files and will not work
correctly when copied by itself:

- ADB and scrcpy executables under `tools`
- DX Manager's ADB file-transfer helper under `tools/adb-proxy`
- scrcpy DLLs and `scrcpy-server`
- Korean resources and configuration files
- License and third-party notice files

Keep the folder structure from the release ZIP and run the program from a
writable location where it can save settings, logs, and screenshots.

## Q17. How does drag-and-drop file transfer work, and can I disable it?


Drop files or folders onto a running DeX/app scrcpy window. The default managed path preserves Unicode names,
folder structure and empty folders, and reserves numbered names instead of overwriting destination items.
The independent status window shows active/waiting/completed/failed items without a synthetic percentage/ETA.

Change the destination under **Settings > Paths > PC → phone file transfer** and reopen scrcpy windows.
The current Settings layout has no separate managed-mode toggle. For advanced configuration, fully exit the app,
back up `config/settings.json` and set `Features.ManagedFileTransferEnabled` to `false`.
Restart the app/open new scrcpy windows. scrcpy then handles file drops normally; standalone APK install behavior remains.

## Q18. The DeX window is black on an older One UI version.

The currently verified baseline is a DeX-capable Galaxy device running Android
16 with One UI 8.x. One UI 7.x and earlier have not been confirmed to work
reliably with the virtual-display method used by DX Manager and may open a
black DeX window. This can depend on the device model, Samsung firmware, and
whether the phone uses the older classic DeX implementation.

Single-app mode may still work because it uses scrcpy's own virtual display,
but input support can differ by firmware. If HID keyboard or HID mouse causes
instability, test again with those options disabled. This does not establish
support for that device or One UI release.

## Q19. Why did ADB always show version 1.0.41, and what version is actually in use?

`Android Debug Bridge version 1.0.41` is a common ADB protocol banner and is
printed by many different platform-tools releases. It does not identify the
actual executable build.

Since 1.1.0, DX Manager reads the following `Version ...` line instead. The bundled
scrcpy ADB reports `37.0.1-15733141`, while the Windows 7/8.1 legacy ADB reports
`34.0.1-9979309`. A manually selected ADB displays its own corresponding
`Version ...` value.

## Q20. A banking or game app refuses to run, or its screen is black.

Some banking, game, streaming, and security-sensitive apps detect USB
debugging or Developer options and refuse to run. An app may also block
mirroring of protected or DRM-controlled content, or disallow virtual and
secondary displays.

This behavior is enforced by the app or Android security policy. DX Manager
does not bypass these restrictions.

## Q21. An app launched from DeX opens on the phone instead.

Some apps do not fully support multiple displays or reuse an existing instance
that is already running on the phone. Close the app completely on the phone and
launch it again from DeX, or try DX Manager's **Force-stop selected app** option.

If it still opens on the phone, the app may not support launching on a
secondary display and DX Manager may not be able to override that behavior.

## Q22. The mouse cannot leave the DeX window.

When **HID mouse (`-M`)** is enabled, scrcpy sends the mouse as if it were
physically connected to Android, so the pointer is captured by the scrcpy
window. Press left `Alt` to release or recapture it temporarily.

To move the pointer freely between Windows and DeX, disable HID mouse and
reopen the DeX or single-app window. In this mode the mouse behaves more like
touch input than a regular hardware mouse. For example, a side button may open
Recent Apps and holding the left button may act as a touchscreen long press.

## Q23. Why is the cleanup permission button under Companion disabled?

Open **Settings > Companion**, select the correct phone and refresh its status. Installation and
cleanup permission are managed here, rather than under Diagnostics.

The phone must be authorized over USB or wireless ADB. DX Manager checks the bundle and installed
app's package, supported version, APK hash and official signing certificate. An absent, altered,
unsupported or differently signed app cannot receive the cleanup permission. Follow the displayed
status and install/update the bundled **Companion 2.1.6** when needed.

The cleanup permission is `WRITE_SECURE_SETTINGS`, limited by Companion's implementation to the
virtual-display and Stay awake controls. It is normally retained through a same-signature update or
phone reboot, and lost on uninstall. This is different from the file-transfer ready state: a failed
cleanup-permission grant is not itself evidence that the transfer tunnel cannot work.

## Q24. DX Companion cannot send a file or folder to the PC.

Start DX Manager, connect the intended phone and update the official Companion to **2.1.6** under
**Settings > Companion**. Under **Settings > Paths > Phone → PC file transfer**, enable receiving
and select **PC receive folder**. Check Companion's file-transfer page for the ready state.

Use Gallery/My Files > Share > **Send to DX Manager** for files, and Companion's folder picker for
folders. If several similarly named share entries exist, choose the official Companion connected to
this PC. Old or separate test installations may be a different app.

A dropped connection, unavailable PC, disabled receiver or unverified/old Companion prevents sending.
Reconnect and start a failed transfer again. Existing names are preserved with numbered siblings;
subfolders and empty folders are retained. The desktop window shows progress items/counts without
an invented percentage or remaining time. The receiver creates a phone-name subfolder below the PC
receive directory. Check that directory before concluding a file is missing.

## Q25. How do update checks behave without Internet?

The automatic check runs once on the first launch of each local day, in the background with an
overall 5-second deadline. Failure is saved as that day's attempt; restarting offline does not retry
in a loop. It does not block local USB/LAN use or show an automatic latest-version/offline dialog.

Use **Settings > About > Check for updates**, beside third-party notices, for a manual result dialog.
A new version found automatically uses a lower-right nonmodal banner or a tray notification. X dismisses
that release; the GitHub button opens its page and leaves the banner visible. A previously cached
new-version banner can still appear offline. Download and installation are manual.

## Q26. Why does the Companion session-protection notification remain?

It monitors the authenticated PC connection, so it can appear when DX Manager detects the phone,
before DeX is started. Keeping this notification while the PC is connected is expected.

Update Companion to **2.1.6**. Once a PC disconnect is detected and both the virtual display and Stay
awake settings are verified inactive, it stops protection and removes the notification without waiting
for the recovery timer. A fresh tunnel gets a brief setup grace. If recovery settings remain active
or cannot be verified, the existing disconnect recovery policy remains. Losing a PC socket while the
configured USB/Wi-Fi transport still appears present is not the same as confirmed transport removal.

## Q27. Does the first-run consent appear every time?

No. A fresh distribution shows it until the acknowledgement checkbox is checked and confirmation is
saved. Declining exits without accepting; normal Enter cannot accept an unchecked notice.
Later launches respect `config/onboarding.json`, which is separate from `config/settings.json` and
is preserved by settings reset. A new extraction without that record, corrupted state or a folder
that cannot save the record can cause the notice to appear again. Use a writable folder.

The acknowledgement belongs to the portable config folder. Copy its history when migrating to a new version folder; a clean folder has fresh state.

## Q28. Is hardware decoding actually active?

The bundled scrcpy **5.0.1** receives `--hwdec=auto` for DeX and all three single-window slots.
Automatic selection depends on the OS, GPU and driver; it does not promise hardware decoding on
every PC. Check scrcpy's session output, for example **Video decoding: d3d11va**, rather than CPU
usage alone. Windows 7 may choose differently from a newer Windows installation.

**Settings > Diagnostics > Environment check** shows the selected scrcpy executable's full version.
An explicit `--hwdec` selection in advanced arguments is respected instead of adding a duplicate.

## Q29. How do Enter and Shift+Enter switching work?

The public 2.5.0 release keeps optional **Scroll Lock** switching under **Settings > Keyboard**.
With it disabled, ordinary Enter is unchanged. With it enabled, the saved mapping and Scroll Lock
state apply while an eligible scrcpy window is active. They do not create automatic per-app AI rules.
If Enter seems reversed, check that option and the Scroll Lock state first.


## Q30. Why is Turn phone screen off disabled at 120 FPS?

120 FPS is an **experimental maximum**, with 60 FPS still the default. The phone screen must stay on.
Selecting 120 does not show a popup; **each actual 120 FPS start** requires a warning confirmation, including
DeX, every single-window slot, saved settings, tray/automatic starts and apply-settings restarts.
Cancel is the default action and cancels the launch.

Heat and battery use increase. Static images on an OLED panel can cause permanent burn-in; avoid prolonged
or unattended use and stop if the phone overheats. The option does not inject input or force Android refresh settings.

Switching back to 30/60 restores the exact earlier screen-off preference, including a preference to keep it on.
This is saved per phone, mode and app profile. If any 120 FPS session is running on a phone, other sessions on
that phone defer their screen-off requests until the last 120 session ends. Other phones remain independent.

## Q31. Why does the actual FPS remain below 120?

The setting is a cap, not a promise of constant output. Device, app, activity, rendering/encoding/decoding
capacity and transport conditions can limit the result. Low activity can reduce frame delivery.
The phone panel must remain on; turning it off manually can also reduce FPS.
Use 30/60 FPS when a higher cap is not useful or the phone becomes hot.

The configured limit cannot be overridden with advanced `--max-fps` arguments, so the warning and screen rules remain consistent.

## Q32. What is the What's new popup after an update?

This local release-note dialog is separate from the daily online update check and mandatory safety/120 FPS consent.
It uses no Internet and keeps its own `config/update-notice.json` checkpoint.

An installation with earlier accepted safety-notice history shows an unseen newer version once, after safety consent
and before device initialization/automatic starts. A fresh folder or installation with no earlier consent history
shows the safety notice and establishes a baseline instead. Confirming the notes saves the current version.
Closing/canceling or failing to save can make the notes reappear; settings reset does not clear their history,
and running an older build does not lower the checkpoint.

## Q33. Why does the ZIP no longer include a version in its filename?

From Windows 2.5.0, every Windows release uses **`DX-Manager-win-x64.zip`** and a versioned inner folder,
such as **`DX Manager v2.5.0`**. Extract the complete folder, not the EXE alone.

Use the [latest release](https://github.com/maze-mei/DX-Manager/releases/latest) or
[fixed latest Windows download](https://github.com/maze-mei/DX-Manager/releases/latest/download/DX-Manager-win-x64.zip).
The latter works when the latest release includes that exact asset name. A URL containing a specific old tag/file
does not automatically switch to a different version. Third-party servers may also keep older copies.

## Q34. How do I retain settings when updating, and why does 2.0.1 not notify me?

2.0.1 and earlier do not contain the new update-check feature. Download the newer version from the official release page.

Fully exit the old app and wait for phone cleanup, then extract the new ZIP. With both copies closed,
copy your personal **config** folder into the new version folder if you want to keep settings and consent/history.
Preserve wanted captures, logs and received files separately. Keep those personal files out of public uploads.
A clean new folder without consent history shows first-run safety guidance again.

See [Updating an existing installation](USER_GUIDE_EN.md#updating-an-existing-installation).
Future versions can notify you from the app, but downloading/installing is still manual.

## Q35. Are the macOS implementation and this Windows ZIP the same release?

No. The public repository has a separate .NET 8 macOS TUI/CLI implementation, with its own runtime, packaging
and validation. The Windows 2.5.0 ZIP does not run on macOS; its scrcpy 5.0.1/702-check results are Windows-specific.

At the 2026-10-24 review, published release assets were Windows ZIPs, not Mac ZIPs.
Follow the [macOS guide](https://github.com/maze-mei/DX-Manager/blob/main/docs/MACOS_GUIDE.md) for source and packaging status.
