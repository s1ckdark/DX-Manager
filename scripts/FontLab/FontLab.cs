using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

using DexManager.Forms;

[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.6.2")]
[assembly: AssemblyTitle("DX Font Lab")]
[assembly: AssemblyVersion("1.0.0.0")]

namespace DexManager.Models { public enum AppTheme { Auto, Light, Dark } }

namespace DexManager.Forms
{
    internal static class Experiment
    {
        internal static int Mode;
#if FONTLAB2
        internal static bool Plus { get { return ComparisonProfile.Plus; } }
        internal static readonly string[] Names = { ComparisonProfile.Label };
        internal static TextRenderingHint Hint { get { return ComparisonProfile.Hint; } }
#else
        internal static bool Plus { get { return Mode == 3 || Mode == 4; } }
        internal static readonly string[] Names = {
            "A · 현재 Pretendard / GDI", "B · 맑은 고딕 / 같은 GDI",
            "C · Pretendard / 정수 픽셀 GDI", "D · Pretendard / GDI+ 회색 AA",
            "E · Pretendard / GDI+ ClearType", "F · 현재 방식 / 패널 합성 제외" };
        internal static TextRenderingHint Hint { get { return Mode == 4 ? TextRenderingHint.ClearTypeGridFit : TextRenderingHint.AntiAliasGridFit; } }
#endif
    }

    // A calls the original font implementation. Only the experimental modes differ.
    internal static class UiFonts
    {
        internal static Font Create(float size, FontStyle style = FontStyle.Regular)
        {
#if FONTLAB2
            return ComparisonProfile.SystemFont ? new Font("Malgun Gothic",size,style,GraphicsUnit.Point) : BaselineUiFonts.Create(size,style);
#else
            if (Experiment.Mode == 1) return new Font("Malgun Gothic", size, style, GraphicsUnit.Point);
            // Quantize the point size to whole pixels at the company's 96-DPI setting.
            if (Experiment.Mode == 2) size = (float)Math.Round(size * 96F / 72F) * 72F / 96F;
            return BaselineUiFonts.Create(size, style);
#endif
        }
        internal static Font Pixels(float size, bool semiBold = false)
        {
            if (size <= 18F) size += 1F;
            return Create(size * 0.75F, semiBold ? FontStyle.Bold : FontStyle.Regular);
        }
    }

    internal static class FontProbe
    {
        internal static readonly List<string> Registrations = new List<string>();
        [DllImport("gdi32.dll")] private static extern IntPtr AddFontMemResourceEx(IntPtr data, uint size, IntPtr reserved, ref uint count);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] private static extern int GetTextFace(IntPtr dc, int count, StringBuilder name);
        [DllImport("user32.dll")] private static extern bool SystemParametersInfo(uint action, uint param, out int value, uint flags);
        internal static IntPtr Register(IntPtr data, uint size, IntPtr reserved, ref uint count)
        {
            var handle = AddFontMemResourceEx(data, size, reserved, ref count);
            Registrations.Add("GDI AddFontMemResourceEx: success=" + (handle != IntPtr.Zero) + ", fonts=" + count + ", bytes=" + size);
            return handle;
        }
        internal static string Face(Font font)
        {
            using (var bitmap = new Bitmap(8, 8)) using (var graphics = Graphics.FromImage(bitmap)) {
                var dc = graphics.GetHdc(); IntPtr hf = IntPtr.Zero, old = IntPtr.Zero;
                try { hf = font.ToHfont(); old = SelectObject(dc, hf); var name = new StringBuilder(256); GetTextFace(dc, name.Capacity, name); return name.ToString(); }
                finally { if (old != IntPtr.Zero) SelectObject(dc, old); if (hf != IntPtr.Zero) DeleteObject(hf); graphics.ReleaseHdc(dc); }
            }
        }
        internal static string EnvironmentReport(Control control)
        {
            int smooth, type, contrast, orientation;
            SystemParametersInfo(0x004A, 0, out smooth, 0); SystemParametersInfo(0x200A, 0, out type, 0);
            SystemParametersInfo(0x200C, 0, out contrast, 0); SystemParametersInfo(0x2012, 0, out orientation, 0);
            using (var g = control.CreateGraphics())
                return "OS: " + Environment.OSVersion + "\r\nCLR: " + Environment.Version + " / x64=" + Environment.Is64BitProcess
                    + "\r\nDPI: " + g.DpiX + " x " + g.DpiY + "\r\nScreen: " + Screen.FromControl(control).Bounds
                    + "\r\nFont smoothing: " + smooth + ", type=" + type + ", contrast=" + contrast + ", orientation=" + orientation;
        }
    }

    // In A/B/C/F every call forwards its original flags to WinForms TextRenderer.
    internal static class TestTextRenderer
    {
        internal static StringFormat Format(TextFormatFlags flags)
        {
            var f = new StringFormat(StringFormat.GenericDefault);
            f.Alignment = (flags & TextFormatFlags.HorizontalCenter) != 0 ? StringAlignment.Center : (flags & TextFormatFlags.Right) != 0 ? StringAlignment.Far : StringAlignment.Near;
            f.LineAlignment = (flags & TextFormatFlags.VerticalCenter) != 0 ? StringAlignment.Center : (flags & TextFormatFlags.Bottom) != 0 ? StringAlignment.Far : StringAlignment.Near;
            if ((flags & TextFormatFlags.WordBreak) == 0) f.FormatFlags |= StringFormatFlags.NoWrap;
            f.Trimming = (flags & TextFormatFlags.EndEllipsis) != 0 ? StringTrimming.EllipsisCharacter : StringTrimming.None;
            f.HotkeyPrefix = (flags & TextFormatFlags.NoPrefix) != 0 ? HotkeyPrefix.None : HotkeyPrefix.Show;
            return f;
        }
        internal static void DrawText(Graphics g, string text, Font font, Rectangle bounds, Color color, TextFormatFlags flags)
        {
            if (!Experiment.Plus) { TextRenderer.DrawText(g, text, font, bounds, color, flags); return; }
            var state = g.Save();
            try { g.TextRenderingHint = Experiment.Hint; using (var f = Format(flags)) using (var b = new SolidBrush(color)) g.DrawString(text ?? "", font, b, bounds, f); }
            finally { g.Restore(state); }
        }
        internal static Size MeasureText(string text, Font font)
        {
            if (!Experiment.Plus) return TextRenderer.MeasureText(text, font);
            using (var b = new Bitmap(8, 8)) using (var g = Graphics.FromImage(b)) return MeasureText(g, text, font, Size.Empty, TextFormatFlags.Default);
        }
        internal static Size MeasureText(Graphics g, string text, Font font, Size proposed, TextFormatFlags flags)
        {
            if (!Experiment.Plus) return TextRenderer.MeasureText(g, text, font, proposed, flags);
            var state = g.Save();
            try { g.TextRenderingHint = Experiment.Hint; using (var f = Format(flags)) {
                var size = g.MeasureString(text ?? "", font, new SizeF(proposed.Width > 0 ? proposed.Width : 10000, proposed.Height > 0 ? proposed.Height : 10000), f);
                return Size.Ceiling(size);
            } } finally { g.Restore(state); }
        }
    }

    internal sealed class TestLabel : Label
    {
        public override Size GetPreferredSize(Size proposedSize)
        {
            if (!Experiment.Plus || !AutoSize) return base.GetPreferredSize(proposedSize);
            int width = MaximumSize.Width > 0 ? MaximumSize.Width : proposedSize.Width;
            using (var bitmap = new Bitmap(8,8)) using (var graphics = Graphics.FromImage(bitmap))
                return TestTextRenderer.MeasureText(graphics, Text, Font, new Size(width,0), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            if (!Experiment.Plus) { base.OnPaint(e); return; }
            var flags = TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak;
            if (TextAlign == ContentAlignment.MiddleLeft) flags |= TextFormatFlags.VerticalCenter;
            if (TextAlign == ContentAlignment.MiddleCenter) flags |= TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter;
            if (AutoEllipsis) flags |= TextFormatFlags.EndEllipsis;
            TestTextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor, flags);
        }
    }

    internal sealed class SampleWindow : Form
    {
        internal readonly int Page;
        internal readonly string ModeName;
        private readonly List<Font> _fonts = new List<Font>();
        private readonly bool _dark;
        private float _scale = 1F;
        internal SampleWindow(int page, bool dark)
        {
            Page = page; _dark = dark; ModeName = Experiment.Names[Experiment.Mode];
            ThemeColors.Use(dark ? DexManager.Models.AppTheme.Dark : DexManager.Models.AppTheme.Light);
            Text = "DX Font Lab — " + ModeName + " — " + new[] { "메인", "도움말", "글자표" }[page];
            Font=UiFonts.Pixels(14); AutoScaleMode = AutoScaleMode.Dpi; AutoScaleDimensions = new SizeF(96, 96);
            DoubleBuffered = true; BackColor = ThemeColors.Current.WindowBackground;
            UiWindowStyle.ApplyStoreSize(this, page == 1 ? 1040 : 1200, page == 1 ? 760 : 800);
            if (page == 1) { FormBorderStyle = FormBorderStyle.Sizable; MaximizeBox = true; }
            MinimumSize = page == 1 ? new Size(760,560) : new Size(900, 650); StartPosition = FormStartPosition.Manual;
            Rebuild(); Resize += delegate { if (IsHandleCreated) Rebuild(); };
        }
        protected override void OnPaintBackground(PaintEventArgs e) { DesktopSurface.Paint(e.Graphics, ClientRectangle); }
        private Rectangle R(int x, int y, int width, int height) { return new Rectangle(P(x), P(y), P(width), P(height)); }
        private int P(int value) { return (int)Math.Round(value * _scale); }
        private Font F(float pixels, bool bold = false)
        {
            using (var original = UiFonts.Pixels(pixels, bold)) {
                var f = UiFonts.Create(original.SizeInPoints * _scale, bold ? FontStyle.Bold : FontStyle.Regular); _fonts.Add(f); return f;
            }
        }
        private TestLabel L(Control parent, string text, int x, int y, int w, int h, float px, bool bold = false, bool secondary = false)
        {
            var label = new TestLabel { Text = text, Bounds = R(x,y,w,h), Font = F(px,bold), BackColor = Color.Transparent,
                ForeColor = secondary ? ThemeColors.Current.TextSecondary : ThemeColors.Current.TextPrimary, TextAlign = ContentAlignment.MiddleLeft };
            parent.Controls.Add(label); return label;
        }
        private RoundedPanel Card(Control parent, int x, int y, int w, int h, int radius = 20)
        {
            var card = new RoundedPanel { Bounds = R(x,y,w,h), Radius = P(radius), BackColor = Color.Transparent,
                FillColor = ThemeColors.Current.CardBackground, BorderColor = ThemeColors.Current.CardBorder, SurfaceOpacity = Page == 0 ? 244 : 255 };
            parent.Controls.Add(card); return card;
        }
        private ThemedButton B(Control parent, string text, int x, int y, int w, int h, bool primary = false, UiSymbol symbol = UiSymbol.None)
        {
            var b = new ThemedButton { Text = text, Bounds = R(x,y,w,h), Font = F(14), Primary = primary, Symbol = symbol, CornerRadius = P(7) };
            parent.Controls.Add(b); return b;
        }
        private void AddIcon(Control parent, UiSymbol symbol, int x, int y, int size, bool tile)
        { parent.Controls.Add(new SymbolView { Symbol = symbol, Tile = tile, Bounds = R(x,y,size,size), ForeColor = ThemeColors.Current.TextSecondary }); }
        private void Section(RoundedPanel card, string title, UiSymbol symbol)
        { AddIcon(card,symbol,24,25,38,true); L(card,title,74,25,(int)(card.Width/_scale)-98,38,20,true); }
        internal void Rebuild()
        {
            SuspendLayout(); while (Controls.Count > 0) Controls[0].Dispose();
            foreach (var font in _fonts) font.Dispose(); _fonts.Clear();
            float dpi = Math.Max(1F, CurrentAutoScaleDimensions.Width / 96F);
            _scale = Page == 1 ? dpi : Math.Max(0.4F, Math.Min(dpi, Math.Min(ClientSize.Width / 1200F, ClientSize.Height / 768F)));
            if (Page == 0) BuildMain(); else if (Page == 1) BuildHelp(); else BuildGlyphs();
            ResumeLayout(true); Invalidate(true);
        }
        private void BuildMain()
        {
            var nav = Card(this,0,0,236,(int)(ClientSize.Height/_scale),0);
            nav.FillColor = nav.BorderColor = ThemeColors.Current.NavigationBackground;
            var body = new BufferedPanel { Bounds = R(236,0,964,(int)(ClientSize.Height/_scale)), BackColor = Color.Transparent, AutoScroll = true };
            Controls.Add(body);
            L(nav,"DX Manager",60,28,168,46,18,true);
            B(nav,"DeX",4,96,224,38,false,UiSymbol.Display);
            B(nav,"단일창 1",4,138,224,38,true,UiSymbol.Launch);
            B(nav,"단일창 2",4,180,224,38,false,UiSymbol.Launch);
            B(nav,"단일창 3",4,222,224,38,false,UiSymbol.Launch);
            B(nav,"도움말 · 문제 해결",4,638,224,38,false,UiSymbol.Help);
            B(nav,"설정",4,682,224,38,false,UiSymbol.Settings);
            const int width = 904; var hero = Card(body,28,40,width,214,24);
            AddIcon(hero,UiSymbol.Phone,28,24,166,true);
            L(hero,"Galaxy S26 Ultra",230,40,550,38,30,true);
            B(hero,"Galaxy S26 Ultra",230,90,230,40);
            B(hero,"USB + Wi-Fi",472,90,130,40);
            B(hero,"",614,90,40,40,false,UiSymbol.Refresh);
            hero.Controls.Add(new StatusRing { Bounds = R(230,142,26,26), StatusColor = ThemeColors.Current.Accent });
            L(hero,"단일창 1 준비됨",266,140,390,30,17,true);
            B(hero,"단일창 시작",706,128,170,56,true,UiSymbol.Launch);
            var display = Card(body,28,274,408,258); Section(display,"디스플레이 설정 · 1",UiSymbol.Display);
            L(display,"해상도",24,84,170,22,13,false,true); L(display,"DPI",214,84,170,22,13,false,true);
            B(display,"1600 × 900",24,114,170,40); B(display,"150",214,114,170,40);
            L(display,"비트레이트 (Mbps)",24,166,180,22,13,false,true); L(display,"최대 FPS",214,166,170,22,13,false,true);
            B(display,"20",24,196,170,40); B(display,"60",214,196,170,40);
            var options = Card(body,454,274,478,258); Section(options,"실행 옵션",UiSymbol.Launch);
            string[] optionsText = { "폰 화면 끄기", "HID 키보드", "HID 마우스", "선택 앱 강제 종료", "동적 창 크기", "잠자기 방지" };
            UiSymbol[] syms = { UiSymbol.Phone,UiSymbol.Keyboard,UiSymbol.Mouse,UiSymbol.Close,UiSymbol.Display,UiSymbol.Moon };
            for (int i=0;i<6;i++) options.Controls.Add(new ThemedCheckBox { Text = optionsText[i], Font = F(14), Symbol = syms[i], Checked = i != 3,
                Bounds = R(i<3?24:245,96+(i%3)*47,205,40), BackColor = ThemeColors.Current.CardBackground });
            var app = Card(body,28,550,width,190); Section(app,"시작 앱",UiSymbol.List);
            B(app,"삼성 브라우저 (com.sec.android.app.sbrowser)",24,86,568,40);
            B(app,"앱 목록 불러오기",604,86,184,40,false,UiSymbol.Refresh);
            B(app,"프로필 저장",798,86,82,40).Enabled=false;
            L(app,"› 고급 옵션",28,132,300,28,14,false,true);
        }
        private void BuildHelp()
        {
            int h = (int)(ClientSize.Height/_scale);
            var nav=Card(this,28,16,300,h-96); var content=Card(this,344,16,(int)(ClientSize.Width/_scale)-372,h-96);
            L(nav,"도움말 · 문제 해결",14,28,274,26,18,true);
            L(nav,"중요한 사용 방법과 복구 절차",14,57,274,24,13,false,true);
            string[] titles={"처음 사용하기","DeX와 단일창","연결과 실행 문제","파일 전송과 DX Companion","종료와 휴대폰 화면 복구","전체 사용 설명서 · FAQ"};
            string[] summaries={"개발자 옵션, USB 디버깅과 첫 연결","두 실행 방식과 시작 앱 선택","기기가 보이지 않거나 실행되지 않을 때","PC와 휴대폰 사이의 양방향 전송","정상 종료와 남은 가상 화면 정리","전체 사용 방법과 자주 묻는 질문"};
            UiSymbol[] icons={UiSymbol.Folder,UiSymbol.Display,UiSymbol.Link,UiSymbol.List,UiSymbol.Power,UiSymbol.Information};
            var menu=new BufferedFlowPanel {Bounds=R(10,94,280,h-200),AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,BackColor=Color.Transparent}; nav.Controls.Add(menu);
            for(int i=0;i<6;i++) {
                var b=new ThemedButton {Text=titles[i],Font=F(14),Primary=i==0,Symbol=icons[i],MenuCardStyle=true,SecondaryText=summaries[i],Size=new Size(menu.ClientSize.Width-4,P(74)),Margin=new Padding(0,0,0,P(8)),CornerRadius=P(6)};
                menu.Controls.Add(b);
            }
            var article=new BufferedFlowPanel {Bounds=R(24,24,(int)(content.Width/_scale)-48,(int)(content.Height/_scale)-48),AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,BackColor=Color.Transparent,Padding=new Padding(0,0,P(8),P(16))}; content.Controls.Add(article);
            ThemedScrollBar.Attach(menu); ThemedScrollBar.Attach(article);
            int w=article.Width-P(24);
            using(UiRendering.Freeze(content)) {
            article.Controls.Add(Block("처음 사용하기",26,true,w,6)); article.Controls.Add(Block("개발자 옵션, USB 디버깅과 첫 연결",13,false,w,18));
            article.Controls.Add(Block("DX Manager는 Android 개발자 옵션과 ADB를 사용해 연결한 휴대폰의 DeX 및 단일창을 실행합니다. 휴대폰에서 개발자 옵션과 USB 디버깅을 먼저 켜세요.",15,false,w,14));
            article.Controls.Add(Block("첫 연결",18,true,w,10));
            string[] steps={"휴대폰을 USB로 연결하고 USB 디버깅을 켭니다.","휴대폰에 표시되는 USB 디버깅 허용 창에서 허용을 누릅니다.","DX Manager 상단의 휴대폰 목록에서 연결된 기기를 선택합니다."};
            for(int i=0;i<3;i++) {
                var row=new Panel {Width=w,Height=P(26),BackColor=Color.Transparent,Margin=new Padding(0,0,0,P(10))}; article.Controls.Add(row);
                var badge=Card(row,0,0,26,26,13); badge.FillColor=badge.BorderColor=ThemeColors.Current.AccentSoft;
                var n=L(badge,(i+1).ToString(),0,0,26,26,15,true); n.TextAlign=ContentAlignment.MiddleCenter; n.ForeColor=ThemeColors.Current.Accent;
                var line=Block(steps[i],15,false,w-P(40),0); line.Location=new Point(P(40),0); row.Controls.Add(line); row.Height=Math.Max(P(26),line.PreferredSize.Height);
            }
            var warning=new RoundedPanel {Radius=P(12),Width=w,BackColor=Color.Transparent,FillColor=ThemeColors.Current.AccentSoft,BorderColor=ThemeColors.Current.Accent,Margin=new Padding(0,0,0,P(14)),Padding=new Padding(P(14),P(12),P(14),P(12)),AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink};
            var stack=new BufferedFlowPanel {Location=new Point(P(14),P(12)),Width=w-P(28),AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,BackColor=Color.Transparent};
            stack.Controls.Add(Block("⚠  휴대폰에서 승인하지 않은 경우",15,true,stack.Width,6));
            stack.Controls.Add(Block("휴대폰의 화면 잠금을 풀고 USB 디버깅 승인 창을 확인하세요. 승인 전에는 DeX와 파일 전송을 시작할 수 없습니다.",15,false,stack.Width,0));
            warning.Controls.Add(stack); article.Controls.Add(warning);
            }
            B(this,"닫기",(int)(ClientSize.Width/_scale)-138,h-60,110,40,true).Click+=delegate { Close(); };
        }
        private TestLabel Block(string text,float px,bool bold,int width,int bottom)
        {return new TestLabel {Text=text,AutoSize=true,MaximumSize=new Size(width,0),Font=F(px,bold),BackColor=Color.Transparent,ForeColor=bold?ThemeColors.Current.TextPrimary:ThemeColors.Current.TextSecondary,Margin=new Padding(0,0,0,P(bottom))};}
        private void BuildGlyphs()
        {
            L(this,"같은 문장 · 굵기 · 작은 크기 비교",28,16,1120,42,26,true);
            L(this,"가나다라마바사 아자차카타파하 / 한글 획 누락, 윗부분·아랫부분, 숫자를 확인하세요.",28,60,1120,35,15,false,true);
            var card=Card(this,28,108,1144,444);
            float[] sizes={12,13,14,15,17,20,26,30};
            for(int i=0;i<sizes.Length;i++) {
                L(card,((int)sizes[i])+" px",18,10+i*52,75,44,13,false,true);
                L(card,"휴대폰 연결 · 설정 0123456789",100,10+i*52,520,44,sizes[i]);
                L(card,"가나다라마바사 · 준비됨 USB",650,10+i*52,470,44,sizes[i],true);
            }
            var logCard=Card(this,28,574,1144,140);
            L(logCard,"로그 본문 비교 (Consolas / RichTextBox, 모든 모드에서 고정)",18,8,1050,28,14,true);
            var logFont=new Font("Consolas",13F*0.75F,GraphicsUnit.Point); _fonts.Add(logFont);
            logCard.Controls.Add(new RichTextBox { ReadOnly=true,BorderStyle=BorderStyle.None,Font=logFont,Bounds=R(18,44,1108,72),
                BackColor=ThemeColors.Current.CardBackground,ForeColor=ThemeColors.Current.TextPrimary,Text="2026-10-06 10:00:00.000 [INFO] 휴대폰 연결을 확인했습니다.\n[INFO] DX Manager를 시작합니다. ABCDEFG abcdefg 0123456789", DetectUrls=false });
        }
        internal string Describe()
        {
            using(var regular=UiFonts.Pixels(14)) using(var semi=UiFonts.Pixels(14,true))
                return ModeName + " / " + new[]{"Main","Help","Glyphs"}[Page] + " / " + (_dark?"Dark":"Light")
#if FONTLAB2
                    + "\r\n" + ComparisonProfile.Notes
#endif
                    + "\r\nOuter=" + Size + ", Client=" + ClientSize + ", FontScale=" + _scale
                    + "\r\nRegular=" + regular.FontFamily.Name + ", GDI face=" + FontProbe.Face(regular)
                    + "\r\nSemiBold=" + semi.FontFamily.Name + ", GDI face=" + FontProbe.Face(semi)
                    + "\r\n" + FontProbe.EnvironmentReport(this);
        }
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing); if(disposing) { foreach(var f in _fonts) f.Dispose(); _fonts.Clear(); }
        }
    }

    internal sealed class Controller : Form
    {
        [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr window,IntPtr dc,uint flags);
        private readonly ComboBox _mode, _page;
        private readonly CheckBox _dark;
        private readonly TextBox _status;
        private readonly List<string> _results=new List<string>();
        private SampleWindow _sample;
        internal Controller()
        {
            Text="DX 글꼴 테스트 · 비교 선택"; ClientSize=new Size(335,535); FormBorderStyle=FormBorderStyle.FixedSingle; MaximizeBox=false;
            Font=new Font("Malgun Gothic",10F); StartPosition=FormStartPosition.Manual; Location=new Point(12,40);
            Controls.Add(new Label {Text="글꼴 / 표시 방식",Bounds=new Rectangle(12,12,310,24)});
            _mode=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Bounds=new Rectangle(12,40,310,32)}; _mode.Items.AddRange(Experiment.Names); _mode.SelectedIndex=0;
            Controls.Add(new Label {Text="확인할 화면",Bounds=new Rectangle(12,82,310,24)});
            _page=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Bounds=new Rectangle(12,110,310,32)}; _page.Items.AddRange(new object[]{"메인 (1200 × 800)","도움말 (1040 × 760)","글자표 / 로그 비교"}); _page.SelectedIndex=0;
#if FONTLAB2
            Text="DX 글꼴 테스트 2 · " + ComparisonProfile.Id;
            _mode.Enabled=false; _page.SelectedIndex=2;
#endif
            _dark=new CheckBox {Text="어두움 테마",Bounds=new Rectangle(12,150,310,28)};
            Controls.AddRange(new Control[]{_mode,_page,_dark});
            AddButton("정상으로 기록",12,190,150,38,delegate {Record("정상");}); AddButton("깨짐으로 기록",172,190,150,38,delegate {Record("깨짐");});
            AddButton("화면 복사",12,238,150,38,CopyScreen); AddButton("다시 그리기",172,238,150,38,delegate {_sample.Rebuild();});
            AddButton("결과 전체 복사",12,284,310,38,delegate {Clipboard.SetText(ResultsText()); _status.Text="결과를 복사했습니다. 메모장이나 채팅에 붙여넣으세요.\r\n\r\n"+ResultsText();});
            _status=new TextBox {Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,Bounds=new Rectangle(12,334,310,185)}; Controls.Add(_status);
            _mode.SelectedIndexChanged+=delegate {OpenSample();}; _page.SelectedIndexChanged+=delegate {OpenSample();}; _dark.CheckedChanged+=delegate {OpenSample();};
            Shown+=delegate {OpenSample();}; FormClosed+=delegate {if(_sample!=null)_sample.Dispose();};
        }
        private void AddButton(string text,int x,int y,int w,int h,Action action)
        {var b=new Button {Text=text,Bounds=new Rectangle(x,y,w,h)}; b.Click+=delegate {try {action();} catch(Exception e) {_status.Text="실패: "+e.Message;}}; Controls.Add(b);}
        private void OpenSample()
        {
            try {
                if(_sample!=null)_sample.Dispose(); Experiment.Mode=_mode.SelectedIndex;
                _sample=new SampleWindow(_page.SelectedIndex,_dark.Checked); var area=Screen.FromControl(this).WorkingArea;
                _sample.Location=new Point(Math.Min(area.Right-_sample.Width,Math.Max(Right+12,area.Left+(area.Width-_sample.Width)/2)),area.Top+24);
                _sample.Show(this); _status.Text=_sample.Describe()+"\r\n\r\n정상/깨짐을 기록하고 결과 전체 복사를 누르세요. 종료하면 기록은 사라집니다.";
            } catch(Exception e) {_status.Text=e.ToString();}
        }
        private string ResultsText()
        {return string.Join("\r\n",FontProbe.Registrations)+"\r\n\r\n"+string.Join("\r\n\r\n",_results);}
        private void Record(string result)
        {_results.Add(DateTime.Now.ToString("s")+" · "+result+"\r\n"+_sample.Describe()); _status.Text="기록됨: "+result+" (총 "+_results.Count+"개)\r\n결과 전체 복사로 가져오세요.\r\n\r\n"+_sample.Describe();}
        private Bitmap ScreenImage()
        {
            _sample.BringToFront(); _sample.Activate(); _sample.Refresh(); Application.DoEvents();
            var image=new Bitmap(_sample.Width,_sample.Height);
            using(var g=Graphics.FromImage(image)) {
                var dc=g.GetHdc(); bool ok;
                try {ok=PrintWindow(_sample.Handle,dc,0);} finally {g.ReleaseHdc(dc);}
                if(!ok) {image.Dispose(); throw new Exception("테스트 창 이미지 복사에 실패했습니다. Windows 화면 캡처를 이용하세요.");}
            }
            return image;
        }
        private void CopyScreen()
        {
            using(var image=ScreenImage()) Clipboard.SetImage(image);
            Record("화면 복사 (그림판에 붙여넣기)");
        }
        internal void VerifyNavigation()
        {
            for(int i=0;i<Experiment.Names.Length;i++) {
                _mode.SelectedIndex=i; _page.SelectedIndex=i%3; _dark.Checked=i%2!=0; Application.DoEvents();
                if(_sample==null || _sample.IsDisposed || _sample.Page!=i%3 || _sample.ModeName!=Experiment.Names[i]) throw new Exception("Controller navigation failed");
            }
            Record("자동 화면 전환 검증"); if(!ResultsText().Contains("자동 화면 전환 검증")) throw new Exception("Result recording failed");
            using(var image=ScreenImage()) using(var ms=new MemoryStream()) {image.Save(ms,ImageFormat.Png);
                if(ms.Length<10000) throw new Exception("Screen capture failed"); Console.WriteLine("PNG:controller-screen|"+Convert.ToBase64String(ms.ToArray()));}
            Console.WriteLine("PASS: controller navigation, result recording and actual screen capture.");
        }
    }

    internal static class FontLabProgram
    {
        [STAThread] private static int Main(string[] args)
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            try {
                if(args.Length>0 && args[0]=="--verify") return Verify();
                Application.Run(new Controller()); return 0;
            } catch(Exception e) {if(args.Length>0) Console.WriteLine(e); else MessageBox.Show(e.ToString(),"DX Font Lab"); return 1;}
        }
        private static int Verify()
        {
            using(var font=UiFonts.Pixels(14)) {}
            foreach(var r in FontProbe.Registrations) Console.WriteLine(r);
#if FONTLAB2
            if(!ComparisonProfile.SystemFont && (FontProbe.Registrations.Count!=2 || FontProbe.Registrations.Exists(r=>r.Contains("success=False") || r.Contains("fonts=0,")))) throw new Exception("GDI registration failed.");
#else
            if(FontProbe.Registrations.Count!=2 || FontProbe.Registrations.Exists(r=>r.Contains("success=False") || r.Contains("fonts=0,"))) throw new Exception("GDI registration failed.");
#endif
            for(int mode=0;mode<Experiment.Names.Length;mode++) for(int page=0;page<3;page++) for(int theme=0;theme<2;theme++) {
                Experiment.Mode=mode;
                using(var sample=new SampleWindow(page,theme==1)) {
                    sample.Location=new Point(-20000,-20000); sample.ShowInTaskbar=false; sample.Show(); sample.Location=new Point(-20000,-20000); Application.DoEvents();
                    using(var regular=UiFonts.Pixels(14)) {
                        string wanted;
#if FONTLAB2
                        using(var systemFont=new Font("Malgun Gothic",10F)) wanted=ComparisonProfile.SystemFont?systemFont.FontFamily.Name:ComparisonProfile.Regular;
#else
                        using(var systemFont=new Font("Malgun Gothic",10F)) wanted=mode==1?systemFont.FontFamily.Name:"DX Store UI Regular";
#endif
                        if(regular.FontFamily.Name!=wanted || FontProbe.Face(regular)!=wanted) throw new Exception("Unexpected font fallback: "+regular.FontFamily.Name+" / "+FontProbe.Face(regular));
                    }
                    using(var semi=UiFonts.Pixels(14,true)) {
#if FONTLAB2
                        if(!ComparisonProfile.SystemFont && FontProbe.Face(semi)!=ComparisonProfile.SemiBold) throw new Exception("SemiBold fallback: "+FontProbe.Face(semi));
#else
                        if(mode!=1 && !FontProbe.Face(semi).StartsWith("DX Store UI SemiBold")) throw new Exception("SemiBold fallback");
#endif
                    }
                    sample.Width-=12; Application.DoEvents(); sample.Width+=12; sample.Refresh(); Application.DoEvents();
                    using(var image=new Bitmap(sample.Width,sample.Height)) {
                        sample.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));
                        using(var ms=new MemoryStream()) { image.Save(ms,ImageFormat.Png); var bytes=ms.ToArray(); if(bytes.Length<10000) throw new Exception("Invalid preview");
                            Console.WriteLine("PNG:"+((char)('A'+mode))+"-"+page+"-"+(theme==1?"dark":"light")+"|"+Convert.ToBase64String(bytes)); }
                    }
                    Console.WriteLine("PASS: "+sample.Describe().Replace("\r\n"," | "));
                }
            }
            using(var controller=new Controller()) {controller.Show(); Application.DoEvents(); controller.VerifyNavigation();}
            Console.WriteLine("PASS: "+(Experiment.Names.Length*6)+" combinations; native GDI face; both weights; resize/repaint; UI capture."); return 0;
        }
    }
}
