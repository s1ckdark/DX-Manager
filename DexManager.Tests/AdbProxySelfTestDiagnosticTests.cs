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
            _ =>
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
            path =>
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
            _ => new ProcessResult
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
            _ => new ProcessResult
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
            _ => new ProcessResult
            {
                FileName = _proxyPath,
                Arguments = "--self-test",
                ExitCode = -1,
                StandardOutput = string.Empty,
                StandardError = string.Empty,
                TimedOut = true
            });

        Assert.Equal(EnvironmentCheckStatus.Failed, item.Status);
        Assert.Contains(
            LocalizationService.Get("Environment.HelperNoResponse"),
            item.Message);
    }

    [Fact]
    public void RunnerThrows_FailsInsteadOfPassing()
    {
        var item = EnvironmentCheckService.BuildFileTransferHelperCheck(
            _proxyPath,
            _ => throw new UnauthorizedAccessException("permission denied"));

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
            _ => null);

        Assert.Equal(EnvironmentCheckStatus.Failed, item.Status);
    }

    private static ProcessResult SelfTestPassed()
    {
        return new ProcessResult
        {
            Arguments = "--self-test",
            ExitCode = 0,
            StandardOutput = "DX Manager ADB proxy 2.0.0 self-test passed.",
            StandardError = string.Empty
        };
    }
}
