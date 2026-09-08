# Known Issues and Constraints

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

## macOS 포터블 서명과 실기 범위

macOS arm64/x64 ZIP은 self-contained이므로 Homebrew와 별도 .NET 설치에
의존하지 않는다. 다만 현재 자동화 설정에는 Apple Developer ID 인증서와
notarization 자격 증명이 구성되어 있지 않다. 브라우저로 받은 ZIP은 최초 실행
시 Gatekeeper 승인이 필요할 수 있으며, 서명·공증 전에는 모든 macOS
버전에서 경고 없는 최초 실행을 보장하지 않는다. 패키징 스크립트가 quarantine
속성을 자동 삭제하거나 macOS 보안 기능을 우회하지 않는다.

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

   그러나 해제 자체는 다른 얘기다: `adb shell input keyevent 224`
   (`KEYCODE_WAKEUP`)만으로 신뢰할 수 있는/자격증명이 필요 없는 상태의 폰은 키가드가
   스스로 해제됐다 — `wm dismiss-keyguard`만으로는 폰이 잠들어 있는 동안(`INTERACTIVE_
   STATE_SLEEP`) 아무 효과가 없었고, 깨우는 것이 선행 조건이었다. DX Manager는 이제 DeX
   시작 시 이 wake를 자동으로 보낸다(`AdbService.WakeScreen`,
   `DexOrchestrator.StartCore`) — 아래 UI-2 잠금 게이트가 "깨운 뒤"의 상태를 판단하도록
   그 앞에 배치했다. 실패해도(`WakeScreen`이 예외를 삼키고 false를 반환) 시작 자체는
   막지 않는다.

   **한계**: `dumpsys trust`의 `deviceLocked=1`처럼 PIN/패턴이 실제로 요구되는
   상태에서는 깨워도 자격증명 화면이 여전히 디스플레이 0에만 뜬다 — DeX 미러(가상
   디스플레이)는 그 화면을 절대 보여줄 수 없다. 그 경우 PC에서 조작하려면
   `--display-id 0`로 여는 별도의 두 번째 scrcpy로 화면을 보면서 입력하거나,
   `input text <PIN>`을 화면 없이 그대로 보내는 수밖에 없다. 이번 Phase는 이 경로를
   앱에 통합하지 않았다 — 자동 wake는 어디까지나 "잠금이 없거나 신뢰 에이전트로 이미
   풀린" 가장 흔한 경우를 위한 것이다.

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

## 개발용 Scrcpy 번들 아키텍처

저장소의 `tools/scrcpy`는 Apple Silicon용 Scrcpy 4.1이며 macOS 개발
빌드에만 복사된다. Intel Mac에서는 이 실행 파일이 기동하지 않으므로 설정에서
시스템에 설치한 Scrcpy 경로를 지정한다. 배포 패키지는 아키텍처별 공식
Scrcpy를 따로 내려받으므로 영향을 받지 않는다.

`tools/scrcpy`를 직접 교체할 때는 파일 수정 시각을 확인한다. 새 파일의 수정
시각이 기존 빌드 산출물보다 과거이면 `CopyToOutputDirectory=PreserveNewest`가
복사를 건너뛰어 이전 실행 파일이 그대로 남는다. 교체 뒤
`touch tools/scrcpy/*` 또는 `dotnet clean`으로 산출물을 갱신한다.
