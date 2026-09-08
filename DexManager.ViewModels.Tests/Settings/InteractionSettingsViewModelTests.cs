using Xunit;
using DexManager.Models;

namespace DexManager.ViewModels.Tests.Settings;

/// <summary>
/// InteractionSettingsViewModel(키매핑 설정 골격)의 로드·저장·재설정·검증
/// 동작을 검증한다. Task 9는 골격만 다룬다 — 실제 단축키 문법 파싱이나
/// 저수준 후크 충돌 감지(WinForms의 HotkeyService)는 Task 10의 몫이므로
/// 여기서는 "비어 있지 않고 서로 달라야 한다"는 최소 구조적 검증만 다룬다.
/// </summary>
public class InteractionSettingsViewModelTests
{
    [Fact]
    public void Constructor_LoadsCurrentKeyMappingsFromGateway()
    {
        // 생성 시 게이트웨이의 현재 KeyMappings 10개 필드를 모두 로드한다.
        var gateway = new FakeSettingsGateway();
        gateway.Update(s =>
        {
            s.KeyMappings.CaptureHotkey = "F9";
            s.KeyMappings.ExitHotkey = "LeftAlt+F9";
            s.KeyMappings.UseLowLevelHotkeys = false;
            s.KeyMappings.LogKeyboardDiagnostics = true;
            s.KeyMappings.ConvertKoreanEnglishKey = false;
            s.KeyMappings.KoreanEnglishInputMode = KeyInputMode.Adb;
            s.KeyMappings.HandleRightWindowsKey = false;
            s.KeyMappings.ConvertEnterToShiftEnter = true;
            s.KeyMappings.EnterInputMode = KeyInputMode.SendInputVirtualKey;
            s.KeyMappings.IgnoreShiftSpace = true;
        });

        var viewModel = new InteractionSettingsViewModel(gateway);

        Assert.Equal("F9", viewModel.CaptureHotkey);
        Assert.Equal("LeftAlt+F9", viewModel.ExitHotkey);
        Assert.False(viewModel.UseLowLevelHotkeys);
        Assert.True(viewModel.LogKeyboardDiagnostics);
        Assert.False(viewModel.ConvertKoreanEnglishKey);
        Assert.Equal(KeyInputMode.Adb, viewModel.KoreanEnglishInputMode);
        Assert.False(viewModel.HandleRightWindowsKey);
        Assert.True(viewModel.ConvertEnterToShiftEnter);
        Assert.Equal(KeyInputMode.SendInputVirtualKey, viewModel.EnterInputMode);
        Assert.True(viewModel.IgnoreShiftSpace);
    }

    [Fact]
    public void Save_WhenEdited_WritesAllFieldsToSettingsKeyMappingsViaGateway()
    {
        // 10개 필드를 모두 편집하고 저장하면 s.KeyMappings에 실제로 대입된다.
        var gateway = new FakeSettingsGateway();
        var viewModel = new InteractionSettingsViewModel(gateway);

        viewModel.CaptureHotkey = "F10";
        viewModel.ExitHotkey = "LeftAlt+F10";
        viewModel.UseLowLevelHotkeys = !viewModel.UseLowLevelHotkeys;
        viewModel.LogKeyboardDiagnostics = !viewModel.LogKeyboardDiagnostics;
        viewModel.ConvertKoreanEnglishKey = !viewModel.ConvertKoreanEnglishKey;
        viewModel.KoreanEnglishInputMode = KeyInputMode.Adb;
        viewModel.HandleRightWindowsKey = !viewModel.HandleRightWindowsKey;
        viewModel.ConvertEnterToShiftEnter = !viewModel.ConvertEnterToShiftEnter;
        viewModel.EnterInputMode = KeyInputMode.SendInputScanCode;
        viewModel.IgnoreShiftSpace = !viewModel.IgnoreShiftSpace;

        Assert.True(viewModel.IsValid);
        viewModel.SaveCommand.Execute(null);

        var saved = gateway.Current.KeyMappings;
        Assert.Equal("F10", saved.CaptureHotkey);
        Assert.Equal("LeftAlt+F10", saved.ExitHotkey);
        Assert.Equal(viewModel.UseLowLevelHotkeys, saved.UseLowLevelHotkeys);
        Assert.Equal(viewModel.LogKeyboardDiagnostics, saved.LogKeyboardDiagnostics);
        Assert.Equal(viewModel.ConvertKoreanEnglishKey, saved.ConvertKoreanEnglishKey);
        Assert.Equal(KeyInputMode.Adb, saved.KoreanEnglishInputMode);
        Assert.Equal(viewModel.HandleRightWindowsKey, saved.HandleRightWindowsKey);
        Assert.Equal(viewModel.ConvertEnterToShiftEnter, saved.ConvertEnterToShiftEnter);
        Assert.Equal(KeyInputMode.SendInputScanCode, saved.EnterInputMode);
        Assert.Equal(viewModel.IgnoreShiftSpace, saved.IgnoreShiftSpace);
        Assert.Equal(1, gateway.UpdateCallCount);
    }

    [Fact]
    public void HasChanges_FalseInitially_TrueAfterEdit_FalseAfterSave()
    {
        // HasChanges는 로드 직후 false, 편집 후 true, 저장 후 false다.
        var gateway = new FakeSettingsGateway();
        var viewModel = new InteractionSettingsViewModel(gateway);

        Assert.False(viewModel.HasChanges);

        viewModel.CaptureHotkey = "F11";

        Assert.True(viewModel.HasChanges);

        viewModel.SaveCommand.Execute(null);

        Assert.False(viewModel.HasChanges);
    }

    [Fact]
    public void ResetToBundledDefaults_RepopulatesFromCreateDefault_NotFromLiveGatewayValue()
    {
        // 재설정은 번들 기본값(AppSettings.CreateDefault())에서 읽어야 한다,
        // gateway.Current(현재값)가 아니라. 이를 검증하기 위해 게이트웨이의
        // 현재값을 번들 기본값과 다르게 먼저 밀어놓는다. Reset이
        // CreateDefault()를 읽으면 기본값으로 복구되고, gateway.Current를
        // 읽으면 이미 live 값이므로 "성공"처럼 보이는 거짓 양성을 피한다.
        var gateway = new FakeSettingsGateway();
        var defaultKeyMappings = AppSettings.CreateDefault().KeyMappings;

        gateway.Update(s =>
        {
            s.KeyMappings.CaptureHotkey = "/* diverged from default */F7";
            s.KeyMappings.ExitHotkey = "LeftAlt+F7";
            s.KeyMappings.UseLowLevelHotkeys = !defaultKeyMappings.UseLowLevelHotkeys;
            s.KeyMappings.IgnoreShiftSpace = !defaultKeyMappings.IgnoreShiftSpace;
        });

        var viewModel = new InteractionSettingsViewModel(gateway);

        // 초기 로드 후에는 gateway.Current(라이브 값)로 로드되어 있다.
        Assert.Equal("/* diverged from default */F7", viewModel.CaptureHotkey);

        viewModel.ResetToBundledDefaultsCommand.Execute(null);

        Assert.Equal(defaultKeyMappings.CaptureHotkey, viewModel.CaptureHotkey);
        Assert.Equal(defaultKeyMappings.ExitHotkey, viewModel.ExitHotkey);
        Assert.Equal(defaultKeyMappings.UseLowLevelHotkeys, viewModel.UseLowLevelHotkeys);
        Assert.Equal(defaultKeyMappings.IgnoreShiftSpace, viewModel.IgnoreShiftSpace);
    }

    [Fact]
    public void CaptureHotkeyEmpty_IsInvalid_AndSaveDoesNotCallGatewayUpdate()
    {
        // CaptureHotkey가 비어 있으면 저장을 막아야 한다. AppSettings.
        // EnsureDefaults()는 빈 CaptureHotkey를 조용히 기본값으로 되돌리므로
        // (AppSettings.cs 181행 부근), 이 게이트가 없으면 사용자가 의도적으로
        // 비운 값이 다음 로드 때 말없이 기본값으로 되살아나는 것처럼 보인다.
        // 그러니 이 비어있음 규칙은 UX 취향이 아니라 그 조용한 되살림을
        // 막는 장치다.
        var gateway = new FakeSettingsGateway();
        var viewModel = new InteractionSettingsViewModel(gateway);

        viewModel.CaptureHotkey = "   ";

        Assert.False(viewModel.IsValid);

        var callsBefore = gateway.UpdateCallCount;
        viewModel.SaveCommand.Execute(null);

        Assert.Equal(callsBefore, gateway.UpdateCallCount);
    }

    [Fact]
    public void ExitHotkeyEmpty_IsInvalid()
    {
        // ExitHotkey가 비어 있어도 동일하게 막는다.
        var gateway = new FakeSettingsGateway();
        var viewModel = new InteractionSettingsViewModel(gateway);

        viewModel.ExitHotkey = string.Empty;

        Assert.False(viewModel.IsValid);
    }

    [Fact]
    public void CaptureAndExitHotkeysEqual_IsInvalid_AndSaveDoesNotCallGatewayUpdate()
    {
        // 두 단축키가 (서수 비교로) 같으면 무효 처리한다. 저수준 후크
        // 충돌 감지(HotkeyService.ShortcutsConflict)는 Task 10 몫이므로,
        // 여기서는 문자열이 완전히 같은 경우만 잡는 저렴한 부분집합이다.
        var gateway = new FakeSettingsGateway();
        var viewModel = new InteractionSettingsViewModel(gateway);

        viewModel.CaptureHotkey = "F8";
        viewModel.ExitHotkey = "F8";

        Assert.False(viewModel.IsValid);

        var callsBefore = gateway.UpdateCallCount;
        viewModel.SaveCommand.Execute(null);

        Assert.Equal(callsBefore, gateway.UpdateCallCount);
    }
}
