using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DexManager.Models;
using DexManager.Platform;
using DexManager.Utils;

namespace DexManager.Services
{
    public sealed class PathService
    {
        private readonly SettingsService _settingsService;
        private readonly LogService _logService;
        private readonly ProcessRunner _processRunner;
        private readonly IPathProvider _pathProvider;
        private readonly IPlatformService _platformService;
        // 후보 프로브 재시도 예산의 시계. 테스트가 시간을 결정적으로
        // 흘릴 수 있게 주입만 받아 ProbeRetryBudget에 그대로 넘긴다 -
        // null이면 예산이 DateTime.UtcNow를 쓴다.
        private readonly Func<DateTime> _utcNow;

        public PathService(
            SettingsService settingsService,
            LogService logService,
            ProcessRunner processRunner,
            IPathProvider pathProvider = null,
            IPlatformService platformService = null,
            Func<DateTime> utcNow = null)
        {
            _settingsService = settingsService;
            _logService = logService;
            _processRunner = processRunner;
            _pathProvider = pathProvider;
            _platformService = platformService;
            _utcNow = utcNow;
        }

        public string SelectAdbPath(AppSettings settings, int timeoutMs)
        {
            if (settings == null) throw new ArgumentNullException("settings");

            _logService.Info(LocalizationService.Format(
                "Log.Path.WindowsDetected",
                PlatformHelper.GetDisplayName(),
                PlatformHelper.CurrentVersion));

            if (settings.Paths.AdbSelectionMode == AdbSelectionMode.Manual)
            {
                // Manual은 후보가 없으면 SelectRequired가 그대로 던지고,
                // 이 실패는 사용자가 설정 화면을 열기도 전에 앱 시작을
                // 막는다(StartupErrorWindow) - 그래서 여기서만 일반적인
                // "Error.Path.AdbUnavailable" 대신 실제 설정된 경로와
                // 설정 파일 위치를 담은 메시지를 만든다. 그래야 그 창에
                // 뜨는 한 줄만 보고도 무엇을 고쳐야 하는지, 어디를
                // 열어야 하는지 알 수 있다.
                return SelectRequired(
                    settings.Paths.AdbPath,
                    LocalizationService.Get(
                        "Path.Description.ManualAdb"),
                    timeoutMs,
                    resolvedPath => LocalizationService.Format(
                        "Error.Path.ManualAdbUnavailable",
                        resolvedPath,
                        _settingsService.SettingsFilePath));
            }

            if (PlatformHelper.RequiresLegacyAdb)
            {
                return SelectRequired(
                    settings.Paths.Win7AdbPath,
                    LocalizationService.Get(
                        "Path.Description.LegacyAdb"),
                    timeoutMs);
            }

            return SelectScrcpyAdbWithLegacyFallback(
                settings,
                timeoutMs);
        }

        public bool IsAdbDirectoryInProcessPath(string adbPath)
        {
            if (string.IsNullOrWhiteSpace(adbPath)) return false;
            var adbDirectory = NormalizeDirectory(Path.GetDirectoryName(adbPath));
            var pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

            return ContainsDirectory(pathValue, adbDirectory);
        }

        public bool IsAdbDirectoryInSystemPath(string adbPath)
        {
            if (string.IsNullOrWhiteSpace(adbPath)) return false;
            if (_platformService != null)
                return _platformService.IsDirectoryInSystemPath(Path.GetDirectoryName(adbPath));

            return IsAdbDirectoryInProcessPath(adbPath);
        }

        public bool TryRegisterAdbDirectoryInSystemPath(string adbPath)
        {
            if (string.IsNullOrWhiteSpace(adbPath)) return false;
            if (_platformService != null)
                return _platformService.TryRegisterDirectoryInSystemPath(Path.GetDirectoryName(adbPath));

            if (!AdminHelper.IsAdministrator())
            {
                _logService.Warning(LocalizationService.Get(
                    "Log.Path.AdminRequired"));
                return false;
            }

            return false;
        }

        private string SelectScrcpyAdbWithLegacyFallback(
            AppSettings settings,
            int timeoutMs)
        {
            // 자동 선택에서 사용자 설정과 가장 가깝게 연결된 후보다 - 사용자가
            // 고른 scrcpy 폴더 옆의 adb를 우선 쓴다. 이게 실패해 아래로
            // 떨어지면(GetRunnableCandidate가 이미 실패 사유를 경고로 남긴
            // 뒤), 최종 선택 시점에 "무엇을 대신 쓰는지"를 한 줄로 분명히
            // 남긴다 - 그렇지 않으면 사용자는 자신의 설정이 조용히
            // 무시됐다는 사실을 알아챌 방법이 없다(개별 후보 경고를 직접
            // 뒤져 짜맞추지 않는 한).
            // 재시도 예산은 이 체인 <b>전체</b>가 하나를 나눠 쓴다 -
            // 후보마다 새로 만들면 재시도가 후보 수만큼 곱해져(각 후보가
            // 자기 몫을 온전히 받아) 예산을 둔 의미가 사라진다. 각 후보의
            // 첫 시도는 예산과 무관하게 항상 실행되므로, 예산이 소진돼도
            // 뒤쪽 후보를 못 찾게 되지는 않는다.
            var retryBudget = ProbeRetryBudget.For(timeoutMs, _utcNow);

            var preferred = GetScrcpyAdb(settings, timeoutMs, retryBudget);
            var selected = preferred;
            if (selected == null && _pathProvider != null)
            {
                foreach (var candidatePath in _pathProvider.GetCandidateAdbPaths())
                {
                    selected = GetRunnableCandidate(
                        candidatePath,
                        "System/Platform ADB",
                        timeoutMs,
                        retryBudget,
                        true);
                    if (selected != null) break;
                }
            }

            if (selected == null)
            {
                selected = GetRunnableCandidate(
                    settings.Paths.Win7AdbPath,
                    LocalizationService.Get(
                        "Path.Description.LegacyAdb"),
                    timeoutMs,
                    retryBudget);
            }

            if (selected == null)
            {
                // Fallback to checking PATH
                var systemAdb = FindExecutableInPath("adb");
                if (!string.IsNullOrWhiteSpace(systemAdb))
                {
                    selected = GetRunnableCandidate(
                        systemAdb,
                        "PATH ADB",
                        timeoutMs,
                        retryBudget,
                        true);
                }
            }

            if (selected == null)
            {
                throw new FileNotFoundException(
                    LocalizationService.Get(
                        "Error.Path.AutomaticAdbNotFound"));
            }

            if (preferred == null)
            {
                _logService.Warning(LocalizationService.Format(
                    "Log.Path.PreferredAdbFallback",
                    LocalizationService.Get("Path.Description.ScrcpyAdb"),
                    selected.Description,
                    selected.Path));
            }

            LogSelection(
                selected,
                LocalizationService.Get("Path.Mode.Automatic"));
            return selected.Path;
        }

        private string SelectRequired(
            string configuredPath,
            string description,
            int timeoutMs,
            Func<string, string> describeUnavailable = null)
        {
            // 후보가 하나뿐인 별개의 경로다 - 자동 선택 체인과 예산을
            // 나눠 쓸 이유가 없으므로 호출마다 새 예산을 준다.
            var candidate = GetRunnableCandidate(
                configuredPath,
                description,
                timeoutMs,
                ProbeRetryBudget.For(timeoutMs, _utcNow));
            if (candidate == null)
            {
                var message = describeUnavailable != null
                    ? describeUnavailable(
                        _settingsService.ResolvePath(configuredPath))
                    : LocalizationService.Format(
                        "Error.Path.AdbUnavailable",
                        description);
                throw new FileNotFoundException(message);
            }

            LogSelection(
                candidate,
                LocalizationService.Get("Path.Mode.Selected"));
            return candidate.Path;
        }

        private AdbPathCandidate GetScrcpyAdb(
            AppSettings settings,
            int timeoutMs,
            ProbeRetryBudget retryBudget)
        {
            var configuredScrcpy = _settingsService.ResolvePath(
                settings.Paths.ScrcpyPath);
            if (string.IsNullOrWhiteSpace(configuredScrcpy)) return null;

            var dir = Path.GetDirectoryName(configuredScrcpy);
            if (string.IsNullOrWhiteSpace(dir)) return null;

            var adbExecutableName = OperatingSystem.IsWindows() ? "adb.exe" : "adb";
            var adbPath = Path.Combine(dir, adbExecutableName);
            return GetRunnableCandidate(
                adbPath,
                LocalizationService.Get(
                    "Path.Description.ScrcpyAdb"),
                timeoutMs,
                retryBudget,
                true);
        }

        private AdbPathCandidate GetRunnableCandidate(
            string configuredPath,
            string description,
            int timeoutMs,
            ProbeRetryBudget retryBudget,
            bool pathIsAbsolute = false)
        {
            if (string.IsNullOrWhiteSpace(configuredPath)) return null;

            var path = pathIsAbsolute
                ? Path.GetFullPath(configuredPath)
                : _settingsService.ResolvePath(configuredPath);
            if (!File.Exists(path))
            {
                _logService.Warning(LocalizationService.Format(
                    "Log.Path.CandidateMissing",
                    description,
                    path));
                return null;
            }

            try
            {
                // 부하로 인한 일시적 타임아웃 때문에 멀쩡한 후보를 죽었다고
                // 오판하지 않도록 짧게 재시도한다(TransientProbeRetry) -
                // 타임아웃이 아닌 실패(파일은 있지만 adb가 아니다 등)는
                // 다시 해봤자 같은 결과이므로 그 경우는 즉시 반환된다.
                // 재시도에 쓸 수 있는 시간은 체인이 공유하는 retryBudget이
                // 정한다 - 첫 시도는 예산과 무관하게 언제나 실행된다.
                // 부하로 fork/exec 자체가 EAGAIN으로 실패하는 경우도 같은
                // 정책으로 재시도된다 - 그 외 예외는 아래 catch가 그대로
                // 받아 "실행 불가"로 기록하고 다음 후보로 넘어간다.
                var result = TransientProbeRetry.Run(
                    delegate
                    {
                        return _processRunner.Run(
                            path,
                            "version",
                            Path.GetDirectoryName(path),
                            ProbeRetryBudget.EffectiveProbeTimeoutMs(timeoutMs),
                            false,
                            Encoding.Default);
                    },
                    retryBudget,
                    retrySkipped: delegate
                    {
                        // 이 줄이 없으면 사용자는 "왜 이 후보만 한 번밖에
                        // 시도하지 않았나"를 로그만 보고는 알 수 없다 -
                        // 앞 후보가 예산을 다 썼다는 사실이 어디에도
                        // 남지 않기 때문이다.
                        _logService.Warning(LocalizationService.Format(
                            "Log.Path.CandidateRetryBudgetExhausted",
                            description));
                    });
                if (!result.IsSuccess)
                {
                    _logService.Warning(LocalizationService.Format(
                        "Log.Path.CandidateExecutionFailed",
                        description,
                        DescribeFailure(result)));
                    return null;
                }

                return new AdbPathCandidate(
                    path,
                    description,
                    AdbVersionParser.GetDisplayVersion(
                        result.StandardOutput,
                        LocalizationService.Get(
                            "Path.VersionUnavailable")),
                    AdbVersionParser.GetVersionNumber(
                        result.StandardOutput));
            }
            catch (Exception ex)
            {
                _logService.Warning(LocalizationService.Format(
                    "Log.Path.CandidateUnavailable",
                    description,
                    ex.Message));
                return null;
            }
        }

        /// <summary>
        /// 후보 프로브 실패 사유를 사람이 읽을 수 있게 설명한다.
        /// <c>result.StandardError</c>는 타임아웃(TimedOut)일 때는 항상
        /// 비어 있다 - 프로세스가 아무 출력도 못 낸 채 강제 종료됐기
        /// 때문이다. 거기에 빈 문자열을 그대로 로그에 박으면 "실행 실패:
        /// (공백)"처럼 원인을 전혀 알 수 없는 메시지가 남는다.
        /// </summary>
        private static string DescribeFailure(ProcessResult result)
        {
            if (result.TimedOut)
                return LocalizationService.Format(
                    "Path.CandidateTimedOut",
                    (long)result.Duration.TotalMilliseconds);
            if (result.Canceled)
                return LocalizationService.Get("Path.CandidateCanceled");
            if (!string.IsNullOrWhiteSpace(result.StandardError))
                return result.StandardError;
            return LocalizationService.Format(
                "Path.CandidateExitCode",
                result.ExitCode);
        }

        private void LogSelection(AdbPathCandidate candidate, string settingsModeLabel)
        {
            _logService.Info(LocalizationService.Format(
                "Log.Path.Selection",
                settingsModeLabel,
                candidate.Description));
            _logService.Info(LocalizationService.Format(
                "Log.Path.SelectedAdbPath",
                candidate.Path));
            _logService.Info(LocalizationService.Format(
                "Log.Path.AdbVersion",
                candidate.VersionText));
        }

        private static string NormalizeDirectory(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) return string.Empty;
            return Path.GetFullPath(directory.Trim().Trim('"'))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static bool ContainsDirectory(string pathValue, string directory)
        {
            var separator = OperatingSystem.IsWindows() ? ';' : ':';
            foreach (var entry in (pathValue ?? string.Empty).Split(
                new[] { separator },
                StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.Equals(
                    NormalizeDirectory(entry),
                    directory,
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string FindExecutableInPath(string executableName)
        {
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrWhiteSpace(pathEnv)) return null;

            var separator = OperatingSystem.IsWindows() ? ';' : ':';
            var extensions = OperatingSystem.IsWindows()
                ? new[] { ".exe", ".cmd", ".bat", "" }
                : new[] { "" };

            foreach (var dir in pathEnv.Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries))
            {
                foreach (var ext in extensions)
                {
                    try
                    {
                        var full = Path.Combine(dir.Trim(), executableName + ext);
                        if (File.Exists(full)) return Path.GetFullPath(full);
                    }
                    catch
                    {
                    }
                }
            }

            return null;
        }

        private sealed class AdbPathCandidate
        {
            public AdbPathCandidate(
                string path,
                string description,
                string versionText,
                Version version)
            {
                Path = path;
                Description = description;
                VersionText = versionText;
                Version = version;
            }

            public string Path { get; private set; }
            public string Description { get; private set; }
            public string VersionText { get; private set; }
            public Version Version { get; private set; }
        }
    }
}
