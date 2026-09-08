using DexManager.Hosting;
using DexManager.Tests.FakePlatform;

namespace DexManager.ViewModels.Tests;

/// <summary>
/// 테스트용 임시 디렉터리와 그 위에서 동작하는 <see cref="ApplicationHost"/>를
/// 만든다. <c>DexManager.Tests</c>의 동등한 헬퍼와 같은 동작이지만, 프로젝트가
/// 달라 재사용할 수 없어 여기에 따로 둔다.
/// </summary>
public sealed class TempHostRoot : IDisposable
{
    private readonly string _root;

    public string Root => _root;

    public TempHostRoot()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            "dxm-vm-tests",
            Guid.NewGuid().ToString("N"));
    }

    /// <param name="pathProvider">
    /// 기본값 대신 쓸 경로 제공자. 같은 <see cref="Root"/>를 가리키는
    /// 제공자를 재사용해 "같은 설정 파일로 앱을 다시 띄운다"를
    /// 흉내내려는 호출자를 위한 것 - 그러지 않으면 매 호출마다 새
    /// <see cref="FakePathProvider"/>가 만들어져 자동 감지 adb 기본값이
    /// 호출마다 달라질 수 있다.
    /// </param>
    public ApplicationHost CreateHost(FakePathProvider pathProvider = null) =>
        new ApplicationHost(
            new FakePlatformService(),
            pathProvider ?? new FakePathProvider(_root),
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
