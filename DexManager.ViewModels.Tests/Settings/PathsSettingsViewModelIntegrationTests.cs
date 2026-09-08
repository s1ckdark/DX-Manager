using Xunit;
using DexManager.Models;
using DexManager.Tests.FakePlatform;

namespace DexManager.ViewModels.Tests.Settings;

/// <summary>
/// PathsSettingsViewModel.Save()가 실제 <see cref="SettingsGateway"/>/
/// <see cref="DexManager.Hosting.ApplicationHost"/>를 거쳐 다음 실행의
/// PathService 선택까지 실제로 이어지는지 검증한다.
///
/// <see cref="PathsSettingsViewModelTests"/>는 인메모리
/// <c>FakeSettingsGateway</c>로 "저장된 값"만 본다 - 그 값이 다음 실행에서
/// 실제로 선택되는지는 보지 않는다. 원래 버그(ADB 경로가 저장은 되는데
/// 아무도 읽지 않던 문제)는 정확히 이 이음매(ViewModel.Save -> 디스크 저장
/// -> 새 프로세스의 PathService 선택)에 있었고, 리뷰가 이 격차를 짚었다 -
/// 이 클래스가 그 이음매를 실제로 건넌다.
/// </summary>
public class PathsSettingsViewModelIntegrationTests : IDisposable
{
    private readonly TempHostRoot _tempRoot = new();

    public void Dispose() => _tempRoot.Dispose();

    [Fact]
    public void Save_WithEnteredAdbPath_IsSelectedByNextHostLaunch()
    {
        // 자동 감지 기본값은 수동 경로와 다른 실행 파일로 둔다 - 그래야
        // Manual이 무시되고 자동 경로가 선택돼도 이 테스트가 우연히
        // 통과하는 일이 없다.
        var autoDefaultProvider = new FakePathProvider(
            _tempRoot.Root,
            adbPath: "/bin/echo");
        var fakeAdb = new FakeAdbExecutable(
            _tempRoot.Root,
            new Dictionary<string, string>());

        using (var startupHost = _tempRoot.CreateHost(
            pathProvider: autoDefaultProvider))
        {
            var gateway = new SettingsGateway(startupHost);
            var viewModel = new PathsSettingsViewModel(gateway);

            // 사용자가 설정 화면에서 실제로 값을 입력하고 저장한다.
            viewModel.AdbPath = fakeAdb.ExecutablePath;
            viewModel.SaveCommand.Execute(null);

            Assert.Equal(
                AdbSelectionMode.Manual,
                startupHost.Settings.Paths.AdbSelectionMode);
        }

        // 앱을 다시 띄운다(새 ApplicationHost, 같은 설정 파일을 디스크에서
        // 다시 읽는다). PathService가 실제로 방금 저장한 경로를 선택해야
        // 한다 - 원래 버그 아래서는 AdbSelectionMode가 절대 Manual로
        // 바뀌지 않았으므로 이 어서션은 되돌리면 실패한다.
        using var relaunchedHost = _tempRoot.CreateHost(
            pathProvider: autoDefaultProvider);

        Assert.Equal(fakeAdb.ExecutablePath, relaunchedHost.Adb.AdbPath);
    }
}
