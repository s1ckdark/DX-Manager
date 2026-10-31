using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using DexManager.Models;
using DexManager.Services;

namespace DexManager.Forms
{
    public sealed partial class SettingsForm : Form, IMessageFilter
    {
        private Panel _deviceDiagnosticsBody;
        private bool _layingOutDiagnosticBody;
        private Control BuildSelectedDeviceDiagnosticsPanel()
        {
            _deviceDiagnosticsBody = new BufferedPanel {Width=620,Height=24,BackColor=_theme.CardBackground};
            _deviceDiagnosticsStatusLabel = new UiLabel {AutoSize=false,ForeColor=_theme.TextSecondary,BackColor=Color.Transparent,
                Text=LocalizationService.Get("Settings.DeviceDiagnosticsWaiting")};
            _deviceDiagnosticsRefreshButton=CreateActionButton(LocalizationService.IsKorean?"새로고침":"Refresh",160);
            _deviceDiagnosticsRefreshButton.Click+=delegate {RefreshDeviceDiagnosticsAsync();};
            _deviceDiagnosticsCardsPanel=new BufferedFlowPanel {AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,
                FlowDirection=FlowDirection.LeftToRight,WrapContents=true,Margin=Padding.Empty,BackColor=Color.Transparent};
            _deviceDiagnosticsBody.Controls.AddRange(new Control[]{_deviceDiagnosticsStatusLabel,_deviceDiagnosticsCardsPanel});
            _deviceDiagnosticsBody.SizeChanged+=delegate {LayoutDeviceDiagnosticBody();};
            _deviceDiagnosticsStatusLabel.TextChanged+=delegate {LayoutDeviceDiagnosticBody();};
            LayoutDeviceDiagnosticBody();
            return _deviceDiagnosticsBody;
        }
        private void LayoutDeviceDiagnosticBody()
        {
            if(_layingOutDiagnosticBody || _deviceDiagnosticsBody==null || _deviceDiagnosticsBody.IsDisposed)return;
            _layingOutDiagnosticBody=true;
            try {
                int width=Math.Max(120,_deviceDiagnosticsBody.Width);
                int statusHeight=Math.Max(_deviceDiagnosticsStatusLabel.Font.Height,_deviceDiagnosticsStatusLabel.GetPreferredSize(new Size(width,0)).Height);
                _deviceDiagnosticsStatusLabel.Bounds=new Rectangle(0,0,width,statusHeight);
                var cards=_deviceDiagnosticsCardsPanel;
                cards.MinimumSize=Size.Empty;cards.MaximumSize=new Size(width,0);cards.MinimumSize=new Size(width,0);cards.Width=width;
                int cardWidth=width>=580?(width-12)/2:width-6;
                cards.SuspendLayout();
                foreach(Control card in cards.Controls) {
                    card.Width=cardWidth;
                    if(card.Controls.Count==0)continue;
                    var content=card.Controls[0];content.Width=cardWidth-28;
                    foreach(Control label in content.Controls)label.MaximumSize=new Size(cardWidth-28,0);
                    content.PerformLayout();card.Height=Math.Max(150,content.PreferredSize.Height+28);
                }
                cards.ResumeLayout(true);
                cards.Visible=cards.Controls.Count>0;
                cards.Location=new Point(0,statusHeight+10);
                _deviceDiagnosticsBody.Height=statusHeight+(cards.Controls.Count>0?10+cards.PreferredSize.Height:0);
            }finally{_layingOutDiagnosticBody=false;}
        }
        private async void RefreshDeviceDiagnosticsAsync()
        {
            if (IsDisposed || Disposing ||
                _deviceDiagnosticsStatusLabel == null ||
                _deviceDiagnosticsRefreshButton == null ||
                _deviceDiagnosticsCardsPanel == null)
            {
                return;
            }

            var devices = _getDeviceSnapshot();
            var connectedCount = devices == null || devices.Devices == null
                ? 0
                : devices.Devices.Count(device =>
                    device != null && device.IsConnected);
            var generation = ++_deviceDiagnosticsGeneration;
            _deviceDiagnosticsRefreshButton.Enabled = false;
            _deviceDiagnosticsStatusLabel.Text = connectedCount == 0
                ? LocalizationService.Get("Settings.DeviceDiagnosticsNone")
                : LocalizationService.Get(
                    "Settings.DeviceDiagnosticsChecking");

            if (connectedCount == 0)
            {
                _lastDeviceDiagnostics = new List<DeviceVersionDiagnostic>();
                RenderDeviceDiagnosticCards(_lastDeviceDiagnostics);
                _deviceDiagnosticsRefreshButton.Enabled = true;
                return;
            }

            IList<DeviceVersionDiagnostic> diagnostics;
            try
            {
                var runtimes = _getRuntimeSnapshot();
                var selectedIdentity = _getSelectedDeviceIdentity();
                var selectedSerial = _getSelectedDeviceSerial();
                diagnostics = await Task.Run(() =>
                    InspectConnectedDeviceDiagnostics(
                        devices,
                        runtimes,
                        selectedIdentity,
                        selectedSerial));
            }
            catch (Exception ex)
            {
                diagnostics = new List<DeviceVersionDiagnostic>
                {
                    new DeviceVersionDiagnostic
                    {
                        DisplayName = string.Empty,
                        ErrorDetail = ex.Message,
                        QuerySucceeded = false
                    }
                };
            }

            if (IsDisposed || Disposing ||
                generation != _deviceDiagnosticsGeneration)
            {
                return;
            }
            _lastDeviceDiagnostics = diagnostics.ToList();
            _deviceDiagnosticsRefreshButton.Enabled = true;
            _deviceDiagnosticsStatusLabel.Text = LocalizationService.Format(
                "Settings.DeviceDiagnosticsComplete",
                diagnostics.Count);
            RenderDeviceDiagnosticCards(_lastDeviceDiagnostics);
        }

        private IList<DeviceVersionDiagnostic>
            InspectConnectedDeviceDiagnostics(
                DeviceRegistrySnapshot devices,
                DeviceRuntimeRegistrySnapshot runtimes,
                string selectedIdentity,
                string selectedSerial)
        {
            var result = new List<DeviceVersionDiagnostic>();
            if (devices == null || devices.Devices == null)
                return result;

            foreach (var device in devices.Devices)
            {
                if (device == null || !device.IsConnected) continue;
                var runtime = runtimes == null
                    ? null
                    : runtimes.FindByIdentity(device.Identity);
                var isSelected = string.Equals(
                    device.Identity,
                    selectedIdentity,
                    StringComparison.OrdinalIgnoreCase);
                var serial = DiagnosticDeviceTargetResolver.Resolve(
                    device,
                    runtime,
                    isSelected,
                    selectedSerial);
                DeviceVersionDiagnostic diagnostic;
                if (string.IsNullOrWhiteSpace(serial))
                {
                    diagnostic = new DeviceVersionDiagnostic
                    {
                        DeviceIdentity = device.Identity,
                        DisplayName = device.DisplayName,
                        ErrorDetail = LocalizationService.Get(
                            "Settings.DeviceDiagnosticsNoActiveTransport"),
                        QuerySucceeded = false
                    };
                }
                else
                {
                    try
                    {
                        diagnostic = _deviceVersionDiagnosticService.Inspect(
                            serial,
                            device);
                    }
                    catch (Exception exception)
                    {
                        diagnostic = new DeviceVersionDiagnostic
                        {
                            DeviceIdentity = device.Identity,
                            Serial = serial,
                            DisplayName = device.DisplayName,
                            TransportKind = device.FindTransport(serial).Kind,
                            ErrorDetail = exception.Message,
                            QuerySucceeded = false
                        };
                    }
                }
                diagnostic.DeviceIdentity = device.Identity ?? string.Empty;
                if (string.IsNullOrWhiteSpace(diagnostic.DisplayName))
                    diagnostic.DisplayName = device.DisplayName ?? string.Empty;
                result.Add(diagnostic);
            }
            return result;
        }

        private void RenderDeviceDiagnosticCards(
            IEnumerable<DeviceVersionDiagnostic> diagnostics)
        {
            if (_deviceDiagnosticsCardsPanel == null ||
                _deviceDiagnosticsCardsPanel.IsDisposed)
            {
                return;
            }

            _deviceDiagnosticsCardsPanel.FlowDirection = FlowDirection.LeftToRight;
            _deviceDiagnosticsCardsPanel.WrapContents = true;
            _deviceDiagnosticsCardsPanel.SuspendLayout();
            try
            {
                while (_deviceDiagnosticsCardsPanel.Controls.Count > 0)
                    _deviceDiagnosticsCardsPanel.Controls[0].Dispose();
                foreach (var diagnostic in diagnostics ??
                    Enumerable.Empty<DeviceVersionDiagnostic>())
                {
                    if (diagnostic == null) continue;
                    var cardWidth = _storeUiReady
                        ? Math.Max(260, (_deviceDiagnosticsCardsPanel.Width - 12) / 2)
                        : 610;
                    var card = new RoundedPanel
                    {
                        Width = cardWidth,
                        Radius = 10,
                        Tag = "diagnostic-card",
                        BackColor = Color.Transparent,
                        FillColor = _theme.CardSoft,
                        BorderColor = _theme.CardBorder,
                        Margin = new Padding(0, 0, 6, 8)
                    };
                    var content = new BufferedFlowPanel
                    {
                        Location = new Point(14, 14),
                        Width = cardWidth - 28,
                        AutoSize = true,
                        AutoSizeMode = AutoSizeMode.GrowAndShrink,
                        FlowDirection = FlowDirection.TopDown,
                        WrapContents = false,
                        BackColor = _theme.CardSoft
                    };
                    card.Controls.Add(content);
                    var name = !string.IsNullOrWhiteSpace(
                        diagnostic.DisplayName)
                        ? diagnostic.DisplayName
                        : !string.IsNullOrWhiteSpace(diagnostic.Model)
                            ? diagnostic.Model
                            : LocalizationService.Get("Common.Unknown");
                    var titleFont = UiFonts.Pixels(15F, true);
                    card.Disposed += delegate { titleFont.Dispose(); };
                    content.Controls.Add(new UiLabel
                    {
                        AutoSize = true,
                        MaximumSize = new Size(Math.Max(220, cardWidth - 28), 0),
                        Font = titleFont,
                        Tag = "diagnostic-title",
                        ForeColor = _theme.TextPrimary,
                        BackColor = _theme.CardSoft,
                        Text = name
                    });
                    content.Controls.Add(new UiLabel
                    {
                        AutoSize = true,
                        MaximumSize = new Size(Math.Max(220, cardWidth - 28), 0),
                        Margin = new Padding(0, 5, 0, 0),
                        ForeColor = _theme.TextSecondary,
                        BackColor = _theme.CardSoft,
                        Text = FormatDeviceDiagnostic(diagnostic)
                    });
                    card.Height = Math.Max(150, content.PreferredSize.Height + 28);
                    _deviceDiagnosticsCardsPanel.Controls.Add(card);
                }
            }
            finally
            {
                _deviceDiagnosticsCardsPanel.ResumeLayout(true);
                LayoutDeviceDiagnosticBody();
            }
        }

        private async void DiagnosticReportButton_Click(
            object sender,
            EventArgs e)
        {
            if (_diagnosticReportButton == null ||
                !_diagnosticReportButton.Enabled)
            {
                return;
            }

            _diagnosticReportButton.Enabled = false;
            try
            {
                var serial = _getSelectedDeviceSerial();
                var identity = _getSelectedDeviceIdentity();
                var devices = _getDeviceSnapshot();
                var runtimes = _getRuntimeSnapshot();
                var deviceDiagnostics = await Task.Run(() =>
                    InspectConnectedDeviceDiagnostics(
                        devices,
                        runtimes,
                        identity,
                        serial));
                if (IsDisposed || Disposing) return;
                _lastDeviceDiagnostics = deviceDiagnostics.ToList();
                RenderDeviceDiagnosticCards(_lastDeviceDiagnostics);
                var diagnostic = deviceDiagnostics.FirstOrDefault(item =>
                    string.Equals(
                        item.DeviceIdentity,
                        identity,
                        StringComparison.OrdinalIgnoreCase)) ??
                    deviceDiagnostics.FirstOrDefault(item =>
                        string.Equals(
                            item.Serial,
                            serial,
                            StringComparison.OrdinalIgnoreCase));
                if (diagnostic == null)
                {
                    diagnostic = new DeviceVersionDiagnostic
                    {
                        DeviceIdentity = identity ?? string.Empty,
                        Serial = serial ?? string.Empty,
                        ErrorDetail = LocalizationService.Get(
                            "Settings.DeviceDiagnosticsNotConnected"),
                        QuerySucceeded = false
                    };
                }

                var companion = await Task.Run(() =>
                    _displayCleanupPermissionService.Inspect(serial));
                var adbVersionResult = await Task.Run(() =>
                    _adbService.GetVersion());
                if (IsDisposed || Disposing) return;
                var adbVersion = adbVersionResult == null
                    ? string.Empty
                    : adbVersionResult.StandardOutput;
                var report = _diagnosticReportService.CreateReport(
                    Application.ProductVersion,
                    _adbService.AdbPath,
                    adbVersion,
                    _settings.Paths.ScrcpyPath,
                    identity,
                    devices,
                    runtimes,
                    diagnostic,
                    companion,
                    _logService.GetSessionEntries(),
                    deviceDiagnostics);

                using (var dialog = new SaveFileDialog
                {
                    AddExtension = true,
                    DefaultExt = "txt",
                    FileName = "DX_Manager_Diagnostic_" +
                        DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt",
                    Filter = LocalizationService.Get(
                        "Settings.DiagnosticReportFilter")
                })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    File.WriteAllText(
                        dialog.FileName,
                        report,
                        new UTF8Encoding(true));
                    DesktopDialogs.Show(
                        this,
                        LocalizationService.Get(
                            "Settings.DiagnosticReportSaved"),
                        LocalizationService.Get("Settings.Diagnostics"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    OpenReportFolder(dialog.FileName);
                }
            }
            catch (Exception ex)
            {
                if (IsDisposed || Disposing) return;
                DesktopDialogs.Show(
                    this,
                    LocalizationService.Format(
                        "Settings.DiagnosticReportFailed",
                        ex.Message),
                    LocalizationService.Get("Settings.Diagnostics"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                if (!IsDisposed && _diagnosticReportButton != null)
                    _diagnosticReportButton.Enabled = true;
            }
        }

        private PhysicalDeviceInfo FindSelectedPhysicalDevice(string serial)
        {
            var snapshot = _getDeviceSnapshot();
            if (snapshot == null) return null;
            var identity = _getSelectedDeviceIdentity();
            var device = snapshot.FindByIdentity(identity);
            return device ?? snapshot.FindByTransportSerial(serial);
        }

        private static void OpenReportFolder(string reportPath)
        {
            try
            {
                Process.Start(
                    "explorer.exe",
                    "/select,\"" + Path.GetFullPath(reportPath) + "\"");
            }
            catch
            {
                // Saving the report is successful even when Explorer cannot
                // be opened by a restricted Windows environment.
            }
        }

        private static string FormatDeviceDiagnostic(
            DeviceVersionDiagnostic diagnostic)
        {
            if (diagnostic == null || !diagnostic.QuerySucceeded)
            {
                return LocalizationService.Format(
                    "Settings.DeviceDiagnosticsFailed",
                    diagnostic == null ||
                    string.IsNullOrWhiteSpace(diagnostic.ErrorDetail)
                        ? LocalizationService.Get("Common.Unknown")
                        : diagnostic.ErrorDetail);
            }

            var android = string.IsNullOrWhiteSpace(
                diagnostic.AndroidVersion)
                ? LocalizationService.Get("Common.Unknown")
                : diagnostic.AndroidVersion;
            if (diagnostic.AndroidSdk > 0)
                android += " (SDK " + diagnostic.AndroidSdk + ")";
            var oneUi = string.IsNullOrWhiteSpace(diagnostic.OneUiVersion)
                ? LocalizationService.Get("Common.Unknown")
                : diagnostic.OneUiVersion;
            var patch = string.IsNullOrWhiteSpace(diagnostic.SecurityPatch)
                ? LocalizationService.Get("Common.Unknown")
                : diagnostic.SecurityPatch;
            return LocalizationService.Format(
                "Settings.DeviceDiagnosticsSummary",
                LocalizationService.Get(
                    "Settings.Transport." + diagnostic.TransportKind),
                android,
                oneUi,
                patch,
                LocalizationService.Get(
                    "Settings.Compatibility." +
                    diagnostic.Compatibility));
        }
    }
}
