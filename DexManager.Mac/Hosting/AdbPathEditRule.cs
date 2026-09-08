using DexManager.Models;

namespace DexManager.Mac.Hosting;

/// <summary>
/// ManageSettings()의 "9. ADB Path" 편집 규칙. GUI의
/// PathsSettingsViewModel.Save()와 같은 의미론을 유지한다:
///
/// <list type="bullet">
/// <item>입력 해석 자체(빈 입력=유지, "-"=명시적으로 지움, 그 외=트림한
/// 새 값)는 <see cref="PathPromptInput"/>이 8번(Scrcpy Path)과 공유한다.</item>
/// <item>값이 실제로 바뀌었을 때만(재저장으로 같은 값을 다시 제출해도
/// 아님) 모드를 건드린다.</item>
/// <item>바뀌었고 비어 있지 않으면 <see cref="AdbSelectionMode.Manual"/>로,
/// 바뀌었고 비었으면(자동 감지로 되돌리기) <see cref="AdbSelectionMode.Auto"/>로
/// 전환한다. 바뀌지 않았으면 모드는 그대로 둔다(<c>NewMode</c>가
/// <c>null</c>).</item>
/// </list>
/// </summary>
internal static class AdbPathEditRule
{
    internal readonly record struct Result(string TrimmedPath, bool Changed, AdbSelectionMode? NewMode);

    internal static Result Apply(string currentAdbPath, string enteredInput)
    {
        var resolved = PathPromptInput.Resolve(currentAdbPath, enteredInput);

        AdbSelectionMode? newMode = resolved.Changed
            ? (string.IsNullOrEmpty(resolved.Value) ? AdbSelectionMode.Auto : AdbSelectionMode.Manual)
            : null;

        return new Result(resolved.Value, resolved.Changed, newMode);
    }
}
