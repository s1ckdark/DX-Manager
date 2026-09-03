using DexManager.Platform;

namespace DexManager.Tests.FakePlatform;

public sealed class FakePathProvider : IPathProvider
{
    private readonly string _root;

    public FakePathProvider(string root)
    {
        _root = root;
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(Path.Combine(_root, "config"));
    }

    public string BaseDirectory => _root;

    public string DefaultSettingsFilePath =>
        Path.Combine(_root, "config", "settings.json");

    public string DefaultScreenshotFolder => Path.Combine(_root, "screenshots");

    public string DefaultLogDirectory => Path.Combine(_root, "logs");

    public string DefaultProxyExecutablePath => Path.Combine(_root, "DXMAdbProxy");

    public bool IsPortablePackage => false;

    // 실존하는 실행 파일을 반환해 경로 탐색 타임아웃을 피한다.
    public string ResolveDefaultAdbPath() => "/bin/echo";

    public string ResolveDefaultScrcpyPath() => "/bin/echo";

    public string ResolveWin7AdbPath() => "/bin/echo";

    public string[] GetCandidateAdbPaths() => new[] { "/bin/echo" };

    public string[] GetCandidateScrcpyPaths() => new[] { "/bin/echo" };
}
