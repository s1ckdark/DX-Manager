using DexManager.Hosting;
using DexManager.Models;
using DexManager.Services;
using DexManager.Tests.FakePlatform;

namespace DexManager.Tests;

public class ApplicationHostTests : IDisposable
{
    private readonly string _root;

    public ApplicationHostTests()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            "dxm-host-tests",
            Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }
        catch
        {
            // 임시 디렉터리 정리 실패는 테스트 결과에 영향을 주지 않는다.
        }
    }

    private ApplicationHost CreateHost(FakeKeyboardService keyboard = null) => new ApplicationHost(
        new FakePlatformService(),
        new FakePathProvider(_root),
        new FakeCaptureService(),
        keyboard ?? new FakeKeyboardService(),
        new FakeAutoStartService());

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
        // ApplicationHost가 읽는 것과 동일한 <_root>/config/settings.json 파일을 생성한다.
        var seedLog = new LogService();
        var seedSettingsService = new SettingsService(seedLog, _root);
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
        var seedSettingsService = new SettingsService(seedLog, _root);
        var seededSettings = seedSettingsService.Load();
        const string preConfiguredAdbPath = "/bin/ls";
        Assert.True(File.Exists(preConfiguredAdbPath));
        seededSettings.Paths.AdbPath = preConfiguredAdbPath;
        seededSettings.Paths.AdbSelectionMode = AdbSelectionMode.Auto;
        seedSettingsService.Save(seededSettings);

        var portablePathProvider = new FakePathProvider(_root, isPortablePackage: true);
        using var host = new ApplicationHost(
            new FakePlatformService(),
            portablePathProvider,
            new FakeCaptureService(),
            new FakeKeyboardService(),
            new FakeAutoStartService());

        // 포터블 패키지에서는 이미 유효했던 ADB 경로였더라도 기본 경로로 교체된다.
        Assert.NotEqual(preConfiguredAdbPath, host.Settings.Paths.AdbPath);
        Assert.Equal(
            portablePathProvider.ResolveDefaultAdbPath(),
            host.Settings.Paths.AdbPath);
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
}
