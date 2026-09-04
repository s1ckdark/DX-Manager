using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DexManager.Models;
using DexManager.Services;

namespace DexManager.ViewModels;

/// <summary>
/// 연결된 기기 목록과 현재 선택. 레지스트리 스냅샷 변경을 구독한다.
/// </summary>
public sealed partial class DeviceListViewModel : ObservableObject, IDisposable
{
    private readonly PhysicalDeviceRegistry _registry;
    private readonly IUiDispatcher _dispatcher;
    private bool _disposed;

    public DeviceListViewModel(
        PhysicalDeviceRegistry registry,
        IUiDispatcher dispatcher)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

        _registry.SnapshotChanged += OnSnapshotChanged;
        Apply(_registry.Current);
    }

    public ObservableCollection<DeviceViewModel> Devices { get; } = new();

    [ObservableProperty]
    private DeviceViewModel _selectedDevice;

    /// <summary>목록이 비어 있는지 여부. 빈 목록 안내 표시에 쓴다.</summary>
    public bool IsEmpty => Devices.Count == 0;

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

        var incoming = snapshot?.Devices ?? new List<PhysicalDeviceInfo>();

        for (var i = Devices.Count - 1; i >= 0; i--)
        {
            var identity = Devices[i].Identity;
            var stillPresent = incoming.Any(d => string.Equals(
                d.Identity, identity, StringComparison.OrdinalIgnoreCase));
            if (!stillPresent) Devices.RemoveAt(i);
        }

        foreach (var info in incoming)
        {
            var existing = Devices.FirstOrDefault(v => string.Equals(
                v.Identity, info.Identity, StringComparison.OrdinalIgnoreCase));

            if (existing != null) existing.Update(info);
            else Devices.Add(new DeviceViewModel(info));
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
    }
}
