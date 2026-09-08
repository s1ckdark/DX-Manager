using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DexManager.Models;
using DexManager.Services;
using DexManager.Tests.FakePlatform;
using DexManager.Utils;
using Xunit;

namespace DexManager.Tests
{
    /// <summary>
    /// PathService.GetRunnableCandidate가 TransientProbeRetry로 실제 배선돼
    /// 있는지, 그리고 선호 후보(사용자가 고른 scrcpy 폴더 옆의 adb)가
    /// 대체됐을 때 그 사실이 로그로 드러나는지를 고정한다
    /// (.omc/research/settle-poll-report.md). TransientProbeRetryTests가
    /// 이미 재시도 "정책"(횟수, 언제 재시도하는지) 자체를 가짜 델리게이트로
    /// 빈틈없이 고정했으므로, 여기서는 진짜 프로세스 하나로 "이음매"만
    /// 검증한다 - 정책을 다시 반복하지 않는다.
    /// </summary>
    public class PathServiceCandidateRetryTests
    {
        [Fact]
        public async Task SelectAdbPath_PreferredCandidateTimesOutOnceThenSucceeds_StillSelectsIt()
        {
            using var root = new TempRoot();
            var counterFile = Path.Combine(root.Path, "probe-count");
            var scrcpyDir = Path.Combine(root.Path, "scrcpy-dir");
            Directory.CreateDirectory(scrcpyDir);
            WriteSlowThenFastAdbScript(
                Path.Combine(scrcpyDir, "adb"),
                counterFile);

            var logService = new LogService();
            var settingsService = new SettingsService(logService, root.Path);
            var processRunner = new ProcessRunner(logService);
            var pathService = new PathService(
                settingsService,
                logService,
                processRunner);
            var settings = AppSettings.CreateDefault();
            settings.Paths.ScrcpyPath = Path.Combine(scrcpyDir, "scrcpy");

            // 첫 시도가 타임아웃 후 재시도로 성공하는 데는 최소
            // AdbSelectionTimeoutMs(여기서는 3000ms 바닥)만큼 실제로 걸린다 -
            // 그 시간을 인위적으로 줄일 방법이 없으므로(그게 바로 이
            // 테스트가 고정하려는 실제 타임아웃 바닥이다) 넉넉한 xUnit
            // 타임아웃 안에서 기다린다.
            var selectedPath = await Task.Run(
                () => pathService.SelectAdbPath(settings, 3000));

            Assert.Equal(
                Path.GetFullPath(Path.Combine(scrcpyDir, "adb")),
                selectedPath);
            // 재시도로 선호 후보 자체가 살아났다는 뜻이므로, "선호 후보를
            // 못 써서 다른 걸 쓴다"는 경고는 나오지 않아야 한다.
            Assert.DoesNotContain(
                logService.GetSessionEntries(),
                line => line.Contains("Preferred ADB", StringComparison.Ordinal));
        }

        [Fact]
        public void SelectAdbPath_PreferredCandidateUnavailable_LogsWhichReplacementWasUsed()
        {
            using var root = new TempRoot();
            var scrcpyDir = Path.Combine(root.Path, "scrcpy-dir");
            Directory.CreateDirectory(scrcpyDir);
            // 의도적으로 "adb"를 두지 않는다 - GetScrcpyAdb가 즉시(타임아웃
            // 없이) 실패해야 이 테스트가 빠르게 끝난다. 실패 사유는 이미
            // TransientProbeRetryTests와 DescribeFailure 쪽에서 다루므로
            // 여기서는 "대체됐다는 사실이 로그에 남는지"만 본다.

            var replacementAdb = new FakeAdbExecutable(
                root.Path,
                new System.Collections.Generic.Dictionary<string, string>());

            var logService = new LogService();
            var settingsService = new SettingsService(logService, root.Path);
            var processRunner = new ProcessRunner(logService);
            var pathProvider = new FakePathProvider(
                root.Path,
                adbPath: replacementAdb.ExecutablePath);
            var pathService = new PathService(
                settingsService,
                logService,
                processRunner,
                pathProvider);
            var settings = AppSettings.CreateDefault();
            settings.Paths.ScrcpyPath = Path.Combine(scrcpyDir, "scrcpy");

            var selectedPath = pathService.SelectAdbPath(settings, 3000);

            Assert.Equal(
                Path.GetFullPath(replacementAdb.ExecutablePath),
                selectedPath);
            Assert.Contains(
                logService.GetSessionEntries(),
                line =>
                    line.Contains("Preferred ADB", StringComparison.Ordinal) &&
                    line.Contains(
                        "ADB from the selected scrcpy folder",
                        StringComparison.Ordinal) &&
                    line.Contains(selectedPath, StringComparison.Ordinal));
        }

        [Fact]
        public async Task SelectAdbPath_PreferredCandidateTimesOutPersistently_LogsHowLongItWaitedInstead()
        {
            using var root = new TempRoot();
            var counterFile = Path.Combine(root.Path, "probe-count");
            var scrcpyDir = Path.Combine(root.Path, "scrcpy-dir");
            Directory.CreateDirectory(scrcpyDir);
            WriteAlwaysSlowAdbScript(Path.Combine(scrcpyDir, "adb"));

            var logService = new LogService();
            var settingsService = new SettingsService(logService, root.Path);
            var processRunner = new ProcessRunner(logService);
            var pathService = new PathService(
                settingsService,
                logService,
                processRunner);
            var settings = AppSettings.CreateDefault();
            settings.Paths.ScrcpyPath = Path.Combine(scrcpyDir, "scrcpy");

            // 결과(성공/예외)는 신경 쓰지 않는다 - PATH에서 진짜 시스템
            // adb를 찾을 수도, 못 찾을 수도 있어 이 개발 머신/CI마다
            // 다르다. 여기서 고정하는 건 오직 "타임아웃을 빈 문자열이
            // 아니라 사람이 읽을 수 있게 남기는가" 하나뿐이다.
            try
            {
                await Task.Run(() => pathService.SelectAdbPath(settings, 3000));
            }
            catch (FileNotFoundException)
            {
            }

            Assert.Contains(
                logService.GetSessionEntries(),
                line =>
                    line.Contains("ADB from the selected scrcpy folder", StringComparison.Ordinal) &&
                    line.Contains("timed out after", StringComparison.Ordinal));
        }

        private static void WriteSlowThenFastAdbScript(
            string scriptPath,
            string counterFilePath)
        {
            var script =
                "#!/bin/sh\n" +
                "if [ \"$*\" = \"version\" ]; then\n" +
                "  if [ ! -f \"" + counterFilePath + "\" ]; then\n" +
                "    touch \"" + counterFilePath + "\"\n" +
                "    exec sleep 5\n" +
                "  fi\n" +
                "  printf 'Android Debug Bridge version 1.0.41\\n'\n" +
                "  exit 0\n" +
                "fi\n" +
                "exit 0\n";
            WriteExecutable(scriptPath, script);
        }

        private static void WriteAlwaysSlowAdbScript(string scriptPath)
        {
            var script =
                "#!/bin/sh\n" +
                "if [ \"$*\" = \"version\" ]; then\n" +
                "  exec sleep 30\n" +
                "fi\n" +
                "exit 0\n";
            WriteExecutable(scriptPath, script);
        }

        private static void WriteExecutable(string path, string script)
        {
            File.WriteAllText(path, script);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(
                    path,
                    UnixFileMode.UserRead |
                    UnixFileMode.UserWrite |
                    UnixFileMode.UserExecute);
            }
        }

        private sealed class TempRoot : IDisposable
        {
            public TempRoot()
            {
                Path = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    "dxm-pathservice-tests",
                    Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Path);
            }

            public string Path { get; }

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(Path)) Directory.Delete(Path, true);
                }
                catch
                {
                }
            }
        }
    }
}
