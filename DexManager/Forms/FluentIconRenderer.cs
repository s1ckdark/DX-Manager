using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace DexManager.Forms
{
    // Original SVGs/provenance are retained in Resources/FluentIcons.
    // Alpha masks keep the UI independent of OS icon fonts and newer Windows APIs.
    internal static class FluentIconRenderer
    {
        internal static bool Enabled = true;
        private static readonly object Sync = new object();
        private static readonly Dictionary<UiSymbol, Bitmap> Images = new Dictionary<UiSymbol, Bitmap>();

        internal static bool TryDraw(Graphics graphics, UiSymbol symbol, Rectangle bounds, Color color)
        {
            if (!Enabled || symbol == UiSymbol.None || bounds.Width <= 0 || bounds.Height <= 0) return false;
            Bitmap image;
            lock (Sync)
            {
                if (!Images.TryGetValue(symbol, out image))
                {
                    using (var stream = typeof(FluentIconRenderer).Assembly.GetManifestResourceStream(
                        "DexManager.Resources.FluentIcons." + symbol + ".png"))
                    {
                        if (stream != null)
                            using (var decoded = Image.FromStream(stream)) image = new Bitmap(decoded);
                    }
                    Images[symbol] = image;
                }
            }
            if (image == null) return false;
            var state = graphics.Save();
            try
            {
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                using (var attributes = new ImageAttributes())
                {
                    var tint = new ColorMatrix();
                    tint.Matrix00 = color.R / 255F;
                    tint.Matrix11 = color.G / 255F;
                    tint.Matrix22 = color.B / 255F;
                    tint.Matrix33 = color.A / 255F;
                    attributes.SetColorMatrix(tint);
                    attributes.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(image, bounds, 0, 0, image.Width, image.Height,
                        GraphicsUnit.Pixel, attributes);
                }
            }
            finally { graphics.Restore(state); }
            return true;
        }
    }
}