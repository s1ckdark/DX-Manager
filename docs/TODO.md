# TODO

## 다음 작업

- [x] upstream v2.5.0(maze-mei, 2026-10-25) 머지 — 포크 main에 Windows 2.5.0 소스·문서·`companion/`
      스냅샷을 반영하고 Mac 프로젝트를 upstream의 `.Mac` 디렉터리 구조로 맞췄다.
  - [x] `DexManager.AdbProxy.Mac/`에 PR #13의 `--self-test` 마커 변경 재적용
  - [x] `DexManager.Mac.csproj`·`DexManager.Mac.sln`·macOS 워크플로·`Package-Mac-Release.sh`의
        proxy/다중 기기 테스트 경로를 `.Mac`으로 전환
  - [ ] upstream Windows 2.5.0 기능 중 Core/Mac 포팅 가치 검토 — scrcpy 5.0.1 `--hwdec=auto`,
        실험적 120 FPS(화면 켜짐 우선·복원 규칙), 하루 1회 업데이트 확인. Core는 upstream이
        건드리지 않았으므로 포팅은 별도 설계·승인 뒤 진행한다.
  - [ ] 번들 Companion을 upstream 정식 2.1.6(`companion/release/DX-Companion.apk`)으로 올릴지 판단 —
        `DexManager.Core/Services/DisplayCleanupPermissionService.cs`의 번들 상수는 아직
        2.0.0(versionCode 6, SHA-256 `7CD4…`) 기준이다. Mac 빌드·패키지는 APK를 번들하지 않아
        지금은 "없음"으로 판정되지만, 번들을 시작하면 해시 불일치로 거부되므로 그때
        `companion/integration/CompanionRelease.cs`를 연결하거나 상수를 갱신해야 한다.
  - [ ] macOS 워크플로와 `Package-Mac-Release.sh`에 `DexManager.Desktop.Tests`(26개) 실행 단계와
        `paths:` 필터 추가 — 현재는 sln 빌드로 컴파일만 되고 실행되지 않는다.
  - [ ] CI 러너에서 `DexOrchestratorDismissKeyguardPollTests.StartAsync_DeviceUnlocksPartwayThroughTheBudget_StopsPollingEarlyAndProceeds`
        실패 — PR #15의 두 번째 CI 실행(41164781227)에서 arm64·x64 러너 모두 "expected at least one
        re-check before the device unlocked, but saw 1"로 실패했고(280/281 통과) 로컬 Apple Silicon에서는
        통과한다. 테스트는 700ms 뒤 잠금 해제를 흉내 내는 벽시계 기반이라, 러너에서 fake adb 프로세스
        기동이 느리면 첫 `dumpsys trust` 확인 전에 이미 풀린다. `KNOWN_ISSUES.md`의 "fake-adb 프로브의
        CPU 경합 flake"와 같은 부류다. main의 CI가 PR #9 이후 빌드 단계에서 멈춰 있어 이 테스트가 CI에서
        실행된 것은 이번이 처음이다. 재현 조건을 더 확인한 뒤 해제 지연을 늘리거나 fake adb를 결정적으로
        바꿀지 판단한다. 이 머지에서는 손대지 않았다.
  - [x] 포크 macOS CI 실패 원인 제거 — `ApplicationHostTests.Dispose_AfterShutdownAsync_DoesNotThrow`의
        `GetAwaiter().GetResult()`가 `--warnaserror`에서 `xUnit1031` 오류가 되어 PR #9 이후 main의
        모든 워크플로 실행이 실패했다. 테스트를 `async`/`await`로 바꿨다(아래 Phase 2 이연 항목의
        "[Task 3] 빌드 경고 신규 발생"과 같은 건).
- [x] macOS GUI Phase 4 — 설정 창 Connection 탭(기기별 USB→무선 전환·IP 직접 연결·해제·
      transport 상태)
  - [x] `IWirelessConnectionGateway`/`WirelessConnectionGateway` — `ApplicationHost.WirelessAdb`의
        기기별 API(`EnableFromUsbForDevice`·`ConnectForDevice`·`DisconnectForDevice`)를
        `Task.Run`으로 감싸고 `FindDevice`·`GetProfile`·`DevicesChanged`를 노출
  - [x] `ConnectionSettingsViewModel` — Host/Port(기본 5555)/AutoReconnect, transport 요약,
        저장 모드 문구, 세 명령(USB→무선 전환·IP:포트 직접 연결·무선 해제), `IsBusy`,
        `StatusMessage`. `HasChanges`/Save가 없다: 동작은 즉시 실행되고 성공 시
        `WirelessAdbService`가 프로필을 직접 저장한다. USB serial은
        `PhysicalDeviceInfo.SelectAuthorizedTransport(DeviceTransportKind.Usb, null)`로 얻는다.
  - [x] `SettingsViewModel`에 5번째 생성자 인자(게이트웨이), identity별 `Connection` 페이지,
        `IsWirelessIdle` 추가 — 동작 중 Save 버튼 비활성(잠금 사유는 `KNOWN_ISSUES.md`)
  - [x] `SettingsWindow.axaml`에 Slots 뒤 Connection 탭, `SettingsWindowStrings`와 resx 두 파일에
        `Settings.Connection.*` 키 6개
  - [x] 자동 테스트: ViewModels.Tests 152(게이트웨이 4 + ConnectionSettingsViewModel 13 +
        SettingsViewModel 통합 6 포함), Desktop.Tests 26, Tests 281.
        `dotnet build DexManager.Mac.sln -c Release --warnaserror` 경고 0
  - [ ] 실기 확인 현황(2026-10-31 SM-F971N; 이 환경에서도 `DOTNET_ROOT`를 주면 GUI가 뜬다 —
        `docs/KNOWN_ISSUES.md` "macOS GUI Connection 탭 실기 검증 범위" 참조):
    - [x] 기기 선택 뒤 탭이 USB serial·저장 방식·포트·자동 재연결을 표시하고 Prepare만 활성
    - [x] 호스트명 `z-fold8`로 Connect wirelessly → 무선 연결 성공, 프로필 Wireless로 저장
    - [ ] USB 연결 상태에서 Prepare → 기기의 `Transports`에 Wireless 항목이 추가로 표시되는가
          (첫 시도는 adbd 재시작으로 페이지가 사라져 결과를 못 봄 → 자리표시자 수정 후 재확인 필요)
    - [x] 케이블 분리 후 기기 행이 유지되고 DeX를 시작할 수 있는가 — 무선 transport로 DeX
          시작·Stop DeX, overlay `null` 회수 확인
    - [ ] Disconnect 뒤 저장 모드가 USB로 돌아가는가
    - [ ] IP를 잘못 입력하면 실패 메시지가 뜨고 버튼이 복귀하는가
    - [ ] 동작 중 Save가 비활성이고 끝나면 다시 활성화되는가
  - [ ] 이연된 리뷰 minor: 연결 호출 중 Cancel/기기 선택 변경 시 호출이 끝나기 전에 idle로
        보고됨, 비운 NumericUpDown이 int `Port`로 바인딩되지 못함, placeholder 문구가 다른 탭처럼
        영어로 하드코딩됨, `OperationCanceledException` 경로 테스트 없음
- [x] **[실기 재현 → 수정]** Connection 탭 "Prepare over USB" 도중 USB 재열거로 페이지가 사라진다 —
      원인: `PhysicalDeviceRegistry.Reconcile`은 한 폴에서 빠진 기기를 유예 없이 제거하고,
      `DeviceListViewModel.Apply`가 선택 행을 폐기한 뒤 `FirstOrDefault()`로 되돌려 휴대폰 한 대면
      `SelectedIdentity`가 null이 되어 설정 창이 페이지를 폐기했다. 수정(`02ae027`, `9838874`):
      선택된 identity의 행은 스냅샷에서 빠져도 같은 `DeviceViewModel` 인스턴스를 "Disconnected"
      자리표시자로 유지하고(transport 없음, Start DeX·슬롯 Start 비활성, Stop은 그대로), 같은
      identity가 돌아오면 같은 행을 갱신한다. 선택이 바뀌지 않으므로 설정 창 페이지가 살아남는다.
      자리표시자는 `PlaceholderGrace`(10초) 이상 사라진 상태에서 **다른 연결된 휴대폰이 있을 때만**
      제거되고 선택이 그쪽으로 넘어간다. 다른 휴대폰이 없으면 무기한 유지된다(의도). 레지스트리는
      건드리지 않았다. 실기 재확인은 아래 체크리스트 항목.
  - [ ] 사소한 보강: 자리표시자가 선택 해제된 채 제거되거나 선택되지 않은 상태로 돌아오면
        `_selectedMissingSinceUtc` 스탬프가 남아, 나중에 그 기기를 다시 선택해 또 빠지면 유예 없이
        만료될 수 있다. 선택 변경 시 스탬프를 지운다.
- [ ] Connection 탭 포트 NumericUpDown 폭이 좁아 "5555" 마지막 자리가 잘려 보인다(실기 확인) —
      `MinWidth`를 늘린다.
- [x] 로컬용 macOS .app 번들 스크립트
  - [x] `scripts/Build-Mac-App.sh` → `dist/DXManager.app`(osx-arm64, ad-hoc 서명, 개인용)
  - [x] 번들: Desktop 앱 + `tools/{adb-proxy,scrcpy,adb}` + `PORTABLE_PACKAGE.txt` + icns;
        재빌드 시 `Contents/MacOS/{config,logs,screenshot}` 보존
  - [ ] 번들 GUI 실제 실행(Finder/Dock) 확인은 사용자 몫
- [ ] 설정 저장 잠금 통합 — `ApplicationHost.UpdateSettings`(`_settingsLock`, live 객체 변경 후 Save)와 `SettingsService.UpdateAndSave`(`_saveSync`, CloneCore→mutate→SaveCore→CopySettings)가 같은 `AppSettings`를 서로 다른 잠금으로 쓴다. `UpdateAndSave`의 CloneCore와 CopySettings 사이에 들어온 UI 저장은 메모리와 디스크에서 조용히 덮어써진다. 수 ms 창이라 드물지만, `UpdateSettings`가 `_saveSync` 아래에서 변경+저장을 한 번에 하도록 바꾸면 이 부류가 사라지고 Connection 탭의 `IsWirelessIdle` Save 게이트는 UX 용도만 남는다. Core 동시성 변경이라 별도 PR로 진행한다.
- [ ] TUI [5]-[1]이 `EnableFromUsb(device.Identity, 5555)`로 identity를 호스트 자리에 넘긴다
      (`DexManager.Mac/Hosting/InteractiveHost.cs:537`) — 메서드는 호스트 IP를 기대한다.
      `EnableFromUsbForDevice`로 교체한다. Connection 탭 브랜치에서는 고치지 않았다.

- [x] macOS GUI Phase 0 — 플랫폼 분리와 ApplicationHost 추출
  - [x] `IPathProvider.IsPortablePackage` 승격
  - [x] `DexManager.Platform.Mac` 라이브러리 분리
  - [x] `ApplicationHost` 조립 루트 추출
  - [x] `InteractiveHost`를 `ApplicationHost` 소비로 전환
  - [x] 실제 기기에서 TUI DeX 시작·중지 회귀 확인
        (2026-09-04 SM-F971N: 시작 시 `SecondaryLauncher` display 47 Resumed,
        `--stop-dex` 후 `overlay=null`·디스플레이 `0 1` 복귀 확인)
- [x] macOS GUI Phase 1 — Avalonia Desktop 골격과 MainWindow

- [ ] 온디바이스 DeX 창 — **보류.** 전제 검증 후 착수 여부 결정
  - 설계와 기술적 타당성은 확정됐으나 제품 가치가 미검증이다.
    스펙 0절에 보류 근거 기록: DeX는 화면을 넓히려고 만든 것인데 폰 화면 안에 넣으면
    그 이유가 사라지고, 입력 모델도 맞지 않으며, One UI의 분할/팝업 뷰가 이미 답을 갖고 있다.
  - [ ] `android-display-mirror` + `android-display-extend` 설치해 일주일 실사용
  - [ ] 판정: 계속 켜게 되는가? 아니면 스펙 폐기. 맞다면 무엇이 부족한지가 그때 드러난다
  - 앱 코드는 아직 한 줄도 없다(문서만). 접는 비용이 가장 싼 시점이다.

- [x] macOS GUI Phase 2 착수 전 선행 정리 (Phase 1 최종 리뷰 지적)
  - [x] `ApplicationHost`로 정리(teardown) 책임 이관 — **Phase 2의 첫 작업**
    - 현상: 조립은 `ApplicationHost`가 공유하나 정리는 `InteractiveHost.ShutdownAsync`에만 있다.
      `Dex.ShutdownAsync`, `SingleWindows.StopAll()`, `HasDeferredDisplayCleanup`,
      `DisposeRuntimeServices` 등 `AGENTS.md`의 프로세스·overlay 불변식을 강제하는 코드 전부.
    - Phase 1에서는 GUI가 런타임을 만들지 않아 무해하나, 스펙 3.3절이 막으려던 분기다.
      Phase 2는 정리 코드를 GUI에 복제하거나 옮겨야 하며 **옮기는 쪽을 먼저 한다.**
    - 부수 증상: TUI 종료 시 호스트의 `DeviceMonitor`·`KeyboardService`가 직접 해제되는데
      `ApplicationHost._disposed`는 `false`로 남는다. 하나의 수명주기를 두 플래그가 추적한다.
  - [x] `UpdateSettings`에 프로덕션 호출자 연결 — 스펙 8.1절 공백 5가 이제 닫혔다
    - TUI 설정 메뉴(`InteractiveHost.cs:792`)가 `ApplicationHost.UpdateSettings`를 거친다.
      입력은 잠금 밖에서 받는다.
    - [x] `UpdateSettings`에 `_disposed` 가드 추가 (`Start`/`Stop`이 세운 패턴과 일치시킨다)
    - [x] `UpdateSettings`가 무관한 필드까지 정규화함을 문서화 —
      `Save` → `SaveCore` → `EnsureDefaults()`가 호출자의 살아있는 객체에 작용한다.
      Phase 3 설정 화면이 `Settings`에 폼을 바인딩하기 전에 알아야 한다. **닫힘(Phase 3
      Task 2).** `EditableRunSettings`가 원본 프로필에 직접 바인딩하는 대신 스칼라 값을
      **복사**하고 저장 시 `ApplyTo`로 되돌려 넣는 사본 패턴을 도입해 이 문제를 우회했다 —
      정규화는 `UpdateSettings` 잠금 안, 대입 뒤에만 실행되므로 편집 중 화면이 바뀌지
      않는다(`DexManager.ViewModels/Settings/EditableRunSettings.cs:6-12` XML 주석에
      명시). 전역 페이지(Paths/Appearance/Interaction)는 스칼라 필드 자체가 폼 상태라
      같은 문제가 애초에 발생하지 않는다.
  - [x] `ApplicationHost._disposed`를 `Interlocked`로 전환 — `DeviceMonitorService`와 동일하게.
    Phase 2에서 창 닫기와 종료 경로가 함께 `Dispose`에 도달하기 전에.
  - [x] `DeviceListViewModel` 생성자의 구독-적용 순서 경합 — 구독 후 `Apply(_registry.Current)`
    사이에 `Reconcile`이 끼면 큐의 오래된 스냅샷이 이긴다. `DeviceRegistrySnapshot.Generation`으로 판별 가능.

- [x] macOS GUI Phase 2 설계 시 주의 (Phase 1 최종 리뷰 지적)
  - [x] DeX 시작·중지 명령을 `ShellViewModel`에 두지 않는다. `host.SelectedSerial`을 읽는 것이
    최소 저항 경로가 되어 복수 기기 불변식이 조용히 무너지며 어떤 테스트도 잡지 못한다.
    명령은 `DeviceViewModel`에 두고 serial을 행에서 가져온다.
    `ApplicationHost.SelectedSerial`은 문서대로 진단 전용으로 유지한다. — 반영됨.
  - [x] `DeviceViewModel`에 `IDisposable`을 붙인다. 행에 세션 상태 구독을 달는 순간
    `Apply`의 `Devices.RemoveAt(i)`가 기기를 뽑을 때마다 하나씩 샌다. 지금은 공짜다. — 반영됨.
  - [x] `new ShellViewModel(...)`가 던질 때 이미 만들어진 `ApplicationHost`가 새지 않게 한다 —
    현재는 `_shell`이 null이라 종료 훅의 `DisposeQuietly()`가 no-op이 된다.
    `App.axaml.cs`의 `DisposeHostQuietly(host)`가 이 경로를 회수한다.
  - [x] 워크플로 `paths:` 필터에 `DexManager.Platform.Mac/**` 추가 — main에 이미 있던 공백이라
    그 프로젝트만 건드린 PR은 macOS 워크플로를 트리거하지 않는다.

- [x] macOS GUI Phase 2 — DeX 시작/중지와 단일창 슬롯
  - [x] 런타임 정리 책임을 `ApplicationHost`로 이관
  - [x] `UpdateSettings`에 프로덕션 호출자 연결과 `_disposed` 가드
  - [x] `ApplicationHost._disposed`를 `Interlocked`로 전환
  - [x] `DeviceListViewModel` 생성자의 구독-적용 경합 해소
  - [x] `DeviceViewModel`에 `IDisposable`
  - [x] identity별 런타임 1개를 보장하는 `DeviceRuntimeCoordinator`
  - [x] 명령을 `DeviceViewModel`에 배치 (`SelectedSerial`은 진단 전용 유지)
  - [x] 워크플로 `paths:`에 `DexManager.Platform.Mac/**` 추가
  - [ ] 실기 검증 — 아래 항목은 사용자 확인 전까지 미확인이다
    (`docs/KNOWN_ISSUES.md`의 "macOS GUI Phase 2 실기 검증 범위" 참조)
    - [ ] 기기 목록에 연결된 기기가 나타난다
    - [ ] `Start DeX`를 누르면 scrcpy 창이 뜨고, 목록의 행에 `DeX` 표시가 붙는다
    - [ ] `Stop DeX`를 누르면 창이 닫히고 `DeX` 표시가 사라진다
    - [ ] 중지 뒤 `adb shell settings get global overlay_display_devices`가 `null`을
      돌려준다 (overlay 회수 확인 — overlay 불변식 직접 검증 항목)
    - [ ] 슬롯 1에 앱 패키지를 넣고 `Start`를 누르면 그 앱 창이 뜬다
    - [ ] 슬롯 `Stop`으로 창이 닫힌다
    - [ ] 창을 닫으면 남은 scrcpy 프로세스가 없다 — `pgrep -fl scrcpy`가 비어야 한다
    - [ ] DeX 실행 중 창을 닫아도 overlay가 회수된다 (overlay 불변식 직접 검증 항목)

- [ ] macOS GUI Phase 2 리뷰에서 지연된 정리 항목 (17건, `.superpowers/sdd/2026-09-07-macos-gui-phase2/deferred-items.txt`에서 이관)
  - 각 Task 리뷰가 발견했으나 12개 Task 각각에서 고치는 대신 한 번에 정리하기로 미룬 항목이다.
    수정하기 전에 반드시 이 목록을 다시 읽고 파일:줄을 확인한다.
  - [ ] **[더 중요함]** `ApplicationHost.ShutdownAsync`(`DexManager.Core/Hosting/ApplicationHost.cs:387-393`)의
    teardown 루프가 같은 `fallbackSerial`/`fallbackIdentity`를 `RuntimeFactory.CreatedInstances`의
    모든 런타임에 방송한다. 런타임이 하나뿐이던 시절엔 무해했으나, 이제 `DeviceRuntimeCoordinator`가
    기기별로 런타임을 하나씩 만들므로, 세션 없는 런타임이 **다른 기기의 serial**로 overlay 정리를
    시도할 수 있다. 검증 결과: 오늘 시점엔 위험한 조합(비-null fallback + 다중 런타임)을 만드는
    호출자가 없다 — GUI(`App.axaml.cs`의 `Dispose()` → `ShutdownAsync(null, null)`, 이 경로만 여러
    런타임을 가질 수 있다)는 항상 `(null, null)`을 넘기고, TUI(`InteractiveHost.cs:847`)는 비-null
    fallback을 넘기지만 `_activeRuntime` 하나만 유지하므로 그 fallback은 항상 자기 자신의 런타임과
    같은 기기를 가리킨다. 그래서 잠재적(latent)이지 실제로 발생하는(live) 버그는 아니다. **이 조합이
    실제로 발생하기 전에 반드시 고쳐야 한다** — 예: 런타임별로 자기 자신의 세션에서만 fallback을
    끌어오게 하거나, fallback을 아예 없애고 세션이 없는 런타임은 정리를 건너뛴다.
  - [ ] **[더 중요함]** `DexManager.Mac/Hosting/InteractiveHost.cs`의 죽은 멤버 2개 — `_settingsService`(:15),
    `_keyboardService`(:29). 둘 다 `_host`로 위임하는 프로퍼티인데 유일한 소비자였던 코드가 이전
    Task에서 삭제되어 이제 선언 자체 말고는 참조가 없다. 삭제 대상.
  - [ ] [Task 1] `ApplicationHost.cs:283-286`(`IsDisposed`)와 `:292-294`(`Dispose`)의 XML 주석이
    이번에 도입한 스레드 안전 보장("원자적으로 한 번만 true로 전이")을 언급하지 않는다.
  - [ ] [Task 3] `DexOrchestrator.cs:459-462`의 근거 주석이 "건너뛰는 경우"를 설명하는데
    "실행되는" 분기 안에 있다. `else if` 위로 옮겨야 한다.
  - [ ] [Task 3] `ApplicationHost.cs:~483` `Dispose()`가 `ShutdownAsync` 자체가 fault하면
    raw 재던짐 → 문서화된 `AggregateException` 계약 위반. 사실상 도달 불가.
  - [ ] [Task 3] `ApplicationHost.cs:336-340` vs `:372` — `_disposed`는 `ShutdownAsync` 끝에서만
    설정되고 `_shutdownStarted`는 진입부에서 설정되므로, 정리 진행 중 두 번째 호출자는 빈 목록과
    `IsDisposed==false`를 본다. 이미 수용된 결과이나, 이제 진입점이 둘이라 도달 가능해졌다는 점만 기록.
  - [ ] [Task 3] 빌드 경고 신규 발생 — `DexManager.Tests/ApplicationHostTests.cs`의
    `Dispose_AfterShutdownAsync_DoesNotThrow`에서 `.GetAwaiter().GetResult()` 때문에
    `xUnit1031` analyzer 경고. 브리프 verbatim이라 구현자 책임 아님. 2026-09-08 기준 여전히
    유효(스펙 5.1절 참조) — 최종 fix wave 후보.
  - [ ] [Task 5] `Apply` 내부 근거 주석 17줄이 가드 본문 3줄보다 길다 — 필드/메서드 위 doc comment로
    옮길 여지. 컨트롤러 지시 사항이라 수용 가능.
  - [ ] [Task 5] 개명된 테스트의 중간 assertion들이 `MarshalsSnapshotChangesThroughTheDispatcher`와
    일부 겹친다. 커버리지 중복이지 결함 아님.
  - [ ] [Task 6] `DeviceListViewModel.Dispose()`가 `Devices`를 비우면서 `OnPropertyChanged(nameof(IsEmpty))`를
    올리지 않는다(`Apply`은 올린다). 해체 중 바인딩이 남아 있으면 빈 상태 배너가 갱신되지 않는다.
  - [ ] [Task 7] `GetOrCreate`를 실제 동시 호출로 검증하는 테스트가 없다(순차 호출만). 이 Task의
    존재 이유인 동시성 보장이 코드 검토로만 확인됨.
  - [ ] [Task 9] `StopDexAsync` 핸들러에 `OperationCanceledException` 전용 catch가 없다(Start에는
    있음). 취소된 중지가 "DeX stop failed: A task was canceled."로 표시된다. 기능적 버그는 아님.
  - [ ] [Task 9] `DeviceRuntimeCommands`가 `GetOrCreate`/`TryGet`을 `Task.Run`으로 감싼 뒤
    `DexOrchestrator`의 메서드를 부르는데 그쪽도 자체 `Task.Run`을 한다 — 무해한 스레드풀 이중 홉.
  - [ ] [Task 10] 슬롯 Stop에 `CanExecute`/`IsBusy` 게이팅이 없다(행의 `StopDexCommand`에는 있음).
    범위 확장 방지를 위해 의도적으로 남김.
  - [ ] [Task 10] 새 fail-fast 테스트가 2초 고정 벽시계 경계를 쓴다. 느린 CI 러너에서 이론적
    flake 벡터(통과 경로). 위험 낮음.
  - [ ] [Task 11] `DisposeHostQuietly`와 `DisposeQuietly`가 실패 시 구분 없는 `Report(ex)`를
    부른다(`App.axaml.cs:60,78,98`). 두 곳이 같은 catch에서 실패하면 "Fatal error:" 두 줄을
    구분할 수 없다. 현재 어느 경로에서도 도달하지 않음.
  - [ ] [Task 12] 목록 행의 `DisplayName`+상태 라벨이 320px 고정 열 안에서 wrap/trim 없는 수평
    `StackPanel` — 긴 기기명이 잘릴 수 있다. 기존 패턴이며 이번에 추가된 것은 `DeX` 라벨뿐.

- [x] macOS GUI Phase 3 — `SettingsWindow` (연결·값·상호작용·테마)
  - [x] `ISettingsGateway` 경계(`Current`/`GetRunProfile`/`Update`) (Task 1)
  - [x] `EditableRunSettings` 사본 편집 패턴 — `FromProfile`/`ApplyTo` (Task 2)
  - [x] 기기별 Display/Stream 값 편집 (Task 3)
  - [x] 기기별 단일창 슬롯 편집 (Task 4)
  - [x] 전역 Paths(scrcpy/adb 경로) 편집 (Task 6)
  - [x] 테마 적용 — `ThemeApplier` + Avalonia 배선 (Task 7)
  - [x] 언어 선택 저장 + `LocalizationService` 런타임 전환(신규 문자열만, 기존 XAML은
    재시작 필요) (Task 8)
  - [x] 상호작용(키매핑) 설정 골격 — `KeyMappingSettings` 10개 스칼라 필드 전부 (Task 9)
  - [x] `SettingsViewModel` 조립 — 5페이지, SaveAll/Cancel, HasChanges 집계 (Task 11)
  - [x] 실행중 기기 DeX 경고 — `IsTargetDexRunning` (Task 5)
  - [x] `SettingsWindow` 뷰(5탭) + `MainWindow` 진입점 + `App.axaml.cs` 배선 (Task 12)
  - [x] 키 조합 캡처 UX — `HotkeyFormatter`, macOS 훅 부재 안내 (Task 10)
  - [x] 사용자 실기 피드백 3건 반영
    - [x] UI-1: Save/Cancel이 창을 닫는다 (`078d1ec`)
    - [x] UI-2: DeX 시작 전 잠금 감지 + 안내 (`f8980e9`, `7467e04`)
    - [x] UI-3: 단일창 슬롯 라벨/도움말/워터마크, 무동작 Custom width/height 제거 (`9b1342b`)
  - [x] `UpdateSettings`가 무관한 필드까지 정규화함을 문서화 — Phase 2 이연 항목 종결
    (위 "macOS GUI Phase 2 착수 전 선행 정리" 참조)
  - [ ] 실기 검증 — 아래 항목은 사용자 확인 전까지 미확인이다
    (`docs/KNOWN_ISSUES.md`의 "macOS GUI Phase 3 실기 검증 범위" 참조)
    - [ ] 테마 실시간 전환이 열려 있는 다른 창에도 반영된다
    - [ ] 언어 변경 후 "재시작 후 적용" 안내가 뜬다
    - [ ] 단축키 필드가 실제 키 입력을 캡처한다
    - [ ] 대상 기기가 DeX 실행 중일 때 안내 문구가 뜬다
    - [ ] 기기 미선택 시 Display/Stream·Slot 탭이 placeholder를 보인다
    - [ ] Save/Cancel을 누르면 실제로 창이 닫힌다

- [ ] macOS GUI Phase 3 리뷰에서 지연된 정리 항목 (파킹, 수정 안 함 — 기록만)
  - [ ] [Task 5] `SettingsViewModel.OnDeviceSelectionPropertyChanged`가
    `IDeviceSelectionSource`에 문서화되지 않은 크로스클래스 UI-스레드 불변식에 의존한다
    (`SelectedIdentity`가 항상 UI 스레드에서만 바뀐다는 가정). 오늘은 안전하나 향후
    off-thread 변경이 생기면 조용히 깨질 수 있다.
  - [ ] [Task 10] hotkey 캡처의 `KeyDown` Tunnel 핸들러가 `e.Handled=true`를 무조건
    설정해 포커스 중 Tab/Shift+Tab/Escape도 삼킨다. hotkey-recorder 표준 UX라 요구
    위반은 아니나, UI-1이 같은 창을 건드렸으니 원하면 통과 예외를 추가할 수 있다.
  - [ ] [UI-1] Save가 무효 페이지를 조용히 건너뛰고도(스킵된 편집 폐기) 창을 닫는다 —
    수정 전에는 창이 열려 있어 사용자가 보고 고칠 수 있었던 안전망이 사라졌다.
  - [ ] [Ruling 6] 설정 창 문자열 약 38/40개가 하드코딩 영어다(MainWindow의 기존
    상태와 동일, 회귀 아님). 언어 설정을 추가한 이번 Phase 이후 지역화 완성이 자연스러운
    후속이다.
  - [ ] [Ruling 7] `SingleWindowSlotSettings.CustomWidth`/`CustomHeight`는 실행 경로가
    전혀 소비하지 않는 레거시 WinForms 잔재다. Slot 탭 UI에서는 숨겼으나(UI-3) 모델
    속성과 `AppSettings` 라운드트립/clamp 로직은 그대로 남아 있다 — 완전히 걷어낼지는
    후속 판단.

- [x] 신호 종료 시 overlay 회수 실기 검증 (PR #9 후속)
      (2026-09-10 SM-F971N, `--dex` + 4× CPU 부하: Ctrl+C 0.33s / SIGINT 0.34s /
      SIGTERM 0.22s / SIGHUP 0.22s, 네 경우 모두 `overlay_display_devices`가
      `null`로 회수되고 scrcpy 정리됨. 상세는 `docs/KNOWN_ISSUES.md`의
      "터미널 앱 신호 종료 실기 검증" 절)
- [ ] `DXMAdbProxy`가 실행 불가일 때 원인과 무관한 오류가 표시된다
  - 현상: 앱은 scrcpy에 `ADB=<번들>/tools/adb-proxy/DXMAdbProxy`를 넘긴다.
    이 프록시는 프레임워크 의존 .NET 앱이라 `DOTNET_ROOT`/공유 런타임을 못 찾으면
    `You must install .NET to run this application`으로 죽고, scrcpy는
    `adb start-server` 실패 → 즉시 종료한다. 사용자에게 보이는 메시지는
    **"scrcpy exited before its window was ready."** 뿐이라 원인을 알 수 없다.
  - 재현: 2026-09-10, tmux 서버가 오래된 환경(=`DOTNET_ROOT` 없음)을 물려준
    셸에서 `dotnet DXManager.Mac.dll --dex`. 환경변수를 주입하니 정상 동작.
  - [x] 포터블 패키지(자체 포함 게시)에서도 재현되는지 확인 — **영향 없음.**
        `scripts/Package-Mac-Release.sh`가 프록시를 `--self-contained`로 게시하고,
        같은 스크립트의 스모크 테스트가 `PATH=/usr/bin:/bin`·깨끗한 `HOME`에서
        `--self-test` 통과를 요구한다. **개발 실행 전용 문제**로 확정.
  - [x] 프록시 실행 실패를 감지해 실제 원인을 표시 (PR #13) — 진단 화면의 프록시
        점검이 파일 존재 확인에서 실제 실행으로 바뀌었다. 실패 시 프로세스가 낸
        사유를 그대로 보여준다. `--diag`로 확인할 것.
  - [ ] DeX 시작 실패 메시지 자체는 그대로다 — 진단을 돌리지 않은 사용자는 여전히
        `scrcpy exited before its window was ready.`만 본다. 시작 경로에서도
        원인을 짚어줄지는 별도 판단. **개발 전용 문제이므로 우선순위 낮음.**
- [ ] adb 후보 프로브 체인 테스트를 가짜 `ProcessRunner`로 전환 (PR #10 후속)
  - 현상: `PathServiceCandidateRetryTests`의 체인 회귀 테스트가 실제
    프로세스 타임아웃 바닥(`Math.Max(timeoutMs, 3000)`)을 타서 약 20초,
    클래스 전체 약 30초. `ProcessRunner`에 주입 가능한 인터페이스가 없어
    실제 `adb version`을 띄운다.
  - 주의: 바닥 값 자체를 낮추면 안 된다 — 부하 시 멀쩡한 후보를 죽었다고
    오판하는 원래 flake로 돌아간다. 프로세스를 가짜로 바꾸는 쪽이 맞다.
- [ ] adb 후보 병렬 프로브 검토 (PR #10 후속, 착수 여부 미정)
  - PR #10은 재시도 배수만 걷어냈다. 후보 N개가 모두 타임아웃하면 첫 시도
    N×타임아웃(실제 5초 × 후보 수)은 그대로 남는다 — 첫 시도를 예산으로
    자르면 fail-open이 깨져서 일부러 남긴 항이다. 없애려면 후보를 병렬로
    프로브해야 하는데, 시작 경로가 동기라 구조 변경이 필요하다.
- [ ] 레거시 Windows `DexManager/Services/PathService.cs`의 프로브 정책 동기화
  - PR #10이 Core 쪽 `Math.Max(timeoutMs, 3000)`을 `ProbeRetryBudget`
    헬퍼로 뽑았지만 Windows 사본(`DexManager.Mac.sln`에 없음)은 자체
    `Math.Max`를 유지해 두 사본이 갈라졌다. 재시도 예산·EAGAIN 재시도도
    없다. Windows 빌드를 다시 만질 때 함께 옮길 것.
- [ ] 세션 중 기기 분리 시 overlay 잔여물 자동 회수
  - 현상: DeX 실행 중 기기가 사라지면 종료 처리가
    `Could not remove the existing virtual display`로 실패하고
    `overlay_display_devices`가 남는다. `settings global` 값이라 재부팅해도 유지된다.
  - 재현: 2026-09-04 SM-F971N(One UI 9.0), 세션 중 USB 분리.
    잔여값 `1600x900/150,hdmi`. 이후 `--stop-dex`로 정상 회수됨.
  - [ ] "기기가 없어서 못 지움"과 "지우기 자체가 실패"를 구분해 처리
  - [ ] 전자는 보류 정리 항목으로 로컬에 기록하고 다음 실행 시 자동 회수
  - [ ] 회수 전까지 시작 시 잔여물 경고 표시

- [x] v2.0.1 DeX 기기별 설정 표시 회귀 수정
  - [x] 첫 물리 identity 결속 뒤 기기별 DeX 설정 UI 재동기화
  - [x] 최초 다중 기기 선택에서 공통 기본값의 기기별 설정 덮어쓰기 차단
  - [x] Windows 버전 2.0.1과 기존 DX Companion 2.0.0(versionCode 6) 호환 확인
  - [x] .NET Framework 4.6.2 x64 Release와 39개 다중 기기 회귀 테스트

- [x] macOS 아키텍처별 포터블 배포 기반
  - [x] arm64/x64 DX Manager와 ADB proxy self-contained single-file publish
  - [x] 공식 scrcpy 4.1 arm64/x86_64 아카이브 SHA-256 검증 및 번들
  - [x] ZIP 재압축 해제, 실행 권한, 아키텍처, 외부 경로, 제외 파일 검증
  - [x] Apple Silicon/x64 GitHub Actions matrix, PR artifact와 검증된 Release 초안
  - [ ] PR에서 첫 GitHub Actions arm64/x64 원격 실행 결과 확인
  - [x] `Q` 정상 종료의 DeX·단일창·전송·overlay cleanup 보강
  - [x] DeX 시작·정리 전 live identity 재검증과 identity별 보류 정리 분리
  - [x] CLI 시작·자연 종료 대기의 `Ctrl+C` 취소 및 종료 경합 보강
  - [ ] 실제 Intel Mac에서 Galaxy DeX 시작·중지와 overlay cleanup 실기 확인
  - [ ] Apple Developer ID 서명·notarization 자격이 준비되면 공개 ZIP 서명

- [x] v2.0.0 공개 후보 준비
  - [x] Windows 버전 2.0.0과 DX Companion 2.0.0(versionCode 6) 일치
  - [x] 다중 기기 README·사용 설명서·FAQ·개발 문서 최신화
  - [x] 진단 페이지의 선택 기기 Android·SDK·One UI·보안 패치 진단
  - [x] 기기 이름·serial·IP·토큰·로컬 경로를 가린 진단 보고서 저장
  - [x] Windows x64/.NET Framework 4.6.2 Release와 전체 회귀 테스트
  - [x] Android 단위 테스트·lint·Release APK 서명 및 해시 재검증
  - [x] 공개용 ZIP의 필수 파일과 비밀키·설정·로그 제외 최종 검사

- [x] v2 기능 구현 후 구조 건강성 감사
  - [x] `MainForm` 기기 탭·연결 수명주기·연결 상태 보조 로직 분리
  - [x] 설정 루트와 설정 DTO·열거형 분리
  - [x] 사용자 입력 컨트롤을 기본 입력·값 입력·드롭다운 팝업으로 분리
  - [x] 구독되지 않은 v1 단일 기기 연결·분리 처리기 제거
- [ ] Scrcpy 주 창과 단일창의 프로세스 종료·출력 drain 공통화 검토
  - 종료 경합과 비정상 분리 회귀 테스트를 먼저 추가한 뒤 진행한다.
- [ ] `PhoneTransferReceiver`의 소켓 프로토콜과 PC 저장 경로·충돌 이름 처리를 분리
  - 대용량 전송·중단·재연결 테스트를 먼저 보강한 뒤 진행한다.
- [ ] `WirelessAdbService`의 기기별 연결 명령과 자동 재연결 snapshot 계산을 분리
  - USB·무선 동시 연결과 여러 무선 기기의 격리 테스트를 유지한다.
- [ ] Android 11+ 무선 ADB의 현재 연결 endpoint를 mDNS로 자동 발견
  - 번들 ADB의 `adb mdns services`에서 `_adb-tls-connect._tcp` 서비스를 조회하고,
    페어링용 `_adb-tls-pairing._tcp` 포트와 실제 연결 포트를 명확히 구분한다.
  - 검색 결과의 첫 항목을 임의로 선택하지 않는다. 물리 기기 identity, 기존 USB
    serial, 모델과 이미 검증된 transport를 대조해 현재 선택한 휴대폰의 endpoint만
    갱신하며, 같은 모델 여러 대와 복수 무선 기기를 안전하게 구분한다.
  - 휴대폰 재부팅, 무선 디버깅 재활성화, Wi-Fi 변경 등으로 연결 포트가 바뀌면
    자동 재연결 전에 새 endpoint를 다시 발견한다. 검증에 성공한 경우에만 기기별
    `WirelessHost`와 `WirelessPort`를 갱신한다.
  - mDNS를 사용할 수 없거나 방화벽·공유기가 multicast 검색을 차단하면 현재의
    수동 IP·연결 포트 입력과 저장된 endpoint를 폴백으로 유지한다.
  - 사용자가 선택한 USB 전용·무선 전용 연결 정책은 그대로 지킨다. 자동 발견을
    이유로 USB와 Wi-Fi 사이를 임의로 전환하지 않는다.
  - Windows 7의 legacy ADB와 `USB로 무선 준비`의 고정 5555 방식은 지원 여부와
    동작이 다르므로 Android 11+ 보안 무선 디버깅 경로와 분리한다.
  - 로그에는 발견 서비스, 대상 기기 판정 근거, 이전/새 endpoint, 폴백 사유와
    연결 결과를 남기되 6자리 페어링 코드와 인증 정보는 기록하지 않는다.
  - 한 대, 같은 모델 두 대, USB+Wi-Fi 혼합, 무선 두 대, 포트 변경, stale 서비스,
    mDNS 차단과 수동 폴백을 독립 회귀 테스트와 실제 기기에서 확인한다.
  - 구현과 실기 검증이 끝난 뒤 README, 영어·한국어 사용 설명서와 FAQ의 무선 ADB
    안내를 갱신한다. 기본 흐름은 `최초 1회 페어링 -> 연결 endpoint 자동 발견`으로
    설명하고, 수동 IP·포트 입력은 폴백으로 안내한다. 페어링 포트와 연결 포트가
    서로 다르며 연결 포트는 바뀔 수 있다는 설명, legacy 5555 방식, 관련 스크린샷과
    캡션도 함께 수정한다.
- [ ] `AppSettings.EnsureDefaults`를 설정 범주별 정규화 helper로 축소
  - 기존 스키마 설정 파일 마이그레이션 표본을 만든 뒤 진행한다.
- [x] v2 1단계 물리 기기·ADB transport 기반
  - [x] v1.3.0 기준 `feature/v2-multi-device` 분기
  - [x] 물리 기기, transport, 방어적 복사 스냅샷 모델 추가
  - [x] 동일 identity의 USB·무선 병합과 서로 다른 휴대폰 분리
  - [x] transport별 상태, 기본 USB 우선과 명시적 transport 선택
  - [x] identity 조회 실패 시 기존 serial 매핑 또는 임시 identity 사용
  - [x] 상태 변화 세대 번호·이벤트와 방어적 복사
  - [x] .NET Framework 4.6.2 x64 Release 및 독립 회귀 테스트
- [x] v2 2단계 전역 ADB 대상 제거
  - [x] `AdbService.TargetSerial`과 프로세스 전역 `ANDROID_SERIAL` 의존 위치 목록화 및 제거
  - [x] 모든 기기별 ADB·Scrcpy 명령에 명시적 serial 전달
  - [x] 기기 선택 상태와 명령 실행 대상을 분리하고 시작·정리 시 serial을 캡처
  - [x] 한 기기의 명령·정리·취소 범위가 다른 기기와 섞이지 않는 독립 회귀 테스트
- [x] v2 3단계 기기별 런타임 세션
  - [x] DeviceMonitor, DeX, 단일창, Scrcpy, 화면 전원 상태를 물리 기기별로 분리
  - [x] Companion reverse·수신 토큰과 PC↔폰 전송 큐를 기기별로 분리
  - [x] 물리 기기와 독립 서비스 묶음을 1:1로 결속하고 USB·무선 전환 시 유지
  - [x] 연결 해제 상태를 정리 증거와 함께 보존하고 의미 없는 감시 갱신은 무시
  - [x] .NET Framework 4.6.2 x64 Release 및 기기 격리 회귀 테스트
- [x] v2 4단계 다중 기기 선택 UI와 실기 검증
  - [x] 메인 화면 기기 선택 UI와 연결 방식·상태 표시
  - [x] 탭별 독립 DeX·단일창·양방향 전송 서비스 선택과 비선택 세션 유지
  - [x] USB·무선 transport 변경, 연결 해제, 전체 종료의 기기별 정리 경로
  - [x] .NET Framework 4.6.2 x64 Release 및 26개 다중 기기 회귀 테스트
  - [x] 두 휴대폰 동시 DeX·단일창 실행과 탭별 실행 상태 복원
  - [x] 두 휴대폰 PC→폰 파일 전송과 각각의 연결 해제 격리
  - [x] 두 휴대폰 폰→PC 파일 전송과 연결 상태 전체 정상 종료 정리
- [x] v2 5단계 기기별 무선 ADB 설정
  - [x] 설정의 연결 페이지에서 대상 휴대폰을 명시적으로 선택
  - [x] 선택한 휴대폰의 정확한 USB serial로 무선 준비와 IP 감지 수행
  - [x] 물리 identity별 USB·무선 모드, IP, 포트와 자동 재연결 저장
  - [x] 혼합 USB·무선 연결과 복수 무선 자동 재연결 지원
  - [x] 기존 전역 무선 설정을 최초 기기 프로필로 안전하게 이관
  - [x] .NET Framework 4.6.2 x64 Release 및 31개 다중 기기 회귀 테스트
- [x] v2 다중 기기 식별·표시 마무리
  - [x] 저장된 USB·무선 연결 정책과 실제 감지 transport 상태를 분리해 표시
  - [x] USB·무선 정책 사이 자동 fallback 제거와 선택 transport 대기 처리
  - [x] 연결 방식 변경·분리 시 이전 세션·전송·ADB reverse 정리
  - [x] 메인 기기 탭 전환 시 설정 창을 유지하고 선택 기기 정보 새로 고침
  - [x] 휴대폰→PC 수신 파일을 휴대폰 표시 이름별 하위 폴더로 분리
  - [x] DeX·단일창 scrcpy 제목에 휴대폰 표시 이름 추가
  - [x] 연결 시 DeX 자동 시작을 연결된 각 휴대폰의 독립 런타임으로 확장
  - [x] 한 대 사용 시 기기 탭 숨김과 두 번째 기기 확인 후 실행 중 탭 유지
  - [x] 기기 선택 UI를 왼쪽 사이드바로 이동하고 시작 시 최신 모델 우선·순차 연결 시 최초 연결 순서 유지
  - [x] .NET Framework 4.6.2 x64 Release 빌드 및 35개 다중 기기 회귀 테스트
- [ ] Windows 종료 중 ADB 오류 반복 방지
  - [x] 새 ADB·보조 프로세스 실행 차단과 실행 중 프로세스 종료 gate
  - [x] DX Manager가 선택한 절대 경로의 ADB 프로세스만 종료
  - [x] 정상 종료의 기기 설정 복원·overlay 정리·`adb kill-server` 경로 유지
  - [x] 프로세스 차단·취소 회귀 테스트 추가
  - [x] Windows 7 회사 PC 1차 확인: 오류창 약 10회에서 2~3회로 감소
  - [x] 제한 시간 내 overlay·절전모드 해제 복원 후 ADB 최종 차단 경로 추가
  - [x] 자식 프로세스 네이티브 오류 대화상자 억제 모드 추가
  - [x] 일반 종료 후 분리된 ADB 서버와 번들 보조 프로세스의 절대 경로 최종 정리
  - [x] 이름이 같은 다른 경로 프로세스를 보존하는 회귀 테스트 추가
  - [x] 공개 UI에 넣었던 Windows 종료 모의 테스트를 RC 검증 후 제거
  - [x] 실제 종료 시 새 ADB 실행·종료를 없애고 미리 연결된 Companion 소켓만 사용
  - [x] Companion 미설치·미검증 기기는 실제 Windows 종료 정리를 건너뛰도록 분리
  - [x] Companion 연결 손실 시 기본 5분 유예와 재연결 취소, 즉시 정리 및 자동 정리 안 함 옵션 추가
  - [x] .NET Framework 4.6.2 Debug 빌드와 37개 다중 기기 회귀 테스트 통과
  - [x] Windows 11 RC2 다중 기기 overlay·절전모드 해제 정리 확인
  - [x] Windows 11 실제 종료에서 ADB 오류창 제거와 Companion 기기 설정 복원 확인
  - [ ] Windows 7 회사 PC에서 오류창 제거와 기기 설정 복원 재확인

- [x] v1.2.0 구조 분리
  - [x] MainForm·SettingsForm feature partial 분리
  - [x] 파일 전송 IPC·처리·ADB·원격 작업·진행 상태 분리
  - [x] 폴더 전송 계획과 실행 결과 모델 분리
  - [x] .NET Framework 4.6.2 x64 Debug/Release 빌드
- [x] v1.2.0 실기 회귀 테스트
  - DeX·단일창 시작/중지와 USB·무선 재연결
  - 화면 끄기·잠자기 방지·키보드 보정과 비정상 분리 정리
  - 단일 파일·폴더·Unicode 파일 전송, 취소와 연결 해제
  - Windows 11 확인 후 Windows 7 SP1 확인

- [x] DX Companion 실제 Galaxy 기기 검증
  - [x] 공식 Release APK 설치와 package/certificate 재확인
  - [x] ADB 1회 권한 부여 전·후 상태 표시
  - [x] 앱 본체의 활성 overlay 삭제와 삭제 후 재조회
  - [x] 빠른 설정 타일·2 × 1 홈 위젯과 절전모드 해제 복구 확인
- [x] DX Manager의 정리 앱 package/v2 인증서 검증과 실제 권한 부여 연결
  - 부여 직전·직후 재검증 및 사후 검증 실패 시 권한 회수
- [x] v1.3.0 번들 DX Companion 관리 코드와 Release 빌드
  - [x] 번들 APK SHA-256/v2 인증서 사전 검증
  - [x] 현재 선택 기기 명시적 설치·업데이트·재설치·삭제
  - [x] 설치 후 package·versionCode·서명·권한 재검증
  - [x] 삭제 전 해당 기기의 파일 수신과 ADB reverse 정리
  - [ ] 실제 기기에서 진단 UI 설치·업데이트·재설치·삭제 회귀 확인
- [x] v1.3.0 포터블 ZIP에 APK 포함 및 비밀키·설정·로그 제외 검사
  - 실제 설치·삭제 회귀 뒤 공개 직전에 같은 검사를 한 번 더 수행
- [x] v1.3.0 공개 후보 x64/.NET 4.6.2 Release와 ZIP 최종 재검증
  - Windows 빌드 경고 0·오류 0, Android 단위 테스트·Release lint 통과
  - 필수 파일, APK 해시·v2 서명과 비밀키·설정·로그 제외 확인
- [x] Companion PC 수신 준비 상태 실기 회귀
  - DX Manager 실행·종료와 USB/무선 연결·분리 시 준비/대기 표시 전환
  - 비정상 종료 뒤 저장된 세션 정보가 남아도 실제 수신기 확인 실패 시 대기 표시
  - 공유 메뉴 전송 직전 수신기 재확인
- [x] 캡처·드롭 파일 휴대폰 저장 경로 ADB 폴더 찾아보기

- [x] v1 최초 휴대폰 고정 로직 실기 확인
  - 시작 시 USB 휴대폰 2대 중 한 대 선택 및 고정
  - 실행 중 다른 휴대폰 연결 무시
  - 고정 휴대폰 분리 시 다른 휴대폰으로 자동 전환하지 않음
  - 같은 휴대폰 재연결 및 USB↔무선 전환 허용
- [x] Windows 7에서 최신 입력/설정 UI 회귀 확인
- [ ] Scrcpy 4.0/SDL3 오른쪽 Shift 재현 내용을 upstream에 보고
- [ ] Scrcpy 4.1/SDL3에서도 오른쪽 Shift 호환 보정의 필요 여부 실기 확인
- [ ] Upstream 수정 시 오른쪽 Shift 치환 우회 제거 여부 검토
- [ ] 한국어 노트북의 `VK_HANGUL + extended scan 0x38` 한영키 실기 확인
  - 브라질 ABNT/ABNT2 또는 AltGr 키보드에서 `?`, `@` 등 특수문자 회귀 확인
- [x] 기기 인식 후 실제 Scrcpy 시작 전 대기 옵션(0~60초, 기본 1초)

- [ ] ADB 프록시 경로 탐색이 두 곳에 복제돼 있다 — 한 곳으로 모을 것
  - `EnvironmentCheckService.Run()`과 `FileTransferCoordinator` 생성자가
    `tools/adb-proxy/DXMAdbProxy` → `.dll` → 베이스 디렉터리 `.dll` → `.exe`
    순서를 각각 따로 적어 두고 있다. 후자에만 명시 경로 우선 분기와
    `Path.GetFullPath`가 있다.
  - 지금은 생산 호출부(`DeviceRuntimeServiceFactory`)가 `proxyPath`를 넘기지
    않아 두 체인이 같은 파일로 수렴한다. 즉 "진단이 검사한 파일 = scrcpy가
    `ADB=`로 받는 파일"이 **현재는** 참이다.
  - 누가 `proxyPath`를 주입하거나 한쪽 체인만 고치면 그 등식이 조용히 깨지고,
    `fix/proxy-runnable-check`가 없앤 결함("검사하지 않은 것을 Passed라 말한다")이
    축소판으로 되살아난다. 탐색을 공유 헬퍼 하나로 뽑고 양쪽이 그것을 쓰게 한다.

## 배포 준비

- [x] `dist\DX Manager` 폴더와 버전별 x64 ZIP 패키징 스크립트
- [x] 생성된 공개 패키지 최종 배포 체크리스트
- [x] 패키징 전 Debug/Release의 logs/screenshot 테스트 파일 자동 제거
- [x] 준비된 한국어/영어 스크린샷을 README와 사용 설명서에 배치
- [x] 번들 구성요소 버전 확인과 제3자 라이선스 원문/고지 포함
- [x] 한국어 Q&A 초안 사용자 검토 및 수정
- [x] 한국어 Q&A 승인 후 독립 영문 FAQ로 번역
- [x] Windows 7 SP1~11 v1.1.0/1.2.0 최종 회귀 테스트
  - [x] Windows 11 실제 기기 관리형 Unicode 단일 파일 전송과 proxy 전달
  - [x] x64 Release 빌드 및 v1.1.0 ZIP 구성 검증
  - [x] Windows 7 SP1~11 Scrcpy 4.1 DeX/단일창 실행과 종료 UI 회귀
  - [x] Windows 7/11 한글·Unicode 복수 파일과 폴더 전체 전송 확인
  - [x] 사용자 대상 경로, 파일·폴더 충돌 이름, 취소와 독립 상태창 확인
  - [x] 관리형 전송 끄기 후 새 창에서 Scrcpy 순정 동작 복귀
  - [x] 설정·진단의 실제 ADB `Version ...` 값 표시 확인
- [x] GitHub 첫 공개 Release 게시 및 저장소 public 전환
- [x] v1.3.0 공개 Release 게시와 DX Companion 번들 배포
- [x] DX Manager 자체 소스 라이선스를 MIT로 확정

별도 설치 프로그램은 보류한다. v1.3.0 WinGet manifest는 등록됐다.
- [ ] 별도 `winget-pkgs` 작업 사본에서 v2.0.0 manifest의 제출·승인 상태를
  확인하고, 필요하면 공개 자산 URL과 SHA-256으로 갱신한다.

## 독립 FAQ 반영 항목

1. 비정상 종료 뒤 휴대폰에 작은 보조 화면이 남았을 때 제거 방법
2. 삼성 브라우저 자동 실행 전에 앱을 강제 종료하는 이유
3. 휴대폰을 두 대 이상 연결했을 때 v2의 독립 세션과 연결 정책
4. 1600×900보다 낮은 해상도에서 DeX UI 일부가 잘리는 이유
5. 해상도/DPI에 따라 바탕화면 배치와 배경화면이 달라지는 이유
6. 최근 앱 화면의 데스크톱이 이상할 때 새 데스크톱으로 복구하는 방법
7. DPI를 120보다 낮게 설정할 수 없는 이유
8. Scrcpy 서버 push 속도가 낮게 표시될 때 USB 케이블 확인
9. 무선 ADB 연결/재연결 실패 시 네트워크와 포트 점검
10. `unauthorized`, `offline` 장치와 RSA 승인 문제
11. Scrcpy 4.0에서 오른쪽 Shift를 왼쪽 Shift로 보정하는 이유
12. 기기 연결 후 시작 대기와 프로세스 제한시간의 차이
13. 사용 중 USB가 분리됐을 때 정리와 같은 휴대폰 재연결 동작
14. 휴대폰 화면 끄기와 stay-awake 옵션의 동작
15. 비정상 종료 뒤 충전 중 화면이 계속 켜질 때 절전모드 해제 복구
16. Windows 7 요구사항과 실행 파일만 복사하면 안 되는 이유
17. 관리형 드래그 앤 드롭 파일 전송, Unicode 이름, 충돌과 순정 전환
18. One UI 7.x 이하에서 검은 DeX 창이 나타날 수 있는 이유
19. ADB 공통 `1.0.41` 문구와 실제 `Version ...` 빌드 값의 차이
20. 금융·게임·DRM 앱의 실행 거부 또는 검은 화면
21. DeX에서 실행한 앱이 휴대폰 화면에서 열리는 경우
22. HID 마우스 사용 중 포인터가 scrcpy 창에 잡히는 동작
23. DX Companion 설치·서명 검증과 권한 버튼 비활성화
24. DX Companion 휴대폰→PC 파일·폴더 전송 준비와 사용 방법

## Upstream Windows 2.5.0 현황 (maze-mei, 머지 시점 기록)

upstream `docs/TODO.md`의 2.5.0 릴리스 범위를 머지 시점 그대로 옮긴 기록이다. 이 포크의 작업 항목이 아니다.

### English — 2026-10-25

#### Release scope completed

- [x] Accepted light/dark UI, tray lifecycle, wireless/TLS, transfer and scrcpy 5.0.1 changes.
- [x] Experimental 120 FPS, every-start warning, prior screen-off restoration and same-phone screen priority.
- [x] Daily background update checks, manual check UI and local per-version changes notes.
- [x] Official Companion 2.1.6 with duplicate-share protection and verified-clean guardian stop behavior.
- [x] English diagnostics/FAQ text and Help fit; environment-check header/button overlap fixed.
- [x] 702 Windows checks; final EXE is identical to the tested preview.
- [x] Clean 39-file ZIP with fixed external name and versioned inner folder.
- [x] Maintainer-reported clean scan for the exact final EXE.
- [x] README, EN/KO guides/35-question FAQs, release notes and 16 approved captures refreshed.
- [x] Matching official Companion source/constants/APK included as a verified public snapshot.
- [x] Public macOS implementation preserved in separate .NET 8 projects.
- [x] Explicit maintainer authorization to commit and publish this release.

#### Future work

No new feature is required by this release request. Reported issues should be reviewed against [known limits](KNOWN_ISSUES.md).
Future releases still need explicit publication approval. Do not add the private AI foreground-app/keymap experiment
or change the Store product as part of routine portable maintenance.

#### Verification boundaries

Automated Windows checks are not universal Windows 7/GPU/DPI/phone/firmware or shutdown-packet guarantees.
macOS build/runtime evidence is separate from Windows verification. See [verification](VERIFICATION_v2.5.0.md).

### 한국어

이번 요청 범위는 최종본·문서·공식 Companion 소스 연결·macOS 보존과 승인된 게시입니다.
새 기능 추가나 Store 변경은 포함하지 않습니다. 실기 미확인 환경은 성공으로 확대하지 않으며
다음 버전의 게시도 메이님의 별도 지시를 따릅니다.
