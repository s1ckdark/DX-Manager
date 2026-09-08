using DexManager.Models;
using DexManager.Services;
using DexManager.Tests.FakePlatform;

namespace DexManager.Tests;

/// <summary>
/// <c>dumpsys window</c> 출력에서 잠금 상태를 읽어내는 순수 파싱 규칙을
/// 고정한다. 실물 기기 없이도 알려진 필드 이름(<c>mShowingLockscreen</c>,
/// <c>mDreamingLockscreen</c>, <c>mKeyguardShowing</c>,
/// <c>isStatusBarKeyguard</c>)을 흉내 낸 문자열만으로 검증할 수 있다.
/// 이 규칙의 안전한 실패 방향은 "모르면 잠기지 않은 것으로 본다"이다 —
/// 파싱이 뚫려도 정상적으로 되는 DeX 시작을 잘못 막아서는 안 되기 때문이다.
/// </summary>
public class AdbServiceLockStateTests
{
    [Fact]
    public void ParseLockState_WhenShowingLockscreenIsTrue_ReturnsLocked()
    {
        const string dump = """
            WINDOW MANAGER POLICY STATE (dumpsys window policy)
              mSafeMode=false
              mSystemReady=true
              mDreamingLockscreen=false
              mShowingLockscreen=true
              isStatusBarKeyguard=true
            """;

        Assert.Equal(LockState.Locked, AdbService.ParseLockState(dump));
    }

    [Fact]
    public void ParseLockState_WhenShowingLockscreenIsFalse_ReturnsUnlocked()
    {
        const string dump = """
            WINDOW MANAGER POLICY STATE (dumpsys window policy)
              mSafeMode=false
              mSystemReady=true
              mDreamingLockscreen=false
              mShowingLockscreen=false
              isStatusBarKeyguard=false
            """;

        Assert.Equal(LockState.Unlocked, AdbService.ParseLockState(dump));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("WINDOW MANAGER something totally unrelated\n mSomeOtherField=true")]
    public void ParseLockState_WhenNoKnownFieldAppears_FailsOpenToUnknown(string dump)
    {
        // fail-open의 핵심 계약: 인식하지 못한 출력은 절대 Locked가 되면
        // 안 된다. 정상적으로 될 DeX 시작을 파싱 공백 때문에 막을 수는
        // 없다.
        Assert.Equal(LockState.Unknown, AdbService.ParseLockState(dump));
    }

    [Fact]
    public void ParseLockState_WhenFieldsDisagree_HigherPriorityFieldWins_EvenWhenItAppearsLater()
    {
        // mDreamingLockscreen이 텍스트상 먼저 나오고 true지만,
        // mShowingLockscreen(우선순위 1위)이 뒤에서 false라고 말한다.
        // "텍스트에서 처음 만난 필드"가 아니라 "고정된 우선순위에서 가장
        // 앞선 필드"가 이겨야 한다 — dumpsys 출력 순서는 보장되지 않는다.
        const string dump = """
            mDreamingLockscreen=true
            some other noise line
            mShowingLockscreen=false
            isStatusBarKeyguard=true
            """;

        Assert.Equal(LockState.Unlocked, AdbService.ParseLockState(dump));
    }

    [Fact]
    public void ParseLockState_WhenOnlyALowerPriorityFieldAppears_UsesIt()
    {
        const string dump = "isStatusBarKeyguard=true";

        Assert.Equal(LockState.Locked, AdbService.ParseLockState(dump));
    }

    [Fact]
    public void IsDeviceLocked_SendsDumpsysWindowAndParsesTheRealShellResult()
    {
        // ParseLockState 자체는 순수 함수라 위에서 문자열만으로 고정했다.
        // 이 테스트는 그 규칙이 실제 AdbService.ShellForSerial 배선을
        // 거쳐도 그대로 동작하는지 - 즉 올바른 기기에 올바른 명령을
        // 보내는지 - 확인한다.
        using var root = new TempHostRoot();
        var adb = new FakeAdbExecutable(
            root.Root,
            new Dictionary<string, string> { ["phone-a"] = "HWA" },
            new Dictionary<string, string>
            {
                ["phone-a"] = "mShowingLockscreen=true"
            });
        var host = root.CreateHost(pathProvider: adb.CreatePathProvider());

        var result = host.Adb.IsDeviceLocked("phone-a");

        Assert.Equal(LockState.Locked, result);
        Assert.Contains(
            adb.Invocations,
            line => line.Contains("-s phone-a", StringComparison.Ordinal) &&
                line.Contains("dumpsys window", StringComparison.Ordinal));
    }
}
