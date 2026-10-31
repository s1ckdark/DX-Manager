using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using DexManager.Models;
using DexManager.Services;
using DexManager.Utils;

namespace DexManager.Forms
{
    public sealed class FileTransferStatusForm : Form
    {
        private const int WsExToolWindow = 0x00000080;
        private const int AnimationIntervalMs = 350;
        private const int AutoHideMilliseconds = 4000;
        private const int VisibleQueueRows = 5;
        private const int ExpandedHeight = 368;
        private const int CompactHeight = 244;
        private readonly FileTransferCoordinator _coordinator;
        private readonly Label _statusLabel;
        private readonly Label[] _queueLabels;
        private readonly Label _detailLabel;
        private readonly Label _countLabel;
        private readonly Label _deviceLabel;
        private readonly Label _targetLabel;
        private readonly RoundedPanel _contentCard;
        private readonly ThemedButton _cancelButton;
        private readonly Timer _animationTimer;
        private readonly ToolTip _toolTip;
        private FileTransferProgress _progress;
        private DateTime _hideAtUtc;
        private bool _cancelRequested;
        private int _animationFrame;
        public event EventHandler PositionUpdateRequested;

        public string SessionId
        {
            get { return _progress == null ? string.Empty : _progress.SessionId; }
        }

        public int TargetProcessId { get; private set; }
        public IntPtr TargetWindowHandle { get; private set; }
        public bool HasResolvedTargetWindow { get; private set; }

        public FileTransferStatusForm(
            FileTransferCoordinator coordinator,
            AppTheme theme,
            string deviceName = null)
        {
            _coordinator = coordinator ??
                throw new ArgumentNullException("coordinator");
            var palette = ThemeColors.Use(theme);
            Text = LocalizationService.Get("FileTransfer.WindowTitle");
            Icon = AppIconProvider.Current;
            Font = UiFonts.Create(9.5F);
            BackColor = palette.WindowBackground;
            ForeColor = palette.TextPrimary;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            ControlBox = false;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            TopMost = false;
            StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(620, ExpandedHeight);
            DoubleBuffered = true;

            _contentCard = new RoundedPanel
            {
                Radius = 12, Location = new Point(20, 86), Size = new Size(580, 220),
                BackColor = Color.Transparent, FillColor = palette.CardBackground,
                BorderColor = palette.CardBorder
            };
            Controls.Add(_contentCard);

            _statusLabel = new UiLabel
            {
                AutoSize = false,
                BackColor = Color.Transparent,
                Font = UiFonts.Pixels(20, true),
                ForeColor = palette.TextPrimary,
                Location = new Point(20, 16),
                Size = new Size(580, 30),
                Text = LocalizationService.Get("FileTransfer.Preparing")
            };
            Controls.Add(_statusLabel);
            _deviceLabel = new UiLabel { Location = new Point(20, 52), Size = new Size(580, 22),
                BackColor = Color.Transparent,
                Font = UiFonts.Pixels(14), ForeColor = palette.TextSecondary, AutoEllipsis = true,
                Text = string.IsNullOrWhiteSpace(deviceName)
                    ? StoreUiText.Get("CommonConnectedPhone", "Connected phone") : deviceName };
            _targetLabel = new UiLabel { Location = new Point(20, 326), Size = new Size(450, 24),
                BackColor = Color.Transparent,
                Font = UiFonts.Pixels(14), ForeColor = palette.TextSecondary, AutoEllipsis = true,
                Text = StoreUiText.Get("FileTransferSendingToPhone", "Sending to phone") };
            Controls.Add(_deviceLabel);
            Controls.Add(_targetLabel);

            _toolTip = new ToolTip
            {
                AutoPopDelay = 12000,
                InitialDelay = 400,
                ReshowDelay = 100
            };
            _queueLabels = new UiLabel[VisibleQueueRows];
            for (var index = 0; index < _queueLabels.Length; index++)
            {
                var label = new UiLabel
                {
                    AutoEllipsis = true,
                    BackColor = Color.Transparent,
                    ForeColor = index == 0
                        ? palette.TextPrimary
                        : palette.TextSecondary,
                    Font = index == 0
                        ? UiFonts.Pixels(15, true)
                        : UiFonts.Pixels(14),
                    Location = new Point(16, 16 + index * 27),
                    Size = new Size(548, 25),
                    Visible = false
                };
                _queueLabels[index] = label;
                _contentCard.Controls.Add(label);
            }

            _detailLabel = new UiLabel
            {
                BackColor = Color.Transparent,
                AutoEllipsis = true,
                ForeColor = palette.TextSecondary, Font = UiFonts.Pixels(14),
                Location = new Point(16, 162),
                Size = new Size(548, 23)
            };
            _countLabel = new UiLabel
            {
                BackColor = Color.Transparent,
                AutoEllipsis = true,
                ForeColor = palette.TextSecondary, Font = UiFonts.Pixels(14),
                Location = new Point(16, 188),
                Size = new Size(548, 28),
                TextAlign = ContentAlignment.MiddleLeft
            };
            _cancelButton = new ThemedButton
            {
                Text = LocalizationService.Get("Common.Cancel"),
                Primary = false,
                Location = new Point(490, 320),
                Size = new Size(110, 36),
                Font = UiFonts.Pixels(14),
                TabStop = false
            };
            _cancelButton.Click += CancelButton_Click;

            _contentCard.Controls.Add(_detailLabel);
            _contentCard.Controls.Add(_countLabel);
            Controls.Add(_cancelButton);

            _animationTimer = new Timer
            {
                Interval = AnimationIntervalMs
            };
            _animationTimer.Tick += AnimationTimer_Tick;
            _animationTimer.Start();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            DesktopSurface.Paint(e.Graphics, ClientRectangle);
        }

        public void ApplyCurrentTheme()
        {
            var palette = ThemeColors.Current;
            BackColor = palette.WindowBackground; ForeColor = palette.TextPrimary;
            _statusLabel.ForeColor = palette.TextPrimary;
            _deviceLabel.ForeColor = _targetLabel.ForeColor = _detailLabel.ForeColor =
                _countLabel.ForeColor = palette.TextSecondary;
            _contentCard.FillColor = palette.CardBackground; _contentCard.BorderColor = palette.CardBorder;
            for (var i = 0; i < _queueLabels.Length; i++)
                _queueLabels[i].ForeColor = i == 0 ? palette.TextPrimary : palette.TextSecondary;
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

        public void UpdateProgress(FileTransferProgress progress)
        {
            if (progress == null || IsDisposed) return;
            if (_progress == null ||
                !string.Equals(
                    _progress.RequestId,
                    progress.RequestId,
                    StringComparison.OrdinalIgnoreCase) ||
                progress.Stage == FileTransferStage.Completed ||
                progress.Stage == FileTransferStage.Failed ||
                progress.Stage == FileTransferStage.Canceled)
            {
                _cancelRequested = false;
            }
            _progress = progress;
            _hideAtUtc = DateTime.MinValue;
            _animationFrame = 0;

            UpdateQueueRows(progress);
            _countLabel.Text = LocalizationService.Format(
                "FileTransfer.Counts",
                progress.CompletedCount,
                progress.FailedCount,
                progress.QueuedCount);
            UpdateStageText();
            UpdateDetailText();
            ApplyCompactLayout(
                progress.Stage == FileTransferStage.Completed ||
                progress.Stage == FileTransferStage.Failed ||
                progress.Stage == FileTransferStage.Canceled);
        }

        public void SetScrcpyTarget(int processId, IntPtr windowHandle)
        {
            if (processId != TargetProcessId)
            {
                TargetProcessId = processId;
                TargetWindowHandle = IntPtr.Zero;
                HasResolvedTargetWindow = false;
            }
            if (windowHandle != IntPtr.Zero)
            {
                TargetWindowHandle = windowHandle;
                HasResolvedTargetWindow = true;
            }
        }

        private void ApplyCompactLayout(bool compact)
        {
            if (compact)
            {
                foreach (var label in _queueLabels)
                    label.Visible = false;
                _detailLabel.Location = new Point(16, 16);
                _countLabel.Location = new Point(16, 44);
                _countLabel.Size = new Size(548, 28);
                _contentCard.Size = new Size(580, 84);
                _cancelButton.Location = new Point(490, 196);
                _targetLabel.Location = new Point(20, 202);
                ClientSize = new Size(620, CompactHeight);
                return;
            }

            _detailLabel.Location = new Point(16, 162);
            _countLabel.Location = new Point(16, 188);
            _countLabel.Size = new Size(548, 28);
            _contentCard.Size = new Size(580, 220);
            _cancelButton.Location = new Point(490, 320);
            _targetLabel.Location = new Point(20, 326);
            ClientSize = new Size(620, ExpandedHeight);
        }

        private void UpdateQueueRows(FileTransferProgress progress)
        {
            for (var index = 0; index < _queueLabels.Length; index++)
            {
                var label = _queueLabels[index];
                if (index >= progress.VisibleQueue.Count)
                {
                    label.Visible = false;
                    label.Text = string.Empty;
                    _toolTip.SetToolTip(label, string.Empty);
                    continue;
                }

                var entry = progress.VisibleQueue[index];
                var marker = entry.Active ? "▶  " : "•  ";
                if (entry.Active &&
                    progress.Stage == FileTransferStage.Completed)
                {
                    marker = "✓  ";
                }
                else if (entry.Active &&
                    progress.Stage == FileTransferStage.Failed)
                {
                    marker = "!  ";
                }
                label.Text = marker + entry.DisplayName;
                label.Font = UiFonts.Pixels(entry.Active ? 15 : 14, entry.Active);
                label.Visible = true;
                _toolTip.SetToolTip(label, entry.DisplayName);
            }
        }

        private void UpdateStageText()
        {
            if (_progress == null) return;
            switch (_progress.Stage)
            {
                case FileTransferStage.Queued:
                    _statusLabel.Text = Animated(
                        LocalizationService.Get(
                            "FileTransfer.Preparing"));
                    SetCancelable(true);
                    break;
                case FileTransferStage.Transferring:
                    _statusLabel.Text = Animated(
                        LocalizationService.Get(
                            "FileTransfer.Transferring"));
                    SetCancelable(true);
                    break;
                case FileTransferStage.Finalizing:
                    _statusLabel.Text = Animated(
                        LocalizationService.Get(
                            "FileTransfer.Finalizing"));
                    _cancelButton.Text = LocalizationService.Get(
                        "Common.Cancel");
                    _cancelButton.Enabled = false;
                    break;
                case FileTransferStage.Completed:
                    _statusLabel.Text = LocalizationService.Format(
                        "FileTransfer.CompletedSummary",
                        _progress.BatchItemCount);
                    SetCancelable(false);
                    if (_progress.QueuedCount == 0)
                    {
                        _hideAtUtc = DateTime.UtcNow.AddMilliseconds(
                            AutoHideMilliseconds);
                    }
                    break;
                case FileTransferStage.Canceled:
                    _statusLabel.Text = LocalizationService.Get(
                        "FileTransfer.Canceled");
                    SetCancelable(false);
                    _hideAtUtc = DateTime.UtcNow.AddMilliseconds(
                        AutoHideMilliseconds);
                    break;
                case FileTransferStage.Failed:
                    _statusLabel.Text = LocalizationService.Get(
                        "FileTransfer.Failed");
                    SetCancelable(false);
                    break;
            }
        }

        private void UpdateDetailText()
        {
            if (_progress == null) return;
            if (_progress.Stage == FileTransferStage.Failed ||
                _progress.Stage == FileTransferStage.Canceled)
            {
                _detailLabel.Text = _progress.Message;
                _toolTip.SetToolTip(_detailLabel, _progress.Message);
                return;
            }
            if (_progress.Stage == FileTransferStage.Completed &&
                !string.IsNullOrWhiteSpace(_progress.FinalFileName) &&
                !string.Equals(
                    _progress.FileName,
                    _progress.FinalFileName,
                    StringComparison.Ordinal))
            {
                _detailLabel.Text = LocalizationService.Format(
                    "FileTransfer.SavedAs",
                    _progress.FinalFileName);
                _toolTip.SetToolTip(
                    _detailLabel,
                    _progress.FinalFileName);
                return;
            }

            var size = FormatBytes(_progress.FileSize);
            if (_progress.StartedUtc == DateTime.MinValue)
            {
                _detailLabel.Text = size;
                return;
            }
            var elapsed = DateTime.UtcNow - _progress.StartedUtc;
            _detailLabel.Text = LocalizationService.Format(
                "FileTransfer.Detail",
                size,
                FormatElapsed(elapsed));
        }

        private string Animated(string text)
        {
            return text.TrimEnd('.', '…') +
                new string('.', _animationFrame % 3 + 1);
        }

        private void SetCancelable(bool cancelable)
        {
            _cancelButton.Text = cancelable
                ? LocalizationService.Get("Common.Cancel")
                : LocalizationService.Get("Common.Close");
            _cancelButton.Enabled = !cancelable || !_cancelRequested;
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            if (_progress == null) return;
            if (_progress.Stage == FileTransferStage.Queued ||
                _progress.Stage == FileTransferStage.Transferring ||
                _progress.Stage == FileTransferStage.Finalizing)
            {
                _cancelRequested = true;
                _cancelButton.Enabled = false;
                _coordinator.CancelTransfer(_progress.RequestId);
                return;
            }
            Hide();
        }

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            var positionHandler = PositionUpdateRequested;
            if (Visible && positionHandler != null)
                positionHandler(this, EventArgs.Empty);
            if (_hideAtUtc != DateTime.MinValue &&
                DateTime.UtcNow >= _hideAtUtc)
            {
                _hideAtUtc = DateTime.MinValue;
                Hide();
                return;
            }
            if (_progress == null || !Visible) return;
            if (_progress.Stage == FileTransferStage.Queued ||
                _progress.Stage == FileTransferStage.Transferring ||
                _progress.Stage == FileTransferStage.Finalizing)
            {
                _animationFrame++;
                UpdateStageText();
                UpdateDetailText();
            }
        }

        private static string FormatElapsed(TimeSpan elapsed)
        {
            if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;
            var totalHours = (int)elapsed.TotalHours;
            return totalHours > 0
                ? string.Format(
                    CultureInfo.CurrentCulture,
                    "{0}:{1:00}:{2:00}",
                    totalHours,
                    elapsed.Minutes,
                    elapsed.Seconds)
                : string.Format(
                    CultureInfo.CurrentCulture,
                    "{0}:{1:00}",
                    (int)elapsed.TotalMinutes,
                    elapsed.Seconds);
        }

        private static string FormatBytes(long bytes)
        {
            var value = (double)Math.Max(bytes, 0L);
            var units = new[] { "B", "KB", "MB", "GB", "TB" };
            var unit = 0;
            while (value >= 1024D && unit < units.Length - 1)
            {
                value /= 1024D;
                unit++;
            }
            return LocalizationService.Format(
                "FileTransfer.Size",
                value.ToString(
                    unit == 0 ? "0" : "0.##",
                    CultureInfo.CurrentCulture),
                units[unit]);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _animationTimer.Stop();
                _animationTimer.Tick -= AnimationTimer_Tick;
                _animationTimer.Dispose();
                _toolTip.Dispose();
                _cancelButton.Click -= CancelButton_Click;
            }
            base.Dispose(disposing);
        }
    }
}
