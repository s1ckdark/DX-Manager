using System;
using System.IO;
using System.Runtime.InteropServices;
using DexManager.Models;
using DexManager.Services;
using Xunit;

namespace DexManager.Tests
{
    public class ScrcpyVersionDiagnosticTests
    {
        private const string Scrcpy41Output =
            "scrcpy 4.1 <https://github.com/Genymobile/scrcpy>\n" +
            "\n" +
            "Dependencies (compiled / linked):\n" +
            " - SDL: 3.2.4 / 3.2.4\n";

        private const string Scrcpy334Output =
            "scrcpy 3.3.4 <https://github.com/Genymobile/scrcpy>\n" +
            "\n" +
            "Dependencies (compiled / linked):\n" +
            " - SDL: 2.32.8 / 2.32.8\n";

        [Fact]
        public void ParseVersionOutput_ReadsMajorMinorAndSdlMajor()
        {
            var info = ScrcpyRuntimeInfo.ParseVersionOutput(Scrcpy41Output);

            Assert.Equal(4, info.MajorVersion);
            Assert.Equal(1, info.MinorVersion);
            Assert.Equal(3, info.SdlMajorVersion);
        }

        [Fact]
        public void ParseVersionOutput_KeepsPatchDigitInDisplayVersion()
        {
            var info = ScrcpyRuntimeInfo.ParseVersionOutput(Scrcpy334Output);

            Assert.Equal(3, info.MajorVersion);
            Assert.Equal(3, info.MinorVersion);
            Assert.Equal(2, info.SdlMajorVersion);
            Assert.Equal("3.3.4", info.DisplayVersion);
        }

        [Fact]
        public void ParseVersionOutput_FallsBackToBundledBaselineWhenOutputIsUnreadable()
        {
            var info = ScrcpyRuntimeInfo.ParseVersionOutput(string.Empty);

            Assert.Equal(4, info.MajorVersion);
            Assert.Equal(1, info.MinorVersion);
            Assert.Equal(3, info.SdlMajorVersion);
        }

        [Fact]
        public void MeetsRecommendedVersion_IsFalseBelowVersion4()
        {
            Assert.False(
                ScrcpyRuntimeInfo.ParseVersionOutput(Scrcpy334Output)
                    .MeetsRecommendedVersion);
            Assert.True(
                ScrcpyRuntimeInfo.ParseVersionOutput(Scrcpy41Output)
                    .MeetsRecommendedVersion);
        }

        [Fact]
        public void BuildScrcpyVersionCheck_WarnsAndNamesVersionBelowVersion4()
        {
            var item = EnvironmentCheckService.BuildScrcpyVersionCheck(
                ScrcpyRuntimeInfo.ParseVersionOutput(Scrcpy334Output));

            Assert.Equal(EnvironmentCheckStatus.Warning, item.Status);
            Assert.Contains("3.3.4", item.Message);
            Assert.Contains("2", item.Message);
        }

        [Fact]
        public void BuildScrcpyVersionCheck_PassesOnVersion4OrNewer()
        {
            var item = EnvironmentCheckService.BuildScrcpyVersionCheck(
                ScrcpyRuntimeInfo.ParseVersionOutput(Scrcpy41Output));

            Assert.Equal(EnvironmentCheckStatus.Passed, item.Status);
            Assert.Contains("4.1", item.Message);
        }

        [Fact]
        public void BuildScrcpyVersionCheck_ReportsUnknownWhenRuntimeInfoMissing()
        {
            var item = EnvironmentCheckService.BuildScrcpyVersionCheck(null);

            Assert.Equal(EnvironmentCheckStatus.Warning, item.Status);
        }
        [Fact]
        public void ResolveScrcpyRuntimeInfo_ReturnsNullWhenExecutableIsMissing()
        {
            var info = EnvironmentCheckService.ResolveScrcpyRuntimeInfo(
                null,
                "/nonexistent_folder_xyz_12345/scrcpy",
                new LogService());

            Assert.Null(info);
        }

        [Fact]
        public void ResolveScrcpyRuntimeInfo_ProbesExecutableWhenScrcpyServiceIsAbsent()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

            var dir = Path.Combine(
                Path.GetTempPath(),
                "dxm-scrcpy-probe-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var fake = Path.Combine(dir, "scrcpy");
            try
            {
                File.WriteAllText(
                    fake,
                    "#!/bin/sh\n" +
                    "echo 'scrcpy 3.3.4 <https://github.com/Genymobile/scrcpy>'\n" +
                    "echo ''\n" +
                    "echo 'Dependencies (compiled / linked):'\n" +
                    "echo ' - SDL: 2.32.8 / 2.32.8'\n");
                File.SetUnixFileMode(
                    fake,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite |
                    UnixFileMode.UserExecute);

                var info = EnvironmentCheckService.ResolveScrcpyRuntimeInfo(
                    null,
                    fake,
                    new LogService());

                Assert.NotNull(info);
                Assert.Equal("3.3.4", info.DisplayVersion);
                Assert.Equal(2, info.SdlMajorVersion);
                Assert.False(info.MeetsRecommendedVersion);
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch { }
            }
        }
    }
}
