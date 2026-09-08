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

        RegisterSignalHandlers();
        try
        {
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
    /// SIGINT/SIGTERM/SIGHUP을 한곳에 등록한다. GUI(DexManager.Desktop.Program)와
    /// 달리 SIGINT까지 여기서 등록하는 이유는, 기존에 <c>Console.CancelKeyPress</c>가
    /// 하던 일(협조적 취소 - 대시보드 루프나 --dex의 watch 루프가 cts를 보고
    /// 자기 흐름대로 정리한 뒤 정상 반환)을 그대로 승계하면서 SIGTERM/SIGHUP과
    /// 등록 지점을 하나로 합쳐 "두 메커니즘이 같은 신호를 다르게 본다"는
    /// 혼란을 없애기 위해서다. HandleSignal 안에서 SIGINT는 다른 두 신호와
    /// 완전히 다른 분기를 탄다 - 이중 등록도, 이중 정리도 없다.
    /// </summary>
    private static void RegisterSignalHandlers()
    {
        _signalRegistrations = new[]
        {
            PosixSignalRegistration.Create(PosixSignal.SIGINT, HandleSignal),
            PosixSignalRegistration.Create(PosixSignal.SIGTERM, HandleSignal),
            PosixSignalRegistration.Create(PosixSignal.SIGHUP, HandleSignal),
        };
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
    /// SIGINT/SIGTERM/SIGHUP 공통 진입점이지만 SIGINT는 근본적으로 다르게
    /// 다룬다 - 하나로 합쳐 등록한 이유(RegisterSignalHandlers 참고)가
    /// "같은 취급"을 뜻하지는 않는다.
    ///
    /// <b>SIGINT(Ctrl+C)</b>: 예전 <c>Console.CancelKeyPress</c>와 동일하게
    /// <c>ctx.Cancel = true</c>로 기본 종료(즉시 프로세스 종료)를 막고
    /// <c>cts.Cancel()</c>만 한다. 그러면 Main의 대시보드 루프 또는 --dex의
    /// watch 루프가 취소를 알아채고 자신의 정리된 종료 흐름
    /// (WaitForDexCleanupAsync → StopDexAsync, 또는 대시보드라면 host.RunAsync
    /// 끝의 ShutdownAsync)을 끝까지 돌린 뒤 정상적으로 반환해 `using host`
    /// Dispose까지 이어진다. 이 흐름이 SIGTERM/SIGHUP 쪽 정리(host.Shutdown()을
    /// 직접, 예산 안에서 부르는 것)보다 세밀하므로 그대로 둔다 - 여기서
    /// host.Shutdown()을 같이 부르면 정상 흐름과 경쟁해 이중 정리가 된다.
    /// 그래서 SIGINT는 SignalCleanupGuard/host.Shutdown 경로를 절대 타지 않는다
    /// - 이것이 "SIGINT는 두 번 정리하지 않는다"를 만족시키는 방법이다.
    ///
    /// <b>SIGTERM/SIGHUP</b>: 협조할 루프가 없다 - launchd, `pkill -TERM`,
    /// 제어 터미널 끊김 어느 쪽도 cts를 지켜보고 있지 않는다. ctx.Cancel을
    /// false로 두면(기본 종료 진행) 관리 코드가 `using host`까지 unwind될
    /// 기회 자체가 없으므로, 여기서 직접 SignalCleanupGuard로 감싸 정확히
    /// 한 번, 예산(SignalCleanupBudgets) 안에서 동기적으로 정리한다.
    /// InteractiveHost.ShutdownAsync 자신도 내부 Interlocked 가드를 갖고
    /// 있어(중복 호출은 이미 안전하다) 이 바깥 가드가 없어도 깨지지는
    /// 않지만, GUI와 같은 패턴을 유지해 신호 두 개가 겹치는 경쟁(예:
    /// SIGTERM 직후 SIGHUP)을 이 호출부에서도 명시적으로 방어하고, 이
    /// 파일만 보고도 "정확히 한 번"을 테스트할 수 있는 seam을 남긴다.
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
                ctx.Cancel = true;
                cts?.Cancel();
                return;
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
            // 어차피 곧 종료되므로(SIGTERM/SIGHUP 기본 동작, 또는 SIGINT는
            // cts.Cancel로 이미 정상 종료가 진행 중이므로) 여기서는 삼키는
            // 편이 크래시보다 낫다.
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
