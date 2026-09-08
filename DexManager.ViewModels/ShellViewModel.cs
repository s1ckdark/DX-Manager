using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DexManager.Hosting;

namespace DexManager.ViewModels;

/// <summary>
/// 앱 전역 상태. <see cref="ApplicationHost"/>의 수명주기를 소유하는
/// 유일한 ViewModel이다.
/// </summary>
public sealed partial class ShellViewModel : ObservableObject, IDisposable
{
    private readonly ApplicationHost _host;
    private readonly IUiDispatcher _dispatcher;
    private bool _disposed;

    public ShellViewModel(ApplicationHost host, IUiDispatcher dispatcher)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

        Devices = new DeviceListViewModel(
            host.DeviceRegistry,
            host.RuntimeSessions,
            dispatcher,
            new DeviceRuntimeCommands(host));
        Devices.PropertyChanged += OnDeviceListPropertyChanged;
        _host.SelectedSerialChanged += OnSelectedSerialChanged;
    }

    public DeviceListViewModel Devices { get; }

    [ObservableProperty]
    private string _statusText = "Starting…";

    /// <summary>
    /// <see cref="OpenSettingsCommand"/>가 새 <see cref="SettingsViewModel"/>을
    /// 준비할 때마다 발생한다. 이 ViewModel은 Avalonia를 몰라야 하므로
    /// 창을 직접 띄우지 않는다 - Desktop 레이어(Task 12)가 이 이벤트를
    /// 구독해 실제 설정 창을 연다.
    /// </summary>
    public event EventHandler<SettingsViewModel> SettingsRequested;

    /// <summary>기기 감시를 시작한다.</summary>
    public void Start()
    {
        _host.Start();
        StatusText = "Watching for devices";
    }

    /// <summary>
    /// 현재 선택된 기기를 대상으로 한 <see cref="SettingsViewModel"/>을
    /// 만들어 <see cref="SettingsRequested"/>로 알린다. 선택된 기기가
    /// 없어도(Devices.SelectedDevice == null) 전역 설정 페이지는 여전히
    /// 유효하므로 항상 실행 가능하다 - 기기별 페이지는
    /// SettingsViewModel이 null로 처리한다.
    /// </summary>
    [RelayCommand]
    private void OpenSettings()
    {
        var gateway = new SettingsGateway(_host);
        var settings = new SettingsViewModel(gateway, Devices);
        SettingsRequested?.Invoke(this, settings);
    }

    private void OnDeviceListPropertyChanged(
        object sender,
        PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(DeviceListViewModel.SelectedDevice)) return;

        // 진단 서비스가 읽는 값이다. 선택이 바뀌면 호스트에 반영한다.
        _host.SelectedSerial = Devices.SelectedDevice?.PrimarySerial ?? string.Empty;
    }

    private void OnSelectedSerialChanged(
        object sender,
        SelectedSerialChangedEventArgs e)
    {
        var current = e?.Current ?? string.Empty;
        _dispatcher.Post(() =>
        {
            // Post는 비동기다. 구독을 해제해도 이미 큐에 들어간 클로저는
            // 되돌릴 수 없으므로 실행 시점에 다시 확인한다.
            if (_disposed) return;

            StatusText = string.IsNullOrEmpty(current)
                ? "No device selected"
                : $"Selected {current}";
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Devices.PropertyChanged -= OnDeviceListPropertyChanged;
        _host.SelectedSerialChanged -= OnSelectedSerialChanged;

        Devices.Dispose();
        _host.Stop();
        _host.Dispose();
    }
}
