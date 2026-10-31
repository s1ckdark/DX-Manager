using System.Drawing;
using Microsoft.Win32;
using DexManager.Models;

namespace DexManager.Forms
{
    internal sealed class ThemePalette
    {
        public bool IsDark { get; set; }
        public Color WindowBackground { get; set; }
        public Color NavigationBackground { get; set; }
        public Color CardBackground { get; set; }
        public Color CardSoft { get; set; }
        public Color CardBorder { get; set; }
        public Color TextPrimary { get; set; }
        public Color TextSecondary { get; set; }
        public Color TextTertiary { get; set; }
        public Color Accent { get; set; }
        public Color AccentHover { get; set; }
        public Color AccentPressed { get; set; }
        public Color AccentSoft { get; set; }
        public Color ControlBorder { get; set; }
        public Color DisabledBackground { get; set; }
        public Color DisabledText { get; set; }
    }

    internal static class ThemeColors
    {
        private static ThemePalette _current = CreateLight();

        public static ThemePalette Current
        {
            get { return _current; }
        }

        public static ThemePalette Use(AppTheme theme)
        {
            _current = Resolve(theme);
            return _current;
        }

        public static ThemePalette Resolve(AppTheme theme)
        {
            var dark = theme == AppTheme.Dark ||
                (theme == AppTheme.Auto && IsWindowsDarkMode());
            return dark ? CreateDark() : CreateLight();
        }

        private static bool IsWindowsDarkMode()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    var value = key == null
                        ? null
                        : key.GetValue("AppsUseLightTheme");
                    return value is int && (int)value == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        private static ThemePalette CreateLight()
        {
            return new ThemePalette
            {
                IsDark = false,
                WindowBackground = Color.FromArgb(248, 250, 255),
                NavigationBackground = Color.FromArgb(242, 246, 255),
                CardBackground = Color.FromArgb(248, 250, 255),
                CardSoft = Color.FromArgb(253, 254, 255),
                CardBorder = Color.FromArgb(220, 225, 238),
                TextPrimary = Color.FromArgb(27, 27, 27),
                TextSecondary = Color.FromArgb(99, 100, 102),
                TextTertiary = Color.FromArgb(142, 144, 147),
                Accent = Color.FromArgb(103, 88, 229),
                AccentHover = Color.FromArgb(118, 107, 255),
                AccentPressed = Color.FromArgb(77, 78, 234),
                AccentSoft = Color.FromArgb(237, 234, 255),
                ControlBorder = Color.FromArgb(211, 211, 214),
                DisabledBackground = Color.FromArgb(241, 241, 243),
                DisabledText = Color.FromArgb(156, 156, 162)
            };
        }

        private static ThemePalette CreateDark()
        {
            return new ThemePalette
            {
                IsDark = true,
                WindowBackground = Color.FromArgb(18, 21, 34),
                NavigationBackground = Color.FromArgb(32, 36, 50),
                CardBackground = Color.FromArgb(37, 42, 58),
                CardSoft = Color.FromArgb(43, 48, 65),
                CardBorder = Color.FromArgb(65, 70, 85),
                TextPrimary = Color.FromArgb(242, 242, 243),
                TextSecondary = Color.FromArgb(208, 211, 218),
                TextTertiary = Color.FromArgb(180, 184, 194),
                Accent = Color.FromArgb(139, 124, 246),
                AccentHover = Color.FromArgb(139, 124, 246),
                AccentPressed = Color.FromArgb(103, 88, 229),
                AccentSoft = Color.FromArgb(57, 56, 86),
                ControlBorder = Color.FromArgb(72, 77, 90),
                DisabledBackground = Color.FromArgb(47, 47, 51),
                DisabledText = Color.FromArgb(112, 112, 118)
            };
        }
    }
}
