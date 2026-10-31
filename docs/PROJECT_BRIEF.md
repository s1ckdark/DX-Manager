# DX Manager project brief

## English — current Windows baseline, 2026-10-24

| Area | Contract |
| --- | --- |
| Product | Windows 2.5.0, WinForms, .NET Framework 4.6.2, x64 |
| Intended OS range | Windows 7 SP1/8.1/10/11; legacy ADB on older systems |
| Runtime | Official scrcpy 5.0.1,16 pinned files; default `--hwdec=auto` |
| Android helper | Shared official Companion 2.1.6/code13/protocol1, Android 13+ |
| Delivery | `DX-Manager-win-x64.zip` → `DX Manager v2.5.0/`;39 files |
| Product code checkpoint | `ccc10a9`; scanned runtime bytes preserved during source/docs publication |
| Source integration | Public main's Mac implementation preserved; official Companion snapshot included |
| Current binary checks |702 Windows checks; exact prepared hashes in [verification](VERIFICATION_v2.5.0.md) |
| Separate products | Store WinUI and macOS .NET8 are separate implementations; no private AI tracker in this release |

### Behavior to preserve

- Stable physical identity joins that phone's USB/Wi-Fi transports. Each phone owns its runtime/settings/transfers.
- New starts prefer authorized USB; live sessions retain captured serials. USB loss does not silently migrate a window to Wi-Fi.
- DeX and three app slots can coexist; start app is optional for DeX and required for each slot.
- Per-phone/mode/app profile settings preserve nullable prior screen-off state at experimental120 FPS.
- Warn once before every actual120 start, default Cancel, before launch I/O. Keep the phone panel on; no injected activity/forced refresh-rate change.
- A phone with any live120 session defers ordinary screen-off requests; the last120 exit allows ordinary requests to resume.
- Keep process-start shutdown gates short and preserve Unicode/empty-folder/collision-safe transfers.
- Input/capture/mini bars target the explicit focused/owned session. Right Shift compatibility correction remains.
- Main-to-tray closes attached manager dialogs; scrcpy sessions continue. Idle auto-hide also minimizes scrcpy.
- Daily update attempts are persisted before background networking; offline does not loop. Manual checks remain available.
- Separate local changes history and safety consent survive ordinary settings reset; fresh packages contain neither.
- Companion protection may run while the manager is idle/connected. Verified disconnected-and-clean state stops it; remaining recovery work retains the transport/delay policy.
- Normal exit cleans device state. Windows shutdown starts no new ADB helper and uses an established authenticated Companion channel best-effort.

### Release preparation boundaries

Current user captures are approved for public docs: no single-window picture; diagnostics before the check;
default-size connection page; connected Companion home images. See [image inventory](SCREENSHOT_PLAN_v2.5.0.md).

Preserve exact final bytes during GitHub documentation work. No publication follows a clean antivirus report.
The source includes the verified official Companion snapshot and preserves remote macOS additions in separate build projects.
See [Building](BUILDING.md), [known constraints](KNOWN_ISSUES.md) and [current TODO](TODO.md).

## 한국어

현재 기준은 Windows 2.5.0 / scrcpy 5.0.1 / 공용 정식 Companion 2.1.6입니다.
휴대폰별 세션 분리·USB 새 실행 우선·실행 중 serial 유지·단일창 시작 앱 필수,
120 실행 경고·화면 켜짐 우선·이전 설정 복원, Unicode 전송·입력·트레이·정리 규칙을 유지합니다.
702개 검증은 Windows 실제 어셈블리/검증 UI 범위이며 모든 기기·OS 실기 검증으로 확대하지 않습니다.
공용 원본의 해당 버전 스냅샷을 소스에 포함하고 macOS 구현은 별도 빌드 프로젝트로 보존합니다. 이번 게시는 메이님이 명시적으로 승인했습니다.
