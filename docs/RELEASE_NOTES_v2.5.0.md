# DX Manager v2.5.0

## English

A major Windows portable update with a refreshed interface, scrcpy 5.0.1, daily update notifications,
experimental 120 FPS and an updated official DX Companion.

[Download Windows x64 ZIP](https://github.com/maze-mei/DX-Manager/releases/latest/download/DX-Manager-win-x64.zip) ·
[User guide](USER_GUIDE_EN.md) · [FAQ](FAQ_EN.md)

### What's new

- **Refreshed light and dark interface:** consistent Fluent icons, redesigned file-transfer windows,
  clearer help cards and improved English text/layout.
- **scrcpy 5.0.1:** automatic hardware-decoder selection with `--hwdec=auto` for DeX and all three app slots.
  An explicit advanced-argument override is respected. Diagnostics now show the full scrcpy version.
- **Update notifications:** one background check per local day, with a five-second deadline and no offline retry loop.
  New versions appear in a lower-right nonmodal banner or a Windows tray notice while hidden.
  Automatic checks stay silent when current or offline. Manual checking is under Settings > About, beside third-party notices.
  Downloads and installation remain manual.
- **Local What's new notes:** existing installations see version-scoped English/Korean changes after upgrading.
  These notes work offline and are separate from first-run safety consent.
- **Experimental maximum 120 FPS:** 60 FPS remains the default. A themed warning appears before every actual 120 FPS start,
  with Cancel selected by default. The phone screen stays on; switching back to 30/60 restores the exact earlier screen-off preference.
  A live 120 FPS session takes screen-on priority over other sessions on the same phone.

**120 FPS is experimental.** Actual FPS depends on the phone, firmware, app, activity and connection.
Keeping the phone screen on increases heat, battery use and OLED burn-in risk.
Avoid prolonged or unattended use, and stop if the device overheats.

### Fixes and reliability

- Running the EXE again restores the existing main window from the tray.
- Moving the main window to the tray closes its attached manager dialogs.
  Idle auto-hide also minimizes scrcpy windows.
- Missing-start-app notices stay with the relevant phone and single-window slot.
- Improved Companion status with six-digit wireless-debugging pairing and reconnect responsiveness.
- Retained authorized USB priority for new launches and the original transport for running sessions.
- Corrected the expanded environment-check button overlap, Korean text in English diagnostics/FAQ,
  and the clipped English Help label.
- Preserved independent sessions/settings/transfers for multiple phones, HID input, Scroll Lock Enter switching,
  Unicode paths, empty folders and collision-safe file naming.

### Official DX Companion 2.1.6

The bundled official APK retains duplicate-share protection and stops session protection promptly after the PC link
is lost when virtual displays and Stay awake are verified clean.
If recovery work remains or cannot be verified, the configured disconnect recovery policy still applies.

**Existing users: update the Android app through Settings > Companion.**
The APK is included but is never installed automatically.
Android 13 or later is required. The official package/signing identity and protocol remain unchanged.
The private AI foreground-app/keymap experiment is not included.

### Download and upgrade

1. Download `DX-Manager-win-x64.zip` and extract the complete `DX Manager v2.5.0` folder.
2. Exit the old copy completely. Follow [the upgrade guide](USER_GUIDE_EN.md#updating-an-existing-installation) to preserve your personal configuration.
3. Run `DXManager.exe` and complete any required first-run notice.
4. Update the optional Companion app if you use recovery or phone-to-PC sharing.

Do not copy the EXE by itself. Windows 7 SP1/8.1/10/11 x64 and .NET Framework 4.6.2 or later are supported targets.
Older systems may need the documented framework/ADB prerequisites. No Internet is required for ordinary local USB/LAN use.

The external Windows ZIP filename is fixed from this release onward.
For websites and bookmarks, use the latest-Windows-ZIP link above.
A hardcoded old tag/filename or third-party mirror will not automatically follow future releases.

This is the Windows binary release. The repository's separate macOS implementation is retained; no Mac binary is included here.
The matching official Companion Android source is included in the source checkout.

---

## 한국어

UI 개편, scrcpy 5.0.1, 하루 한 번 업데이트 알림, 실험적 120 FPS와 정식 Companion 업데이트를 담은
Windows 포터블 버전입니다.

[Windows x64 ZIP 다운로드](https://github.com/maze-mei/DX-Manager/releases/latest/download/DX-Manager-win-x64.zip) ·
[사용 설명서](USER_GUIDE_KO.md) · [자주 묻는 질문](FAQ_KO.md)

### 주요 변경사항

- **밝음·다크 UI 개편:** Fluent 아이콘을 통일하고 양방향 파일 전송창, 도움말 카드와 영어 문구·배치를 정리했습니다.
- **scrcpy 5.0.1:** DeX와 단일창 세 슬롯에 `--hwdec=auto`를 기본 적용합니다.
  고급 인수에서 직접 지정한 값은 유지하며 환경 검사에서 scrcpy의 전체 버전을 확인할 수 있습니다.
- **업데이트 알림:** 하루 한 번 백그라운드에서 확인하며 제한 시간은 5초입니다.
  새 버전은 오른쪽 아래 배너 또는 트레이 상태의 Windows 알림으로 안내합니다.
  최신 버전이거나 오프라인이면 자동 확인 안내창을 띄우지 않고 재시도를 반복하지 않습니다.
  직접 확인은 설정 > 정보의 제3자 라이선스 보기 옆에 있습니다. 다운로드·설치는 직접 진행합니다.
- **로컬 변경사항 안내:** 기존 설치를 새 버전으로 바꾸면 해당 버전의 한·영 변경사항을 안내합니다.
  인터넷이 필요 없으며 첫 실행 안전 동의와는 별개입니다.
- **실험적 최대 120 FPS:** 기본값은 60 FPS입니다. 실제 120 FPS 실행마다 경고를 표시하고 기본 선택은 취소입니다.
  휴대폰 화면을 켜두며 30/60으로 돌아오면 이전 화면 끄기 설정을 정확히 복원합니다.
  같은 휴대폰에 120 FPS 세션이 하나라도 실행 중이면 화면 켜짐을 우선합니다.

**120 FPS는 실험적 기능입니다.** 실제 FPS는 휴대폰·펌웨어·앱·화면 변화와 연결 환경에 따라 달라집니다.
휴대폰 화면을 계속 켜두면 발열·배터리 사용과 OLED 번인 위험이 커집니다.
장시간·무인 사용을 피하고 과열되면 사용을 중단하세요.

### 수정·안정화

- EXE를 다시 실행하면 트레이의 기존 메인창을 복원합니다.
- 메인창이 트레이로 들어갈 때 부속 관리 창을 닫습니다. 자동 숨김은 scrcpy 창도 최소화합니다.
- 시작 앱 미선택 안내는 해당 휴대폰·단일창 슬롯에만 유지합니다.
- 6자리 코드 무선 디버깅의 Companion 상태 표시와 재연결 응답성을 개선했습니다.
- 새 실행의 승인된 USB 우선과 실행 중 세션의 연결 방식 유지를 보존했습니다.
- 펼친 환경 검사 버튼 겹침, 영어 진단·FAQ의 한글 문구와 영어 도움말 버튼 잘림을 수정했습니다.
- 복수 기기의 독립 세션·설정·전송, HID 입력, Scroll Lock Enter 전환,
  Unicode 경로·빈 폴더·동명 파일 번호 처리를 유지합니다.

### 정식 DX Companion 2.1.6

중복 공유 방지를 유지하면서 PC 연결이 끊기고 가상 화면·잠자기 방지가 정리됐음을 확인하면 감시를 바로 중단합니다.
복구할 항목이 남았거나 상태를 확인하지 못하면 설정된 연결 해제 복구 정책을 유지합니다.

**기존 사용자는 설정 > Companion에서 Android 앱도 업데이트하세요.**
APK는 동봉되며 자동으로 설치하지 않습니다. Android 13 이상이 필요합니다.
정식 패키지·서명·통신 규약은 유지하며 개인 테스트용 AI 앱 자동 감지·키맵은 포함하지 않습니다.

### 다운로드·업데이트

1. `DX-Manager-win-x64.zip`을 받고 `DX Manager v2.5.0` 폴더 전체를 풉니다.
2. 기존 프로그램을 완전히 종료하고 [개인 설정 유지 안내](USER_GUIDE_KO.md#기존-설치에서-업데이트)를 확인합니다.
3. `DXManager.exe`를 실행하고 필요한 첫 실행 안내에 동의합니다.
4. 복구·휴대폰→PC 공유를 사용한다면 Companion도 업데이트합니다.

EXE만 교체하지 마세요. Windows 7 SP1·8.1·10·11 x64와 .NET Framework 4.6.2 이상을 대상으로 합니다.
구형 Windows는 안내된 프레임워크·ADB 준비가 필요할 수 있습니다. 일반 USB·로컬 LAN 사용에는 인터넷이 필요 없습니다.

이번 버전부터 외부 ZIP 이름을 고정합니다. 외부 사이트·즐겨찾기는 위 최신 Windows ZIP 주소를 사용하세요.
이전 태그·파일명을 고정한 주소나 외부 서버의 복사본은 자동 갱신되지 않습니다.

이번 실행 파일 배포는 Windows용입니다. 별도의 macOS 소스는 보존하며 Mac 실행 파일은 포함하지 않습니다.
해당 버전의 정식 Companion Android 소스도 공개 저장소에 함께 포함합니다.

## SHA-256 and verification / 해시·검증

| File | SHA-256 |
| --- | --- |
| `DX-Manager-win-x64.zip` | `EBB91A332B603C4096B1B1FB6D179F4D45B55F5A63FC657F8B989BEA8DE08764` |
| `DXManager.exe` | `D3342F2A2B80DC5945D069CF408412A4DEDEBB6C0A54C78795485CB5DAD5FC19` |
| `DXMAdbProxy.exe` | `90C9DC4838F61640D2F05B31339C5A9E7BDAF346A3FFF3332A2532A2BDEC2C93` |
| `DX-Companion.apk` | `904491111341D46458BB9CE9B342D9210A20BB923BC0F605484D6AF7B90DFCC9` |

The Windows executable passed 702 automated checks before packaging.
The maintainer reports a clean antivirus scan of this exact final EXE; no engine/count is claimed without a supplied report.
The inspected EXE, APK and ZIP bytes are preserved. See [verification details](VERIFICATION_v2.5.0.md) for the scope and limits.

Windows 실행 파일은 포장 전 702개 자동 확인을 통과했습니다.
메이님이 현재 최종 EXE의 백신 검사에서 깨끗하다고 확인했습니다. 제공되지 않은 검사 엔진·탐지 수는 기재하지 않습니다.
검사한 EXE·APK·ZIP은 그대로 유지하며 실제 검증 범위는 위 검증 기록을 따릅니다.
