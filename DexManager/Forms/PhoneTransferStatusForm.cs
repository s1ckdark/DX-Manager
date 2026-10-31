using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using DexManager.Models;
using DexManager.Services;
using DexManager.Utils;

namespace DexManager.Forms
{
    public sealed class PhoneTransferStatusForm : Form
    {
        private const int WsExToolWindow = 0x00000080;
        private const int AutoHideMilliseconds = 4000;
        private readonly Label _statusLabel;
        private readonly Label _itemLabel;
        private readonly Label _countLabel;
        private readonly Label _byteLabel;
        private readonly Label _deviceLabel;
        private readonly Label _destinationLabel;
        private readonly Label[] _queueLabels;
        private readonly RoundedPanel _contentCard;
        private readonly List<string> _items = new List<string>();
        private Guid _batchId;
        private string _activeItem;
        private readonly TransferProgressBar _progressBar;
        private readonly ThemedButton _openFolderButton;
        private readonly ThemedButton _closeButton;
        private readonly Timer _autoHideTimer;
        private string _destinationFolder;
        private DateTime _hideAtUtc;

        public PhoneTransferStatusForm(AppTheme theme, string deviceName = null)
        {
            var palette = ThemeColors.Use(theme);
            Text = LocalizationService.Get("PhoneTransfer.WindowTitle");
            Icon = AppIconProvider.Current;
            StartPosition = FormStartPosition.Manual;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            ControlBox = false;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            TopMost = false;
            ClientSize = new Size(560, 370);
            BackColor = palette.WindowBackground;
            Font = UiFonts.Create(9.5F);
            DoubleBuffered = true;
            _contentCard = new RoundedPanel { Radius = 12, BackColor = Color.Transparent,
                FillColor = palette.CardBackground, BorderColor = palette.CardBorder,
                Location = new Point(20, 86), Size = new Size(520, 214) };
            Controls.Add(_contentCard);

            _statusLabel = new UiLabel
            {
                AutoSize = false,
                BackColor = Color.Transparent,
                Font = UiFonts.Pixels(20, true),
                ForeColor = palette.TextPrimary,
                Location = new Point(20, 16),
                Size = new Size(520, 30),
                Text = LocalizationService.Get("PhoneTransfer.Receiving")
            };
            _itemLabel = new UiLabel
            {
                BackColor = Color.Transparent,
                AutoEllipsis = true,
                ForeColor = palette.TextSecondary,
                Location = new Point(16, 30),
                Size = new Size(488, 24), Font = UiFonts.Pixels(15, true)
            };
            _progressBar = new TransferProgressBar
            {
                Location = new Point(16, 16),
                Size = new Size(488, 4),
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 28
            };
            _countLabel = new UiLabel
            {
                BackColor = Color.Transparent,
                ForeColor = palette.TextSecondary,
                Location = new Point(16, 155),
                Size = new Size(230, 22), Font = UiFonts.Pixels(14)
            };
            _byteLabel = new UiLabel
            {
                BackColor = Color.Transparent,
                ForeColor = palette.TextSecondary,
                Location = new Point(258, 155),
                Size = new Size(246, 22), Font = UiFonts.Pixels(14),
                TextAlign = ContentAlignment.TopRight
            };
            _openFolderButton = new ThemedButton
            {
                Text = LocalizationService.Get(
                    "PhoneTransfer.OpenFolder"),
                Primary = false,
                Location = new Point(290, 314),
                Size = new Size(128, 36),
                Font = UiFonts.Pixels(14),
                Enabled = false
            };
            _openFolderButton.Click += delegate { OpenDestination(); };
            _closeButton = new ThemedButton
            {
                Text = LocalizationService.Get("Common.Close"),
                Primary = false,
                Location = new Point(430, 314),
                Size = new Size(110, 36), Font = UiFonts.Pixels(14)
            };
            _closeButton.Click += delegate { Hide(); };

            Controls.Add(_statusLabel);
            _contentCard.Controls.Add(_itemLabel);
            _contentCard.Controls.Add(_progressBar);
            _contentCard.Controls.Add(_countLabel);
            _contentCard.Controls.Add(_byteLabel);
            _deviceLabel = new UiLabel { Location = new Point(20, 52), Size = new Size(520, 22),
                BackColor = Color.Transparent,
                Font = UiFonts.Pixels(14), ForeColor = palette.TextSecondary, AutoEllipsis = true,
                Text = string.IsNullOrWhiteSpace(deviceName) ? StoreUiText.Get("CommonConnectedPhone", "Connected phone") : deviceName };
            _destinationLabel = new UiLabel { Location = new Point(16, 176), Size = new Size(488, 20),
                BackColor = Color.Transparent,
                Font = UiFonts.Pixels(13), ForeColor = palette.TextSecondary, AutoEllipsis = true };
            Controls.Add(_deviceLabel);
            _contentCard.Controls.Add(_destinationLabel);
            _queueLabels = new Label[4];
            for (var i = 0; i < _queueLabels.Length; i++)
            {
                _queueLabels[i] = new UiLabel { Location = new Point(16, 54 + i * 24), Size = new Size(488, 23),
                    BackColor = Color.Transparent,
                    Font = UiFonts.Pixels(14), ForeColor = palette.TextSecondary, AutoEllipsis = true };
                _contentCard.Controls.Add(_queueLabels[i]);
            }
            Controls.Add(_openFolderButton);
            Controls.Add(_closeButton);

            _autoHideTimer = new Timer { Interval = 250 };
            _autoHideTimer.Tick += AutoHideTimer_Tick;
            _autoHideTimer.Start();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            DesktopSurface.Paint(e.Graphics, ClientRectangle);
        }

        public void ApplyCurrentTheme()
        {
            var palette = ThemeColors.Current;
            BackColor = palette.WindowBackground;
            _statusLabel.ForeColor = palette.TextPrimary;
            _itemLabel.ForeColor = palette.TextPrimary;
            _deviceLabel.ForeColor = _countLabel.ForeColor =
                _byteLabel.ForeColor = _destinationLabel.ForeColor = palette.TextSecondary;
            foreach (var label in _queueLabels) label.ForeColor = palette.TextSecondary;
            _contentCard.FillColor = palette.CardBackground; _contentCard.BorderColor = palette.CardBorder;
            Invalidate(true);
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var parameters = base.CreateParams;
                parameters.ExStyle |= WsExToolWindow;
                return parameters;
            }
        }

        public void UpdateProgress(PhoneTransferProgress progress)
        {
            if (progress == null || IsDisposed) return;
            _hideAtUtc = DateTime.MinValue;
            _destinationFolder = progress.DestinationFolder;
            _destinationLabel.Text = progress.DestinationFolder;
            if (_batchId != progress.BatchId)
            {
                _batchId = progress.BatchId; _items.Clear(); _activeItem = null;
            }
            if (!string.IsNullOrWhiteSpace(progress.CurrentItem) && progress.Stage != PhoneTransferStage.Failed &&
                !string.Equals(_activeItem, progress.CurrentItem, StringComparison.Ordinal))
            {
                _items.Add(progress.CurrentItem); _activeItem = progress.CurrentItem;
                if (_items.Count > 5) _items.RemoveAt(0);
            }
            for (var i = 0; i < _queueLabels.Length; i++)
                _queueLabels[i].Text = i + 1 < _items.Count ? "•  " + _items[i] : string.Empty;
            _itemLabel.Text = progress.Stage == PhoneTransferStage.Failed
                ? progress.Error
                : string.IsNullOrWhiteSpace(_activeItem) ? string.Empty :
                    (progress.Stage == PhoneTransferStage.Completed ? "✓  " : "▶  ") + _activeItem;
            _countLabel.Text = LocalizationService.Format(
                "PhoneTransfer.Items",
                progress.CompletedItems,
                progress.TotalItems);
            _byteLabel.Text = progress.TotalBytes > 0
                ? LocalizationService.Format(
                    "PhoneTransfer.Bytes",
                    FormatBytes(progress.ReceivedBytes),
                    FormatBytes(progress.TotalBytes))
                : LocalizationService.Format(
                    "PhoneTransfer.BytesUnknown",
                    FormatBytes(progress.ReceivedBytes));

            if (progress.TotalBytes > 0)
            {
                _progressBar.Style = ProgressBarStyle.Continuous;
                _progressBar.MarqueeAnimationSpeed = 0;
                var percentage = (int)Math.Max(
                    0,
                    Math.Min(
                        100,
                        progress.ReceivedBytes * 100L /
                        Math.Max(1L, progress.TotalBytes)));
                _progressBar.Value = percentage;
            }
            else if (progress.Stage == PhoneTransferStage.Receiving)
            {
                _progressBar.Style = ProgressBarStyle.Marquee;
                _progressBar.MarqueeAnimationSpeed = 28;
            }

            if (progress.Stage == PhoneTransferStage.Completed)
            {
                _statusLabel.Text = LocalizationService.Get(
                    "PhoneTransfer.Completed");
                _progressBar.Style = ProgressBarStyle.Continuous;
                _progressBar.MarqueeAnimationSpeed = 0;
                _progressBar.Value = 100;
                _openFolderButton.Enabled = Directory.Exists(
                    _destinationFolder);
                _hideAtUtc = DateTime.UtcNow.AddMilliseconds(
                    AutoHideMilliseconds);
            }
            else if (progress.Stage == PhoneTransferStage.Failed ||
                progress.Stage == PhoneTransferStage.Canceled)
            {
                _statusLabel.Text = LocalizationService.Get(
                    "PhoneTransfer.Failed");
                _progressBar.Style = ProgressBarStyle.Continuous;
                _progressBar.MarqueeAnimationSpeed = 0;
                _progressBar.Value = 0;
                _openFolderButton.Enabled = Directory.Exists(
                    _destinationFolder);
                if (progress.Stage == PhoneTransferStage.Canceled)
                {
                    _hideAtUtc = DateTime.UtcNow.AddMilliseconds(
                        AutoHideMilliseconds);
                }
            }
            else
            {
                _statusLabel.Text = LocalizationService.Get(
                    "PhoneTransfer.Receiving");
            }
        }

        private void AutoHideTimer_Tick(object sender, EventArgs e)
        {
            if (_hideAtUtc == DateTime.MinValue ||
                DateTime.UtcNow < _hideAtUtc)
            {
                return;
            }
            _hideAtUtc = DateTime.MinValue;
            Hide();
        }

        private void OpenDestination()
        {
            if (string.IsNullOrWhiteSpace(_destinationFolder) ||
                !Directory.Exists(_destinationFolder))
            {
                return;
            }
            Process.Start(new ProcessStartInfo
            {
                FileName = _destinationFolder,
                UseShellExecute = true
            });
        }

        private static string FormatBytes(long bytes)
        {
            var value = Math.Max(0L, bytes);
            var units = new[] { "B", "KB", "MB", "GB", "TB" };
            var amount = (double)value;
            var unit = 0;
            while (amount >= 1024D && unit < units.Length - 1)
            {
                amount /= 1024D;
                unit++;
            }
            return unit == 0
                ? value + " " + units[unit]
                : amount.ToString("0.##") + " " + units[unit];
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _autoHideTimer.Stop();
                _autoHideTimer.Tick -= AutoHideTimer_Tick;
                _autoHideTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    // Store-style accent track with the same known-byte/marquee decisions as the original form.
    internal sealed class TransferProgressBar : Control
    {
        private readonly Timer _timer;
        private ProgressBarStyle _style;
        private int _value, _phase, _speed = 28;
        public TransferProgressBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            _timer = new Timer { Interval = 28 };
            _timer.Tick += delegate { _phase = (_phase + 3) % 200; Invalidate(); };
        }
        public ProgressBarStyle Style { get { return _style; } set { _style = value; RefreshAnimation(); Invalidate(); } }
        public int Value { get { return _value; } set { _value = Math.Max(0, Math.Min(100, value)); Invalidate(); } }
        public int MarqueeAnimationSpeed { get { return _speed; } set { _speed = value; RefreshAnimation(); } }
        protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); RefreshAnimation(); }
        private void RefreshAnimation()
        {
            if (_timer == null) return;
            _timer.Interval = Math.Max(15, _speed);
            _timer.Enabled = Visible && _style == ProgressBarStyle.Marquee && _speed > 0;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 1 || Height < 1) return;
            DesktopSurface.PaintBehind(e.Graphics, this);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var bounds = new Rectangle(0, 0, Width, Height);
            using (var track = new SolidBrush(ThemeColors.Current.ControlBorder)) e.Graphics.FillRectangle(track, bounds);
            Rectangle fill;
            if (_style == ProgressBarStyle.Marquee)
            {
                var start = (int)((Width + Width / 3F) * _phase / 200F) - Width / 3;
                fill = new Rectangle(Math.Max(0, start), 0, Math.Max(0, Math.Min(Width, start + Width / 3) - Math.Max(0, start)), Height);
            }
            else fill = new Rectangle(0, 0, Width * _value / 100, Height);
            using (var accent = new SolidBrush(ThemeColors.Current.Accent)) e.Graphics.FillRectangle(accent, fill);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { _timer.Stop(); _timer.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
