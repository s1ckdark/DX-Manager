using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using DexManager.Mac.Hosting;
using DexManager.Utils;

namespace DexManager.Mac;

internal static class Program
{
    internal static string Version =>
        Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "unknown";

    // 신호 처리기(HandleSignal)는 PosixSignalRegistration이 부르는 스레드풀
    // 스레드에서 실행되고, Main의 지역 변수가 아직 만들어지지 않았거나
    // (초기화 도중 신호가 오는 경우) 이미 정리된 뒤(Dispose 이후) 도착할
    // 수도 있다. 그래서 공유 상태를 정적 필드로 둔다 - null이면 아직/이미
    // host가 없다는 뜻이라 조용히 무시한다.
    private static readonly ShutdownCleanupGuard SignalCleanupGuard = new();
    private static CancellationTokenSource _cts;
    private static InteractiveHost _host;
    private static PosixSignalRegistration[] _signalRegistrations;

    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        if (args.Length > 0)
        {
            var firstArg = args[0].ToLowerInvariant();
            if (firstArg is "-h" or "--help")
            {
                PrintHelp();
                return 0;
            }
            if (firstArg is "-v" or "--version")
            {
                Console.WriteLine($"DX Manager for macOS - Version {Version}");
                return 0;
            }
        }

        using var cts = new CancellationTokenSource();
        _cts = cts;

        try
        {
            RegisterSignalHandlers();
            using var host = new InteractiveHost();
            _host = host;

            if (args.Length > 0)
            {
                var cmd = args[0].ToLowerInvariant();
                switch (cmd)
                {
                    case "--diag" or "-d":
                        await host.RunDiagnosticsAsync(cts.Token);
                        return 0;

                    case "--dex" or "-x":
                        if (!await host.StartDexAsync(cts.Token))
                        {
                            return 1;
                        }
                        AnsiConsole.Info("DeX launched. Press Ctrl+C to stop...");
                        try
                        {
                            while (!cts.Token.IsCancellationRequested &&
                                   host.IsDexRunning)
                            {
                                await Task.Delay(500, cts.Token);
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            // Expected on Ctrl+C
                        }
                        if (!cts.Token.IsCancellationRequested)
                        {
                            try
                            {
                                if (await host.WaitForDexCleanupAsync(cts.Token))
                                {
                                    return 0;
                                }
                                if (!cts.Token.IsCancellationRequested)
                                {
                                    return 1;
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                // Continue through the atomic stop/cleanup path.
                            }
                        }
                        return await host.StopDexAsync() ? 0 : 1;

                    case "--stop-dex":
                        return await host.StopDexAsync(
                            cleanupUntrackedOverlay: true,
                            cancellationToken: cts.Token)
                            ? 0
                            : 1;

                    default:
                        AnsiConsole.Warning($"Unknown option: '{args[0]}'. Showing help:");
                        PrintHelp();
                        return 1;
                }
            }

            await host.RunAsync(cts.Token);
            return 0;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.Error($"Fatal error: {ex.Message}");
            return 1;
        }
        finally
        {
            _host = null;
            DisposeSignalHandlers();
        }
    }

    /// <summary>
    /// SIGINT/SIGTERM/SIGHUP을 한곳에 등록한다 - 이제 세 신호 모두
    /// HandleSignalCore 안에서 완전히 같은 정리 경로(가드+예산으로 감싼
    /// host.Shutdown(), 셋이 같은 예산을 공유 - SignalCleanupBudgets 참고)를
    /// 탄다. 유일한 차이는 SIGINT가 추가로 cts를 취소해 협조적으로
    /// 기다리는 코드에 기회를 준다는 것뿐이다(HandleSignalCore 참고).
    ///
    /// <c>PosixSignalRegistration.Create</c> 각각이 던질 수 있다고 보고
    /// (예: 플랫폼 미지원) 하나씩 등록하며 이미 등록된 것들을 리스트에
    /// 모은다 - 뒤엣것이 던지면 앞서 등록된 것들을 여기서 바로 정리하고
    /// 다시 던진다. 그러지 않으면 앞선 등록이 아무 데도 저장되지 못한 채
    /// 새어 나간다(_signalRegistrations는 전부 성공해야 대입되므로).
    /// 호출부(Main)는 이제 이 메서드를 try 안에서 부르므로, 그래도 던지면
    /// "Fatal error" 경로로 정상 보고된다.
    /// </summary>
    private static void RegisterSignalHandlers()
    {
        var registrations = new List<PosixSignalRegistration>(3);
        try
        {
            registrations.Add(PosixSignalRegistration.Create(PosixSignal.SIGINT, HandleSignal));
            registrations.Add(PosixSignalRegistration.Create(PosixSignal.SIGTERM, HandleSignal));
            registrations.Add(PosixSignalRegistration.Create(PosixSignal.SIGHUP, HandleSignal));
            _signalRegistrations = registrations.ToArray();
        }
        catch
        {
            foreach (var registration in registrations)
            {
                registration.Dispose();
            }
            throw;
        }
    }

    private static void DisposeSignalHandlers()
    {
        if (_signalRegistrations == null) return;
        foreach (var registration in _signalRegistrations)
        {
            registration.Dispose();
        }
        _signalRegistrations = null;
    }

    /// <summary>
    /// SIGINT/SIGTERM/SIGHUP 공통 진입점. 세 신호 모두 <b>같은</b> 정리
    /// 경로를 탄다 - SignalCleanupGuard로 감싼 host.Shutdown()을
    /// SignalCleanupBudgets.For(signal) 예산 안에서 동기 실행하고,
    /// ctx.Cancel은 항상 false로 둬 정리가 끝나든 예산을 넘기든 신호의
    /// 기본 동작(프로세스 종료)이 그대로 이어지게 한다. GUI
    /// (DexManager.Desktop.Program.HandleTerminationSignal)와 동일한 설계다.
    ///
    /// <para>
    /// 이전 버전은 SIGINT를 <c>ctx.Cancel = true</c> + <c>cts.Cancel()</c>만
    /// 하는 별도 분기로 두어, 대시보드 루프나 하위 메뉴가 취소를 "관측"하고
    /// 스스로 정리된 종료를 밟기를 기대했다. 코드 리뷰가 실기 pty로 반증했다:
    /// .NET Unix의 <c>StdInReader</c>는 blocking <c>read()</c>가 EINTR로
    /// 깨면 재시도하므로, <c>Console.ReadLine()</c>에 블로킹된 대시보드
    /// 프롬프트는 cts가 취소돼도 반환하지 않는다 - Enter를 눌러야만
    /// 풀린다. <c>ctx.Cancel = true</c>가 기본 종료까지 억제해서, 두 번째
    /// Ctrl+C도 아무 효과가 없었다(옛 <c>Console.CancelKeyPress</c> 방식도
    /// 재현 결과 완전히 동일 - 이 버전이 만든 회귀가 아니라 선재 결함이었지만,
    /// 이 코드가 "정상 종료"라고 주석으로 단언한 것은 사실과 달랐다). 이
    /// 함수가 사실상 SIGTERM/SIGHUP과 유일하게 신뢰할 수 있던 정리 경로를
    /// 만들었던 것과 대비해, 대시보드에서 Ctrl+C는 SIGTERM으로 죽이거나
    /// Enter를 눌러야만 나갈 수 있었다 - 가장 흔한 종료 신호가 개행 없이는
    /// 절대 신뢰할 수 없는 정리 경로였던 셈이다.
    /// </para>
    /// <para>
    /// 지금은 SIGINT도 SIGTERM/SIGHUP과 똑같이 신호 처리기 자신이 직접
    /// 정리한다 - 어떤 코드가 무엇에 블로킹돼 있든 상관없다. 예산은 세
    /// 신호 모두 <see cref="SignalCleanupBudgets.Budget"/>으로 동일하다.
    /// 처음 이 경로를 도입했을 때는 SIGINT에 더 짧은 별도 예산
    /// (Interactive, 5초)을 줬었다 - "화면 앞에 사람이 있으니 짧게
    /// 자른다"는 근거였다. 코드 리뷰가 그 근거를 반증했다: 부하가 걸린
    /// 정리 사슬은 5초를 넘기기 쉽고(8~18초로 관측됨), <c>--dex</c> 실행
    /// 중 Ctrl+C를 누르면 정리가 중간에 잘려 overlay가 그대로 남았다 -
    /// 정확히 이 신호 배선 자체가 막으려던 누수가 가장 흔한 종료 신호에서
    /// 재발한 것이다. 자세한 근거는 SignalCleanupBudgets의 문서 참고.
    /// cts.Cancel()은 그래도 먼저 호출해 둔다 - --dex의 watch 루프
    /// (Task.Delay(500, cts.Token))처럼 실제로 토큰을 관측할 수 있는
    /// 코드가 있다면 정리된 중단 메시지를 낼 최선의 기회를 준다는 뜻이고,
    /// 아무도 관측하지 못해도(대시보드처럼) 해가 되지 않는다 - 진짜 정리는
    /// 뒤따르는 guard 경로가 보장한다. SignalCleanupGuard가 신호 경로들
    /// 사이의 중복 실행을 막고, InteractiveHost._shutdownStarted와
    /// ApplicationHost._shutdownStarted가 신호 경로와 (드물게 실행될 수
    /// 있는) 정상 경로 사이의 중복까지 막는다 - 3중 가드(경쟁 자체는
    /// 남아 있다 - --dex의 main 스레드가 cts.Cancel() 뒤 host.Shutdown()이
    /// 아니라 host.StopDexAsync()를 부르므로 InteractiveHost._shutdownStarted의
    /// 보호를 받지 않지만, DexOrchestrator의 단일 _operationGate 락과
    /// ShutdownAsync의 _shutdownTask 메모이제이션이 그 경쟁에서도 안전을
    /// 보장한다 - 이 구조는 그대로 유지한다).
    /// </para>
    /// </summary>
    private static void HandleSignal(PosixSignalContext ctx)
    {
        var host = _host;
        HandleSignalCore(ctx, _cts, SignalCleanupGuard, host == null ? null : host.Shutdown);
    }

    /// <summary>
    /// HandleSignal의 실제 로직. 정적 필드(_cts/_host) 대신 의존성을
    /// 주입받는 순수한 형태로 분리해 실제 신호 전달 없이 단위 테스트할 수
    /// 있게 한다 - <see cref="PosixSignalContext"/>는 테스트를 위해 공개
    /// 생성자를 제공한다.
    /// </summary>
    internal static void HandleSignalCore(
        PosixSignalContext ctx,
        CancellationTokenSource cts,
        ShutdownCleanupGuard guard,
        Action cleanupAction)
    {
        try
        {
            if (ctx.Signal == PosixSignal.SIGINT)
            {
                // 최선의 노력일 뿐이다 - 실제로 관측하는 코드가 없어도
                // 무해하다. 진짜 정리는 아래 guard 경로가 한다.
                cts?.Cancel();
            }

            if (cleanupAction == null) return;

            BoundedExecutor.RunWithBudget(
                () => guard.TryRunOnce(cleanupAction),
                SignalCleanupBudgets.For(ctx.Signal));
        }
        catch
        {
            // 신호 처리기에서 예외가 새어 나가면 CLR이 fail-fast로 프로세스를
            // 죽인다 - 의도한 "정리하고 기본 종료"가 크래시로 바뀐다. 프로세스는
            // 어차피 곧 종료되므로(세 신호 모두 기본 동작이 종료다) 여기서는
            // 삼키는 편이 크래시보다 낫다.
        }
    }

    private static void PrintHelp()
    {
        AnsiConsole.Header($"DX MANAGER for macOS (.NET 8) - v{Version}");
        Console.WriteLine("Usage:");
        Console.WriteLine("  DXManager.Mac              # Start Interactive Dashboard");
        Console.WriteLine("  DXManager.Mac --dex         # Launch DeX immediately (Ctrl+C to stop)");
        Console.WriteLine("  DXManager.Mac --stop-dex    # Stop active DeX session");
        Console.WriteLine("  DXManager.Mac --diag        # Run system diagnostics");
        Console.WriteLine("  DXManager.Mac --version     # Display version info");
        Console.WriteLine("  DXManager.Mac --help        # Show this help message");
        Console.WriteLine();
        Console.WriteLine("Source developers may use: dotnet run --project DexManager.Mac -- [option]");
        Console.WriteLine();
    }
}
