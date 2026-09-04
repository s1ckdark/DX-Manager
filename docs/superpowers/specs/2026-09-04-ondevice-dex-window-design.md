# 온디바이스 DeX 창 (DX DeX Window) 설계

- 작성일: 2026-09-04
- 상태: 설계 승인됨, 구현 계획 대기
- 대상: `DXDexWindow` (신규 안드로이드 앱)

## 1. 배경과 목표

DX Manager는 PC에서 scrcpy로 휴대폰의 가상 디스플레이를 미러링해 DeX 데스크톱을 제공한다. 이 설계의 목표는 **PC 없이 휴대폰 자체에서** 같은 DeX 데스크톱을 띄우고 조작하는 독립 안드로이드 앱을 만드는 것이다.

사용자는 휴대폰 화면 위에 뜨는 플로팅 창에서 DeX 데스크톱을 보고 터치·문자로 조작한다. 대화면 폴더블에서 특히 유용하다.

요구된 입력 범위는 터치·문자·클립보드다. 이 중 클립보드는 같은 기기의 시스템 클립보드가 하나이므로 별도 구현 없이 충족된다(4.5절).

### 지원 대상

**Samsung Galaxy 기기(One UI) 전용이다.** 다른 제조사는 지원 대상이 아니다.

근거는 2.3절이다. 이 설계는 삼성이 보조 디스플레이에 자동으로 붙이는 `com.sec.android.app.launcher/com.honeyspace.dexservice.SecondaryLauncher`를 데스크톱 셸로 사용한다. 다른 제조사에는 이에 상응하는 셸이 없으므로 앱이 런처·작업표시줄·창 관리를 직접 구현해야 하고, 그것은 이 프로젝트의 몇 배 규모다.

비삼성 기기에서 우연히 동작할 수는 있으나 검증하지 않으며, 런처가 없는 환경을 위한 자체 데스크톱 셸은 만들지 않는다. 앱은 시작 시 제조사를 확인해 비지원 안내를 표시한다.

### 비목표

- 비삼성 기기 지원 및 자체 데스크톱 셸 구현
- DX Manager(PC)와의 연동 — 이 앱은 완전히 독립적으로 동작한다
- 기존 DX Companion(`DXDisplayCleanup`)의 수정 — 그 앱의 안전 경계를 그대로 유지한다
- Companion의 `WRITE_SECURE_SETTINGS`를 이용한 Shizuku 자동 시작 (8절 참조)
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

예외는 `INTERNAL_SYSTEM_WINDOW`(시스템)와 `ACTIVITY_EMBEDDING`(대상 앱이 `FLAG_ALLOW_EMBEDDED`를 선언한 경우에 한정)뿐이다. 실제로 그 플래그를 켠 서드파티 앱은 거의 없다.

`COMPANION_DEVICE_APP_STREAMING` role은 `CREATE_VIRTUAL_DEVICE` 권한을 부여하지만, CompanionDeviceManager로 **원격 기기와 연결**해야 얻을 수 있고 원격 스트리밍을 전제한 설계라 같은 기기 내 사용에는 맞지 않는다.

**결론**: shell uid 권한이 필요하며, 이를 일반 앱에 중계하는 표준 수단이 **Shizuku**다. scrcpy가 동작하는 이유도 동일하다 — `adb shell`이 shell uid(2000)로 실행되기 때문이다.

### 2.2 실기 검증 결과 (2026-09-03)

기기: Galaxy Z Fold (SM-F971N), Android 17 (SDK 37), One UI 9.0

| 항목 | 결과 |
| :--- | :--- |
| `am start --display 42 -n com.android.settings/.Settings` | ✅ Display #42에 실제로 배치됨 |
| `input -d 42 keyevent KEYCODE_BACK` | ✅ 해당 디스플레이에만 전달, Display #0 무영향 |
| `input -d 42 tap 800 450` | ✅ 오류 없이 수용 |

BACK 키 검증이 결정적이다. 입력 전 Display #42의 최상위는 `com.android.settings/.SubSettings`였고, 입력 후 `com.android.chrome/...Main`으로 바뀌었다. Display #0은 변화가 없었다.

**One UI 9.0은 shell 수준의 디스플레이별 앱 실행과 입력 주입을 제한하지 않는다.**

### 2.3 삼성 보조 런처가 자동으로 붙는다

오버레이 디스플레이에 다음이 자동으로 실행되어 있었다.

```
com.sec.android.app.launcher/com.honeyspace.dexservice.SecondaryLauncher
```

앱 목록·작업표시줄·창 관리를 포함한 데스크톱 셸을 삼성이 제공한다는 뜻이다. **이 앱이 데스크톱 환경을 직접 구현할 필요가 없다.**

앱이 만든 가상 디스플레이에도 붙을 가능성이 높다. `scrcpy --new-display` 실험의 logcat에 다음이 있었다.

```
E/ActivityTaskManager: Abort starting home on DefaultTaskDisplayArea_d22 recursively.
```

Android가 shell이 만든 가상 디스플레이에도 홈(런처)을 띄우려 시도했다는 증거다. 당시 중단된 이유는 잠금 화면이었다(2.4절). 다만 이는 정황 증거이며 확정은 Phase 0에서 한다.

### 2.4 잠금 상태에서는 보조 디스플레이가 동작하지 않는다

화면이 꺼져 잠긴 상태에서는 가상 디스플레이가 생성되어도 홈이 시작되지 않고 곧 철거된다.

```
V/ShellDesktopMode:   handlePotentialReconnect: Keyguard is locked; aborting.
V/WindowManagerShell: handlePotentialReconnect: Keyguard is locked; aborting.
```

앱은 이 조건을 사용자에게 안내해야 한다.

### 2.5 PC 없는 Shizuku 시작

Shizuku는 Android 11+에서 **무선 디버깅**으로 기기 자체에서 시작할 수 있다. PC 연결이 필요 없다.

```
설정 > 개발자 옵션 > 무선 디버깅 > 페어링 코드로 기기 페어링
  → Shizuku 앱에서 시작
```

**제약: 재부팅할 때마다 다시 수행해야 한다.** 이것이 이 앱의 최대 UX 비용이다.

참고:
- [Activity launch policy — AOSP](https://source.android.com/docs/core/display/multi_display/activity-launch)
- [Companion app streaming — AOSP](https://source.android.com/docs/core/permissions/app-streaming)
- [VirtualDisplay — Android Developers](https://developer.android.com/reference/android/hardware/display/VirtualDisplay)
- [Shizuku 사용 설명서](https://shizuku.rikka.app/guide/setup/)

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
| `minSdk` | 30 (Android 11) | PC 없는 Shizuku 시작이 무선 디버깅 기반 |
| `targetSdk` / `compileSdk` | 36 | Companion과 동일 |
| 언어 | Java | 저장소의 유일한 안드로이드 앱이 Java |
| 신규 의존성 | `dev.rikka.shizuku:api`, `dev.rikka.shizuku:provider` | 이 저장소 안드로이드 코드의 첫 서드파티 의존성 |

Companion은 `minSdk 24`를 유지한다. Companion은 구형 기기의 복구 도구로 남고, 이 앱은 무선 디버깅이 가능한 기기만 대상으로 한다.

### 3.2 별도 앱으로 만드는 이유

DX Companion의 README는 다음 안전 경계를 선언한다.

> `WRITE_SECURE_SETTINGS`를 문서화된 두 복구 설정에만 사용한다.
> 셸을 제공하거나 임의 명령을 실행하지 않으며, 클라우드에 접속하거나 데이터를 수집하지 않는다.

Shizuku 기반 온디바이스 DeX는 shell uid 프로세스에 연결해 임의 앱을 실행하고 입력을 주입한다. 이는 위 선언과 정면으로 충돌하며, DX Manager가 패키지명과 서명 인증서를 검증한 뒤 권한을 부여하는 신뢰 모델과도 맞지 않는다.

따라서 두 앱을 분리하고 **서명 키도 분리한다**. Companion을 쓰던 사용자가 이 앱 때문에 넓어진 권한을 떠안지 않는다.

### 3.3 런타임 데이터 흐름

```
FloatingWindowService (foreground, TYPE_APPLICATION_OVERLAY)
  └ DexSurfaceView ──── Surface ────┐
                                     ↓
                          ShizukuGateway (shell uid 중계)
                                     ↓
                    VirtualDisplayController
                      DisplayManager.createVirtualDisplay(
                          surface, width, height, densityDpi,
                          TRUSTED | SHOULD_SHOW_SYSTEM_DECORATIONS)
                                     ↓
                                 displayId
                        ┌────────────┴────────────┐
                   InputForwarder              AppLauncher
              injectInputEvent(displayId)   am start --display
```

Surface는 디스플레이 생성 시 한 번만 전달된다. 이후 Android가 그 디스플레이를 앱의 Surface에 직접 합성하므로 **프레임 단위 복사나 인코딩 경로가 없다.**

같은 기기 안에서 H.264로 인코딩했다가 디코딩하는 것은 순수한 낭비다. 지연·발열·배터리 모두 이 설계가 유리하다.

### 3.4 컴포넌트 책임

| 컴포넌트 | 책임 | 의존 |
| :--- | :--- | :--- |
| `ShizukuGateway` | Shizuku 바인딩, 권한 확인, shell 호출 중계 | Shizuku API |
| `VirtualDisplayController` | 디스플레이 생성·리사이즈·해제, 수명주기 소유 | `ShizukuGateway` |
| `FloatingWindowService` | 플로팅 창, 포그라운드 알림, 세션 유지 | — |
| `DexSurfaceView` | Surface 제공, 터치 이벤트 수집 | — |
| `InputForwarder` | 좌표 변환, 이벤트 재구성·주입 | `ShizukuGateway`, displayId |
| `AppLauncher` | 보조 디스플레이에 앱 실행 | `ShizukuGateway`, displayId |
| `OnboardingState` | Shizuku 상태 판정과 안내 | `ShizukuGateway` |

### 3.5 디스플레이 누수 방지

가상 디스플레이는 생성한 프로세스가 유지되는 동안 존재한다. 앱이 비정상 종료하거나 Shizuku가 죽으면 디스플레이가 남을 수 있다.

이 저장소는 같은 문제를 이미 겪었다. DX Companion이 존재하는 이유가 `overlay_display_devices` 잔여물 정리이며, DX Manager는 `--stop-dex`에 `cleanupUntrackedOverlay` 경로를 둔다.

따라서 다음을 설계에 포함한다.

- `FloatingWindowService` 종료 시 명시적 해제
- 앱 시작 시 자기 이름으로 만들어진 고아 디스플레이를 찾아 정리
- 포그라운드 알림에 상시 "중지" 액션 노출

## 4. UI 및 입력 설계

### 4.1 플로팅 창

`TYPE_APPLICATION_OVERLAY` 창을 포그라운드 서비스가 소유한다. 구성은 얇은 컨트롤 바와 `SurfaceView`다.

컨트롤 바는 창 이동 핸들을 겸하며 뒤로·홈·최근 버튼, 앱 실행 버튼, 중지 버튼을 포함한다.

**앱 실행 버튼의 위치**: 2.3절에 따라 삼성 보조 런처가 붙으면 앱 실행은 그 런처가 담당하므로 이 버튼은 보조 수단이다. 런처가 붙지 않는 경우(8절 열린 항목)에는 유일한 실행 수단이 된다. 따라서 v1에서는 설치된 앱 목록을 띄워 `AppLauncher`로 실행하는 최소 형태로 만들고, Phase 0에서 런처 부착이 확인되면 눈에 덜 띄는 위치로 옮긴다.

### 4.2 창 크기와 해상도

**창 크기가 곧 디스플레이 해상도다.** 디스플레이를 다시 만들지 않고 `VirtualDisplay.resize(width, height, densityDpi)`(API 21+)로 조정한다.

| | 재생성 | `resize()` |
| :--- | :--- | :--- |
| displayId | 변경됨 | 유지 |
| 디스플레이 위의 앱 | 재배치·재시작 | 유지 (구성 변경만 발생) |
| 삼성 보조 런처 | 다시 부착 필요 | 유지 |

두 가지 규칙을 함께 적용한다.

- **리사이즈가 끝난 시점에만** 적용한다(디바운스). 드래그 중 매 프레임 호출하면 구성 변경이 폭주한다.
- **창 이동은 리사이즈가 아니므로** 아무 동작도 하지 않는다.

디스플레이를 만든 주체가 shell 프로세스이므로 `resize()` 호출도 `ShizukuGateway`를 경유한다.

### 4.3 입력 매핑

```
SurfaceView의 터치 (뷰 좌표)
  → 스케일 변환: x * (displayWidth / viewWidth), y * (displayHeight / viewHeight)
  → MotionEvent 재구성 (멀티터치 포인터 유지, source = SOURCE_TOUCHSCREEN)
  → event.setDisplayId(displayId)
  → injectInputEvent(event, INJECT_INPUT_EVENT_MODE_ASYNC)
```

창 크기와 해상도가 같아지면 스케일 계수는 1이 되지만, 디바운스 적용 전 과도기와 밀도 차이를 위해 변환 경로는 항상 유지한다.

뒤로·홈·최근은 컨트롤 바 버튼에서 `KeyEvent`를 같은 경로로 주입한다.

### 4.4 문자 입력

두 단계로 구성한다.

1. **1차**: 보조 디스플레이의 자체 IME. `TRUSTED` + `SHOULD_SHOW_SYSTEM_DECORATIONS`를 세우면 조건이 충족된다.
2. **폴백**: 컨트롤 바의 입력 상자에 입력한 문자열을 `KeyEvent`로 주입한다.

폴백을 v1에 포함하는 이유는 삼성이 보조 디스플레이 IME를 제한할 가능성이 미검증이기 때문이다. DX Manager가 scrcpy에서 `--prefer-text`를 사용하는 것과 같은 대비다.

### 4.5 클립보드 — 구현하지 않는다

같은 기기의 시스템 클립보드가 하나이므로, 보조 디스플레이의 앱이 직접 읽고 쓴다. 이 앱은 클립보드에 관여하지 않는다.

관여해서도 안 된다. Android 10+는 포커스가 없는 앱의 클립보드 읽기를 차단하며, 플로팅 오버레이는 일반적으로 포커스를 갖지 않는다.

### 4.6 온보딩

앱은 네 가지 상태를 구분해 안내한다.

| 상태 | 안내 |
| :--- | :--- |
| Shizuku 미설치 | 설치 안내와 링크 |
| Shizuku 미실행 | 무선 디버깅 페어링 절차 (PC 불필요) |
| 권한 미부여 | Shizuku 권한 요청 |
| 준비됨 | DeX 창 시작 |

**재부팅 후 재실행이 필요하다는 점을 숨기지 않고 명시한다.** 또한 2.4절에 따라 화면이 잠긴 상태에서는 동작하지 않음을 안내한다.

## 5. 검증 전략

### 5.1 미검증 위험을 먼저 해소한다

| 항목 | 상태 |
| :--- | :--- |
| `am start --display N`로 타 앱 실행 | ✅ 실기 검증 (2.2절) |
| `input -d N`로 디스플레이별 입력 | ✅ 실기 검증 (2.2절) |
| 삼성 보조 런처 자동 부착 (오버레이 디스플레이) | ✅ 실기 확인 (2.3절) |
| **Surface를 shell 프로세스에 전달해 디스플레이 생성** | ❌ 미검증 — 최대 위험 |
| **앱이 만든 디스플레이에도 런처가 붙는가** | ⚠️ 정황 증거만 (2.3절) |
| **보조 디스플레이 IME 표시** | ❌ 미검증 |

Phase 0에서 이 셋을 버리는 코드로 먼저 확인한다. 여기서 막히면 접근안을 scrcpy-server 내장 방식으로 선회해야 하는데, 그 판단을 앱 구현이 끝난 뒤에 내리면 늦다.

### 5.2 자동 테스트

- 좌표 변환, 온보딩 상태 머신, 디스플레이 수명주기를 단위 테스트로 검증한다. Companion이 이미 같은 방식의 테스트를 갖고 있다.
- Shizuku 호출은 게이트웨이 인터페이스 뒤에 두어 fake로 대체 가능하게 한다.

### 5.3 실기 검증

실제 기기에서의 표시·조작·정리는 사용자 확인 항목이다. `AGENTS.md` 원칙에 따라 대신 성공했다고 가정하지 않고 미확인으로 명시한다.

## 6. 전달 단계

| Phase | 내용 | 완료 조건 |
| :--- | :--- | :--- |
| 0 | 기술 스파이크 (버리는 코드) | Surface 전달로 디스플레이 생성 성공, 런처·IME 부착 여부 판정 |
| 1 | 앱 뼈대 + 전체화면으로 디스플레이 표시 | 화면이 보임 |
| 2 | 터치·키 입력 주입 | 조작 가능 |
| 3 | 플로팅 창 + `resize()` 연동 | 실사용 가능 |
| 4 | 온보딩 상태 머신 + 누수 정리 | 재부팅·크래시 후 잔여물 없음 |
| 5 | 문자 입력 폴백, 배포 준비 | |

Phase 3 종료 시점부터 실사용이 가능하다.

Phase 1~2를 전체화면으로 먼저 만드는 것은 의도적이다. 플로팅 창은 좌표계·포커스·터치 가로채기가 얽혀 있어, "디스플레이가 뜨지 않는다"와 동시에 디버깅하면 원인 분리가 불가능해진다.

## 7. 배포

- Play Store는 Shizuku 의존 앱에 정책 리스크가 있어 v1 목표에서 제외한다.
- Companion과 마찬가지로 APK 직배포로 시작한다.
- **서명 키는 Companion과 분리한다.** 3.2절에서 신뢰 경계를 나눈 결정을 서명에서도 유지한다.
- 서명 관련 파일(`signing.properties`, 키스토어, 비밀번호)은 커밋하지 않는다. Companion의 `SIGNING.md` 규칙을 따른다.

## 8. 열린 항목

- **Surface를 shell 프로세스에 전달하는 경로가 문서화되어 있지 않다.** Phase 0의 핵심 검증 대상이다. 실패 시 scrcpy-server 내장 방식으로 선회한다.
- **삼성 보조 런처가 앱 생성 디스플레이에 붙는지 미확정.** 붙지 않으면 앱이 자체 런처 UI를 제공해야 하며 범위가 크게 늘어난다.
- **보조 디스플레이 IME 동작 미검증.** 4.4절의 폴백이 이에 대한 대비다.
- **Shizuku 재부팅 후 재실행 문제.** Companion이 보유한 `WRITE_SECURE_SETTINGS`로 무선 ADB를 자동으로 켜는 경로가 이론적으로 존재하나, Shizuku 본체에 구현되어 있지 않고 미검증이다. v1의 성패를 여기에 걸지 않는다.
- ~~**대상 기기 범위.**~~ — **해결됨(2026-09-04).** Samsung Galaxy(One UI) 전용으로 한정한다. 1절 "지원 대상" 참조. 다만 검증은 여전히 Galaxy Z Fold(One UI 9.0) 한 대에서만 이루어졌으므로, **삼성 내에서도 One UI 버전과 기기별 차이는 미검증**이다. 특히 폴더블이 아닌 기기와 One UI 8.x 이하는 확인이 필요하다.
