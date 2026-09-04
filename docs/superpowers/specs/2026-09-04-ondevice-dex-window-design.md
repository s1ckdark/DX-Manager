# 온디바이스 DeX 창 (DX DeX Window) 설계

- 작성일: 2026-09-04
- 개정: 2026-09-04 — 접근안을 Shizuku 경유 Surface 전달에서 scrcpy 자체 기동으로 선회
- 재개정: 2026-09-04 — Phase 0 검증 실패로 **Shizuku 경로로 복귀**. 근거는 5.5절
- 확정: 2026-09-04 — 선행 사례 조사로 **Surface 직접 연결** 채택. 근거는 2.8절
- 상태: 설계 확정, Phase 1 착수 가능
- 대상: `DXDexWindow` (신규 안드로이드 앱)

## 1. 배경과 목표

DX Manager는 PC에서 scrcpy로 휴대폰의 가상 디스플레이를 미러링해 DeX 데스크톱을 제공한다. 이 설계의 목표는 **PC 없이 휴대폰 자체에서** 같은 DeX 데스크톱을 띄우고 조작하는 독립 안드로이드 앱을 만드는 것이다.

사용자는 휴대폰 화면 위에 뜨는 플로팅 창에서 DeX 데스크톱을 보고 터치·문자로 조작한다. 대화면 폴더블에서 특히 유용하다.

요구된 입력 범위는 터치·문자·클립보드다. 세 가지 모두 Shizuku가 열어주는 시스템 서비스로 직접 처리한다(4.3절).

### 지원 대상

**Samsung Galaxy 기기(One UI) 전용이다.** 다른 제조사는 지원 대상이 아니다.

근거는 2.3절이다. 이 설계는 삼성이 보조 디스플레이에 자동으로 붙이는 `com.sec.android.app.launcher/com.honeyspace.dexservice.SecondaryLauncher`를 데스크톱 셸로 사용한다. 다른 제조사에는 이에 상응하는 셸이 없으므로 앱이 런처·작업표시줄·창 관리를 직접 구현해야 하고, 그것은 이 프로젝트의 몇 배 규모다.

비삼성 기기에서 우연히 동작할 수는 있으나 검증하지 않으며, 런처가 없는 환경을 위한 자체 데스크톱 셸은 만들지 않는다. 앱은 시작 시 제조사를 확인해 비지원 안내를 표시한다.

### 비목표

- 비삼성 기기 지원 및 자체 데스크톱 셸 구현
- DX Manager(PC)와의 연동 — 이 앱은 완전히 독립적으로 동작한다
- 기존 DX Companion(`DXDisplayCleanup`)의 수정 — 그 앱의 안전 경계를 그대로 유지한다
- ~~**Shizuku 의존** — 개정으로 제거되었다(3.2절)~~ — **재개정으로 복원.** 앱 내장 ADB 클라이언트 경로가 Phase 0에서 막혔다(5.5절)
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

이 제약은 `IWindowManager.setDisplayImePolicy`로 정면 해결을 시도한다(4.3절). 실패 시 대안도 같은 절에 있다.

### 2.6 기기가 자기 adb 데몬에 접속할 수 있다 (2026-09-04)

무선 디버깅이 켜진 상태에서 기기 내부에서 자기 adb 데몬 포트로 접속이 성공했다.

```
$ adb -s R5***TP shell "timeout 3 toybox nc localhost 34493 </dev/null; echo connect_exit=$?"
connect_exit=0
```

**한계**: 이 테스트는 `adb shell`을 통해 실행되어 **shell uid**로 동작했다. 일반 앱 uid에서도 가능한지는 별도 확인이 필요하다. `INTERNET` 권한으로 localhost TCP 소켓을 여는 통상적인 경우이므로 가능성은 높으나 미검증이다. Phase 0의 검증 대상이다(5절).

### 2.7 접근안 선택: Shizuku + Surface 직접 연결

세 가지 방식을 검토했고, Phase 0 검증과 선행 사례 조사를 거쳐 최종 결정했다.

| | **Shizuku + Surface 직접 (채택)** | Shizuku + scrcpy-server | scrcpy 자체 기동 (폐기) |
| :--- | :--- | :--- | :--- |
| 사용자가 설치할 앱 | 2개 (본 앱 + Shizuku) | 2개 | 1개 |
| 앱이 구현할 것 | Binder 호출 | Binder + scrcpy 클라이언트 | ADB 클라이언트 전체 |
| 디스플레이 화면 전달 | **Surface 직접 — 인코딩 없음** | H.264 소켓 스트림 | H.264 소켓 스트림 |
| 입력·문자·클립보드 | `IInputManager` 직접 주입 | scrcpy 프로토콜 | scrcpy 프로토콜 |
| 상태 | **프로덕션 선행 사례 있음(2.8절)** | 실기 검증됨(2.2·2.3절) | **불가 — 5.5절** |

**채택 근거**: 같은 기기 안에서 H.264로 인코딩했다가 곧바로 디코딩하는 왕복이 사라진다. 가상 디스플레이가 앱의 `SurfaceView`에 직접 렌더링되므로 지연·발열·배터리 비용이 원리적으로 발생하지 않고, `VideoPipeline` 컴포넌트 자체가 불필요해진다. 2.8절에서 이 경로가 프로덕션 앱에서 동작함을 확인했다.

`INJECT_TEXT`로 IME 제약(2.5절)을 우회하려던 계획은 `setDisplayImePolicy`로 대체한다(4.3절).

참고:
- [Activity launch policy — AOSP](https://source.android.com/docs/core/display/multi_display/activity-launch)
- [VirtualDisplay — Android Developers](https://developer.android.com/reference/android/hardware/display/VirtualDisplay)
- [Shizuku](https://shizuku.rikka.app/) (API는 Apache-2.0)

### 2.8 선행 사례 조사 (2026-09-04)

같은 문제를 이미 푼 프로덕션 앱이 존재한다. 개발자 jqssun의 두 앱이 Play 스토어와 F-Droid에 배포 중이다.

| 앱 | 역할 | 라이선스 |
| :--- | :--- | :--- |
| [android-display-extend](https://github.com/jqssun/android-display-extend) | 물리·가상 디스플레이 관리, 앱 배치, 입력 라우팅 | GPLv3 |
| [android-display-mirror](https://github.com/jqssun/android-display-mirror) | 가상 디스플레이 생성 + 스트리밍 | GPLv3 |

**라이선스 경계**: 두 앱은 GPLv3, DX Manager는 MIT다. **코드는 일절 가져오지 않는다.** 조사 대상은 "어떤 플랫폼 API가 존재하고 어떤 인자를 받는가"라는 사실이며, 이는 저작권 보호 대상이 아니다. 아래 확인 내용은 모두 이 범위에 한정된다.

#### 확인된 사실

`Extend`의 `ManagedVirtualDisplayActivity`는 **우리 요구사항과 동일한 구조**를 구현한다. 앱 안의 `SurfaceView`에 가상 디스플레이를 렌더링한다.

경로는 다음과 같다.

1. `SurfaceView`의 `SurfaceHolder`에서 `Surface`를 얻는다.
2. 그 `Surface`를 `VirtualDisplayConfig.Builder(...).setSurface(surface)`에 넣는다.
3. Shizuku Binder 래퍼로 감싼 `IDisplayManager`에 `createVirtualDisplay(config, callback, projection, "com.android.shell")`을 호출한다.
4. 반환된 displayId로 `DisplayManagerGlobal.getInstance().createVirtualDisplayWrapper(...)`를 호출해 `VirtualDisplay` 핸들을 얻는다.

**이로써 "Surface를 Shizuku 경유로 전달할 수 있는가"라는 최우선 미검증 항목이 해소된다.** `Surface`는 `Parcelable`이고 Shizuku 브리지는 평범한 `IBinder` 래퍼이므로, 일반 Binder 트랜잭션 규칙이 그대로 적용된다.

#### 확보한 API 목록

| 용도 | API | 비고 |
| :--- | :--- | :--- |
| 시스템 서비스 접근 | `IDisplayManager.Stub.asInterface(new ShizukuBinderWrapper(SystemServiceHelper.getSystemService(Context.DISPLAY_SERVICE)))` | `IWindowManager`, `IInputManager`도 동일 패턴 |
| 디스플레이 생성 | `IDisplayManager.createVirtualDisplay(config, callback, projection, callingPackage)` | |
| 입력 주입 | `IInputManager.injectInputEvent(event, 0)` | scrcpy 프로토콜 불필요 |
| 디스플레이에 앱 실행 | `ActivityOptions.makeBasic().setLaunchDisplayId(N)` | `am start --display N` 불필요 |
| **보조 디스플레이 IME** | **`IWindowManager.setDisplayImePolicy(displayId, policy)`** | **2.5절 제약의 정식 해법 후보** |
| 해상도·밀도·회전 | `setForcedDisplaySize`, `setForcedDisplayDensityForUser`, `freezeDisplayRotation` | |

#### 가상 디스플레이 플래그 비트값

`DisplayManager`에 공개 상수가 없는 것들이다.

| 플래그 | 비트 | 용도 |
| :--- | :--- | :--- |
| `SUPPORTS_TOUCH` | `1 << 6` | 터치 입력 수용 |
| `ROTATES_WITH_CONTENT` | `1 << 7` | |
| `TRUSTED` | `1 << 10` | 시스템 데코레이션·런처 부착 |
| `OWN_DISPLAY_GROUP` | `1 << 11` | |
| `ALWAYS_UNLOCKED` | `1 << 12` | **2.4절 잠금 철거 문제의 해법 후보 — 미검증** |
| `TOUCH_FEEDBACK_DISABLED` | `1 << 13` | |
| `DEVICE_DISPLAY_GROUP` | `1 << 15` | API 34+ |

`TRUSTED` 또는 `OWN_DISPLAY_GROUP`을 쓰려면 **API 33 이상**이 필요하다.

#### 이 조사로 답이 나오지 **않은** 것

선행 사례는 외부 디스플레이가 주 대상이므로, 우리 시나리오에 고유한 다음 항목은 여전히 미검증이다.

- 이 경로로 만든 디스플레이에 **삼성 `SecondaryLauncher`가 자동 부착되는지** (2.3절 검증은 `overlay_display_devices` 경로였다)
- `ALWAYS_UNLOCKED`가 실제로 2.4절의 잠금 철거를 막는지
- `setDisplayImePolicy`로 보조 디스플레이에 IME가 실제로 뜨는지

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
| `minSdk` | **33 (Android 13)** | `VIRTUAL_DISPLAY_FLAG_TRUSTED` / `OWN_DISPLAY_GROUP`의 하한(2.8절) |
| `targetSdk` / `compileSdk` | 36 | Companion과 동일 |
| 언어 | Java | 저장소의 유일한 안드로이드 앱이 Java |
| Gradle / AGP | wrapper 8.14.5 / AGP 8.13.2 | `DXDisplayCleanup`에서 검증된 조합 |
| 신규 의존성 | `dev.rikka.shizuku:api`, `dev.rikka.shizuku:provider` | 이 저장소 안드로이드 코드의 첫 서드파티 의존성 |

hidden API(`IDisplayManager`, `VirtualDisplayConfig`, `DisplayManagerGlobal` 등)는 컴파일 전용 stub으로 해결한다. 런타임 호출은 Shizuku 프로세스 문맥에서 이루어지므로 앱 프로세스의 non-SDK 제한을 받지 않는다.

### 3.2 별도 앱으로 만드는 이유

DX Companion의 README는 다음 안전 경계를 선언한다.

> `WRITE_SECURE_SETTINGS`를 문서화된 두 복구 설정에만 사용한다.
> 셸을 제공하거나 임의 명령을 실행하지 않으며, 클라우드에 접속하거나 데이터를 수집하지 않는다.

이 앱은 Shizuku를 통해 shell uid 권한으로 임의 앱을 실행하고 입력을 주입한다. 위 선언과 정면으로 충돌하며, DX Manager가 패키지명과 서명 인증서를 검증한 뒤 권한을 부여하는 신뢰 모델과도 맞지 않는다.

따라서 두 앱을 분리하고 **서명 키도 분리한다.**

### 3.3 런타임 데이터 흐름

```
FloatingWindowService (foreground, TYPE_APPLICATION_OVERLAY)
  ├ DexSurfaceView
  │   └ SurfaceHolder.getSurface() ─┐
  │                                  │  (Parcelable, Binder 전달)
  └ 컨트롤 바                        │
        │                            ▼
        │      ShizukuGateway ── IDisplayManager.createVirtualDisplay(
        │            │                config.setSurface(surface), … )
        │            │                        │
        │            │                        ▼
        │            │            가상 디스플레이 (displayId = N)
        │            │              └ 삼성 SecondaryLauncher 자동 부착 (2.3절)
        │            │                        │
        │            │        시스템이 SurfaceView에 직접 합성 ──┘
        │            │           (H.264 인코딩·디코딩 없음)
        │            │
        └ 입력 ──────┴─→ IInputManager.injectInputEvent(event, 0)
```

핵심은 **화면 데이터가 앱을 거치지 않는다**는 점이다. 앱은 렌더 목적지(`Surface`)를 시스템에 한 번 건네줄 뿐이고, 이후 합성은 SurfaceFlinger가 처리한다. 앱이 하는 일은 창을 띄우고, 좌표를 변환해 입력을 주입하고, 수명주기를 관리하는 것뿐이다.

### 3.4 컴포넌트 책임

| 컴포넌트 | 책임 | 의존 |
| :--- | :--- | :--- |
| `ShizukuGateway` | Shizuku 바인딩·권한 요청, 시스템 서비스 프록시(`IDisplayManager`·`IWindowManager`·`IInputManager`) 획득 | Shizuku API |
| `VirtualDisplaySession` | 디스플레이 생성·해제, 플래그 조합, `resize()`, displayId 보유 | `ShizukuGateway` |
| `DisplayConfigurator` | 해상도·밀도·회전·IME 정책 적용 | `ShizukuGateway` |
| `InputInjector` | 뷰 좌표 → 디스플레이 좌표 변환, `MotionEvent`·`KeyEvent` 주입 | `ShizukuGateway` |
| `ClipboardBridge` | 양방향 클립보드 동기화 | — |
| `FloatingWindowService` | 플로팅 창, 포그라운드 알림, 세션 유지 | `VirtualDisplaySession` |
| `DexSurfaceView` | `Surface` 제공, 터치 수집 | — |
| `OnboardingState` | Shizuku 설치·실행·권한 상태 판정과 안내 | `ShizukuGateway` |

`VideoPipeline`과 `ControlChannel`은 사라졌다. 전자는 인코딩 왕복이 없어져서, 후자는 scrcpy 프로토콜 대신 `IInputManager`를 직접 쓰기 때문이다.

### 3.5 세션 정리

가상 디스플레이는 shell uid 문맥에서 생성된다. 앱이 비정상 종료하면 디스플레이가 남을 수 있다.

이 저장소는 같은 문제를 이미 겪었다. DX Companion이 존재하는 이유가 `overlay_display_devices` 잔여물 정리이며, DX Manager는 `--stop-dex`에 `cleanupUntrackedOverlay` 경로를 둔다.

따라서 다음을 설계에 포함한다.

- `FloatingWindowService` 종료 시 `VirtualDisplaySession.release()` 보장
- 앱 시작 시 `DisplayManager.getDisplays()`로 이 앱이 만든 잔여 디스플레이를 찾아 정리 (디스플레이 이름에 고정 접두사를 부여해 식별)
- 포그라운드 알림에 상시 "중지" 액션 노출

`IVirtualDisplayCallback`은 앱 프로세스가 죽으면 Binder death로 시스템에 통지된다. 이것이 1차 방어선이지만, 위 정리 경로를 생략할 근거로 삼지 않는다.

## 4. UI 및 입력 설계

### 4.1 플로팅 창

`TYPE_APPLICATION_OVERLAY` 창을 포그라운드 서비스가 소유한다. 구성은 얇은 컨트롤 바와 `SurfaceView`다.

컨트롤 바는 창 이동 핸들을 겸하며 뒤로·홈·최근 버튼, 문자 입력 상자, 중지 버튼을 포함한다.

2.3절에 따라 앱 실행은 삼성 보조 런처가 담당하므로 앱 실행 UI는 만들지 않는다.

### 4.2 창 크기와 해상도

**창 크기가 곧 디스플레이 해상도다.** 앱이 디스플레이의 소유자이므로 `VirtualDisplay.resize(width, height, dpi)`로 재기동 없이 크기를 바꾼다.

- **리사이즈가 끝난 시점에만** 적용한다(디바운스). 드래그 중에는 스케일링으로 보여준다.
- **창 이동은 리사이즈가 아니므로** 아무 동작도 하지 않는다.
- 디스플레이가 유지되므로 그 위의 앱은 재시작되지 않는다. 구성 변경만 전달된다.

`resize()`가 이 기기에서 기대대로 동작하는지는 Phase 3에서 확인한다(8절).

### 4.3 입력·문자·클립보드 — 시스템 서비스 직접 호출

scrcpy 프로토콜을 쓰지 않는다. Shizuku가 시스템 서비스를 직접 열어주므로 중간 프로토콜이 필요 없다.

| 필요한 것 | 수단 |
| :--- | :--- |
| 터치·드래그 | `IInputManager.injectInputEvent(MotionEvent, 0)` |
| 스크롤 | `MotionEvent`(`ACTION_SCROLL`) 주입 |
| 뒤로·홈·최근 | `IInputManager.injectInputEvent(KeyEvent, 0)` |
| **문자 입력** | **`setDisplayImePolicy` + 표준 IME** (아래) |
| 클립보드 | 앱 프로세스의 `ClipboardManager` 양방향 동기화 |

좌표는 `SurfaceView` 뷰 좌표에서 디스플레이 좌표로 변환하고, `MotionEvent`에 대상 displayId를 설정해 주입한다.

#### 문자 입력 방식의 변경

이전 설계는 scrcpy의 `INJECT_TEXT`로 2.5절의 IME 제약을 우회하려 했다. Shizuku 경로에서는 **`IWindowManager.setDisplayImePolicy(displayId, policy)`** 를 1순위로 쓴다(2.8절). 제약을 우회하는 대신 정면으로 푸는 방법이고, 사용자가 평소 쓰는 IME(예측·다국어·클립보드 이력)를 그대로 쓸 수 있다.

이 API가 이 기기에서 기대대로 동작하지 않으면 대안은 다음과 같다.

1. 컨트롤 바에 텍스트 입력란을 두고, 주 디스플레이의 IME로 입력받아 `KeyEvent` 시퀀스로 주입
2. 위가 비ASCII에서 실패하면 `IInputManager`의 문자 주입 경로 조사

Phase 2에서 1순위를 먼저 검증하고, 실패 시 대안 1로 내려간다.

### 4.4 온보딩

앱은 네 가지 상태를 구분해 안내한다.

| 상태 | 안내 |
| :--- | :--- |
| Shizuku 미설치 | 설치 안내와 배포처 링크 |
| Shizuku 미실행 | 무선 디버깅으로 PC 없이 시작하는 절차 |
| 권한 미승인 | 권한 요청 다이얼로그 재호출 |
| 준비됨 | DeX 창 시작 |

2.4절에 따라 화면이 잠긴 상태에서는 동작하지 않음을 안내한다.

## 5. 검증 전략

### 5.1 Phase 0 검증 현황

| 항목 | 상태 |
| :--- | :--- |
| shell 권한의 디스플레이별 앱 실행·입력 주입 | ✅ 검증 (2.2절) |
| 삼성 보조 런처 자동 부착 | ✅ 검증 (2.3절, Phase 0 Task 2) |
| 보조 디스플레이 IME | ✅ 검증 — 기본값으로는 뜨지 않음 (2.5절, Phase 0 Task 3) |
| 기기가 자기 adb 데몬에 접속 (shell uid) | ✅ 검증 (2.6절) |
| 일반 앱 uid에서 adb 데몬 접속 | ✅ 검증 — 가능 (5.5절). 접근안 변경으로 불필요해짐 |
| 앱 안에서 ADB 페어링·RSA 인증 구현 | ❌ **불가** — hidden API 제한 (5.5절) |
| **Surface를 Shizuku 경유로 전달** | ✅ **성립** — 프로덕션 선행 사례 (2.8절) |

### 5.2 Phase 0 재작성 범위

**Phase 0은 종료한다.** Task 1~3의 실기 결과는 접근안과 무관하게 유효하며(5.5절), 남은 최대 위험이었던 Surface 전달 가능 여부는 선행 사례 조사로 해소되었다(2.8절).

재작성 예정이던 Task 4~6은 취소한다. 기기에서 증명하려던 것을 프로덕션 코드가 이미 증명하고 있으므로, 같은 결론에 실기 패스를 더 쓰지 않는다. 남은 미검증 항목은 우리 시나리오에 고유한 것들이며 Phase 1~3의 관문으로 배치했다(6·8절).

### 5.3 자동 테스트

좌표 변환, 온보딩 상태 머신, 플래그 조합 로직을 단위 테스트로 검증한다. `ShizukuGateway`는 인터페이스 뒤에 두어 fake로 대체 가능하게 한다 — 시스템 서비스 프록시는 JVM 테스트에서 얻을 수 없다.

### 5.4 실기 검증

실제 기기에서의 표시·조작·정리는 사용자 확인 항목이다. `AGENTS.md` 원칙에 따라 대신 성공했다고 가정하지 않고 미확인으로 명시한다.

### 5.5 Phase 0 판정 (2026-09-04) — Shizuku 경로로 복귀

기기: Galaxy Z Fold (SM-F971N), Android 17 (SDK 37), One UI 9.0. 총 13회의 실기 패스.

| 검증 항목 | 결과 |
| :--- | :--- |
| 일반 앱 uid에서 adbd 소켓 접속 | ✅ 가능 (`app uid: 10494`, `connect: OK`) |
| 앱이 두 엔드포인트를 mDNS로 런타임 발견 | ✅ 가능 (라이브러리의 `AdbMdns`, 6회 중 5회 성공) |
| 앱 내 TLS 1.3 핸드셰이크 완료 | ✅ 가능 — 단 **인메모리 키에 한함** |
| **앱 내 ADB 페어링 완료(셸 획득)** | ❌ **불가 — 이번 환경에서 미달성** |

**결론: 접근안을 Shizuku 경유로 되돌린다.**

차단 지점은 `PairingConnectionCtx.exportKeyingMaterial()`이다. TLS 핸드셰이크 직후 SPAKE2용 키 재료를 뽑는 단계에서, 라이브러리가 Conscrypt의 `exportKeyingMaterial(SSLSocket, String, byte[], int)`을 리플렉션으로 호출하는데 `NoSuchMethodException`이 발생한다. Android의 non-SDK interface(hidden API) 제한이 유력하며, 라이브러리 README도 이런 내부 접근에 hidden-API 우회 도구를 언급한다. 그 우회를 도입하는 것은 스파이크 범위를 넘고 프로덕션 앱에 넣기에도 부담이 크다.

13패스 중 **페어링 코드가 실패 원인이었던 적은 한 번도 없다.** 모든 실패는 SPAKE2 교환 이전 단계에서 발생했다.

#### 접근안과 무관하게 유효한 발견

아래는 Shizuku 경로에서도 그대로 적용된다.

- **삼성 보조 런처 자동 부착**(2.3절) — 디스플레이를 누가 만들든 성립한다. 앱이 데스크톱 셸을 구현할 필요가 없다는 이 설계의 핵심 전제.
- **보조 디스플레이 IME 미표시**(2.5절) — 문자 입력은 IME를 경유하지 않는 경로가 필요하다.
- **잠금 상태 제약**(2.4절) — 잠긴 화면에서는 가상 디스플레이가 철거된다.

#### ADB 클라이언트 경로에만 해당하는 발견 (기록 보존)

- **adbd의 TLS 리스너 포트 두 개가 모두 회전한다.** 연결 포트와 페어링 포트 모두, 페어링 대화상자를 열어둔 상태에서도 바뀐다. 사람이 값을 전달하는 방식은 원리적으로 성립하지 않으며, 클라이언트가 사용 직전에 mDNS로 발견해야 한다.
- **AndroidKeyStore 키는 이 라이브러리의 핸드셰이크 경로와 호환되지 않는다.** 번들 Conscrypt와 플랫폼 Conscrypt 양쪽에서 동일한 `RSA routines:OPENSSL_internal:internal error`가 발생했고(4회), 인메모리 RSA 키로 바꾸자 핸드셰이크가 즉시 성공했다. 추출 불가능한 하드웨어 보호 키를 쓰려면 다른 라이브러리나 다른 경로가 필요하다.

#### Shizuku 경로에서 다시 열리는 질문 — **해소됨 (2026-09-04)**

Shizuku는 shell uid 프로세스와 **Binder 다리**를 모두 제공하므로, 원래 검토했던 두 방식이 다시 선택지가 되었다.

| | 화면 전달 | 판정 |
| :--- | :--- | :--- |
| **Surface 직접 연결** | 인코딩 없음 | ✅ **성립 — 채택.** 프로덕션 선행 사례로 확인(2.8절) |
| Shizuku로 scrcpy-server 기동 | H.264 소켓 | 불채택. 같은 결과에 인코딩 왕복 비용만 추가된다 |

기기 스파이크 없이 문서 조사로 결론이 났다. `Surface`는 `Parcelable`이고 Shizuku 브리지는 평범한 `IBinder` 래퍼이므로, 애초에 특별한 제약이 없었다.

#### Phase 0에서 얻은 절차적 교훈

13회의 실기 패스 중 상당수는 **선행 사례를 먼저 찾았다면 불필요했다.** 신규 플랫폼 API를 다룰 때는 "이것이 가능한가"를 기기에서 밑바닥부터 증명하기 전에, 같은 문제를 이미 푼 프로덕션 코드가 있는지 먼저 확인한다. 이 순서를 Phase 1 이후에도 유지한다.

## 6. 전달 단계

| Phase | 내용 | 완료 조건 |
| :--- | :--- | :--- |
| 0 | 기술 스파이크 (버리는 코드) | **완료(5.5·2.8절).** 권한 경로와 화면 전달 방식 확정 |
| 1 | 앱 뼈대 + 전체화면으로 DeX 화면 표시 | Shizuku 권한 획득, 가상 디스플레이 생성, 삼성 런처 부착, 화면이 보임 |
| 2 | 입력 — 터치·키·문자 | 조작 가능. `setDisplayImePolicy` 검증 포함(4.3절) |
| 3 | 플로팅 창 + 리사이즈 | 실사용 가능. `VirtualDisplay.resize()` 확인 |
| 4 | 온보딩 상태 머신 + 세션 정리 | 재부팅·크래시 후 잔여물 없음 |
| 5 | 클립보드, 배포 준비 | |

Phase 3 종료 시점부터 실사용이 가능하다.

Phase 1~2를 전체화면으로 먼저 만드는 것은 의도적이다. 플로팅 창은 좌표계·포커스·터치 가로채기가 얽혀 있어, "화면이 뜨지 않는다"와 동시에 디버깅하면 원인 분리가 불가능해진다.

**Phase 1의 첫 관문은 삼성 `SecondaryLauncher` 부착 확인이다**(2.8절 미해결 항목). 이것이 성립하지 않으면 앱이 데스크톱 셸을 직접 구현해야 하므로 설계 전제가 무너진다. 화면 표시보다 먼저 확인한다.

## 7. 배포

- Companion과 마찬가지로 APK 직배포로 시작한다. Shizuku 의존 앱은 Play Store 정책상 배포 사례가 있으나, v1 목표에서는 제외한다.
- **서명 키는 Companion과 분리한다.**
- 서명 관련 파일(`signing.properties`, 키스토어, 비밀번호)은 커밋하지 않는다. Companion의 `SIGNING.md` 규칙을 따른다.
- Shizuku API는 Apache-2.0이므로 라이선스 고지를 포함한다.
- **GPLv3 코드는 포함하지 않는다.** 2.8절의 선행 사례 조사는 API 사실 확인에 한정했으며 코드를 가져오지 않았다. 이 앱은 DX Manager와 동일하게 MIT를 유지한다.

## 8. 열린 항목

- ~~**앱 안의 ADB 페어링·인증 구현 난이도가 미검증이다.**~~ — **해결됨(2026-09-04).** Phase 0에서 13패스에 걸쳐 검증했고 `exportKeyingMaterial`의 hidden API 제한에 막혔다. Shizuku 의존으로 복귀했다(5.5절).
- ~~**Surface를 Shizuku 경유로 shell 프로세스에 전달할 수 있는지 미검증.**~~ — **해결됨(2026-09-04).** 성립한다. 프로덕션 선행 사례로 확인했고 인코딩 왕복 없는 경로를 채택했다(2.8절).
- ~~**일반 앱 uid에서 localhost adbd 접속 가능 여부 미검증**~~ — **해결됨(2026-09-04).** 가능하다. 다만 Shizuku 복귀로 이 사실은 더 이상 이 설계에 필요하지 않다.
- ~~**재부팅 후 재연결 동작 미검증.**~~ — 앱 내장 ADB 클라이언트 경로가 폐기되어 무의미해졌다.
- ~~**같은 기기 내 H.264 인코딩→디코딩의 실제 비용 미측정.**~~ — Surface 직접 연결 채택으로 인코딩 왕복 자체가 사라져 무의미해졌다.
- ~~**실행 중인 디스플레이의 크기 변경 수단 미검증.**~~ — Surface 직접 연결이므로 `VirtualDisplay.resize()`를 쓴다. Phase 3에서 확인한다.
- **이 경로로 만든 디스플레이에 삼성 `SecondaryLauncher`가 부착되는지 미검증.** 2.3절 검증은 `overlay_display_devices` 경로였다. 설계의 핵심 전제이므로 Phase 1의 첫 관문으로 둔다(6절).
- **`ALWAYS_UNLOCKED` 플래그가 2.4절 잠금 철거를 막는지 미검증.** 성립하면 잠금 상태 제약이 사라진다. Phase 1에서 함께 확인한다.
- **`setDisplayImePolicy`로 보조 디스플레이에 IME가 실제로 뜨는지 미검증.** 실패 시 대안이 4.3절에 있다. Phase 2에서 확인한다.
- **Shizuku 재부팅 후 재시작 문제.** 무선 디버깅으로 PC 없이 시작할 수 있으나 재부팅마다 반복해야 한다. Companion이 보유한 `WRITE_SECURE_SETTINGS`로 자동화하는 아이디어는 여전히 미검증이다.
- **삼성 내 기기·One UI 버전별 차이 미검증.** 검증은 Galaxy Z Fold(One UI 9.0) 한 대에서만 이루어졌다. 특히 폴더블이 아닌 기기와 One UI 8.x 이하는 확인이 필요하다.

