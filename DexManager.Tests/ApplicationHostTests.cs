using DexManager.Hosting;
using DexManager.Models;
using DexManager.Services;
using DexManager.Tests.FakePlatform;

namespace DexManager.Tests;

public class ApplicationHostTests : IDisposable
{
    private readonly TempHostRoot _tempRoot;

    public ApplicationHostTests()
    {
        _tempRoot = new TempHostRoot();
    }

    public void Dispose()
    {
        _tempRoot.Dispose();
    }

    private ApplicationHost CreateHost(FakeKeyboardService keyboard = null) =>
        _tempRoot.CreateHost(keyboard);

    [Fact]
    public void Constructor_ComposesAllServices()
    {
        using var host = CreateHost();

        Assert.NotNull(host.Settings);
        Assert.NotNull(host.SettingsService);
        Assert.NotNull(host.Log);
        Assert.NotNull(host.ProcessRunner);
        Assert.NotNull(host.PathService);
        Assert.NotNull(host.Adb);
        Assert.NotNull(host.WirelessAdb);
        Assert.NotNull(host.DeviceRegistry);
        Assert.NotNull(host.RuntimeSessions);
        Assert.NotNull(host.DeviceMonitor);
        Assert.NotNull(host.PermissionService);
        Assert.NotNull(host.EnvironmentCheck);
        Assert.NotNull(host.DiagnosticReport);

        // RuntimeFactory는 Task 6에서 초기화되므로 여기서 단정하지 않는다.
        // Task 6의 Constructor_InitializesRuntimeFactory가 검증한다.
    }

    [Fact]
    public void SelectedSerial_DefaultsToEmptyAndIsSettable()
    {
        using var host = CreateHost();

        Assert.Equal(string.Empty, host.SelectedSerial);

        host.SelectedSerial = "R5CT1234567";

        Assert.Equal("R5CT1234567", host.SelectedSerial);
    }

    [Fact]
    public void SelectedSerial_NormalizesNullToEmpty()
    {
        using var host = CreateHost();

        host.SelectedSerial = null;

        Assert.NotNull(host.SelectedSerial);
        Assert.Equal(string.Empty, host.SelectedSerial);
    }

    [Fact]
    public void SelectedSerial_RaisesChangedOnlyWhenValueDiffers()
    {
        using var host = CreateHost();

        var events = new List<(string Previous, string Current)>();
        host.SelectedSerialChanged += (_, e) => events.Add((e.Previous, e.Current));

        host.SelectedSerial = "R5KLTEST";
        host.SelectedSerial = "R5KLTEST";   // 같은 값 — 발생하지 않아야 한다
        host.SelectedSerial = "OTHER";
        host.SelectedSerial = null;         // "" 로 정규화되며 변경으로 간주

        Assert.Equal(3, events.Count);
        Assert.Equal((string.Empty, "R5KLTEST"), events[0]);
        Assert.Equal(("R5KLTEST", "OTHER"), events[1]);
        Assert.Equal(("OTHER", string.Empty), events[2]);
    }

    [Fact]
    public void Dispose_DisposesKeyboardServiceAndStopsDeviceMonitor()
    {
        var keyboard = new FakeKeyboardService();
        var host = CreateHost(keyboard: keyboard);

        host.DeviceMonitor.Start();
        host.Dispose();

        Assert.True(host.IsDisposed);
        Assert.Equal(1, keyboard.DisposeCallCount);
        Assert.Throws<ObjectDisposedException>(() => host.DeviceMonitor.Start());
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var keyboard = new FakeKeyboardService();
        var host = CreateHost(keyboard: keyboard);

        host.Dispose();
        var ex = Record.Exception(() => host.Dispose());
        host.Dispose();

        Assert.Null(ex);
        Assert.Equal(1, keyboard.DisposeCallCount);
    }

    [Fact]
    public void Constructor_InitializesRuntimeFactory()
    {
        using var host = CreateHost();

        Assert.NotNull(host.RuntimeFactory);
    }

    [Fact]
    public void EnsureDefaultPaths_DisablesHidInputOnMac()
    {
        // 실제 SettingsService/직렬화 경로로 "HID가 켜져 있던" 상태를 미리 저장해 둔다.
        // ApplicationHost가 읽는 것과 동일한 <_tempRoot.Root>/config/settings.json 파일을 생성한다.
        var seedLog = new LogService();
        var seedSettingsService = new SettingsService(seedLog, _tempRoot.Root);
        var seededSettings = seedSettingsService.Load();
        seededSettings.Scrcpy.UseHidKeyboard = true;
        seededSettings.Scrcpy.UseHidMouse = true;
        Assert.NotEmpty(seededSettings.SingleWindowSlots);
        foreach (var slot in seededSettings.SingleWindowSlots)
        {
            slot.UseHidKeyboard = true;
            slot.UseHidMouse = true;
        }
        seedSettingsService.Save(seededSettings);

        using var host = CreateHost();

        // macOS는 HID 키보드/마우스를 지원하지 않으므로 조립 시 꺼져야 한다.
        Assert.False(host.Settings.Scrcpy.UseHidKeyboard);
        Assert.False(host.Settings.Scrcpy.UseHidMouse);
        foreach (var slot in host.Settings.SingleWindowSlots)
        {
            Assert.False(slot.UseHidKeyboard);
            Assert.False(slot.UseHidMouse);
        }
    }

    [Fact]
    public void EnsureDefaultPaths_PortablePackage_ForcesConfiguredAdbPathToDefault()
    {
        // 이미 유효하고 존재하는 ADB 경로가 설정되어 있어도, 포터블 패키지에서는
        // AdbSelectionMode가 Manual이 아닌 한 기본(번들) ADB 경로로 강제 교체되어야 한다.
        // 실제 SettingsService/직렬화 경로로 사전 상태를 저장해 둔다.
        var seedLog = new LogService();
        var seedSettingsService = new SettingsService(seedLog, _tempRoot.Root);
        var seededSettings = seedSettingsService.Load();
        const string preConfiguredAdbPath = "/bin/ls";
        Assert.True(File.Exists(preConfiguredAdbPath));
        seededSettings.Paths.AdbPath = preConfiguredAdbPath;
        seededSettings.Paths.AdbSelectionMode = AdbSelectionMode.Auto;
        seedSettingsService.Save(seededSettings);

        var portablePathProvider = new FakePathProvider(_tempRoot.Root, isPortablePackage: true);
        using var host = _tempRoot.CreateHost(pathProvider: portablePathProvider);

        // 포터블 패키지에서는 이미 유효했던 ADB 경로였더라도 기본 경로로 교체된다.
        Assert.NotEqual(preConfiguredAdbPath, host.Settings.Paths.AdbPath);
        Assert.Equal(
            portablePathProvider.ResolveDefaultAdbPath(),
            host.Settings.Paths.AdbPath);
    }

    [Fact]
    public void ManualAdbSelection_UsesConfiguredAdbPath_NotAutoDetectedDefault()
    {
        // Manual 모드에서는 설정된 ADB 경로가 실제로 "선택"되어야 한다 -
        // 그저 저장되는 것과는 다르다. 오늘 고친 버그는 정확히 그
        // 차이였다: PathsSettingsViewModel.Save()가 AdbPath는 쓰면서
        // AdbSelectionMode는 절대 Manual로 바꾸지 않아, 이 값을 읽는
        // 코드가 어디에도 없었다.
        //
        // 자동 감지 기본값을 수동 경로와 다른 실행 파일(/bin/echo)로
        // 일부러 둔다 - 둘이 같으면 Manual이 통째로 무시되고 자동 경로가
        // 선택돼도 이 테스트는 (우연히) 통과해 버린다.
        var seedLog = new LogService();
        var seedSettingsService = new SettingsService(seedLog, _tempRoot.Root);
        var seededSettings = seedSettingsService.Load();
        var fakeAdb = new FakeAdbExecutable(
            _tempRoot.Root,
            new Dictionary<string, string>());
        seededSettings.Paths.AdbPath = fakeAdb.ExecutablePath;
        seededSettings.Paths.AdbSelectionMode = AdbSelectionMode.Manual;
        seedSettingsService.Save(seededSettings);

        var pathProvider = new FakePathProvider(
            _tempRoot.Root,
            adbPath: "/bin/echo");
        using var host = _tempRoot.CreateHost(pathProvider: pathProvider);

        Assert.Equal(fakeAdb.ExecutablePath, host.Adb.AdbPath);
    }

    [Fact]
    public void ManualAdbSelection_WithUnusablePath_ThrowsActionableError()
    {
        // Manual 모드에서 설정된 경로가 실행될 수 없으면(오타 등) 앱은
        // 조용히 다른 adb로 넘어가지 않고 시작을 포기해야 한다 - 그리고
        // App.axaml.cs의 CreateMainWindow가 이 예외를 잡아 StartupErrorWindow에
        // Message를 그대로 보여주므로, 그 한 줄만 보고도 사용자가 무엇을
        // 어디서 고쳐야 하는지 알 수 있어야 한다: 실패한 경로 자체와,
        // 되돌리려면 열어야 할 설정 파일 경로.
        var seedLog = new LogService();
        var seedSettingsService = new SettingsService(seedLog, _tempRoot.Root);
        var seededSettings = seedSettingsService.Load();
        var badPath = Path.Combine(_tempRoot.Root, "does-not-exist-adb");
        seededSettings.Paths.AdbPath = badPath;
        seededSettings.Paths.AdbSelectionMode = AdbSelectionMode.Manual;
        seedSettingsService.Save(seededSettings);

        var pathProvider = new FakePathProvider(_tempRoot.Root);

        var ex = Assert.Throws<FileNotFoundException>(
            () => _tempRoot.CreateHost(pathProvider: pathProvider));

        Assert.Contains(badPath, ex.Message);
        Assert.Contains(
            Path.Combine(_tempRoot.Root, "config", "settings.json"),
            ex.Message);
    }

    [Fact]
    public void Start_ThenStop_LeavesHostRestartable()
    {
        using var host = CreateHost();

        var started = LocalizationService.Get("Log.DeviceMonitor.Started");
        var stopped = LocalizationService.Get("Log.DeviceMonitor.Stopped");

        // WaitForFirstPoll은 Start가 감시 타이머를 실제로 돌렸을 때만 통과한다.
        // 두 번째 Start가 이벤트를 다시 Reset하므로, 두 번 모두 폴링이
        // 새로 일어났는지 구분해서 볼 수 있다.
        host.Start();
        Assert.True(host.DeviceMonitor.WaitForFirstPoll(5000, CancellationToken.None));
        host.Stop();

        host.Start();
        Assert.True(host.DeviceMonitor.WaitForFirstPoll(5000, CancellationToken.None));
        host.Stop();

        // Stop이 타이머를 실제로 회수했을 때만 중지 로그가 남는다
        // (타이머가 없으면 DeviceMonitorService.StopTimer는 기록 전에 반환한다).
        var lifecycle = host.Log.GetSessionEntries()
            .Where(entry => entry.Contains(started) || entry.Contains(stopped))
            .Select(entry => entry.Contains(started) ? "start" : "stop")
            .ToArray();

        Assert.Equal(new[] { "start", "stop", "start", "stop" }, lifecycle);
        Assert.False(host.IsDisposed);
    }

    [Fact]
    public void Start_AfterDispose_Throws()
    {
        var host = CreateHost();
        host.Dispose();

        Assert.Throws<ObjectDisposedException>(() => host.Start());
    }

    [Fact]
    public void Stop_AfterDispose_DoesNotThrow()
    {
        var host = CreateHost();
        host.Dispose();

        host.Stop();
    }

    [Fact]
    public void UpdateSettings_AppliesMutationAndPersists()
    {
        using var host = CreateHost();

        host.UpdateSettings(s => s.Timing.ProcessTimeoutMs = 12345);

        Assert.Equal(12345, host.Settings.Timing.ProcessTimeoutMs);

        var reloaded = host.SettingsService.Load();
        Assert.Equal(12345, reloaded.Timing.ProcessTimeoutMs);
    }

    [Fact]
    public void UpdateSettings_ConcurrentMutationsDoNotLoseUpdates()
    {
        // ProcessTimeoutMs가 아니라 AdbWakeUpDelayMs를 쓴다: Save()가 내부적으로
        // AppSettings.EnsureDefaults()를 호출하는데, ProcessTimeoutMs의 하한은
        // 1000이라 0에서 시작해 누적하면 첫 저장에서 기본값(15000)으로 튕겨
        // 나간다. AdbWakeUpDelayMs는 하한이 0이라 같은 정수 누적 성질을
        // 가지면서 이 문제를 피한다.
        using var host = CreateHost();

        host.UpdateSettings(s => s.Timing.AdbWakeUpDelayMs = 0);

        Parallel.For(0, 200, _ =>
            host.UpdateSettings(s => s.Timing.AdbWakeUpDelayMs += 1));

        Assert.Equal(200, host.Settings.Timing.AdbWakeUpDelayMs);
    }

    [Fact]
    public void UpdateSettings_NullMutation_Throws()
    {
        using var host = CreateHost();

        Assert.Throws<ArgumentNullException>(() => host.UpdateSettings(null));
    }

    [Fact]
    public void Dispose_ConcurrentCalls_DisposeKeyboardServiceOnce()
    {
        var keyboard = new FakeKeyboardService();
        var host = CreateHost(keyboard: keyboard);

        // 창 닫기 경로와 프로세스 종료 경로가 Phase 2에서 동시에 도달할 수 있다.
        // 이 테스트는 bool 플래그가 검사와 대입 사이에 다른 스레드를 들여보내는
        // 경합을 보이도록 설계되었으나, 실제로는 32스레드 동시 호출에서도
        // pre-fix 코드에서 재현되지 않는다 (race window가 너무 좁음).
        // 따라서 이 테스트는 강한 증거가 아니라 최선의 노력으로서의 회귀 방지일 뿐이다.
        //
        // 이 테스트가 실제로 검증하는 것:
        // - Dispose()가 멱등하다 (여러 호출에서도 정리 본문이 한 번만 실행)
        // - 동시 진입 하에서 DisposeCount가 1로 유지된다
        // - 어떤 스레드도 예외를 던지지 않는다
        //
        // 수정의 정확성은 테스트가 빨간색을 보이는가가 아니라
        // Interlocked.Exchange의 원자성에 의존한다.
        var start = new ManualResetEventSlim(false);
        var threads = new Thread[32];
        for (var i = 0; i < threads.Length; i++)
        {
            threads[i] = new Thread(() =>
            {
                start.Wait();
                try { host.Dispose(); }
                catch (AggregateException) { }
            });
            threads[i].Start();
        }

        start.Set();
        foreach (var thread in threads) thread.Join();

        Assert.Equal(1, keyboard.DisposeCallCount);
    }

    [Fact]
    public async Task ShutdownAsync_DisposesRuntimeServicesCreatedByTheFactory()
    {
        using var root = new TempHostRoot();
        var host = root.CreateHost();
        var runtime = host.RuntimeFactory.Create();
        // 아직 아무 것도 종료를 요청받지 않았다는 사전 조건 — 아래 단언이
        // "원래 false였다가 실제로 바뀐 값"을 보게 하려는 것이다.
        Assert.False(runtime.Dex.IsShutdownRequested);

        var errors = await host.ShutdownAsync(null, null);

        Assert.Empty(errors);
        // SingleWindowService.Dispose는 멱등이며 두 번째 호출이 조용히 반환한다.
        // 이미 해제됐다면 StopAll이 새 프로세스를 만들지 않는다.
        Assert.Equal(0, runtime.SingleWindows.RunningCount);
        // 위 두 단언은 ShutdownAsync가 런타임 반복문을 건너뛰어도 참이다
        // (RunningCount는 시작 전이나 후나 0이다). 이 단언이 실제 증거다 —
        // Dex.IsShutdownRequested가 true가 되는 유일한 경로는
        // ApplicationHost.ShutdownAsync의 런타임 반복문이 이 인스턴스에
        // 대해 runtime.Dex.RequestShutdown() 또는 runtime.Dex.ShutdownAsync()를
        // 실제로 호출하는 것뿐이다. 반복문이 건너뛰어지면 이 값은 계속
        // false로 남는다.
        Assert.True(runtime.Dex.IsShutdownRequested);
        Assert.True(host.IsDisposed);
    }

    [Fact]
    public async Task ShutdownAsync_IsIdempotentAndReportsNoErrorsOnSecondCall()
    {
        using var root = new TempHostRoot();
        var host = root.CreateHost();
        host.RuntimeFactory.Create();

        var first = await host.ShutdownAsync(null, null);
        var second = await host.ShutdownAsync(null, null);

        Assert.Empty(first);
        Assert.Empty(second);
    }

    [Fact]
    public void Dispose_AfterShutdownAsync_DoesNotThrow()
    {
        using var root = new TempHostRoot();
        var host = root.CreateHost();
        host.RuntimeFactory.Create();

        host.ShutdownAsync(null, null).GetAwaiter().GetResult();

        // 종료 훅이 ShutdownAsync를 부른 뒤 using이 Dispose를 또 부른다.
        host.Dispose();
    }

    [Fact]
    public async Task ShutdownAsync_LogsEachCollectedExceptionAtTheStepThatFailed()
    {
        // Ruling C 회귀 방지: DisposeRuntimeServices가 하던 실패-로그 남기기가
        // ApplicationHost.ShutdownAsync로 옮겨오면서 사라졌던 결함이다.
        // 여기서는 가짜 중 유일하게 실패를 주입할 수 있는 키보드 서비스로
        // 재현한다 — 수집된 예외 목록에 그 예외가 들어있을 뿐 아니라,
        // 그 실패를 가리키는 ERROR 로그 항목도 남아야 한다. GUI에는 콘솔이
        // 없어 이 로그가 유일한 흔적이기 때문이다.
        var thrown = new InvalidOperationException("keyboard dispose boom");
        var keyboard = new FakeKeyboardService { ThrowOnDispose = thrown };
        using var root = new TempHostRoot();
        var host = root.CreateHost(keyboard: keyboard);

        var errors = await host.ShutdownAsync(null, null);

        Assert.Contains(thrown, errors);
        var expectedMessage = LocalizationService.Get(
            "Log.ApplicationHost.KeyboardServiceDisposeFailed");
        Assert.Contains(
            host.Log.GetSessionEntries(),
            entry =>
                entry.Contains("[ERROR]", StringComparison.Ordinal) &&
                entry.Contains(expectedMessage, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ShutdownAsync_DisposesKeyboardServiceBeforeTheRuntimeLoop()
    {
        // Ruling B 회귀 방지: 브리핑을 그대로 옮긴 코드는 DeviceMonitor·키보드
        // 해제를 런타임 반복문 뒤로 미뤄버렸었다. 원래 순서(감시 중지 →
        // 감시자·키보드 해제 → 런타임 정리)로 되돌린 것을 고정한다.
        // 가짜로 대체할 수 있는 참여자는 키보드 서비스뿐이라 순서 전체를
        // 못박을 수는 없다 — 키보드 해제 시점에 아직 런타임 반복문의 관측
        // 가능한 효과(Dex.RequestShutdown 호출)가 일어나지 않았다는 것만
        // 확인한다.
        using var root = new TempHostRoot();
        var keyboard = new FakeKeyboardService();
        var host = root.CreateHost(keyboard: keyboard);
        var runtime = host.RuntimeFactory.Create();

        var runtimeShutdownRequestedDuringKeyboardDispose = true;
        keyboard.OnDispose = () =>
            runtimeShutdownRequestedDuringKeyboardDispose =
                runtime.Dex.IsShutdownRequested;

        await host.ShutdownAsync(null, null);

        Assert.Equal(1, keyboard.DisposeCallCount);
        Assert.False(runtimeShutdownRequestedDuringKeyboardDispose);
        // 반복문이 실제로는 돌았다는 것도 함께 확인해, 위 단언이 "반복문이
        // 통째로 건너뛰어져서" 우연히 false가 된 게 아님을 보인다.
        Assert.True(runtime.Dex.IsShutdownRequested);
    }

    [Fact]
    public void UpdateSettings_AfterDispose_Throws()
    {
        using var root = new TempHostRoot();
        var host = root.CreateHost();
        host.Dispose();

        // Start/Stop이 세운 패턴과 같아야 한다 — 해제된 호스트에 쓰기를
        // 허용하면 디스크에 남는 마지막 값이 종료 순서에 좌우된다.
        Assert.Throws<ObjectDisposedException>(
            () => host.UpdateSettings(s => s.VirtualDisplay.Width = 1280));
    }
}
