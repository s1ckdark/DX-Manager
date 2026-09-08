using System.Collections.Generic;
using Avalonia.Input;

namespace DexManager.Desktop;

/// <summary>
/// 설정 창의 단축키 입력란이 캡처한 Avalonia Key/KeyModifiers를
/// AppSettings.KeyMappings가 저장하는 "[Modifier+]...+Key" 형식 문자열로
/// 바꾼다(예: "F8", "LeftAlt+F8"). Avalonia 타입을 직접 다루므로 Avalonia를
/// 참조하지 않는 DexManager.ViewModels가 아니라 여기(DexManager.Desktop)에
/// 둔다 - Task 7의 ThemeApplier와 같은 배치 원칙이다.
///
/// Task 10 Step-0에서 확인한 트레이드오프: Avalonia의 KeyModifiers는
/// Alt/Control/Shift/Meta만 구분하고 좌우를 구분하지 않는다(왼쪽/오른쪽
/// Alt를 구분하려면 별도의 원시 키다운 상태 추적이 필요한데, 그 구분을
/// 소비하는 곳이 현재 macOS에는 전혀 없다 - MacKeyboardService는
/// Start/Stop/ReloadConfiguration이 빈 메서드인 완전한 스텁이다).
/// 그래서 이 매핑은 항상 일반 토큰(Control/Alt/Shift/Win)만 만든다.
/// AppSettings.cs의 시드 기본값 ExitHotkey="LeftAlt+F8"은 사용자가 이
/// 필드를 다시 캡처하기 전까지만 "LeftAlt"로 남고, 캡처 후에는 "Alt+F8"이
/// 된다 - 의도된 동작이다.
///
/// macOS의 Command 키(Avalonia에서 <c>KeyModifiers.Meta</c>)는 "Meta"가
/// 아니라 "Win"으로 쓴다. 이 문자열을 실제로 읽는 유일한 파서인
/// <c>KeyShortcut.TryParse</c>(DexManager/Services/HotkeyService.cs)가 아는
/// 수정자 어휘는 Alt/Ctrl/Control/Shift/Windows/Win(및 좌우 변형)뿐이고,
/// "Meta"는 그 목록에 없다 - 인식되지 않은 토큰은 메인 키 후보로 떨어져
/// <c>Enum.TryParse&lt;Keys&gt;("Meta")</c>에서 실패하고, TryParse가
/// "단축키 전체"에 대해 false를 돌려준다. 그러면 ParseShortcuts가 조용히
/// 시드 기본값으로 되돌아가고, WinForms 설정 창은 같은 값을 아예 거부한다.
/// "Win"은 그 파서에서 Meta/Super/Command 계열 수정자를 가리키는 이름
/// (AnyWindows)이므로, 이 토큰이라야 저장한 값이 실제로 다시 읽힌다.
/// </summary>
public static class HotkeyFormatter
{
    // 수정자 키 자체가 눌렸을 때(예: Alt만 누름) Avalonia는 그 키 자체를
    // Key.LeftAlt/RightAlt 등으로 보고한다. 이런 "수정자만 눌림"은
    // 단축키가 아니다 - 뒤따라올 실제 키를 기다려야 한다.
    private static readonly HashSet<Key> ModifierOnlyKeys = new()
    {
        Key.LeftAlt, Key.RightAlt,
        Key.LeftCtrl, Key.RightCtrl,
        Key.LeftShift, Key.RightShift,
        Key.LWin, Key.RWin,
    };

    /// <summary>
    /// key/modifiers 조합을 저장용 문자열로 바꾼다. key가 Key.None이거나
    /// 수정자 키 자체면(순수 수정자 눌림) null을 반환한다 - 호출부는 이때
    /// 바인딩된 값을 갱신하지 않아야 한다.
    /// </summary>
    public static string Format(Key key, KeyModifiers modifiers)
    {
        if (key == Key.None || ModifierOnlyKeys.Contains(key))
            return null;

        var parts = new List<string>(5);
        if ((modifiers & KeyModifiers.Control) != 0) parts.Add("Control");
        if ((modifiers & KeyModifiers.Alt) != 0) parts.Add("Alt");
        if ((modifiers & KeyModifiers.Shift) != 0) parts.Add("Shift");
        // ⌘(Meta)는 파서가 아는 이름인 "Win"으로 쓴다 - 위 클래스 문서 참고.
        if ((modifiers & KeyModifiers.Meta) != 0) parts.Add("Win");
        parts.Add(key.ToString());

        return string.Join("+", parts);
    }

    /// <summary>
    /// 이 키는 단축키로 캡처하지 말고 그대로 흘려보내야 하는가.
    ///
    /// 단축키 입력란의 Tunnel 핸들러는 원문 텍스트 입력을 막기 위해 모든
    /// 키를 <c>e.Handled = true</c>로 삼켰는데, 그러면 Tab/Shift+Tab으로
    /// 포커스를 옮길 수도, Escape로 창을 닫을 수도 없다 - 마우스 없이는
    /// 빠져나올 수 없는 키보드 트랩이다(Shift+Tab도 <see cref="Key.Tab"/>으로
    /// 온다). 표준 단축키 레코더도 정확히 이 이유로 Tab을 통과시킨다.
    /// 나머지 키는 지금까지처럼 전부 캡처한다.
    /// </summary>
    public static bool ShouldPassThroughForNavigation(Key key)
        => key == Key.Tab || key == Key.Escape;
}
