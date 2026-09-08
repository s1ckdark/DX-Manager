using Avalonia.Input;
using DexManager.Desktop;
using Xunit;

namespace DexManager.Desktop.Tests;

/// <summary>
/// HotkeyFormatter.Format의 Avalonia Key/KeyModifiers → 저장 문자열 매핑을
/// 고정한다. AppSettings.EnsureDefaults의 기본값("F8", "LeftAlt+F8")과 같은
/// "[Modifier+]...+Key" 형식을 지킨다 - 다만 Avalonia의 KeyModifiers는
/// 좌우를 구분하지 않으므로(Task 10 Step-0 합의), 새로 캡처한 조합은 항상
/// 일반 토큰(Control/Alt/Shift/Meta)으로 저장한다. 기본값의 "LeftAlt"는
/// 사용자가 다시 캡처하기 전까지만 화면에 남는다 - 이건 트레이드오프로
/// 문서화된 동작이지 버그가 아니다.
///
/// 순수 정적 함수라 Avalonia Application/윈도우 없이도 테스트할 수 있다 -
/// 실제 키 이벤트 전달 경로(SettingsWindow.axaml.cs의 KeyDown 배선)는
/// 여기서 다루지 않는다 - 그건 실기 검증 대상이다.
/// </summary>
public class HotkeyFormatterTests
{
    [Fact]
    public void Format_PlainKey_ReturnsKeyNameOnly()
    {
        Assert.Equal("F8", HotkeyFormatter.Format(Key.F8, KeyModifiers.None));
    }

    [Fact]
    public void Format_SingleModifier_PrependsGenericModifierToken()
    {
        Assert.Equal("Alt+F8", HotkeyFormatter.Format(Key.F8, KeyModifiers.Alt));
    }

    [Fact]
    public void Format_MultipleModifiers_UsesFixedDeterministicOrder()
    {
        // Control | Alt | Shift 비트를 어떤 순서로 조합해 넘기든(플래그
        // enum이라 조합 순서 자체는 없지만, 호출부의 다양한 실제 눌림
        // 순서를 흉내낸다) 출력 순서는 항상 Control, Alt, Shift, Meta다.
        var combined = KeyModifiers.Shift | KeyModifiers.Control | KeyModifiers.Alt;
        Assert.Equal("Control+Alt+Shift+F8", HotkeyFormatter.Format(Key.F8, combined));
    }

    [Fact]
    public void Format_AllFourModifiers_EmitsTheCommandModifierAsWinLast()
    {
        var all = KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift | KeyModifiers.Meta;
        Assert.Equal("Control+Alt+Shift+Win+F8", HotkeyFormatter.Format(Key.F8, all));
    }

    [Fact]
    public void Format_CommandKeyAlone_EmitsWinNotMeta()
    {
        // KeyShortcut.TryParse(DexManager/Services/HotkeyService.cs)가 받는
        // 수정자 어휘에 "Meta"는 없다 - Meta 토큰 하나가 단축키 "전체"의
        // 파싱을 실패시키고, HotkeyService.ParseShortcuts는 조용히 시드
        // 기본값으로 되돌아간다. macOS의 Command 키는 그 파서가 아는
        // Meta/Super 계열 이름인 "Win"으로 써야 왕복이 성립한다.
        Assert.Equal("Win+F8", HotkeyFormatter.Format(Key.F8, KeyModifiers.Meta));
    }

    [Fact]
    public void Format_NeverEmitsTheMetaTokenTheParserRejects()
    {
        var all = KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift | KeyModifiers.Meta;
        Assert.DoesNotContain("Meta", HotkeyFormatter.Format(Key.F8, all), System.StringComparison.Ordinal);
    }

    // --- F-6: 단축키 입력란에서 키보드로 빠져나갈 수 있어야 한다 ---

    [Theory]
    [InlineData(Key.Tab)]
    [InlineData(Key.Escape)]
    public void ShouldPassThroughForNavigation_LetsFocusAndDismissKeysOut(Key key)
    {
        // Tunnel 핸들러가 모든 키를 e.Handled = true로 삼키면 키보드
        // 사용자는 이 입력란에서 나갈 수도, 창을 닫을 수도 없다
        // (Shift+Tab도 Key.Tab으로 온다).
        Assert.True(HotkeyFormatter.ShouldPassThroughForNavigation(key));
    }

    [Theory]
    [InlineData(Key.F8)]
    [InlineData(Key.A)]
    [InlineData(Key.Enter)]
    [InlineData(Key.Space)]
    [InlineData(Key.Back)]
    public void ShouldPassThroughForNavigation_StillCapturesEveryOtherKey(Key key)
    {
        Assert.False(HotkeyFormatter.ShouldPassThroughForNavigation(key));
    }

    [Fact]
    public void Format_LetterKeyWithControl_ReturnsControlPlusLetter()
    {
        Assert.Equal("Control+A", HotkeyFormatter.Format(Key.A, KeyModifiers.Control));
    }

    [Theory]
    [InlineData(Key.LeftAlt)]
    [InlineData(Key.RightAlt)]
    [InlineData(Key.LeftCtrl)]
    [InlineData(Key.RightCtrl)]
    [InlineData(Key.LeftShift)]
    [InlineData(Key.RightShift)]
    [InlineData(Key.LWin)]
    [InlineData(Key.RWin)]
    public void Format_ModifierKeyPressedAlone_ReturnsNull(Key modifierKey)
    {
        // 대응하는 KeyModifiers 플래그가 함께 실려 오더라도(Avalonia가
        // 그렇게 보고한다) 순수 수정자 눌림은 단축키를 만들지 않는다.
        Assert.Null(HotkeyFormatter.Format(modifierKey, KeyModifiers.Alt));
    }

    [Fact]
    public void Format_KeyNone_ReturnsNull()
    {
        Assert.Null(HotkeyFormatter.Format(Key.None, KeyModifiers.None));
    }
}
