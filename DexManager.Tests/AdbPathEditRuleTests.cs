using DexManager.Mac.Hosting;
using DexManager.Models;
using Xunit;

namespace DexManager.Tests;

/// <summary>
/// InteractiveHost.ManageSettings()의 "9. ADB Path" 편집 규칙
/// (AdbPathEditRule)을 GUI의 PathsSettingsViewModel.Save()와 같은
/// 의미론으로 고정한다. 실기 조사 §12가 지목한 결함(8/9가 switch에
/// 없어 "저장됨"을 거짓 보고)과, PR #5가 고친 GUI의 무동작 ADB 칸(값만
/// 쓰고 모드를 안 바꿔 PathService가 절대 읽지 않는 문제)을 TUI 쪽에서도
/// 막는다.
/// </summary>
public class AdbPathEditRuleTests
{
    [Fact]
    public void Apply_NonEmptyPathEnteredOverEmptyBaseline_SetsManualModeAndMarksChanged()
    {
        var result = AdbPathEditRule.Apply(currentAdbPath: "", enteredInput: "/usr/local/bin/adb");

        Assert.True(result.Changed);
        Assert.Equal("/usr/local/bin/adb", result.TrimmedPath);
        Assert.Equal(AdbSelectionMode.Manual, result.NewMode);
    }

    [Fact]
    public void Apply_ClearedOverNonEmptyBaseline_SetsAutoModeAndMarksChanged()
    {
        var result = AdbPathEditRule.Apply(currentAdbPath: "/usr/local/bin/adb", enteredInput: "");

        Assert.True(result.Changed);
        Assert.Equal("", result.TrimmedPath);
        Assert.Equal(AdbSelectionMode.Auto, result.NewMode);
    }

    [Fact]
    public void Apply_ReenteringTheSameValue_DoesNotFlipModeAndIsNotChanged()
    {
        var result = AdbPathEditRule.Apply(
            currentAdbPath: "/usr/local/bin/adb",
            enteredInput: "/usr/local/bin/adb");

        Assert.False(result.Changed);
        Assert.Null(result.NewMode);
        Assert.Equal("/usr/local/bin/adb", result.TrimmedPath);
    }

    [Fact]
    public void Apply_ReenteringSameValueWithSurroundingWhitespace_TrimsAndIsNotChanged()
    {
        // 붙여넣기로 흔히 섞여 들어오는 앞뒤 공백 - 트림 후 비교해야
        // "편집됨"으로 잘못 잡혀 모드가 뒤집히지 않는다.
        var result = AdbPathEditRule.Apply(
            currentAdbPath: "/usr/local/bin/adb",
            enteredInput: "  /usr/local/bin/adb  ");

        Assert.False(result.Changed);
        Assert.Null(result.NewMode);
        Assert.Equal("/usr/local/bin/adb", result.TrimmedPath);
    }

    [Fact]
    public void Apply_NewValueHasSurroundingWhitespace_TrimsBeforeStoring()
    {
        var result = AdbPathEditRule.Apply(
            currentAdbPath: "",
            enteredInput: "  /opt/homebrew/bin/adb  ");

        Assert.True(result.Changed);
        Assert.Equal("/opt/homebrew/bin/adb", result.TrimmedPath);
        Assert.Equal(AdbSelectionMode.Manual, result.NewMode);
    }

    [Fact]
    public void Apply_BothBaselineAndInputEmpty_IsNotChanged()
    {
        var result = AdbPathEditRule.Apply(currentAdbPath: "", enteredInput: "   ");

        Assert.False(result.Changed);
        Assert.Null(result.NewMode);
        Assert.Equal("", result.TrimmedPath);
    }

    [Fact]
    public void Apply_NullBaselineAndNullInput_IsNotChanged()
    {
        var result = AdbPathEditRule.Apply(currentAdbPath: null, enteredInput: null);

        Assert.False(result.Changed);
        Assert.Null(result.NewMode);
        Assert.Equal("", result.TrimmedPath);
    }

    [Fact]
    public void Apply_DifferentNonEmptyPathOverExistingManualPath_StaysManualAndMarksChanged()
    {
        var result = AdbPathEditRule.Apply(
            currentAdbPath: "/usr/local/bin/adb",
            enteredInput: "/opt/homebrew/bin/adb");

        Assert.True(result.Changed);
        Assert.Equal("/opt/homebrew/bin/adb", result.TrimmedPath);
        Assert.Equal(AdbSelectionMode.Manual, result.NewMode);
    }
}
