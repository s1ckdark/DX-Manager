using DexManager.Models;
using DexManager.Platform;
using DexManager.Services;
using DexManager.Utils;

namespace DexManager.Hosting;

/// <summary>
/// 플랫폼 중립 서비스 조립 루트. TUI와 GUI가 동일한 조립·정리 경로를
/// 공유하도록 한다. 화면 출력과 입력 처리는 포함하지 않는다.
/// </summary>
public sealed class ApplicationHost : IDisposable
{
    private const int AdbSelectionTimeoutMs = 5000;

    private readonly IPlatformService _platformService;
    private readonly IPathProvider _pathProvider;
    private readonly ICaptureService _captureService;
    private readonly IKeyboardService _keyboardService;
    private readonly IAutoStartService _autoStartService;

    private bool _disposed;

    public ApplicationHost(
        IPlatformService platformService,
        IPathProvider pathProvider,
        ICaptureService captureService,
        IKeyboardService keyboardService,
        IAutoStartService autoStartService)
    {
        _platformService = platformService
            ?? throw new ArgumentNullException(nameof(platformService));
        _pathProvider = pathProvider
            ?? throw new ArgumentNullException(nameof(pathProvider));
        _captureService = captureService
            ?? throw new ArgumentNullException(nameof(captureService));
        _keyboardService = keyboardService
            ?? throw new ArgumentNullException(nameof(keyboardService));
        _autoStartService = autoStartService
            ?? throw new ArgumentNullException(nameof(autoStartService));

        SelectedSerial = string.Empty;

        Log = new LogService();
        Log.SetLogDirectory(_pathProvider.DefaultLogDirectory);

        SettingsService = new SettingsService(Log, _pathProvider.BaseDirectory);
        Settings = SettingsService.Load();

        ProcessRunner = new ProcessRunner(Log);
        PathService = new PathService(
            SettingsService,
            Log,
            ProcessRunner,
            _pathProvider,
            _platformService);

        EnsureDefaultPaths();

        var adbPath = PathService.SelectAdbPath(Settings, AdbSelectionTimeoutMs);
        Adb = new AdbService(
            adbPath,
            Settings.Timing.ProcessTimeoutMs,
            ProcessRunner,
            Log);

        WirelessAdb = new WirelessAdbService(
            Adb,
            SettingsService,
            Settings,
            Log);

        DeviceRegistry = new PhysicalDeviceRegistry();
        RuntimeSessions = new DeviceRuntimeSessionRegistry();

        DeviceMonitor = new DeviceMonitorService(
            Adb,
            WirelessAdb,
            DeviceRegistry,
            Log,
            Settings.Timing.DeviceMonitorIntervalMs,
            Settings.Timing.DisconnectMonitorIntervalMs);

        PermissionService = new DisplayCleanupPermissionService(Adb);

        EnvironmentCheck = new EnvironmentCheckService(
            Adb,
            null,
            PathService,
            Log,
            SettingsService,
            Settings,
            () => SelectedSerial ?? string.Empty);

        DiagnosticReport = new DiagnosticReportService();

        InitializeRuntimeFactory();
    }

    public IPlatformService PlatformService => _platformService;
    public IPathProvider PathProvider => _pathProvider;
    public ICaptureService CaptureService => _captureService;
    public IKeyboardService KeyboardService => _keyboardService;
    public IAutoStartService AutoStartService => _autoStartService;

    public AppSettings Settings { get; }
    public SettingsService SettingsService { get; }
    public LogService Log { get; }
    public ProcessRunner ProcessRunner { get; }
    public PathService PathService { get; }
    public AdbService Adb { get; }
    public WirelessAdbService WirelessAdb { get; }
    public PhysicalDeviceRegistry DeviceRegistry { get; }
    public DeviceRuntimeSessionRegistry RuntimeSessions { get; }
    public DeviceMonitorService DeviceMonitor { get; }
    public DisplayCleanupPermissionService PermissionService { get; }
    public EnvironmentCheckService EnvironmentCheck { get; }
    public DiagnosticReportService DiagnosticReport { get; }
    public DeviceRuntimeServiceFactory RuntimeFactory { get; private set; }

    /// <summary>
    /// 현재 선택된 기기의 transport serial. 진단 서비스가 이 값을 읽는다.
    /// 소비 호스트(TUI/GUI)가 갱신한다.
    /// </summary>
    public string SelectedSerial { get; set; }

    /// <summary>
    /// 기기 감시를 시작한다. 호스트 인스턴스 하나는 소비자 하나가 소유한다 —
    /// 여러 소비자가 한 호스트를 공유하지 않는다.
    /// </summary>
    public void Start()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ApplicationHost));
        DeviceMonitor.Start();
    }

    /// <summary>
    /// 기기 감시를 중지한다. 해제된 호스트에서는 아무 일도 하지 않는다 —
    /// 정리 경로가 순서를 어겨 호출해도 앱이 죽지 않게 한다.
    /// </summary>
    public void Stop()
    {
        if (_disposed) return;
        DeviceMonitor.Stop();
    }

    private void EnsureDefaultPaths()
    {
        var modified = false;
        var currentAdb = Settings.Paths.AdbPath ?? string.Empty;
        var forcePortableAdb = _pathProvider.IsPortablePackage &&
            Settings.Paths.AdbSelectionMode != AdbSelectionMode.Manual;
        if (forcePortableAdb ||
            string.IsNullOrWhiteSpace(currentAdb) ||
            !File.Exists(currentAdb) ||
            currentAdb.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            var adb = _pathProvider.ResolveDefaultAdbPath();
            if (File.Exists(adb))
            {
                Settings.Paths.AdbPath = adb;
                modified = true;
            }
        }

        var currentScrcpy = Settings.Paths.ScrcpyPath ?? string.Empty;
        if (_pathProvider.IsPortablePackage ||
            string.IsNullOrWhiteSpace(currentScrcpy) ||
            !File.Exists(currentScrcpy) ||
            currentScrcpy.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            var scrcpy = _pathProvider.ResolveDefaultScrcpyPath();
            if (File.Exists(scrcpy))
            {
                Settings.Paths.ScrcpyPath = scrcpy;
                modified = true;
            }
        }

        if (Settings.Scrcpy != null &&
            (Settings.Scrcpy.UseHidKeyboard || Settings.Scrcpy.UseHidMouse))
        {
            Settings.Scrcpy.UseHidKeyboard = false;
            Settings.Scrcpy.UseHidMouse = false;
            modified = true;
        }

        if (Settings.SingleWindowSlots != null)
        {
            foreach (var slot in Settings.SingleWindowSlots)
            {
                if (slot != null && (slot.UseHidKeyboard || slot.UseHidMouse))
                {
                    slot.UseHidKeyboard = false;
                    slot.UseHidMouse = false;
                    modified = true;
                }
            }
        }

        if (modified)
        {
            SettingsService.Save(Settings);
        }
    }

    private void InitializeRuntimeFactory()
    {
        var scrcpyPath = Settings.Paths.ScrcpyPath;
        if (string.IsNullOrWhiteSpace(scrcpyPath) ||
            !File.Exists(scrcpyPath) ||
            scrcpyPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            scrcpyPath = _pathProvider.ResolveDefaultScrcpyPath();
        }

        var adbPath = Adb.AdbPath;
        var coordinator = new ScrcpyLaunchCoordinator();

        RuntimeFactory = new DeviceRuntimeServiceFactory(
            scrcpyPath,
            adbPath,
            Settings.Timing.ProcessTimeoutMs,
            ProcessRunner,
            Adb,
            coordinator,
            SettingsService,
            Settings,
            Log,
            RuntimeSessions,
            _platformService);
    }

    /// <summary>
    /// 이 호스트가 이미 해제되었는지 여부. 해제된 호스트는 재사용할 수 없다 —
    /// <see cref="DeviceMonitor"/>가 <see cref="ObjectDisposedException"/>을 던진다.
    /// 인스턴스 하나는 한 번만 사용한다.
    /// </summary>
    public bool IsDisposed => _disposed;

    /// <summary>
    /// 호스트가 소유한 서비스를 정리한다. 멱등하다.
    /// 정리 중 발생한 예외는 모두 수집한 뒤 <see cref="AggregateException"/>으로
    /// 던진다 — 앞선 실패가 뒤의 정리를 막지 않게 하기 위함이다.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        var errors = new List<Exception>();

        try
        {
            DeviceMonitor?.Dispose();
        }
        catch (Exception ex)
        {
            errors.Add(ex);
        }

        try
        {
            _keyboardService?.Dispose();
        }
        catch (Exception ex)
        {
            errors.Add(ex);
        }

        if (errors.Count > 0)
        {
            throw new AggregateException(
                "ApplicationHost disposal did not complete cleanly.",
                errors);
        }
    }
}
