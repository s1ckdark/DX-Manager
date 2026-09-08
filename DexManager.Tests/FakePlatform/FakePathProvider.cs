using DexManager.Platform;

namespace DexManager.Tests.FakePlatform;

public sealed class FakePathProvider : IPathProvider
{
    private readonly string _root;
    private readonly string _adbPath;
    private readonly string[] _candidateAdbPaths;

    public FakePathProvider(
        string root,
        bool isPortablePackage = false,
        string adbPath = null,
        string[] candidateAdbPaths = null)
    {
        _root = root;
        // 기본값은 실존하는 실행 파일이라 경로 탐색이 타임아웃 없이 끝난다.
        // 테스트가 adb 호출 자체를 관측해야 할 때만 기록용 스크립트로 바꾼다.
        _adbPath = string.IsNullOrWhiteSpace(adbPath) ? "/bin/echo" : adbPath;
        // 후보가 여럿인 체인(PathService의 자동 선택)을 흉내 내야 하는
        // 테스트만 목록을 직접 넘긴다 - 기본값은 지금까지와 같이 후보 하나다.
        _candidateAdbPaths = candidateAdbPaths ?? new[] { _adbPath };
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(Path.Combine(_root, "config"));
        IsPortablePackage = isPortablePackage;
    }

    public string BaseDirectory => _root;

    public string DefaultSettingsFilePath =>
        Path.Combine(_root, "config", "settings.json");

    public string DefaultScreenshotFolder => Path.Combine(_root, "screenshots");

    public string DefaultLogDirectory => Path.Combine(_root, "logs");

    public string DefaultProxyExecutablePath => Path.Combine(_root, "DXMAdbProxy");

    public bool IsPortablePackage { get; set; }

    public string ResolveDefaultAdbPath() => _adbPath;

    public string ResolveDefaultScrcpyPath() => "/bin/echo";

    public string ResolveWin7AdbPath() => _adbPath;

    public string[] GetCandidateAdbPaths() => _candidateAdbPaths;

    public string[] GetCandidateScrcpyPaths() => new[] { "/bin/echo" };
}
