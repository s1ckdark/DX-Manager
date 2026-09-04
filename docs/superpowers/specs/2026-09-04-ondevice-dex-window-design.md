# 온디바이스 DeX 창 (DX DeX Window) 설계

- 작성일: 2026-09-04
- 개정: 2026-09-04 — 접근안을 Shizuku 경유 Surface 전달에서 scrcpy 자체 기동으로 선회
- 상태: 설계 승인됨, Phase 0 재작성 대기
- 대상: `DXDexWindow` (신규 안드로이드 앱)

## 1. 배경과 목표

DX Manager는 PC에서 scrcpy로 휴대폰의 가상 디스플레이를 미러링해 DeX 데스크톱을 제공한다. 이 설계의 목표는 **PC 없이 휴대폰 자체에서** 같은 DeX 데스크톱을 띄우고 조작하는 독립 안드로이드 앱을 만드는 것이다.

사용자는 휴대폰 화면 위에 뜨는 플로팅 창에서 DeX 데스크톱을 보고 터치·문자로 조작한다. 대화면 폴더블에서 특히 유용하다.

요구된 입력 범위는 터치·문자·클립보드다. 세 가지 모두 scrcpy의 control 프로토콜이 이미 제공한다(4.3절).

### 지원 대상

**Samsung Galaxy 기기(One UI) 전용이다.** 다른 제조사는 지원 대상이 아니다.

근거는 2.3절이다. 이 설계는 삼성이 보조 디스플레이에 자동으로 붙이는 `com.sec.android.app.launcher/com.honeyspace.dexservice.SecondaryLauncher`를 데스크톱 셸로 사용한다. 다른 제조사에는 이에 상응하는 셸이 없으므로 앱이 런처·작업표시줄·창 관리를 직접 구현해야 하고, 그것은 이 프로젝트의 몇 배 규모다.

비삼성 기기에서 우연히 동작할 수는 있으나 검증하지 않으며, 런처가 없는 환경을 위한 자체 데스크톱 셸은 만들지 않는다. 앱은 시작 시 제조사를 확인해 비지원 안내를 표시한다.

### 비목표

- 비삼성 기기 지원 및 자체 데스크톱 셸 구현
- DX Manager(PC)와의 연동 — 이 앱은 완전히 독립적으로 동작한다
- 기존 DX Companion(`DXDisplayCleanup`)의 수정 — 그 앱의 안전 경계를 그대로 유지한다
- **Shizuku 의존** — 개정으로 제거되었다(3.2절)
- 여러 개의 동시 DeX 창
- 화면 녹화·캡처
- Play Store 배포 (7절 참조)

## 2. 타당성 조사 결과

### 2.1 문서로 확정된 권한 제약

일반 앱 권한만으로는 불가능하다. AOSP 문서 기준:

| 동작 | 일반 앱 | 근거 |
| :--- | :--- | :--- |
| 자기 `VirtualDisplay` 생성·렌더링 | 가능 | — |
| `VIRTUAL_DISPLAY_FLAG_TRUSTED` 생성 | 불가 | `ADD_TRUSTED_DISPLAY` 없으면 `SecurityException` |
| 다른 앱을 그 디스플레이에 실행 | 불가 | Android 10부터 자기 액티비티만 허용 |
| 보조 디스플레이에 IME 표시 | 불가 | `TRUSTED` + `SHOULD_SHOW_SYSTEM_DECORATIONS` 필요 |

**결론**: shell uid(2000) 권한이 필요하다. scrcpy가 동작하는 이유도 동일하다 — `adb shell`이 shell uid로 실행되기 때문이다.

### 2.2 실기 검증: shell 권한이면 필요한 동작이 전부 된다 (2026-09-03)

기기: Galaxy Z Fold (SM-F971N), Android 17 (SDK 37), One UI 9.0

| 항목 | 결과 |
| :--- | :--- |
| `am start --display 42 -n com.android.settings/.Settings` | ✅ Display #42에 실제로 배치됨 |
| `input -d 42 keyevent KEYCODE_BACK` | ✅ 해당 디스플레이에만 전달, Display #0 무영향 |
| `input -d 42 tap 800 450` | ✅ 오류 없이 수용 |

BACK 키 검증이 결정적이다. 입력 전 Display #42의 최상위는 `com.android.settings/.SubSettings`였고, 입력 후 `com.android.chrome/...Main`으로 바뀌었다. Display #0은 변화가 없었다.

**One UI 9.0은 shell 수준의 디스플레이별 앱 실행과 입력 주입을 제한하지 않는다.**

### 2.3 삼성 보조 런처가 자동으로 붙는다 — Phase 0에서 확정

Phase 0 Task 2에서 실기로 확정했다. `scrcpy --new-display`로 만든 shell 소유 가상 디스플레이(id=44)에 다음이 자동으로 실행되었다.

```
Display #44 (activities from top to bottom):
      topResumedActivity=...com.sec.android.app.launcher/com.honeyspace.dexservice.SecondaryLauncher
Display #0 (activities from top to bottom):
      topResumedActivity=...com.sec.android.app.launcher/.activities.LauncherActivity
```

그 디스플레이는 `FLAG_TRUSTED`와 `FLAG_SHOULD_SHOW_SYSTEM_DECORATIONS`를 모두 가졌으며 소유자는 `com.android.shell`(uid 2000)이었다.

**앱 목록·작업표시줄·창 관리를 포함한 데스크톱 셸을 삼성이 제공한다.** 이 앱은 데스크톱 환경을 직접 구현하지 않는다.

### 2.4 잠금 상태에서는 보조 디스플레이가 동작하지 않는다

화면이 꺼져 잠긴 상태에서는 가상 디스플레이가 생성되어도 홈이 시작되지 않고 곧 철거된다.

```
V/ShellDesktopMode:   handlePotentialReconnect: Keyguard is locked; aborting.
V/WindowManagerShell: handlePotentialReconnect: Keyguard is locked; aborting.
```

앱은 이 조건을 사용자에게 안내해야 한다.

### 2.5 보조 디스플레이에 IME가 뜨지 않는다 — Phase 0에서 확정

Phase 0 Task 3에서 실기로 확정했다. 보조 디스플레이(id 46)의 텍스트 입력란을 탭했을 때, 삼성 키보드는 **기본 화면(display 0)** 에 결합되었다.

```
mCurDisplayId=0, mSelectedDisplayId=0          ← IME가 결합된 디스플레이
mCurClient=ClientState{... mSelfReportedDisplayId=46}   ← 텍스트 입력란의 디스플레이
mInputShown=true
```

`mInputShown=true`는 키보드가 떴다는 뜻일 뿐 **어느 디스플레이에** 떴는지를 말하지 않는다. 보조 디스플레이 스크린샷으로 키보드 부재를 시각 확인했다.

**중요한 파생 발견**: 2.3절에서 그 디스플레이가 `FLAG_TRUSTED`와 `FLAG_SHOULD_SHOW_SYSTEM_DECORATIONS`를 모두 가진 것을 확인했음에도 IME가 뜨지 않았다. 즉 AOSP 문서가 말하는 두 플래그 조건은 One UI 9.0에서 **필요조건이지만 충분조건이 아니다.**

이 제약은 4.3절의 `INJECT_TEXT` 경로로 우회한다. 그 경로는 IME를 경유하지 않는다.

### 2.6 기기가 자기 adb 데몬에 접속할 수 있다 (2026-09-04)

무선 디버깅이 켜진 상태에서 기기 내부에서 자기 adb 데몬 포트로 접속이 성공했다.

```
$ adb -s R5***TP shell "timeout 3 toybox nc localhost 34493 </dev/null; echo connect_exit=$?"
connect_exit=0
```

**한계**: 이 테스트는 `adb shell`을 통해 실행되어 **shell uid**로 동작했다. 일반 앱 uid에서도 가능한지는 별도 확인이 필요하다. `INTERNET` 권한으로 localhost TCP 소켓을 여는 통상적인 경우이므로 가능성은 높으나 미검증이다. Phase 0의 검증 대상이다(5절).

### 2.7 접근안 선택: scrcpy 자체 기동

두 가지 방식을 검토했다.

| | Shizuku 경유 | **scrcpy 자체 기동 (채택)** |
| :--- | :--- | :--- |
| 사용자가 설치할 앱 | 2개 | **1개** |
| 앱이 구현할 것 | Binder 호출 | **ADB 클라이언트 전체** |
| 디스플레이 화면 전달 | Surface 직접 (인코딩 없음) | H.264 소켓 스트림 |
| 입력·클립보드 | 직접 구현 | **scrcpy 프로토콜 재사용** |
| 재부팅 후 | Shizuku 재시작 필요 | ADB 키 보존 가능성 (미검증) |

Shizuku는 두 가지 역할을 한다. (a) shell uid 프로세스를 띄우고 유지하는 것, (b) 앱이 그 프로세스에 `Surface` 같은 객체를 건네게 하는 Binder 다리. scrcpy 방식은 (a)만 대체하고 (b)는 대체하지 못하므로, 화면은 Surface 직접 연결이 아니라 H.264 스트림으로 받는다.

**채택 근거**: 사용자가 앱 하나만 설치하면 되고, 입력·문자·클립보드를 scrcpy 프로토콜이 이미 제공하며(4.3절), 특히 `INJECT_TEXT`가 2.5절의 IME 제약을 우회한다. 대가는 앱 안에 ADB 클라이언트를 구현하는 부담과 같은 기기 내 인코딩 왕복이다.

참고:
- [Activity launch policy — AOSP](https://source.android.com/docs/core/display/multi_display/activity-launch)
- [VirtualDisplay — Android Developers](https://developer.android.com/reference/android/hardware/display/VirtualDisplay)
- [scrcpy](https://github.com/Genymobile/scrcpy) (Apache-2.0)

## 3. 아키텍처

### 3.1 프로젝트 배치

기존 Companion과 나란히 두는 독립 Gradle 프로젝트로 만든다.

```
DXDisplayCleanup/          기존 DX Companion — 무변경
DXDexWindow/               신규
  app/
  build.gradle
  settings.gradle
```

| 항목 | 값 | 근거 |
| :--- | :--- | :--- |
| `applicationId` | `io.github.mazemei.dxdexwindow` | Companion의 `io.github.mazemei.*` 규칙 |
| `minSdk` | 30 (Android 11) | 무선 디버깅이 Android 11+ 기능 |
| `targetSdk` / `compileSdk` | 36 | Companion과 동일 |
| 언어 | Java | 저장소의 유일한 안드로이드 앱이 Java |
| Gradle / AGP | wrapper 8.14.5 / AGP 8.13.2 | `DXDisplayCleanup`에서 검증된 조합 |
| 신규 의존성 | ADB 클라이언트 라이브러리, `scrcpy-server.jar` 번들 | 이 저장소 안드로이드 코드의 첫 서드파티 의존성 |

### 3.2 별도 앱으로 만드는 이유

DX Companion의 README는 다음 안전 경계를 선언한다.

> `WRITE_SECURE_SETTINGS`를 문서화된 두 복구 설정에만 사용한다.
> 셸을 제공하거나 임의 명령을 실행하지 않으며, 클라우드에 접속하거나 데이터를 수집하지 않는다.

이 앱은 자기 기기의 adb 데몬에 접속해 shell 명령을 실행한다. 위 선언과 정면으로 충돌하며, DX Manager가 패키지명과 서명 인증서를 검증한 뒤 권한을 부여하는 신뢰 모델과도 맞지 않는다.

따라서 두 앱을 분리하고 **서명 키도 분리한다.**

### 3.3 런타임 데이터 흐름

```
FloatingWindowService (foreground, TYPE_APPLICATION_OVERLAY)
  ├ DexSurfaceView ←── MediaCodec 디코딩 ←── video 소켓
  └ 컨트롤 바 ──── 입력·클립보드 ────────→ control 소켓
                                              ↑
                                    AdbClient (앱 내장)
                            localhost:<무선디버깅 포트> → adbd
                                              ↓
                              app_process → scrcpy-server (shell uid)
                                    ├ --new-display=WxH/dpi 로 가상 디스플레이 생성
                                    ├ 삼성 SecondaryLauncher 자동 부착 (2.3절)
                                    ├ 화면 캡처 → H.264 인코딩 → video 소켓
                                    └ control 소켓 ← 입력·클립보드 메시지
```

앱은 scrcpy 클라이언트의 온디바이스 판이다. PC의 scrcpy가 하던 일을 앱이 하되, 전송 경로가 USB/네트워크 대신 localhost다.

### 3.4 컴포넌트 책임

| 컴포넌트 | 책임 | 의존 |
| :--- | :--- | :--- |
| `AdbClient` | 페어링, RSA 인증, shell 명령 실행, 소켓 포워딩 | ADB 라이브러리 |
| `ScrcpyServerController` | `scrcpy-server.jar` 푸시, `app_process` 기동, 소켓 연결, 수명주기 | `AdbClient` |
| `VideoPipeline` | H.264 스트림 → `MediaCodec` → `Surface` 렌더 | `ScrcpyServerController` |
| `ControlChannel` | scrcpy control 메시지 인코딩·전송 | `ScrcpyServerController` |
| `FloatingWindowService` | 플로팅 창, 포그라운드 알림, 세션 유지 | — |
| `DexSurfaceView` | 디코딩 출력 표시, 터치 수집 | — |
| `OnboardingState` | 무선 디버깅·페어링·연결 상태 판정과 안내 | `AdbClient` |

### 3.5 세션 정리

scrcpy-server는 shell 프로세스로 뜬다. 앱이 비정상 종료하면 서버가 남아 가상 디스플레이가 유지될 수 있다.

이 저장소는 같은 문제를 이미 겪었다. DX Companion이 존재하는 이유가 `overlay_display_devices` 잔여물 정리이며, DX Manager는 `--stop-dex`에 `cleanupUntrackedOverlay` 경로를 둔다.

따라서 다음을 설계에 포함한다.

- `FloatingWindowService` 종료 시 control 소켓으로 정상 종료 요청 후 프로세스 확인
- 앱 시작 시 남아 있는 `scrcpy-server` 프로세스를 찾아 정리
- 포그라운드 알림에 상시 "중지" 액션 노출

## 4. UI 및 입력 설계

### 4.1 플로팅 창

`TYPE_APPLICATION_OVERLAY` 창을 포그라운드 서비스가 소유한다. 구성은 얇은 컨트롤 바와 `SurfaceView`다.

컨트롤 바는 창 이동 핸들을 겸하며 뒤로·홈·최근 버튼, 문자 입력 상자, 중지 버튼을 포함한다.

2.3절에 따라 앱 실행은 삼성 보조 런처가 담당하므로 앱 실행 UI는 만들지 않는다.

### 4.2 창 크기와 해상도

**창 크기가 곧 디스플레이 해상도다.** 다만 접근안 선회로 `VirtualDisplay.resize()`를 쓸 수 없게 되었다 — 디스플레이를 만드는 주체가 앱이 아니라 scrcpy-server이기 때문이다.

따라서 리사이즈는 **scrcpy-server 재기동**으로 처리한다.

- **리사이즈가 끝난 시점에만** 적용한다(디바운스). 드래그 중에는 스케일링으로 보여준다.
- **창 이동은 리사이즈가 아니므로** 아무 동작도 하지 않는다.
- 재기동 시 디스플레이가 새로 만들어지므로 그 위의 앱이 재배치·재시작될 수 있다. 이 비용을 사용자에게 알린다.

scrcpy가 실행 중인 새 디스플레이의 크기를 바꾸는 control 메시지를 제공하는지는 **미검증**이다. 제공한다면 재기동보다 그쪽이 낫다(8절).

### 4.3 입력·문자·클립보드 — scrcpy 프로토콜 재사용

직접 구현하지 않는다. scrcpy의 control 프로토콜이 이미 제공한다.

| 필요한 것 | scrcpy control 메시지 |
| :--- | :--- |
| 터치·드래그 | `INJECT_TOUCH_EVENT` |
| 스크롤 | `INJECT_SCROLL_EVENT` |
| 뒤로·홈·최근 | `INJECT_KEYCODE` |
| **문자 입력** | **`INJECT_TEXT`** |
| 클립보드 | `SET_CLIPBOARD` / `GET_CLIPBOARD` |

`INJECT_TEXT`가 2.5절의 IME 제약을 우회한다. IME를 경유하지 않고 텍스트를 직접 주입하므로, 보조 디스플레이에 키보드가 뜨지 않는 것이 문제가 되지 않는다. DX Manager가 PC에서 `--prefer-text`로 쓰는 경로와 같다.

좌표는 `SurfaceView` 뷰 좌표에서 디스플레이 좌표로 변환해 전달한다.

### 4.4 온보딩

앱은 네 가지 상태를 구분해 안내한다.

| 상태 | 안내 |
| :--- | :--- |
| 무선 디버깅 꺼짐 | 개발자 옵션에서 켜는 절차 |
| 미페어링 | 페어링 코드 입력 화면 |
| 연결 실패 | 재연결 또는 재페어링 |
| 준비됨 | DeX 창 시작 |

2.4절에 따라 화면이 잠긴 상태에서는 동작하지 않음을 안내한다.

## 5. 검증 전략

### 5.1 Phase 0 검증 현황

| 항목 | 상태 |
| :--- | :--- |
| shell 권한의 디스플레이별 앱 실행·입력 주입 | ✅ 검증 (2.2절) |
| 삼성 보조 런처 자동 부착 | ✅ 검증 (2.3절, Phase 0 Task 2) |
| 보조 디스플레이 IME | ✅ 검증 — 뜨지 않음 (2.5절, Phase 0 Task 3). `INJECT_TEXT`로 우회 |
| 기기가 자기 adb 데몬에 접속 (shell uid) | ✅ 검증 (2.6절) |
| **일반 앱 uid에서 adb 데몬 접속** | ❌ 미검증 |
| **앱 안에서 ADB 페어링·RSA 인증 구현** | ❌ 미검증 — 최대 위험 |
| **앱이 scrcpy-server를 기동하고 소켓으로 프레임 수신** | ❌ 미검증 |
| **재부팅 후 ADB 키 보존과 자동 재연결** | ❌ 미검증 |

### 5.2 Phase 0 재작성 범위

접근안 선회로 기존 Phase 0의 Task 4~6(Shizuku 경유 Surface 전달)은 무효가 되었다. Task 1~3의 결과는 접근안과 무관하게 유효하므로 보존한다.

새 Phase 0은 5.1절의 미검증 4건 중 앞의 셋을 확인한다. 여기서 막히면 접근안을 다시 검토해야 하며, 그 판단을 앱 구현이 끝난 뒤에 내리면 늦다.

### 5.3 자동 테스트

좌표 변환, 온보딩 상태 머신, control 메시지 인코딩을 단위 테스트로 검증한다. `AdbClient`는 인터페이스 뒤에 두어 fake로 대체 가능하게 한다.

### 5.4 실기 검증

실제 기기에서의 표시·조작·정리는 사용자 확인 항목이다. `AGENTS.md` 원칙에 따라 대신 성공했다고 가정하지 않고 미확인으로 명시한다.

## 6. 전달 단계

| Phase | 내용 | 완료 조건 |
| :--- | :--- | :--- |
| 0 | 기술 스파이크 (버리는 코드) | 앱 uid에서 adbd 접속, ADB 인증, scrcpy-server 기동과 프레임 수신 확인 |
| 1 | 앱 뼈대 + 전체화면으로 DeX 화면 표시 | 화면이 보임 |
| 2 | control 채널 — 터치·키·문자 | 조작 가능 |
| 3 | 플로팅 창 + 리사이즈 | 실사용 가능 |
| 4 | 온보딩 상태 머신 + 세션 정리 | 재부팅·크래시 후 잔여물 없음 |
| 5 | 클립보드, 배포 준비 | |

Phase 3 종료 시점부터 실사용이 가능하다.

Phase 1~2를 전체화면으로 먼저 만드는 것은 의도적이다. 플로팅 창은 좌표계·포커스·터치 가로채기가 얽혀 있어, "화면이 뜨지 않는다"와 동시에 디버깅하면 원인 분리가 불가능해진다.

## 7. 배포

- Play Store는 앱 내 ADB 클라이언트에 정책 리스크가 있어 v1 목표에서 제외한다.
- Companion과 마찬가지로 APK 직배포로 시작한다.
- **서명 키는 Companion과 분리한다.**
- 서명 관련 파일(`signing.properties`, 키스토어, 비밀번호)은 커밋하지 않는다. Companion의 `SIGNING.md` 규칙을 따른다.
- `scrcpy-server.jar`를 번들하므로 scrcpy의 Apache-2.0 라이선스 고지를 포함한다. 번들 버전을 고정하고 SHA-256으로 검증한다. `scripts/Package-Mac-Release.sh`가 같은 방식을 쓴다.

## 8. 열린 항목

- **앱 안의 ADB 페어링·인증 구현 난이도가 미검증이다.** Phase 0의 핵심 검증 대상이다. 실패 시 Shizuku 의존으로 되돌아가야 한다.
- **일반 앱 uid에서 localhost adbd 접속 가능 여부 미검증** (2.6절의 한계).
- **재부팅 후 재연결 동작 미검증.** ADB 키가 보존되어 자동 재연결된다면 Shizuku 대비 명확한 UX 우위이나, 무선 디버깅 자체가 재부팅 시 꺼지는 기기가 많다.
- **실행 중인 새 디스플레이의 크기 변경 수단 미검증.** scrcpy control 프로토콜에 해당 메시지가 있으면 4.2절의 재기동을 피할 수 있다.
- **같은 기기 내 H.264 인코딩→디코딩의 실제 비용 미측정.** 지연·발열·배터리 영향을 Phase 1에서 측정한다.
- **삼성 내 기기·One UI 버전별 차이 미검증.** 검증은 Galaxy Z Fold(One UI 9.0) 한 대에서만 이루어졌다. 특히 폴더블이 아닌 기기와 One UI 8.x 이하는 확인이 필요하다.
