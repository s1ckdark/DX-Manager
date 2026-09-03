using System;

namespace DexManager.Platform
{
    public interface IPathProvider
    {
        string BaseDirectory { get; }
        string DefaultSettingsFilePath { get; }
        string DefaultScreenshotFolder { get; }
        string DefaultLogDirectory { get; }
        string DefaultProxyExecutablePath { get; }

        /// <summary>
        /// 번들된 도구(포터블 패키지)를 우선 사용하는 배포 형태인지 여부.
        /// </summary>
        bool IsPortablePackage { get; }

        string ResolveDefaultAdbPath();
        string ResolveDefaultScrcpyPath();
        string ResolveWin7AdbPath();

        string[] GetCandidateAdbPaths();
        string[] GetCandidateScrcpyPaths();
    }
}
