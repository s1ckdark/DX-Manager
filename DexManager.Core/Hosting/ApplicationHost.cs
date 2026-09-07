using System.Threading;
using System.Threading.Tasks;
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

    private readonly object _settingsLock = new object();

    private int _disposed;
    private string _selectedSerial = string.Empty;

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

    /// <summary>
    /// 현재 설정. 읽기 전용으로 취급한다 — 수정과 저장은
    /// <see cref="UpdateSettings"/>를 거쳐야 갱신 유실이 없다.
    /// </summary>
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
    /// 물리 기기별 런타임을 하나로 유지하는 코디네이터.
    /// GUI는 이것을 통해서만 런타임을 얻는다. TUI는 단일 런타임 동작을
    /// 유지하므로 사용하지 않는다.
    /// </summary>
    public DeviceRuntimeCoordinator RuntimeCoordinator { get; private set; }

    /// <summary>
    /// 현재 선택된 기기의 transport serial. 진단 서비스가 이 값을 읽는다.
    /// 소비 호스트(TUI/GUI)가 갱신한다.
    /// <c>null</c>을 대입하면 <see cref="string.Empty"/>로 정규화되므로
    /// 이 속성은 절대 <c>null</c>을 반환하지 않는다.
    /// </summary>
    public string SelectedSerial
    {
        get => _selectedSerial;
        set
        {
            var next = value ?? string.Empty;
            var previous = _selectedSerial;
            if (string.Equals(previous, next, StringComparison.Ordinal)) return;
            _selectedSerial = next;
            SelectedSerialChanged?.Invoke(
                this,
                new SelectedSerialChangedEventArgs(previous, next));
        }
    }

    /// <summary>
    /// <see cref="SelectedSerial"/>이 실제로 바뀔 때 발생한다.
    /// 같은 값을 다시 대입하면 발생하지 않는다.
    /// </summary>
    public event EventHandler<SelectedSerialChangedEventArgs> SelectedSerialChanged;

    /// <summary>
    /// 기기 감시를 시작한다. 호스트 인스턴스 하나는 소비자 하나가 소유한다 —
    /// 여러 소비자가 한 호스트를 공유하지 않는다.
    /// </summary>
    public void Start()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(ApplicationHost));
        DeviceMonitor.Start();
    }

    /// <summary>
    /// 기기 감시를 중지한다. 해제된 호스트에서는 아무 일도 하지 않는다 —
    /// 정리 경로가 순서를 어겨 호출해도 앱이 죽지 않게 한다.
    /// </summary>
    public void Stop()
    {
        if (IsDisposed) return;
        DeviceMonitor.Stop();
    }

    /// <summary>
    /// 설정을 수정하고 저장한다. 수정과 저장이 한 잠금 안에서 일어나므로
    /// 소비자 둘이 동시에 읽기-수정-쓰기를 해도 갱신이 유실되지 않는다.
    /// 설정을 바꿀 때는 <see cref="Settings"/>를 직접 수정하지 말고
    /// 이 메서드를 쓴다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <paramref name="mutate"/>가 건드리지 않은 필드도 값이 바뀔 수 있다.
    /// 저장 경로가 <c>Save</c> → <c>SaveCore</c> → <c>EnsureDefaults()</c>로
    /// 이어지며, <c>EnsureDefaults</c>는 호출자가 들고 있는 살아있는
    /// <see cref="Settings"/> 객체 자체를 정규화한다. 폼을 이 객체에
    /// 양방향 바인딩하면 저장 직후 화면 값이 정규화된 값으로 바뀐다.
    /// </para>
    /// <para>
    /// 잠금 안에서 사용자 입력을 기다리지 않는다. 입력을 먼저 받아
    /// mutation으로 포장한 뒤 넘긴다 — 그러지 않으면 다른 소비자가
    /// 프롬프트가 닫힐 때까지 막힌다.
    /// </para>
    /// </remarks>
    /// <exception cref="ObjectDisposedException">
    /// 호스트가 이미 해제된 경우.
    /// </exception>
    public void UpdateSettings(Action<AppSettings> mutate)
    {
        if (mutate == null) throw new ArgumentNullException(nameof(mutate));
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(ApplicationHost));

        lock (_settingsLock)
        {
            mutate(Settings);
            SettingsService.Save(Settings);
        }
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

        RuntimeCoordinator = new DeviceRuntimeCoordinator(
            RuntimeFactory,
            RuntimeSessions);
    }

    /// <summary>
    /// 이 호스트가 이미 해제되었는지 여부. <see cref="ShutdownAsync"/>가 모든
    /// 정리 단계를 마친 시점에 원자적으로 <c>true</c>가 된다 — 정리 도중에는
    /// 여전히 <c>false</c>다. 해제된 호스트는 재사용할 수 없다 —
    /// <see cref="DeviceMonitor"/>가 <see cref="ObjectDisposedException"/>을 던진다.
    /// 인스턴스 하나는 한 번만 사용한다.
    /// </summary>
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    private int _shutdownStarted;

    /// <summary>
    /// 호스트가 소유한 런타임과 서비스를 정리한다. 멱등하다 — 두 번째
    /// 호출부터는 아무 일도 하지 않고 빈 목록을 돌려준다.
    /// 예외를 던지지 않고 수집해 돌려주므로, 호출자가 자기 방식으로
    /// 보고할 수 있다. 앞선 실패가 뒤의 정리를 막지 않는다.
    /// </summary>
    /// <param name="fallbackSerial">
    /// DeX 세션이 자기 serial을 모를 때 쓸 대체값. 없으면 <c>null</c>.
    /// <see cref="RuntimeCoordinator"/>가 모르는 런타임에만 쓰인다 —
    /// 코디네이터가 아는 런타임은 자기 결속 serial로 정리한다.
    /// </param>
    /// <param name="fallbackIdentity">
    /// 같은 용도의 물리 기기 identity 대체값. 없으면 <c>null</c>.
    /// 적용 범위도 <paramref name="fallbackSerial"/>과 같다.
    /// </param>
    public async Task<IReadOnlyList<Exception>> ShutdownAsync(
        string fallbackSerial,
        string fallbackIdentity)
    {
        if (Interlocked.Exchange(ref _shutdownStarted, 1) != 0)
            return Array.Empty<Exception>();

        var errors = new List<Exception>();

        // 원래 InteractiveHost.ShutdownAsync의 순서를 그대로 유지한다:
        // 감시 중지 → 감시자·키보드 해제 → 런타임 정리. 관례상 더 나은
        // 순서가 있어 보여도, 실제 기기로 검증된 이 순서를 임의로 바꾸지
        // 않는다 — 바꾸려면 그 자체가 별도의 의도적인 변경이어야 한다.
        try
        {
            DeviceMonitor?.Stop();
        }
        catch (Exception ex)
        {
            Log.Error(
                LocalizationService.Get(
                    "Log.ApplicationHost.DeviceMonitorStopFailed"),
                ex);
            errors.Add(ex);
        }

        try
        {
            DeviceMonitor?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Error(
                LocalizationService.Get(
                    "Log.ApplicationHost.DeviceMonitorDisposeFailed"),
                ex);
            errors.Add(ex);
        }

        try
        {
            _keyboardService?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Error(
                LocalizationService.Get(
                    "Log.ApplicationHost.KeyboardServiceDisposeFailed"),
                ex);
            errors.Add(ex);
        }

        foreach (var runtime in RuntimeFactory.CreatedInstances)
        {
            // 런타임마다 자기 기기를 겨눈다. CreatedInstances는 생성 순서만
            // 남은 목록이라 identity 결속을 잃어버렸으므로, 코디네이터에게
            // 되물어 복원한다. 이걸 하지 않고 호출자가 준 대체값 하나를
            // 모든 런타임에 그대로 뿌리면 두 가지가 깨진다.
            // (1) GUI는 대체값이 항상 (null, null)이라 세션이 이미 끝나고
            //     회수만 밀린 런타임을 통째로 건너뛴다 — overlay 설정이
            //     폰에 남아 재부팅해도 살아남는다.
            // (2) 런타임이 둘 이상일 때 한 기기의 serial이 다른 기기의
            //     런타임을 겨눈다.
            // 그 결과 코디네이터가 아는 런타임은 DeX를 한 번도 시작한 적이
            // 없어도 종료 때 overlay 회수를 시도한다. 이는 TUI가 이미 하고
            // 있던 동작과 같다 — 회수는 멱등이고, 남은 overlay를 놓치는 쪽이
            // 훨씬 비싸다.
            var runtimeSerial = fallbackSerial;
            var runtimeIdentity = fallbackIdentity;
            DeviceRuntimeBinding binding;
            if (RuntimeCoordinator != null &&
                runtime != null &&
                RuntimeCoordinator.TryGetBinding(
                    runtime.InstanceId,
                    out binding))
            {
                runtimeSerial = binding.Serial;
                runtimeIdentity = binding.Identity;
            }

            await ShutdownRuntimeAsync(
                runtime,
                runtimeSerial,
                runtimeIdentity,
                errors);
        }

        Interlocked.Exchange(ref _disposed, 1);
        return errors;
    }

    /// <summary>
    /// 런타임 하나를 정리한다. 순서는 TUI가 검증해 온 순서를 그대로 따른다 —
    /// 먼저 모든 서비스에 종료를 알려 새 작업을 막고, 단일창을 내리고,
    /// DeX overlay를 회수한 뒤, 마지막에 서비스를 해제한다.
    /// 수집하는 예외마다 그 자리에서 Error로 로그를 남긴다 — GUI는 콘솔이
    /// 없어 반환값의 <see cref="AggregateException"/> 메시지 말고는 달리
    /// 남는 흔적이 없기 때문이다.
    /// </summary>
    private async Task ShutdownRuntimeAsync(
        DeviceRuntimeServiceSet runtime,
        string fallbackSerial,
        string fallbackIdentity,
        ICollection<Exception> errors)
    {
        if (runtime == null) return;

        try
        {
            runtime.FileTransfers.RequestShutdown();
            runtime.PhoneTransfers.RequestShutdown();
            runtime.CompanionGuardian.RequestShutdown();
            runtime.ScreenOff.RequestShutdown();
            runtime.SingleWindows.RequestShutdown();
            runtime.Dex.RequestShutdown();
        }
        catch (Exception ex)
        {
            Log.Error(
                LocalizationService.Get(
                    "Log.ApplicationHost.RuntimeRequestShutdownFailed"),
                ex);
            errors.Add(ex);
        }

        try
        {
            runtime.SingleWindows.StopAll();
        }
        catch (Exception ex)
        {
            Log.Error(
                LocalizationService.Get(
                    "Log.ApplicationHost.RuntimeStopAllFailed"),
                ex);
            errors.Add(ex);
        }

        try
        {
            var serial = runtime.Dex.CurrentSession?.Serial ?? fallbackSerial;
            var identity = runtime.Dex.CurrentSession?.DeviceIdentity
                ?? fallbackIdentity
                ?? string.Empty;

            await runtime.Dex.ShutdownAsync(serial, identity);

            if (runtime.Dex.HasDeferredDisplayCleanup)
            {
                // 콘솔에 그대로 노출되는 예외 메시지이므로 문구를 바꾸지
                // 않는다 — 로그 쪽 설명만 지역화 키로 별도로 남긴다.
                var deferredError = new InvalidOperationException(
                    "DeX display cleanup was deferred because the " +
                    "target device was unavailable.");
                Log.Error(
                    LocalizationService.Get(
                        "Log.ApplicationHost.RuntimeDisplayCleanupDeferred"),
                    deferredError);
                errors.Add(deferredError);
            }
        }
        catch (Exception ex)
        {
            Log.Error(
                LocalizationService.Get(
                    "Log.ApplicationHost.RuntimeDexShutdownFailed"),
                ex);
            errors.Add(ex);
        }

        var disposables = new IDisposable[]
        {
            runtime.SingleWindows,
            runtime.Scrcpy,
            runtime.ScreenOff,
            runtime.PhoneTransfers,
            runtime.CompanionGuardian,
            runtime.FileTransfers
        };

        foreach (var disposable in disposables)
        {
            try
            {
                disposable.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error(
                    LocalizationService.Format(
                        "Log.ApplicationHost.RuntimeDisposalFailed",
                        disposable.GetType().Name),
                    ex);
                errors.Add(ex);
            }
        }
    }

    /// <summary>
    /// 호스트가 소유한 서비스를 정리한다. 멱등하다 — 내부적으로
    /// <see cref="ShutdownAsync"/>를 실행하므로 반복 호출해도 안전하다.
    /// 정리 중 발생한 예외는 모두 수집한 뒤 <see cref="AggregateException"/>으로
    /// 던진다 — 앞선 실패가 뒤의 정리를 막지 않게 하기 위함이다.
    /// </summary>
    public void Dispose()
    {
        // ShutdownAsync 안의 await가 호출자의 SynchronizationContext를 잡으면
        // UI 스레드에서 부를 때 교착한다. 스레드 풀로 넘겨 컨텍스트를 끊는다.
        var errors = Task
            .Run(() => ShutdownAsync(null, null))
            .GetAwaiter()
            .GetResult();

        if (errors.Count > 0)
        {
            throw new AggregateException(
                "ApplicationHost disposal did not complete cleanly.",
                errors);
        }
    }
}

/// <summary>
/// <see cref="ApplicationHost.SelectedSerialChanged"/>가 전달하는 값.
/// 두 속성 모두 <c>null</c>이 아니다.
/// </summary>
public sealed class SelectedSerialChangedEventArgs : EventArgs
{
    public SelectedSerialChangedEventArgs(string previous, string current)
    {
        Previous = previous ?? string.Empty;
        Current = current ?? string.Empty;
    }

    public string Previous { get; }
    public string Current { get; }
}
