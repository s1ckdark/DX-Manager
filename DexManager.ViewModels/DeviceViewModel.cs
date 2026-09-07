using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DexManager.Models;
using DexManager.Services;

namespace DexManager.ViewModels;

/// <summary>
/// 기기 1대의 표시용 상태. <see cref="Identity"/>는 불변이며 목록 안에서
/// 이 ViewModel을 식별하는 키다.
/// </summary>
public sealed partial class DeviceViewModel : ObservableObject, IDisposable
{
    private readonly DeviceRuntimeSessionRegistry _sessions;
    private readonly IUiDispatcher _dispatcher;
    private readonly IDeviceRuntimeCommands _commands;
    private bool _disposed;

    public DeviceViewModel(
        PhysicalDeviceInfo info,
        DeviceRuntimeSessionRegistry sessions,
        IUiDispatcher dispatcher,
        IDeviceRuntimeCommands commands)
    {
        if (info == null) throw new ArgumentNullException(nameof(info));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));

        Identity = info.Identity ?? string.Empty;
        Update(info);

        // 구독보다 먼저 슬롯을 만들어야 아래 첫 ApplyRuntime 호출이
        // 슬롯까지 반영된다.
        for (var slot = 1; slot <= 3; slot++)
        {
            Slots.Add(new SingleWindowSlotViewModel(
                slot,
                Identity,
                () => PrimarySerial,
                _commands));
        }

        // DexOrchestrator에는 public event가 없다. 상태 변화를 실시간으로
        // 관측하는 유일한 경로가 이 레지스트리다.
        _sessions.Changed += OnSessionsChanged;
        ApplyRuntime(_sessions.Current);
    }

    /// <summary>기기의 영속 식별자. 재연결되어도 유지된다.</summary>
    public string Identity { get; }

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _primarySerial = string.Empty;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _transportSummary = string.Empty;

    [ObservableProperty]
    private bool _isDexRunning;

    /// <summary>이 기기의 단일창 슬롯 1~3. 생성자에서 채워지며 이후
    /// 개수가 바뀌지 않는다.</summary>
    public ObservableCollection<SingleWindowSlotViewModel> Slots { get; } = new();

    /// <summary>
    /// 이 행의 명령이 하나 진행 중인지 여부. scrcpy 시작은 느리므로
    /// 버튼을 두 번 눌러 세션이 겹치는 것을 막는다.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartDexCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopDexCommand))]
    private bool _isBusy;

    /// <summary>마지막 명령의 결과 문구. 실패해도 예외를 올리지 않는다.</summary>
    [ObservableProperty]
    private string _lastCommandMessage = string.Empty;

    private bool CanRunCommand() => !_disposed && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanRunCommand))]
    private async Task StartDexAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            var started = await _commands.StartDexAsync(
                Identity,
                PrimarySerial,
                cancellationToken);
            LastCommandMessage = started
                ? "DeX started."
                : "DeX did not start.";
        }
        catch (OperationCanceledException)
        {
            LastCommandMessage = "DeX start was cancelled.";
        }
        catch (Exception ex)
        {
            // 명령 실패가 창을 죽이면 안 된다. 행에 문구로만 남긴다.
            LastCommandMessage = $"DeX start failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunCommand))]
    private async Task StopDexAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            var stopped = await _commands.StopDexAsync(
                Identity,
                PrimarySerial,
                cancellationToken);
            LastCommandMessage = stopped
                ? "DeX stopped and the display overlay was cleaned up."
                : "DeX stopped, but display cleanup was deferred.";
        }
        catch (Exception ex)
        {
            LastCommandMessage = $"DeX stop failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// 새 스냅샷의 값으로 갱신한다. 기존 인스턴스를 재사용하므로
    /// 목록 바인딩과 선택 상태가 유지된다.
    /// </summary>
    public void Update(PhysicalDeviceInfo info)
    {
        if (info == null || _disposed) return;

        DisplayName = info.DisplayName ?? string.Empty;
        IsConnected = info.IsConnected;

        // 현재 serial 을 선호값으로 넘겨 같은 transport 를 계속 고르게 한다.
        PrimarySerial =
            info.SelectPreferredTransport(PrimarySerial)?.Serial ?? string.Empty;

        TransportSummary = info.Transports == null
            ? string.Empty
            : string.Join(", ", info.Transports.Select(t => $"{t.Kind}: {t.Serial}"));
    }

    private void OnSessionsChanged(
        object sender,
        DeviceRuntimeRegistryChangedEventArgs e)
    {
        // 런타임 스레드에서 온다. 관측 가능한 상태를 건드리기 전에
        // UI 스레드로 넘긴다.
        var snapshot = e?.Snapshot;
        _dispatcher.Post(() => ApplyRuntime(snapshot));
    }

    private void ApplyRuntime(DeviceRuntimeRegistrySnapshot snapshot)
    {
        // Post는 비동기다. 구독을 해제해도 이미 큐에 들어간 클로저는
        // 되돌릴 수 없으므로 실행 시점에 다시 확인해야 한다.
        if (_disposed) return;

        var session = snapshot?.FindByIdentity(Identity);
        IsDexRunning = session?.Dex?.IsRunning == true;

        foreach (var slot in Slots) slot.ApplyRuntime(session);
    }

    /// <summary>이 행이 이미 해제되었는지 여부.</summary>
    public bool IsDisposed => _disposed;

    /// <summary>
    /// 이 행이 건 구독을 해제한다. 멱등하다.
    /// 목록에서 제거될 때와 목록 자체가 해제될 때 호출된다.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _sessions.Changed -= OnSessionsChanged;

        foreach (var slot in Slots) slot.Dispose();
        Slots.Clear();
    }
}
