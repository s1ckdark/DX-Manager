# 온디바이스 DeX 창 Phase 0 — 기술 스파이크 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 온디바이스 DeX 창 설계의 미검증 위험에 답을 내고, 접근안(scrcpy 자체 기동)을 유지할지 Shizuku 의존으로 되돌릴지 판정한다.

**Architecture:** 이것은 **버리는 코드를 쓰는 스파이크**다. 산출물은 앱이 아니라 답이다. 가장 싸고 정보량이 큰 실험부터 배치한다 — Task 2·3은 코드 없이 `scrcpy`와 `adb`만으로 답하고, Task 4~6에서만 스파이크 앱을 만들어 앱이 자기 기기의 adb 데몬을 통해 셸을 얻고 `scrcpy-server`를 띄울 수 있는지 확인한다.

**Tech Stack:** Android (Java, minSdk 30, compileSdk 36), Gradle wrapper 8.14.5, AGP 8.13.2, libadb-android, adb, scrcpy 4.1

**Spec:** `docs/superpowers/specs/2026-09-04-ondevice-dex-window-design.md`

## 진행 상태 (2026-09-04)

**Task 1~3은 이미 완료되었다.** 접근안 선회 이전에 실행되었으나 그 결과는 접근안과 무관하게 유효하며, 실제로 이번 선회의 근거가 되었다.

| Task | 결과 |
| :--- | :--- |
| 1 | 툴체인 검증 완료. 기준선: `overlay=null`, `stay_on=0`, 디스플레이 `0 1` |
| 2 | **Q2 통과** — 삼성 `SecondaryLauncher`가 shell 생성 TRUSTED 디스플레이에 자동 부착 |
| 3 | **Q3 아니오** — 보조 디스플레이에 IME가 뜨지 않고 display 0에 결합 |

Task 4~6은 접근안 선회로 **전면 재작성되었다.** 기존의 Shizuku 경유 Surface 전달 검증은 무효가 되었으며, 그 질문 자체가 새 접근안에서는 존재하지 않는다.

## Global Constraints

- 이 Phase의 코드는 **전부 버린다.** 저장소에 커밋하지 않으며 스크래치 디렉터리에서만 작업한다.
- 산출물은 스펙 문서에 기록되는 **판정과 근거**다.
- 지원 대상은 Samsung Galaxy(One UI)다. 다른 제조사에서의 동작은 검증하지 않는다.
- 기기 설정을 변경했으면 **반드시 원복한다.** 특히 `overlay_display_devices`와 `stay_on_while_plugged_in`.
- 실기 조작은 사용자의 개인 기기에서 이루어진다. 앱 설치·삭제 외에 사용자 데이터를 건드리지 않는다.
- 실기 결과를 대신 성공했다고 가정하지 않는다(`AGENTS.md` 원칙).

## 환경 (모든 Task 공통)

```bash
export JAVA_HOME=/Users/dave/.asdf/installs/java/temurin-17.0.8+7
export ANDROID_HOME=/Users/dave/Library/Android/sdk
export PATH="$JAVA_HOME/bin:$PATH"
```

확인된 환경: JDK 17.0.8, Android platform-36, build-tools 36.1.0, Gradle wrapper 8.14.5.

스파이크 작업 디렉터리(저장소 밖):

```
/private/tmp/claude-501/-Users-dave-iWorks-DX-Manager/ac296bd2-8484-4146-a937-10d857effd0b/scratchpad/dexwindow-spike/
```

## 기기 연결

Task 1 실행 시점에 adb 대상이 셋 붙어 있었다 — USB(`R5***TP`), 무선(`IP:PORT`), 에뮬레이터(`emulator-5554`). **모든 adb 명령에 `-s <serial>`을 명시한다.** 생략하면 엉뚱한 대상에 명령이 간다.

**USB serial `R5***TP`를 우선한다** — 포트가 바뀌지 않기 때문이다. USB가 인식되지 않으면 무선으로 붙는다.

```bash
cd /Users/dave/iWorks/DX-Manager
./tools/adb/adb devices -l                      # USB serial 이 보이면 그것을 쓴다
./tools/adb/adb mdns services                   # 없으면 _adb-tls-connect._tcp 의 IP:PORT
./tools/adb/adb connect <IP>:<PORT>
```

이 Mac은 해당 기기와 이미 페어링되어 있어 **Mac에서 붙을 때는** 페어링 코드가 필요 없다. 무선 포트는 재부팅·재활성화 시 바뀌므로 매번 `mdns services`로 다시 확인한다.

Task 5의 **앱 내부 페어링은 별개다.** 앱은 자기만의 ADB 키를 갖고 처음 붙으므로 사용자가 페어링 코드를 새로 발급해야 한다.

`gradlew`는 저장소에 실행 비트 없이(100644) 추적되어 있다. **`bash ./gradlew ...` 형태로 실행한다** — `chmod +x`는 추적 파일을 변경하므로 금지한다.

**중요**: 2.4절에 따라 화면이 잠겨 있으면 보조 디스플레이가 동작하지 않는다. 모든 실험 전에 깨우고 잠금 해제 상태를 확인한다.

```bash
./tools/adb/adb -s <SERIAL> shell input keyevent KEYCODE_WAKEUP
./tools/adb/adb -s <SERIAL> shell dumpsys window | grep -oE 'isKeyguardShowing=[a-z]+' | head -1
# isKeyguardShowing=false 여야 한다
```

---

## File Structure

| 경로 | 책임 | 상태 |
| :--- | :--- | :--- |
| `<스크래치>/dexwindow-spike/` | 스파이크 Gradle 프로젝트 | 생성, 최종 삭제 |
| `.../app/src/main/java/.../SpikeActivity.java` | 결과 표시, 프로브 호출 진입점 | 생성 (Task 4) |
| `.../app/src/main/java/.../AdbProbe.java` | ADB 페어링·셸 획득·scrcpy-server 기동 | 생성 (Task 5·6) |
| `docs/superpowers/specs/2026-09-04-ondevice-dex-window-design.md` | 판정 반영 | 수정 (Task 7) |

---

## Task 1: 환경 준비와 기준선

> **✅ 완료 (2026-09-04).** 툴체인 검증 성공, 기준선 `overlay=null` / `stay_on=0` / 디스플레이 `0 1`. 아래 본문은 실행 당시 기준이며 참고용으로 보존한다.

스파이크 앱을 만들기 전에 툴체인이 실제로 안드로이드 앱을 빌드할 수 있는지 확인한다. 여기서 막히면 이후 모든 Task가 툴체인 문제인지 코드 문제인지 구분되지 않는다.

**Files:**
- 없음 (환경 작업, 커밋 없음)

**Interfaces:**
- Consumes: 없음
- Produces: 검증된 빌드 환경, 연결된 기기 serial

- [ ] **Step 1: 환경 변수 확인**

Run:
```bash
export JAVA_HOME=/Users/dave/.asdf/installs/java/temurin-17.0.8+7
export ANDROID_HOME=/Users/dave/Library/Android/sdk
export PATH="$JAVA_HOME/bin:$PATH"
java -version
ls "$ANDROID_HOME/platforms"
```
Expected: `openjdk version "17.0.8"`, platforms 목록에 `android-36` 포함

- [ ] **Step 2: 기존 Companion 빌드로 툴체인 검증**

Run:
```bash
cd /Users/dave/iWorks/DX-Manager/DXDisplayCleanup
export JAVA_HOME=/Users/dave/.asdf/installs/java/temurin-17.0.8+7
export ANDROID_HOME=/Users/dave/Library/Android/sdk
./gradlew --no-daemon assembleDebug
```
Expected: `BUILD SUCCESSFUL`

빌드가 실패하면 원인을 기록하고 **BLOCKED로 보고한다.** 서명 관련 실패라면 `assembleDebug`는 디버그 키로 서명하므로 `signing.properties` 없이도 통과해야 한다. 그 외 실패는 툴체인 문제다.

`DXDisplayCleanup/`은 읽기만 하고 **수정하지 않는다.** 빌드 산출물(`app/build/`)은 생성되지만 커밋하지 않는다.

- [ ] **Step 3: 기기 연결**

Run:
```bash
cd /Users/dave/iWorks/DX-Manager
./tools/adb/adb mdns services
```
Expected: `_adb-tls-connect._tcp` 행에 `<IP>:<PORT>` 표시

그 값으로 연결한다.

```bash
./tools/adb/adb connect <IP>:<PORT>
./tools/adb/adb devices -l
```
Expected: 해당 endpoint가 `device` 상태로 표시

기기가 나타나지 않으면 사용자에게 무선 디버깅이 켜져 있는지 확인을 요청하고 **BLOCKED로 보고한다.** 임의로 페어링을 시도하지 않는다.

- [ ] **Step 4: 기기 기준선 기록**

Run:
```bash
S=<IP>:<PORT>
./tools/adb/adb -s $S shell input keyevent KEYCODE_WAKEUP
sleep 2
echo "keyguard: $(./tools/adb/adb -s $S shell dumpsys window | grep -oE 'isKeyguardShowing=[a-z]+' | head -1)"
echo "overlay : $(./tools/adb/adb -s $S shell settings get global overlay_display_devices | tr -d '\r')"
echo "stay_on : $(./tools/adb/adb -s $S shell settings get global stay_on_while_plugged_in | tr -d '\r')"
echo "displays: $(./tools/adb/adb -s $S shell dumpsys display | grep -oE 'mDisplayId=[0-9]+' | sort -u | tr '\n' ' ')"
```

세 값을 기록한다. 이후 Task가 끝날 때마다 이 값으로 돌아왔는지 확인한다. 예상 기준값은 `isKeyguardShowing=false`, `overlay: null`, `stay_on: 0`, 디스플레이 `0`과 `1`.

- [ ] **Step 5: 기준선 보고**

커밋하지 않는다. Step 1~4의 결과를 보고서에 기록한다.

---

## Task 2: Q2 검증 — 삼성 런처가 shell 생성 디스플레이에 붙는가

> **✅ 완료 (2026-09-04) — Q2 통과.** 삼성 `SecondaryLauncher`가 shell 생성 TRUSTED 디스플레이(id=44)에 자동 부착됨을 확인했다. 본문의 "우리 앱이 Shizuku로 만들려는 것과 같은 조건"이라는 서술은 선회 이전 표현이며, 결과 자체는 새 접근안에도 그대로 적용된다 — scrcpy가 만드는 디스플레이가 바로 그것이기 때문이다.

**코드를 쓰지 않는다.** `scrcpy --new-display`가 이미 shell 권한으로 TRUSTED 디스플레이를 만든다. 앞서 수집한 logcat에서 그 디스플레이가 `FLAG_TRUSTED`와 `FLAG_SHOULD_SHOW_SYSTEM_DECORATIONS`를 가진 것이 확인되었으므로, 우리 앱이 Shizuku로 만들려는 것과 같은 조건이다.

이 Task가 실패하면 앱이 자체 데스크톱 셸을 만들어야 하고, 그것은 스펙 1절의 "지원 대상" 근거를 무너뜨린다. **가장 먼저 확인해야 할 항목이다.**

**Files:**
- 없음 (관찰만, 커밋 없음)

**Interfaces:**
- Consumes: Task 1의 기기 serial
- Produces: Q2 판정 (런처 부착 여부)

- [ ] **Step 1: 화면을 깨우고 잠금 해제 확인**

Run:
```bash
cd /Users/dave/iWorks/DX-Manager
S=<Task 1의 serial>
./tools/adb/adb -s $S shell input keyevent KEYCODE_WAKEUP
sleep 2
./tools/adb/adb -s $S shell dumpsys window | grep -oE 'isKeyguardShowing=[a-z]+' | head -1
```
Expected: `isKeyguardShowing=false`

`true`면 사용자에게 잠금 해제를 요청한다. 잠긴 상태에서는 이 실험이 반드시 실패하며(2.4절), 그 실패는 Q2에 대한 답이 아니다.

- [ ] **Step 2: scrcpy로 새 디스플레이 생성 (백그라운드)**

Run:
```bash
timeout 40 /opt/homebrew/bin/scrcpy -s <SERIAL> --new-display=1600x900/150 --no-audio > /tmp/spike-q2.log 2>&1 &
sleep 8
```

`scrcpy`가 8초 안에 죽으면 로그를 확인한다. 잠금 상태이거나 다른 이유일 수 있다.

- [ ] **Step 3: 그 디스플레이의 액티비티 확인**

Run:
```bash
./tools/adb/adb -s $S shell dumpsys activity activities | grep -E "^Display #|topResumedActivity="
```

Expected (Q2 = 통과): 새 디스플레이 번호 아래에
`com.sec.android.app.launcher/com.honeyspace.dexservice.SecondaryLauncher`
가 나타난다.

Expected (Q2 = 실패): 새 디스플레이에 아무 액티비티도 없거나 `SecondaryLauncher`가 없다.

두 경우 모두 **출력 전체를 보고서에 그대로 붙여넣는다.** 요약하지 않는다.

- [ ] **Step 4: 디스플레이 플래그 확인**

Run:
```bash
./tools/adb/adb -s $S shell dumpsys display | grep -A2 'uniqueId="virtual:' | head -20
```

`FLAG_TRUSTED`와 `FLAG_SHOULD_SHOW_SYSTEM_DECORATIONS`가 실제로 붙어 있는지 확인해 기록한다. 이는 우리 앱이 재현해야 할 조건이다.

- [ ] **Step 5: 정리**

Run:
```bash
pkill -x scrcpy 2>/dev/null; sleep 2; pkill -9 -x scrcpy 2>/dev/null
./tools/adb/adb -s $S shell dumpsys display | grep -oE 'mDisplayId=[0-9]+' | sort -u | tr '\n' ' '
```
Expected: Task 1 Step 4에서 기록한 기준 디스플레이 목록으로 복귀

남아 있으면 보고서에 기록한다.

- [ ] **Step 6: 판정 기록**

Q2의 답을 보고서에 한 줄로 명시한다: "삼성 보조 런처가 shell 생성 TRUSTED 디스플레이에 붙는다 / 붙지 않는다". 커밋하지 않는다.

---

## Task 3: Q3 검증 — 보조 디스플레이에 IME가 표시되는가

> **✅ 완료 (2026-09-04) — Q3 아니오.** IME가 보조 디스플레이(id 46)가 아니라 display 0에 결합되었다. 본문이 말하는 "스펙 4.4절의 폴백"은 선회로 사라졌다. 새 접근안에서는 scrcpy control 프로토콜의 `INJECT_TEXT`가 IME를 아예 경유하지 않으므로 이 제약이 문제되지 않는다(스펙 4.3절).

Task 2와 마찬가지로 **코드를 쓰지 않는다.** 같은 방식으로 디스플레이를 만들고, 텍스트 입력란이 있는 앱을 그 위에 띄운 뒤 IME가 뜨는지 관찰한다.

Q3가 실패하면 스펙 4.4절의 폴백(컨트롤 바 입력 상자 → KeyEvent 주입)이 유일한 문자 입력 수단이 된다. 설계는 이미 그 폴백을 포함하므로 **이 Task의 실패는 설계를 무너뜨리지 않는다.** 범위만 조정된다.

**Files:**
- 없음 (관찰만, 커밋 없음)

**Interfaces:**
- Consumes: Task 1의 기기 serial, Task 2에서 확인한 절차
- Produces: Q3 판정 (IME 표시 여부)

- [ ] **Step 1: 잠금 해제 확인 후 디스플레이 생성**

Run:
```bash
cd /Users/dave/iWorks/DX-Manager
S=<serial>
./tools/adb/adb -s $S shell input keyevent KEYCODE_WAKEUP
sleep 2
./tools/adb/adb -s $S shell dumpsys window | grep -oE 'isKeyguardShowing=[a-z]+' | head -1
timeout 60 /opt/homebrew/bin/scrcpy -s <SERIAL> --new-display=1600x900/150 --no-audio > /tmp/spike-q3.log 2>&1 &
sleep 8
```

- [ ] **Step 2: 새 디스플레이 번호 확인**

Run:
```bash
./tools/adb/adb -s $S shell dumpsys activity activities | grep -E "^Display #"
```

Task 1 Step 4의 기준 목록(`0`, `1`)에 없는 번호가 새 디스플레이다. 이후 `<DID>`로 표기한다.

- [ ] **Step 3: 텍스트 입력이 있는 앱을 그 디스플레이에 실행**

Run:
```bash
./tools/adb/adb -s $S shell am start --display <DID> -a android.intent.action.WEB_SEARCH
sleep 3
./tools/adb/adb -s $S shell dumpsys activity activities | grep -E "^Display #|topResumedActivity=" | head -10
```

`WEB_SEARCH` 인텐트를 받는 앱이 없으면 대신 연락처 검색을 쓴다.

```bash
./tools/adb/adb -s $S shell am start --display <DID> -a android.intent.action.SEARCH
```

둘 다 실패하면 설정 앱의 검색 화면을 사용한다.

```bash
./tools/adb/adb -s $S shell am start --display <DID> -n com.android.settings/.Settings
```

어느 것을 썼는지 보고서에 기록한다.

- [ ] **Step 4: 입력란을 탭해 IME를 호출**

Run:
```bash
./tools/adb/adb -s $S shell input -d <DID> tap 800 200
sleep 3
```

좌표는 1600x900 디스플레이의 상단 중앙이다. 검색창이 그 부근에 없으면 scrcpy 창에서 눈으로 확인한 위치로 조정하고, 사용한 좌표를 기록한다.

- [ ] **Step 5: IME 상태 확인**

Run:
```bash
./tools/adb/adb -s $S shell dumpsys input_method | grep -E "mDisplayId|mInputShown|mShowRequested|mCurTokenDisplayId|mBoundToMethod" | head -10
```

Expected (Q3 = 통과): `mInputShown=true`이고 IME가 결합된 디스플레이 id가 `<DID>`와 일치

Expected (Q3 = 실패): `mInputShown=false`이거나 IME의 디스플레이 id가 `0`(기본 화면)

**출력 전체를 보고서에 붙여넣는다.** 특히 IME가 기본 화면에 떴는지 보조 디스플레이에 떴는지가 핵심이다.

- [ ] **Step 6: 정리**

Run:
```bash
pkill -x scrcpy 2>/dev/null; sleep 2; pkill -9 -x scrcpy 2>/dev/null
echo "overlay : $(./tools/adb/adb -s $S shell settings get global overlay_display_devices | tr -d '\r')"
echo "displays: $(./tools/adb/adb -s $S shell dumpsys display | grep -oE 'mDisplayId=[0-9]+' | sort -u | tr '\n' ' ')"
```
Expected: Task 1의 기준값으로 복귀

- [ ] **Step 7: 판정 기록**

Q3의 답을 보고서에 한 줄로 명시한다. 커밋하지 않는다.

---

## Task 4: 스파이크 앱 뼈대와 앱 uid에서의 adbd 접속

여기서부터 코드를 쓴다. 이 Task는 **ADB 프로토콜을 전혀 다루지 않는다.** 오직 하나만 확인한다 — 일반 앱 uid의 프로세스가 자기 기기의 adb 데몬 포트에 TCP로 붙을 수 있는가.

스펙 2.6절에서 같은 확인을 했으나 그것은 `adb shell`을 통해 **shell uid**로 실행되었다. 앱 uid는 다른 SELinux 도메인이며 네트워크 정책도 다를 수 있다. 여기서 막히면 접근안 전체가 무너지므로 ADB 라이브러리를 붙이기 전에 먼저 확인한다.

**Files:**
- Create: `<스크래치>/dexwindow-spike/settings.gradle`
- Create: `<스크래치>/dexwindow-spike/build.gradle`
- Create: `<스크래치>/dexwindow-spike/gradle.properties`
- Create: `<스크래치>/dexwindow-spike/app/build.gradle`
- Create: `<스크래치>/dexwindow-spike/app/src/main/AndroidManifest.xml`
- Create: `<스크래치>/dexwindow-spike/app/src/main/java/io/github/mazemei/dexspike/SpikeActivity.java`

**Interfaces:**
- Consumes: Task 1의 빌드 환경과 기기 serial
- Produces: 설치된 스파이크 앱. `SpikeActivity`가 `--es port <N>` 인텐트 엑스트라로 받은 포트에 TCP 접속을 시도하고 결과를 화면과 logcat(`DexSpike` 태그)에 출력한다.

- [ ] **Step 1: Gradle 프로젝트 뼈대 생성**

```bash
SPIKE=/private/tmp/claude-501/-Users-dave-iWorks-DX-Manager/ac296bd2-8484-4146-a937-10d857effd0b/scratchpad/dexwindow-spike
mkdir -p "$SPIKE/app/src/main/java/io/github/mazemei/dexspike"
/bin/cp -Rf /Users/dave/iWorks/DX-Manager/DXDisplayCleanup/gradle "$SPIKE/gradle"
/bin/cp -f /Users/dave/iWorks/DX-Manager/DXDisplayCleanup/gradlew "$SPIKE/gradlew"
chmod +x "$SPIKE/gradlew"
```

`settings.gradle`:

```groovy
pluginManagement {
    repositories {
        google()
        mavenCentral()
        gradlePluginPortal()
    }
}
dependencyResolutionManagement {
    repositoriesMode.set(RepositoriesMode.FAIL_ON_PROJECT_REPOS)
    repositories {
        google()
        mavenCentral()
        maven { url "https://jitpack.io" }
    }
}
rootProject.name = "DexWindowSpike"
include ':app'
```

JitPack 저장소는 Task 5에서 필요하다. 지금 넣어두면 Task 5에서 다시 건드리지 않는다.

`build.gradle` (루트):

```groovy
plugins {
    id "com.android.application" version "8.13.2" apply false
}
```

버전 `8.13.2`는 추측이 아니라 같은 저장소의 `DXDisplayCleanup/build.gradle`이 쓰는 값이며, 같은 Gradle wrapper(8.14.5)와 JDK 17로 빌드되는 것이 Task 1에서 확인되었다.

`gradle.properties`:

```properties
org.gradle.jvmargs=-Xmx2048m
android.useAndroidX=true
```

- [ ] **Step 2: 앱 모듈 설정**

`app/build.gradle`:

```groovy
plugins {
    id 'com.android.application'
}

android {
    namespace 'io.github.mazemei.dexspike'
    compileSdk 36

    defaultConfig {
        applicationId "io.github.mazemei.dexspike"
        minSdk 30
        targetSdk 36
        versionCode 1
        versionName "0.1-spike"
    }

    compileOptions {
        sourceCompatibility JavaVersion.VERSION_17
        targetCompatibility JavaVersion.VERSION_17
    }
}

dependencies {
}
```

- [ ] **Step 3: 매니페스트 작성**

`app/src/main/AndroidManifest.xml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<manifest xmlns:android="http://schemas.android.com/apk/res/android">

    <uses-permission android:name="android.permission.INTERNET" />

    <application
        android:label="DeX Spike"
        android:allowBackup="false"
        android:usesCleartextTraffic="true">

        <activity
            android:name=".SpikeActivity"
            android:exported="true">
            <intent-filter>
                <action android:name="android.intent.action.MAIN" />
                <category android:name="android.intent.category.LAUNCHER" />
            </intent-filter>
        </activity>
    </application>
</manifest>
```

- [ ] **Step 4: 소켓 접속을 시도하는 액티비티 작성**

`app/src/main/java/io/github/mazemei/dexspike/SpikeActivity.java`:

```java
package io.github.mazemei.dexspike;

import android.app.Activity;
import android.os.Bundle;
import android.os.Process;
import android.util.Log;
import android.widget.ScrollView;
import android.widget.TextView;

import java.io.InputStream;
import java.io.OutputStream;
import java.net.InetSocketAddress;
import java.net.Socket;

public class SpikeActivity extends Activity {

    public static final String TAG = "DexSpike";

    private TextView status;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        status = new TextView(this);
        status.setTextSize(13f);
        ScrollView sv = new ScrollView(this);
        sv.addView(status);
        setContentView(sv);

        int port = getIntent().getIntExtra("port", -1);
        new Thread(() -> {
            String result = probe(port);
            runOnUiThread(() -> status.setText(result));
            Log.i(TAG, result);
        }).start();
    }

    private String probe(int port) {
        StringBuilder sb = new StringBuilder();
        sb.append("app uid: ").append(Process.myUid()).append('\n');
        sb.append("target port: ").append(port).append('\n');

        if (port <= 0) {
            sb.append("FAILED: no port supplied. "
                    + "Launch with --ei port <N>\n");
            return sb.toString();
        }

        Socket socket = null;
        try {
            socket = new Socket();
            socket.connect(new InetSocketAddress("127.0.0.1", port), 3000);
            sb.append("connect: OK\n");
            sb.append("localPort: ").append(socket.getLocalPort()).append('\n');

            // adbd 는 접속 직후 아무것도 보내지 않는다. 여기서는 소켓이
            // 열렸다는 사실만 확인하고, 프로토콜은 Task 5에서 다룬다.
            socket.setSoTimeout(1500);
            OutputStream out = socket.getOutputStream();
            out.flush();
            InputStream in = socket.getInputStream();
            try {
                int b = in.read();
                sb.append("first byte: ").append(b).append('\n');
            } catch (Exception readEx) {
                sb.append("read timed out or closed: ")
                  .append(readEx.getClass().getSimpleName())
                  .append(" (expected — adbd waits for a client message)\n");
            }
        } catch (Throwable t) {
            sb.append("FAILED: ").append(t).append('\n');
            for (StackTraceElement e : t.getStackTrace()) {
                sb.append("  at ").append(e).append('\n');
            }
        } finally {
            if (socket != null) {
                try {
                    socket.close();
                } catch (Exception ignored) {
                    // 진단에 영향 없음
                }
            }
        }
        return sb.toString();
    }
}
```

- [ ] **Step 5: 빌드**

```bash
cd "$SPIKE"
export JAVA_HOME=/Users/dave/.asdf/installs/java/temurin-17.0.8+7
export ANDROID_HOME=/Users/dave/Library/Android/sdk
bash ./gradlew --no-daemon assembleDebug
```
Expected: `BUILD SUCCESSFUL`, `app/build/outputs/apk/debug/app-debug.apk` 생성

Task 1에서 확인했듯 `gradlew`는 실행 비트가 없으므로 `bash ./gradlew`로 실행한다. 스크래치의 사본에 `chmod +x`를 했더라도 이 형태가 항상 안전하다.

- [ ] **Step 6: 현재 무선 디버깅 포트 확인**

```bash
cd /Users/dave/iWorks/DX-Manager
./tools/adb/adb mdns services
```

`_adb-tls-connect._tcp` 행의 포트를 기록한다. 이 값은 무선 디버깅을 껐다 켜거나 재부팅하면 바뀐다.

- [ ] **Step 7: 설치와 실행**

```bash
S=R5***TP
PORT=<Step 6에서 확인한 포트>
./tools/adb/adb -s $S install -r "$SPIKE/app/build/outputs/apk/debug/app-debug.apk"
./tools/adb/adb -s $S logcat -c
./tools/adb/adb -s $S shell am start -n io.github.mazemei.dexspike/.SpikeActivity --ei port $PORT
sleep 4
./tools/adb/adb -s $S logcat -d -s DexSpike
```

Expected (통과): `app uid:` 가 10000 이상(일반 앱 범위)이고 `connect: OK`

Expected (실패): `FAILED:` 와 예외. `ECONNREFUSED`면 포트가 틀렸거나 데몬이 그 포트를 안 열고 있는 것이고, `EACCES`나 SELinux 거부면 앱 uid에서 막힌 것이다. **둘을 구분해서 기록한다.**

`app uid`가 2000(shell)으로 나오면 무언가 잘못된 것이다 — 앱은 일반 uid로 실행되어야 한다.

- [ ] **Step 8: 결과 기록**

`app uid`와 `connect` 결과를 보고서에 원문으로 붙여넣는다. 커밋하지 않는다.

---

## Task 5: ADB 페어링과 셸 획득 (최대 위험)

앱이 자기 기기의 adb 데몬과 **ADB 프로토콜로** 대화해 셸을 얻을 수 있는지 확인한다. Android 11+ 무선 디버깅은 TLS와 SPAKE2 페어링을 요구하므로, 직접 구현하지 않고 검증된 라이브러리를 쓴다.

여기서 막히면 접근안을 Shizuku 의존으로 되돌려야 한다. **이 Task가 Phase 0의 핵심이다.**

**Files:**
- Modify: `<스크래치>/dexwindow-spike/app/build.gradle`
- Modify: `<스크래치>/dexwindow-spike/app/src/main/java/io/github/mazemei/dexspike/SpikeActivity.java`
- Create: `<스크래치>/dexwindow-spike/app/src/main/java/io/github/mazemei/dexspike/AdbProbe.java`

**Interfaces:**
- Consumes: Task 4의 앱 뼈대와 `SpikeActivity.TAG`
- Produces: `AdbProbe.pairAndShell(Context, String host, int pairPort, String pairCode, int connectPort, String command)` — 페어링·연결·명령 실행 결과를 문자열로 반환

- [ ] **Step 1: ADB 라이브러리 좌표 확인**

**추측하지 않는다.** libadb-android의 현재 배포 좌표를 저장소에서 확인한다.

```bash
curl -sL https://raw.githubusercontent.com/MuntashirAkon/libadb-android/master/README.md | grep -iA6 "implementation\|dependencies\|jitpack" | head -30
```

README가 제시하는 `implementation` 좌표와 필요한 추가 의존성(예: `spake2-java`, Conscrypt)을 **그대로** 기록한다. 최신 태그가 필요하면 다음으로 확인한다.

```bash
curl -sL "https://api.github.com/repos/MuntashirAkon/libadb-android/releases/latest" | grep '"tag_name"'
```

라이브러리를 가져올 수 없거나 좌표를 확정할 수 없으면 **BLOCKED로 보고한다.** 다른 라이브러리(예: `flyfishxu/Kadb`)로 임의 교체하지 않는다 — 그것은 계획 변경이며 컨트롤러가 판정할 사항이다.

- [ ] **Step 2: 의존성 추가**

`app/build.gradle`의 `dependencies` 블록을 Step 1에서 확인한 좌표로 채운다. 예시 형태는 다음과 같으나 **실제 좌표는 Step 1의 결과를 쓴다.**

```groovy
dependencies {
    implementation 'com.github.MuntashirAkon:libadb-android:<확인한 버전>'
    implementation 'com.github.MuntashirAkon:spake2-java:<확인한 버전>'
    implementation 'org.conscrypt:conscrypt-android:<확인한 버전>'
}
```

빌드가 의존성 해석에 실패하면 오류 전문을 기록하고, JitPack 저장소가 `settings.gradle`에 있는지 확인한다(Task 4 Step 1에서 이미 추가했다).

- [ ] **Step 3: 페어링·셸 프로브 작성**

`AdbProbe.java`:

```java
package io.github.mazemei.dexspike;

import android.content.Context;
import android.util.Log;

import java.io.ByteArrayOutputStream;
import java.io.InputStream;

public final class AdbProbe {

    static final String TAG = SpikeActivity.TAG;

    private AdbProbe() {
    }

    /**
     * 무선 디버깅으로 페어링한 뒤 연결해 셸 명령을 실행한다.
     * pairCode 가 비어 있으면 페어링을 건너뛰고 기존 키로 연결만 시도한다.
     */
    public static String pairAndShell(Context context,
                                      String host,
                                      int pairPort,
                                      String pairCode,
                                      int connectPort,
                                      String command) {
        StringBuilder sb = new StringBuilder();
        sb.append("host=").append(host)
          .append(" pairPort=").append(pairPort)
          .append(" connectPort=").append(connectPort)
          .append(" pairCode=").append(pairCode == null || pairCode.isEmpty()
                  ? "<none>" : "<supplied>")
          .append('\n');

        Object manager = null;
        try {
            // 라이브러리 API 는 버전마다 다를 수 있으므로 먼저 표면을 덤프한다.
            Class<?> cls = Class.forName(
                    "io.github.muntashirakon.adb.AdbConnectionManager");
            sb.append("AdbConnectionManager methods:\n");
            for (java.lang.reflect.Method m : cls.getMethods()) {
                if (m.getDeclaringClass() == cls) {
                    sb.append("  ").append(m.toGenericString()).append('\n');
                }
            }
        } catch (Throwable t) {
            sb.append("class lookup FAILED: ").append(t).append('\n');
            Log.i(TAG, sb.toString());
            return sb.toString();
        }

        try {
            manager = Class
                    .forName("io.github.muntashirakon.adb.AdbConnectionManager")
                    .getMethod("getInstance", Context.class)
                    .invoke(null, context);
            sb.append("manager: ").append(manager).append('\n');
        } catch (Throwable t) {
            sb.append("getInstance FAILED: ").append(t).append('\n');
            Throwable c = t.getCause();
            if (c != null) sb.append("cause: ").append(c).append('\n');
            Log.i(TAG, sb.toString());
            return sb.toString();
        }

        if (pairCode != null && !pairCode.isEmpty()) {
            try {
                Object r = manager.getClass()
                        .getMethod("pair", String.class, int.class, String.class)
                        .invoke(manager, host, pairPort, pairCode);
                sb.append("pair result: ").append(r).append('\n');
            } catch (Throwable t) {
                sb.append("pair FAILED: ").append(t).append('\n');
                Throwable c = t.getCause();
                if (c != null) sb.append("cause: ").append(c).append('\n');
            }
        }

        try {
            Object connected = manager.getClass()
                    .getMethod("connect", String.class, int.class)
                    .invoke(manager, host, connectPort);
            sb.append("connect result: ").append(connected).append('\n');
        } catch (Throwable t) {
            sb.append("connect FAILED: ").append(t).append('\n');
            Throwable c = t.getCause();
            if (c != null) sb.append("cause: ").append(c).append('\n');
            Log.i(TAG, sb.toString());
            return sb.toString();
        }

        try {
            Object stream = manager.getClass()
                    .getMethod("openStream", String.class)
                    .invoke(manager, "shell:" + command);
            InputStream in = (InputStream) stream.getClass()
                    .getMethod("openInputStream").invoke(stream);
            ByteArrayOutputStream bos = new ByteArrayOutputStream();
            byte[] buf = new byte[4096];
            int n;
            long deadline = System.currentTimeMillis() + 5000;
            while (System.currentTimeMillis() < deadline
                    && (n = in.read(buf)) > 0) {
                bos.write(buf, 0, n);
                if (bos.size() > 8192) break;
            }
            sb.append("shell output: ")
              .append(bos.toString("UTF-8")).append('\n');
        } catch (Throwable t) {
            sb.append("shell FAILED: ").append(t).append('\n');
            Throwable c = t.getCause();
            if (c != null) sb.append("cause: ").append(c).append('\n');
        }

        String out = sb.toString();
        Log.i(TAG, out);
        return out;
    }
}
```

리플렉션을 쓰는 이유는 라이브러리 API 시그니처를 추측하지 않기 위해서다. 첫 블록이 `AdbConnectionManager`의 실제 메서드 목록을 덤프하므로, 시그니처가 예상과 다르면 그 출력이 정답을 알려준다. **덤프 결과가 아래 호출들과 맞지 않으면 덤프에 맞춰 호출부를 고치고, 무엇을 고쳤는지 보고서에 기록한다.**

- [ ] **Step 4: 액티비티에서 호출**

`SpikeActivity.onCreate`의 `new Thread(...)` 블록을 다음으로 교체한다.

```java
        int port = getIntent().getIntExtra("port", -1);
        int pairPort = getIntent().getIntExtra("pair_port", -1);
        String pairCode = getIntent().getStringExtra("pair_code");

        new Thread(() -> {
            StringBuilder all = new StringBuilder();
            all.append(probe(port)).append("\n--- adb probe ---\n");
            all.append(AdbProbe.pairAndShell(
                    getApplicationContext(),
                    "127.0.0.1", pairPort, pairCode, port, "id; echo READY"));
            String result = all.toString();
            runOnUiThread(() -> status.setText(result));
            Log.i(TAG, result);
        }).start();
```

- [ ] **Step 5: 빌드**

```bash
cd "$SPIKE"
export JAVA_HOME=/Users/dave/.asdf/installs/java/temurin-17.0.8+7
export ANDROID_HOME=/Users/dave/Library/Android/sdk
bash ./gradlew --no-daemon assembleDebug
```
Expected: `BUILD SUCCESSFUL`

- [ ] **Step 6: 사용자에게 페어링 코드를 요청한다**

페어링에는 사용자만 얻을 수 있는 값이 필요하다. **임의로 진행하지 말고 요청한다.**

사용자에게 다음을 부탁한다.

> 폰에서 **설정 → 개발자 옵션 → 무선 디버깅 → "페어링 코드로 기기 페어링"** 을 여시고, 화면에 표시되는 **IP:포트**와 **6자리 코드**를 알려주세요. 이 대화상자는 열어둔 채로 두셔야 합니다 — 닫으면 페어링 포트가 사라집니다.

**주의**: 페어링 포트는 연결 포트(Task 4 Step 6의 `_adb-tls-connect._tcp`)와 **다른 값**이다. 둘을 혼동하면 페어링이 실패한다.

값을 받기 전까지 다음 Step으로 넘어가지 않는다.

- [ ] **Step 7: 설치와 실행**

```bash
S=R5***TP
PORT=<연결 포트>
PAIR_PORT=<페어링 대화상자의 포트>
PAIR_CODE=<6자리 코드>
cd /Users/dave/iWorks/DX-Manager
./tools/adb/adb -s $S install -r "$SPIKE/app/build/outputs/apk/debug/app-debug.apk"
./tools/adb/adb -s $S logcat -c
./tools/adb/adb -s $S shell am start -n io.github.mazemei.dexspike/.SpikeActivity \
    --ei port $PORT --ei pair_port $PAIR_PORT --es pair_code $PAIR_CODE
sleep 15
./tools/adb/adb -s $S logcat -d -s DexSpike
```

Expected (통과): `pair result:` 성공, `connect result:` 성공, `shell output:` 에 `uid=2000(shell)` 과 `READY`

Expected (실패): 어느 단계에서 무엇이 던져졌는지. 예외와 cause를 모두 기록한다.

**`shell output` 의 uid가 2000(shell)인지 확인한다.** 이것이 shell 권한 획득의 증거다.

- [ ] **Step 8: 결과 기록**

`AdbConnectionManager` 메서드 덤프, 페어링·연결·셸 각 단계의 결과를 원문으로 붙여넣는다. 페어링 코드는 일회성이지만 **보고서에 기록하지 않는다.** 커밋하지 않는다.

---

## Task 6: scrcpy-server 기동과 프레임 수신

Task 5에서 셸을 얻었다면, 그 셸로 `scrcpy-server`를 띄우고 앱이 소켓으로 H.264 프레임을 받을 수 있는지 확인한다. 이것이 접근안 C의 마지막 조각이다.

전체 디코딩과 렌더링은 Phase 1의 몫이다. 여기서는 **바이트가 실제로 도착하는지**만 확인한다.

**Files:**
- Modify: `<스크래치>/dexwindow-spike/app/src/main/java/io/github/mazemei/dexspike/AdbProbe.java`
- Modify: `<스크래치>/dexwindow-spike/app/src/main/java/io/github/mazemei/dexspike/SpikeActivity.java`

**Interfaces:**
- Consumes: Task 5의 `AdbProbe.pairAndShell(...)` 이 확립한 연결과 API 시그니처
- Produces: `AdbProbe.runScrcpyServer(Context, int connectPort, String serverPath)` — 서버 기동과 소켓 수신 결과를 문자열로 반환

- [ ] **Step 1: scrcpy-server.jar 를 기기에 올린다**

저장소가 이미 번들한 scrcpy의 서버 파일을 쓴다. 새로 내려받지 않는다.

```bash
cd /Users/dave/iWorks/DX-Manager
ls -l tools/scrcpy/scrcpy-server 2>/dev/null || find /opt/homebrew -name "scrcpy-server" 2>/dev/null | head -2
```

찾은 경로를 기기의 `/data/local/tmp/`에 올린다.

```bash
S=R5***TP
./tools/adb/adb -s $S push <찾은 경로> /data/local/tmp/scrcpy-server-spike.jar
./tools/adb/adb -s $S shell ls -l /data/local/tmp/scrcpy-server-spike.jar
```

**버전을 기록한다.** scrcpy 클라이언트와 서버 버전이 다르면 서버가 거부한다. Task 2·3에서 쓴 scrcpy는 4.1이다.

- [ ] **Step 2: 서버 기동 명령을 확인한다**

앞선 실기에서 관측한 실제 명령 형태는 다음과 같다.

```
CLASSPATH=/data/local/tmp/scrcpy-server.jar app_process / com.genymobile.scrcpy.Server 4.1 \
    scid=<8자리 hex> log_level=info audio=false new_display=1600x900/150
```

`scid`는 소켓 이름을 구분하는 임의의 8자리 16진수다. 서버는 `localabstract:scrcpy_<scid>` 이름의 abstract 소켓을 연다.

- [ ] **Step 3: 서버 기동과 소켓 수신 코드 작성**

`AdbProbe`에 다음을 추가한다.

```java
    /** scrcpy-server 를 셸로 띄우고 video 소켓에서 바이트를 받아본다. */
    public static String runScrcpyServer(Context context,
                                         int connectPort,
                                         String serverPath) {
        StringBuilder sb = new StringBuilder();
        String scid = String.format("%08x",
                new java.util.Random().nextInt(Integer.MAX_VALUE));
        sb.append("scid=").append(scid).append('\n');

        try {
            Object manager = Class
                    .forName("io.github.muntashirakon.adb.AdbConnectionManager")
                    .getMethod("getInstance", Context.class)
                    .invoke(null, context);

            String cmd = "CLASSPATH=" + serverPath
                    + " app_process / com.genymobile.scrcpy.Server 4.1"
                    + " scid=" + scid
                    + " log_level=debug audio=false"
                    + " new_display=1600x900/150";
            sb.append("cmd: ").append(cmd).append('\n');

            Object stream = manager.getClass()
                    .getMethod("openStream", String.class)
                    .invoke(manager, "shell:" + cmd);
            InputStream serverLog = (InputStream) stream.getClass()
                    .getMethod("openInputStream").invoke(stream);

            // 서버 로그를 별도 스레드에서 계속 읽는다. 읽지 않으면 서버가 막힌다.
            final StringBuilder logBuf = new StringBuilder();
            Thread logReader = new Thread(() -> {
                byte[] b = new byte[2048];
                try {
                    int n;
                    while ((n = serverLog.read(b)) > 0) {
                        synchronized (logBuf) {
                            logBuf.append(new String(b, 0, n, "UTF-8"));
                        }
                    }
                } catch (Throwable ignored) {
                    // 서버 종료 시 정상적으로 끊긴다
                }
            });
            logReader.setDaemon(true);
            logReader.start();

            Thread.sleep(4000);
            synchronized (logBuf) {
                sb.append("--- server log ---\n")
                  .append(logBuf).append("\n--- end ---\n");
            }

            // abstract 소켓에 붙는다. 이름은 서버가 여는 규약을 따른다.
            Object sockStream = manager.getClass()
                    .getMethod("openStream", String.class)
                    .invoke(manager, "localabstract:scrcpy_" + scid);
            InputStream video = (InputStream) sockStream.getClass()
                    .getMethod("openInputStream").invoke(sockStream);

            byte[] head = new byte[64];
            int got = video.read(head);
            sb.append("video socket first read: ").append(got)
              .append(" bytes\n");
            if (got > 0) {
                StringBuilder hex = new StringBuilder();
                for (int i = 0; i < Math.min(got, 32); i++) {
                    hex.append(String.format("%02x ", head[i]));
                }
                sb.append("head bytes: ").append(hex).append('\n');
            }
        } catch (Throwable t) {
            sb.append("FAILED: ").append(t).append('\n');
            Throwable c = t.getCause();
            if (c != null) sb.append("cause: ").append(c).append('\n');
            for (StackTraceElement e : t.getStackTrace()) {
                sb.append("  at ").append(e).append('\n');
            }
        }

        String out = sb.toString();
        Log.i(TAG, out);
        return out;
    }
```

`localabstract:` 스트림 열기가 이 라이브러리에서 지원되지 않으면 Step 3의 메서드 덤프(Task 5)에서 대안을 찾는다. 지원되지 않는다는 사실 자체가 유효한 결과이며, **그 경우 보고하고 임의로 우회하지 않는다.**

- [ ] **Step 4: 액티비티에서 호출**

Task 5에서 만든 스레드 블록 끝에 다음을 추가한다.

```java
            all.append("\n--- scrcpy server ---\n");
            all.append(AdbProbe.runScrcpyServer(
                    getApplicationContext(), port,
                    "/data/local/tmp/scrcpy-server-spike.jar"));
```

- [ ] **Step 5: 잠금 해제 확인 후 실행**

2.4절에 따라 잠긴 상태에서는 가상 디스플레이가 철거된다.

```bash
cd /Users/dave/iWorks/DX-Manager
S=R5***TP
./tools/adb/adb -s $S shell input keyevent KEYCODE_WAKEUP
sleep 2
./tools/adb/adb -s $S shell dumpsys window | grep -oE 'isKeyguardShowing=[a-z]+' | head -1
```
Expected: `isKeyguardShowing=false`

`true`면 사용자에게 잠금 해제를 요청하고 **BLOCKED로 보고한다.**

```bash
cd "$SPIKE"
export JAVA_HOME=/Users/dave/.asdf/installs/java/temurin-17.0.8+7
export ANDROID_HOME=/Users/dave/Library/Android/sdk
bash ./gradlew --no-daemon assembleDebug
cd /Users/dave/iWorks/DX-Manager
./tools/adb/adb -s $S install -r "$SPIKE/app/build/outputs/apk/debug/app-debug.apk"
./tools/adb/adb -s $S logcat -c
./tools/adb/adb -s $S shell am start -n io.github.mazemei.dexspike/.SpikeActivity \
    --ei port $PORT --ei pair_port $PAIR_PORT --es pair_code "$PAIR_CODE"
sleep 25
./tools/adb/adb -s $S logcat -d -s DexSpike
```

Task 5에서 이미 페어링했다면 `pair_code`를 비워도 기존 키로 연결될 수 있다. 그 경우 `--es pair_code ""`로 실행하고, 페어링 없이 연결되는지도 함께 기록한다 — **재부팅 후 자동 재연결 가능성**과 직결되는 정보다.

- [ ] **Step 6: 디스플레이가 실제로 생겼는지 독립 확인**

앱의 보고를 믿지 않고 시스템에서 확인한다.

```bash
./tools/adb/adb -s $S shell dumpsys display | grep -oE 'mDisplayId=[0-9]+' | sort -u | tr '\n' ' '
./tools/adb/adb -s $S shell dumpsys activity activities | grep -E "^Display #|topResumedActivity=" | head -10
```

새 디스플레이가 있으면 그 위에 `SecondaryLauncher`가 붙었는지도 확인한다. 붙었다면 2.3절이 앱 기동 경로에서도 재현된 것이다.

- [ ] **Step 7: 정리**

```bash
./tools/adb/adb -s $S shell am force-stop io.github.mazemei.dexspike
./tools/adb/adb -s $S shell "pkill -f com.genymobile.scrcpy.Server" 2>/dev/null
sleep 3
./tools/adb/adb -s $S shell rm -f /data/local/tmp/scrcpy-server-spike.jar
./tools/adb/adb -s $S shell pm uninstall io.github.mazemei.dexspike
sleep 2
echo "overlay : $(./tools/adb/adb -s $S shell settings get global overlay_display_devices | tr -d '\r')"
echo "displays: $(./tools/adb/adb -s $S shell dumpsys display | grep -oE 'mDisplayId=[0-9]+' | sort -u | tr '\n' ' ')"
```
Expected: 앱 제거됨, 임시 jar 제거됨, 디스플레이가 Task 1 기준선(`0 1`)으로 복귀

디스플레이가 남아 있으면 **누수가 재현된 것이다.** 스펙 3.5절의 우려가 실증된 것이므로 반드시 기록한다.

- [ ] **Step 8: 결과 기록**

서버 로그, 소켓 수신 바이트 수와 앞부분 hex, 디스플레이 확인 결과를 원문으로 붙여넣는다. 커밋하지 않는다.

---

## Task 7: 판정과 스펙 반영

세 검증의 답을 스펙에 기록하고 접근안 유지 여부를 판정한다. **이것이 Phase 0의 실제 산출물이다.**

**Files:**
- Modify: `docs/superpowers/specs/2026-09-04-ondevice-dex-window-design.md`

**Interfaces:**
- Consumes: Task 4·5·6의 결과
- Produces: 갱신된 스펙, Phase 1 착수 가능 여부

- [ ] **Step 1: 스파이크 잔여물 제거 확인**

```bash
cd /Users/dave/iWorks/DX-Manager
S=R5***TP
./tools/adb/adb -s $S shell pm list packages | grep dexspike || echo "앱 제거됨"
./tools/adb/adb -s $S shell ls /data/local/tmp/ | grep -i scrcpy-server-spike || echo "임시 jar 제거됨"
echo "overlay : $(./tools/adb/adb -s $S shell settings get global overlay_display_devices | tr -d '\r')"
echo "stay_on : $(./tools/adb/adb -s $S shell settings get global stay_on_while_plugged_in | tr -d '\r')"
echo "displays: $(./tools/adb/adb -s $S shell dumpsys display | grep -oE 'mDisplayId=[0-9]+' | sort -u | tr '\n' ' ')"
```
Expected: 앱과 jar 제거됨, `overlay: null`, `stay_on: 0`, 디스플레이 `0 1`

**페어링으로 추가된 ADB 키는 남는다.** 이것은 정상이며 제거하지 않는다 — 사용자가 원하면 개발자 옵션에서 "무선 디버깅 승인 취소"로 지울 수 있다는 점을 보고서에 안내한다.

- [ ] **Step 2: 스펙 5.1절 표 갱신**

`docs/superpowers/specs/2026-09-04-ondevice-dex-window-design.md`의 5.1절 표에서 미검증 4행의 상태를 실제 결과로 바꾼다.

변경 전:
```
| **일반 앱 uid에서 adb 데몬 접속** | ❌ 미검증 |
| **앱 안에서 ADB 페어링·RSA 인증 구현** | ❌ 미검증 — 최대 위험 |
| **앱이 scrcpy-server를 기동하고 소켓으로 프레임 수신** | ❌ 미검증 |
| **재부팅 후 ADB 키 보존과 자동 재연결** | ❌ 미검증 |
```

각 행을 `✅ 검증 (Phase 0)` 또는 `❌ 불가 — <이유>`로 바꾸고 근거를 한 줄 덧붙인다. 재부팅 항목은 이번 Phase에서 재부팅을 하지 않았다면 **미검증으로 남긴다.** 하지 않은 것을 했다고 적지 않는다.

- [ ] **Step 3: 스펙 8절 열린 항목 갱신**

해소된 항목은 취소선과 `— **해결됨(날짜).** <결론>` 형식으로 바꾼다. 기존 항목들이 이미 이 형식을 쓴다.

새로 발견된 위험이 있으면 추가한다. 특히 Task 6 Step 7에서 디스플레이 누수가 재현되었다면 반드시 기록한다.

- [ ] **Step 4: 판정 절 추가**

스펙 5절 끝에 다음 형식으로 절을 추가한다.

```markdown
### 5.5 Phase 0 판정 (2026-09-XX)

기기: Galaxy Z Fold (SM-F971N), Android 17 (SDK 37), One UI 9.0

| 검증 | 결과 |
| :--- | :--- |
| 일반 앱 uid에서 adbd 접속 | <결과> |
| 앱 내 ADB 페어링·인증 | <결과> |
| scrcpy-server 기동과 프레임 수신 | <결과> |

**결론**: 접근안(scrcpy 자체 기동)을 유지한다 / Shizuku 의존으로 되돌린다.

<판정 근거 2~3문장>
```

`<결과>`와 `<판정 근거>`는 실제 측정값으로 채운다. 추측을 적지 않는다.

- [ ] **Step 5: 커밋**

```bash
cd /Users/dave/iWorks/DX-Manager
git add docs/superpowers/specs/2026-09-04-ondevice-dex-window-design.md
git commit -m "docs: record Phase 0 spike results for on-device DeX window

앱 uid adbd 접속, ADB 페어링·인증, scrcpy-server 기동 검증 결과를 스펙
5.1절과 8절에 반영하고 5.5절에 판정을 추가한다. 스파이크 코드는 스크래치
디렉터리에서만 작업했고 저장소에 남기지 않는다."
```

- [ ] **Step 6: 스파이크 디렉터리 삭제**

```bash
rm -rf /private/tmp/claude-501/-Users-dave-iWorks-DX-Manager/ac296bd2-8484-4146-a937-10d857effd0b/scratchpad/dexwindow-spike
```

판정이 스펙에 기록되었으므로 코드는 더 이상 필요 없다. 이것이 스파이크의 정의다.

---

## Self-Review 결과

**1. 스펙 coverage**

| 스펙 요구 | 담당 Task |
| :--- | :--- |
| 5.1 — 일반 앱 uid에서 adbd 접속 | Task 4 |
| 5.1 — 앱 내 ADB 페어링·인증 | Task 5 |
| 5.1 — scrcpy-server 기동과 프레임 수신 | Task 6 |
| 5.1 — 재부팅 후 재연결 | Task 6 Step 5에서 부분 관찰(페어링 없이 재연결), 재부팅 자체는 범위 밖 |
| 2.3 삼성 런처 부착 | Task 2에서 확정, Task 6 Step 6에서 앱 기동 경로 재확인 |
| 2.5 IME 미표시 | Task 3에서 확정 |
| 3.5 세션 정리 우려 | Task 6 Step 7에서 누수 실증 여부 확인 |
| 2.4 잠금 제약 | Task 2·3·6의 각 시작 Step에서 확인 |

Phase 1~5는 이 계획의 범위 밖이다.

**2. Placeholder 스캔**

Task 5 Step 2의 라이브러리 좌표와 Step 3의 호출부는 Step 1이 확인한 실제 값에 맞추도록 되어 있다. 이는 미완성이 아니라 **의도된 순서**다 — 외부 라이브러리의 버전과 시그니처를 추측해 적는 것이 오히려 계획 실패다. 리플렉션 덤프가 그 자리에서 정답을 알려주므로 코드 자체는 완결되어 있다.

그 외 "TBD", "적절히 처리", 코드 없는 코드 단계는 없다.

**3. 타입 일관성**

- `SpikeActivity.TAG`(Task 4에서 `public static final`)를 `AdbProbe`가 참조한다.
- `AdbProbe.pairAndShell(Context, String, int, String, int, String)`(Task 5) → `runScrcpyServer(Context, int, String)`(Task 6)는 같은 클래스에 정의되고 `SpikeActivity`의 같은 스레드 블록에서 순서대로 호출된다.
- `status` 필드와 `probe(int)` 메서드는 Task 4에서 정의되고 Task 5·6에서 같은 이름으로 쓰인다.

**4. 계획 작성 중 확인 완료된 사항**

- JDK 17.0.8, Android platform-36, build-tools 36.1.0이 이 Mac에 있다(Task 1에서 실증).
- AGP 8.13.2 + Gradle wrapper 8.14.5 + JDK 17 조합이 `DXDisplayCleanup`에서 빌드된다(Task 1에서 실증).
- `gradlew`는 실행 비트가 없어 `bash ./gradlew`로 실행해야 한다(Task 1에서 실증).
- 기기가 자기 adb 데몬(localhost)에 접속 가능하다 — 단 shell uid 기준(스펙 2.6절).
- ADB 라이브러리로 `MuntashirAkon/libadb-android`가 존재하며 `pair(host, port, code)`와 `openStream("shell:")` API를 제공한다. **정확한 배포 좌표와 버전은 Task 5 Step 1에서 확인한다.**

**5. 남은 실행 시 위험**

- libadb-android의 메서드 시그니처가 리플렉션 덤프와 다를 수 있다. Task 5 Step 3이 덤프를 먼저 출력하므로 그 자리에서 교정 가능하다.
- 이 라이브러리가 `localabstract:` 스트림을 지원하지 않으면 Task 6이 막힌다. 그 경우 대안은 TCP 포워딩이나, 지원 여부 자체가 유효한 결과이므로 보고하고 판정을 받는다.
- 페어링 코드는 사용자만 얻을 수 있고 대화상자를 닫으면 무효가 된다. Task 5 Step 6이 이를 명시한다.
- scrcpy 서버 버전(4.1)과 기동 인자가 scrcpy 4.1 기준이다. 다른 버전의 jar를 쓰면 서버가 거부한다.
