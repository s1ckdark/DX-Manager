using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DexManager.Models;
using DexManager.Services;

namespace DexManager.ViewModels;

/// <summary>
/// 연결된 기기 목록과 현재 선택. 레지스트리 스냅샷 변경을 구독한다.
/// </summary>
public sealed partial class DeviceListViewModel : ObservableObject, IDisposable, IDeviceSelectionSource
{
    private readonly PhysicalDeviceRegistry _registry;
    private readonly DeviceRuntimeSessionRegistry _sessions;
    private readonly IUiDispatcher _dispatcher;
    private readonly IDeviceRuntimeCommands _commands;
    private bool _disposed;
    private readonly Func<DateTime> _utcNow;
    private long _appliedGeneration = -1;
    private string _selectedMissingIdentity;
    private DateTime _selectedMissingSinceUtc;

    /// <summary>
    /// 선택된 기기가 스냅샷에서 사라진 뒤 자리표시자로 유지되는 최소 시간.
    /// <c>adb tcpip</c> 는 adbd 를 1~2초 재시작하므로 그동안 USB transport 가
    /// 한 번의 폴링에서 빠진다. 이 유예 안에서는 다른 휴대폰이 연결돼 있어도
    /// 선택과 설정 페이지를 유지한다. 유예가 지나도 연결된 다른 기기가 없으면
    /// 자리표시자("Disconnected")를 계속 남긴다 — 의도된 동작이다.
    /// </summary>
    public static readonly TimeSpan PlaceholderGrace = TimeSpan.FromSeconds(10);

    public DeviceListViewModel(
        PhysicalDeviceRegistry registry,
        DeviceRuntimeSessionRegistry sessions,
        IUiDispatcher dispatcher,
        IDeviceRuntimeCommands commands,
        Func<DateTime> utcNow = null)
    {
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));

        _registry.SnapshotChanged += OnSnapshotChanged;
        Apply(_registry.Current);
    }

    public ObservableCollection<DeviceViewModel> Devices { get; } = new();

    [ObservableProperty]
    private DeviceViewModel _selectedDevice;

    /// <summary>목록이 비어 있는지 여부. 빈 목록 안내 표시에 쓴다.</summary>
    public bool IsEmpty => Devices.Count == 0;

    /// <summary>
    /// <see cref="IDeviceSelectionSource"/> 구현. <see cref="SelectedDevice"/>가
    /// 없으면 null이다. SettingsViewModel이 DeviceListViewModel 전체
    /// 대신 이 좁은 인터페이스만 참조하도록 노출한다.
    /// </summary>
    public string SelectedIdentity => SelectedDevice?.Identity;

    partial void OnSelectedDeviceChanged(DeviceViewModel value) =>
        OnPropertyChanged(nameof(SelectedIdentity));

    private void OnSnapshotChanged(
        object sender,
        DeviceRegistrySnapshotChangedEventArgs e)
    {
        // 이 이벤트는 기기 감시 스레드에서 온다. 관측 가능한 상태를
        // 건드리기 전에 UI 스레드로 넘긴다.
        var snapshot = e?.Current;
        _dispatcher.Post(() => Apply(snapshot));
    }

    private void Apply(DeviceRegistrySnapshot snapshot)
    {
        if (_disposed) return;

        // 이 가드가 막는 것: 생성자의 구독과 동기 Apply 사이에 Reconcile이
        // 두 번 이상 끼면, 그 사이 세대들이 디스패처 큐에 먼저 쌓인다.
        // 생성자는 그중 최신 세대를 곧바로 반영하는데, 가드가 없으면
        // 큐에 남아 있던 옛 세대들이 뒤늦게 실행되며 최신 상태 위에
        // 거꾸로 덮인다. 그 일시적 되돌림과 중복 Apply를 막는다.
        //
        // 이 가드가 막지 "않는" 것: PhysicalDeviceRegistry.Reconcile은
        // 기기 목록이 실제로 바뀔 때만 SnapshotChanged를 올리고, 그때만
        // Generation을 1 증가시킨다. 즉 이벤트로 전달되는 세대는 항상
        // 단조 증가하며 순서대로 도착하므로, 최종 상태는 이 가드가 없어도
        // 결국 옳다 — 이 가드는 영구적 오류가 아니라 일시적 되돌림만 막는다.
        //
        // 이 가드를 직접 검증하는 테스트는 없다: 구독과 첫 Apply 사이의
        // 경합 창에 주입할 수 있는 이음새가 프로덕션 코드에 없고, 그런
        // 이음새를 추가하는 것은 이 클래스의 설계를 오염시킨다고 판단해
        // 추가하지 않았다. 따라서 이 가드를 실수로 지워도 기존 테스트
        // 스위트는 잡아내지 못한다.
        var generation = snapshot?.Generation ?? 0;
        if (generation <= _appliedGeneration) return;
        _appliedGeneration = generation;

        var incoming = snapshot?.Devices ?? new List<PhysicalDeviceInfo>();

        for (var i = Devices.Count - 1; i >= 0; i--)
        {
            var identity = Devices[i].Identity;
            var stillPresent = incoming.Any(d => string.Equals(
                d.Identity, identity, StringComparison.OrdinalIgnoreCase));
            if (!stillPresent)
            {
                // 선택된 행은 transport 가 잠깐 사라져도(adb tcpip 의 adbd
                // 재시작 등) 연결 끊김 자리표시자로 남긴다. 행과 선택이
                // 유지돼야 기기별 설정 페이지와 진행 중인 작업이 살아남고,
                // 선택이 다른 휴대폰으로 미끄러지지 않는다.
                if (ReferenceEquals(Devices[i], SelectedDevice) && !PlaceholderExpired(Devices[i].Identity, incoming))
                {
                    Devices[i].Update(new PhysicalDeviceInfo
                    {
                        Identity = Devices[i].Identity,
                        DisplayName = Devices[i].DisplayName,
                        Transports = new List<DeviceTransportInfo>()
                    });
                    continue;
                }

                // 행이 건 구독을 끊고 뽑는다. 순서를 바꾸면 뽑힌 행이
                // 계속 이벤트를 받는다.
                Devices[i].Dispose();
                Devices.RemoveAt(i);
            }
        }

        if (SelectedDevice != null && incoming.Any(d => string.Equals(
                d.Identity, SelectedDevice.Identity, StringComparison.OrdinalIgnoreCase)))
        {
            _selectedMissingIdentity = null;
        }

        foreach (var info in incoming)
        {
            var existing = Devices.FirstOrDefault(v => string.Equals(
                v.Identity, info.Identity, StringComparison.OrdinalIgnoreCase));

            if (existing != null) existing.Update(info);
            else Devices.Add(new DeviceViewModel(info, _sessions, _dispatcher, _commands));
        }

        // 레지스트리가 이미 정렬해 둔 순서(incoming)를 그대로 따라간다.
        // 터미널 UI도 snapshot.Devices 순서를 그대로 쓰므로, 두 UI가
        // 같은 데이터를 다른 순서로 보여주면 안 된다. Move는 선택 상태를
        // 건드리지 않는 이동 이벤트를 내므로 remove+insert 대신 이걸 쓴다.
        for (var target = 0; target < incoming.Count; target++)
        {
            var identity = incoming[target].Identity;
            var currentIndex = -1;
            for (var i = 0; i < Devices.Count; i++)
            {
                if (string.Equals(Devices[i].Identity, identity, StringComparison.OrdinalIgnoreCase))
                {
                    currentIndex = i;
                    break;
                }
            }
            if (currentIndex >= 0 && currentIndex != target)
            {
                Devices.Move(currentIndex, target);
            }
        }

        if (SelectedDevice == null || !Devices.Contains(SelectedDevice))
        {
            SelectedDevice = Devices.FirstOrDefault();
        }

        OnPropertyChanged(nameof(IsEmpty));
    }

    // 선택 행이 사라진 시각을 기록하고, 유예가 지났고 다른 연결된 기기가
    // 있을 때만 자리표시자를 만료시킨다. 시각은 주입된 시계를 쓴다 —
    // 레지스트리가 CapturedAtUtc 를 DateTime.UtcNow 로 직접 찍어 테스트가
    // 스냅샷 시각을 정할 수 없기 때문이다.
    private bool PlaceholderExpired(string identity, IList<PhysicalDeviceInfo> incoming)
    {
        var now = _utcNow();
        if (!string.Equals(_selectedMissingIdentity, identity, StringComparison.OrdinalIgnoreCase))
        {
            _selectedMissingIdentity = identity;
            _selectedMissingSinceUtc = now;
        }

        if (now - _selectedMissingSinceUtc < PlaceholderGrace) return false;
        if (!incoming.Any(d => d.IsConnected)) return false;

        _selectedMissingIdentity = null;
        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _registry.SnapshotChanged -= OnSnapshotChanged;

        foreach (var device in Devices) device.Dispose();
        Devices.Clear();
    }
}
