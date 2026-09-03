using DexManager.Hosting;
using DexManager.Tests.FakePlatform;

namespace DexManager.Tests;

public class ApplicationHostTests : IDisposable
{
    private readonly string _root;

    public ApplicationHostTests()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            "dxm-host-tests",
            Guid.NewGuid().ToString("N"));
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

    private ApplicationHost CreateHost() => new ApplicationHost(
        new FakePlatformService(),
        new FakePathProvider(_root),
        new FakeCaptureService(),
        new FakeKeyboardService(),
        new FakeAutoStartService());

    [Fact]
    public void Constructor_ComposesAllServices()
    {
        using var host = CreateHost();

        Assert.NotNull(host.Settings);
        Assert.NotNull(host.SettingsService);
        Assert.NotNull(host.Log);
        Assert.NotNull(host.ProcessRunner);
        Assert.NotNull(host.PathService);
        Assert.NotNull(host.Adb);
        Assert.NotNull(host.WirelessAdb);
        Assert.NotNull(host.DeviceRegistry);
        Assert.NotNull(host.RuntimeSessions);
        Assert.NotNull(host.DeviceMonitor);
        Assert.NotNull(host.PermissionService);
        Assert.NotNull(host.EnvironmentCheck);
        Assert.NotNull(host.DiagnosticReport);

        // RuntimeFactory는 Task 6에서 초기화되므로 여기서 단정하지 않는다.
        // Task 6의 Constructor_InitializesRuntimeFactory가 검증한다.
    }

    [Fact]
    public void SelectedSerial_DefaultsToEmptyAndIsSettable()
    {
        using var host = CreateHost();

        Assert.Equal(string.Empty, host.SelectedSerial ?? string.Empty);

        host.SelectedSerial = "R5CT1234567";

        Assert.Equal("R5CT1234567", host.SelectedSerial);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var host = CreateHost();

        host.Dispose();
        host.Dispose();
    }
}
