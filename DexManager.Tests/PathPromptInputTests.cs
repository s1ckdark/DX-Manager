using DexManager.Mac.Hosting;
using Xunit;

namespace DexManager.Tests;

/// <summary>
/// InteractiveHost.ManageSettings()의 경로 프롬프트(8. Scrcpy Path /
/// 9. ADB Path)가 공유하는 입력 해석 규칙. 코드 리뷰가 지목한 결함
/// (mac-fix-review-report.md §4 Minor)을 고정한다: 그냥 Enter는 값을
/// 지우지 않는다 - GUI 텍스트 상자와 달리 콘솔 입력은 항상 빈 줄에서
/// 시작하므로, 빈 입력을 "비움"으로 해석하면 값을 보러 들어왔다가
/// 취소하려는 사용자의 Manual 설정이 조용히 삭제된다.
/// </summary>
public class PathPromptInputTests
{
    [Fact]
    public void Resolve_BlankInputOverNonEmptyBaseline_KeepsCurrentValueUnchanged()
    {
        var result = PathPromptInput.Resolve(currentValue: "/usr/local/bin/adb", enteredInput: "");

        Assert.False(result.Changed);
        Assert.Equal("/usr/local/bin/adb", result.Value);
    }

    [Fact]
    public void Resolve_WhitespaceOnlyInputOverNonEmptyBaseline_KeepsCurrentValueUnchanged()
    {
        var result = PathPromptInput.Resolve(currentValue: "/usr/local/bin/adb", enteredInput: "   ");

        Assert.False(result.Changed);
        Assert.Equal("/usr/local/bin/adb", result.Value);
    }

    [Fact]
    public void Resolve_NullInputOverNonEmptyBaseline_KeepsCurrentValueUnchanged()
    {
        var result = PathPromptInput.Resolve(currentValue: "/usr/local/bin/adb", enteredInput: null);

        Assert.False(result.Changed);
        Assert.Equal("/usr/local/bin/adb", result.Value);
    }

    [Fact]
    public void Resolve_ClearTokenOverNonEmptyBaseline_ClearsAndMarksChanged()
    {
        var result = PathPromptInput.Resolve(currentValue: "/usr/local/bin/adb", enteredInput: "-");

        Assert.True(result.Changed);
        Assert.Equal("", result.Value);
    }

    [Fact]
    public void Resolve_ClearTokenOverAlreadyEmptyBaseline_IsNotChanged()
    {
        // 이미 자동 감지 상태에서 "-"를 다시 입력해도 모드를 뒤집으면 안
        // 된다 - 재저장으로 우연히 바뀌지 않는다는 규칙의 연장.
        var result = PathPromptInput.Resolve(currentValue: "", enteredInput: "-");

        Assert.False(result.Changed);
        Assert.Equal("", result.Value);
    }

    [Fact]
    public void Resolve_ClearTokenWithSurroundingWhitespace_IsStillRecognized()
    {
        var result = PathPromptInput.Resolve(currentValue: "/usr/local/bin/adb", enteredInput: "  -  ");

        Assert.True(result.Changed);
        Assert.Equal("", result.Value);
    }

    [Fact]
    public void Resolve_NewNonEmptyValue_TrimsAndMarksChanged()
    {
        var result = PathPromptInput.Resolve(currentValue: "", enteredInput: "  /opt/homebrew/bin/adb  ");

        Assert.True(result.Changed);
        Assert.Equal("/opt/homebrew/bin/adb", result.Value);
    }

    [Fact]
    public void Resolve_ReenteringSameValue_IsNotChanged()
    {
        var result = PathPromptInput.Resolve(
            currentValue: "/opt/homebrew/bin/adb",
            enteredInput: "/opt/homebrew/bin/adb");

        Assert.False(result.Changed);
        Assert.Equal("/opt/homebrew/bin/adb", result.Value);
    }

    [Fact]
    public void Resolve_NullBaselineTreatedAsEmpty()
    {
        var result = PathPromptInput.Resolve(currentValue: null, enteredInput: "");

        Assert.False(result.Changed);
        Assert.Equal("", result.Value);
    }

    [Fact]
    public void Resolve_LiteralValueEqualToClearTokenAfterTrimIsAlwaysTreatedAsClear()
    {
        // "-"라는 실제 경로를 저장하는 것은 불가능해진다(의도된 트레이드오프
        // - 유효한 실행파일 경로가 될 수 없는 값을 예약 토큰으로 쓴다).
        var result = PathPromptInput.Resolve(currentValue: "/usr/local/bin/adb", enteredInput: "-");

        Assert.NotEqual("-", result.Value);
    }
}
