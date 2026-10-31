using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using DexManager.Services;

namespace DexManager.Forms
{
    public sealed partial class MainForm
    {
        private UpdateCheckService _updateChecks;
        private UpdateAvailableBanner _updateBanner;
        private string _dismissedUpdateVersion;

        internal void InitializeUpdateChecks(UpdateCheckService checks)
        {
            _updateChecks = checks;
            FormClosed += delegate { checks.Dispose(); };
            ClientSizeChanged += delegate { PositionUpdateBanner(); };
        }

        private async void BeginAutomaticUpdateCheck()
        {
            var checks = _updateChecks;
            if (checks == null || _exitInProgress || _systemShutdownInProgress) return;
            try
            {
                var result = await checks.CheckAsync(true).ConfigureAwait(false);
                RunOnUi(delegate
                {
                    if (IsDisposed || Disposing || _exitInProgress || _systemShutdownInProgress) return;
                    RefreshUpdateBanner();
                    if (result.Outcome == UpdateCheckOutcome.Available && result.Notify &&
                        (_hiddenToTray || !Visible || WindowState == FormWindowState.Minimized))
                        _trayService.ShowBalloon(LocalizationService.Get("Update.AvailableTitle"),
                            LocalizationService.Format("Update.TrayMessage", result.Update.VersionText));
                });
            }
            catch (Exception ex) { _logService.Warning("Update check was skipped: " + ex.Message); }
        }

        private void RefreshUpdateBanner()
        {
            if (_updateChecks == null || _exitInProgress || _systemShutdownInProgress) return;
            var update = _updateChecks.KnownUpdate;
            if (update == null || string.Equals(_dismissedUpdateVersion, update.VersionText,
                StringComparison.Ordinal))
            {
                if (_updateBanner != null) _updateBanner.Visible = false;
                return;
            }
            if (_updateBanner == null)
            {
                _updateBanner = new UpdateAvailableBanner { Visible = false };
                _updateBanner.Dismissed += delegate
                {
                    var dismissed = _updateBanner.AvailableUpdate;
                    if (dismissed == null) return;
                    _dismissedUpdateVersion = dismissed.VersionText;
                    var checks = _updateChecks;
                    Task.Run(delegate { checks.Dismiss(dismissed); });
                };
                Controls.Add(_updateBanner);
            }
            _updateBanner.SetUpdate(update);
            _updateBanner.ApplyCurrentTheme();
            PositionUpdateBanner();
            _updateBanner.Visible = true;
            _updateBanner.BringToFront();
        }

        private void PositionUpdateBanner()
        {
            if (_updateBanner == null) return;
            var top = ClientSize.Height - _updateBanner.Height - DesktopPixels(28);
            if (_appNotice != null && _appNotice.Visible)
                top = Math.Min(top, _appNotice.Top - _updateBanner.Height - DesktopPixels(12));
            _updateBanner.Location = new Point(Math.Max(8,
                ClientSize.Width - _updateBanner.Width - DesktopPixels(32)), Math.Max(16, top));
        }

        private void ApplyUpdateTheme()
        {
            if (_updateBanner != null) _updateBanner.ApplyCurrentTheme();
            foreach (Form form in Application.OpenForms)
            {
                var dialog = form as UpdateCheckDialog;
                if (dialog != null && !dialog.IsDisposed) dialog.ApplyCurrentTheme();
            }
        }

        private void ShowUpdateCheckDialog(Form owner)
        {
            if (_exitInProgress || _systemShutdownInProgress) return;
            var checks = _updateChecks;
            using (var dialog = new UpdateCheckDialog(Application.ProductVersion, async delegate
            {
                var result = checks == null ? new UpdateCheckResult(UpdateCheckOutcome.Failed) :
                    await checks.CheckAsync(false);
                if (!IsDisposed && !_exitInProgress && !_systemShutdownInProgress)
                    RunOnUi(RefreshUpdateBanner);
                return result;
            }))
                dialog.ShowDialog(owner);
        }
    }
}
