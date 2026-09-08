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

        public PathService(
            SettingsService settingsService,
            LogService logService,
            ProcessRunner processRunner,
            IPathProvider pathProvider = null,
            IPlatformService platformService = null)
        {
            _settingsService = settingsService;
            _logService = logService;
            _processRunner = processRunner;
            _pathProvider = pathProvider;
            _platformService = platformService;
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
            var preferred = GetScrcpyAdb(settings, timeoutMs);
            var selected = preferred;
            if (selected == null && _pathProvider != null)
            {
                foreach (var candidatePath in _pathProvider.GetCandidateAdbPaths())
                {
                    selected = GetRunnableCandidate(
                        candidatePath,
                        "System/Platform ADB",
                        timeoutMs,
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
                    timeoutMs);
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
            var candidate = GetRunnableCandidate(
                configuredPath,
                description,
                timeoutMs);
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
            int timeoutMs)
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
                true);
        }

        private AdbPathCandidate GetRunnableCandidate(
            string configuredPath,
            string description,
            int timeoutMs,
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
                var result = TransientProbeRetry.Run(delegate
                {
                    return _processRunner.Run(
                        path,
                        "version",
                        Path.GetDirectoryName(path),
                        Math.Max(timeoutMs, 3000),
                        false,
                        Encoding.Default);
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
