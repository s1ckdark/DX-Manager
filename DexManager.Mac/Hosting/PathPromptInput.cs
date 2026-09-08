using System;

namespace DexManager.Mac.Hosting;

/// <summary>
/// ManageSettings()의 경로 편집 프롬프트(8. Scrcpy Path / 9. ADB Path)가
/// 공유하는 입력 해석 규칙.
///
/// GUI의 텍스트 상자는 현재 값으로 미리 채워진 채 열리므로 "아무것도
/// 편집하지 않고 저장"이 곧 "값 유지"다. 콘솔의 <c>Console.ReadLine()</c>은
/// 항상 빈 줄에서 시작하므로, 그냥 Enter를 "비움(편집)"으로 해석하면 그
/// 미묘한 차이가 파괴적으로 뒤집힌다 - 사용자가 값을 보러 들어왔다가
/// 취소하려고 Enter를 누르면 설정이 조용히 지워진다(코드 리뷰 지적,
/// mac-fix-review-report.md §4 Minor).
///
/// 그래서 여기서는 규칙을 셋으로 나눈다:
/// <list type="bullet">
/// <item>빈 입력(공백만 포함해도) → <b>유지</b>. Changed=false.</item>
/// <item><see cref="ClearToken"/>("-")을 입력 → <b>명시적으로 지움</b>.
/// Changed는 기존 값이 이미 비어 있지 않았을 때만 true.</item>
/// <item>그 외 → 트림한 값을 새 값으로 쓴다.</item>
/// </list>
/// Enter는 절대 파괴적이지 않다 - 지우려면 명시적인 토큰이 필요하다.
/// </summary>
internal static class PathPromptInput
{
    internal const string ClearToken = "-";

    internal readonly record struct Result(string Value, bool Changed);

    internal static Result Resolve(string currentValue, string enteredInput)
    {
        var baseline = currentValue ?? string.Empty;
        var trimmed = (enteredInput ?? string.Empty).Trim();

        if (trimmed.Length == 0)
        {
            return new Result(baseline, false);
        }

        var resolved = string.Equals(trimmed, ClearToken, StringComparison.Ordinal)
            ? string.Empty
            : trimmed;
        var changed = !string.Equals(resolved, baseline, StringComparison.Ordinal);
        return new Result(resolved, changed);
    }
}
