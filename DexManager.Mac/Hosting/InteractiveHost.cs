using DexManager.Hosting;
using DexManager.Mac.Platform;
using DexManager.Models;
using DexManager.Platform;
using DexManager.Services;
using DexManager.Utils;

namespace DexManager.Mac.Hosting;

public sealed class InteractiveHost : IDisposable
{
    private readonly ApplicationHost _host;

    // 기존 필드명을 유지해 나머지 1,000여 줄의 코드를 수정하지 않는다.
    private SettingsService _settingsService => _host.SettingsService;
    private LogService _logService => _host.Log;
    private ProcessRunner _processRunner => _host.ProcessRunner;
    private PathService _pathService => _host.PathService;
    private AdbService _adbService => _host.Adb;
    private WirelessAdbService _wirelessAdb => _host.WirelessAdb;
    private PhysicalDeviceRegistry _deviceRegistry => _host.DeviceRegistry;
    private DeviceRuntimeSessionRegistry _runtimeSessions => _host.RuntimeSessions;
    private DeviceMonitorService _deviceMonitor => _host.DeviceMonitor;
    private DisplayCleanupPermissionService _permissionService => _host.PermissionService;
    private EnvironmentCheckService _envCheckService => _host.EnvironmentCheck;
    private DiagnosticReportService _diagnosticReportService => _host.DiagnosticReport;
    private AppSettings _settings => _host.Settings;
    private DeviceRuntimeServiceFactory _runtimeFactory => _host.RuntimeFactory;
    private IKeyboardService _keyboardService => _host.KeyboardService;

    private DeviceRuntimeServiceSet _activeRuntime;
    private string _selectedDeviceSerial
    {
        get => _host.SelectedSerial;
        set => _host.SelectedSerial = value;
    }
    private string _selectedDeviceIdentity;
    private bool _isRunning;
    private bool _disposed;
    private int _shutdownStarted;

    public InteractiveHost()
    {
        _host = MacApplicationHostFactory.Create();

        _host.DeviceMonitor.DeviceConnected += (_, e) =>
        {
            var d = e.Current;
            AnsiConsole.Success($"Device connected: {d.DisplayName} [{d.Serial}]");
        };
        _host.DeviceMonitor.DeviceDisconnected += (_, e) =>
        {
            var d = e.Current;
            AnsiConsole.Warning($"Device disconnected: {d.DisplayName} [{d.Serial}]");
        };
        _host.DeviceMonitor.StateChanged += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(_selectedDeviceSerial) && e.Current.IsConnected)
            {
                _selectedDeviceSerial = e.Current.Serial;
            }
        };
    }

        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            _isRunning = true;
            _host.Start();

            AnsiConsole.Clear();
            PrintBanner();

            while (_isRunning && !cancellationToken.IsCancellationRequested)
            {
                PrintDashboard();
                Console.Write($"\n{AnsiConsole.Bold}{AnsiConsole.BrightCyan}DX-Manager (macOS) >> {AnsiConsole.Reset}");
                var input = Console.ReadLine()?.Trim();

                if (string.IsNullOrWhiteSpace(input)) continue;

                switch (input.ToUpperInvariant())
                {
                    case "1":
                        await StartDexAsync(cancellationToken);
                        break;
                    case "2":
                        await StopDexAsync(
                            cleanupUntrackedOverlay: true,
                            cancellationToken: cancellationToken);
                        break;
                    case "3":
                        await StartSingleWindowAsync();
                        break;
                    case "4":
                        await StopSingleWindowAsync();
                        break;
                    case "5":
                        await ManageWirelessAdbAsync();
                        break;
                    case "6":
                        await ManageFileTransferAsync();
                        break;
                    case "7":
                        await RunDiagnosticsAsync(cancellationToken);
                        break;
                    case "8":
                        await ManageCompanionAppAsync();
                        break;
                    case "9":
                        ManageSettings();
                        break;
                    case "S":
                        SelectTargetDevice();
                        break;
                    case "L":
                        ViewRecentLogs();
                        break;
                    case "C":
                    case "CLEAR":
                        AnsiConsole.Clear();
                        PrintBanner();
                        break;
                    case "Q":
                    case "QUIT":
                    case "EXIT":
                        _isRunning = false;
                        break;
                    default:
                        AnsiConsole.Warning($"Unknown command: '{input}'. Enter 1-9, S, L, C, or Q.");
                        Thread.Sleep(800);
                        break;
                }
            }

            await ShutdownAsync();
        }

        private void PrintBanner()
        {
            AnsiConsole.WriteLine($"{AnsiConsole.BrightBlue}╔══════════════════════════════════════════════════════════════════════╗");
            AnsiConsole.WriteLine($"{AnsiConsole.BrightBlue}║  {AnsiConsole.Bold}{AnsiConsole.BrightWhite}DX MANAGER for macOS (.NET 8 Native Edition){AnsiConsole.Reset}{AnsiConsole.BrightBlue}                      ║");
            AnsiConsole.WriteLine($"{AnsiConsole.BrightBlue}║  {AnsiConsole.Dim}Samsung DeX & High-Performance Screen Mirroring Suite{AnsiConsole.Reset}{AnsiConsole.BrightBlue}               ║");
            AnsiConsole.WriteLine($"{AnsiConsole.BrightBlue}╚══════════════════════════════════════════════════════════════════════╝{AnsiConsole.Reset}");
        }

        private void PrintDashboard()
        {
            var snapshot = _deviceRegistry.Current;
            AnsiConsole.SubHeader("CONNECTED DEVICES & SYSTEM STATUS");

            if (snapshot.Devices.Count == 0)
            {
                AnsiConsole.WriteLine($"  {AnsiConsole.BrightYellow}[!] No devices connected. Plug in a Galaxy device or connect via Wireless ADB.{AnsiConsole.Reset}");
            }
            else
            {
                var index = 1;
                foreach (var device in snapshot.Devices)
                {
                    var primarySerial = device.SelectPreferredTransport(null)?.Serial ?? "(no-serial)";
                    var isSelected = string.Equals(primarySerial, _selectedDeviceSerial, StringComparison.OrdinalIgnoreCase);
                    var marker = isSelected ? $"{AnsiConsole.BrightGreen}* [ACTIVE]{AnsiConsole.Reset}" : "          ";
                    var transportList = string.Join(", ", device.Transports.Select(t => $"{t.Kind}: {t.Serial}"));
                    var status = device.IsConnected ? "Connected" : "Disconnected";

                    Console.WriteLine($"  {marker} [{index++}] {AnsiConsole.Bold}{device.DisplayName}{AnsiConsole.Reset} - Status: {AnsiConsole.BrightGreen}{status}{AnsiConsole.Reset}");
                    Console.WriteLine($"               Transports: {AnsiConsole.Dim}{transportList}{AnsiConsole.Reset}");
                }
            }

            var activeDevice = GetSelectedDevice();
            var activeSerial = GetPrimarySerial(activeDevice);
            var activeName = activeDevice != null ? $"{activeDevice.DisplayName} ({activeSerial})" : "None";

            Console.WriteLine();
            AnsiConsole.KeyValue("Selected Device", activeName, AnsiConsole.BrightCyan, AnsiConsole.Bold + AnsiConsole.BrightWhite);
            AnsiConsole.KeyValue("Resolution / DPI", $"{_settings.VirtualDisplay.Width}x{_settings.VirtualDisplay.Height} @ {_settings.VirtualDisplay.Dpi} DPI");
            AnsiConsole.KeyValue("Stream Bitrate/FPS", $"{_settings.Scrcpy.BitRate} / {_settings.Scrcpy.MaxFps} FPS");
            AnsiConsole.KeyValue("Screen Off / Awake", $"ScreenOff={_settings.Scrcpy.TurnScreenOff}, StayAwake={_settings.Scrcpy.StayAwake}");

            AnsiConsole.SubHeader("OPERATIONS MENU");
            Console.WriteLine($"  {AnsiConsole.BrightGreen}[1]{AnsiConsole.Reset} Start DeX Mode                {AnsiConsole.BrightRed}[2]{AnsiConsole.Reset} Stop DeX Mode");
            Console.WriteLine($"  {AnsiConsole.BrightGreen}[3]{AnsiConsole.Reset} Start Single App Window       {AnsiConsole.BrightRed}[4]{AnsiConsole.Reset} Stop Single App Window");
            Console.WriteLine($"  {AnsiConsole.BrightCyan}[5]{AnsiConsole.Reset} Wireless ADB Management       {AnsiConsole.BrightCyan}[6]{AnsiConsole.Reset} File Transfer Coordinator");
            Console.WriteLine($"  {AnsiConsole.BrightCyan}[7]{AnsiConsole.Reset} Diagnostics & Environment     {AnsiConsole.BrightCyan}[8]{AnsiConsole.Reset} DX Companion Guardian");
            Console.WriteLine($"  {AnsiConsole.BrightMagenta}[9]{AnsiConsole.Reset} Settings & Configuration      {AnsiConsole.BrightYellow}[S]{AnsiConsole.Reset} Select Active Device");
            Console.WriteLine($"  {AnsiConsole.BrightBlack}[L]{AnsiConsole.Reset} View Recent Logs              {AnsiConsole.BrightRed}[Q]{AnsiConsole.Reset} Exit DX Manager");
        }

        private PhysicalDeviceInfo GetSelectedDevice()
        {
            var snapshot = _deviceRegistry.Current;
            if (!string.IsNullOrWhiteSpace(_selectedDeviceIdentity))
            {
                var identityMatch = snapshot.Devices.FirstOrDefault(d =>
                    string.Equals(
                        d.Identity,
                        _selectedDeviceIdentity,
                        StringComparison.OrdinalIgnoreCase));
                if (identityMatch != null) return identityMatch;
                if (!PhysicalDeviceRegistry.IsTemporaryIdentity(
                    _selectedDeviceIdentity))
                {
                    return null;
                }
            }
            if (string.IsNullOrWhiteSpace(_selectedDeviceSerial))
            {
                return snapshot.Devices.FirstOrDefault();
            }

            return snapshot.Devices.FirstOrDefault(d =>
                d.Transports.Any(t => string.Equals(t.Serial, _selectedDeviceSerial, StringComparison.OrdinalIgnoreCase)) ||
                string.Equals(d.Identity, _selectedDeviceSerial, StringComparison.OrdinalIgnoreCase));
        }

        private string GetPrimarySerial(PhysicalDeviceInfo device)
        {
            if (device == null) return null;
            return device.SelectPreferredTransport(_selectedDeviceSerial)?.Serial;
        }

        private DeviceRuntimeServiceSet GetOrCreateRuntime()
        {
            if (_activeRuntime == null)
            {
                _activeRuntime = _runtimeFactory.Create();
            }
            return _activeRuntime;
        }

        public bool IsDexRunning =>
            _activeRuntime?.Dex.IsRunning == true;

        public bool IsDexCleanupComplete
        {
            get
            {
                var runtime = _activeRuntime;
                return runtime == null || runtime.Dex.IsCleanupComplete;
            }
        }

        public async Task<bool> WaitForDexCleanupAsync(
            CancellationToken cancellationToken = default)
        {
            var runtime = _activeRuntime;
            if (runtime == null) return true;

            var timeoutMs = (int)Math.Min(
                60000L,
                Math.Max(
                    15000L,
                    (long)_settings.Timing.ProcessTimeoutMs * 2L));
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            var nextRetryUtc = DateTime.MinValue;
            while (!runtime.Dex.IsCleanupComplete &&
                   DateTime.UtcNow < deadline)
            {
                if (!runtime.Dex.IsRunning &&
                    runtime.Dex.CurrentSession == null &&
                    runtime.Dex.HasDeferredDisplayCleanup &&
                    DateTime.UtcNow >= nextRetryUtc)
                {
                    nextRetryUtc = DateTime.UtcNow.AddSeconds(1);
                    var device = GetSelectedDevice();
                    var serial = GetPrimarySerial(device);
                    if (device != null &&
                        !string.IsNullOrWhiteSpace(serial))
                    {
                        await runtime.Dex.CleanupConnectedOverlayAsync(
                            serial,
                            device.Identity);
                    }
                }
                await Task.Delay(100, cancellationToken);
            }
            return runtime.Dex.IsCleanupComplete;
        }

        private void SelectTargetDevice()
        {
            var snapshot = _deviceRegistry.Current;
            if (snapshot.Devices.Count == 0)
            {
                AnsiConsole.Warning("No connected devices to select.");
                return;
            }

            AnsiConsole.Header("SELECT ACTIVE DEVICE");
            for (var i = 0; i < snapshot.Devices.Count; i++)
            {
                var d = snapshot.Devices[i];
                var s = GetPrimarySerial(d);
                Console.WriteLine($"  [{i + 1}] {d.DisplayName} - Serial: {s}");
            }

            Console.Write("\nEnter device number: ");
            var choice = Console.ReadLine();
            if (int.TryParse(choice, out var idx) && idx >= 1 && idx <= snapshot.Devices.Count)
            {
                var target = snapshot.Devices[idx - 1];
                _selectedDeviceSerial = GetPrimarySerial(target);
                _selectedDeviceIdentity = target.Identity;
                AnsiConsole.Success($"Selected device: {target.DisplayName}");
            }
        }

        public async Task<bool> StartDexAsync(
            CancellationToken cancellationToken = default)
        {
            await WaitForDeviceSnapshotAsync(cancellationToken);
            var device = GetSelectedDevice();
            var serial = GetPrimarySerial(device);
            if (string.IsNullOrWhiteSpace(serial))
            {
                AnsiConsole.Error("No device connected. Please connect a Galaxy device.");
                return false;
            }
            _selectedDeviceSerial = serial;
            _selectedDeviceIdentity = device.Identity;
            AnsiConsole.Header($"STARTING DeX ON {device.DisplayName}");
            var runtime = GetOrCreateRuntime();

            try
            {
                AnsiConsole.Info("Configuring overlay display resolution and launching scrcpy...");
                if (!await runtime.Dex.StartAsync(
                    serial,
                    device.Identity,
                    cancellationToken))
                {
                    AnsiConsole.Warning(
                        "A new DeX session was not started. " +
                        "Stop the current session before choosing another device.");
                    await Task.Delay(1000);
                    return false;
                }
                _selectedDeviceSerial =
                    runtime.Dex.CurrentSession?.Serial ?? serial;
                _selectedDeviceIdentity =
                    runtime.Dex.CurrentSession?.DeviceIdentity ??
                    device.Identity;
                AnsiConsole.Success("DeX successfully started!");
                await Task.Delay(1000);
                return true;
            }
            catch (OperationCanceledException)
            {
                AnsiConsole.Info("DeX launch was cancelled; cleanup is in progress.");
                throw;
            }
            catch (Exception ex)
            {
                AnsiConsole.Error($"DeX launch error: {ex.Message}");
                await Task.Delay(1000);
                return false;
            }
        }

        public async Task<bool> StopDexAsync(
            bool cleanupUntrackedOverlay = false,
            CancellationToken cancellationToken = default)
        {
            var runtime = _activeRuntime;
            var stopKnownRuntime = !cleanupUntrackedOverlay;
            PhysicalDeviceInfo device = null;
            string serial = null;

            if (stopKnownRuntime && runtime == null)
            {
                AnsiConsole.Warning("No active DeX session is tracked.");
                return false;
            }

            if (cleanupUntrackedOverlay)
            {
                stopKnownRuntime = runtime != null &&
                    (runtime.Dex.CurrentSession != null ||
                     runtime.Dex.IsRunning);
            }

            if (!stopKnownRuntime)
            {
                await WaitForDeviceSnapshotAsync(cancellationToken);
                device = GetSelectedDevice();
                if (device == null)
                {
                    AnsiConsole.Warning("No target device selected.");
                    return false;
                }

                serial = GetPrimarySerial(device);
                if (string.IsNullOrWhiteSpace(serial) ||
                    !_adbService.IsAuthorizedDeviceConnected(serial))
                {
                    AnsiConsole.Warning(
                        "The target device is not connected and authorized; " +
                        "DeX display cleanup was not attempted.");
                    return false;
                }

                runtime = GetOrCreateRuntime();
            }

            var targetName = device?.DisplayName ??
                runtime.Dex.CurrentSession?.Serial ??
                "the selected device";
            AnsiConsole.Info($"Stopping DeX on {targetName}...");
            try
            {
                if (stopKnownRuntime)
                {
                    if (!await runtime.Dex.StopOrConfirmCleanupAsync())
                    {
                        AnsiConsole.Warning(
                            "DeX stopped, but display cleanup was deferred until " +
                            "the target device reconnects.");
                        await Task.Delay(800);
                        return false;
                    }
                }
                else
                {
                    if (!await runtime.Dex.CleanupConnectedOverlayAsync(
                        serial,
                        device.Identity))
                    {
                        throw new InvalidOperationException(
                            "The DeX display overlay could not be removed.");
                    }
                }
                AnsiConsole.Success("DeX session stopped and display overlay cleaned up.");
                await Task.Delay(800);
                return true;
            }
            catch (Exception ex)
            {
                AnsiConsole.Error($"Error stopping DeX: {ex.Message}");
                await Task.Delay(800);
                return false;
            }
        }

        public async Task StartSingleWindowAsync()
        {
            var device = GetSelectedDevice();
            var serial = GetPrimarySerial(device);
            if (string.IsNullOrWhiteSpace(serial))
            {
                AnsiConsole.Error("No device connected.");
                return;
            }

            AnsiConsole.Header($"START SINGLE APP WINDOW ({device.DisplayName})");
            Console.Write("Enter Slot Number (1-3) [default: 1]: ");
            var slotInput = Console.ReadLine();
            var slot = int.TryParse(slotInput, out var parsedSlot) && parsedSlot >= 1 && parsedSlot <= 3 ? parsedSlot : 1;

            Console.Write("Enter Android App Package Name (e.g. com.sec.android.app.sbrowser): ");
            var appPackage = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(appPackage))
            {
                AnsiConsole.Warning("App package name is required for Single Window mode.");
                return;
            }

            var runtime = GetOrCreateRuntime();
            try
            {
                AnsiConsole.Info($"Launching Window #{slot} ({appPackage}) for {device.DisplayName}...");
                var slotSettings = new SingleWindowSlotSettings
                {
                    Slot = slot,
                    Width = _settings.VirtualDisplay.Width,
                    Height = _settings.VirtualDisplay.Height,
                    Dpi = _settings.VirtualDisplay.Dpi,
                    BitRate = _settings.Scrcpy.BitRate,
                    MaxFps = _settings.Scrcpy.MaxFps,
                    StayAwake = _settings.Scrcpy.StayAwake,
                    TurnScreenOff = _settings.Scrcpy.TurnScreenOff,
                    StartAppPackage = appPackage,
                    AdditionalArguments = _settings.Scrcpy.AdditionalArguments
                };

                runtime.SingleWindows.Start(slot, slotSettings, serial);
                AnsiConsole.Success($"Single window #{slot} launched!");
            }
            catch (Exception ex)
            {
                AnsiConsole.Error($"Failed to launch single window: {ex.Message}");
            }

            await Task.Delay(1000);
        }

        public async Task StopSingleWindowAsync()
        {
            var runtime = GetOrCreateRuntime();
            Console.Write("Enter Slot Number to stop (1-3) or 'A' for all: ");
            var slotInput = Console.ReadLine()?.Trim().ToUpperInvariant();

            if (slotInput == "A" || slotInput == "ALL")
            {
                runtime.SingleWindows.StopAll();
                AnsiConsole.Success("Stopped all single window instances.");
            }
            else if (int.TryParse(slotInput, out var slot) && slot >= 1 && slot <= 3)
            {
                runtime.SingleWindows.Stop(slot);
                AnsiConsole.Success($"Stopped window #{slot}.");
            }

            await Task.Delay(800);
        }

        public async Task ManageWirelessAdbAsync()
        {
            AnsiConsole.Header("WIRELESS ADB MANAGEMENT");
            Console.WriteLine("  [1] Switch Selected USB Device to Wireless Mode");
            Console.WriteLine("  [2] Connect to Wireless Device (IP:Port)");
            Console.WriteLine("  [3] Disconnect Wireless Device");
            Console.WriteLine("  [4] Return to Main Menu");
            Console.Write("\nChoice: ");

            var choice = Console.ReadLine()?.Trim();
            switch (choice)
            {
                case "1":
                    var device = GetSelectedDevice();
                    if (device == null)
                    {
                        AnsiConsole.Error("No device connected via USB.");
                        break;
                    }
                    AnsiConsole.Info($"Enabling Wireless Mode for {device.DisplayName} [{device.Identity}]...");
                    var tcpResult = _wirelessAdb.EnableFromUsb(device.Identity, 5555);
                    if (tcpResult.Success)
                    {
                        AnsiConsole.Success($"Wireless ADB enabled: {tcpResult.Message} ({tcpResult.Endpoint})");
                    }
                    else
                    {
                        AnsiConsole.Error($"Failed to enable Wireless ADB: {tcpResult.Message}");
                    }
                    break;

                case "2":
                    Console.Write("Enter Device IP Address (e.g. 192.168.1.50): ");
                    var targetIp = Console.ReadLine()?.Trim();
                    Console.Write("Enter Port [default 5555]: ");
                    var portInput = Console.ReadLine()?.Trim();
                    var port = int.TryParse(portInput, out var p) ? p : 5555;

                    if (!string.IsNullOrWhiteSpace(targetIp))
                    {
                        AnsiConsole.Info($"Connecting to {targetIp}:{port}...");
                        var res = _wirelessAdb.Connect(targetIp, port);
                        if (res.Success) AnsiConsole.Success($"Connected: {res.Message} ({res.Endpoint})");
                        else AnsiConsole.Error($"Connection failed: {res.Message}");
                    }
                    break;

                case "3":
                    AnsiConsole.Info("Disconnecting wireless ADB sessions...");
                    var discRes = _wirelessAdb.Disconnect();
                    AnsiConsole.Success($"Disconnected: {discRes.Message}");
                    break;
            }

            await Task.Delay(1000);
        }

        public async Task ManageFileTransferAsync()
        {
            var device = GetSelectedDevice();
            var serial = GetPrimarySerial(device);
            if (string.IsNullOrWhiteSpace(serial))
            {
                AnsiConsole.Error("No device connected.");
                return;
            }

            AnsiConsole.Header("FILE TRANSFER COORDINATOR");
            Console.WriteLine("  [1] Send File or Folder to Phone (via ADB Push)");
            Console.WriteLine("  [2] Return to Main Menu");
            Console.Write("\nChoice: ");

            var choice = Console.ReadLine()?.Trim();
            if (choice == "1")
            {
                Console.Write("Enter local file or folder path: ");
                var path = Console.ReadLine()?.Trim().Trim('\'', '"');
                if (!string.IsNullOrWhiteSpace(path) && (File.Exists(path) || Directory.Exists(path)))
                {
                    var targetFolder = _settings.Paths.FileTransferTargetFolder;
                    if (string.IsNullOrWhiteSpace(targetFolder)) targetFolder = "/sdcard/Download";

                    Console.Write($"Enter remote target directory [default: {targetFolder}]: ");
                    var remote = Console.ReadLine()?.Trim();
                    if (string.IsNullOrWhiteSpace(remote)) remote = targetFolder;

                    AnsiConsole.Info($"Pushing '{path}' to '{remote}' on {device.DisplayName}...");
                    var pushRes = _adbService.PushForSerial(serial, path, remote);
                    if (pushRes.IsSuccess)
                    {
                        AnsiConsole.Success("Transfer completed successfully!");
                    }
                    else
                    {
                        AnsiConsole.Error($"Transfer failed: {pushRes.StandardError}");
                    }
                }
                else
                {
                    AnsiConsole.Warning("Local path does not exist.");
                }
            }

            await Task.Delay(1000);
        }

        public async Task RunDiagnosticsAsync(
            CancellationToken cancellationToken = default)
        {
            await WaitForDeviceSnapshotAsync(cancellationToken);
            AnsiConsole.Header("SYSTEM ENVIRONMENT & DIAGNOSTICS");
            AnsiConsole.Info("Running environment checks...");

            var results = _envCheckService.Run();
            foreach (var item in results)
            {
                var badgeColor = item.Status == EnvironmentCheckStatus.Passed ? AnsiConsole.BrightGreen :
                                 item.Status == EnvironmentCheckStatus.Warning ? AnsiConsole.BrightYellow : AnsiConsole.BrightRed;
                var badge = item.Status == EnvironmentCheckStatus.Passed ? "PASS" :
                            item.Status == EnvironmentCheckStatus.Warning ? "WARN" : "FAIL";

                Console.WriteLine($"  [{badgeColor}{badge}{AnsiConsole.Reset}] {item.Name,-28} : {item.Message}");
            }

            var device = GetSelectedDevice();
            var serial = GetPrimarySerial(device);
            if (!string.IsNullOrWhiteSpace(serial))
            {
                AnsiConsole.SubHeader($"DEVICE DIAGNOSTICS FOR {device.DisplayName}");
                var diag = new DeviceVersionDiagnosticService(_adbService).Inspect(serial, device);
                var companionStatus = _permissionService.Inspect(serial);
                var report = _diagnosticReportService.CreateReport(
                    Program.Version,
                    _adbService.AdbPath,
                    "Android Debug Bridge",
                    _settings.Paths.ScrcpyPath,
                    device.Identity,
                    _deviceRegistry.Current,
                    _runtimeSessions.Current,
                    diag,
                    companionStatus,
                    _logService.GetSessionEntries());

                Console.WriteLine(report);
            }

            Console.WriteLine("\nPress Enter to return...");
            Console.ReadLine();
            await Task.CompletedTask;
        }

        public async Task ManageCompanionAppAsync()
        {
            var device = GetSelectedDevice();
            var serial = GetPrimarySerial(device);
            if (string.IsNullOrWhiteSpace(serial))
            {
                AnsiConsole.Error("No device connected.");
                return;
            }

            AnsiConsole.Header($"DX COMPANION MANAGEMENT ({device.DisplayName})");
            var status = _permissionService.Inspect(serial);
            Console.WriteLine($"  Current Companion Status: {status.State}");
            Console.WriteLine($"  Detail:                   {status.Detail}");
            Console.WriteLine($"  Installed Package Code:   {status.VersionCode}");

            var bundled = _permissionService.InspectBundledApk();
            Console.WriteLine($"  Bundled Companion APK:    {bundled.State}");
            if (bundled.State == BundledCompanionState.Ready)
            {
                Console.WriteLine("\n  [1] Install Bundled Companion APK & Grant Permission");
            }
            else
            {
                Console.WriteLine("\n  [1] Install Bundled Companion APK & Grant Permission (unavailable)");
                AnsiConsole.Warning(
                    "This package does not contain a verified DX Companion APK. " +
                    "Automatic installation is unavailable.");
            }
            Console.WriteLine("  [2] Grant WRITE_SECURE_SETTINGS Permission Only");
            Console.WriteLine("  [3] Return to Main Menu");
            Console.Write("\nChoice: ");

            var choice = Console.ReadLine()?.Trim();
            if (choice == "1")
            {
                if (bundled.State != BundledCompanionState.Ready)
                {
                    AnsiConsole.Error(
                        "Installation was not started because a verified bundled APK is unavailable.");
                    await Task.Delay(1000);
                    return;
                }

                AnsiConsole.Info("Installing Companion APK and granting permissions...");
                var res = _permissionService.InstallAndGrant(serial);
                if (res.State == DisplayCleanupPermissionState.Granted || res.State == DisplayCleanupPermissionState.Ready)
                    AnsiConsole.Success($"Companion APK installed and granted! ({res.State})");
                else
                    AnsiConsole.Error($"Installation/Grant result: {res.State} - {res.Detail}");
            }
            else if (choice == "2")
            {
                AnsiConsole.Info("Granting WRITE_SECURE_SETTINGS...");
                var res = _permissionService.Grant(serial);
                if (res.State == DisplayCleanupPermissionState.Granted || res.State == DisplayCleanupPermissionState.Ready)
                    AnsiConsole.Success($"Permission granted! ({res.State})");
                else
                    AnsiConsole.Error($"Grant result: {res.State} - {res.Detail}");
            }

            await Task.Delay(1000);
        }

        public void ManageSettings()
        {
            AnsiConsole.Header("SETTINGS & PREFERENCES");
            AnsiConsole.KeyValue("1. Display Width", _settings.VirtualDisplay.Width.ToString());
            AnsiConsole.KeyValue("2. Display Height", _settings.VirtualDisplay.Height.ToString());
            AnsiConsole.KeyValue("3. Display DPI", _settings.VirtualDisplay.Dpi.ToString());
            AnsiConsole.KeyValue("4. Bitrate", _settings.Scrcpy.BitRate);
            AnsiConsole.KeyValue("5. Max FPS", _settings.Scrcpy.MaxFps.ToString());
            AnsiConsole.KeyValue("6. Turn Screen Off", _settings.Scrcpy.TurnScreenOff.ToString());
            AnsiConsole.KeyValue("7. Stay Awake", _settings.Scrcpy.StayAwake.ToString());
            AnsiConsole.KeyValue("8. Scrcpy Path", _settings.Paths.ScrcpyPath ?? "(auto)");
            AnsiConsole.KeyValue("9. ADB Path", _settings.Paths.AdbPath ?? "(auto)");

            Console.Write("\nEnter setting # to modify (or Enter to go back): ");
            var opt = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(opt)) return;

            // 입력을 먼저 받아 mutation으로 포장한다. UpdateSettings는
            // 잠금을 잡으므로 그 안에서 사용자 입력을 기다리면 다른
            // 소비자가 프롬프트가 닫힐 때까지 막힌다.
            //
            // mutate는 실제로 값이 바뀔 때만 설정한다 - 취소(빈 입력),
            // 잘못된 입력, 인식되지 않은 항목, 기존과 같은 값 재입력은
            // 모두 "바뀐 것 없음"으로 취급해 아래에서 저장하지도, 저장했다고
            // 말하지도 않는다. 이전에는 8/9가 아예 처리되지 않아(switch에
            // 없는 항목) mutate가 항상 null로 남았는데도 무조건 저장하고
            // "Settings updated and saved."를 출력해, 하지도 않은 편집을
            // 저장했다고 거짓 보고했다 - 1~7도 유효하지 않은 입력에서 같은
            // 거짓을 보고했다(실기 조사 §12).
            Action<AppSettings> mutate = null;
            switch (opt)
            {
                case "1":
                    Console.Write("Enter Width (e.g. 1920, 2560): ");
                    if (int.TryParse(Console.ReadLine(), out var w) &&
                        w != _settings.VirtualDisplay.Width)
                        mutate = s => s.VirtualDisplay.Width = w;
                    break;
                case "2":
                    Console.Write("Enter Height (e.g. 1080, 1440): ");
                    if (int.TryParse(Console.ReadLine(), out var h) &&
                        h != _settings.VirtualDisplay.Height)
                        mutate = s => s.VirtualDisplay.Height = h;
                    break;
                case "3":
                    Console.Write("Enter DPI (e.g. 160, 200, 240): ");
                    if (int.TryParse(Console.ReadLine(), out var dpi) &&
                        dpi != _settings.VirtualDisplay.Dpi)
                        mutate = s => s.VirtualDisplay.Dpi = dpi;
                    break;
                case "4":
                    Console.Write("Enter Bitrate (e.g. 16M, 24M, 32M): ");
                    var br = Console.ReadLine()?.Trim();
                    if (!string.IsNullOrWhiteSpace(br) &&
                        !string.Equals(br, _settings.Scrcpy.BitRate, StringComparison.Ordinal))
                        mutate = s => s.Scrcpy.BitRate = br;
                    break;
                case "5":
                    Console.Write("Enter Max FPS (e.g. 60, 120): ");
                    if (int.TryParse(Console.ReadLine(), out var fps) &&
                        fps != _settings.Scrcpy.MaxFps)
                        mutate = s => s.Scrcpy.MaxFps = fps;
                    break;
                case "6":
                    // 토글이므로 선택하는 순간 항상 값이 바뀐다.
                    mutate = s => s.Scrcpy.TurnScreenOff = !s.Scrcpy.TurnScreenOff;
                    break;
                case "7":
                    mutate = s => s.Scrcpy.StayAwake = !s.Scrcpy.StayAwake;
                    break;
                case "8":
                {
                    // 그냥 Enter는 절대 파괴적이지 않다 - 유지다. 지우려면
                    // "-"를 명시적으로 입력해야 한다(PathPromptInput 참고).
                    // 콘솔 입력은 GUI 텍스트 상자와 달리 항상 빈 줄에서
                    // 시작하므로, 빈 입력을 "비움"으로 해석하면 값을 보러
                    // 들어왔다가 취소하려는 사용자의 설정을 조용히 지우게
                    // 된다(코드 리뷰 지적).
                    var currentScrcpy = _settings.Paths.ScrcpyPath ?? string.Empty;
                    var currentScrcpyDisplay = string.IsNullOrEmpty(currentScrcpy)
                        ? "(auto)"
                        : currentScrcpy;
                    Console.Write(
                        $"Enter Scrcpy Path [{currentScrcpyDisplay}] (Enter to keep, '-' to clear): ");
                    var scrcpyResult = PathPromptInput.Resolve(currentScrcpy, Console.ReadLine());
                    if (scrcpyResult.Changed)
                        mutate = s => s.Paths.ScrcpyPath = scrcpyResult.Value;
                    break;
                }
                case "9":
                {
                    // ADB Path는 값만 쓰지 않는다 - PathService는
                    // AdbSelectionMode == Manual일 때만 Paths.AdbPath를
                    // 읽으므로(PathService.cs:43), 경로만 써 넣고 모드를
                    // 안 바꾸면 GUI PR #5가 고친 것과 같은 무동작 칸이
                    // 된다. GUI의 PathsSettingsViewModel.Save()와 같은
                    // 규칙(AdbPathEditRule)을 그대로 따른다: 트림 후
                    // 채워서 편집 -> Manual, 비워서 편집("-") -> Auto,
                    // 바뀌지 않았으면(Enter만 누름 포함) 모드를 건드리지
                    // 않는다. 그냥 Enter가 Manual 경로를 지우던 문제도
                    // PathPromptInput의 "빈 입력=유지" 규칙으로 함께 막혔다.
                    var currentAdb = _settings.Paths.AdbPath ?? string.Empty;
                    var currentAdbDisplay = string.IsNullOrEmpty(currentAdb) ? "(auto)" : currentAdb;
                    Console.Write(
                        $"Enter ADB Path [{currentAdbDisplay}] (Enter to keep, '-' to clear): ");
                    var adbInput = Console.ReadLine();
                    var adbResult = AdbPathEditRule.Apply(currentAdb, adbInput);
                    if (adbResult.Changed)
                    {
                        mutate = s =>
                        {
                            s.Paths.AdbPath = adbResult.TrimmedPath;
                            if (adbResult.NewMode.HasValue)
                                s.Paths.AdbSelectionMode = adbResult.NewMode.Value;
                        };
                    }
                    break;
                }
            }

            if (mutate != null)
            {
                _host.UpdateSettings(mutate);
                AnsiConsole.Success("Settings updated and saved.");
            }
            else
            {
                AnsiConsole.Warning("No changes were made.");
            }
            Thread.Sleep(800);
        }

        private void ViewRecentLogs()
        {
            AnsiConsole.Header("RECENT LOG ENTRIES");
            var entries = _logService.GetSessionEntries();
            var count = Math.Min(30, entries.Length);
            for (var i = entries.Length - count; i < entries.Length; i++)
            {
                Console.WriteLine(entries[i]);
            }
            Console.WriteLine("\nPress Enter to return...");
            Console.ReadLine();
        }

        private async Task WaitForDeviceSnapshotAsync(
            CancellationToken cancellationToken = default)
        {
            _host.Start();
            var pollTimeoutMs = (int)Math.Min(
                60000L,
                Math.Max(
                    15000L,
                    (long)_settings.Timing.ProcessTimeoutMs * 3L));
            var completed = await Task.Run(
                () => _deviceMonitor.WaitForFirstPoll(
                    pollTimeoutMs,
                    cancellationToken),
                cancellationToken);
            if (!completed)
            {
                AnsiConsole.Warning(
                    "The first device scan did not finish before the timeout. " +
                    "The connected-device list may still be incomplete.");
            }
        }

        public async Task ShutdownAsync()
        {
            if (Interlocked.Exchange(ref _shutdownStarted, 1) != 0) return;

            _isRunning = false;
            var device = GetSelectedDevice();
            var fallbackSerial = GetPrimarySerial(device) ??
                _selectedDeviceSerial;
            var fallbackIdentity = device?.Identity ??
                _activeRuntime?.Dex.CurrentSession?.DeviceIdentity ??
                _selectedDeviceIdentity ??
                string.Empty;

            AnsiConsole.Info("Shutting down DX Manager and cleaning up active sessions...");

            var errors = await _host.ShutdownAsync(
                fallbackSerial,
                fallbackIdentity);

            foreach (var error in errors)
            {
                AnsiConsole.Error($"Error during shutdown: {error.Message}");
            }

            if (errors.Count == 0)
            {
                AnsiConsole.Success("DX Manager stopped cleanly. Goodbye!");
            }
            else
            {
                AnsiConsole.Warning("DX Manager stopped, but one or more cleanup steps could not be confirmed.");
            }
        }

        public void Shutdown()
        {
            ShutdownAsync().GetAwaiter().GetResult();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Shutdown();
            _host?.Dispose();
        }
    }
