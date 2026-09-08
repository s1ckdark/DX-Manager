using DexManager.Models;
using DexManager.Services;
using DexManager.Tests.FakePlatform;
using DexManager.Utils;

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
    public void ParseLockState_WhenShowingLockscreenIsTrueAndTheKeyguardIsSecured_ReturnsLocked()
    {
        const string dump = """
            WINDOW MANAGER POLICY STATE (dumpsys window policy)
              mSafeMode=false
              mSystemReady=true
              mDreamingLockscreen=false
              mShowingLockscreen=true
              isStatusBarKeyguard=true
              KeyguardServiceDelegate
                showing=true
                occluded=false
                secure=true
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
        const string dump = """
            isStatusBarKeyguard=true
            KeyguardServiceDelegate
              secure=true
            """;

        Assert.Equal(LockState.Locked, AdbService.ParseLockState(dump));
    }

    // --- F-8: 잠금 화면이 "떠 있음"만으로는 Locked가 아니다 ---

    [Fact]
    public void ParseLockState_WhenTheKeyguardIsShowingButNotSecured_DoesNotReportLocked()
    {
        // 이게 가장 흔한 첫 시작 상태다: PIN/패턴이 없는(스와이프 전용)
        // 폰이 책상 위에서 화면만 꺼진 채 꽂혀 있으면 mKeyguardShowing은
        // true다. 여기서 Locked를 돌려주면 "먼저 폰 잠금을 해제하세요"로
        // 시작을 막게 되는데, 그 폰에는 해제할 잠금 자체가 없다 -
        // 원래는 되던 시작을 막는, 이 기능이 절대 해선 안 되는 실패
        // 방향이다.
        const string dump = """
            WINDOW MANAGER POLICY STATE (dumpsys window policy)
              mKeyguardShowing=true
              KeyguardServiceDelegate
                showing=true
                occluded=false
                secure=false
            """;

        Assert.NotEqual(LockState.Locked, AdbService.ParseLockState(dump));
    }

    [Fact]
    public void ParseLockState_WhenTheKeyguardIsShowingButNoSecureSignalIsAvailable_FailsOpenToUnknown()
    {
        // secure 신호를 전혀 찾지 못하면 "잠겨 있다"고 확신할 수 없다.
        // 다른 모든 불확실 경로와 똑같이 fail-open이어야 한다.
        const string dump = """
            WINDOW MANAGER POLICY STATE (dumpsys window policy)
              mKeyguardShowing=true
              isStatusBarKeyguard=true
            """;

        Assert.Equal(LockState.Unknown, AdbService.ParseLockState(dump));
    }

    [Fact]
    public void ParseLockState_WhenTheKeyguardIsNotShowing_StaysUnlockedEvenOnASecuredPhone()
    {
        // PIN이 걸린 폰이라도 잠금 화면이 떠 있지 않으면 잠겨 있지 않다 -
        // secure=true가 단독으로 Locked를 만들어서는 안 된다.
        const string dump = """
            mShowingLockscreen=false
            KeyguardServiceDelegate
              secure=true
            """;

        Assert.Equal(LockState.Unlocked, AdbService.ParseLockState(dump));
    }

    [Fact]
    public void ParseLockState_AcceptsAnExplicitlyKeyguardNamedSecureFieldOutsideTheDelegateBlock()
    {
        // 스킨에 따라 KeyguardServiceDelegate 블록 대신 이름 자체에
        // keyguard가 박힌 필드로 노출되기도 한다. 이름이 명시적이면
        // 블록 안이 아니어도 신뢰할 수 있다.
        const string dump = """
            mKeyguardShowing=true
            isKeyguardSecure=true
            """;

        Assert.Equal(LockState.Locked, AdbService.ParseLockState(dump));
    }

    [Fact]
    public void ParseLockState_IgnoresABareSecureFieldOutsideTheKeyguardDelegateBlock()
    {
        // dumpsys window에는 창(WindowState) 목록도 함께 실린다. 이름
        // 없는 secure= 하나를 아무 데서나 주워 오면 엉뚱한 창의 플래그로
        // 잠금을 확신하게 된다 - 그 방향의 오탐이 곧 시작 차단이다.
        //
        // 여기서 KeyguardServiceDelegate 블록은 "존재하되 secure를 찍지
        // 않는" 모양이고, 무관한 secure=true는 그 블록보다 앞에 있다.
        // 블록 범위로 잘라 읽지 않고 출력 전체에서 secure=를 찾으면 이
        // 값을 주워 Locked로 단정하게 된다.
        const string dump = """
            mKeyguardShowing=true
            Window #3 Window{abc u0 com.example/.Main}:
              secure=true
            KeyguardServiceDelegate
              showing=true
              occluded=false
            """;

        Assert.Equal(LockState.Unknown, AdbService.ParseLockState(dump));
    }

    // --- F-9: 좌측 단어 경계 ---

    [Fact]
    public void ParseLockState_DoesNotMatchAFieldThatMerelyEndsWithAKnownName()
    {
        // 오른쪽 경계는 이미 있었지만(mShowingLockscreenFoo는 거부됨)
        // 왼쪽에는 없어서, 이름이 알려진 필드로 "끝나는" 필드가 우선순위
        // 스캔을 가로챌 수 있었다.
        const string dump = """
            XmKeyguardShowing=true
            KeyguardServiceDelegate
              secure=true
            """;

        Assert.Equal(LockState.Unknown, AdbService.ParseLockState(dump));
    }

    [Fact]
    public void ParseLockState_DoesNotMatchASecureFieldThatMerelyEndsWithSecure()
    {
        const string dump = """
            mKeyguardShowing=true
            KeyguardServiceDelegate
              mIsDeviceSecure=true
            """;

        Assert.Equal(LockState.Unknown, AdbService.ParseLockState(dump));
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
                ["phone-a"] = "mShowingLockscreen=true isKeyguardSecure=true"
            });
        var host = root.CreateHost(pathProvider: adb.CreatePathProvider());

        var result = host.Adb.IsDeviceLocked("phone-a");

        Assert.Equal(LockState.Locked, result);
        Assert.Contains(
            adb.Invocations,
            line => line.Contains("-s phone-a", StringComparison.Ordinal) &&
                line.Contains("dumpsys window", StringComparison.Ordinal));
    }

    // --- dumpsys trust: 권위 있는 신호 (실기 SM-F971N 확인) ---

    [Fact]
    public void ParseTrustState_WhenTheCurrentUserLineShowsDeviceLockedOne_ReturnsLocked()
    {
        const string dump = """
             User "Owner" (id=0, flags=0x4c13) (current): trustState=TRUSTED, trustManaged=1, deviceLocked=1, isActiveUnlockRunning=0, strongAuthRequired=0x1
            """;

        Assert.Equal(LockState.Locked, AdbService.ParseTrustState(dump));
    }

    [Fact]
    public void ParseTrustState_WhenTheCurrentUserLineShowsDeviceLockedZero_ReturnsUnlocked()
    {
        const string dump = """
             User "Owner" (id=0, flags=0x4c13) (current): trustState=TRUSTED, trustManaged=1, deviceLocked=0, isActiveUnlockRunning=0, strongAuthRequired=0x0
            """;

        Assert.Equal(LockState.Unlocked, AdbService.ParseTrustState(dump));
    }

    [Fact]
    public void ParseTrustState_WhenANonCurrentUserLineShowsDeviceLockedOne_DoesNotWinOverTheCurrentUser()
    {
        // 다중 사용자 기기: id=10(비-current)이 deviceLocked=1이어도,
        // 실제로 세션을 판단해야 하는 (current) 사용자(id=0)가
        // deviceLocked=0이면 그 값이 이겨야 한다.
        const string dump = """
             User "Guest" (id=10, flags=0x0): trustState=TRUSTED, trustManaged=0, deviceLocked=1, isActiveUnlockRunning=0, strongAuthRequired=0x1
             User "Owner" (id=0, flags=0x4c13) (current): trustState=TRUSTED, trustManaged=1, deviceLocked=0, isActiveUnlockRunning=0, strongAuthRequired=0x0
            """;

        Assert.Equal(LockState.Unlocked, AdbService.ParseTrustState(dump));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("SOME_UNRECOGNIZED_TRUST_SHAPE")]
    public void ParseTrustState_WhenNoCurrentUserLineWithDeviceLockedAppears_FailsOpenToUnknown(string dump)
    {
        Assert.Equal(LockState.Unknown, AdbService.ParseTrustState(dump));
    }

    [Fact]
    public void ParseTrustState_WhenTheCurrentUserLineHasNoDeviceLockedField_FailsOpenToUnknown()
    {
        const string dump = """
             User "Owner" (id=0, flags=0x4c13) (current): trustState=TRUSTED, trustManaged=1
            """;

        Assert.Equal(LockState.Unknown, AdbService.ParseTrustState(dump));
    }

    [Fact]
    public void ParseTrustState_WhenALongerFieldEndsWithDeviceLocked_DoesNotWinOverTheRealField()
    {
        // 좌측 워드 경계 고정. `deviceLocked`로 끝나는 더 긴 이름이 앞에
        // 오더라도 진짜 `deviceLocked`가 이겨야 한다. 경계가 없으면 앞선
        // `mDeviceLocked=1`이 먼저 매치되어 잠기지 않은 폰을 Locked로
        // 판정하고, DeX 시작을 잘못 막는다 - 이 기능이 절대 하면 안 되는
        // 실패 방향이다. 같은 성격의 가드가 MatchBooleanField에도 있고,
        // 그쪽은 실기 출력의 `simSecure=false`가 `secure=false`로 오독되는
        // 것을 막아 load-bearing임이 확인됐다.
        const string dump = """
             User "Owner" (id=0, flags=0x4c13) (current): mDeviceLocked=1, trustManaged=1, deviceLocked=0, strongAuthRequired=0x0
            """;

        Assert.Equal(LockState.Unlocked, AdbService.ParseTrustState(dump));
    }

    [Fact]
    public void IsDeviceLocked_PrefersDumpsysTrustOverDumpsysWindow_WhenTrustGivesAnAnswer()
    {
        // dumpsys window 쪽은 (일부러) 반대 답을 준다 - trust가 이겨야
        // 실기에서 window의 secure 필드가 아예 없는 기기에서도 정확한
        // 판단이 나온다는 걸 고정한다.
        using var root = new TempHostRoot();
        var adb = new FakeAdbExecutable(
            root.Root,
            new Dictionary<string, string> { ["phone-a"] = "HWA" },
            new Dictionary<string, string>
            {
                ["phone-a"] = "mShowingLockscreen=false"
            },
            new Dictionary<string, string>
            {
                ["phone-a"] =
                    " User \"Owner\" (id=0, flags=0x4c13) (current): " +
                    "trustState=TRUSTED, trustManaged=1, deviceLocked=1, " +
                    "isActiveUnlockRunning=0, strongAuthRequired=0x1"
            });
        var host = root.CreateHost(pathProvider: adb.CreatePathProvider());

        var result = host.Adb.IsDeviceLocked("phone-a");

        Assert.Equal(LockState.Locked, result);
        Assert.Contains(
            adb.Invocations,
            line => line.Contains("-s phone-a", StringComparison.Ordinal) &&
                line.Contains("dumpsys trust", StringComparison.Ordinal));
    }

    [Fact]
    public void IsDeviceLocked_WhenDumpsysTrustYieldsNothing_FallsBackToDumpsysWindow()
    {
        using var root = new TempHostRoot();
        var adb = new FakeAdbExecutable(
            root.Root,
            new Dictionary<string, string> { ["phone-a"] = "HWA" },
            new Dictionary<string, string>
            {
                ["phone-a"] = "mShowingLockscreen=true isKeyguardSecure=true"
            });
            // dumpsysTrustOutputByTransport를 아예 넘기지 않는다 - 이
            // 기기/빌드에는 dumpsys trust 신호가 없다고 흉내낸다.
        var host = root.CreateHost(pathProvider: adb.CreatePathProvider());

        var result = host.Adb.IsDeviceLocked("phone-a");

        Assert.Equal(LockState.Locked, result);
        Assert.Contains(
            adb.Invocations,
            line => line.Contains("-s phone-a", StringComparison.Ordinal) &&
                line.Contains("dumpsys window", StringComparison.Ordinal));
    }

    [Fact]
    public void IsDeviceLocked_WhenBothDumpsysTrustAndWindowYieldNothing_FailsOpenToUnknown()
    {
        using var root = new TempHostRoot();
        var adb = new FakeAdbExecutable(
            root.Root,
            new Dictionary<string, string> { ["phone-a"] = "HWA" },
            new Dictionary<string, string>
            {
                ["phone-a"] = "SOME_UNRECOGNIZED_DUMP_SHAPE"
            },
            new Dictionary<string, string>
            {
                ["phone-a"] = "SOME_UNRECOGNIZED_TRUST_SHAPE"
            });
        var host = root.CreateHost(pathProvider: adb.CreatePathProvider());

        var result = host.Adb.IsDeviceLocked("phone-a");

        Assert.Equal(LockState.Unknown, result);
    }

    // --- WakeScreen: 화면 깨우기 ---

    [Fact]
    public void WakeScreen_SendsKeyeventWakeupToTheCorrectDevice()
    {
        using var root = new TempHostRoot();
        var adb = new FakeAdbExecutable(
            root.Root,
            new Dictionary<string, string> { ["phone-a"] = "HWA" });
        var host = root.CreateHost(pathProvider: adb.CreatePathProvider());

        var result = host.Adb.WakeScreen("phone-a");

        Assert.True(result);
        Assert.Contains(
            adb.Invocations,
            line => line.Contains("-s phone-a", StringComparison.Ordinal) &&
                line.Contains("input keyevent 224", StringComparison.Ordinal));
    }

    [Fact]
    public void WakeScreen_WhenTheAdbProcessCannotEvenBeLaunched_ReturnsFalseInsteadOfThrowing()
    {
        // IsDeviceLocked의 동일한 예외-던짐 사례와 같은 이유: 화면 깨우기는
        // 최선-노력 전처리일 뿐이므로, 이 예외 하나 때문에 DeX 시작
        // 전체가 죽어서는 안 된다.
        var logService = new LogService();
        var processRunner = new ProcessRunner(logService);
        var missingAdbPath = Path.Combine(
            Path.GetTempPath(),
            "dxm-tests-missing-adb",
            Guid.NewGuid().ToString("N"),
            "adb");
        var adbService = new AdbService(
            missingAdbPath,
            1000,
            processRunner,
            logService);

        var result = adbService.WakeScreen("phone-a");

        Assert.False(result);
    }

    // --- DismissKeyguard: 키가드 해제 (fix round 1 - 실기 재확인) ---

    [Fact]
    public void DismissKeyguard_SendsWmDismissKeyguardToTheCorrectDevice()
    {
        using var root = new TempHostRoot();
        var adb = new FakeAdbExecutable(
            root.Root,
            new Dictionary<string, string> { ["phone-a"] = "HWA" });
        var host = root.CreateHost(pathProvider: adb.CreatePathProvider());

        var result = host.Adb.DismissKeyguard("phone-a");

        Assert.True(result);
        Assert.Contains(
            adb.Invocations,
            line => line.Contains("-s phone-a", StringComparison.Ordinal) &&
                line.Contains("wm dismiss-keyguard", StringComparison.Ordinal));
    }

    [Fact]
    public void DismissKeyguard_WhenTheAdbProcessCannotEvenBeLaunched_ReturnsFalseInsteadOfThrowing()
    {
        // WakeScreen/IsDeviceLocked와 동일한 이유: 키가드 해제는 최선-노력
        // 보조 단계일 뿐이므로, 이 예외 하나 때문에 DeX 시작 전체가
        // 죽어서는 안 된다.
        var logService = new LogService();
        var processRunner = new ProcessRunner(logService);
        var missingAdbPath = Path.Combine(
            Path.GetTempPath(),
            "dxm-tests-missing-adb",
            Guid.NewGuid().ToString("N"),
            "adb");
        var adbService = new AdbService(
            missingAdbPath,
            1000,
            processRunner,
            logService);

        var result = adbService.DismissKeyguard("phone-a");

        Assert.False(result);
    }

    [Fact]
    public void IsDeviceLocked_WhenTheAdbProcessCannotEvenBeLaunched_FailsOpenToUnknownInsteadOfThrowing()
    {
        // ShellForSerial이 타는 ProcessRunner.Run은 결과가 실패인 채로
        // "돌아오는" 게 아니라, 실행 파일이 없으면 FileNotFoundException을
        // 그대로 "던진다"(ProcessRunner.cs). 이 잠금 탐지는 어디까지나
        // 최선-노력 보조 신호이므로, 이 예외 하나 때문에 DeX 시작 전체가
        // 죽어서는 안 된다 - 그러면 이 기능이 도우려던 대상을 스스로
        // 깨뜨리는 셈이다. IsSuccess 분기만으로는 "실패로 반환된" 경우만
        // 잡고 "던져진" 경우를 놓친다는 게 이 테스트가 고정하는 간극이다.
        var logService = new LogService();
        var processRunner = new ProcessRunner(logService);
        var missingAdbPath = Path.Combine(
            Path.GetTempPath(),
            "dxm-tests-missing-adb",
            Guid.NewGuid().ToString("N"),
            "adb");
        var adbService = new AdbService(
            missingAdbPath,
            1000,
            processRunner,
            logService);

        var result = adbService.IsDeviceLocked("phone-a");

        Assert.Equal(LockState.Unknown, result);
    }
}
