# DX Manager Store update handoff — 2026-10-05

> Historical scope — current documentation at [the documentation index](README.md). This dated record describes its own build/artifact/state; current2.5.0 behavior and checksums are in the user guides and verification record.
> 과거 시점의 작업 기록입니다. 현재 동작·파일 해시는 문서 목차의2.5.0 안내를 따릅니다.


## English summary

The user accepted the portable Fluent icon trial and requested documentation so the Store edition can be updated later. This handoff covers Companion 2.1.4, its earlier2.1.3 queued-request cleanup,31 Fluent Regular assets including a derived mouse, mini-bar transport indicators, and the final launch/reconnect rules. It also identifies portable UI refinements to compare with Store before porting.
This documentation task does not change Store product code, its APK, any submitted package, or Partner Center. Store validation must be performed separately; portable results are not Store test results.

## 1. 범위와 현재 기준

메이님은 포터블 아이콘 적용본을 확인한 뒤 “스토어판 수정할 수 있게 정리해서 문서화 — APK 문제, 아이콘 등”을 요청했다. 다음 Store 작업은 이 문서를 인계 자료로 사용한다.

| 구분 | 2026-10-05에 직접 확인한 기준 | 이번 작업 범위 |
|---|---|---|
| 포터블 원본 | `E:\vs\dex system-portable-backport`, `feature/portable-backport-v2.0.1-20260928`; Windows 앱2.0.1 | 완료한 소스·자원·검증 근거 제공 |
| Store 대상 | `E:\vs\dex system-store`, `store/winui3`; .NET 10/WinUI 3, Windows 10/11 | 인계 문서 작성; 제품 코드는 수정하지 않음 |
| Store 로컬 Companion | 코드2.1.2/versionCode 9, 실제 로컬 번들 SHA256도 기존 검증값과 일치 | 다음 업데이트에서2.1.4로 교체할 대상 |
| 새 Companion | 공식 서명2.1.4/versionCode 11, Android 13/API33 이상 | 서명된 동일 APK 재사용 우선 |
| 제출된 Store 패키지 | 이번 작업에서 MSIX/Partner Center 상태를 조회하지 않음 | 로컬 코드 상태를 제출물 상태로 단정하지 않음 |

Store에는 기존 미커밋 변경이 있다. 작업 시작 때 Git 상태를 다시 확인하고 보존한다. 이 문서는 두 저장소에 동일하게 보관한다. 공통 Git object database가 있더라도 전체 branch merge나 폴더 통째 덮어쓰기로 이식하지 않는다.

## 2. 다음 작업의 우선순위

| 순서 | 항목 | 이식 유형 | 완료 기준 |
|---|---|---|---|
| A | Companion 2.1.4 번들·PC 검증값 | 검증된 바이너리 재사용 | 실제 publish/MSIX 안 APK까지 버전·서명·해시 일치 |
| B | Android 소스·테스트 동기화 | 차이 확인 후 필요한 부분 재사용 | 새 APK와 관리 중인 Android 소스의 차이를 기록·해소 |
| C | Fluent 아이콘·파생 마우스 | 자원 재사용, WinUI 표시 현대화 | 밝음/다크·DPI·접근성·패키지 자원 검증 |
| D | 미니바 연결 상태 | Store의 HWND/기기 격리를 유지한 현대화 | USB/Wi-Fi/동시 연결 및 갱신·기기 분리 |
| E | 실행·자동 실행·UI 후속 | Store 현상/소스 비교 후 필요한 부분만 | 이미 정상인 Store 기능을 보존하며 최종 규칙 확인 |

먼저 `AGENTS.md`와 `docs/STORE_PORTING_WORKFLOW.md` 전체를 읽고 Store 전용 문서·빌드 절차를 따른다. 이 문서 작성은 문서 인계이며, Store 코드 구현 완료·빌드·제출 승인을 뜻하지 않는다. 현재 제출물을 이 문서만으로 다시 제출하지 않는다.

## 3. Companion APK: 정확한 파일과 검증값

| 항목 | 새 번들 기준 |
|---|---|
| 파일 | `E:\vs\dex system-portable-backport\DXDisplayCleanup\bundled\DX-Companion-v2.1.4.apk` |
| 전달 APK | `E:\vs\dex system\dist\DX Companion 2.1.4\DX-Companion-v2.1.4.apk` |
| 전달 ZIP | `E:\vs\dex system\dist\DX-Companion-v2.1.4.zip` |
| Android package | `io.github.mazemei.dxdisplaycleanup` |
| versionName / versionCode | `2.1.4` / `11` |
| minSdk / targetSdk | `33` / `36` |
| APK SHA256 | `95DAF4E734FD36FE07AD8388FE8E4C98D96D1EFA0A8E359D8C8E3E27383B6536` |
| 인증서 SHA256 | `AD615803C63760439750C36801E8152AB8664C60EE481EF1473F1DF5E80733BE` |
| ZIP SHA256 | `FE4C5E84F4D98C43ED2A98F3484D73DFA1812BDACC50967BAECD44D19E74B51B` |
| 서명 | 기존 공식 RSA4096 인증서, APK Signature Scheme v2 |

현재 Store 코드와 로컬 번들은2.1.2/code9를 기대하며 해시는 `D5B09491B2DDEE0358EC0A678994256E814A9434250650E11052A930A21D9854`다. 이는 이번에 확인한 로컬 기준이다.

### 3.1 꼭 함께 갱신할 Store 위치

- `DexManager.Store/Services/DisplayCleanupPermissionService.cs`: `BundledVersionCode`, `BundledVersionName`, `ExpectedApkSha256`. package·인증서는 유지한다.
- `DexManager.Store/DexManager.Store.csproj`: 현재 APK 공급원이 `../dist/DX Manager/tools/companion/DX-Companion.apk`이며 output/publish로 복사한다. 원본 APK만 바꿔놓고 이 공급 경로가 구버전인 채 남지 않게 한다.
- Store 로컬 `dist/DX Manager/tools/companion/DX-Companion.apk`, unpackaged preview의 같은 경로, 최종 MSIX에 포함된 APK를 각각 확인한다.
- `scripts/Package-AndroidCleanup.ps1` 및 관련 배포·검증 문서의 기대 버전·해시·파일명.
- 기존 APK 설치/업데이트/권한·Guardian 경로: 버전 변경 이후 정상 작동하는지 확인한다. 정확한 package·버전·인증서 검증을 느슨하게 바꾸지 않는다.
- 설치된 더 최신 Companion을 구버전으로 자동 강등하지 않는다. 이미2.1.4가 설치된 휴대폰도 확인한다.

서명 비밀, keystore, `signing.properties`와 비밀번호는 문서·Git·패키지에 복사하지 않는다. 기존 공식 서명 APK를 그대로 재사용하면 Store 작업에서 임의 재서명할 필요가 없다. APK 자체가 달라지면2.1.4의 고정 해시를 그대로 사용하지 말고 새 파일을 다시 검증한다.

### 3.2 이름 충돌 수정 —2.1.4

원인: `PhoneTransferPlanner.sanitizeSegment()`의 `trim()`과 `/`, `\` 치환 뒤 서로 다른 형제 항목의 경로가 같아질 수 있었다. Store 로컬 소스에는 아직 형제 이름 할당기가 없다.

이것은 실제 문서 제공자/API가 만든 표시 이름의 드문 경계 사례다. 일반 탐색기 파일 전송에서 손실이 재현됐다는 뜻이 아니다. `/`, `\` 예시는 제공자 표시 이름의 치환을 설명하며, 일반 Windows 파일명에 해당 문자를 넣을 수 있다는 뜻도 아니다.

| 원래 표시 이름 예시 | 필요한 결과 |
|---|---|
| `photo.txt`, ` photo.txt`, `photo.txt ` | 정리 뒤 같은 이름이 되어도 서로 다른 전송 항목으로 유지 |
| `A/B`, `A_B`, `A\B` | `_` 치환 후 충돌을 번호로 분리 |
| `file.txt`, ` file.txt`, 기존 `file (1).txt` | 기존 번호 이름을 예약해서 새 이름이 먼저 차지하지 않음 |
| 같은 이름이 된 폴더와 파일 | 동일 부모 안에서 한 이름 공간으로 분리 |
| `사진  .txt` | 확장자 앞의 내부 공백 유지 |
| 이름을 바꾼 폴더의 자식 | 변경된 부모 경로를 이어받되 원래 URI·내용 보존 |

포터블 참조: `DXDisplayCleanup/app/src/main/java/io/github/mazemei/dxdisplaycleanup/PhoneTransferPlanner.java`, `TransferSiblingNames.java`, `app/src/test/java/.../TransferSiblingNamesTest.java`.

수정은 부모별로 자연스럽게 정리된 이름을 먼저 예약하고, 중복에 번호를 붙인 뒤 재귀 탐색한다. 파일은 확장자 앞, 폴더는 이름 뒤에 번호를 붙인다. rootId, 원본 content URI, 파일 내용과 DXMR v1 프로토콜은 유지한다. Windows 수신부의 기존 대소문자·금지문자 충돌 처리를 제거하거나 중복으로 리팩터링하지 않는다.

### 3.3 대기 전송 종료 보강 —2.1.3부터 포함

2.1.4에는2.1.3의 수정도 포함된다. 실행 중 작업뿐 아니라 아직 실행되지 않은 대기 요청도 서비스 종료/timeout 때 정리한다. 공통 취소 플래그를 다음 작업이 다시 풀어버리는 경계도 보강했다.

참조 파일:

- `PhoneTransferService.java`: `onStartCommand`, `onDestroy`, `onTimeout`, 실행·대기 요청의 완료/종료 처리.
- `TransferRequestTracker.java`, `TransferRequestTrackerTest.java`: 요청별 취소와 일회 완료/정리 추적.
- 요청 이력, URI grant, stream/socket, wake lock와 foreground-service 종료의 기존 계약.

Store2.1.2에 `onDestroy/onTimeout`이 이미 있다는 사실만으로 이 보강까지 반영됐다고 판정하지 않는다. 전체 대기 요청과 중복 finally 경계를 비교한다. 테스트 통과와 Android 15+ 실제 dataSync timeout 재현은 구분한다.

### 3.4 Android 소스 관리

Store의 `DXDisplayCleanup`은 로컬2.1.2 소스다. 바이너리만 교체한다면 소스 동기화가 아직 남았음을 기록한다. 소스를 함께 동기화할 때에는 위 변경 및 관련 테스트만 비교해서 반영하고 Gradle versionCode/name도 맞춘다. 현재 사용자 승인 범위와 Store의 Companion 변경 규칙을 확인해 독립 APK 작업으로 처리한다. 다른 worktree의 포터블 원본이나 서명 비밀을 변경하지 않는다.

## 4. Fluent UI System Icons

### 4.1 재사용할 자원과 보존할 것

- 공식 저장소: `https://github.com/microsoft/fluentui-system-icons`
- 고정 source commit: `a563cf9166f4f91aa617557ed272612b7f0a2f72`
- 포터블 자원: `E:\vs\dex system-portable-backport\DexManager\Resources\FluentIcons`
- `svg/`: 원본 SVG 및 분리한 마우스 SVG.
- `sources.json`: 공식 이름·원본 URL; 포함된 로컬 경로는 포터블 출처이며 Store 실행 경로로 하드코딩하지 않는다.
- `README.md`: PNG별 SHA256, 변환·파생 설명.
- `DexManager/licenses/Fluent-System-Icons-LICENSE.txt`: 완전한 MIT 고지.
- `DexManager/Forms/FluentIconRenderer.cs`, `DesktopSurfaces.cs`: 포터블의 색상·기존 렌더러 보존 참고.

작은 메뉴·버튼은 Regular 중심으로 통일했다. 현재31종이 포함돼 있다. 제품 로고와 큰 휴대폰 그림은 유지했다. 기존 그림/렌더러는 삭제하지 않았고, 수정 전 소스 사본은 `.build-tools/fluent-apply-20261005-212848/before`에 있다. 이 사본과 `.build-tools` 프리뷰는 로컬 참고물이며 Git/배포물에 자동 포함되는 것으로 생각하지 않는다.

### 4.2 마우스는 파생 자원임

확인한 공식 카탈로그에는 단독 물리 마우스가 없고 `Keyboard Mouse`가 있다. 포인터 `Cursor`를 마우스로 둔갑시키지 않는다.

- 원본: `svg/Mouse-keyboard-original.svg`, 공식 `Keyboard Mouse 20 Regular`.
- 파생: `svg/Mouse.svg`, `Mouse.png`.
- 원본의 마우스 외곽·내곽 두 윤곽만 유지하고 키보드를 제거했다.
- 가운데 정렬용 viewBox는 `12 0 8 12`다.
- 새 윤곽을 직접 그린 자원도, 공식적으로 별도 제공되는 Mouse 아이콘도 아니다. 같은 파생 자원을 Store에 재사용하고 출처·수정 사실을 유지한다.

### 4.3 Store 표시 방식

WinUI 3 본문에는 현재 `FontIcon`이 있다. Windows 폰트 의존 코드를 통째로 바꾸기보다 검증한 아이콘 자원과 역할을 재사용한다. 투명 PNG를 `BitmapIcon/BitmapIconSource`의 monochrome 처리와 테마 Foreground로 쓰거나, 기존 WinUI 자원 구조에 맞는 SVG 표시를 사용한다. 포터블 GDI 렌더러를 WinUI 본문에 억지로 옮기지 않는다.

Store 미니바는 `DexManager.Store/Forms/MiniControlBarForm.cs`의 WinForms/GDI 창이다. 이 부분은 포터블 렌더러 방식의 직접 재사용을 검토할 수 있지만 namespace·embedded resource 이름과 Store의 기존 수명주기를 맞춘다.

대상 파일:

- `DexManager.Store/MainPage.xaml`, `MainPage.xaml.cs`
- `SettingsWindow.xaml`, `SettingsWindow.xaml.cs`, 도움말·라이선스 창의 기존 아이콘
- `Styles/DesignTokens.xaml`
- `Forms/MiniControlBarForm.cs`, `Services/MiniControlBarManager.cs`
- `DexManager.Store.csproj`의 자원 output/publish/MSIX 포함과 라이선스 복사

구체적인 주의점:

- FontIcon 전용 Style을 다른 타입에 그대로 적용하지 않는다. 아이콘 타입과 크기·색상 스타일을 맞춘다.
- 기존 글리프의 `Translation` 기준선 보정을 새 bitmap/SVG에 그대로 적용하지 말고 실제 정렬을 확인한다.
- 아이콘은 라벨과 세로 중심을 맞추고 메인/설정/도움말의 기존 패딩·제목 배치를 보존한다.
- Light/Dark/High Contrast, Windows 10/11 및100/125/150/200% DPI를 확인한다.
- URI/자원 경로가 unpackaged preview와 MSIX에서 모두 동작해야 한다.
- MIT 저작권·허가 고지와 마우스 변경 설명을 source와 실제 배포물에 포함한다.
- 런타임 아이콘 다운로드, 새 사용자용 아이콘 설정, 원본 로고/큰 휴대폰 그림의 교체는 이번 승인 디자인에 포함되지 않는다.

### 4.4 적용 자원 대응표

| 포터블 symbol | 공식 이름 | 재사용 자원 |
|---|---|---|
| Display | Desktop | Display.png / svg/Display.svg |
| Launch | Play | Launch.png / svg/Launch.svg |
| Phone | Phone | Phone.png / svg/Phone.svg |
| Keyboard | Keyboard | Keyboard.png / svg/Keyboard.svg |
| Mouse | Keyboard Mouse | Mouse.png / svg/Mouse.svg |
| Moon | Weather Moon | Moon.png / svg/Moon.svg |
| Close | Dismiss Circle | Close.png / svg/Close.svg |
| Settings | Settings | Settings.png / svg/Settings.svg |
| Help | Question Circle | Help.png / svg/Help.svg |
| Refresh | Arrow Clockwise | Refresh.png / svg/Refresh.svg |
| List | Document Bullet List | List.png / svg/List.svg |
| Link | Link | Link.png / svg/Link.svg |
| Folder | Folder Open | Folder.png / svg/Folder.svg |
| Information | Info | Information.png / svg/Information.svg |
| Power | Power | Power.png / svg/Power.svg |
| Language | Translate | Language.png / svg/Language.svg |
| Palette | Dark Theme | Palette.png / svg/Palette.svg |
| Usb | USB Plug | Usb.png / svg/Usb.svg |
| Wifi | WiFi 1 | Wifi.png / svg/Wifi.svg |
| Menu | Navigation | Menu.png / svg/Menu.svg |
| Chevron | Chevron Down | Chevron.png / svg/Chevron.svg |
| Options | Play Settings | Options.png / svg/Options.svg |
| Apps | Apps List | Apps.png / svg/Apps.svg |
| ScreenOff | Phone Dismiss | ScreenOff.png / svg/ScreenOff.svg |
| ScreenOn | Phone | ScreenOn.png / svg/ScreenOn.svg |
| Fullscreen | Arrow Expand | Fullscreen.png / svg/Fullscreen.svg |
| ResetSize | Arrow Autofit Content | ResetSize.png / svg/ResetSize.svg |
| Capture | Screenshot | Capture.png / svg/Capture.svg |
| Manager | Phone Desktop | Manager.png / svg/Manager.svg |
| ChevronUp | Chevron Up | ChevronUp.png / svg/ChevronUp.svg |
| Save | Save | Save.png / svg/Save.svg |

## 5. 메인·미니바 연결 상태와 실행 규칙

### 5.1 관측된 연결과 실행 serial을 분리

사용자 표시에는 같은 물리 identity의 authorized USB/Wi-Fi를 집계한다. 설정의 mode 한 값이나 실행 중인 serial 한 값만으로 동시 연결 표시를 만들지 않는다.

| 실제 관측 | 표시 |
|---|---|
| USB만 authorized | USB Plug + `USB` |
| Wi-Fi만 authorized | WiFi 1 + `Wi-Fi` |
| 둘 다 authorized | 두 아이콘 + `USB + Wi-Fi` |
| unauthorized/offline | 연결됐다고 집계하지 않음 |

Store 로컬 `MainViewModel.TransportIconGlyph`는 현재 **명령에 선택된 transport**를 표현하며 USB-first 설명이 있다. 이 표시와 모든 연결 집계를 구분한다. Store에 이미 있는 USB 우선/재연결 로직을 포터블 로직으로 덮어쓰지 말고, 아래 요구를 충족하는지 비교한다.

### 5.2 최종 실행·재연결 규칙

1. 새 DeX/단일창 시작은 선택한 물리 기기 안에서 authorized USB 우선, USB가 없으면 authorized Wi-Fi.
2. 이미 실행/시작 중인 세션은 캡처한 serial을 유지한다. USB를 추가로 꽂았다고 기존 Wi-Fi 창을 중지·이동하지 않는다.
3. USB 세션 실행 중 USB가 빠지면 그 USB 세션을 종료한다. 남은 Wi-Fi로 즉시 자동 실행/이동하지 않는다.
4. 자동 DeX 시작 설정이 ON이면 USB를 다시 꽂거나 완전히 끊겼다가 재연결할 때 기존 자동 실행을 복구한다. OFF면 자동 실행하지 않는다.
5. 재연결된 USB가 다시 새 실행 대상으로 선택돼야 한다. 연결 해제 표식이 영구히 남으면 안 된다.
6. 다른 휴대폰·다른 serial의 세션/전송/정리를 중지하지 않는다. 늦게 도착한 종료 콜백도 캡처한 세션에 한정한다.

포터블에서 처음 추가한 `RequireManualStartAfterTransportLoss`가 재연결 후에도 남아 자동 실행을 막는 회귀가 있었다. 최종본은 잃었던 serial의 복귀 또는 새 물리 연결 시 차단을 푼다. **초기의 “한 번 끊기면 무조건 수동 시작” 버전을 복사하지 않는다.**

`STORE_PORTING_WORKFLOW.md`의 과거 “반대 transport로 fallback하지 않는다” 문구와 비교할 때, 2026-10-05 사용자의 새 시작 USB 우선 요구 및 기존 세션 유지 규칙을 구분한다. 무선 주소/재연결 설정과 새 세션 실행 선택은 역할이 다르다. 정책 문서는 최종 사용자 요구와 맞춰 갱신하되 이미 정상인 Store 동작을 불필요하게 다시 구현하지 않는다.

포터블 참고: `Models/PhysicalDeviceInfo.cs`의 `SelectLaunchTransport/SelectRuntimeTransport`; `Forms/MainForm.DeviceConnections.cs`, `MainForm.DeviceConnectionState.cs`, `MainForm.Sessions.cs`; `Services/DexOrchestrator.cs`의 `StopForSerialAsync`; `SingleWindowService.cs`의 `StopForSerial`.

Store 확인 위치: `ViewModels/MainViewModel.cs`, `MainPage.xaml.cs`의 `DeviceAutoStartPlanner`와 auto-start 요청, `MainWindow.xaml.cs`, `Services/DeviceRuntimeServiceBinding.cs`, `Models/PhysicalDeviceModels.cs`. 실제 시작/정리 serial은 실행 로그로 확인한다.

### 5.3 미니바

- 이번 사용자의 추가 요구에 미니바도 포함됐다. 기존 command icon 교체뿐 아니라 USB/Wi-Fi/동시 연결 상태 표시와 갱신이 대상이다.
- `PhysicalDeviceRegistry` snapshot을 기기별 manager로 전달하고 기존 bar들을 갱신한다. 새 bar도 최신 상태를 처음부터 적용한다.
- 한 기기의 상태가 다른 기기의 bar를 바꾸지 않게 한다. 표시 이름이 같아도 identity/manager로 분리한다.
- command의 PID/HWND/serial 캡처, 포커스·최소화·창 위치 추적 및 화면/캡처/종료 동작을 유지한다.
- 펼친 bar에서 상태를 보여주고 접힌 bar의 기존 크기와 동작을 보존한다. 포터블의44px 값을 Store에 무조건 강제하지 않는다.
- 포터블 참고: `Forms/MiniControlBarForm.cs`, `Services/MiniControlBarManager.cs`, `MainForm.DeviceConnections.cs`.
- 실제 세션에서 bar 생성 후 USB/Wi-Fi를 추가·해제하고, 다른 기기의 bar도 동시에 확인한다.

### 5.4 숨겨진 창에서의 표시 배치

포터블 프리뷰에서 `Control.Visible`이 부모 창의 숨김 여부까지 포함해 USB 배지 글자와 아이콘이 겹치는 문제가 발견됐다. 최종본은 연결 flag로 배치한다. Store도 tray에 숨긴 동안 상태가 바뀐 뒤 다시 열었을 때 겹침·잘림이 없는지 확인한다. WinForms의 픽셀 계산식을 WinUI에 그대로 복사하지 않는다.

## 6. 기타 사용자 요구: Store 비교 체크리스트

아래는 포터블에서 수정/확인한 UX다. **모두 Store에도 같은 버그가 있다는 목록은 아니다.** 이미 맞는 Store 항목은 완료/유지로 분류하고 필요한 것만 적용한다.

| 항목 | 최종 요구/확인할 내용 | Store 주의점 |
|---|---|---|
| 단일창 시작 앱 | 미선택이면 시작 처리와 ADB·wake-up을 시작 전에 막음; DeX는 앱 미선택 실행 가능 | Store는 이미 미선택 버튼 비활성화/서비스 검증이 있음. gray 클릭 안내 UX는 별도 비교, WinForms Enabled 트릭을 그대로 이식하지 않음 |
| 오류 안내 | 해당 기기·슬롯의 시작 앱을 고르면 그 미선택 오류만 닫힘; unrelated 오류는 유지; 수동 닫기 가능 | 테마 변경 후에도 색/투명도/모서리 배경이 안정적이어야 함 |
| 준비 상태 | `DeX 실행 준비됨`; 선택 앱이 있으면 `단일창 N 준비됨`, 없으면 `단일창 N 대기중` | 한·영 문자열 키와 기기별 모드 상태 분리 |
| 메인 세 카드 | 디스플레이 설정·실행 옵션·시작 앱의 설명 줄 제거, 제목을 왼쪽 아이콘과 같은 중심선에 맞춤 | 사용자 승인된 포터블 디자인과 비교; 기존 Store 폰트/레이아웃 기반 유지 |
| 사용자 해상도 | 4K 다음 nonselectable `사용자 지정 해상도`, 최근 숫자 항목, 맨 아래 `사용자 지정...` | 구분선을 선택값으로 저장/표시하지 않음; 숫자는 즉시 반영; 별도 버튼 추가 아님 |
| 사용자 해상도 대화상자 | 열릴 때 owner 앞에 활성화, 가로 숫자 바로 선택/편집 가능 | Store 창 활성화 방식에 맞춤 |
| 설정 경로 | ADB·Scrcpy 경로 칸 시작 위치/폭을 저장 폴더와 정렬 | 현 WinUI grid/stretch 규칙으로 확인 |
| 기기 정보·환경 검사 | 미연결 문구 전체 표시와 불필요한 빈 공간 제거; 연결 후 카드/결과도 안 잘림 | 한/두 기기·긴 텍스트·폭 측정과 동적 높이 확인 |
| Companion 상태 | 위 요약과 중복되는 번들 검증·설치 상태 **표시 행** 정리 | 실제 검증/설치 기능을 삭제하지 않음 |
| 정보 탭 메일 | 다크 모드에서도 읽히는 링크 색 | ThemeResource/접근성 대비 확인 |
| 스크롤·닫기 버튼 | 스크롤 유무로 오른쪽 끝이 흔들리거나 가려지지 않음; 휠과 thumb 동기화; 닫기 오른쪽 정렬 | WinUI 기본 ScrollViewer를 레거시 scrollbar로 교체하지 않음 |
| 둥근 카드/버튼 | 모서리에 다른 배경/거친 clipping이 생기지 않음 | WinUI native round/clip/Mica 구조를 유지하며 실제 화면 확인 |
| 도움말 | 타일 내용 수직 중앙, 아이콘과 문구 간격 | 새 Fluent 크기에 맞는 기준선/패딩 확인 |
| 고급 옵션 | 처음부터 예시 표시, 토글 위치 안정 | 기본 placeholder/확장 레이아웃 확인 |
| One UI9 정보 | 사용자 확인한 S26 Ultra/Android17 SDK37/One UI9.0에 정상 사용 문구 | 전체 기기·새 버전을 일괄 검증됨으로 표시하지 않음 |

포터블의 매 실행 동의창은 메이님 요청에 따른 검토용 상태다. 이를 Store 배포 정책으로 자동 이식하지 않는다. Store에서 동의창 프리뷰를 확인할 때는 이미 있는 `build-store-preview.ps1 -ShowFirstRunNotice`를 사용하고, 기존 versioned onboarding 저장/일반 설정 초기화 분리를 유지한다.

## 7. 검증 근거와 아직 남은 확인

| 구분 | 근거/상태 |
|---|---|
| 새 APK 자동 검증 | 2026-10-05 Android20 unit tests, Release lint/assemble, package/API/version/permissions/v2 signature/certificate 검증 기록 |
| APK 파일 현재 확인 | 이 문서 작성 시2.1.4 APK/ZIP 해시 재조회, Store 로컬 구버전 APK/코드와 비교 |
| 포터블 실행 파일 | SHA256 `A219A3A61079140A0205B5D06C20FE2B6BFBD062CD3B78B61296C5D7158846FA`; .NET4.6.2 x64 Release |
| 포터블 자동 검증 |62 foundation regressions;31 자원·투명도·tint; 미니바 세 상태/갱신/기기별 분리/unauthorized/접힘; USB 재연결·즉시 폴백 차단·자동 실행OFF |
| UI 프리뷰 | `E:\vs\dex system-portable-backport\.build-tools\ui-review\20261005-215353`;44 PNG의 디코딩 확인, `verification.txt`, `verified-build.txt` |
| 사용자 확인 | 재연결 자동 실행 보정 후 “오케이 됐다”; Fluent 적용본 후 “오케이 됐다” 및 Store 인계 요청 |
| 아직 미확인 | 실제 문서 제공자의 충돌/하위 폴더 전송, Android timeout/대기 정리, 모든 실제 미니바/복수 기기 경계·DPI·OS 조합 |
| Store 검증 | 이번 문서 작업에서 Store build/test/publish/MSIX 검증 실행 안 함. 포터블 테스트 숫자를 Store 통과 숫자로 사용하지 않음 |

프리뷰는 실제 포터블 컨트롤을 synthetic 기기 및 명령 차단으로 렌더링한 것이다. 실제 휴대폰 전송/ADB 성공 증거가 아니다. 포터블 테스트용 GDI PNG 저장 우회, WinForms scrollbar/각종 픽셀 좌표 및 내부 비교 flag를 Store 제품 코드에 복사하지 않는다.

### 7.1 Store 구현 후 반드시 확인

- `scripts/build-store-preview.ps1`로 표준 x64 프리뷰 생성. 일반 publish만 하면 ADB proxy 패키징을 빠뜨릴 수 있다.
- Store foundation 및 기존 다중 기기 tests: 현재 checkout의 실제 수를 다시 기록.
- `scripts/Test-StorePackage.ps1` 등 현 Store 절차로 output/MSIX 필수 파일 확인.
- `tools/adb-proxy/DXMAdbProxy.exe`, Scrcpy 및 새 Companion APK 포함·정확한 경로.
- 자원/라이선스가 unpackaged preview 및 MSIX에 모두 포함, icon 요청 시 외부 네트워크 없음.
- 밝음/다크/고대비, 한국어/영어, Windows 10/11과100~200% DPI.
- 실제 두 기기: USB-only/Wi-Fi-only/both, manual/auto DeX·single, USB 해제, USB 재연결, 모두 해제 후 재연결, 자동 실행OFF, 다른 기기 세션 유지.
- 단일창 미선택 시 앱 실행용 ADB/wake-up이 없고 DeX는 앱 없이 실행 가능.
- APK2.1.2 ->2.1.4 업데이트 및 이미2.1.4 설치된 기기; 설치/권한/Guardian/일반 전송 보존.
- 충돌하는 파일/폴더, 기존 `(1)` 이름, 빈 폴더, Unicode, 내부 공백, 취소/연결 해제.
- 실제 bar의 표시/위치/비활성화/접힘/명령 target HWND와 기기 분리.

## 8. 인계 자료를 찾는 경로

- 현재 문서: 두 worktree의 `docs/STORE_UPDATE_HANDOFF_2026-10-05.md`.
- 이전 APK 전용 기록: 포터블 `docs/STORE_COMPANION_FOLLOWUP_2026-10-05.md`; 현재 문서가 APK+아이콘+연결/UI 항목을 통합한다.
- 포터블 `docs/SESSION.md`, `docs/TODO.md`, `docs/DECISIONS.md`의2026-10-05 최종 기록.
- `DexManager/Resources/FluentIcons/README.md`, `sources.json`, `svg/`, PNG 및 license.
- 새 APK/ZIP:3절 절대 경로와 해시.

Store 작업을 재개하는 프롬프트 예시:

> docs/STORE_UPDATE_HANDOFF_2026-10-05.md와 Store 전용 작업 규칙을 먼저 읽고, 기존 미커밋 변경을 보존해 주세요. Companion 2.1.4 번들/검증값과 소스 차이를 먼저 비교하고, 승인된 Fluent Regular 아이콘·파생 마우스·미니바 연결 표시를 Store에 적용해 프리뷰를 만들어 주세요. 이미 정상인 USB 우선/재연결 자동 실행을 보존하며 문서의 최종 요구를 검증해 주세요. 포터블 제품 코드, APK 서명 비밀과 현재 제출물을 변경하지 말고 실제 제출은 별도 승인 후 진행해 주세요.
## 2026-10-05 최종 포터블 버전 확정 추가 기록

사용자가 실기 정상 동작을 확인하고 최종 Windows 버전을2.5.0으로 지정했다. Companion2.1.4/code11과 승인한 아이콘/연결 수정은 동일하다. 포터블의 매 실행 동의창 검토 설정은 해제하고 최초 동의 저장만 존중한다. 위 본문의2.0.1 및 A219A3... 실행 파일은 인계 시점의 소스/프리뷰 기준이며, 최종2.5.0 실행 파일 SHA256은 BACEA6027137EC0ACE770133034F258EF36F68CA977FD03FF9395F58717D3632다. 최종 ZIP은 E:\vs\dex system\dist\DX-Manager-v2.5.0-win-x64.zip, SHA256 B2064404BB45B65FA863558C82898C5B1BFFAC46CB7035BA5E55CEA2892765E0다. Store Windows/패키지 버전은 별도 제품 정책에 따라 결정하고 포터블의2.5.0을 자동 적용하지 않는다. Store 코드·APK·제출물은 변경하지 않았으며 GitHub 업로드는 바이러스 검사와 사용자 지시를 기다린다.
