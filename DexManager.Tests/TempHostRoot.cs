using DexManager.Hosting;
using DexManager.Tests.FakePlatform;

namespace DexManager.Tests;

/// <summary>
/// 테스트용 임시 디렉터리와 그 위에서 동작하는 <see cref="ApplicationHost"/>를
/// 만든다.
/// </summary>
public sealed class TempHostRoot : IDisposable
{
    private readonly string _root;

    public TempHostRoot()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            "dxm-tests",
            Guid.NewGuid().ToString("N"));
    }

    public ApplicationHost CreateHost() => new ApplicationHost(
        new FakePlatformService(),
        new FakePathProvider(_root),
        new FakeCaptureService(),
        new FakeKeyboardService(),
        new FakeAutoStartService());

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
}
