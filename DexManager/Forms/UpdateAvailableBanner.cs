using System;
using System.Drawing;
using System.Windows.Forms;
using DexManager.Models;
using DexManager.Services;

namespace DexManager.Forms
{
    internal sealed class UpdateAvailableBanner : Panel
    {
        private readonly RoundedPanel _surface;
        private readonly UiLabel _title;
        private readonly UiLabel _version;
        private readonly ThemedButton _open;
        private readonly ThemedButton _close;
        internal ReleaseUpdate AvailableUpdate { get; private set; }
        internal event EventHandler Dismissed;

        internal UpdateAvailableBanner()
        {
            DoubleBuffered = true;
            Size = new Size(450, 100);
            Font = UiFonts.Pixels(14);
            BackColor = Color.Transparent;
            _surface = new RoundedPanel { Dock = DockStyle.Fill, Radius = 14, Size = ClientSize };
            _title = new UiLabel
            {
                Text = LocalizationService.Get("Update.AvailableTitle"),
                Font = UiFonts.Pixels(15, true), AutoSize = false,
                Bounds = new Rectangle(16, 12, 390, 24), BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _version = new UiLabel
            {
                Font = UiFonts.Pixels(14), AutoSize = false,
                Bounds = new Rectangle(16, 48, 226, 34), BackColor = Color.Transparent,
                Anchor = AnchorStyles.Left | AnchorStyles.Bottom | AnchorStyles.Right
            };
            _open = new ThemedButton
            {
                Text = LocalizationService.Get("Update.OpenRelease"), Primary = true,
                Font = UiFonts.Pixels(13, true), Bounds = new Rectangle(250, 48, 182, 34),
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom
            };
            _open.Click += delegate { UpdateCheckDialog.OpenRelease(FindForm(), AvailableUpdate); };
            _close = new ThemedButton
            {
                Text = "\u00d7", Font = UiFonts.Pixels(18), Bounds = new Rectangle(410, 6, 32, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                AccessibleName = LocalizationService.Get("Common.Close")
            };
            _close.Click += delegate
            {
                Visible = false;
                var handler = Dismissed;
                if (handler != null) handler(this, EventArgs.Empty);
            };
            _surface.Controls.AddRange(new Control[] { _title, _version, _open, _close });
            Controls.Add(_surface);
            ApplyCurrentTheme();
        }

        internal void SetUpdate(ReleaseUpdate update)
        {
            AvailableUpdate = update;
            _version.Text = update == null ? string.Empty :
                LocalizationService.Format("Update.NewVersion", update.VersionText);
        }

        internal void ApplyCurrentTheme()
        {
            var theme = ThemeColors.Current;
            _surface.FillColor = theme.CardBackground;
            _surface.BorderColor = theme.Accent;
            _title.ForeColor = theme.TextPrimary;
            _version.ForeColor = theme.TextSecondary;
            Invalidate(true);
        }
    }
}
