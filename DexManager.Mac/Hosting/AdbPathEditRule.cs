using System;
using DexManager.Models;

namespace DexManager.Mac.Hosting;

/// <summary>
/// ManageSettings()의 "9. ADB Path" 편집 규칙. GUI의
/// PathsSettingsViewModel.Save()와 같은 의미론을 유지한다:
///
/// <list type="bullet">
/// <item>입력은 트림한 뒤 현재 저장된 값과 비교한다 - 붙여넣기로 흔히
/// 섞여 들어오는 앞뒤 공백 하나 때문에 "편집됨"으로 잘못 잡히지
/// 않는다.</item>
/// <item>트림한 값이 저장된 값과 실제로 다를 때만 "바뀜"으로 본다 - 같은
/// 값을 다시 제출해도(재저장) 모드를 건드리지 않는다.</item>
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
        var baseline = currentAdbPath ?? string.Empty;
        var trimmed = (enteredInput ?? string.Empty).Trim();
        var changed = !string.Equals(trimmed, baseline, StringComparison.Ordinal);

        AdbSelectionMode? newMode = changed
            ? (string.IsNullOrWhiteSpace(trimmed) ? AdbSelectionMode.Auto : AdbSelectionMode.Manual)
            : null;

        return new Result(trimmed, changed, newMode);
    }
}
