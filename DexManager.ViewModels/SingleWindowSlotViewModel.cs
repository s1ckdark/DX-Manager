using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DexManager.Models;

namespace DexManager.ViewModels;

/// <summary>
/// 단일창 슬롯 하나의 상태와 명령. 슬롯은 1~3 고정이다 —
/// <c>SingleWindowService.Start</c>가 범위 밖 값을 거부한다.
/// </summary>
/// <remarks>
/// 이 ViewModel은 레지스트리를 직접 구독하지 않는다. 부모
/// <see cref="DeviceViewModel"/>이 자기 구독에서 받은 세션 스냅샷을
/// <see cref="ApplyRuntime"/>으로 넘긴다. 구독 지점을 하나로 유지해
/// 해제 누락을 없앤다.
/// </remarks>
public sealed partial class SingleWindowSlotViewModel : ObservableObject, IDisposable
{
    private readonly string _identity;
    private readonly Func<string> _serialProvider;
    private readonly IDeviceRuntimeCommands _commands;
    private bool _disposed;

    public SingleWindowSlotViewModel(
        int slot,
        string identity,
        Func<string> serialProvider,
        IDeviceRuntimeCommands commands)
    {
        if (slot < 1 || slot > 3)
        {
            throw new ArgumentOutOfRangeException(
                nameof(slot),
                slot,
                "Single window slots are 1 through 3.");
        }

        Slot = slot;
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
        _serialProvider = serialProvider
            ?? throw new ArgumentNullException(nameof(serialProvider));
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
    }

    public int Slot { get; }

    /// <summary>화면에 표시할 슬롯 이름.</summary>
    public string Title => $"Window #{Slot}";

    [ObservableProperty]
    private bool _isRunning;

    /// <summary>실행할 Android 앱 패키지. 비어 있으면 시작할 수 없다.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private string _appPackage = string.Empty;

    /// <summary>
    /// 시작 명령이 진행 중인지 여부. GetOrCreate의 scrcpy 프로브를
    /// 기다리는 동안 중복 클릭으로 두 번째 시작이 겹치는 것을 막는다.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private bool _isBusy;

    /// <summary>마지막 명령의 결과 문구. 실패해도 예외를 올리지 않는다.</summary>
    [ObservableProperty]
    private string _lastCommandMessage = string.Empty;

    /// <summary>
    /// 부모가 넘긴 세션 스냅샷에서 이 슬롯의 상태만 골라 반영한다.
    /// 세션이 없으면 중지 상태로 본다.
    /// </summary>
    public void ApplyRuntime(DeviceRuntimeSessionSnapshot session)
    {
        if (_disposed) return;

        var slot = session?.SingleWindows?
            .FirstOrDefault(s => s != null && s.Slot == Slot);
        IsRunning = slot?.IsRunning == true;
    }

    private bool CanStart()
        => !_disposed && !IsBusy && !string.IsNullOrWhiteSpace(AppPackage);

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        IsBusy = true;
        try
        {
            // IDeviceRuntimeCommands.StartSingleWindow는 동기다 —
            // DeviceRuntimeCoordinator.GetOrCreate가 이 기기의 첫 런타임을
            // 만들 때 자물쇠를 쥔 채 scrcpy 버전 프로브를 기다린다(최대
            // ~3초). 이 명령은 UI 스레드의 핸들러가 부르므로, 그대로
            // 부르면 창이 얼어붙는다. DeX 시작 명령과 같은 이유로
            // Task.Run으로 스레드 풀에 넘긴다. serial은 호출 시점에
            // 읽는다 — USB에서 무선으로 전환되면 생성 시점 값은 이미
            // 죽은 transport다.
            var serial = _serialProvider();
            var appPackage = AppPackage.Trim();
            await Task.Run(() => _commands.StartSingleWindow(
                _identity,
                serial,
                Slot,
                appPackage));
            LastCommandMessage = string.Empty;
        }
        catch (Exception ex)
        {
            // 명령 실패가 창을 죽이면 안 된다. 슬롯에 문구로만 남긴다.
            LastCommandMessage = $"Window #{Slot} start failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Stop()
    {
        try
        {
            _commands.StopSingleWindow(_identity, Slot);
            LastCommandMessage = string.Empty;
        }
        catch (Exception ex)
        {
            LastCommandMessage = $"Window #{Slot} stop failed: {ex.Message}";
        }
    }

    public void Dispose() => _disposed = true;
}
