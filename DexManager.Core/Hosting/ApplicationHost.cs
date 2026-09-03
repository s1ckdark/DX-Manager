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

    private void EnsureDefaultPaths()
    {
        // Task 6에서 InteractiveHost로부터 이관한다.
    }

    private void InitializeRuntimeFactory()
    {
        // Task 6에서 InteractiveHost로부터 이관한다.
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}
