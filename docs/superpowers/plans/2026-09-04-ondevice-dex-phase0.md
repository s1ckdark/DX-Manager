# 온디바이스 DeX 창 Phase 0 — 기술 스파이크 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 온디바이스 DeX 창 설계의 미검증 위험 3건에 답을 내고, 접근안 A(직접 Surface 연결)를 계속 갈지 접근안 C(scrcpy-server 내장)로 선회할지 판정한다.

**Architecture:** 이것은 **버리는 코드를 쓰는 스파이크**다. 산출물은 앱이 아니라 답이다. 가장 싸고 정보량이 큰 실험부터 배치한다 — 위험 2건은 코드 없이 `scrcpy`와 `adb`만으로 답하고, 나머지 1건(Shizuku 경유 Surface 전달)에만 스파이크 앱을 만든다.

**Tech Stack:** Android (Java, minSdk 30, compileSdk 36), Gradle 8.14.5, Shizuku API, adb, scrcpy 4.1

**Spec:** `docs/superpowers/specs/2026-09-04-ondevice-dex-window-design.md`

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

기기는 무선 ADB로 붙는다. USB 케이블은 앞서 인식되지 않았다.

```bash
cd /Users/dave/iWorks/DX-Manager
./tools/adb/adb mdns services          # _adb-tls-connect._tcp 항목의 IP:PORT 확인
./tools/adb/adb connect <IP>:<PORT>
./tools/adb/adb devices
```

이 Mac은 해당 기기와 이미 페어링되어 있어 페어링 코드가 필요 없다. 포트는 재부팅·재활성화 시 바뀌므로 매번 `mdns services`로 다시 확인한다.

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
| `.../app/src/main/java/.../SpikeActivity.java` | 전체화면 SurfaceView, Shizuku 상태 표시 | 생성 |
| `.../app/src/main/java/.../DisplayManagerProbe.java` | `IDisplayManager` 시그니처 덤프와 호출 | 생성 |
| `docs/superpowers/specs/2026-09-04-ondevice-dex-window-design.md` | 판정 반영 | 수정 |

---

## Task 1: 환경 준비와 기준선

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

## Task 4: 스파이크 앱 뼈대와 Shizuku 연결

여기서부터 코드를 쓴다. Q1(Surface 전달)만이 코드로 답할 수 있는 질문이다.

이 Task는 **디스플레이를 아직 만들지 않는다.** Shizuku에 연결해 shell 권한을 실제로 얻는 데까지만 간다. 여기서 막히면 이후가 무의미하므로 단계를 나눈다.

**Files:**
- Create: `<스크래치>/dexwindow-spike/settings.gradle`
- Create: `<스크래치>/dexwindow-spike/build.gradle`
- Create: `<스크래치>/dexwindow-spike/gradle.properties`
- Create: `<스크래치>/dexwindow-spike/app/build.gradle`
- Create: `<스크래치>/dexwindow-spike/app/src/main/AndroidManifest.xml`
- Create: `<스크래치>/dexwindow-spike/app/src/main/java/io/github/mazemei/dexspike/SpikeActivity.java`

**Interfaces:**
- Consumes: Task 1의 빌드 환경과 기기 serial
- Produces: 설치된 스파이크 앱, `SpikeActivity`가 Shizuku 권한 보유 상태를 화면과 logcat에 보고

- [ ] **Step 1: Gradle 프로젝트 뼈대 생성**

스크래치 디렉터리를 만든다.

```bash
SPIKE=/private/tmp/claude-501/-Users-dave-iWorks-DX-Manager/ac296bd2-8484-4146-a937-10d857effd0b/scratchpad/dexwindow-spike
mkdir -p "$SPIKE/app/src/main/java/io/github/mazemei/dexspike"
cd "$SPIKE"
```

Gradle wrapper는 기존 Companion의 것을 복사해 버전을 맞춘다.

```bash
cp -R /Users/dave/iWorks/DX-Manager/DXDisplayCleanup/gradle "$SPIKE/gradle"
cp /Users/dave/iWorks/DX-Manager/DXDisplayCleanup/gradlew "$SPIKE/gradlew"
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
    }
}
rootProject.name = "DexWindowSpike"
include ':app'
```

`build.gradle` (루트):

```groovy
plugins {
    id "com.android.application" version "8.13.2" apply false
}
```

버전 `8.13.2`는 추측이 아니라 같은 저장소의 `DXDisplayCleanup/build.gradle`이 실제로 쓰는 값이다. 그 프로젝트가 같은 Gradle wrapper(8.14.5)와 같은 JDK 17로 빌드되므로 조합이 검증되어 있다.

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
    implementation 'dev.rikka.shizuku:api:13.1.5'
    implementation 'dev.rikka.shizuku:provider:13.1.5'
}
```

AGP 8.13.2는 `DXDisplayCleanup`에서 검증된 조합이므로 여기서 실패할 가능성은 낮다. 그래도 실패하면 오류 메시지가 요구하는 버전으로 바꾸고 **무엇으로 바꿨는지 보고서에 기록한다.**

- [ ] **Step 3: 매니페스트 작성**

`app/src/main/AndroidManifest.xml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<manifest xmlns:android="http://schemas.android.com/apk/res/android">

    <uses-permission android:name="moe.shizuku.manager.permission.API_V23" />

    <application
        android:label="DeX Spike"
        android:allowBackup="false">

        <activity
            android:name=".SpikeActivity"
            android:exported="true">
            <intent-filter>
                <action android:name="android.intent.action.MAIN" />
                <category android:name="android.intent.category.LAUNCHER" />
            </intent-filter>
        </activity>

        <provider
            android:name="rikka.shizuku.ShizukuProvider"
            android:authorities="${applicationId}.shizuku"
            android:multiprocess="false"
            android:enabled="true"
            android:exported="true"
            android:permission="android.permission.INTERACT_ACROSS_USERS_FULL" />
    </application>
</manifest>
```

- [ ] **Step 4: Shizuku 상태를 보고하는 액티비티 작성**

`app/src/main/java/io/github/mazemei/dexspike/SpikeActivity.java`:

```java
package io.github.mazemei.dexspike;

import android.app.Activity;
import android.os.Bundle;
import android.util.Log;
import android.widget.TextView;

import rikka.shizuku.Shizuku;

public class SpikeActivity extends Activity {

    static final String TAG = "DexSpike";
    private static final int REQ = 1000;

    private TextView status;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        status = new TextView(this);
        status.setTextSize(16f);
        setContentView(status);

        Shizuku.addRequestPermissionResultListener(
                (requestCode, grantResult) -> report());
        report();
    }

    private void report() {
        StringBuilder sb = new StringBuilder();
        boolean ping = false;
        try {
            ping = Shizuku.pingBinder();
        } catch (Throwable t) {
            sb.append("pingBinder threw: ").append(t).append('\n');
        }
        sb.append("binder alive: ").append(ping).append('\n');

        if (ping) {
            int uid = -1;
            int version = -1;
            try {
                uid = Shizuku.getUid();
                version = Shizuku.getVersion();
            } catch (Throwable t) {
                sb.append("uid/version threw: ").append(t).append('\n');
            }
            sb.append("shizuku uid: ").append(uid)
              .append(" (2000 = shell)\n");
            sb.append("shizuku version: ").append(version).append('\n');

            boolean granted = false;
            try {
                granted = Shizuku.checkSelfPermission()
                        == android.content.pm.PackageManager.PERMISSION_GRANTED;
            } catch (Throwable t) {
                sb.append("checkSelfPermission threw: ").append(t).append('\n');
            }
            sb.append("permission granted: ").append(granted).append('\n');

            if (!granted) {
                try {
                    Shizuku.requestPermission(REQ);
                } catch (Throwable t) {
                    sb.append("requestPermission threw: ").append(t).append('\n');
                }
            }
        }

        String text = sb.toString();
        status.setText(text);
        Log.i(TAG, text);
    }
}
```

- [ ] **Step 5: 빌드**

Run:
```bash
cd "$SPIKE"
export JAVA_HOME=/Users/dave/.asdf/installs/java/temurin-17.0.8+7
export ANDROID_HOME=/Users/dave/Library/Android/sdk
./gradlew --no-daemon assembleDebug
```
Expected: `BUILD SUCCESSFUL`, `app/build/outputs/apk/debug/app-debug.apk` 생성

- [ ] **Step 6: Shizuku 설치 여부 확인**

Run:
```bash
cd /Users/dave/iWorks/DX-Manager
./tools/adb/adb -s $S shell pm list packages | grep -i shizuku
```

`moe.shizuku.privileged.api`가 없으면 **사용자에게 Shizuku 설치와 실행을 요청하고 BLOCKED로 보고한다.** 임의로 설치하지 않는다 — 사용자의 개인 기기다.

설치되어 있으면 실행 중인지 확인한다.

```bash
./tools/adb/adb -s $S shell ps -A | grep -i shizuku
```

- [ ] **Step 7: 스파이크 앱 설치와 실행**

Run:
```bash
./tools/adb/adb -s $S install -r "$SPIKE/app/build/outputs/apk/debug/app-debug.apk"
./tools/adb/adb -s $S shell am start -n io.github.mazemei.dexspike/.SpikeActivity
sleep 3
./tools/adb/adb -s $S logcat -d -s DexSpike | tail -20
```

Expected: `binder alive: true`, `shizuku uid: 2000 (2000 = shell)`, `permission granted: true`

권한 요청 대화상자가 뜨면 사용자에게 승인을 요청한다.

`binder alive: false`면 Shizuku가 실행 중이 아니다. 사용자에게 무선 디버깅으로 Shizuku를 시작해 달라고 요청한다(스펙 2.5절).

- [ ] **Step 8: 결과 기록**

`shizuku uid`가 **2000**인지가 핵심이다. 이것이 shell 권한 확보의 증거다. logcat 출력 전체를 보고서에 붙여넣는다. 커밋하지 않는다.

---

## Task 5: `IDisplayManager` 시그니처 런타임 덤프

`createVirtualDisplay`는 hidden API이며 Android 버전마다 시그니처가 다르다. **추측해서 호출하지 않는다.** 이 기기의 실제 시그니처를 런타임에 열거해 확인한 뒤 Task 6에서 호출한다.

**Files:**
- Create: `<스크래치>/dexwindow-spike/app/src/main/java/io/github/mazemei/dexspike/DisplayManagerProbe.java`
- Modify: `<스크래치>/dexwindow-spike/app/src/main/java/io/github/mazemei/dexspike/SpikeActivity.java`

**Interfaces:**
- Consumes: Task 4의 Shizuku 연결 (`Shizuku.checkSelfPermission()`이 GRANTED)
- Produces: `DisplayManagerProbe.dumpSignatures()` — `IDisplayManager`의 `*VirtualDisplay*` 메서드 시그니처를 logcat에 출력

- [ ] **Step 1: 프로브 클래스 작성**

`DisplayManagerProbe.java`:

```java
package io.github.mazemei.dexspike;

import android.os.IBinder;
import android.util.Log;

import java.lang.reflect.Method;

import rikka.shizuku.ShizukuBinderWrapper;
import rikka.shizuku.SystemServiceHelper;

public final class DisplayManagerProbe {

    static final String TAG = SpikeActivity.TAG;

    private DisplayManagerProbe() {
    }

    /** IDisplayManager 인터페이스의 VirtualDisplay 관련 메서드를 전부 출력한다. */
    public static String dumpSignatures() {
        StringBuilder sb = new StringBuilder();
        try {
            IBinder raw = SystemServiceHelper.getSystemService("display");
            sb.append("raw binder: ").append(raw).append('\n');

            IBinder wrapped = new ShizukuBinderWrapper(raw);
            sb.append("wrapped: ").append(wrapped).append('\n');

            Class<?> stub =
                    Class.forName("android.hardware.display.IDisplayManager$Stub");
            Method asInterface = stub.getMethod("asInterface", IBinder.class);
            Object dm = asInterface.invoke(null, wrapped);
            sb.append("IDisplayManager: ").append(dm).append('\n');

            Class<?> iface =
                    Class.forName("android.hardware.display.IDisplayManager");
            for (Method m : iface.getMethods()) {
                if (m.getName().toLowerCase().contains("virtualdisplay")) {
                    sb.append("METHOD ").append(m.toGenericString()).append('\n');
                }
            }

            // VirtualDisplayConfig 의 존재와 Builder 메서드도 확인한다.
            try {
                Class<?> cfg = Class.forName(
                        "android.hardware.display.VirtualDisplayConfig$Builder");
                for (Method m : cfg.getMethods()) {
                    if (m.getDeclaringClass() == cfg) {
                        sb.append("CFG ").append(m.toGenericString()).append('\n');
                    }
                }
            } catch (Throwable t) {
                sb.append("VirtualDisplayConfig$Builder absent: ")
                  .append(t).append('\n');
            }
        } catch (Throwable t) {
            sb.append("FAILED: ").append(t).append('\n');
            for (StackTraceElement e : t.getStackTrace()) {
                sb.append("  at ").append(e).append('\n');
            }
        }
        String out = sb.toString();
        Log.i(TAG, out);
        return out;
    }
}
```

- [ ] **Step 2: 액티비티에서 호출**

`SpikeActivity.report()`의 마지막, `status.setText(text)` 앞에 다음을 추가한다.

```java
            if (granted) {
                sb.append("--- display manager probe ---\n");
                sb.append(DisplayManagerProbe.dumpSignatures());
            }
```

`granted` 변수는 `if (ping) { ... }` 블록 안에서 선언되어 있으므로, 이 코드도 같은 블록 안 마지막에 넣는다.

- [ ] **Step 3: 빌드와 설치**

Run:
```bash
cd "$SPIKE"
export JAVA_HOME=/Users/dave/.asdf/installs/java/temurin-17.0.8+7
export ANDROID_HOME=/Users/dave/Library/Android/sdk
./gradlew --no-daemon assembleDebug
cd /Users/dave/iWorks/DX-Manager
./tools/adb/adb -s $S install -r "$SPIKE/app/build/outputs/apk/debug/app-debug.apk"
./tools/adb/adb -s $S logcat -c
./tools/adb/adb -s $S shell am start -n io.github.mazemei.dexspike/.SpikeActivity
sleep 4
./tools/adb/adb -s $S logcat -d -s DexSpike
```

- [ ] **Step 4: 시그니처 판독**

logcat에서 `METHOD` 로 시작하는 줄을 전부 보고서에 붙여넣는다. 여기에 `createVirtualDisplay`의 정확한 파라미터 목록이 나온다.

`FAILED:` 가 출력되면 그 예외와 스택트레이스 전체를 기록한다. `ClassNotFoundException`이면 hidden API 차단(non-SDK interface restriction)일 수 있으며, 그 경우 **이것이 Q1에 대한 부정적 답**이다. 임의로 우회를 시도하지 말고 보고한다.

- [ ] **Step 5: 결과 기록**

커밋하지 않는다.

---

## Task 6: Surface로 VirtualDisplay 생성 시도 (Q1 본편)

Task 5에서 확인한 실제 시그니처로 호출한다. 이것이 접근안 A의 성립 여부를 가른다.

**Files:**
- Modify: `<스크래치>/dexwindow-spike/app/src/main/java/io/github/mazemei/dexspike/SpikeActivity.java`
- Modify: `<스크래치>/dexwindow-spike/app/src/main/java/io/github/mazemei/dexspike/DisplayManagerProbe.java`

**Interfaces:**
- Consumes: Task 5의 `dumpSignatures()` 출력에서 확인한 `createVirtualDisplay` 시그니처
- Produces: Q1 판정 (Surface 전달로 디스플레이 생성 성공 여부)

- [ ] **Step 1: SurfaceView를 화면에 올린다**

`SpikeActivity`의 `onCreate`에서 `setContentView(status)`를 다음으로 교체한다.

```java
        android.widget.FrameLayout root = new android.widget.FrameLayout(this);

        surfaceView = new android.view.SurfaceView(this);
        root.addView(surfaceView, new android.widget.FrameLayout.LayoutParams(
                android.widget.FrameLayout.LayoutParams.MATCH_PARENT,
                android.widget.FrameLayout.LayoutParams.MATCH_PARENT));

        root.addView(status, new android.widget.FrameLayout.LayoutParams(
                android.widget.FrameLayout.LayoutParams.MATCH_PARENT,
                android.widget.FrameLayout.LayoutParams.WRAP_CONTENT));

        setContentView(root);

        surfaceView.getHolder().addCallback(new android.view.SurfaceHolder.Callback() {
            @Override
            public void surfaceCreated(android.view.SurfaceHolder holder) {
                Log.i(TAG, "surfaceCreated");
            }

            @Override
            public void surfaceChanged(android.view.SurfaceHolder holder,
                                       int format, int width, int height) {
                Log.i(TAG, "surfaceChanged " + width + "x" + height);
            }

            @Override
            public void surfaceDestroyed(android.view.SurfaceHolder holder) {
                Log.i(TAG, "surfaceDestroyed");
                DisplayManagerProbe.release();
            }
        });
```

필드를 클래스 상단에 추가한다.

```java
    private android.view.SurfaceView surfaceView;
```

- [ ] **Step 2: 생성 메서드 작성**

`DisplayManagerProbe`에 다음을 추가한다. **Task 5에서 확인한 시그니처에 맞춰 호출부를 조정한다.** 아래는 Android 14 이후의 일반적 형태이며, 실제 시그니처가 다르면 그쪽을 따른다.

```java
    private static Object heldCallback;
    private static int createdDisplayId = -1;

    /** Surface를 백엔드로 TRUSTED 가상 디스플레이를 만든다. 성공 시 displayId 반환. */
    public static String createDisplay(android.view.Surface surface,
                                       int width, int height, int dpi) {
        StringBuilder sb = new StringBuilder();
        try {
            IBinder wrapped = new ShizukuBinderWrapper(
                    SystemServiceHelper.getSystemService("display"));
            Class<?> stub =
                    Class.forName("android.hardware.display.IDisplayManager$Stub");
            Object dm = stub.getMethod("asInterface", IBinder.class)
                    .invoke(null, wrapped);

            // IVirtualDisplayCallback 은 콜백만 받는 인터페이스이므로
            // 동적 프록시로 최소 구현을 만든다.
            Class<?> cbIface = Class.forName(
                    "android.hardware.display.IVirtualDisplayCallback");
            heldCallback = java.lang.reflect.Proxy.newProxyInstance(
                    cbIface.getClassLoader(),
                    new Class<?>[]{cbIface},
                    (proxy, method, args) -> {
                        Log.i(TAG, "callback: " + method.getName());
                        if (method.getName().equals("asBinder")) {
                            return new android.os.Binder();
                        }
                        return null;
                    });

            // VirtualDisplayConfig 구성
            Class<?> builderCls = Class.forName(
                    "android.hardware.display.VirtualDisplayConfig$Builder");
            Object builder = builderCls
                    .getConstructor(String.class, int.class, int.class, int.class)
                    .newInstance("dexspike", width, height, dpi);

            int flags = flag("VIRTUAL_DISPLAY_FLAG_PUBLIC")
                    | flag("VIRTUAL_DISPLAY_FLAG_PRESENTATION")
                    | flag("VIRTUAL_DISPLAY_FLAG_TRUSTED")
                    | flag("VIRTUAL_DISPLAY_FLAG_OWN_DISPLAY_GROUP")
                    | flag("VIRTUAL_DISPLAY_FLAG_SHOULD_SHOW_SYSTEM_DECORATIONS");
            sb.append("flags: 0x").append(Integer.toHexString(flags)).append('\n');

            builderCls.getMethod("setFlags", int.class).invoke(builder, flags);
            builderCls.getMethod("setSurface", android.view.Surface.class)
                    .invoke(builder, surface);
            Object config = builderCls.getMethod("build").invoke(builder);

            // createVirtualDisplay 호출 — Task 5에서 확인한 시그니처를 사용한다.
            Method create = null;
            for (Method m : dm.getClass().getInterfaces()[0].getMethods()) {
                if (m.getName().equals("createVirtualDisplay")) {
                    create = m;
                    sb.append("using: ").append(m.toGenericString()).append('\n');
                    break;
                }
            }
            if (create == null) {
                sb.append("FAILED: createVirtualDisplay not found\n");
                Log.i(TAG, sb.toString());
                return sb.toString();
            }

            Object[] callArgs = buildArgs(create, config, heldCallback);
            Object result = create.invoke(dm, callArgs);
            createdDisplayId = ((Integer) result);
            sb.append("createdDisplayId: ").append(createdDisplayId).append('\n');
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

    /** 파라미터 순서에 맞춰 인자를 채운다. 모르는 타입은 null 로 둔다. */
    private static Object[] buildArgs(Method m, Object config, Object callback) {
        Class<?>[] types = m.getParameterTypes();
        Object[] args = new Object[types.length];
        for (int i = 0; i < types.length; i++) {
            String n = types[i].getName();
            if (n.endsWith("VirtualDisplayConfig")) {
                args[i] = config;
            } else if (n.endsWith("IVirtualDisplayCallback")) {
                args[i] = callback;
            } else if (n.equals("java.lang.String")) {
                args[i] = "io.github.mazemei.dexspike";
            } else if (types[i] == int.class) {
                args[i] = 0;
            } else {
                args[i] = null;
            }
        }
        return args;
    }

    private static int flag(String name) throws Exception {
        return android.hardware.display.DisplayManager.class
                .getField(name).getInt(null);
    }

    public static void release() {
        heldCallback = null;
        createdDisplayId = -1;
    }
```

`flag(...)`가 `NoSuchFieldException`을 던지면 그 상수가 이 SDK에 공개되어 있지 않다는 뜻이다. 값을 하드코딩하지 말고 **어떤 상수가 없었는지 기록하고 그 플래그를 제외한 채 다시 시도한다.** 어떤 조합으로 성공했는지가 중요한 결과다.

- [ ] **Step 3: 액티비티에서 호출**

`surfaceChanged` 콜백 안에서 호출한다. Surface가 유효해진 시점이다.

```java
            @Override
            public void surfaceChanged(android.view.SurfaceHolder holder,
                                       int format, int width, int height) {
                Log.i(TAG, "surfaceChanged " + width + "x" + height);
                String r = DisplayManagerProbe.createDisplay(
                        holder.getSurface(), 1600, 900, 150);
                status.setText(r);
            }
```

- [ ] **Step 4: 빌드·설치·실행**

Run:
```bash
cd "$SPIKE"
export JAVA_HOME=/Users/dave/.asdf/installs/java/temurin-17.0.8+7
export ANDROID_HOME=/Users/dave/Library/Android/sdk
./gradlew --no-daemon assembleDebug
cd /Users/dave/iWorks/DX-Manager
./tools/adb/adb -s $S shell input keyevent KEYCODE_WAKEUP
./tools/adb/adb -s $S install -r "$SPIKE/app/build/outputs/apk/debug/app-debug.apk"
./tools/adb/adb -s $S logcat -c
./tools/adb/adb -s $S shell am start -n io.github.mazemei.dexspike/.SpikeActivity
sleep 5
./tools/adb/adb -s $S logcat -d -s DexSpike
```

Expected (Q1 = 통과): `createdDisplayId:` 뒤에 0보다 큰 숫자

Expected (Q1 = 실패): `FAILED:` 와 예외

- [ ] **Step 5: 디스플레이가 실제로 생겼는지 독립 확인**

앱의 보고를 믿지 않고 시스템에서 직접 확인한다.

```bash
./tools/adb/adb -s $S shell dumpsys display | grep -B2 -A2 "dexspike" | head -20
./tools/adb/adb -s $S shell dumpsys activity activities | grep -E "^Display #|topResumedActivity=" | head -12
```

디스플레이가 목록에 있으면 **플래그를 기록한다.** `FLAG_TRUSTED`가 실제로 붙었는지가 Q3(IME)와 직결된다.

그 디스플레이에 `SecondaryLauncher`가 붙었는지도 확인한다. 붙었다면 Task 2의 정황 증거가 확정된다.

- [ ] **Step 6: 화면에 실제로 렌더링되는지 확인**

기기 화면을 캡처해 SurfaceView 영역에 디스플레이 내용이 보이는지 확인한다.

```bash
./tools/adb/adb -s $S exec-out screencap -p > /tmp/spike-render.png
```

캡처 파일을 열어 확인하고 결과를 보고서에 기록한다. 검은 화면이면 디스플레이는 만들어졌으나 합성이 되지 않는 것이며, 이는 별개의 문제로 기록한다.

- [ ] **Step 7: 정리**

Run:
```bash
./tools/adb/adb -s $S shell am force-stop io.github.mazemei.dexspike
sleep 2
./tools/adb/adb -s $S shell dumpsys display | grep -oE 'mDisplayId=[0-9]+' | sort -u | tr '\n' ' '
```
Expected: Task 1의 기준 디스플레이 목록으로 복귀

남아 있으면 **누수가 재현된 것이다.** 스펙 3.5절의 우려가 실증된 것이므로 반드시 기록하고, 다음으로 정리한다.

```bash
./tools/adb/adb -s $S shell pm uninstall io.github.mazemei.dexspike
sleep 2
./tools/adb/adb -s $S shell dumpsys display | grep -oE 'mDisplayId=[0-9]+' | sort -u | tr '\n' ' '
```

- [ ] **Step 8: 결과 기록**

커밋하지 않는다.

---

## Task 7: 판정과 스펙 반영

세 질문의 답을 스펙에 기록하고 접근안 유지 여부를 판정한다. **이것이 Phase 0의 실제 산출물이다.**

**Files:**
- Modify: `docs/superpowers/specs/2026-09-04-ondevice-dex-window-design.md`

**Interfaces:**
- Consumes: Task 2·3·6의 판정
- Produces: 갱신된 스펙, Phase 1 착수 가능 여부

- [ ] **Step 1: 스파이크 앱 제거 확인**

Run:
```bash
cd /Users/dave/iWorks/DX-Manager
./tools/adb/adb -s $S shell pm list packages | grep dexspike || echo "제거됨"
echo "overlay : $(./tools/adb/adb -s $S shell settings get global overlay_display_devices | tr -d '\r')"
echo "stay_on : $(./tools/adb/adb -s $S shell settings get global stay_on_while_plugged_in | tr -d '\r')"
echo "displays: $(./tools/adb/adb -s $S shell dumpsys display | grep -oE 'mDisplayId=[0-9]+' | sort -u | tr '\n' ' ')"
```
Expected: 앱 제거됨, Task 1 Step 4의 기준값과 동일

- [ ] **Step 2: 스펙 5.1절 표 갱신**

`docs/superpowers/specs/2026-09-04-ondevice-dex-window-design.md`의 5.1절 표에서 세 행의 상태를 실제 결과로 바꾼다.

변경 전:
```
| **Surface를 shell 프로세스에 전달해 디스플레이 생성** | ❌ 미검증 — 최대 위험 |
| **앱이 만든 디스플레이에도 런처가 붙는가** | ⚠️ 정황 증거만 (2.3절) |
| **보조 디스플레이 IME 표시** | ❌ 미검증 |
```

변경 후에는 각 행을 `✅ 검증 (Phase 0)` 또는 `❌ 불가 — <이유>`로 바꾸고, 근거가 되는 명령과 출력 요지를 한 줄로 덧붙인다.

- [ ] **Step 3: 스펙 8절 열린 항목 갱신**

해소된 항목은 취소선과 함께 `— **해결됨(날짜).** <결론>` 형식으로 바꾼다. 스펙의 기존 항목들이 이미 이 형식을 쓰고 있다.

새로 발견된 위험이 있으면 항목을 추가한다. 특히 Task 6 Step 7에서 디스플레이 누수가 재현되었다면 반드시 기록한다.

- [ ] **Step 4: 판정 절 추가**

스펙 5절 끝에 다음 형식으로 절을 추가한다.

```markdown
### 5.4 Phase 0 판정 (2026-09-XX)

기기: Galaxy Z Fold (SM-F971N), Android 17 (SDK 37), One UI 9.0

| 질문 | 결과 |
| :--- | :--- |
| Q1 Surface 전달로 디스플레이 생성 | <결과> |
| Q2 삼성 보조 런처 부착 | <결과> |
| Q3 보조 디스플레이 IME | <결과> |

**결론**: 접근안 A를 유지한다 / 접근안 C로 선회한다.

<판정 근거 2~3문장>
```

`<결과>`와 `<판정 근거>`는 실제 측정값으로 채운다. 추측을 적지 않는다.

- [ ] **Step 5: 커밋**

```bash
cd /Users/dave/iWorks/DX-Manager
git add docs/superpowers/specs/2026-09-04-ondevice-dex-window-design.md
git commit -m "docs: record Phase 0 spike results for on-device DeX window

Q1/Q2/Q3 검증 결과를 스펙 5.1절과 8절에 반영하고 5.4절에 판정을 추가한다.
스파이크 코드는 스크래치 디렉터리에서만 작업했고 저장소에 남기지 않는다."
```

- [ ] **Step 6: 스파이크 디렉터리 삭제**

Run:
```bash
rm -rf /private/tmp/claude-501/-Users-dave-iWorks-DX-Manager/ac296bd2-8484-4146-a937-10d857effd0b/scratchpad/dexwindow-spike
```

판정이 스펙에 기록되었으므로 코드는 더 이상 필요 없다. 이것이 스파이크의 정의다.

---

## Self-Review 결과

**1. 스펙 coverage**

| 스펙 요구 | 담당 Task |
| :--- | :--- |
| 5.1 미검증 위험 — Surface 전달 | Task 5, 6 |
| 5.1 미검증 위험 — 런처 부착 | Task 2 (+ Task 6 Step 5에서 재확인) |
| 5.1 미검증 위험 — IME 표시 | Task 3 |
| 6절 Phase 0 완료 조건 | Task 7 |
| 3.5 디스플레이 누수 우려 | Task 6 Step 7에서 실증 여부 확인 |
| 2.4 잠금 상태 제약 | Task 2·3·6의 각 Step 1에서 확인 |

Phase 1~5는 이 계획의 범위 밖이며 별도 계획으로 작성한다.

**2. Placeholder 스캔**

Task 6 Step 2의 `createVirtualDisplay` 호출부는 Task 5가 덤프한 실제 시그니처에 맞춰 조정하도록 되어 있다. 이는 미완성이 아니라 **의도된 순서**다 — hidden API 시그니처를 추측해 적는 것이 오히려 계획 실패다. 인자 채우기는 `buildArgs`가 리플렉션으로 처리하므로 코드 자체는 완결되어 있다.

그 외 "TBD", "적절히 처리", 코드 없는 코드 단계는 없다.

**3. 타입 일관성**

- `DisplayManagerProbe.dumpSignatures()`(Task 5) → `createDisplay(Surface, int, int, int)`(Task 6) → `release()`(Task 6 Step 1의 `surfaceDestroyed`) 모두 같은 클래스에 정의되고 같은 이름으로 호출된다.
- `SpikeActivity.TAG`를 `DisplayManagerProbe`가 참조하므로 Task 4에서 `static final`로 선언해 둔다.
- `status`와 `surfaceView` 필드는 Task 4와 Task 6에서 같은 이름을 쓴다.

**4. 계획 작성 중 확인 완료된 사항**

- JDK 17.0.8이 `/Users/dave/.asdf/installs/java/temurin-17.0.8+7`에 설치되어 있다.
- Android platform-36과 build-tools 36.1.0이 `~/Library/Android/sdk`에 있다.
- AGP **8.13.2** + Gradle wrapper 8.14.5 + JDK 17 조합이 `DXDisplayCleanup`에서 실제로 사용 중이다. 추측값이 아니다.
- 저장소가 `google()` + `mavenCentral()`을 `FAIL_ON_PROJECT_REPOS` 모드로 쓰는 패턴을 따랐다.

**5. 남은 실행 시 위험**

- `dev.rikka.shizuku:api:13.1.5` 버전이 최신인지 확인하지 않았다. 빌드 실패 시 Maven Central에서 확인해 조정하고 기록한다.
- Shizuku가 기기에 설치되어 실행 중인지 확인하지 않았다. Task 4 Step 6에서 확인하며, 없으면 사용자에게 요청하고 BLOCKED로 보고한다. **개인 기기이므로 임의 설치는 금지한다.**
- Android 17의 non-SDK interface restriction이 `IDisplayManager` 접근 자체를 막을 수 있다. Task 5에서 `ClassNotFoundException`이나 `NoSuchMethodException`으로 드러나며, 그것은 Q1에 대한 유효한 부정적 답이다. 우회를 시도하지 않는다.
- Task 6의 동적 프록시로 만든 `IVirtualDisplayCallback`이 실제 Binder 전송을 견디지 못할 수 있다. `asBinder`가 반환하는 `Binder`가 올바른 인터페이스 디스크립터를 갖지 않기 때문이다. 이 경우 예외 메시지를 기록하고, 실제 `Binder` 서브클래스로 디스크립터를 맞추는 것을 다음 시도로 남긴다.
