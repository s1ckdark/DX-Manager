using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DexManager.Hosting;
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
    ///
    /// 체인 전체의 재시도 예산(ProbeRetryBudget)이 후보마다가 아니라
    /// <b>체인 하나당 하나</b>로 배선됐는지도 여기서 본다 - 그 배선은
    /// PathService 안에서만 관측할 수 있어 단위 테스트로는 고정되지 않는다.
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
            // 다르다. 여기서 고정하는 건 "타임아웃을 빈 문자열이 아니라
            // 사람이 읽을 수 있게 남기는가"와, 아래의 바닥 적용 여부다.
            // 일부러 바닥(3000ms)보다 짧은 값을 넘긴다 - 그래야 프로브가
            // 예산과 <b>같은</b> 함수로 타임아웃을 올려 잡는지 관측된다.
            const int belowFloorTimeoutMs = 1000;
            try
            {
                await Task.Run(
                    () => pathService.SelectAdbPath(settings, belowFloorTimeoutMs));
            }
            catch (FileNotFoundException)
            {
            }

            var timeoutLine = Assert.Single(
                logService.GetSessionEntries(),
                line =>
                    line.Contains("ADB from the selected scrcpy folder", StringComparison.Ordinal) &&
                    line.Contains("timed out after", StringComparison.Ordinal));

            // 프로브가 실제로 기다린 시간은 넘긴 1000ms가 아니라 바닥까지
            // 올린 값이어야 한다. 프로브와 예산이 서로 다른 타임아웃을 쓰기
            // 시작하면(드리프트) 예산이 실제 프로브보다 커지거나 작아져
            // 상한이 무너지므로, 그 계산이 한 함수에서만 나오는지 여기서
            // 관측한다.
            var waitedMs = int.Parse(
                Regex.Match(timeoutLine, @"timed out after (\d+)ms").Groups[1].Value);
            Assert.True(
                waitedMs >= ProbeRetryBudget.EffectiveProbeTimeoutMs(belowFloorTimeoutMs),
                "probe waited " + waitedMs + "ms, expected at least " +
                ProbeRetryBudget.EffectiveProbeTimeoutMs(belowFloorTimeoutMs) + "ms");
        }

        /// <summary>
        /// 실제로 배송되는 선택 타임아웃. 상수를 베끼지 않고
        /// <see cref="ApplicationHost"/>가 넘기는 바로 그 값을 참조한다 -
        /// 예산이 이 값에서 유도되므로, 여기가 바뀌면 예산도 테스트도
        /// 함께 따라가야 의미가 유지된다.
        /// </summary>
        private const int TimeoutMs = ApplicationHost.AdbSelectionTimeoutMs;

        [Fact]
        public async Task SelectAdbPath_EveryCandidateTimesOut_TheSharedBudgetStopsRetryingAfterTheFirstCandidate()
        {
            using var root = new TempRoot();
            var probeLog = Path.Combine(root.Path, "probe-log");
            var candidates = new[] { "cand1", "cand2", "cand3" }
                .Select(name =>
                {
                    var scriptPath = Path.Combine(root.Path, name);
                    WriteCountingAlwaysSlowAdbScript(scriptPath, probeLog, name);
                    return scriptPath;
                })
                .ToArray();

            var logService = new LogService();
            var settingsService = new SettingsService(logService, root.Path);
            var processRunner = new ProcessRunner(logService);
            var pathProvider = new FakePathProvider(
                root.Path,
                candidateAdbPaths: candidates);
            // 프로브 하나가 타임아웃 상한을 꽉 채우는 상황을 흉내 낸다.
            // step을 숫자로 적지 않고 예산이 쓰는 것과 <b>같은</b> 함수에서
            // 유도한다 - 그러지 않으면 이 테스트는 실제 경로에 존재하지도
            // 않는 프로브 타임아웃을 가정하게 되고, 예산이 실경로에서
            // 아무것도 막지 못하는 상태를 초록으로 가려 준다(리뷰가 잡아낸
            // 결함이 정확히 그것이다).
            var clock = new AdvancingClock(
                ProbeRetryBudget.EffectiveProbeTimeout(TimeoutMs));
            var pathService = new PathService(
                settingsService,
                logService,
                processRunner,
                pathProvider,
                utcNow: clock.Now);
            var settings = AppSettings.CreateDefault();
            // scrcpy 옆 adb는 일부러 없는 경로로 둔다 - 프로브 없이 즉시
            // 탈락해야 후보 셋만 남아 계산이 분명해진다.
            settings.Paths.ScrcpyPath = Path.Combine(root.Path, "no-scrcpy", "scrcpy");

            try
            {
                await Task.Run(() => pathService.SelectAdbPath(settings, TimeoutMs));
            }
            catch (FileNotFoundException)
            {
                // 후보가 모두 죽었을 때 PATH에서 진짜 adb를 찾느냐는 이
                // 머신마다 다르다 - 여기서 고정하는 건 프로브 횟수뿐이다.
            }

            var probes = File.ReadAllLines(probeLog);
            // 핵심 단언: 후보 3개가 모두 타임아웃해도 프로브는 3×2=6번이
            // 아니라 4번만 돈다. 첫 후보가 예산을 다 썼으므로 2·3번
            // 후보는 첫 시도만 받는다 - 첫 시도까지 잘렸다면 4가 아니라
            // 2가 나왔을 것이고, 예산이 없었다면 6이 나왔을 것이다.
            Assert.Equal(4, probes.Length);
            Assert.Equal(2, probes.Count(line => line == "cand1"));
            Assert.Equal(1, probes.Count(line => line == "cand2"));
            Assert.Equal(1, probes.Count(line => line == "cand3"));

            // 재시도를 건너뛴 사실이 로그에 남아야 한다 - 그렇지 않으면
            // "왜 이 후보만 한 번만 시도했나"를 사용자가 추적할 수 없다.
            Assert.Contains(
                logService.GetSessionEntries(),
                line =>
                    line.Contains("System/Platform ADB", StringComparison.Ordinal) &&
                    line.Contains("was probed only once", StringComparison.Ordinal));
        }

        private static void WriteCountingAlwaysSlowAdbScript(
            string scriptPath,
            string probeLogPath,
            string name)
        {
            var script =
                "#!/bin/sh\n" +
                "if [ \"$*\" = \"version\" ]; then\n" +
                "  printf '" + name + "\\n' >> \"" + probeLogPath + "\"\n" +
                "  exec sleep 30\n" +
                "fi\n" +
                "exit 0\n";
            WriteExecutable(scriptPath, script);
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

        /// <summary>
        /// 조회될 때마다 정해진 만큼 앞으로 가는 시계. "프로브 하나가 이만큼
        /// 걸렸다"를 실제로 기다리지 않고 흉내 내기 위한 것이다 - 예산은
        /// 재시도 시작·끝에 한 번씩 시계를 보므로, 재시도 하나가 정확히
        /// <c>step</c>만큼 청구된다.
        /// </summary>
        private sealed class AdvancingClock
        {
            private readonly TimeSpan _step;
            private DateTime _current =
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            public AdvancingClock(TimeSpan step)
            {
                _step = step;
            }

            public DateTime Now()
            {
                var now = _current;
                _current += _step;
                return now;
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
