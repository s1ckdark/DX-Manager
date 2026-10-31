using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using DexManager.Models;
using DexManager.Services;
using DexManager.Utils;

namespace DexManager.Forms
{
    public sealed partial class MainForm : Form
    {
        private readonly DeviceAppCatalog _deviceAppCatalog = new DeviceAppCatalog();
        private RoundedPanel _appNotice;
        private Label _appNoticeLabel;
        private Timer _appNoticeTimer;
        private DesktopNoticeKind _noticeKind;
        private bool _missingStartAppNotice;
        private int _missingStartAppNoticeSlot;
        private string _missingStartAppNoticeDevice;
        private readonly Dictionary<string, HashSet<int>> _missingStartAppNotices =
            new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);

        private void RefreshAppItems(string packageName, string appName)
        {
            var recent = string.IsNullOrWhiteSpace(_selectedDeviceIdentity)
                ? new List<RememberedAppSettings>()
                : GetSelectedDeviceRunSettings().RememberedApps;
            var items = _deviceAppCatalog.CreateItems(
                _selectedDeviceIdentity, GetSelectedDeviceSerial(), recent,
                packageName, appName, NoStartAppText);
            _startAppBox.SelectedIndex = -1;
            _startAppBox.Items.Clear();
            foreach (var item in items) _startAppBox.Items.Add(item);
            SetSelectedAppPackage(packageName, appName);
        }

        private void ShowAppsLoadedNotice(int count, string sourceDevice)
        {
            ShowDesktopNotice(string.IsNullOrWhiteSpace(sourceDevice) ? LocalizationService.Format("Main.AppsLoaded", count) : LocalizationService.Format("Main.DeviceAppsLoaded", sourceDevice, count), DesktopNoticeKind.Success);
        }
        private enum DesktopNoticeKind { Information, Success, Warning, Error }
        private bool IsSingleWindowAppMissing(int slot)
        {
            if(slot<=0)return false;
            return string.IsNullOrWhiteSpace(slot==_selectedMode ? GetSelectedAppPackage() : GetSingleWindowSettings(slot).StartAppPackage);
        }
        private void UpdateStartAppAvailability()
        {
            var button=_startButton as ThemedButton;
            if(button!=null){button.ExecutionBlocked=IsSingleWindowAppMissing(_selectedMode);button.Invalidate();}
            UpdateMissingStartAppNotice();
        }
        private void UpdateMissingStartAppNotice()
        {
            var identity = _selectedDeviceIdentity ?? string.Empty;
            HashSet<int> slots;
            var pending = _missingStartAppNotices.TryGetValue(identity, out slots) &&
                slots.Contains(_selectedMode);
            var ownsNotice = _missingStartAppNotice &&
                _selectedMode == _missingStartAppNoticeSlot &&
                string.Equals(identity, _missingStartAppNoticeDevice, StringComparison.OrdinalIgnoreCase);

            if (pending && !IsSingleWindowAppMissing(_selectedMode))
            {
                RemoveMissingStartAppNotice(identity, _selectedMode);
                pending = false;
            }
            if (!pending)
            {
                if (ownsNotice) DismissDesktopNotice();
                else if (_missingStartAppNotice && _appNotice != null) _appNotice.Visible = false;
                return;
            }
            if (ownsNotice)
            {
                _appNotice.Visible = true;
                _appNotice.BringToFront();
            }
            else if (_missingStartAppNotice || _appNotice == null || !_appNotice.Visible)
            {
                ShowMissingStartAppNotice(_selectedMode);
            }
        }
        private void RemoveMissingStartAppNotice(string identity, int slot)
        {
            identity = identity ?? string.Empty;
            HashSet<int> slots;
            if (!_missingStartAppNotices.TryGetValue(identity, out slots)) return;
            slots.Remove(slot);
            if (slots.Count == 0) _missingStartAppNotices.Remove(identity);
        }
        private void ShowDesktopNotice(string message, DesktopNoticeKind kind)
        {
            if (_exitInProgress || IsDisposed || Disposing) return;
            _noticeKind = kind;
            _missingStartAppNotice=false;
            if (_appNotice == null)
            {
                _appNoticeLabel = new UiLabel { Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter, TabStop = false };
                _appNotice = new RoundedPanel { Visible = false,
                    Padding = new Padding(12, 8, 38, 8), TabStop = false, BackdropSource=_appCard };
                _appNotice.Controls.Add(_appNoticeLabel);
                var dismiss = new ThemedButton { Text = "×", Size = new Size(28, 28), TabStop = false, Anchor = AnchorStyles.Top | AnchorStyles.Right, AccessibleName = LocalizationService.Get("Common.Close") };
                dismiss.Click += delegate { DismissDesktopNotice(); };
                _appNotice.Controls.Add(dismiss);
                _appNotice.SizeChanged += delegate { dismiss.Location = new Point(_appNotice.Width - 32, (_appNotice.Height - 28) / 2); dismiss.BringToFront(); };
                _appNotice.VisibleChanged += delegate { PositionUpdateBanner(); };
                Controls.Add(_appNotice);
                _appNoticeTimer = new Timer { Interval = 5000 };
                _appNoticeTimer.Tick += delegate { DismissDesktopNotice(); };
                Resize += delegate { PositionAppNotice(); };
                Disposed += delegate { _appNoticeTimer.Dispose(); };
            }
            _appNoticeTimer.Stop();
            _appNoticeLabel.Text = (kind == DesktopNoticeKind.Success ? "✓  " : kind == DesktopNoticeKind.Error ? "×  " : kind == DesktopNoticeKind.Warning ? "⚠  " : "ⓘ  ") + message;
            _appNoticeLabel.Font = UiFonts.Pixels(14F);
            ApplyDesktopNoticeTheme();
            PositionAppNotice();
            _appNotice.Visible = true;
            _appNotice.BringToFront();
            if (kind == DesktopNoticeKind.Information || kind == DesktopNoticeKind.Success) _appNoticeTimer.Start();
        }

        private void ShowMissingStartAppNotice(int slot)
        {
            var identity = _selectedDeviceIdentity ?? string.Empty;
            HashSet<int> slots;
            if (!_missingStartAppNotices.TryGetValue(identity, out slots))
            {
                slots = new HashSet<int>();
                _missingStartAppNotices.Add(identity, slots);
            }
            slots.Add(slot);
            ShowDesktopNotice(LocalizationService.Get("Error.SingleWindow.AppRequired"),DesktopNoticeKind.Error);
            _missingStartAppNotice=true;_missingStartAppNoticeSlot=slot;_missingStartAppNoticeDevice=identity;
            _appNotice.Visible = _selectedMode == slot;
        }
        private void DismissDesktopNotice()
        {
            if(_appNoticeTimer!=null)_appNoticeTimer.Stop();
            if(_appNotice!=null)_appNotice.Visible=false;
            if (_missingStartAppNotice)
                RemoveMissingStartAppNotice(_missingStartAppNoticeDevice, _missingStartAppNoticeSlot);
            _missingStartAppNotice=false;
        }
        private void ApplyDesktopNoticeTheme()
        {
            if(_appNotice==null)return;
            _appNoticeLabel.ForeColor = _theme.TextPrimary;
            _appNoticeLabel.BackColor = Color.Transparent;
            _appNotice.SurfaceOpacity = 230;
            _appNotice.FillColor = _noticeKind == DesktopNoticeKind.Error
                ? (_theme.IsDark ? Color.FromArgb(49,42,55) : Color.FromArgb(255,235,239))
                : _noticeKind == DesktopNoticeKind.Warning ? (_theme.IsDark ? Color.FromArgb(60,53,42) : Color.FromArgb(255,247,221))
                : _noticeKind == DesktopNoticeKind.Success ? (_theme.IsDark ? Color.FromArgb(38,57,48) : Color.FromArgb(230,248,234)) : _theme.CardSoft;
            _appNotice.BorderColor = _noticeKind == DesktopNoticeKind.Error
                ? (_theme.IsDark ? Color.FromArgb(174,100,119) : Color.FromArgb(226,151,169))
                : _noticeKind == DesktopNoticeKind.Warning ? Color.FromArgb(177,149,88)
                : _noticeKind == DesktopNoticeKind.Success ? Color.FromArgb(115,161,126) : _theme.CardBorder;
        }

        private void PositionAppNotice()
        {
            if (_appNotice == null) return;
            var width = Math.Max(120, Math.Min(ClientSize.Width - 32, DesktopPixels(620)));
            var textSize = UiText.MeasureText(_appNoticeLabel.Text, Font,
                new Size(width - 60, 0), TextFormatFlags.WordBreak);
            width = Math.Max(260, Math.Min(width, UiText.MeasureText(_appNoticeLabel.Text, _appNoticeLabel.Font).Width + 76));
            _appNotice.Size = new Size(width, Math.Max(48, textSize.Height + 24));
            _appNotice.Location = new Point(ClientSize.Width - width - DesktopPixels(32),
                Math.Max(16, ClientSize.Height - _appNotice.Height - DesktopPixels(28)));
            PositionUpdateBanner();
        }

        private void SetSelectedAppPackage(
            string packageName,
            string appName)
        {
            packageName = packageName ?? string.Empty;
            foreach (var item in _startAppBox.Items)
            {
                var app = item as ScrcpyAppInfo;
                if (app == null ||
                    !string.Equals(
                        app.PackageName ?? string.Empty,
                        packageName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(appName) &&
                    string.Equals(
                        app.Name,
                        app.PackageName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    app.Name = appName;
                }
                _startAppBox.SelectedItem = item;
                return;
            }

            if (string.IsNullOrWhiteSpace(packageName))
            {
                _startAppBox.SelectedIndex = 0;
                return;
            }

            var placeholder = new ScrcpyAppInfo
            {
                Name = string.IsNullOrWhiteSpace(appName)
                    ? packageName
                    : appName,
                PackageName = packageName
            };
            _startAppBox.Items.Add(placeholder);
            _startAppBox.SelectedItem = placeholder;
        }

        private void StartAppBox_SelectionChangeCommitted(
            object sender,
            EventArgs e)
        {
            UpdateStartAppAvailability();
            if (_selectedMode > 0 &&
                ApplySelectedAppProfileIfAvailable())
            {
                UpdateRunningState();
                return;
            }

            SaveSelectedAppIdentity();
            UpdateAppProfileControls();
            UpdateRunningState();
        }

        private void SaveSelectedAppIdentity()
        {
            var packageName = GetSelectedAppPackage();
            var appName = GetSelectedAppName(packageName);
            var selectedMode = _selectedMode;
            try
            {
                _settingsService.UpdateAndSave(_settings, delegate(
                    AppSettings candidate)
                {
                    var runSettings = GetDeviceRunSettings(
                        candidate,
                        _selectedDeviceIdentity);
                    if (selectedMode == 0)
                    {
                        runSettings.Scrcpy.StartAppPackage = packageName;
                        runSettings.Scrcpy.StartAppName = appName;
                    }
                    else
                    {
                        var slot = GetSingleWindowSettings(
                            runSettings,
                            selectedMode);
                        slot.StartAppPackage = packageName;
                        slot.StartAppName = appName;
                    }
                });
            }
            catch (Exception ex)
            {
                _logService.Error(
                    LocalizationService.Get(
                        "Log.Main.AppIdentitySaveFailed"),
                    ex);
            }
        }

        private void RememberStartedApp(
            string deviceIdentity,
            string packageName,
            string appName)
        {
            packageName = (packageName ?? string.Empty).Trim();
            if (packageName.Length == 0 || string.IsNullOrWhiteSpace(deviceIdentity)) return;

            appName = string.IsNullOrWhiteSpace(appName)
                ? packageName
                : appName.Trim();
            try
            {
                _settingsService.UpdateAndSave(_settings, delegate(
                    AppSettings candidate)
                {
                    var rememberedApps = candidate.GetOrCreateDeviceRunSettings(deviceIdentity).RememberedApps;

                    for (var index = rememberedApps.Count - 1;
                        index >= 0;
                        index--)
                    {
                        var remembered = rememberedApps[index];
                        if (remembered == null ||
                            string.IsNullOrWhiteSpace(
                                remembered.PackageName) ||
                            string.Equals(
                                remembered.PackageName,
                                packageName,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            rememberedApps.RemoveAt(index);
                        }
                    }

                    rememberedApps.Insert(
                        0,
                        new RememberedAppSettings
                        {
                            Name = appName,
                            PackageName = packageName
                        });
                    while (rememberedApps.Count >
                        MaxRememberedApps)
                    {
                        rememberedApps.RemoveAt(rememberedApps.Count - 1);
                    }
                });
            }
            catch (Exception ex)
            {
                _logService.Error(
                    LocalizationService.Get(
                        "Log.Main.RememberedAppSaveFailed"),
                    ex);
                return;
            }

            if (string.Equals(deviceIdentity, _selectedDeviceIdentity, StringComparison.OrdinalIgnoreCase))
                RefreshAppItems(GetSelectedAppPackage(), GetSelectedAppName(GetSelectedAppPackage()));
        }

        private void AddNoStartAppItem()
        {
            _startAppBox.Items.Add(new ScrcpyAppInfo
            {
                Name = NoStartAppText,
                PackageName = string.Empty
            });
            if (_startAppBox.SelectedIndex < 0)
                _startAppBox.SelectedIndex = 0;
        }

        private static CheckBox CreateOption(string text, int x, int y)
        {
            return new ThemedCheckBox
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(284, 30)
            };
        }

        private static ThemedSelectControl CreateCustomSelect(
            int x,
            int y,
            int width)
        {
            return new ThemedSelectControl
            {
                Location = new Point(x, y),
                Size = new Size(width, 32)
            };
        }

        private static ThemedNumberControl CreateCustomNumber(
            int min,
            int max,
            int x,
            int y,
            int width,
            bool showStepButtons)
        {
            var control = new ThemedNumberControl
            {
                Minimum = min,
                Maximum = max,
                Increment = 1,
                ShowStepButtons = showStepButtons,
                Location = new Point(x, y),
                Size = new Size(width, 32)
            };
            control.Value = min;
            return control;
        }

        private static ThemedTextControl CreateCustomText(
            int x,
            int y,
            int width)
        {
            return new ThemedTextControl
            {
                Location = new Point(x, y),
                Size = new Size(width, 32)
            };
        }

        private ThemedButton CreateThemedButton(
            string text,
            bool primary,
            int x,
            int y,
            int width)
        {
            return new ThemedButton
            {
                Text = text,
                Primary = primary,
                ForeColor = _theme.TextPrimary,
                Location = new Point(x, y),
                Size = new Size(width, 34)
            };
        }
    }
}
