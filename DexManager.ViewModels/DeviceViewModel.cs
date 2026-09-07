using CommunityToolkit.Mvvm.ComponentModel;
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
    private bool _disposed;

    public DeviceViewModel(
        PhysicalDeviceInfo info,
        DeviceRuntimeSessionRegistry sessions,
        IUiDispatcher dispatcher)
    {
        if (info == null) throw new ArgumentNullException(nameof(info));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

        Identity = info.Identity ?? string.Empty;
        Update(info);

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

    // 단일창 슬롯(Slots)은 Task 10이 SingleWindowSlotViewModel과 함께
    // 도입한다. 이 Task는 DeX 세션 상태만 다루므로 여기서는 만들지 않는다.

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
    }
}
