using System;
using System.Drawing;
using System.Windows.Forms;
using DexManager.Services;

namespace DexManager.Forms
{
    public sealed partial class SettingsForm
    {
        private static int _desktopCardWidth = 662;
        private Control _companionCard;
        private Button _desktopSaveButton, _desktopCloseButton;
        private ThemedButton _settingsPaneToggle;
        private bool _settingsPaneCompact;

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            DesktopSurface.Paint(e.Graphics, ClientRectangle);
        }

        private Control CreateSettingRow(Control page, UiSymbol symbol, string title,
            string description, Control editor, string fixedValue = null)
        {
            var card = new RoundedPanel { Size = new Size(CardWidth, 72), Radius = 5,
                Margin = new Padding(0, 0, 0, 8), BackColor = Color.Transparent };
            card.Controls.Add(new SymbolView { Symbol = symbol, ForeColor = _theme.TextPrimary,
                Bounds = new Rectangle(18, 26, 22, 22) });
            var rightWidth = editor is ThemedCheckBox ? 52 : 224;
            card.Controls.Add(new UiLabel { Text = title, Bounds = new Rectangle(56, 13, CardWidth - rightWidth - 85, 26),
                ForeColor = _theme.TextPrimary, BackColor = Color.Transparent, AutoEllipsis = true, Tag = "primary" });
            card.Controls.Add(new UiLabel { Text = description, Bounds = new Rectangle(56, 39, CardWidth - rightWidth - 85, 30),
                Font = UiFonts.Create(8.5F), ForeColor = _theme.TextTertiary, BackColor = Color.Transparent, AutoEllipsis = true, Tag = "primary" });
            if (editor != null)
            {
                editor.Dock = DockStyle.None;
                editor.Bounds = new Rectangle(CardWidth - rightWidth - 18, 20, rightWidth, 38);
                editor.AccessibleName = title;
                card.Controls.Add(editor);
            }
            else card.Controls.Add(new UiLabel { Text = fixedValue, TextAlign = ContentAlignment.MiddleRight,
                Bounds = new Rectangle(CardWidth - 242, 18, 224, 40), ForeColor = _theme.TextPrimary, BackColor = Color.Transparent, Tag = "primary" });
            page.Controls.Add(card); return card;
        }

        private CheckBox AddSettingToggle(Control page, string key, string descriptionKey, UiSymbol symbol)
        {
            var box = new ThemedCheckBox { Text = string.Empty, BackColor = _theme.CardBackground };
            CreateSettingRow(page, symbol, LocalizationService.Get(key),
                string.IsNullOrEmpty(descriptionKey) ? string.Empty : LocalizationService.Get(descriptionKey), box);
            return box;
        }

        private Control BuildCompanionPage()
        {
            var page = CreatePage();
            if (_companionCard != null) page.Controls.Add(_companionCard);
            return page;
        }

        private void LayoutDesktopSettings()
        {
            if (_contentHost == null || _sidebar == null) return;
            var s = Math.Max(1F, CurrentAutoScaleDimensions.Width / 96F);
            var pane=_settingsPaneCompact?64:236;
            _titleLabel.Bounds = new Rectangle((int)(28*s), (int)(18*s), ClientSize.Width - (int)(56*s), (int)(42*s));
            _descriptionLabel.Bounds = new Rectangle((int)(29*s), (int)(61*s), ClientSize.Width - (int)(58*s), (int)(24*s));
            _sidebar.Bounds = new Rectangle(0, (int)(84*s), (int)(pane*s), ClientSize.Height - (int)(146*s));
            _contentHost.Bounds = new Rectangle((int)((pane+24)*s), (int)(99*s), ClientSize.Width - (int)((pane+24)*s), ClientSize.Height - (int)(161*s));
            _bottomPanel.Bounds = new Rectangle(0, ClientSize.Height - (int)(62*s), ClientSize.Width, (int)(62*s));
            _saveStatusLabel.Bounds = new Rectangle((int)(260*s), 0, Math.Max(80, ClientSize.Width - (int)(530*s)), (int)(58*s));
            var bodyRight=_contentHost.Right-SystemInformation.VerticalScrollBarWidth-_pages[0].Padding.Horizontal-4;
            _desktopCloseButton.Bounds = new Rectangle(bodyRight-(int)(112*s), (int)(10*s), (int)(112*s), (int)(40*s));
            UpdateStoreCardSizes();
            _desktopSaveButton.Bounds = new Rectangle(ClientSize.Width - (int)(260*s), (int)(10*s), (int)(112*s), (int)(40*s));
            if (_navigationButtons.Count > 5)
                _navigationButtons[5].Top = _sidebar.Height - (int)(48*s);
            foreach(var button in _navigationButtons){button.Width=(int)((pane-8)*s);button.Text=_settingsPaneCompact?string.Empty:(string)button.Tag;}
            if(_settingsPaneToggle!=null)_settingsPaneToggle.Bounds=new Rectangle((int)(4*s),(int)(8*s),(int)(48*s),(int)(36*s));
        }
    }
}
