using System.Drawing;
using System.Windows.Forms;

namespace DexManager.Forms
{
    internal static class UiWindowStyle
    {
        private static readonly Size StandardClientSize =
            new Size(920, 696);

        public static void ApplyStoreSize(Form form, int width, int height)
        {
            form.FormBorderStyle = FormBorderStyle.FixedSingle;
            form.MaximizeBox = false;
            form.SizeGripStyle = SizeGripStyle.Hide;
            using (var graphics = form.CreateGraphics())
            {
                var area = Screen.FromControl(form).WorkingArea;
                var dpi = graphics.DpiX / 96F;
                var fit = System.Math.Min(1F, System.Math.Min((area.Width - 32F) / (width * dpi), (area.Height - 32F) / (height * dpi)));
                form.Size = new Size((int)(width * dpi * fit), (int)(height * dpi * fit));
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(area.Left + (area.Width - form.Width) / 2, area.Top + (area.Height - form.Height) / 2);
                form.Shown += delegate { form.Location = new Point(area.Left + (area.Width - form.Width) / 2, area.Top + (area.Height - form.Height) / 2); };
            }
        }
        public static void ApplyDesktopSize(Form form, int width, int height)
        {
            form.FormBorderStyle = FormBorderStyle.FixedSingle;
            form.MaximizeBox = false;
            form.SizeGripStyle = SizeGripStyle.Hide;
            using (var graphics = form.CreateGraphics())
            {
                var scale = graphics.DpiX / 96F;
                var area = Screen.FromControl(form).WorkingArea;
                form.ClientSize = new Size(
                    System.Math.Min((int)(width * scale), System.Math.Max(480, area.Width - 32)),
                    System.Math.Min((int)(height * scale), System.Math.Max(400, area.Height - 48)));
            }
        }
        public static void ApplyFixedStandardSize(Form form)
        {
            form.FormBorderStyle = FormBorderStyle.FixedSingle;
            form.MaximizeBox = false;
            form.SizeGripStyle = SizeGripStyle.Hide;
            form.ClientSize = StandardClientSize;
        }
    }
}
