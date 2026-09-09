using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DexManager.FileTransfer;
using DexManager.Models;
using DexManager.Utils;

namespace DexManager.Services
{
    public sealed class EnvironmentCheckService
    {
        private readonly AdbService _adbService;
        private readonly ScrcpyService _scrcpyService;
        private readonly PathService _pathService;
        private readonly LogService _logService;
        private readonly SettingsService _settingsService;
        private readonly AppSettings _settings;
        private readonly Func<string> _targetSerialProvider;

        public EnvironmentCheckService(
            AdbService adbService,
            ScrcpyService scrcpyService,
            PathService pathService,
            LogService logService,
            SettingsService settingsService,
            AppSettings settings,
            Func<string> targetSerialProvider = null)
        {
            _adbService = adbService;
            _scrcpyService = scrcpyService;
            _pathService = pathService;
            _logService = logService;
            _settingsService = settingsService;
            _settings = settings;
            _targetSerialProvider = targetSerialProvider ?? (() => string.Empty);
        }

        public IList<EnvironmentCheckItem> Run()
        {
            var results = new List<EnvironmentCheckItem>();
            AddFileCheck(
                results,
                LocalizationService.Get("Environment.AdbFile"),
                _adbService.AdbPath);
            var scrcpyPath = _scrcpyService != null ? _scrcpyService.ScrcpyPath : _settings?.Paths?.ScrcpyPath;
            AddFileCheck(
                results,
                LocalizationService.Get("Environment.ScrcpyFile"),
                scrcpyPath);
            var proxyDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "tools",
                "adb-proxy");
            var proxyCandidate = Path.Combine(proxyDir, OperatingSystem.IsWindows() ? "DXMAdbProxy.exe" : "DXMAdbProxy");
            if (!File.Exists(proxyCandidate)) proxyCandidate = Path.Combine(proxyDir, "DXMAdbProxy.dll");
            if (!File.Exists(proxyCandidate)) proxyCandidate = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DXMAdbProxy.dll");
            if (!File.Exists(proxyCandidate)) proxyCandidate = Path.Combine(proxyDir, "DXMAdbProxy.exe");

            AddFileTransferHelperCheck(results, proxyCandidate);
            AddAdbVersionCheck(results);
            results.Add(BuildScrcpyVersionCheck(
                ResolveScrcpyRuntimeInfo(
                    _scrcpyService,
                    scrcpyPath,
                    _logService)));

            results.Add(new EnvironmentCheckItem
            {
                Name = LocalizationService.Get(
                    "Environment.WindowsVersion"),
                Status = EnvironmentCheckStatus.Passed,
                Message = PlatformHelper.GetDisplayName()
            });

            var inPath = _pathService.IsAdbDirectoryInProcessPath(
                _adbService.AdbPath);
            var inSystemPath = _pathService.IsAdbDirectoryInSystemPath(
                _adbService.AdbPath);
            results.Add(new EnvironmentCheckItem
            {
                Name = "ADB PATH",
                Status = inSystemPath
                    ? EnvironmentCheckStatus.Passed
                    : EnvironmentCheckStatus.Warning,
                Message = inSystemPath
                    ? LocalizationService.Get(
                        "Environment.PathSystem")
                    : inPath
                        ? LocalizationService.Get(
                            "Environment.PathProcess")
                        : LocalizationService.Get(
                            "Environment.PathAbsolute")
            });

            results.Add(new EnvironmentCheckItem
            {
                Name = LocalizationService.Get("Environment.Admin"),
                Status = AdminHelper.IsAdministrator()
                    ? EnvironmentCheckStatus.Passed
                    : EnvironmentCheckStatus.Warning,
                Message = AdminHelper.IsAdministrator()
                    ? LocalizationService.Get("Environment.AdminYes")
                    : LocalizationService.Get("Environment.AdminNo")
            });

            try
            {
                var devices = _adbService.GetDevices();
                AddDeviceResult(results, devices);
                AddDeviceScreenshotFolderCheck(
                    results,
                    devices,
                    _targetSerialProvider());
            }
            catch (Exception ex)
            {
                results.Add(new EnvironmentCheckItem
                {
                    Name = LocalizationService.Get(
                        "Environment.AdbDevice"),
                    Status = EnvironmentCheckStatus.Failed,
                    Message = LocalizationService.Format(
                        "Environment.DeviceQueryFailed",
                        ex.Message)
                });
            }

            AddPcScreenshotFolderCheck(results);
            _logService.Info(LocalizationService.Get(
                "Log.Environment.Completed"));
            return results;
        }

        internal static ScrcpyRuntimeInfo ResolveScrcpyRuntimeInfo(
            ScrcpyService scrcpyService,
            string scrcpyPath,
            LogService logService)
        {
            if (scrcpyService != null)
                return scrcpyService.RuntimeInfo;

            // The macOS host builds scrcpy services per physical device, so the
            // diagnostics page probes the configured executable directly.
            if (string.IsNullOrWhiteSpace(scrcpyPath) || !File.Exists(scrcpyPath))
                return null;

            try
            {
                return ScrcpyRuntimeInfo.Detect(
                    scrcpyPath,
                    3000,
                    new ProcessRunner(logService));
            }
            catch
            {
                return null;
            }
        }

        internal static EnvironmentCheckItem BuildScrcpyVersionCheck(
            ScrcpyRuntimeInfo runtimeInfo)
        {
            var name = LocalizationService.Get(
                "Environment.ScrcpyVersion");
            if (runtimeInfo == null)
            {
                return new EnvironmentCheckItem
                {
                    Name = name,
                    Status = EnvironmentCheckStatus.Warning,
                    Message = LocalizationService.Get(
                        "Environment.ScrcpyVersionUnknown")
                };
            }

            var supported = runtimeInfo.MeetsRecommendedVersion;
            return new EnvironmentCheckItem
            {
                Name = name,
                Status = supported
                    ? EnvironmentCheckStatus.Passed
                    : EnvironmentCheckStatus.Warning,
                Message = LocalizationService.Format(
                    supported
                        ? "Environment.ScrcpyVersionValue"
                        : "Environment.ScrcpyVersionOutdated",
                    runtimeInfo.DisplayVersion,
                    runtimeInfo.SdlMajorVersion)
            };
        }

        private void AddAdbVersionCheck(
            ICollection<EnvironmentCheckItem> results)
        {
            try
            {
                var version = _adbService.GetVersion();
                var displayVersion = AdbVersionParser.GetDisplayVersion(
                    version.StandardOutput,
                    LocalizationService.Get(
                        "Settings.NoVersionOutput"));
                results.Add(new EnvironmentCheckItem
                {
                    Name = LocalizationService.Get(
                        "Environment.AdbVersion"),
                    Status = version.IsSuccess
                        ? EnvironmentCheckStatus.Passed
                        : EnvironmentCheckStatus.Failed,
                    Message = version.IsSuccess
                        ? displayVersion
                        : version.StandardError
                });
            }
            catch (Exception ex)
            {
                results.Add(new EnvironmentCheckItem
                {
                    Name = LocalizationService.Get(
                        "Environment.AdbVersion"),
                    Status = EnvironmentCheckStatus.Failed,
                    Message = ex.Message
                });
            }
        }

        // Existence proves nothing on its own: the debug build is
        // framework-dependent, and without a discoverable .NET runtime the
        // apphost aborts before Main(). So the candidate is launched.
        //
        // The candidate comes from the same search chain FileTransferCoordinator
        // uses for the path it hands scrcpy as the ADB environment variable, and
        // no production caller passes that constructor an explicit proxyPath, so
        // today the two resolve to the same file. The chains are duplicated, not
        // shared - see docs/TODO.md. This check speaks for the file it launched.
        private void AddFileTransferHelperCheck(
            ICollection<EnvironmentCheckItem> results,
            string path)
        {
            results.Add(BuildFileTransferHelperCheck(
                path,
                _settings.Timing.ProcessTimeoutMs,
                (candidate, timeoutMs) => new ProcessRunner(_logService).Run(
                    candidate,
                    FileTransferEnvironment.SelfTestArgument,
                    null,
                    timeoutMs,
                    false)));
        }

        /// <summary>
        /// 자기진단 실행에 허용할 시간을 일반 프로세스 예산에서 유도한다.
        /// </summary>
        // A third of the general process budget - 5000ms at the 15000ms default.
        // AppSettings.EnsureDefaults does not clamp ProcessTimeoutMs; NormalizeRange
        // substitutes the 15000ms default whenever the stored value falls outside
        // 1000..120000, so the value reaching this method is always in that range
        // and the quotient never drops below 333ms. A working self-test measured
        // 20ms on this machine, so even the floor leaves a wide margin.
        //
        // The bound this buys is not exactly this value: on a timeout
        // ProcessRunner.Run waits a further 2000ms in TryTerminateProcess for the
        // kill to land, so a wedged proxy holds the diagnostics page for up to
        // timeoutMs + 2000ms - about 7s at the default, against 17s if the full
        // ADB budget were used here.
        internal static int ResolveSelfTestTimeoutMs(int processTimeoutMs)
        {
            return processTimeoutMs / 3;
        }

        // The general process budget is taken as a parameter rather than read
        // from settings here so that a test can observe the timeout this check
        // actually hands the runner, not just the arithmetic that derives it.
        internal static EnvironmentCheckItem BuildFileTransferHelperCheck(
            string path,
            int processTimeoutMs,
            Func<string, int, ProcessResult> selfTestRunner)
        {
            var name = LocalizationService.Get(
                "Environment.FileTransferHelper");
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return new EnvironmentCheckItem
                {
                    Name = name,
                    Status = EnvironmentCheckStatus.Failed,
                    Message = LocalizationService.Format(
                        "Environment.FileMissing",
                        path)
                };
            }

            ProcessResult result;
            try
            {
                result = selfTestRunner(
                    path,
                    ResolveSelfTestTimeoutMs(processTimeoutMs));
            }
            catch (Exception ex)
            {
                // The exception is swallowed so one broken helper cannot end the
                // whole diagnostics run, but the item stays Failed: reporting
                // Passed after swallowing is the defect this check removes.
                return BuildHelperFailure(name, ex.Message);
            }

            // A null result carries no evidence either way, which is the same
            // situation as a run that never answered. This reason is a complete
            // sentence and names its own subject, so it is not wrapped in
            // Environment.HelperRunFailed the way a process-supplied reason is.
            if (result == null || result.TimedOut)
            {
                return new EnvironmentCheckItem
                {
                    Name = name,
                    Status = EnvironmentCheckStatus.Failed,
                    Message = LocalizationService.Get(
                        "Environment.HelperNoResponse")
                };
            }

            // Both conditions are required. Measured on this machine, an apphost
            // that cannot find the runtime exits 131 and prints nothing on
            // stdout, so the exit code alone would already be enough; the marker
            // is kept because the exit code comes from an apphost this project
            // does not control, and an earlier session reported seeing exit 0
            // for the same failure. The marker is the shared constant the proxy
            // itself prints (FileTransferEnvironment), so it cannot drift; it
            // only appears after Main() starts, which is what separates "the
            // process ran" from "the apphost died before any managed code".
            var passed = result.IsSuccess &&
                (result.StandardOutput ?? string.Empty).IndexOf(
                    FileTransferEnvironment.SelfTestSuccessMarker,
                    StringComparison.OrdinalIgnoreCase) >= 0;
            if (passed)
            {
                return new EnvironmentCheckItem
                {
                    Name = name,
                    Status = EnvironmentCheckStatus.Passed,
                    Message = path
                };
            }

            return BuildHelperFailure(name, DescribeHelperFailure(result));
        }

        private static EnvironmentCheckItem BuildHelperFailure(
            string name,
            string reason)
        {
            return new EnvironmentCheckItem
            {
                Name = name,
                Status = EnvironmentCheckStatus.Failed,
                Message = LocalizationService.Format(
                    "Environment.HelperRunFailed",
                    CollapseLines(reason))
            };
        }

        // The apphost failure spans several lines and the diagnostics list
        // prints one line per item, so the reason is joined with the separator
        // the other Environment.* messages already use.
        private static string CollapseLines(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            return string.Join(
                " · ",
                value.Split(
                    new[] { "\r\n", "\n", "\r" },
                    StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim())
                    .Where(line => line.Length > 0));
        }

        private static string DescribeHelperFailure(ProcessResult result)
        {
            if (!string.IsNullOrWhiteSpace(result.StandardError))
                return result.StandardError;
            if (!string.IsNullOrWhiteSpace(result.StandardOutput))
                return result.StandardOutput;
            return LocalizationService.Format(
                "Environment.HelperNoOutput",
                result.ExitCode);
        }

        private void AddPcScreenshotFolderCheck(
            ICollection<EnvironmentCheckItem> results)
        {
            var path = _settingsService.ResolvePath(
                _settings.Paths.ScreenshotFolder);
            try
            {
                Directory.CreateDirectory(path);
                var testPath = Path.Combine(
                    path,
                    ".dx_manager_write_test");
                File.WriteAllText(testPath, "ok");
                File.Delete(testPath);
                results.Add(new EnvironmentCheckItem
                {
                    Name = LocalizationService.Get(
                        "Environment.PcCaptureFolder"),
                    Status = EnvironmentCheckStatus.Passed,
                    Message = path
                });
            }
            catch (Exception ex)
            {
                results.Add(new EnvironmentCheckItem
                {
                    Name = LocalizationService.Get(
                        "Environment.PcCaptureFolder"),
                    Status = EnvironmentCheckStatus.Failed,
                    Message = LocalizationService.Format(
                        "Environment.FolderWriteFailed",
                        path,
                        ex.Message)
                });
            }
        }

        private void AddDeviceScreenshotFolderCheck(
            ICollection<EnvironmentCheckItem> results,
            IList<AdbDeviceInfo> devices,
            string serial)
        {
            var authorized = !string.IsNullOrWhiteSpace(serial) &&
                devices.Any(device =>
                    device.Status == AdbDeviceStatus.Device &&
                    string.Equals(
                        device.Serial,
                        serial,
                        StringComparison.OrdinalIgnoreCase));
            if (!authorized)
            {
                results.Add(new EnvironmentCheckItem
                {
                    Name = LocalizationService.Get(
                        "Environment.DeviceCaptureFolder"),
                    Status = EnvironmentCheckStatus.Warning,
                    Message = LocalizationService.Get(
                        "Environment.DeviceFolderSkipped")
                });
                return;
            }

            var folder = _settings.Paths.DeviceScreenshotFolder;
            var testFile = folder.TrimEnd('/') +
                "/.dx_manager_write_test";
            var command = "mkdir -p " + ShellQuote(folder) +
                " && touch " + ShellQuote(testFile) +
                " && rm -f " + ShellQuote(testFile);
            var result = _adbService.ShellForSerial(
                serial,
                command,
                false);
            results.Add(new EnvironmentCheckItem
            {
                Name = LocalizationService.Get(
                    "Environment.DeviceCaptureFolder"),
                Status = result.IsSuccess
                    ? EnvironmentCheckStatus.Passed
                    : EnvironmentCheckStatus.Failed,
                Message = result.IsSuccess
                    ? folder
                    : LocalizationService.Format(
                        "Environment.DeviceFolderFailed",
                        result.StandardError)
            });
        }

        private static string ShellQuote(string value)
        {
            return "'" + (value ?? string.Empty)
                .Replace("'", "'\\''") + "'";
        }

        private static void AddFileCheck(
            ICollection<EnvironmentCheckItem> results,
            string name,
            string path)
        {
            var exists = File.Exists(path);
            results.Add(new EnvironmentCheckItem
            {
                Name = name,
                Status = exists
                    ? EnvironmentCheckStatus.Passed
                    : EnvironmentCheckStatus.Failed,
                Message = exists
                    ? path
                    : LocalizationService.Format(
                        "Environment.FileMissing",
                        path)
            });
        }

        private static void AddDeviceResult(
            ICollection<EnvironmentCheckItem> results,
            IList<AdbDeviceInfo> devices)
        {
            var authorized = devices.FirstOrDefault(
                device => device.Status == AdbDeviceStatus.Device);
            if (authorized != null)
            {
                results.Add(new EnvironmentCheckItem
                {
                    Name = LocalizationService.Get(
                        "Environment.AdbDevice"),
                    Status = EnvironmentCheckStatus.Passed,
                    Message = LocalizationService.Format(
                        "Environment.DeviceAuthorized",
                        authorized.Serial)
                });
                return;
            }

            var unauthorized = devices.FirstOrDefault(
                device => device.Status == AdbDeviceStatus.Unauthorized);
            if (unauthorized != null)
            {
                results.Add(new EnvironmentCheckItem
                {
                    Name = LocalizationService.Get(
                        "Environment.AdbDevice"),
                    Status = EnvironmentCheckStatus.Warning,
                    Message = LocalizationService.Format(
                        "Environment.DeviceUnauthorized",
                        unauthorized.Serial)
                });
                return;
            }

            var offline = devices.FirstOrDefault(
                device => device.Status == AdbDeviceStatus.Offline);
            if (offline != null)
            {
                results.Add(new EnvironmentCheckItem
                {
                    Name = LocalizationService.Get(
                        "Environment.AdbDevice"),
                    Status = EnvironmentCheckStatus.Warning,
                    Message = LocalizationService.Get(
                        "Environment.DeviceOfflineMessage")
                });
                return;
            }

            results.Add(new EnvironmentCheckItem
            {
                Name = LocalizationService.Get(
                    "Environment.AdbDevice"),
                Status = EnvironmentCheckStatus.Failed,
                Message = LocalizationService.Get(
                    "Environment.NoDevice")
            });
        }
    }
}
