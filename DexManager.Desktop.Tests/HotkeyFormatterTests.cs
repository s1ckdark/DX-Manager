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
    public void Format_AllFourModifiers_IncludesMetaLast()
    {
        var all = KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift | KeyModifiers.Meta;
        Assert.Equal("Control+Alt+Shift+Meta+F8", HotkeyFormatter.Format(Key.F8, all));
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
