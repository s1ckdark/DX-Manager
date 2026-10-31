using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using DexManager.Forms;
using DexManager.Models;
using DexManager.Services;

internal static class OnboardingUiIntegrationTests
{
    private static int checks;
    private static readonly Assembly Product = typeof(MainForm).Assembly;
    private static void Check(bool value, string name)
    {
        if (!value) throw new Exception(name);
        checks++; Console.WriteLine("PASS " + name);
    }
    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
    private static bool Confirm(Form owner, bool accept, string capture)
    {
        Exception failure = null;
        bool handled = false;
        using (var timer = new Timer { Interval = 50 })
        {
            timer.Tick += delegate
            {
                if (handled) return;
                var dialog = Application.OpenForms.Cast<Form>().FirstOrDefault(
                    f => f.GetType().FullName == "DexManager.Forms.DesktopDialog");
                if (dialog == null) return;
                handled = true; timer.Stop();
                try
                {
                    var all = Descendants(dialog).ToArray();
                    var box = all.OfType<CheckBox>().Single();
                    var primary = all.OfType<Button>().Single(b => b.DialogResult == DialogResult.OK);
                    var secondary = all.OfType<Button>().Single(b => b.DialogResult == DialogResult.Cancel);
                    Check(!box.Checked && !primary.Enabled,
                        "fresh consent requires the acknowledgement checkbox");
                    Check(ReferenceEquals(dialog.AcceptButton, secondary),
                        "the default Enter action cannot accept an unchecked notice");
                    Check(all.OfType<Label>().Any(label => label.Text.Length > 250),
                        "the real first-run notice contains its complete localized explanation");
                    using (var image = new Bitmap(dialog.Width, dialog.Height))
                    {
                        dialog.DrawToBitmap(image, new Rectangle(Point.Empty, dialog.Size));
                        image.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, capture + ".png"), ImageFormat.Png);
                    }
                    if (accept)
                    {
                        box.Checked = true;
                        Check(primary.Enabled, "checking acknowledgement enables Confirm and start");
                        primary.PerformClick();
                    }
                    else secondary.PerformClick();
                }
                catch (Exception ex) { failure = ex; dialog.DialogResult = DialogResult.Cancel; dialog.Close(); }
            };
            timer.Start();
            var type = Product.GetType("DexManager.Forms.DesktopDialogs");
            var accepted = (bool)type.GetMethod("ConfirmFirstRun").Invoke(null, new object[] { owner });
            if (failure != null) throw failure;
            Check(handled, "the actual first-run modal was displayed");
            return accepted;
        }
    }
    [STAThread] private static int Main()
    {
        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            LocalizationService.Apply(AppLanguage.Korean);
            foreach (var theme in new[] { AppTheme.Light, AppTheme.Dark })
            {
                Product.GetType("DexManager.Forms.ThemeColors").GetMethod("Use")
                    .Invoke(null, new object[] { theme });
                var directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "consent-" + theme);
                var state = Path.Combine(directory, "onboarding.json");
                if (File.Exists(state)) File.Delete(state);
                var service = new PortableOnboardingService(directory);
                Check(service.ShouldShowNoticeForLaunch(), "fresh state shows consent in " + theme);
                using (var owner = new Form { Text = "Isolated consent fixture", ShowInTaskbar = false,
                    StartPosition = FormStartPosition.Manual, Location = new Point(-32000, -32000),
                    ClientSize = new Size(1200, 768) })
                {
                    owner.Show(); Application.DoEvents();
                    Check(!Confirm(owner, false, "Consent-Decline-" + theme), "declining the notice does not accept it");
                    Check(!File.Exists(state) && service.ShouldShowNoticeForLaunch(),
                        "declining leaves the next launch gated by consent");
                    Check(Confirm(owner, true, "Consent-Accept-" + theme), "checked confirmation accepts the notice");
                    string error;
                    Check(service.TryAccept(out error), "acceptance is saved successfully: " + error);
                    Check(!new PortableOnboardingService(directory).ShouldShowNoticeForLaunch(),
                        "relaunch respects the saved acknowledgement");
                }
            }
            Console.WriteLine("PASS actual first-run consent UI: " + checks);
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("FAIL " + ex);
            return 1;
        }
    }
}
