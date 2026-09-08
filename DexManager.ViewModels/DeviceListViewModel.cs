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
    private long _appliedGeneration = -1;

    public DeviceListViewModel(
        PhysicalDeviceRegistry registry,
        DeviceRuntimeSessionRegistry sessions,
        IUiDispatcher dispatcher,
        IDeviceRuntimeCommands commands)
    {
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
                // 행이 건 구독을 끊고 뽑는다. 순서를 바꾸면 뽑힌 행이
                // 계속 이벤트를 받는다.
                Devices[i].Dispose();
                Devices.RemoveAt(i);
            }
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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _registry.SnapshotChanged -= OnSnapshotChanged;

        foreach (var device in Devices) device.Dispose();
        Devices.Clear();
    }
}
