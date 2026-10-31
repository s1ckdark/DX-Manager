using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using DexManager.Models;
using DexManager.Services;
using DexManager.Utils;

namespace DexManager.Forms
{
    public sealed partial class SettingsForm : Form, IMessageFilter
    {
        public void ApplyCurrentTheme()
        {
            _theme = ThemeColors.Current;
            BackColor = _theme.WindowBackground;
            _contentHost.BackColor = Color.Transparent;
            _bottomPanel.BackColor = Color.Transparent;
            _titleLabel.ForeColor = _theme.TextPrimary;
            _titleLabel.BackColor = Color.Transparent;
            _descriptionLabel.ForeColor = _theme.TextTertiary;
            _descriptionLabel.BackColor = Color.Transparent;
            _saveStatusLabel.BackColor = _theme.WindowBackground;
            if (!_saveStatusLabel.Visible)
                _saveStatusLabel.ForeColor = _theme.TextTertiary;
            ApplySurfaceTheme(
                _bottomPanel,
                _theme.WindowBackground);

            _sidebar.BackColor = _theme.WindowBackground;
            _sidebar.FillColor = _theme.NavigationBackground;
            _sidebar.BorderColor = _theme.NavigationBackground;
            ApplySurfaceTheme(
                _sidebar,
                _theme.NavigationBackground);

            foreach (var page in _pages)
            {
                page.BackColor = Color.Transparent;
                foreach (Control heading in page.Controls) if (heading is Label) heading.ForeColor = (heading.Tag as string == "primary" || heading.Font.FontFamily.Name.EndsWith("SemiBold", StringComparison.Ordinal)) ? _theme.TextPrimary : _theme.TextTertiary;
                foreach (Control control in page.Controls)
                {
                    var card = control as RoundedPanel;
                    if (card == null) continue;
                    card.BackColor = _theme.WindowBackground;
                    card.FillColor = _theme.CardBackground;
                    card.BorderColor = _theme.CardBorder;
                    ApplySurfaceTheme(card, _theme.CardBackground);
                }
            }

            ApplyStoreCardTheme();
            if (_deviceDiagnosticsCardsPanel != null) { _deviceDiagnosticsCardsPanel.BackColor=_theme.CardBackground; ApplySurfaceTheme(_deviceDiagnosticsCardsPanel, _theme.CardBackground); }
            if (_deviceDiagnosticsStatusLabel != null) { _deviceDiagnosticsStatusLabel.BackColor=_theme.CardBackground; _deviceDiagnosticsStatusLabel.ForeColor=_theme.TextSecondary; }
            Invalidate(true);
        }

        private void ApplySurfaceTheme(
            Control parent,
            Color surface)
        {
            foreach (Control control in parent.Controls)
            {
                var diagnosticCard = control as RoundedPanel;
                if (diagnosticCard != null && control.Tag as string == "diagnostic-card")
                {
                    diagnosticCard.FillColor = _theme.CardSoft;
                    diagnosticCard.BorderColor = _theme.CardBorder;
                    ApplySurfaceTheme(diagnosticCard, _theme.CardSoft);
                    diagnosticCard.Invalidate(true);
                    continue;
                }
                var panel = control as Panel;
                if (panel != null && !(control is RoundedPanel))
                    panel.BackColor = surface;

                var label = control as Label;
                var link=control as LinkLabel;
                if(link!=null){link.LinkColor=_theme.IsDark?Color.FromArgb(174,160,255):_theme.Accent;link.ActiveLinkColor=_theme.IsDark?Color.FromArgb(205,193,255):_theme.AccentHover;link.VisitedLinkColor=link.LinkColor;}
                if (label != null)
                {
                    label.BackColor = surface;
                    if (label != _saveStatusLabel)
                    {
                        label.ForeColor = label.Tag as string == "diagnostic-title" ? _theme.TextPrimary : (string.Equals(label.Tag as string, "primary", StringComparison.Ordinal) || label.Font.FontFamily.Name.EndsWith("SemiBold", StringComparison.Ordinal))
                            ? _theme.TextSecondary
                            : _theme.TextTertiary;
                    }
                }

                if (control is SymbolView) control.ForeColor = _theme.TextSecondary;
                var radio = control as RadioButton;
                if (radio != null)
                {
                    radio.BackColor = surface;
                    radio.ForeColor = _theme.TextPrimary;
                }

                var check = control as ThemedCheckBox;
                if (check != null)
                {
                    check.BackColor = surface;
                    check.ForeColor = _theme.TextPrimary;
                }

                var button = control as ThemedButton;
                if (button != null)
                {
                    button.BackColor = surface;
                    button.ForeColor = _theme.TextSecondary;
                }

                if (control.HasChildren)
                    ApplySurfaceTheme(control, surface);
                control.Invalidate();
            }
        }

        private Control BuildSidebar()
        {
            _sidebar = new RoundedPanel
            {
                Location = new Point(16, 16),
                Size = new Size(188, 664),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom |
                    AnchorStyles.Left,
                Radius = 0,
                BackColor = _theme.NavigationBackground,
                FillColor = _theme.NavigationBackground,
                BorderColor = _theme.CardBorder
            };

            var labels = new[]
            {
                LocalizationService.Get("Settings.General"),
                LocalizationService.Get("Settings.Connection"),
                LocalizationService.Get("Settings.Paths"),
                LocalizationService.Get("Settings.Keyboard"),
                LocalizationService.Get("Settings.Diagnostics"),
                LocalizationService.Get("Settings.About"),
                "Companion"
            };
            var order = new[] { 0, 2, 4, 1, 5, 6, 3 };
            var symbols = new[] { UiSymbol.Settings, UiSymbol.Link, UiSymbol.Folder, UiSymbol.Keyboard, UiSymbol.Information, UiSymbol.Information, UiSymbol.Phone };
            for (var index = 0; index < labels.Length; index++)
            {
                var pageIndex = index;
                var button = new ThemedButton
                {
                    Text = labels[index],
                    Tag = labels[index],
                    Primary = index == 0,
                    CornerRadius = 5,
                    NavigationStyle = true,
                    ShowNavigationDot = false,
                    Symbol = symbols[index],
                    TabStop = true,
                    Location = new Point(4, 44 + order[index] * 42),
                    Size = new Size(226, 36),
                    BackColor = _theme.NavigationBackground,
                    ForeColor = _theme.TextSecondary
                };
                button.Click += delegate { ShowPage(pageIndex); };
                _navigationButtons.Add(button);
                _sidebar.Controls.Add(button);
            }
            _settingsPaneToggle = new ThemedButton { Symbol = UiSymbol.Menu, Text = string.Empty, NavigationStyle = true, AccessibleName = LocalizationService.IsKorean ? "메뉴 접기 / 펼치기" : "Toggle navigation pane" };
            _settingsPaneToggle.Click += delegate { _settingsPaneCompact = !_settingsPaneCompact; LayoutDesktopSettings(); };
            _sidebar.Controls.Add(_settingsPaneToggle);
            return _sidebar;
        }

        private void ShowPage(int index)
        {
            if (index < 0 || index >= _pages.Count) return;
            using (UiRendering.Freeze(_contentHost))
            {
            _activePageIndex = index;
            for (var i = 0; i < _pages.Count; i++)
            {
                _pages[i].Visible = i == index;
                _navigationButtons[i].Primary = i == index;
                _navigationButtons[i].Invalidate();
            }
            _pages[index].BringToFront();
            ThemedScrollBar.RefreshFor((ScrollableControl)_pages[index]);
            }
            if (index == 4 || index == 6)
            {
                RefreshDeviceDiagnosticsAsync();
                RefreshDisplayCleanupStatusAsync();
            }
        }
    }
}
