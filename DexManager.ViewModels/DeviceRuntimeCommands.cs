using DexManager.Hosting;
using DexManager.Models;

namespace DexManager.ViewModels;

/// <summary>
/// <see cref="IDeviceRuntimeCommands"/>의 실제 구현. 코디네이터에서 이
/// 기기의 런타임을 얻어 명령을 넘긴다. 런타임을 직접 만들지 않는다 —
/// 중복 생성 방지는 코디네이터의 책임이다.
/// </summary>
public sealed class DeviceRuntimeCommands : IDeviceRuntimeCommands
{
    private readonly ApplicationHost _host;

    public DeviceRuntimeCommands(ApplicationHost host)
        => _host = host ?? throw new ArgumentNullException(nameof(host));

    public async Task<bool> StartDexAsync(
        string identity,
        string serial,
        CancellationToken cancellationToken)
    {
        // DeviceRuntimeCoordinator.GetOrCreate는 이 기기의 첫 런타임을
        // 만들 때 자물쇠를 쥔 채 DeviceRuntimeServiceFactory.Create()의
        // scrcpy 버전 프로브를 기다린다 — 최대 ~3초. 이 메서드는 UI 스레드의
        // 명령 핸들러가 부르므로, await 이전에 동기로 실행되는 부분이 있으면
        // 그만큼 창이 얼어붙는다. Task.Run으로 스레드 풀에 넘겨 UI 스레드는
        // 곧바로 반환된 Task를 기다리게 한다.
        var runtime = await Task.Run(
            () => _host.RuntimeCoordinator.GetOrCreate(identity, serial),
            cancellationToken).ConfigureAwait(false);

        return await runtime.Dex
            .StartAsync(serial, identity, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> StopDexAsync(
        string identity,
        string serial,
        CancellationToken cancellationToken)
    {
        // TryGet 자체는 딕셔너리 조회뿐이지만, GetOrCreate와 같은 자물쇠를
        // 공유한다. 다른 기기의 GetOrCreate가 scrcpy 프로브를 기다리는
        // 동안에는 이 조회도 그 자물쇠에 걸려 몇 초씩 막힐 수 있다 — 그래서
        // 이것도 스레드 풀로 넘긴다.
        var (found, runtime) = await Task.Run(
            () =>
            {
                var ok = _host.RuntimeCoordinator.TryGet(identity, out var rt);
                return (ok, rt);
            },
            cancellationToken).ConfigureAwait(false);

        // 아직 아무것도 시작하지 않았으면 빈 런타임을 만들지 않는다.
        if (!found) return true;

        return await runtime.Dex.StopOrConfirmCleanupAsync().ConfigureAwait(false);
    }

    public void StartSingleWindow(
        string identity,
        string serial,
        int slot,
        string appPackage)
    {
        var runtime = _host.RuntimeCoordinator.GetOrCreate(identity, serial);
        var settings = _host.Settings;

        runtime.SingleWindows.Start(
            slot,
            new SingleWindowSlotSettings
            {
                Slot = slot,
                Width = settings.VirtualDisplay.Width,
                Height = settings.VirtualDisplay.Height,
                Dpi = settings.VirtualDisplay.Dpi,
                BitRate = settings.Scrcpy.BitRate,
                MaxFps = settings.Scrcpy.MaxFps,
                StayAwake = settings.Scrcpy.StayAwake,
                TurnScreenOff = settings.Scrcpy.TurnScreenOff,
                StartAppPackage = appPackage,
                AdditionalArguments = settings.Scrcpy.AdditionalArguments
            },
            serial);
    }

    public void StopSingleWindow(string identity, int slot)
    {
        if (!_host.RuntimeCoordinator.TryGet(identity, out var runtime)) return;
        runtime.SingleWindows.Stop(slot);
    }
}
