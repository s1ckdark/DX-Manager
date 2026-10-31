using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using DexManager.Models;
using DexManager.Services;
using DexManager.Utils;

namespace DexManager.Forms
{
    internal sealed class UpdateCheckDialog : Form
    {
        private readonly RoundedPanel _surface;
        private readonly UiLabel _title;
        private readonly UiLabel _message;
        private readonly UiLabel _detail;
        private readonly ThemedButton _releaseButton;
        private readonly ThemedButton _closeButton;
        private readonly Func<Task<UpdateCheckResult>> _check;
        private readonly string _currentVersion;
        private ReleaseUpdate _update;

        internal UpdateCheckDialog(string currentVersion, Func<Task<UpdateCheckResult>> check)
        {
            _currentVersion = currentVersion;
            _check = check;
            Text = LocalizationService.Get("Update.DialogTitle");
            Icon = AppIconProvider.Current;
            Font = UiFonts.Pixels(14);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);
            ClientSize = new Size(540, 268);
            _surface = new RoundedPanel { Dock = DockStyle.Fill, Radius = 14 };
            _title = new UiLabel
            {
                Text = LocalizationService.Get("Update.Checking"),
                Font = UiFonts.Pixels(22, true), AutoSize = false,
                Bounds = new Rectangle(24, 24, 492, 40), BackColor = Color.Transparent
            };
            _message = new UiLabel
            {
                Font = UiFonts.Pixels(15), AutoSize = false,
                Bounds = new Rectangle(26, 80, 488, 56), BackColor = Color.Transparent,
                Text = LocalizationService.Format("Update.CurrentVersion", currentVersion)
            };
            _detail = new UiLabel
            {
                Font = UiFonts.Pixels(14), AutoSize = false,
                Bounds = new Rectangle(26, 142, 488, 44), BackColor = Color.Transparent,
                Text = LocalizationService.Get("Update.CheckingDetail")
            };
            _releaseButton = new ThemedButton
            {
                Text = LocalizationService.Get("Update.OpenRelease"), Primary = true,
                Font = UiFonts.Pixels(14, true), Bounds = new Rectangle(192, 208, 200, 38),
                Visible = false
            };
            _releaseButton.Click += delegate { OpenRelease(this, _update); };
            _closeButton = new ThemedButton
            {
                Text = LocalizationService.Get("Common.Close"),
                Font = UiFonts.Pixels(14), Bounds = new Rectangle(404, 208, 110, 38),
                DialogResult = DialogResult.Cancel
            };
            _surface.Controls.AddRange(new Control[]
                { _title, _message, _detail, _releaseButton, _closeButton });
            Controls.Add(_surface);
            CancelButton = _closeButton;
            ApplyCurrentTheme();
            Shown += CheckWhenShown;
        }

        private async void CheckWhenShown(object sender, EventArgs e)
        {
            if (_check == null) return;
            UpdateCheckResult result;
            try { result = await _check(); }
            catch { result = new UpdateCheckResult(UpdateCheckOutcome.Failed); }
            if (!IsDisposed && !Disposing) DisplayResult(result);
        }

        internal void DisplayResult(UpdateCheckResult result)
        {
            _update = result.Update;
            var available = result.Outcome == UpdateCheckOutcome.Available && _update != null;
            _releaseButton.Visible = available;
            _closeButton.Primary = !available;
            if (available)
            {
                _title.Text = LocalizationService.Get("Update.AvailableTitle");
                _message.Text = LocalizationService.Format("Update.VersionPair",
                    _currentVersion, _update.VersionText);
                _detail.Text = LocalizationService.Get("Update.AvailableDetail");
            }
            else if (result.Outcome == UpdateCheckOutcome.Current)
            {
                _title.Text = LocalizationService.Get("Update.CurrentTitle");
                _message.Text = LocalizationService.Format("Update.CurrentVersion", _currentVersion);
                _detail.Text = LocalizationService.Get("Update.CurrentDetail");
            }
            else
            {
                var busy = result.Outcome == UpdateCheckOutcome.Busy;
                _title.Text = LocalizationService.Get(busy ? "Update.BusyTitle" : "Update.FailedTitle");
                _message.Text = LocalizationService.Get(busy ? "Update.BusyDetail" : "Update.FailedDetail");
                _detail.Text = LocalizationService.Format("Update.CurrentVersion", _currentVersion);
            }
        }

        internal void ApplyCurrentTheme()
        {
            var theme = ThemeColors.Current;
            BackColor = theme.WindowBackground;
            _surface.FillColor = theme.CardBackground;
            _surface.BorderColor = theme.CardBorder;
            _title.ForeColor = theme.TextPrimary;
            _message.ForeColor = theme.TextPrimary;
            _detail.ForeColor = theme.TextSecondary;
            Invalidate(true);
        }

        internal static void OpenRelease(IWin32Window owner, ReleaseUpdate update)
        {
            if (update == null) return;
            try
            {
                Process.Start(new ProcessStartInfo(update.ReleaseUrl) { UseShellExecute = true });
            }
            catch
            {
                DesktopDialogs.Show(owner, LocalizationService.Get("Update.OpenFailed"),
                    LocalizationService.Get("Update.DialogTitle"));
            }
        }
    }
}
