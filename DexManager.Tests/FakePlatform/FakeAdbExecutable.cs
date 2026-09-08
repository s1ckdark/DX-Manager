using System.Text;

namespace DexManager.Tests.FakePlatform;

/// <summary>
/// 실제 <c>adb</c> 자리에 놓이는 셸 스크립트. 호출될 때마다 인자를 파일에
/// 기록하므로, 테스트가 "정리 경로가 기기에 명령을 보냈는가"를 프로덕션
/// 코드에 관측용 구멍을 뚫지 않고도 확인할 수 있다.
/// </summary>
/// <remarks>
/// <para>
/// 등록된 기기에 대해서만 <c>get-state</c>와 <c>getprop ro.serialno</c>에
/// 답한다. 나머지 명령은 성공(exit 0)에 빈 출력으로 처리한다 —
/// overlay 설정 삭제가 여기에 해당한다. 생성자에 넘기는 값은 폰의 하드웨어
/// 일련번호이고, <c>AdbService.GetDeviceIdentity</c>가 여기에 <c>serial:</c>을
/// 붙여 identity를 만든다 — <see cref="IdentityOf"/>가 그 규칙을 그대로 따른다.
/// </para>
/// <para>
/// <c>devices</c>에는 일부러 답하지 않는다. 그래야 회수가 성공하는 유일한
/// 경로가 "호출자가 이 런타임의 serial을 정확히 넘겨준 경우"로 좁혀져,
/// identity뿐 아니라 serial 전달까지 테스트가 함께 고정한다.
/// </para>
/// </remarks>
public sealed class FakeAdbExecutable
{
    private readonly string _root;
    private readonly string _logPath;

    public FakeAdbExecutable(
        string root,
        IReadOnlyDictionary<string, string> hardwareSerialsByTransport,
        IReadOnlyDictionary<string, string> dumpsysWindowOutputByTransport = null)
    {
        _root = root;
        var directory = Path.Combine(root, "fake-adb");
        Directory.CreateDirectory(directory);
        ExecutablePath = Path.Combine(directory, "adb");
        _logPath = Path.Combine(directory, "invocations.log");
        File.WriteAllText(_logPath, string.Empty);

        var script = new StringBuilder();
        script.Append("#!/bin/sh\n");
        script.Append("printf '%s\\n' \"$*\" >> \"")
            .Append(_logPath).Append("\"\n");
        script.Append("case \"$*\" in\n");
        foreach (var device in hardwareSerialsByTransport)
        {
            var prefix = "*\"-s " + device.Key + " \"*";
            script.Append("  ").Append(prefix)
                .Append("\"get-state\"*) printf 'device\\n'; exit 0 ;;\n");
            script.Append("  ").Append(prefix)
                .Append("\"getprop ro.serialno\"*) printf '")
                .Append(device.Value).Append("\\n'; exit 0 ;;\n");
        }
        // dumpsys window의 실제 출력은 등호·따옴표·개행이 뒤섞여 있어 셸
        // case 본문에 직접 박아 넣으면 인용 규칙이 쉽게 깨진다. 그래서
        // 값을 파일에 그대로 적어 두고, 매칭되면 그 파일을 그대로 cat한다 —
        // 내용에 어떤 문자가 와도 안전하다.
        if (dumpsysWindowOutputByTransport != null)
        {
            var dumpIndex = 0;
            foreach (var device in dumpsysWindowOutputByTransport)
            {
                var dumpFilePath = Path.Combine(
                    directory,
                    "dumpsys-window-" + dumpIndex + ".txt");
                File.WriteAllText(dumpFilePath, device.Value ?? string.Empty);
                dumpIndex++;

                var prefix = "*\"-s " + device.Key + " \"*";
                script.Append("  ").Append(prefix)
                    .Append("\"dumpsys window\"*) cat \"")
                    .Append(dumpFilePath).Append("\"; exit 0 ;;\n");
            }
        }
        script.Append("esac\n");
        script.Append("exit 0\n");

        File.WriteAllText(ExecutablePath, script.ToString());
        // 이 솔루션은 macOS 전용이지만, 분석기가 Windows도 후보로 보기 때문에
        // 플랫폼 가드를 명시한다.
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                ExecutablePath,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite |
                UnixFileMode.UserExecute);
        }
    }

    /// <summary>
    /// <c>getprop ro.serialno</c>가 돌려주는 하드웨어 일련번호로부터
    /// <c>AdbService</c>가 만들어 낼 identity를 계산한다.
    /// </summary>
    public static string IdentityOf(string hardwareSerial) =>
        "serial:" + hardwareSerial;

    /// <summary>이 가짜 adb 실행 파일의 전체 경로.</summary>
    public string ExecutablePath { get; }

    /// <summary>지금까지 기록된 호출의 인자 문자열.</summary>
    public IReadOnlyList<string> Invocations => File
        .ReadAllLines(_logPath)
        .Where(line => !string.IsNullOrWhiteSpace(line))
        .ToArray();

    /// <summary>
    /// 기록을 비운다. 호스트를 만드는 동안 경로 탐색이 실행하는
    /// <c>adb version</c> 호출을 준비 단계에서 걷어내는 데 쓴다.
    /// </summary>
    public void ClearInvocations() => File.WriteAllText(_logPath, string.Empty);

    /// <summary>이 실행 파일을 adb로 돌려주는 경로 제공자를 만든다.</summary>
    public FakePathProvider CreatePathProvider() =>
        new FakePathProvider(_root, adbPath: ExecutablePath);
}
