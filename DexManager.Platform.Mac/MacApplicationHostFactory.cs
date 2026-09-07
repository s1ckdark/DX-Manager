using DexManager.Hosting;

namespace DexManager.Mac.Platform;

/// <summary>
/// macOS 플랫폼 서비스로 <see cref="ApplicationHost"/>를 조립한다.
/// TUI와 GUI가 같은 조립을 쓰게 해, 한쪽만 바뀌어 두 실행 경로가
/// 갈라지는 것을 막는다.
/// </summary>
public static class MacApplicationHostFactory
{
    public static ApplicationHost Create()
    {
        var pathProvider = new MacPathProvider();

        return new ApplicationHost(
            new MacPlatformService(),
            pathProvider,
            new MacCaptureService(pathProvider.DefaultScreenshotFolder),
            new MacKeyboardService(),
            new MacAutoStartService());
    }
}
