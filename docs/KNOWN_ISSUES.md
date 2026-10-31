# Known issues and limits

## Current scope — 2026-10-24

Windows2.5.0 / scrcpy5.0.1 / official Companion2.1.6. See [FAQ](FAQ_EN.md), [한국어 FAQ](FAQ_KO.md) and [exact verification](VERIFICATION_v2.5.0.md).

### Device and application limits

- Intended Windows targets are7 SP1/8.1/10/11 x64. Windows7 needs the framework/legacy ADB prerequisites.
  Automated702 checks are not physical verification of every OS/GPU/driver/DPI combination.
- Android16/One UI8.x and the maintainer's S26 Ultra/Android17/One UI9 are reported working cases.
  Older Classic DeX firmware may show a black desktop. Protected apps/content and secondary-display restrictions are not bypassed.
- Lower DeX resolutions or DPI combinations can change launcher layout, wallpaper and icon behavior. The configured virtual resolution
  differs from the PC scrcpy window's display size.
- Experimental120 FPS is a maximum only. Keep the phone screen on and confirm heat/battery/OLED warnings at every start.
  Firmware, app/activity, performance and transport can limit real FPS. Manual screen-off can still lower it.
- SDL3 right Shift compatibility maps it to left Shift in eligible scrcpy windows. Android cannot distinguish the sides in that session.

### Connection, transfer and cleanup limits

- Guest Wi-Fi/client isolation/VLAN/firewall rules can block local traffic or endpoint discovery even with the same Wi-Fi name.
  Pairing and connection ports differ. Verify phone ownership before updating an endpoint; don't guess another phone.
- New USB preference does not migrate live wireless sessions. USB loss stops affected sessions without an immediate Wi-Fi replacement.
- Android shares do not supply an arbitrary folder tree; use Companion's folder selector for folders.
  Transfer progress intentionally omits unsupported percentage/ETA. Disconnect/cancel/partial cleanup can require retry.
- Complete normal exit while still connected is preferred. Windows shutdown uses an existing authenticated Companion channel best-effort;
  no new ADB helper is started.
- Companion protection can remain while connected or while recovery is active/unverified.
  A lost PC socket alone does not guarantee recovery while the underlying configured transport still appears present.
  Version2.1.6 promptly stops a disconnected, verified-clean guardian.
- Companion cleanup removes the global simulated-display setting, including manually configured displays; it cannot identify their origin.

### Notifications and portable state

- A background update starts before the optional startup hide-to-tray finishes. If a response arrives unusually early,
  the main banner can be created before hiding and no separate tray notice may be issued at that moment.
  The cached banner remains available when the main window is reopened. This is a static ordering risk; no change was requested.
- Update checking is once per local day, not an Internet reachability monitor. Offline results consume the automatic attempt;
  use the manual button if a connection becomes available later. Cached update availability can remain offline.
- A fresh extraction folder lacks the previous config/history unless the user copies it. Safety consent can reappear in a clean folder.
  Local changes notes identify older installations through previous accepted consent, not settings-file existence alone.

### Source and release scope

The final Windows EXE has a maintainer-reported clean antivirus result for its exact D3342F… hash.
The public source includes the matching Companion source/constants/APK snapshot under `companion/`.
Historical scan results still refer to their own exact files.
Remote macOS projects/workflow/docs are preserved in the merged source, with separate .NET 8 proxy/foundation projects. Published Mac ZIPs were not found at the review date.
Documentation preparation does not authorize commit/push/tag/release.

## 한국어

기기·OS·GPU·펌웨어·앱별 제한,120 FPS의 최대값 특성과 발열·번인, 무선 포트·네트워크 격리,
연결 손실 시 정리의 한계는 위 현행 설명을 따릅니다.
프리뷰/최종 파일은 검증했지만 모든 환경의 실기 결과나 새 백신 검사로 확대하지 않습니다.
공용 Companion의 해당 버전 소스·APK 스냅샷을 공개 소스에 포함하고 macOS 프로젝트를 분리해 보존합니다.
아래 옛 Companion·UI·단일 기기 기록은 당시 상태이며 현재 문제 목록과 구분합니다.

<details>
<summary>Earlier implementation/preparation records (historical)</summary>

These dated records describe earlier states. Use the current sections above for2.5.0.


## 2026-10-19 portable 120 FPS preparation

The maintainer authorized implementing the Store handoff in the portable product
and rebuilding the fixed preview/final ZIP locally. Store is read-only; reference
commit11050e7822c43ec409d7d943ece5d9884d997b99 is not merged.
Added default60/experimental120, one fail-closed warning before every120 start,
exact screen-off preference restoration, per-phone mixed-session screen policy
and local version-scoped change notes. scrcpy5.0.1 and signed Companion2.1.6 are
unchanged. No AI app detection, Android changes, commit/push/tag or publication.
See PORTABLE_120FPS_2026-10-19.md for implementation and fresh verification.
October16 scan evidence remains historical; the new EXE needs a fresh user scan.


## Current state — 2026-10-16

- Latest public Windows release:2.0.1. Prepared local Windows build:2.5.0; not published.
- Active portable branch: feature/portable-backport-v2.0.1-20260928, local HEAD8b19906 plus accepted uncommitted work.
- scrcpy 5.0.1; official shared Companion 2.1.6/code13/protocol1; Android 13 minimum.
- New launches USB-first; live serials stay captured. Companion management has its own Settings page.
- First-run consent is saved once; updates check daily in the background with offline failure recorded.
- The exact release ZIP/EXE/APK have been built and the maintainer reports clean antivirus results.
- Current work is local GitHub-documentation/screenshot preparation; no release approval has been given.
- Standalone public-source shared-component closure, remote macOS preservation and new portable captures
  are tracked in DOCUMENTATION_AUDIT_2026-10-16.md and SCREENSHOT_PLAN_v2.5.0.md.

See PROJECT_BRIEF.md, BUILDING.md and VERIFICATION_v2.5.0.md for the current contracts and boundaries.
Older dated implementation notes below describe earlier stages; their old version pins are historical.

## 새 WinForms 테마의 검증 범위

밝음·다크 카드 배경은 GDI+ 알파 색상 혼합이며 실제 데스크톱을 비추는 Acrylic/Mica가 아니다.
제목 표시줄은 Windows 네이티브 테마를 따른다. 새 배치는 명령을 차단한 UI 사본에서
확인했으며 Windows 7·높은 DPI·실제 복수 휴대폰 선택 UI는 실기 확인이 남아 있다.

## Companion 2.1.2의 대기 전송 종료 및 2.1.3 수정 검증 한계

Android 15+ dataSync timeout 또는 서비스 제거 때 여러 전송이 대기 중이면,
실행되지 않은 작업의 finally가 호출되지 않아 URI 권한과 queued 이력이 남을 수
있다. 다음 작업이 공통 취소 상태를 되돌리는 경계도 Store 작업방에서 확인했다.
이는 코드 근거로 확인한 제약이며 이번 일반 전송 실기 성공과 구분한다.

포터블 Android 소스에는 요청별 추적·일회 종료 정리 보강과 회귀 테스트가 있다.
사용자 승인으로 새 서명 APK 2.1.3/versionCode 10에 반영했다. 실기 timeout 및 대기 전송 종료는 아직 확인하지 않았다.
문서 제공자에서 서로 다른 이름이 trim/slash 치환 뒤 같아지는 형제 항목은
2026-10-05의 서명 APK 2.1.4/versionCode 11에서 번호를 붙여 분리한다. 기존 번호 이름을
예약하고 파일 확장자와 폴더 하위 경로를 유지한다. Android 단위 테스트 20개 및 Release
lint/서명 빌드를 통과했으며, 실제 문서 제공자 전송은 아직 미확인이다.

## 네트워크 격리

무선 ADB는 PC와 휴대폰의 직접 로컬 통신이 필요하다. 같은 대역이어도 게스트
Wi-Fi, AP/client isolation, VLAN 또는 인트라넷 차단이 있으면
`adb connect`가 10060 timeout으로 실패한다.

ASUS 공유기는 게스트 네트워크의 인트라넷 접근 허용과 AP 격리 해제를
확인한다. 개발 중 5GHz 초기 연결 실패가 2.4GHz 연결 및 ADB 재시작 뒤
해소됐다. 재현 시 ARP, 5555 포트와 공유기 격리를 먼저 확인한다.

## 무선 준비 지속성

휴대폰 재부팅이나 Android 정책으로 `adb tcpip`가 풀리면 USB로 무선 준비를
다시 실행한다. IP는 비워두면 자동 감지된다.

## Windows 7

자동화된 Windows 7 테스트 환경은 없다. 릴리스 전 회사 PC 실기 확인이
필요하다.

## 여러 휴대폰

v2는 연결된 여러 물리 휴대폰을 동시에 관리한다. 각 휴대폰은 독립된 DeX·
단일창·설정·파일 전송·Companion 세션을 가지며, 같은 휴대폰의 USB와 무선
ADB transport는 하나의 기기로 병합한다. USB 또는 무선 중 사용자가 선택한
연결 방식만 사용하며 반대 방식으로 자동 전환하지 않는다.

현재 자동화 및 실기 기준은 휴대폰 두 대다. 설계상 더 많은 기기도 등록할 수
있지만, PC 자원·ADB 안정성·Samsung 펌웨어에 따른 실사용 상한까지 보장하지는
않는다.

## Android와 One UI 호환

현재 정상 동작을 확인한 휴대폰 기준은 Android 16 / One UI 8.x가 설치된 DeX
지원 Galaxy 기기다. One UI 7.x 이하에서는 가상 디스플레이에 DeX가 원활하게
나타나는지 확인되지 않았으며 검은 Scrcpy/DeX 창이 열릴 수 있다. 기기 모델,
Samsung 펌웨어와 클래식 DeX 구현 여부에 따라서도 결과가 달라질 수 있다.

단일창은 Scrcpy 자체 가상 디스플레이를 사용해 동작할 수 있지만 HID 키보드와
마우스 호환성까지 보장하지 않는다. 특정 기기에서 불안정하면 두 HID 옵션을
끄고 비교할 수 있으나 이를 해당 버전의 공식 지원으로 간주하지 않는다.

## Samsung DeX 저해상도와 DPI

Samsung DeX의 기본 최소 해상도는 1600×900이다. 더 낮은 가상 해상도도
실행될 수 있지만 앱 서랍 상단이 잘리는 등 일부 DeX UI가 정상 배치되지
않을 수 있다. 해상도와 DPI 조합에 따라 DeX가 서로 다른 바탕화면 배치와
배경화면 상태를 사용하는 것도 Samsung DeX 동작이다.

DPI는 120 미만에서 overlay 생성이 실패하므로 DX Manager가 입력을 거부하고
편집 전 값으로 복원한다. 매우 높은 DPI는 생성되더라도 UI가 지나치게 커져
사용하기 어려울 수 있다. 사용자 지정 가로·세로 값은 Android overlay 제한에
맞춰 각각 4096을 넘길 수 없다.

## 비정상 종료 뒤 overlay 잔존

정상 종료에서는 overlay를 제거한다. Windows 세션 종료 중에는 새 ADB 프로세스를
시작하지 않으며, 이미 인증되어 연결된 DX Companion guardian 세션이 있을 때만
overlay 제거와 `절전모드 해제` 복원을 요청한다. 다만 프로세스 강제 종료,
전원 차단, 이미 끊어진 기기, Companion 미설치·미연결 또는 Windows가 정리 시간을
주지 않는 경우에는 Android의 보조 디스플레이나 `절전모드 해제` 상태가 휴대폰에
남을 수 있다. 특히 Windows 7의 실제 종료 순서는 시스템과 ADB 상태에 따라 달라질
수 있으므로 Windows 종료 자체를 정상적인 세션 정리 방법으로 보장하지 않는다.
DX Companion을 사용하거나 개발자 옵션의
`보조 디스플레이 시뮬레이션`에서 임의 항목을 켰다가 다시 끄면 제거할 수 있다.
자세한 복구 절차는 사용자용
[`FAQ_KO.md`](FAQ_KO.md)에 정리했다.

## 외부 Scrcpy

Scrcpy 3.3.4와 4.x의 주요 옵션 차이는 자동 처리한다. 3.3.4에서는
`--keep-active` 대신 `-w`를 사용하고 `--flex-display`를 제외한다.
그 밖의 과거/향후 버전과 사용자가 직접 입력한 추가 인자의 호환성까지
보장하지는 않는다. 기준 번들은 Scrcpy 4.1이다.

## Scrcpy 4.x/SDL3 오른쪽 Shift 호환 보정

Windows용 Scrcpy 4.0/SDL3에서 물리 오른쪽 Shift가 Android로 정상 전달되지
않는 현상을 재현했다. 3.3.4/SDL2에서는 정상이며 `-K`, `-M` 사용 여부와
관계없이 발생했다. 이 4.0 재현 근거를 바탕으로 현재 SDL3 기반 Scrcpy 4.x
클라이언트에도 호환 보정을 적용한다.

DX Manager는 SDL3 Scrcpy가 활성화된 동안 오른쪽 Shift를 왼쪽 Shift로
치환한다. 타이핑은 정상화되지만 Android 앱에서 좌우 Shift를 서로 다른
키로 사용하는 경우에는 두 키를 구분할 수 없다. 다른 Windows 앱과 SDL2
Scrcpy에는 영향을 주지 않는다. Upstream 보고 및 수정 여부를 추적한다.

## 관리형 파일 전송

DX Manager 관리형 전송 대상은 `/sdcard/` 또는 `/storage/emulated/0/` 아래의
하위 폴더로 제한한다. 단독 APK 설치와 그 밖의 ADB 명령은 Scrcpy의 순정
동작을 유지한다. Android 경로의 각 구성요소는 UTF-8 기준 255바이트를 넘을
수 없으며, 충돌 접미사를 붙인 최종 이름도 이 제한을 만족해야 한다.

폴더 전송은 정션·심볼릭 링크 등 재분석 지점을 건너뛴다. 상태창에는 현재
항목과 다음 4개, 원본 크기, 경과 시간과 누적 완료·실패·대기 수를 표시하지만
신뢰할 수 없는 퍼센트와 남은 시간은 표시하지 않는다. 전송 중 Scrcpy 창,
기기 연결 또는 DX Manager가 종료되면 현재 전송을 취소하고 임시 파일·staging
폴더 정리를 시도한다. 이미 기기 연결이 사라졌다면 정리가 실패해 로그만 남을
수 있다.

설정에서 관리형 전송을 끄면 새로 여는 DeX·단일창부터 순정 Scrcpy 파일
드롭을 사용한다. 이미 실행 중인 창의 환경 변수는 바뀌지 않는다. 순정 방식은
일부 Windows 환경에서 한글 등 비ASCII 로컬 파일명을 보존하지 못할 수 있다.

## 앱별 보안 및 다중 디스플레이 제한

일부 금융·게임·스트리밍 앱은 USB 디버깅 또는 개발자 옵션 활성 상태를
감지해 실행을 거부한다. 보호된 화면과 DRM 콘텐츠는 Scrcpy에서 검게 보이거나
캡처가 차단될 수 있으며, 가상·보조 디스플레이 실행 자체를 거부하는 앱도
있다. 이러한 앱 및 Android 보안 정책은 우회하지 않는다.

다중 디스플레이를 완전히 지원하지 않거나 기존 휴대폰 task를 재사용하는 앱은
DeX에서 시작해도 휴대폰 화면에서 열릴 수 있다. 선택 앱 강제 종료가 일부
경우에 도움이 되지만, 앱 자체 정책까지 강제로 변경할 수는 없다.

## Android 정리 앱 권한 부여

공식 DX Companion이 설치된 경우에만 진단 페이지에서 권한을 부여할 수 있다.
번들 설치 기능은 정확한 APK SHA-256과 v2 서명 인증서를 먼저 검사하고 현재
선택된 ADB 기기에만 설치한다. 설치 후에도 `base.apk`의 서명, package와 버전을
다시 확인한다. 패키지 누락·서명 불일치·ADB 미승인 상태에서는 설치 또는 권한
버튼을 비활성화하며 다른 서명의 같은 패키지에는 권한을 부여하지 않는다.
권한 부여 직전과 직후에도 다시 검사하고 사후 검증이 실패하면 즉시 회수한다.

Companion은 Android의 단일 전역 `overlay_display_devices` 설정을 삭제하므로,
DX Manager가 만든 화면과 사용자가 개발자 옵션에서 직접 선택한 보조
디스플레이를 구분할 수 없다. 정리하면 현재 설정된 시뮬레이션 화면이 모두
제거된다. 앱을 삭제한 뒤 다시 설치하면 권한도 사라져 다시 부여해야 한다.
개발자 옵션의 절전모드 해제를 끄는 기능도 제공하며, 타일과 위젯의 기본
동작은 두 항목을 함께 정리하는 것이다.

## 배포

- 설치 프로그램 없음
- Assembly/File version은 `2.0.0.0`
- 앱 아이콘, 제작자/GitHub 링크, MIT 라이선스와 제3자 고지 완료
- README, 사용 설명서와 FAQ의 한국어/영어 스크린샷 배치 완료
- 공개 ZIP에서 개인 설정, PDB, 로그와 테스트 스크린샷 제외 확인
- 자동화 테스트 없이 주요 흐름은 실기 테스트에 의존

## 2026-10-05 final acceptance boundary

메이님이 실기 검증을 완료했고 잘 작동한다고 보고하여2.5.0 최종본으로 준비했다. 위의 이전 미확인 기록을 사용자 확인이 전혀 없다는 뜻으로 읽지 않는다. 사용자가 명시하지 않은 모든 OS/DPI 조합, 강제 Android timeout 또는 드문 ADB Kill 실패를 독립 재현했다고 확대하지 않는다. GitHub 업로드와 바이러스 검사 결과는 현재 대기 상태다.

</details>

## macOS 포크 현행 기록 (s1ckdark/DX-Manager)

아래 절은 이 포크의 macOS GUI/TUI 작업에서 확인된 현행 제약과 실기 검증 범위다. 위 upstream Windows 2.5.0 범위와 구분한다.

Apple Silicon ZIP은 Apple Silicon Mac에서 실제 바이너리 기동을 확인했다.
Intel ZIP은 아키텍처와 외부 경로를 검사하고 Apple Silicon Mac의 Rosetta에서
실행 파일 기동을 확인했지만, Intel 실기 전체 DeX 흐름은 GitHub의
`macos-15-intel` 자동 빌드와 별도로 실제 Intel Mac 확인이 남아 있다.

## macOS GUI Phase 2 실기 검증 범위

Phase 2(DeX 시작/중지 + 단일창 슬롯) 종료 시점에는 실기 검증 8개 항목을 **이 개발
환경에서 하나도 수행하지 못했다.** 이후 Phase 3 진행 중(Task 10 착수, 2026-09-08)
사용자가 실제 Mac에서 GUI를 사용해 그중 일부를 실증했다. 아래는 그 결과를 반영해
갱신한 상태다. 여전히 사용자가 직접 확인하지 않은 항목은 성공했다고 가정하지 않고
미확인으로 남긴다(`AGENTS.md` 원칙).

- 이 개발 환경 자체는 여전히 GUI를 기동하지 못한다 — `dotnet run --project
  DexManager.Desktop`이 `System.InvalidOperationException: Avalonia.Native was not
  able to start the RenderTimer. Native error code is: -6661`로 종료 코드 134를 내고
  죽는다. 스택은 `Avalonia.AppBuilder.Setup()` 내부에서 끝나며, 이 저장소의 어떤
  코드도 실행되기 전이다. 즉 코드 결함이 아니라 이 환경의 제약이다. 아래 검증은 모두
  사용자의 실제 Mac에서 이루어졌다.

미확인 항목 8개(스펙 5.3절, `docs/TODO.md`의 "macOS GUI Phase 2" 체크리스트 참조) 중
현재 상태:

1. 기기 목록에 연결된 기기가 나타난다 — **확인됨.** 근거: 사용자가 실기 세션에서
   기기를 선택해 Start DeX를 실행했다(아래 UI-2 항목 참조) — 목록에 기기가 없었다면
   시작 자체가 불가능하다.
2. `Start DeX` → scrcpy 창이 뜨고 `DeX` 표시가 붙는다 — **확인됨(창이 뜨는 동작만).**
   같은 세션에서 Start DeX 실행 결과로 창이 떴고, 그 화면에서 잠금화면 미러링 문제
   (UI-2, 아래 "macOS GUI Phase 3 알려진 한계" 참조)를 사용자가 발견했다 — 창이 뜨지
   않았다면 이 문제를 볼 수 없었다. 목록 행의 `DeX` 표시 자체를 사용자가 별도로
   확인했다는 보고는 없다.
3. `Stop DeX` → 창 닫힘과 `DeX` 표시 제거 — 미확인 (Stop을 눌렀다는 보고 없음)
4. 중지 뒤 `adb shell settings get global overlay_display_devices`가 `null` — **미확인**
5. 슬롯 1에 앱 패키지 지정 후 `Start` → 앱 창 — 미확인 (사용자는 슬롯 UI 화면을
   봤을 뿐(UI-3 항목) 실행하지는 않았다)
6. 슬롯 `Stop` → 창 닫힘 — 미확인
7. 창을 닫은 뒤 `pgrep -fl scrcpy`가 비어 있음 — 미확인
8. DeX 실행 중 창을 닫아도 overlay가 회수됨 — **미확인**

4번과 8번은 이번 Phase가 지키려던 display-overlay 불변식을 실제로 행사하는 유일한
검사이며, 사용자의 실기 세션에서도 명시적으로 확인되지 않았다. 자동 테스트와 코드
검토로만 뒷받침되어 있다. 4번과 8번이 실기에서 통과하기 전까지 이 불변식은 "지키도록
작성된 코드"이지 "실기로 지켜짐이 확인된 코드"가 아니다.

## macOS GUI Phase 3 알려진 한계

1. **DeX 미러는 잠금화면을 "보여줄" 수 없다 — 이건 구조적 제약이다. 다만 PC에서
   폰 잠금을 "해제"하는 것 자체는 가능하다(실기 확인, SM-F971N One UI).** DeX는
   `overlay_display_devices`로 새 가상 디스플레이를 만들고 scrcpy가
   `--display-id <가상 디스플레이>`로 그 화면만 미러링한다. Android의 keyguard(잠금화면)는
   항상 주 디스플레이(0)에만 렌더링되며 그 디스플레이는 미러링 대상이 아니므로, DeX
   미러에는 잠금화면이 절대 나타나지 않는다 — scrcpy 플래그 문제가 아니다.

   그러나 해제 자체는 다른 얘기다 — 다만 실기로 확인한 결과 **두 단계가 모두
   필요하다**: `adb shell input keyevent 224`(`KEYCODE_WAKEUP`)로 먼저 깨우고, 그
   다음 `adb shell wm dismiss-keyguard`로 키가드를 해제한다. `wm dismiss-keyguard`
   단독으로는 폰이 잠들어 있는 동안(`INTERACTIVE_STATE_SLEEP`) 아무 효과가 없었고,
   반대로 깨우기만으로는 키가드가 풀리지 않았다(fix round 1) — 첫 수동 시험에서
   깨우기만으로 풀린 것처럼 보였던 건 마침 그 시점에 Smart Lock 신뢰가 재승인된
   우연이었다. DX Manager는 이제 DeX 시작 시 이 두 단계를 순서대로 자동으로 보낸다
   (`AdbService.WakeScreen` → `AdbService.DismissKeyguard`,
   `DexOrchestrator.StartCore`) — 아래 UI-2 잠금 게이트가 "깨우고 해제까지 시도한
   뒤"의 상태를 판단하도록 그 앞에 배치했다. 둘 중 하나가 실패해도(각각 예외를
   삼키고 false를 반환) 시작 자체는 막지 않는다.

   **한계 — 잠금화면은 PC로 미러링할 수 없다.** `dumpsys trust`의 `deviceLocked=1`처럼
   PIN/패턴이 실제로 요구되는 상태에서는 깨워도 자격증명 화면이 디스플레이 0에만 뜨고,
   DeX 미러(가상 디스플레이)는 그 화면을 보여주지 않는다. **그렇다고 디스플레이 0을
   따로 미러링해서 우회할 수도 없다**: 안드로이드는 키가드에 `FLAG_SECURE`를 설정하며,
   보안 창은 화면 캡처에서 제외되므로 `scrcpy --display-id 0` 세션은 연결되어 살아
   있으면서도 **빈 화면만 렌더링한다**. 실기 확인(SM-F971N, One UI, Android 17)에서
   scrcpy는 오류 없이 기동해 `[server] INFO: Device: [samsung] samsung SM-F971N`을
   찍었지만 창에는 아무것도 보이지 않았고, 결국 폰에서 직접 해제해야 했다. 이는 기기나
   빌드의 특성이 아니라 안드로이드의 의도된 보안 동작이므로 어떤 scrcpy 옵션으로도
   우회할 수 없다.

   따라서 PC에서 "PIN 화면을 보면서 입력하는" 경로는 존재하지 않는다. 화면 없이
   `input text <PIN>`을 그대로 보내는 것은 기술적으로 가능하지만, 사용자의 PIN을 앱이
   다루게 되므로 의도적으로 구현하지 않았다. 자동 wake+dismiss는 어디까지나 "잠금이
   없거나 신뢰 에이전트로 이미 풀린" 가장 흔한 경우를 위한 것이고, 자격증명이 실제로
   필요하면 폰에서 직접 해제해야 한다.

   **시작 버튼은 한 번만 누르면 된다 — 2단계 잠금 대기.** dismiss 직후 잠금 게이트는
   먼저 짧은 폴(예산 2000ms, 간격 150ms)로 해제 반영을 기다린다. 그래도 `Locked`면
   바로 실패시키지 않고 "잠금 해제를 기다린다"는 로그(`Log.Dex.WaitingForUserUnlock`)를
   남긴 뒤 두 번째 폴(예산 15000ms, 간격 500ms,
   `DexOrchestrator.UserUnlockWaitBudgetMs`)로 사용자가 폰에서 PIN·지문·얼굴 인증을
   마치기를 기다린다. 이전에는 짧은 폴만 있어서 사람이 2초 안에 인증을 끝내지 못하면
   첫 시작이 `Error.Dex.DeviceLocked`로 실패했고, 잠금을 푼 뒤 한 번 더 눌러야 연결됐다.
   두 단계 모두 `Unlocked`나 `Unknown`이 나오면 즉시 진행한다(fail-open 유지). 두 번째
   단계까지 `Locked`일 때만 기존 오류를 낸다. 두 번째 단계의 대기는 `_shutdownSignal`로
   하므로, 대기 중 종료가 요청되면 남은 확인을 돌리지 않고 시작을 조용히 포기한다(첫머리의
   종료 검사와 같은 `return false`). 진행 표시나 UI 문구는 바꾸지 않았다 — 확인이
   정상 응답하면 최대 약 17초 동안 시작 버튼이 진행 중 상태로 머문다. 다만 이미 나간
   adb 확인 하나(`IsDeviceLocked`)는 종료 신호로 끊기지 않고 자체 타임아웃
   (`AppSettings.Timing.ProcessTimeoutMs`, 기본 15000ms)까지 기다릴 수 있다. 같은 2단계
   대기는 시작 버튼뿐 아니라 실행 중 설정 적용(`ApplyRuntimeSettingsCore`가 `StartCore`를
   다시 부른다)에도 적용된다. 실제 PIN/생체 인증 기기에서 한 번 누름으로
   연결되는지는 아직 실기로 확인하지 않았다(테스트는 가짜 adb의 `dumpsys trust` 전환으로만
   고정했다).

2. **UI-2의 잠금 감지는 `dumpsys trust`를 1순위로 쓴다 — `dumpsys window`의 secure
   필드 접근은 One UI에서 실기 확인 결과 발동하지 않는다.** 원래 구현
   (`AdbService.ParseLockState`)은 `dumpsys window` 출력에서 `mShowingLockscreen` →
   `mDreamingLockscreen` → `mKeyguardShowing` → `isStatusBarKeyguard` 순서로 "잠금
   화면이 떠 있는가"를 본 뒤, 그게 참이면 다시 `isKeyguardSecure` 계열 필드나
   `KeyguardServiceDelegate` 블록의 `secure=`로 "실제로 보안 설정됐는가"를 확인했다.
   **실기(SM-F971N, One UI) 확인 결과, 이 두 번째 단계가 이 기기에서 절대 발동하지
   않는다** — tier-1 secure 필드가 하나도 없고, `KeyguardServiceDelegate` 블록에도 맨
   `secure=`가 없다(그 블록의 실제 필드는 `showing`/`inputRestricted`/`occluded`/
   `trusted`/`simSecure`/`dreaming`/… 뿐이다). 그 결과 `ParseKeyguardSecure`가 항상
   null을 돌려주고 `ParseLockState`는 항상 `Unknown`으로 fail-open한다 — 설계상
   안전하지만(정상 시작을 막지 않는다) 이 게이트가 이 기기에서는 그냥 무동작이었다는
   뜻이다.

   그래서 `IsDeviceLocked`는 이제 `dumpsys trust`를 먼저 본다. 실기 출력 예:
   `User "..." (id=0, ...) (current): trustState=TRUSTED, trustManaged=1,
   deviceLocked=0, isActiveUnlockRunning=0, strongAuthRequired=0x0`. `(current)`가
   붙은 사용자 줄(다중 사용자 대비)의 `deviceLocked=1`이면 `Locked`, `deviceLocked=0`이면
   `Unlocked`다 — 이 신호는 키가드가 "떠 있는지"가 아니라 "실제로 잠겨 있는지"를
   직접 말해주므로 Smart Lock 등 신뢰 에이전트도 정확히 반영하고, F-8(스와이프 전용
   폰의 오탐)을 애초에 만들지 않는다. `dumpsys trust`가 아무 신호도 못 주는
   기기/빌드에서는 위 `dumpsys window` 휴리스틱으로 폴백한다 — 삭제하지 않았다. 두
   신호 모두 알려진 필드를 찾지 못하면 여전히 `Unknown`으로 fail-open하며, `IsDeviceLocked`
   자체는 adb 실행 실패까지 `try/catch`로 감싼다(UI-2 fix round 1). `dumpsys window`
   경로의 F-8 좌측 단어 경계 보정(F-9)은 여전히 유효하며 실기에서 load-bearing이었다
   (같은 `KeyguardServiceDelegate` 블록의 `simSecure=false`가 경계 없이는 `secure=false`로
   오독됐다). 근거: `.omc/research/2026-09-08-realdevice-lock-findings.md`.

3. **단축키가 macOS에서 전혀 동작하지 않는다.** `MacKeyboardService.Start`/`Stop`/
   `ReloadConfiguration`(`DexManager.Platform.Mac/Platform/MacKeyboardService.cs`)이
   모두 빈 no-op이고, `ApplicationHost`도 이 서비스의 `Start()`를 호출하지 않는다.
   실제로 동작하는 유일한 단축키 경로는 `DexManager/Services/HotkeyService.cs`의 Win32
   `RegisterHotKey`뿐이며 WinForms(Windows) 전용이다. 설정 화면(Task 10)은 단축키
   문자열을 캡처해 저장하고 화면에도 그렇게 안내하지만, macOS에는 저장된 값을 실제로
   듣는 쪽이 없다.

4. **HID 키보드/마우스(`-K`/`-M`)는 Windows 전용이다.**
   `DexManager.Core/Services/ScrcpyService.cs:415-416`,
   `DexManager.Core/Services/SingleWindowService.cs:623-624`가
   `OperatingSystem.IsWindows()`로 게이팅하므로 macOS에서는 `UseHidKeyboard`/
   `UseHidMouse` 체크박스를 켜도 scrcpy 인자에 반영되지 않는다. Slot 탭 툴팁(UI-3)에
   이 사실을 명시했다.

5. **설정 창은 대부분 지역화되지 않았다.** 창의 사용자 노출 문자열 가운데 라벨·헤더·
   버튼 텍스트는 대부분 하드코딩 영어이고(Ruling 6), 지역화(resx)를 거치는 것은 UI-3이
   추가한 슬롯 툴팁 계층과 몇 개의 안내 문구뿐이다. 정확한 비율은 UI 변경마다 달라지므로
   숫자로 고정하지 않는다. MainWindow도 원래 전부 하드코딩 영어였으므로 이번 Phase의 회귀는
   아니며, Phase 3 Task 8의 범위는 "언어 설정 저장 + 신규 문자열 런타임 적용"으로
   한정됐다. 지역화 완성은 후속 과제다(`docs/TODO.md`의 Phase 3 지연 항목 참조).

6. **파킹된 리뷰 발견 사항 (수정하지 않기로 결정, 기록만 남김 — 상세는
   `docs/TODO.md`의 "macOS GUI Phase 3 리뷰에서 지연된 정리 항목" 참조):**
   - `SettingsViewModel`의 `SelectedIdentity` 재계산(Task 5)이 `IDeviceSelectionSource`에
     문서화되지 않은 암묵적 UI-스레드 불변식에 의존한다.
   (전체 브랜치 리뷰의 최종 수정 라운드에서 아래 두 항목은 해결되어 목록에서 빠졌다:
   단축키 캡처 필드의 Tab/Shift+Tab/Escape 삼킴 → 이제 통과시킨다. Save가 무효 페이지를
   건너뛴 채 창을 닫던 문제 → Save 버튼이 HasChanges와 전 페이지 유효성을 함께 본다.)

## macOS GUI Phase 3 실기 검증 범위

Phase 3가 도입한 `SettingsWindow`의 시각·상호작용 동작은 개발 중 어떤 윈도우 서버에서도
실행되지 않았다(이 개발 환경은 GUI가 기동하지 않는다 — 위 "macOS GUI Phase 2 실기 검증
범위" 참조). 다만 사용자가 실기에서 UI-1/UI-3의 근거가 된 조작(설정 창 열기, 탭 이동,
Cancel 클릭, 슬롯 화면 확인)을 했으므로 **창 자체가 뜨고 탭·버튼이 반응한다는 사실은
실증됐다.** 아래는 그 실증 범위를 벗어나 여전히 미확인인 항목이다.

- 테마를 바꾸면 열려 있는 다른 창에도 실시간으로 반영되는가 (Task 7)
- 언어를 바꾸고 저장하면 "재시작 후 적용" 안내가 뜨는가 (Task 8)
- 단축키 필드가 실제 키 입력을 캡처해 텍스트로 표시하는가 (Task 10)
- 대상 기기가 DeX 실행 중일 때 안내 문구가 뜨는가 (Task 5)
- 기기를 선택하지 않았을 때 Display/Stream·Slot 탭이 placeholder로 전환되는가
  (Task 11/12)
- Save/Cancel을 눌렀을 때 실제로 창이 닫히는가 — UI-1의 수정 자체는 자동 테스트로만
  검증됐다. 사용자가 신고한 것은 수정 **전** 동작이며, 수정 후 재확인 보고는 없다.

이 목록은 코드 검토와 자동 테스트(xUnit 284개 — Desktop 17 + ViewModels 112 +
Core 155 — + 다중기기 39개)로만 뒷받침된다.

## macOS GUI Connection 탭 실기 검증 범위

설정 창 Connection 탭(기기별 USB→무선 전환·IP:포트 직접 연결·무선 해제)은 자동 테스트로만
검증됐다(ViewModels.Tests 152, Desktop.Tests 26, Core Tests 281; 게이트웨이 테스트는
`TempHostRoot`의 실제 `ApplicationHost`를 쓰지만 adb는 가짜다).

2026-10-31 이 개발 환경에서 GUI 기동을 다시 시도한 결과, `DOTNET_ROOT=$HOME/.dotnet`을
주고 `dotnet DXManager.Desktop.dll`로 실행하면 Avalonia 창이 정상적으로 뜬다(이전 기록의
RenderTimer -6661은 재현되지 않았다). apphost `DXManager.Desktop`을 직접 실행하면
`DXMAdbProxy`와 같은 이유로 "You must install .NET" 오류가 난다. 기기가 연결되지 않은
상태에서 설정 창을 열어 Connection 탭이 다른 기기별 탭과 같은 자리에 나타나고 기기
미선택 placeholder를 보이는 것, Cancel과 `Cmd+Q` 종료가 정상인 것까지 확인했다. 첫
시도에서 Connection 탭을 클릭하자 설정 창이 사라진 일이 한 번 있었으나 다시 열어
클릭했을 때는 재현되지 않았고 프로세스는 살아 있었으며 stderr에 아무것도 없었다.
원인은 모른다.

2026-10-31 SM-F971N(R5KL808ZQTP)을 USB로 연결해 같은 방식으로 실기 확인했다. 기기 선택 뒤
Connection 탭은 `Usb: R5KL808ZQTP`, 저장된 방식 USB, 포트 5555, 자동 재연결 켬을 표시했고
"Prepare over USB"만 활성, 나머지 둘은 비활성이었다. Prepare를 누르자 휴대폰에는
`service.adb.tcp.port=5555`가 적용됐으나(USB transport_id 36→37로 재열거), 약 1초 뒤
설정 창의 Connection 탭이 "Select a device…" placeholder로 바뀌었다. `adb tcpip`가 adbd를
재시작하면서 USB transport가 잠시 사라지고, 기기 목록이 선택을 비우면서
`LoadDevicePages("")`가 진행 중인 Connection 페이지를 폐기한 것이다. 그래서 결과 메시지를
볼 수 없었고 Save 게이트도 그 순간 풀렸다. 프로필은 저장되지 않았다. 셸에서 직접
`adb connect 192.168.1.86:5555`를 실행하니 `No route to host`였으므로 연결 실패 자체는 이
Mac과 휴대폰 사이의 네트워크 경로 문제이지 앱 논리 문제가 아니다(몇 분 뒤 같은 대역에서
5555 포트 도달이 확인됐다). "전환 도중 기기 재열거로 페이지가 사라져 결과를 잃는" 동작은 실제로
재현된 결함이며, 같은 날 `DeviceListViewModel`에서 수정했다: 선택된 기기 행을 transport가 없는
"Disconnected" 자리표시자로 유지하고 같은 identity가 돌아오면 같은 행을 갱신한다. 자리표시자는
10초 이상 사라진 뒤 다른 연결된 휴대폰이 있을 때만 제거되며, 다른 휴대폰이 없으면 남는다.
그래서 마지막 휴대폰을 영구히 뽑아도 목록은 비지 않고 "Disconnected" 행이 남는다(의도된 동작,
`docs/TODO.md` 참조). 수정 뒤 Prepare 경로의 실기 재확인은 아직 하지 않았다. 포트 입력란(NumericUpDown)은 폭이 좁아 "5555"의 마지막 자리가 잘려
보인다.

같은 날 네트워크가 정상인 상태(Mac 192.168.1.19, 휴대폰 192.168.1.86, 5555 포트 도달)에서
사용자가 GUI를 직접 조작해 확인한 결과: IP 입력란에 호스트명 `z-fold8`을 넣고 "Connect
wirelessly"를 누르자 무선 연결이 성공했고, 프로필이 `Mode=Wireless`, `WirelessHost=z-fold8`,
`WirelessPort=5555`, `AutoReconnect=true`로 저장됐다. 이후 USB를 분리한 상태에서 `adb devices`에는
`z-fold8:5555`만 남았고, 그 transport로 DeX 시작과 **Stop DeX**가 정상 동작했다고 사용자가
보고했다. Stop DeX 뒤 `overlay_display_devices`는 `null`, `stay_on_while_plugged_in`은 `0`,
디스플레이는 내장 0·1만 남아 overlay 회수가 확인됐다(2026-10-31, SM-F971N). 호스트명 입력이
동작한 것은 `NormalizeHost`가 IP 외 호스트명도 허용하기 때문이며, 로컬 DNS/mDNS 해석에 의존한다.
아래 나머지 항목은 여전히 **미확인**이다.

- USB 연결 상태에서 Prepare → 기기의 `Transports`에 Wireless 항목이 추가 표시되는가
  (2026-10-31: 네트워크 미도달로 연결 단계가 실패해 확인하지 못함)
- 케이블 분리 후 기기 행이 유지되고 DeX를 시작할 수 있는가 — **확인됨**(2026-10-31, 호스트명
  `z-fold8:5555` 무선 transport로 DeX 시작·Stop DeX·overlay 회수)
- Disconnect 뒤 저장 모드가 USB로 돌아가는가
- IP를 잘못 입력하면 실패 메시지가 뜨고 버튼이 복귀하는가
- 동작 중 Save 버튼이 비활성이고 끝나면 다시 활성화되는가
- [ ] 포트 입력란을 비웠을 때 Port 값과 연결 버튼 상태(NumericUpDown null 바인딩 미확인)

알려진 한계:

- Android 11+ 6자리 페어링은 지원하지 않는다. Core `Pair(host, port, code)`는 기기 프로필을
  건드리지 않는다(Windows 앱은 기기를 인식하는 자체 `Pair` 사본을 가진다).
- 휴대폰을 재부팅하면 `adb tcpip` 모드가 풀린다. 무선 전환은 다시 USB로 해야 한다.
- 동작 중 Save를 막는 이유: `WirelessAdbService`는 `SettingsService.UpdateAndSave`(잠금
  `_saveSync`)로 저장하고, 설정 페이지는 `ApplicationHost.UpdateSettings`(잠금
  `_settingsLock`)로 저장한다. 같은 살아 있는 `AppSettings`에 대해 잠금이 둘이라 서로를
  배제하지 못하므로 `SettingsViewModel.IsWirelessIdle`이 false인 동안 Save를 비활성화한다.
  다만 이 게이트는 창을 좁힐 뿐 닫지는 못한다(페이지 교체나 설정 창을 닫았다 다시 열면
  Core 호출이 아직 도는 중에도 게이트가 풀린다). 근본 해결은 `TODO.md`에 기록한 설정 저장
  잠금 통합이다.
- TUI [5]-[1]의 `EnableFromUsb(device.Identity, 5555)` 호출(`InteractiveHost.cs:537`)은
  identity를 호스트 자리에 넘긴다. 이 브랜치에서는 고치지 않았다(`TODO.md` 참조).

## 터미널 앱 신호 종료 실기 검증

PR #9는 `DexManager.Mac`에 `PosixSignalRegistration`(SIGTERM/SIGINT/SIGHUP)을 넣어
`overlay_display_devices` 누수를 막았다. 리뷰 단계에서는 실제 pty로 종료 시간까지
측정했으나 기기가 연결돼 있지 않아 **"overlay가 실제로 회수되는가"는 미검증**이었다.

2026-09-10 SM-F971N(One UI 9.0, Android 17)에서 확인했다. 조건은 `--dex` 모드로
DeX를 띄운 뒤 `yes > /dev/null` 4개로 CPU 부하를 건 상태다 — 정리 사슬이 한가할 때보다
오래 걸리게 만들어, 예산이 짧으면 잘리도록 한 것이다.

| 신호 | 종료까지 | `overlay_display_devices` | scrcpy |
| :--- | ---: | :--- | :--- |
| Ctrl+C (tmux pty, `send-keys C-c`) | 0.33초 | `null` 회수 | 정리됨 |
| SIGINT (`kill -INT`) | 0.34초 | `null` 회수 | 정리됨 |
| SIGTERM (`kill -TERM`) | 0.22초 | `null` 회수 | 정리됨 |
| SIGHUP (`kill -HUP`) | 0.22초 | `null` 회수 | 정리됨 |

Ctrl+C와 SIGINT는 앱이 `DeX session stopped and display overlay cleaned up`을 출력한
뒤 종료했다. 15초 예산은 상한일 뿐 지연이 아니라는 점도 함께 확인된다 — 네 경우 모두
0.4초 안에 끝났다.

**계측 함정 기록.** 첫 시도에서 SIGINT만 21.79초가 걸리고 overlay가 남아 결함으로
보였으나, 이는 앱이 아니라 검증 장치의 문제였다. tmux 서버가 오래전에 기동해
`DOTNET_ROOT`가 없는 환경을 물려주었고, 그래서 scrcpy에 넘긴 `DXMAdbProxy`(프레임워크
의존 .NET 앱)가 실행되지 못했다. 정리 경로가 adb를 호출할 수 없으니 overlay를 회수할
방법이 없었던 것이다. tmux 세션에 환경을 명시적으로 주입한 뒤 같은 신호가 0.34초에
깨끗하게 끝났다. 신호 검증 하네스는 **앱이 adb를 실제로 실행할 수 있는 환경**에서
돌려야 하며, 그렇지 않으면 정리 실패를 앱 결함으로 오독하게 된다. 이 프록시 실행 실패가
사용자에게 어떻게 보이는지는 `docs/TODO.md`에 별도 항목으로 남겼다.

## 개발용 Scrcpy 번들 아키텍처

저장소의 `tools/scrcpy`는 Apple Silicon용 Scrcpy 4.1이며 macOS 개발
빌드에만 복사된다. Intel Mac에서는 이 실행 파일이 기동하지 않으므로 설정에서
시스템에 설치한 Scrcpy 경로를 지정한다. 배포 패키지는 아키텍처별 공식
Scrcpy를 따로 내려받으므로 영향을 받지 않는다.

`tools/scrcpy`를 직접 교체할 때는 파일 수정 시각을 확인한다. 새 파일의 수정
시각이 기존 빌드 산출물보다 과거이면 `CopyToOutputDirectory=PreserveNewest`가
복사를 건너뛰어 이전 실행 파일이 그대로 남는다. 교체 뒤
`touch tools/scrcpy/*` 또는 `dotnet clean`으로 산출물을 갱신한다.

## 테스트: fake-adb 프로브의 CPU 경합 flake

실제 `ApplicationHost`를 생성하는 테스트는 시작·종료 과정에서 fake-adb 셸
스크립트(`FakeAdbExecutable`)를 실제 서브프로세스로 띄운다. 이 flake는 두
증상 중 하나로 나타난다:

1. `PathServiceCandidateRetryTests.SelectAdbPath_PreferredCandidateUnavailable_LogsWhichReplacementWasUsed`가
   예상한 fake adb 경로 대신 실제 시스템 adb(예: `/opt/homebrew/bin/adb`)를
   assert해 실패한다.
2. 테스트 `finally`의 `ApplicationHost.Dispose()` overlay 회수 경로에서
   `AggregateException: ... Could not remove the existing virtual display`가
   터진다.

영향받는 클래스: `DexOrchestratorWakeTests`, `DexOrchestratorShutdownTests`,
`DexOrchestratorStartLockGateTests`, `DexOrchestratorDismissKeyguardPollTests`,
`PathServiceCandidateRetryTests`, `AdbServiceLockStateTests`,
`ApplicationHostTests` — 즉 `FakeAdbExecutable`을 실제로 띄우는 모든 테스트.

**메커니즘.** `PathService`의 `adb version` 프로브는 5초 타임아웃에 2회
재시도(300ms 간격, `TransientProbeRetry`, 타임아웃일 때만 재시도)를 두는데,
CPU 경합 아래서는 이 사소한 셸 스크립트조차 스케줄링이 밀려 두 시도 모두
타임아웃할 수 있다. 그러면 그 세션 동안 실제 시스템 adb로 fail-open하고,
이후 fake serial을 넘기는 모든 호출이 "device not found"로 실패한다. 전체
진단은 `.omc/research/settle-poll-report.md`, 재현·측정 근거는
`.omc/research/flake-fix-report.md` 참조.

**트리거 조건(정확히 표현할 것).** `dotnet test` 자체가 아니라 **외부 CPU
경합**(동시에 도는 빌드, 다른 테스트 실행, 리소스를 공유하는 CI 러너)이
조건이다. 유휴 머신에서 `dotnet test DexManager.Mac.sln`을 10회 반복하면
**0/10** — 전혀 재현되지 않는다. 같은 커밋에서 4개의 `dotnet test` 프로세스를
동시에 돌리는 경합 아래서는 **3/12(25%)** 실패, 실패는 모두 위 증상 1번과
같은 신호(fail-open된 실제 adb 경로)였다. 즉 이 flake는 "합성 스트레스에서만"
일어나는 게 아니라 실제 CPU 경합 상황(빌드와 테스트가 겹치는 로컬 개발, 여러
잡이 리소스를 공유하는 CI 러너)에서도 재현되는 문제이며, 4× 병렬 스트레스는
그 조건을 증폭해 관측한 것뿐이다.

**증상을 보면 할 일.** 조용한(유휴) 머신에서 다시 돌린다. 경합 없이도
재현되면 이건 다른 버그이니 별도로 조사한다. 프로덕션 코드에 재시도나
타임아웃을 더 추가해서 "고치려" 하지 말 것 — 현재 재시도 정책(2회, 타임아웃일
때만)과 fail-open 시맨틱은 의도된 설계이고 리뷰를 거쳤다(PR #5). 잔존
실패율은 이미 문서화된 한계다.

**왜 고치지 않았는가.** 서브프로세스를 띄우는 22개 테스트를 하나의 xUnit
컬렉션으로 묶어 직렬화하거나, `ApplicationHost`의 하드코딩된
`AdbSelectionTimeoutMs`(5000ms, `private const`)를 테스트에서만 늘리도록
프로덕션에 seam을 추가하는 방법을 모두 검토했다. 두 방법 다 유휴 머신 기준
0/10인 조건을 고치는 데 비해 비용(직렬화는 실행 시간 증가, seam 추가는
프로덕션 코드 변경)이 더 크다고 판단해 보류했다.
