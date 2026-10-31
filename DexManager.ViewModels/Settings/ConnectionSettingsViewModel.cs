using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DexManager.Models;
using DexManager.Services;

namespace DexManager.ViewModels;

/// <summary>
/// 기기 하나의 무선 ADB 연결 페이지. 저장/취소 흐름이 없고 각 동작은
/// 즉시 실행되며 성공하면 서비스가 프로필을 저장한다.
/// </summary>
public sealed partial class ConnectionSettingsViewModel : ObservableObject, IDisposable
{
    private const int DefaultPort = 5555;

    private readonly IWirelessConnectionGateway _wireless;
    private readonly IUiDispatcher _dispatcher;
    private bool _disposed;

    /// <param name="identity">대상 물리 기기 identity.</param>
    /// <param name="wireless">무선 연결 경계.</param>
    /// <param name="dispatcher">UI 스레드 디스패처.</param>
    public ConnectionSettingsViewModel(string identity, IWirelessConnectionGateway wireless, IUiDispatcher dispatcher)
    {
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
        _wireless = wireless ?? throw new ArgumentNullException(nameof(wireless));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

        var profile = _wireless.GetProfile(Identity);
        _host = profile?.WirelessHost ?? string.Empty;
        _port = profile != null && profile.WirelessPort > 0 ? profile.WirelessPort : DefaultPort;
        _autoReconnect = profile?.AutoReconnect ?? true;
        Refresh();

        _wireless.DevicesChanged += OnDevicesChanged;
    }

    /// <summary>대상 물리 기기 identity.</summary>
    public string Identity { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private string _host;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPortValid))]
    [NotifyCanExecuteChangedFor(nameof(PrepareWirelessCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private int _port;

    [ObservableProperty]
    private bool _autoReconnect;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrepareWirelessCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisconnectCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _transportSummary = string.Empty;

    [ObservableProperty]
    private string _savedModeText = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrepareWirelessCommand))]
    private bool _hasUsbTransport;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DisconnectCommand))]
    private bool _canDisconnect;

    /// <summary>포트가 1~65535 범위인지.</summary>
    public bool IsPortValid => Port is >= 1 and <= 65535;

    partial void OnPortChanged(int value)
    {
        var rangeMessage = LocalizationService.Get("Settings.Connection.PortOutOfRange");
        if (!IsPortValid)
            StatusMessage = rangeMessage;
        else if (StatusMessage == rangeMessage)
            StatusMessage = string.Empty;
    }

    /// <summary>
    /// 기기 스냅샷과 저장된 프로필을 다시 읽어 상태를 갱신한다.
    /// 반드시 UI 스레드에서 호출해야 한다.
    /// </summary>
    public void Refresh()
    {
        var device = _wireless.FindDevice(Identity);
        var profile = _wireless.GetProfile(Identity);

        var transports = device?.Transports?.Where(t => t != null && t.IsAuthorized).ToList()
            ?? new List<DeviceTransportInfo>();

        TransportSummary = transports.Count == 0
            ? LocalizationService.Get("Settings.Connection.NoTransport")
            : string.Join(", ", transports.Select(t => $"{t.Kind}: {t.Serial}"));

        HasUsbTransport = !string.IsNullOrEmpty(
            device?.SelectAuthorizedTransport(DeviceTransportKind.Usb, null)?.Serial);

        var savedWireless = profile != null && profile.Mode == AdbConnectionMode.Wireless;
        string mode;
        if (savedWireless && !string.IsNullOrWhiteSpace(profile.WirelessHost))
            mode = LocalizationService.Get("Settings.Wireless") + " " + $"{profile.WirelessHost}:{profile.WirelessPort}";
        else if (savedWireless)
            mode = LocalizationService.Get("Settings.Wireless");
        else
            mode = LocalizationService.Get("Settings.Usb");
        SavedModeText = LocalizationService.Format("Settings.Connection.SavedMode", mode);

        CanDisconnect = savedWireless || transports.Any(t => t.Kind == DeviceTransportKind.Wireless);
    }

    private bool CanPrepare() => !IsBusy && HasUsbTransport && IsPortValid;

    private bool CanConnect() => !IsBusy && !string.IsNullOrWhiteSpace(Host) && IsPortValid;

    private bool CanRunDisconnect() => !IsBusy && CanDisconnect;

    /// <summary>USB로 연결된 기기에서 무선 ADB를 켠다. Host가 비면 IP를 자동 감지한다.</summary>
    [RelayCommand(CanExecute = nameof(CanPrepare))]
    private Task PrepareWirelessAsync()
    {
        // 작업 시작 시점의 USB serial을 캡처한다.
        var usbSerial = _wireless.FindDevice(Identity)?
            .SelectAuthorizedTransport(DeviceTransportKind.Usb, null)?.Serial;
        var host = (Host ?? string.Empty).Trim();
        var port = Port;
        var auto = AutoReconnect;
        return RunAsync(() => _wireless.EnableFromUsbAsync(Identity, usbSerial, host, port, auto, CancellationToken.None));
    }

    /// <summary>지정한 host:port로 무선 연결한다.</summary>
    [RelayCommand(CanExecute = nameof(CanConnect))]
    private Task ConnectAsync()
    {
        var host = Host.Trim();
        var port = Port;
        var auto = AutoReconnect;
        return RunAsync(() => _wireless.ConnectAsync(Identity, host, port, auto, CancellationToken.None));
    }

    /// <summary>무선 연결을 끊고 USB 모드로 되돌린다.</summary>
    [RelayCommand(CanExecute = nameof(CanRunDisconnect))]
    private Task DisconnectAsync() =>
        RunAsync(() => _wireless.DisconnectAsync(Identity, CancellationToken.None));

    private async Task RunAsync(Func<Task<WirelessConnectionResult>> operation)
    {
        IsBusy = true;
        try
        {
            var result = await operation();
            if (_disposed) return;
            StatusMessage = result.Endpoint == string.Empty
                ? result.Message
                : $"{result.Message} ({result.Endpoint})";
            Refresh();
        }
        catch (OperationCanceledException)
        {
            if (!_disposed) StatusMessage = string.Empty;
        }
        catch (Exception ex)
        {
            if (!_disposed)
                StatusMessage = LocalizationService.Format("Settings.WirelessOperationFailed", ex.Message);
        }
        finally
        {
            if (!_disposed) IsBusy = false;
        }
    }

    private void OnDevicesChanged(object sender, EventArgs e)
    {
        // 임의 스레드에서 온다. UI 스레드로 넘기고 실행 시점에 다시 확인한다.
        if (_disposed) return;
        _dispatcher.Post(() =>
        {
            if (_disposed) return;
            Refresh();
        });
    }

    /// <summary>구독을 해제한다. 멱등하다.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _wireless.DevicesChanged -= OnDevicesChanged;
    }
}
