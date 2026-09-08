using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DexManager.Models;
using DexManager.Utils;

namespace DexManager.Services
{
    public sealed class AdbService
    {
        private const int PackageInstallTimeoutMs = 120000;
        private readonly string _adbPath;
        private readonly int _defaultTimeoutMs;
        private readonly ProcessRunner _processRunner;
        private readonly LogService _logService;

        public AdbService(
            string adbPath,
            int defaultTimeoutMs,
            ProcessRunner processRunner,
            LogService logService)
        {
            if (string.IsNullOrWhiteSpace(adbPath))
                throw new ArgumentException(
                    LocalizationService.Get("Error.Adb.PathEmpty"),
                    "adbPath");

            _adbPath = Path.GetFullPath(adbPath);
            _defaultTimeoutMs = Math.Max(defaultTimeoutMs, 1000);
            _processRunner = processRunner;
            _logService = logService;
        }

        public string AdbPath
        {
            get { return _adbPath; }
        }

        public bool IsProcessShutdownRequested
        {
            get { return _processRunner.IsShutdownRequested; }
        }

        public void BeginProcessShutdown()
        {
            _processRunner.BeginShutdown();
            TerminateSelectedAdbProcesses();
        }

        public void BlockNewProcessesForWindowsShutdown()
        {
            _processRunner.BlockNewProcessesForWindowsShutdown();
        }

        private void TerminateSelectedAdbProcesses()
        {
            var processName = Path.GetFileNameWithoutExtension(_adbPath);
            if (string.IsNullOrWhiteSpace(processName)) return;

            var terminatedCount = 0;
            foreach (var process in Process.GetProcessesByName(processName))
            {
                using (process)
                {
                    try
                    {
                        var executablePath = process.MainModule == null
                            ? null
                            : process.MainModule.FileName;
                        if (string.IsNullOrWhiteSpace(executablePath)) continue;

                        var fullPath = Path.GetFullPath(executablePath);
                        if (!string.Equals(
                            fullPath,
                            _adbPath,
                            StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (process.HasExited) continue;
                        process.Kill();
                        terminatedCount++;
                    }
                    catch
                    {
                        // Windows session shutdown must continue even when a
                        // process exits between enumeration and inspection.
                    }
                }
            }

            if (terminatedCount > 0)
            {
                _logService.Info(LocalizationService.Format(
                    "Log.Adb.ShutdownProcessesTerminated",
                    terminatedCount));
            }
        }

        public ProcessResult StartServer()
        {
            var result = Run("start-server");
            LogCommandResult(
                LocalizationService.Get("Log.Adb.StartServerResult"),
                result);
            return result;
        }

        public ProcessResult GetVersion()
        {
            return Run("version");
        }

        public void LogStartupDiagnostics()
        {
            _logService.Info(LocalizationService.Format(
                "Log.Adb.SelectedPath",
                _adbPath));
            LogCommandResult(
                LocalizationService.Get("Log.Adb.VersionResult"),
                GetVersion());
        }

        public ProcessResult KillServer()
        {
            return Run("kill-server");
        }

        public ProcessResult GetState(string serial)
        {
            return RunForSerial(serial, "get-state", true);
        }

        public ProcessResult ShellForSerial(
            string serial,
            string command,
            bool writeLog)
        {
            if (string.IsNullOrWhiteSpace(serial))
                throw new ArgumentException(
                    LocalizationService.Get("Error.Adb.SerialEmpty"),
                    "serial");
            if (string.IsNullOrWhiteSpace(command))
                throw new ArgumentException("ADB shell command is empty.", "command");
            return RunForSerial(
                serial,
                "shell " + command,
                writeLog);
        }

        public string GetDeviceDisplayName(string serial)
        {
            var settingCommands = new[]
            {
                "settings get global device_name",
                "settings get secure bluetooth_name"
            };

            foreach (var command in settingCommands)
            {
                var value = ReadDeviceText(serial, command);
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }

            var properties = new[]
            {
                "ro.product.marketname",
                "ro.product.vendor.marketname",
                "ro.product.model"
            };

            foreach (var property in properties)
            {
                var value = ReadDeviceText(
                    serial,
                    "getprop " + property);
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }

            return string.Empty;
        }

        public string GetDeviceIdentity(string serial)
        {
            var serialProperties = new[]
            {
                "ro.serialno",
                "ro.boot.serialno"
            };

            foreach (var property in serialProperties)
            {
                var value = ReadDeviceText(
                    serial,
                    "getprop " + property);
                if (!string.IsNullOrWhiteSpace(value))
                    return "serial:" + value;
            }

            var androidId = ReadDeviceText(
                serial,
                "settings get secure android_id");
            return string.IsNullOrWhiteSpace(androidId)
                ? string.Empty
                : "android:" + androidId;
        }

        private string ReadDeviceText(string serial, string command)
        {
            var result = ShellForSerial(serial, command, false);
            if (!result.IsSuccess ||
                string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                return string.Empty;
            }

            var value = result.StandardOutput.Trim();
            return string.Equals(
                       value,
                       "null",
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       value,
                       "unknown",
                       StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : value;
        }

        public ProcessResult PushForSerial(
            string serial,
            string localPath,
            string remotePath)
        {
            ValidatePushPaths(localPath, remotePath);
            return RunForSerial(
                serial,
                "push " + Quote(localPath) + " " + Quote(remotePath),
                true);
        }

        public ProcessResult PullForSerial(
            string serial,
            string remotePath,
            string localPath,
            bool writeLog)
        {
            if (string.IsNullOrWhiteSpace(remotePath))
                throw new ArgumentException(
                    LocalizationService.Get("Error.Adb.RemotePathEmpty"),
                    "remotePath");
            if (string.IsNullOrWhiteSpace(localPath))
                throw new ArgumentException(
                    "Local ADB pull path is empty.",
                    "localPath");
            return RunForSerial(
                serial,
                "pull " + Quote(remotePath) + " " + Quote(localPath),
                writeLog);
        }

        public ProcessResult CleanupForWindowsShutdown(
            string serial,
            bool removeOverlay,
            bool restoreStayAwake,
            string originalStayAwakeValue)
        {
            var commands = new List<string>();
            if (removeOverlay)
            {
                commands.Add(
                    "settings delete global overlay_display_devices");
            }
            if (restoreStayAwake)
            {
                if (originalStayAwakeValue == null)
                {
                    commands.Add(
                        "settings delete global stay_on_while_plugged_in");
                }
                else
                {
                    int parsed;
                    if (!int.TryParse(
                            originalStayAwakeValue,
                            out parsed) ||
                        parsed < 0)
                    {
                        throw new ArgumentException(
                            "The original stay-awake value is invalid.",
                            "originalStayAwakeValue");
                    }
                    commands.Add(
                        "settings put global stay_on_while_plugged_in " +
                        parsed.ToString(
                            System.Globalization.CultureInfo.InvariantCulture));
                }
            }
            if (commands.Count == 0)
                throw new ArgumentException(
                    "No Windows shutdown cleanup command was requested.");

            return Run(
                AdbCommandBuilder.ForShellCommands(
                    serial,
                    commands.ToArray()),
                false,
                _defaultTimeoutMs);
        }

        public ProcessResult InstallPackageForSerial(
            string serial,
            string apkPath,
            bool replaceExisting)
        {
            if (string.IsNullOrWhiteSpace(apkPath))
                throw new ArgumentException(
                    "APK path is empty.",
                    "apkPath");
            var fullPath = Path.GetFullPath(apkPath);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException(
                    "APK file was not found.",
                    fullPath);
            return RunForSerial(
                serial,
                "install " + (replaceExisting ? "-r " : string.Empty) +
                Quote(fullPath),
                true,
                PackageInstallTimeoutMs);
        }

        public ProcessResult UninstallPackageForSerial(
            string serial,
            string packageName)
        {
            if (string.IsNullOrWhiteSpace(packageName) ||
                !Regex.IsMatch(
                    packageName,
                    @"^[A-Za-z0-9_]+(?:\.[A-Za-z0-9_]+)+$",
                    RegexOptions.CultureInvariant))
            {
                throw new ArgumentException(
                    "Android package name is invalid.",
                    "packageName");
            }
            return RunForSerial(
                serial,
                "uninstall " + packageName,
                true);
        }

        public ProcessResult ReverseForSerial(
            string serial,
            int devicePort,
            int localPort,
            bool writeLog)
        {
            ValidatePort(devicePort, "devicePort");
            ValidatePort(localPort, "localPort");
            return RunForSerial(
                serial,
                "reverse tcp:" + devicePort + " tcp:" + localPort,
                writeLog);
        }

        public ProcessResult RemoveReverseForSerial(
            string serial,
            int devicePort,
            bool writeLog)
        {
            ValidatePort(devicePort, "devicePort");
            return RunForSerial(
                serial,
                "reverse --remove tcp:" + devicePort,
                writeLog);
        }

        private static void ValidatePushPaths(
            string localPath,
            string remotePath)
        {
            if (!File.Exists(localPath))
                throw new FileNotFoundException(
                    LocalizationService.Get("Error.Adb.PushFileNotFound"),
                    localPath);
            if (string.IsNullOrWhiteSpace(remotePath))
                throw new ArgumentException(
                    LocalizationService.Get("Error.Adb.RemotePathEmpty"),
                    "remotePath");
        }

        public ProcessResult EnableTcpIp(string serial, int port)
        {
            ValidatePort(port, "port");
            return RunForSerial(
                serial,
                "tcpip " + port,
                true);
        }

        public ProcessResult Connect(string endpoint, bool writeLog)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException(
                    LocalizationService.Get(
                        "Error.Adb.WirelessEndpointEmpty"),
                    "endpoint");
            return Run("connect " + Quote(endpoint.Trim()), writeLog);
        }

        public ProcessResult Disconnect(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException(
                    LocalizationService.Get(
                        "Error.Adb.WirelessEndpointEmpty"),
                    "endpoint");
            return Run("disconnect " + Quote(endpoint.Trim()), true);
        }

        public ProcessResult Pair(
            string endpoint,
            string pairingCode)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException(
                    LocalizationService.Get(
                        "Error.Adb.PairingEndpointEmpty"),
                    "endpoint");
            if (string.IsNullOrWhiteSpace(pairingCode))
                throw new ArgumentException(
                    LocalizationService.Get(
                        "Error.Adb.PairingCodeEmpty"),
                    "pairingCode");

            _logService.Info(LocalizationService.Format(
                "Log.Adb.PairingAttempt",
                endpoint.Trim()));
            var result = Run(
                "pair " + Quote(endpoint.Trim()) + " " +
                Quote(pairingCode.Trim()),
                false);
            LogCommandResult(
                LocalizationService.Get("Log.Adb.PairResult"),
                SanitizePairResult(result));
            return result;
        }

        public IList<AdbDeviceInfo> GetDevices()
        {
            return GetDevices(true);
        }

        public IList<AdbDeviceInfo> GetDevices(bool writeLog)
        {
            IList<AdbDeviceInfo> devices;
            TryGetDevices(writeLog, out devices);
            return devices;
        }

        public bool TryGetDevices(
            bool writeLog,
            out IList<AdbDeviceInfo> devices)
        {
            var result = Run("devices", writeLog);
            devices = ParseDevices(result.StandardOutput);
            var querySucceeded = result.IsSuccess &&
                !string.IsNullOrWhiteSpace(result.StandardOutput);

            if (writeLog)
            {
                LogCommandResult(
                    LocalizationService.Get("Log.Adb.DevicesResult"),
                    result);
                _logService.Info(LocalizationService.Format(
                    "Log.Adb.DeviceCount",
                    devices.Count));
            }
            return querySucceeded;
        }

        public bool IsAuthorizedDeviceConnected(string serial)
        {
            if (string.IsNullOrWhiteSpace(serial)) return false;
            var state = GetState(serial);
            return state.IsSuccess &&
                string.Equals(
                    state.StandardOutput.Trim(),
                    "device",
                    StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// <c>dumpsys window</c>로 잠금 화면 상태를 확인한다. Android
        /// 버전·제조사 스킨마다 노출하는 필드가 달라 절대적으로 믿을 수는
        /// 없으므로, 알려진 필드를 하나도 찾지 못하면 <see
        /// cref="LockState.Unknown"/>을 돌려준다 — 호출자는 이를
        /// <see cref="LockState.Locked"/>가 아닌 값과 똑같이 취급해
        /// (fail-open) 파싱 공백이 정상적인 DeX 시작을 막지 않게 해야
        /// 한다.
        /// </summary>
        public LockState IsDeviceLocked(string serial)
        {
            // ShellForSerial → ProcessRunner.Run은 실행 파일이 없거나
            // 프로세스를 띄우지 못하면 예외를 그대로 던진다(반환값이 아니라
            // throw다). 이 탐지는 어디까지나 최선-노력 보조 신호이므로,
            // 어떤 예외가 나든 잠금 여부를 "모른다"로 접어야 한다 -
            // 그렇지 않으면 이 기능이 자신이 돕기로 한 DeX 시작 자체를
            // 깨뜨리게 된다.
            try
            {
                var result = ShellForSerial(serial, "dumpsys window", false);
                return result.IsSuccess
                    ? ParseLockState(result.StandardOutput)
                    : LockState.Unknown;
            }
            catch (Exception ex)
            {
                _logService.Warning(LocalizationService.Format(
                    "Log.Adb.LockProbeFailed",
                    ex.Message));
                return LockState.Unknown;
            }
        }

        /// <summary>
        /// 잠금 상태를 두 신호의 논리곱으로 판단한다: (1) 잠금 화면이 떠
        /// 있는가, (2) 그 잠금 화면이 실제로 "보안 설정된" 것인가.
        ///
        /// (1)은 <c>mShowingLockscreen</c> → <c>mDreamingLockscreen</c> →
        /// <c>mKeyguardShowing</c> → <c>isStatusBarKeyguard</c> 순서로, 이
        /// 고정된 우선순위에서 출력에 실제로 나타나는 첫 필드의 값을
        /// 취한다. dumpsys 출력에서 필드가 등장하는 순서는 보장되지
        /// 않으므로, "텍스트에서 먼저 만난 필드"가 아니라 "우선순위가 더
        /// 높은 필드"가 이겨야 여러 Android 버전·스킨에 걸쳐 결정적이다.
        ///
        /// (2)가 필요한 이유: 최신 One UI에서 우선순위 스캔은 대개
        /// <c>mKeyguardShowing</c>에 도달하는데, 이 필드는 키가드가 올라와
        /// 있기만 하면 true다 — PIN·패턴·지문이 하나도 걸려 있지 않은
        /// 스와이프 전용 폰이 그저 화면만 꺼진 채 책상 위에 놓여 있는,
        /// "가장 흔한 첫 시작 상태"까지 포함해서다. 그 상태에서 Locked를
        /// 돌려주면 해제할 잠금이 없는 폰에 대고 "먼저 잠금을 해제하라"며
        /// 원래 잘 되던 DeX 시작을 막게 된다 — 이 기능이 절대 만들어서는
        /// 안 되는 실패 방향이다. 그래서 잠겨 있다고 "확신"하려면 키가드가
        /// 보안 설정돼 있다는 적극적 증거가 필요하다.
        ///
        /// 확신하지 못하는 모든 경우(잠금 화면 신호 자체가 없음, 보안
        /// 신호를 못 찾음, 파싱 불가)는 전부 <see cref="LockState.Unknown"/>
        /// 이며 호출자는 그대로 진행한다(fail-open).
        /// </summary>
        public static LockState ParseLockState(string dumpsysWindowOutput)
        {
            if (string.IsNullOrWhiteSpace(dumpsysWindowOutput))
                return LockState.Unknown;

            var showing = ParseKeyguardShowing(dumpsysWindowOutput);

            // 잠금 화면 신호를 하나도 못 찾았다 - 판단 불가.
            if (showing == null) return LockState.Unknown;

            // 잠금 화면이 떠 있지 않다면 보안 여부와 무관하게 잠겨 있지 않다.
            if (showing == false) return LockState.Unlocked;

            var secure = ParseKeyguardSecure(dumpsysWindowOutput);

            // 보안 신호가 없거나 읽을 수 없으면 "잠겼다"고 확신할 수 없다.
            if (secure == null) return LockState.Unknown;

            // 키가드는 떠 있지만 보안 설정이 아니다(스와이프 전용) -
            // 해제할 잠금이 없으므로 시작을 막을 이유가 없다.
            return secure == true ? LockState.Locked : LockState.Unlocked;
        }

        /// <summary>
        /// 잠금 화면이 떠 있는지를 우선순위 스캔으로 읽는다. 알려진 필드가
        /// 하나도 없으면 null.
        /// </summary>
        private static bool? ParseKeyguardShowing(string dumpsysWindowOutput)
        {
            foreach (var fieldName in LockFieldPriority)
            {
                var value = MatchBooleanField(dumpsysWindowOutput, fieldName);
                if (value != null) return value;
            }

            return null;
        }

        /// <summary>
        /// 키가드가 "보안 설정"돼 있는지(= 실제로 해제 자격 증명을 요구하는지)를
        /// 읽는다. 찾지 못하면 null이고, 호출자는 그때 fail-open한다.
        ///
        /// 신호는 두 단계로 찾는다.
        ///
        /// 1. 이름 자체에 keyguard가 박혀 있는 필드
        ///    (<c>isKeyguardSecure</c> / <c>mIsKeyguardSecure</c> /
        ///    <c>mKeyguardSecure</c> / <c>keyguardSecure</c>)는 의미가
        ///    모호하지 않으므로 출력 어디에 있든 그대로 신뢰한다.
        /// 2. AOSP <c>KeyguardServiceDelegate.dump()</c>는 이 값을 그냥
        ///    <c>secure=</c>라는 맨 이름으로 찍는다(같은 블록의
        ///    <c>showing=</c>/<c>occluded=</c>와 나란히). 이 이름은 너무
        ///    일반적이라 출력 아무 데서나 주워 오면 안 된다 —
        ///    <c>dumpsys window</c>에는 창 목록도 함께 실리고, 엉뚱한 창의
        ///    플래그를 잠금 근거로 삼으면 그게 곧 "되던 시작을 막는"
        ///    오탐이 된다. 그래서 이 맨 이름은 반드시
        ///    <c>KeyguardServiceDelegate</c> 헤더 뒤쪽 블록 안에서만 읽는다.
        /// </summary>
        private static bool? ParseKeyguardSecure(string dumpsysWindowOutput)
        {
            foreach (var fieldName in KeyguardSecureFieldNames)
            {
                var value = MatchBooleanField(dumpsysWindowOutput, fieldName);
                if (value != null) return value;
            }

            var delegateIndex = dumpsysWindowOutput.IndexOf(
                KeyguardServiceDelegateMarker,
                StringComparison.OrdinalIgnoreCase);
            if (delegateIndex < 0) return null;

            var block = dumpsysWindowOutput.Substring(
                delegateIndex,
                Math.Min(
                    KeyguardServiceDelegateBlockLength,
                    dumpsysWindowOutput.Length - delegateIndex));

            return MatchBooleanField(block, "secure");
        }

        /// <summary>
        /// <c>이름=true|false</c>를 양쪽 단어 경계와 함께 읽는다. 오른쪽은
        /// <c>=</c>가 이미 경계 노릇을 하지만(<c>mShowingLockscreenFoo</c>는
        /// 거부된다) 왼쪽에는 경계가 없어서, 알려진 이름으로 "끝나기만"
        /// 하는 필드(<c>XmKeyguardShowing</c>)가 스캔을 가로챌 수 있었다.
        /// </summary>
        private static bool? MatchBooleanField(string text, string fieldName)
        {
            var match = Regex.Match(
                text,
                @"(?<![A-Za-z0-9_])" + Regex.Escape(fieldName) + @"\s*=\s*(true|false)",
                RegexOptions.IgnoreCase);
            if (!match.Success) return null;

            return string.Equals(
                match.Groups[1].Value,
                "true",
                StringComparison.OrdinalIgnoreCase);
        }

        private static readonly string[] LockFieldPriority =
        {
            "mShowingLockscreen",
            "mDreamingLockscreen",
            "mKeyguardShowing",
            "isStatusBarKeyguard"
        };

        private static readonly string[] KeyguardSecureFieldNames =
        {
            "isKeyguardSecure",
            "mIsKeyguardSecure",
            "mKeyguardSecure",
            "keyguardSecure"
        };

        private const string KeyguardServiceDelegateMarker = "KeyguardServiceDelegate";

        // KeyguardServiceDelegate.dump()가 찍는 항목은 십수 줄뿐이다.
        // 블록 뒤에 이어지는 창 목록까지 훑어 엉뚱한 secure=를 줍지
        // 않도록 넉넉하되 유한한 창으로 자른다.
        private const int KeyguardServiceDelegateBlockLength = 2000;

        public AdbWakeUpResult WakeUp(
            string targetSerial,
            Func<string, bool> scrcpyWakeUp)
        {
            _logService.Info(
                LocalizationService.Get("Log.Adb.WakeUpStarting"));
            var normalizedTarget = string.IsNullOrWhiteSpace(targetSerial)
                ? string.Empty
                : targetSerial.Trim();
            if (!IsTcpIpSerial(normalizedTarget))
                KillServer();
            StartServer();

            var devicesBefore = GetDevices();
            if (ContainsAuthorizedDevice(devicesBefore, normalizedTarget))
            {
                return new AdbWakeUpResult(true, false, devicesBefore);
            }

            if (scrcpyWakeUp == null)
            {
                _logService.Warning(LocalizationService.Get(
                    "Log.Adb.WakeUpScrcpyUnavailable"));
                return new AdbWakeUpResult(false, false, devicesBefore);
            }

            _logService.Warning(LocalizationService.Get(
                "Log.Adb.WakeUpFallback"));
            var scrcpyStarted = scrcpyWakeUp(normalizedTarget);
            var devicesAfter = GetDevices();
            var success = scrcpyStarted &&
                ContainsAuthorizedDevice(devicesAfter, normalizedTarget);

            if (success)
                _logService.Info(LocalizationService.Get(
                    "Log.Adb.WakeUpDeviceFound"));
            else
                _logService.Warning(LocalizationService.Get(
                    "Log.Adb.WakeUpDeviceMissing"));

            return new AdbWakeUpResult(success, true, devicesAfter);
        }

        public static IList<AdbDeviceInfo> ParseDevices(string output)
        {
            var devices = new List<AdbDeviceInfo>();
            if (string.IsNullOrWhiteSpace(output)) return devices;

            var lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (line.Length == 0 ||
                    line.StartsWith("List of devices", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("* daemon", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var columns = line.Split(
                    new[] { '\t', ' ' },
                    StringSplitOptions.RemoveEmptyEntries);
                if (columns.Length < 2) continue;

                devices.Add(new AdbDeviceInfo
                {
                    Serial = columns[0],
                    RawStatus = columns[1],
                    Status = ParseStatus(columns[1])
                });
            }

            return devices;
        }

        private ProcessResult Run(string arguments)
        {
            return Run(arguments, true);
        }

        private ProcessResult Run(string arguments, bool writeLog)
        {
            return Run(arguments, writeLog, _defaultTimeoutMs);
        }

        private ProcessResult Run(
            string arguments,
            bool writeLog,
            int timeoutMs)
        {
            var outputEncoding = string.Equals(
                (arguments ?? string.Empty).Trim(),
                "version",
                StringComparison.OrdinalIgnoreCase)
                ? Encoding.Default
                : Encoding.UTF8;
            return _processRunner.Run(
                _adbPath,
                arguments,
                Path.GetDirectoryName(_adbPath),
                Math.Max(timeoutMs, 1000),
                writeLog,
                outputEncoding);
        }

        private ProcessResult RunForSerial(
            string serial,
            string arguments,
            bool writeLog)
        {
            return Run(
                AdbCommandBuilder.ForDevice(serial, arguments),
                writeLog);
        }

        private ProcessResult RunForSerial(
            string serial,
            string arguments,
            bool writeLog,
            int timeoutMs)
        {
            return Run(
                AdbCommandBuilder.ForDevice(serial, arguments),
                writeLog,
                timeoutMs);
        }

        public static bool IsTcpIpSerial(string serial)
        {
            if (string.IsNullOrWhiteSpace(serial)) return false;

            var value = serial.Trim();
            var separator = value.LastIndexOf(':');
            if (separator <= 0 || separator == value.Length - 1)
                return false;

            int port;
            return int.TryParse(value.Substring(separator + 1), out port) &&
                port > 0 &&
                port <= 65535;
        }

        private static bool ContainsAuthorizedDevice(
            IEnumerable<AdbDeviceInfo> devices,
            string serial)
        {
            foreach (var device in devices ?? Enumerable.Empty<AdbDeviceInfo>())
            {
                if (device == null || !device.IsAuthorized) continue;
                if (string.IsNullOrWhiteSpace(serial) ||
                    string.Equals(
                        device.Serial,
                        serial,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool IsEmulatorSerial(string serial)
        {
            return !string.IsNullOrWhiteSpace(serial) &&
                serial.Trim().StartsWith(
                    "emulator-",
                    StringComparison.OrdinalIgnoreCase);
        }

        private static void ValidatePort(int port, string parameterName)
        {
            if (port < 1 || port > 65535)
                throw new ArgumentOutOfRangeException(parameterName);
        }

        private static ProcessResult SanitizePairResult(
            ProcessResult result)
        {
            if (result == null) return null;
            return new ProcessResult
            {
                FileName = result.FileName,
                Arguments = "pair <address> <hidden>",
                ExitCode = result.ExitCode,
                StandardOutput = result.StandardOutput,
                StandardError = result.StandardError,
                TimedOut = result.TimedOut,
                Canceled = result.Canceled,
                Duration = result.Duration
            };
        }

        private static AdbDeviceStatus ParseStatus(string status)
        {
            if (string.Equals(status, "device", StringComparison.OrdinalIgnoreCase))
                return AdbDeviceStatus.Device;
            if (string.Equals(status, "unauthorized", StringComparison.OrdinalIgnoreCase))
                return AdbDeviceStatus.Unauthorized;
            if (string.Equals(status, "offline", StringComparison.OrdinalIgnoreCase))
                return AdbDeviceStatus.Offline;
            if (string.Equals(status, "no", StringComparison.OrdinalIgnoreCase))
                return AdbDeviceStatus.NoPermissions;
            return AdbDeviceStatus.Unknown;
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private void LogCommandResult(string title, ProcessResult result)
        {
            if (result == null) return;
            if (result.Canceled && IsProcessShutdownRequested) return;

            var text = !string.IsNullOrWhiteSpace(result.StandardOutput)
                ? result.StandardOutput
                : result.StandardError;
            text = (text ?? string.Empty)
                .Replace("\r", " ")
                .Replace("\n", " | ")
                .Trim();

            var message = title + ": ExitCode=" + result.ExitCode +
                ", Timeout=" + result.TimedOut;
            if (!string.IsNullOrWhiteSpace(text)) message += ", " + text;

            if (result.IsSuccess)
                _logService.Info(message);
            else
                _logService.Warning(message);
        }
    }

    public sealed class AdbWakeUpResult
    {
        public AdbWakeUpResult(
            bool success,
            bool usedScrcpy,
            IList<AdbDeviceInfo> devices)
        {
            Success = success;
            UsedScrcpy = usedScrcpy;
            Devices = devices ?? new List<AdbDeviceInfo>();
        }

        public bool Success { get; private set; }
        public bool UsedScrcpy { get; private set; }
        public IList<AdbDeviceInfo> Devices { get; private set; }
    }
}
