using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DexManager.Forms
{
    internal enum UiSymbol { None, Display, Launch, Phone, Keyboard, Mouse, Moon,
        Close, Settings, Help, Refresh, List, Link, Folder, Information, Power, Language, Palette, Usb, Wifi, Menu, Chevron, Options, Apps, ScreenOff, ScreenOn, Fullscreen, ResetSize, Capture, Manager, ChevronUp, Save }

    internal static class DesktopSurface
    {
        public static void PaintBehind(Graphics graphics, Control child)
        {
            if(child.Parent==null){graphics.Clear(ThemeColors.Current.WindowBackground);return;}
            var state=graphics.Save();
            graphics.TranslateTransform(-child.Left,-child.Top);
            PaintSurface(graphics,child.Parent);
            graphics.Restore(state);
            var overlay=child as RoundedPanel;
            if(overlay!=null && overlay.BackdropSource!=null && overlay.BackdropSource!=child && overlay.BackdropSource.Visible) {
                var source=overlay.BackdropSource;
                var offset=child.PointToClient(source.PointToScreen(Point.Empty));
                state=graphics.Save();
                graphics.SetClip(new Rectangle(offset,source.Size),CombineMode.Intersect);
                graphics.TranslateTransform(offset.X,offset.Y);
                PaintSurface(graphics,source);
                graphics.Restore(state);
            }
        }
        private static void PaintSurface(Graphics graphics, Control surface)
        {
            if(surface is Form) {
                if(surface is MainForm || surface is SettingsForm || surface is HelpForm || surface is DocumentationForm || surface is LogForm || surface is FileTransferStatusForm || surface is PhoneTransferStatusForm)Paint(graphics,surface.ClientRectangle);
                else using(var brush=new SolidBrush(surface.BackColor))graphics.FillRectangle(brush,surface.ClientRectangle);
                return;
            }
            PaintBehind(graphics,surface);
            var card=surface as RoundedPanel;
            if(card!=null){card.PaintSurface(graphics);return;}
            if(surface.BackColor.A==255)using(var brush=new SolidBrush(surface.BackColor))graphics.FillRectangle(brush,surface.ClientRectangle);
        }
        private static Bitmap _background;
        private static Size _backgroundSize;
        private static bool _backgroundDark;
        public static void Paint(Graphics graphics, Rectangle bounds)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0) return;
            if (_background == null || _backgroundSize != bounds.Size || _backgroundDark != ThemeColors.Current.IsDark)
            {
                if (_background != null) _background.Dispose();
                _backgroundSize = bounds.Size;
                _backgroundDark = ThemeColors.Current.IsDark;
                _background = new Bitmap(bounds.Width, bounds.Height);
                using (var cachedGraphics = Graphics.FromImage(_background))
                    PaintBackground(cachedGraphics, new Rectangle(Point.Empty, bounds.Size));
            }
            graphics.DrawImageUnscaled(_background, bounds.Location);
        }
        private static void PaintBackground(Graphics graphics, Rectangle bounds)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0) return;
            var theme = ThemeColors.Current;
            using (var brush = new LinearGradientBrush(bounds,
                theme.NavigationBackground, theme.WindowBackground, 22F))
            {
                brush.InterpolationColors = new ColorBlend
                {
                    Colors = new[] { theme.NavigationBackground, theme.WindowBackground,
                        theme.IsDark ? Color.FromArgb(24, 21, 38) : Color.FromArgb(249, 246, 255) },
                    Positions = new[] { 0F, 0.55F, 1F }
                };
                graphics.FillRectangle(brush, bounds);
            }
            var scale = bounds.Width / 1200F;
            Ambient(graphics, new RectangleF(-240*scale,-300*scale,680*scale,680*scale),
                theme.IsDark ? Color.FromArgb(45,80,115,175) : Color.FromArgb(62,142,200,255));
            Ambient(graphics, new RectangleF(bounds.Width-500*scale,-360*scale,760*scale,760*scale),
                theme.IsDark ? Color.FromArgb(72,106,92,255) : Color.FromArgb(82,106,92,255));
            Ambient(graphics, new RectangleF(bounds.Width-400*scale,bounds.Height-300*scale,620*scale,620*scale),
                theme.IsDark ? Color.FromArgb(25,100,160,150) : Color.FromArgb(56,156,226,209));
        }

        private static void Ambient(Graphics graphics, RectangleF bounds, Color color)
        {
            using(var path=new GraphicsPath())
            {
                path.AddEllipse(bounds);
                using(var brush=new PathGradientBrush(path))
                {
                    brush.CenterColor=color;
                    brush.SurroundColors=new[]{Color.FromArgb(0,color)};
                    graphics.FillPath(brush,path);
                }
            }
        }

        public static GraphicsPath RoundedPath(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            int diameter = Math.Min(Math.Min(bounds.Width, bounds.Height), radius * 2);
            if (diameter <= 0) { path.AddRectangle(bounds); return path; }
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure(); return path;
        }

        // Outline icons are independent of newer Windows icon fonts.
        public static void DrawIcon(Graphics graphics, UiSymbol symbol, Rectangle bounds, Color color)
        {
            if (FluentIconRenderer.TryDraw(graphics, symbol, bounds, color)) return;
            var state = graphics.Save();
            graphics.TranslateTransform(bounds.X, bounds.Y);
            graphics.ScaleTransform(bounds.Width / 24F, bounds.Height / 24F);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(color, bounds.Width > 48 ? 36F / bounds.Width : 1.4F) { StartCap = LineCap.Round,
                EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                switch (symbol)
                {
                    case UiSymbol.Menu:
                        graphics.DrawLine(pen,4,6,20,6); graphics.DrawLine(pen,4,12,20,12); graphics.DrawLine(pen,4,18,20,18); break;
                    case UiSymbol.Wifi:
                        graphics.DrawArc(pen, 1, 4, 22, 20, 225, 90);
                        graphics.DrawArc(pen, 5, 9, 14, 13, 225, 90);
                        graphics.DrawArc(pen, 9, 14, 6, 6, 225, 90);
                        using (var dot = new SolidBrush(color)) graphics.FillEllipse(dot, 11, 20, 2, 2);
                        break;
                    case UiSymbol.Usb:
                        graphics.DrawRectangle(pen,6,7,12,15); graphics.DrawRectangle(pen,8,1,8,6);
                        graphics.DrawLine(pen,10,3,10,5); graphics.DrawLine(pen,14,3,14,5); break;
                    case UiSymbol.Language:
                        graphics.DrawLines(pen,new[]{new Point(2,21),new Point(7,7),new Point(12,21)});graphics.DrawLine(pen,4,16,10,16);
                        graphics.DrawLine(pen,13,5,22,5);graphics.DrawLine(pen,18,2,18,5);graphics.DrawArc(pen,13,5,8,12,0,140);graphics.DrawLine(pen,15,9,22,17); break;
                    case UiSymbol.Palette:
                        graphics.DrawArc(pen,3,3,18,18,0,320);graphics.DrawArc(pen,13,13,8,8,100,180);
                        graphics.DrawEllipse(pen,7,7,1,1);graphics.DrawEllipse(pen,12,5,1,1);graphics.DrawEllipse(pen,17,8,1,1);break;
                    case UiSymbol.Display:
                        graphics.DrawRectangle(pen, 3, 4, 18, 13);
                        graphics.DrawLine(pen, 12, 17, 12, 21); graphics.DrawLine(pen, 8, 21, 16, 21); break;
                    case UiSymbol.Phone:
                        using (var path = RoundedPath(new Rectangle(6, 2, 12, 20), 2)) graphics.DrawPath(pen, path);
                        graphics.DrawLine(pen, 10, 19, 14, 19); graphics.DrawEllipse(pen, 11.5F, 4, 1, 1); break;
                    case UiSymbol.Options:
                    case UiSymbol.Launch:
                        graphics.DrawPolygon(pen, new[] { new Point(7, 3), new Point(20, 12), new Point(7, 21) }); break;
                    case UiSymbol.Keyboard:
                        graphics.DrawRectangle(pen, 2, 6, 20, 13);
                        for (int y = 9; y <= 12; y += 3) for (int x = 5; x <= 19; x += 3)
                            graphics.DrawLine(pen, x, y, x + 1, y);
                        graphics.DrawLine(pen, 8, 16, 16, 16); break;
                    case UiSymbol.Mouse:
                        graphics.DrawEllipse(pen, 6, 2, 12, 20); graphics.DrawLine(pen, 12, 3, 12, 9); break;
                    case UiSymbol.Moon:
                        graphics.DrawArc(pen, 3, 3, 18, 18, 30, 255);
                        graphics.DrawArc(pen, 9, -1, 14, 17, 60, 185); break;
                    case UiSymbol.Close:
                        graphics.DrawEllipse(pen, 3, 3, 18, 18); graphics.DrawLine(pen, 8, 8, 16, 16);
                        graphics.DrawLine(pen, 16, 8, 8, 16); break;
                    case UiSymbol.Refresh:
                        graphics.DrawArc(pen, 3, 3, 18, 18, 35, 290);
                        graphics.DrawLines(pen, new[] { new Point(15, 3), new Point(21, 3), new Point(21, 9) }); break;
                    case UiSymbol.Apps:
                    case UiSymbol.List:
                        for (int y = 4; y <= 20; y += 7) { graphics.DrawRectangle(pen, 3, y, 3, 3);
                            graphics.DrawLine(pen, 10, y + 1, 21, y + 1); } break;
                    case UiSymbol.Settings:
                        graphics.DrawEllipse(pen, 6, 6, 12, 12); graphics.DrawEllipse(pen, 10, 10, 4, 4);
                        for (int i = 0; i < 8; i++) { double a = i * Math.PI / 4;
                            graphics.DrawLine(pen, 12 + (float)Math.Cos(a) * 6, 12 + (float)Math.Sin(a) * 6,
                                12 + (float)Math.Cos(a) * 9, 12 + (float)Math.Sin(a) * 9); } break;
                    case UiSymbol.Help:
                        graphics.DrawArc(pen, 7, 3, 10, 9, 175, 270); graphics.DrawLine(pen, 12, 12, 12, 16);
                        graphics.DrawEllipse(pen, 11.5F, 20, 1, 1); break;
                    case UiSymbol.Folder:
                        graphics.DrawPolygon(pen, new[] { new Point(3, 5), new Point(10, 5), new Point(12, 8),
                            new Point(21, 8), new Point(21, 20), new Point(3, 20) }); break;
                    case UiSymbol.Power:
                        graphics.DrawArc(pen, 3, 4, 18, 18, -55, 290); graphics.DrawLine(pen, 12, 2, 12, 12); break;
                    case UiSymbol.Link:
                        graphics.DrawArc(pen, 2, 8, 13, 8, 90, 270); graphics.DrawArc(pen, 9, 8, 13, 8, -90, 270);
                        graphics.DrawLine(pen, 8, 12, 16, 12); break;
                    case UiSymbol.Information:
                        graphics.DrawEllipse(pen, 3, 3, 18, 18); graphics.DrawLine(pen, 12, 11, 12, 17);
                        graphics.DrawEllipse(pen, 11.5F, 7, 1, 1); break;
                }
            }
            graphics.Restore(state);
        }
    }

    internal sealed class SymbolView : Control
    {
        public UiSymbol Symbol { get; set; }
        public bool Tile { get; set; }
        public SymbolView()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            BackColor = Color.Transparent;
            TabStop = false;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var colors = ThemeColors.Current;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (Tile)
            {
                using (var path = DesktopSurface.RoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), Width > 64 ? 36 : Width / 2))
                using (var brush = new SolidBrush(colors.AccentSoft)) e.Graphics.FillPath(brush, path);
            }
            if (Tile && Symbol == UiSymbol.Phone && Width > 64)
            {
                var scale = Width / 166F;
                var body = new Rectangle((int)((Width - 66*scale)/2), (int)((Height - 112*scale)/2), (int)(66*scale), (int)(112*scale));
                using (var path = DesktopSurface.RoundedPath(body, (int)(14*scale)))
                using (var brush = new LinearGradientBrush(body, colors.AccentSoft, colors.IsDark ? Color.FromArgb(63,70,99) : Color.FromArgb(210,210,255), 45F))
                using (var pen = new Pen(colors.Accent, 2*scale)) { e.Graphics.FillPath(brush,path); e.Graphics.DrawPath(pen,path); }
                using (var brush = new SolidBrush(colors.Accent))
                {
                    e.Graphics.FillEllipse(brush, Width/2F-3*scale, body.Top+8*scale, 6*scale, 6*scale);
                    e.Graphics.FillRectangle(brush, Width/2F-9*scale, body.Bottom-9*scale, 18*scale, 2*scale);
                }
                return;
            }
            var iconSize = System.Math.Min(System.Math.Min(Width, Height), System.Math.Max(1, (int)System.Math.Round(20F * e.Graphics.DpiX / 96F)));
            DesktopSurface.DrawIcon(e.Graphics, Symbol, new Rectangle((Width-iconSize)/2, (Height-iconSize)/2,
                iconSize, iconSize), Tile ? colors.Accent : ForeColor);
        }
    }
}
