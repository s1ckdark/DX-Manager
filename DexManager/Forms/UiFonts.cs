using System.Drawing;
using System.Drawing.Text;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

namespace DexManager.Forms
{
    internal static class UiFonts
    {
        private static readonly PrivateFontCollection Fonts = new PrivateFontCollection();
        private static readonly List<System.IntPtr> FontMemory = new List<System.IntPtr>();
        private static readonly FontFamily Regular;
        private static readonly FontFamily SemiBold;

        [DllImport("gdi32.dll")]
        private static extern System.IntPtr AddFontMemResourceEx(System.IntPtr data, uint size,
            System.IntPtr reserved, ref uint count);

        static UiFonts()
        {
            foreach (var name in new[] { "Regular", "SemiBold" })
            {
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
                    "DexManager.Resources.Fonts.DXStoreUI-" + name + ".ttf"))
                {
                    if (stream == null) throw new System.InvalidOperationException("Bundled UI font is missing.");
                    var data = new byte[stream.Length];
                    int offset = 0;
                    while (offset < data.Length) offset += stream.Read(data, offset, data.Length - offset);
                    var memory = Marshal.AllocHGlobal(data.Length);
                    Marshal.Copy(data, 0, memory, data.Length);
                    FontMemory.Add(memory);
                    uint count = 0;
                    AddFontMemResourceEx(memory, (uint)data.Length, System.IntPtr.Zero, ref count);
                    Fonts.AddMemoryFont(memory, data.Length);
                }
            }
            foreach (var family in Fonts.Families)
            {
                if (family.Name == "DX Store UI Regular") Regular = family;
                if (family.Name.StartsWith("DX Store UI SemiBold", System.StringComparison.Ordinal)) SemiBold = family;
            }
            if (Regular == null || SemiBold == null)
                throw new System.InvalidOperationException("Bundled UI font could not be loaded: " + string.Join(", ", System.Linq.Enumerable.Select(Fonts.Families, f => f.Name)));
        }

        public static Font Create(
            float size,
            FontStyle style = FontStyle.Regular)
        {
            return new Font((style & FontStyle.Bold) != 0 ? SemiBold : Regular,
                size, style & ~FontStyle.Bold, GraphicsUnit.Point);
        }

        public static Font Pixels(float size, bool semiBold = false)
        {
            if (size <= 18F) size += 1F;
            return Create(size * 0.75F, semiBold ? FontStyle.Bold : FontStyle.Regular);
        }
    }
}
