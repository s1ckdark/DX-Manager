using DexManager.FileTransfer;
using DexManager.Models;
using DexManager.Services;

namespace DexManager.Tests;

/// <summary>
/// 진단 화면의 "파일 전송 보조 프로그램" 항목이 파일 존재만 보지 않고
/// 실제로 프록시를 실행해 판정하는지 확인한다. 디버그 빌드의 DXMAdbProxy는
/// 프레임워크 의존 앱이라 .NET 런타임을 못 찾으면 Main()에 닿기도 전에 죽는데,
/// 예전에는 이 경우가 Passed로 보고됐다.
/// 프로세스 실행은 전부 가짜로 주입한다 — 실제 바이너리를 띄우지 않는다.
/// </summary>
public class AdbProxySelfTestDiagnosticTests : IDisposable
{
    /// <summary>실기에서 관측한 apphost 실패 출력의 첫 줄.</summary>
    private const string MissingRuntimeStderr =
        "You must install .NET to run this application.\n" +
        "\n" +
        "App: /tmp/tools/adb-proxy/DXMAdbProxy\n" +
        ".NET location: Not found";

    /// <summary>테스트가 달리 명시하지 않을 때 쓰는 일반 프로세스 예산.</summary>
    private const int DefaultProcessTimeoutMs = 15000;

    private readonly string _root;
    private readonly string _proxyPath;

    public AdbProxySelfTestDiagnosticTests()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            "dxm-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _proxyPath = Path.Combine(_root, "DXMAdbProxy");
        // 실행되지는 않는다. File.Exists 분기를 통과시키기 위한 자리표시자다.
        File.WriteAllText(_proxyPath, "not a real executable");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }
        catch
        {
            // 임시 디렉터리 정리 실패는 테스트 결과에 영향을 주지 않는다.
        }
    }

    [Fact]
    public void FileMissing_FailsWithTheExistingMessageAndNeverRuns()
    {
        var missing = Path.Combine(_root, "absent", "DXMAdbProxy");
        var invoked = false;

        var item = EnvironmentCheckService.BuildFileTransferHelperCheck(
            missing,
            DefaultProcessTimeoutMs,
            (_, _) =>
            {
                invoked = true;
                return SelfTestPassed();
            });

        Assert.False(invoked);
        Assert.Equal(EnvironmentCheckStatus.Failed, item.Status);
        Assert.Equal(
            LocalizationService.Format("Environment.FileMissing", missing),
            item.Message);
    }

    [Fact]
    public void SelfTestSucceeds_PassesAndNamesThePath()
    {
        var item = EnvironmentCheckService.BuildFileTransferHelperCheck(
            _proxyPath,
            DefaultProcessTimeoutMs,
            (path, _) =>
            {
                Assert.Equal(_proxyPath, path);
                return SelfTestPassed();
            });

        Assert.Equal(EnvironmentCheckStatus.Passed, item.Status);
        Assert.Equal(_proxyPath, item.Message);
    }

    [Fact]
    public void SelfTestFails_ReportsFailedAndCarriesTheProcessOutput()
    {
        var item = EnvironmentCheckService.BuildFileTransferHelperCheck(
            _proxyPath,
            DefaultProcessTimeoutMs,
            (_, _) => new ProcessResult
            {
                FileName = _proxyPath,
                Arguments = "--self-test",
                ExitCode = 131,
                StandardOutput = string.Empty,
                StandardError = MissingRuntimeStderr
            });

        Assert.Equal(EnvironmentCheckStatus.Failed, item.Status);
        // 사용자가 봐야 하는 것은 scrcpy 이야기가 아니라 이 사유다.
        Assert.Contains(
            "You must install .NET to run this application.",
            item.Message);
        Assert.Contains(".NET location: Not found", item.Message);
        // 진단 목록은 한 항목당 한 줄이라 줄바꿈은 접힌다.
        Assert.DoesNotContain("\n", item.Message);
    }

    [Fact]
    public void ExitCodeZeroWithoutTheSuccessMarker_IsNotTreatedAsPassed()
    {
        var item = EnvironmentCheckService.BuildFileTransferHelperCheck(
            _proxyPath,
            DefaultProcessTimeoutMs,
            (_, _) => new ProcessResult
            {
                FileName = _proxyPath,
                Arguments = "--self-test",
                ExitCode = 0,
                StandardOutput = string.Empty,
                StandardError = MissingRuntimeStderr
            });

        Assert.Equal(EnvironmentCheckStatus.Failed, item.Status);
        Assert.Contains(
            "You must install .NET to run this application.",
            item.Message);
    }

    [Fact]
    public void SelfTestTimesOut_FailsWithTheNoResponseReason()
    {
        var item = EnvironmentCheckService.BuildFileTransferHelperCheck(
            _proxyPath,
            DefaultProcessTimeoutMs,
            (_, _) => new ProcessResult
            {
                FileName = _proxyPath,
                Arguments = "--self-test",
                ExitCode = -1,
                StandardOutput = string.Empty,
                StandardError = string.Empty,
                TimedOut = true
            });

        Assert.Equal(EnvironmentCheckStatus.Failed, item.Status);
        // Contains가 아니라 Equal이다. 예전에는 이 사유가
        // Environment.HelperRunFailed 안에 중첩돼 주어를 두 번 말했다
        // ("...실행할 수 없습니다: ...응답하지 않아 중단했습니다").
        Assert.Equal(
            LocalizationService.Get("Environment.HelperNoResponse"),
            item.Message);
    }

    [Fact]
    public void RunnerThrows_FailsInsteadOfPassing()
    {
        var item = EnvironmentCheckService.BuildFileTransferHelperCheck(
            _proxyPath,
            DefaultProcessTimeoutMs,
            (_, _) => throw new UnauthorizedAccessException("permission denied"));

        // 예외를 삼켜 진단 전체를 죽이지는 않되, 삼킨 뒤 Passed로 보고하면
        // 이 작업이 고치려는 결함이 그대로 남는다.
        Assert.Equal(EnvironmentCheckStatus.Failed, item.Status);
        Assert.Contains("permission denied", item.Message);
    }

    [Fact]
    public void NullResult_FailsInsteadOfPassing()
    {
        var item = EnvironmentCheckService.BuildFileTransferHelperCheck(
            _proxyPath,
            DefaultProcessTimeoutMs,
            (_, _) => null);

        Assert.Equal(EnvironmentCheckStatus.Failed, item.Status);
    }

    /// <summary>
    /// 자기진단 타임아웃이 <c>AppSettings.Timing.ProcessTimeoutMs</c>에서
    /// 유도된 값임을 고정한다. 예전에는 이 계산이 private 메서드 안에만 있어
    /// 테스트가 쓰는 이음매를 완전히 우회했고, <c>/ 3</c>을 <c>* 3</c>으로
    /// 바꿔도 스위트 273개가 전부 초록이었다(기본값 기준 5초 → 45초).
    /// 여러 지점을 함께 고정하므로 상수를 그대로 돌려주도록 바꾸면 깨진다.
    /// </summary>
    [Theory]
    [InlineData(15000, 5000)]  // AppSettings.Timing.ProcessTimeoutMs 기본값
    [InlineData(1000, 333)]    // NormalizeRange가 허용하는 하한
    [InlineData(120000, 40000)] // 상한
    [InlineData(3000, 1000)]
    public void ResolveSelfTestTimeoutMs_IsOneThirdOfTheProcessBudget(
        int processTimeoutMs,
        int expectedMs)
    {
        Assert.Equal(
            expectedMs,
            EnvironmentCheckService.ResolveSelfTestTimeoutMs(processTimeoutMs));
    }

    /// <summary>
    /// 유도식이 맞다는 것만으로는 부족하다 — 점검이 러너에게 실제로 그 값을
    /// 넘기는지까지 고정한다. 두 지점을 함께 보므로 유도를 건너뛰고 예산을
    /// 그대로 넘기거나(15000), 5000을 하드코딩해도 여기서 깨진다.
    /// </summary>
    [Theory]
    [InlineData(15000, 5000)]
    [InlineData(3000, 1000)]
    public void SelfTestRunner_ReceivesTheDerivedTimeout(
        int processTimeoutMs,
        int expectedTimeoutMs)
    {
        var observed = -1;

        EnvironmentCheckService.BuildFileTransferHelperCheck(
            _proxyPath,
            processTimeoutMs,
            (_, timeoutMs) =>
            {
                observed = timeoutMs;
                return SelfTestPassed();
            });

        Assert.Equal(expectedTimeoutMs, observed);
    }

    [Fact]
    public void ResolveSelfTestTimeoutMs_FollowsTheDefaultProcessBudget()
    {
        // 값을 다른 출처에 다시 못 박거나(예: 하드코딩 5000) 기본값만 바꾸고
        // 유도식을 따라오지 않으면 여기서 깨진다. 자기진단이 일반 ADB 예산보다
        // 짧아야 한다는 것 자체도 함께 고정한다.
        var processTimeoutMs =
            AppSettings.CreateDefault().Timing.ProcessTimeoutMs;

        var timeoutMs =
            EnvironmentCheckService.ResolveSelfTestTimeoutMs(processTimeoutMs);

        Assert.Equal(5000, timeoutMs);
        Assert.Equal(processTimeoutMs / 3, timeoutMs);
        Assert.True(timeoutMs < processTimeoutMs);
    }

    /// <summary>
    /// DXMAdbProxy가 실제로 찍는 줄과 같은 모양으로 만든다. 표식은 프록시가
    /// 쓰는 것과 같은 선언(<see cref="FileTransferEnvironment"/>)에서 가져오므로
    /// 이 테스트가 표식 사본을 따로 들고 있지 않다.
    /// </summary>
    private static ProcessResult SelfTestPassed()
    {
        return new ProcessResult
        {
            Arguments = FileTransferEnvironment.SelfTestArgument,
            ExitCode = 0,
            StandardOutput = "DX Manager ADB proxy 2.0.0 " +
                FileTransferEnvironment.SelfTestSuccessMarker + ".",
            StandardError = string.Empty
        };
    }
}
