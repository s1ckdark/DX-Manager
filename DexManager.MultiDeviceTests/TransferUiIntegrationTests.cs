using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using DexManager.Forms;
using DexManager.Models;
using DexManager.Services;
using DexManager.Utils;

internal static class TransferUiIntegrationTests
{
    private static int checks;
    private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; Console.WriteLine("PASS " + name); }
    private static object Field(object instance, string name) { return instance.GetType().GetField(name, Hidden).GetValue(instance); }
    private static object Model(Type type, params object[] args) { return Activator.CreateInstance(type, Hidden, null, args, null); }
    private static void Capture(Form form, string name)
    {
        form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-5000, -5000);
        form.Show(); form.PerformLayout(); Application.DoEvents();
        using (var bitmap = new Bitmap(form.Width, form.Height))
        using (var stream = new MemoryStream())
        {
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
            bitmap.Save(stream, ImageFormat.Png);
            Console.WriteLine("IMAGE:" + name + ":" + Convert.ToBase64String(stream.ToArray()));
        }
        ValidateBounds(form);
    }
    private static void ValidateBounds(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            if (!child.Visible) continue;
            Check(child.Left >= 0 && child.Top >= 0 && child.Right <= parent.ClientSize.Width &&
                child.Bottom <= parent.ClientSize.Height, "visible transfer control fits its parent: " + child.GetType().Name);
            ValidateBounds(child);
        }
    }
    private static FileTransferProgress PcProgress(FileTransferStage stage)
    {
        var queue = new List<FileTransferQueueEntry>();
        for (var i = 0; i < 5; i++) queue.Add((FileTransferQueueEntry)Model(typeof(FileTransferQueueEntry),
            "사진 " + (i + 1) + " · 긴 한글 파일 이름이 잘리는 경우를 확인합니다.jpg", 1048576L, i == 0));
        return (FileTransferProgress)Model(typeof(FileTransferProgress), 1L, "fixture-request", "fixture-session",
            stage, "사진 1.jpg", "사진 1.jpg", 1048576L, 0, 1, 0, 4, queue, DateTime.UtcNow.AddSeconds(-23), false, 5,
            stage == FileTransferStage.Failed ? "전송 실패 · 대상 폴더에 쓸 수 없습니다." : "");
    }
    private static PhoneTransferProgress PhoneProgress(Guid batch, PhoneTransferStage stage, string name, int count)
    {
        return (PhoneTransferProgress)Model(typeof(PhoneTransferProgress), (long)count + 1, batch, stage,
            name, count, 5, (long)count * 1048576, 5L * 1048576,
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "receive", "Galaxy S26 Ultra"),
            stage == PhoneTransferStage.Failed ? "전송 실패 · 테스트 오류 메시지입니다." : "");
    }
    private static void Resolve(SettingsForm form, AppSettings settings, string identity,
        AdbConnectionMode mode, string expected, params DeviceTransportInfo[] transports)
    {
        settings.GetOrCreateDeviceWirelessConnection(identity, false).Mode = mode;
        var model = new PhysicalDeviceInfo { Identity = identity, DisplayName = "fixture", Transports = transports };
        var optionType = typeof(SettingsForm).GetNestedType("WirelessDeviceOption", BindingFlags.NonPublic);
        var option = Activator.CreateInstance(optionType, new object[] { model });
        var box = Field(form, "_wirelessDeviceBox");
        var items = (IList)box.GetType().GetProperty("Items").GetValue(box, null);
        items.Clear(); items.Add(option); box.GetType().GetProperty("SelectedIndex").SetValue(box, 0, null);
        var actual = (string)typeof(SettingsForm).GetMethod("ResolveStoreTargetSerial", Hidden).Invoke(form, null);
        Check(actual == expected, "actual settings resolves " + identity + " as " + expected);
    }
    private static DeviceTransportInfo Transport(string serial, DeviceTransportKind kind, bool authorized = true)
    {
        return new DeviceTransportInfo { Serial = serial, Kind = kind,
            Status = authorized ? AdbDeviceStatus.Device : AdbDeviceStatus.Unauthorized };
    }
    [STAThread] private static int Main()
    {
        try
        {
            Application.EnableVisualStyles(); LocalizationService.Apply(AppLanguage.Korean);
            var settings = AppSettings.CreateDefault(); var log = new LogService();
            var runner = new ProcessRunner(log); runner.BlockNewProcessesForWindowsShutdown();
            var adb = new AdbService(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "unused-adb.exe"), 1000, runner, log);
            var service = new SettingsService(log); var wifi = new WirelessAdbService(adb, service, settings, log);
            using (var form = new SettingsForm(service, settings, adb, wifi, null, null, null, null, log, null, null, null, null, null, null))
            {
                Resolve(form, settings, "usb-only", AdbConnectionMode.Usb, "fixture-usb", Transport("fixture-usb", DeviceTransportKind.Usb));
                Resolve(form, settings, "tcp-only", AdbConnectionMode.Usb, "192.0.2.1:5555", Transport("192.0.2.1:5555", DeviceTransportKind.Wireless));
                const string tls = "adb-fixture._adb-tls-connect._tcp";
                Resolve(form, settings, "tls-only", AdbConnectionMode.Usb, tls, Transport(tls, DeviceTransportKind.Wireless));
                Resolve(form, settings, "both", AdbConnectionMode.Wireless, "fixture-usb", Transport(tls, DeviceTransportKind.Wireless), Transport("fixture-usb", DeviceTransportKind.Usb));
                Resolve(form, settings, "unauthorized", AdbConnectionMode.Usb, "", Transport(tls, DeviceTransportKind.Wireless, false));
            }
            using (var coordinator = new FileTransferCoordinator(adb.AdbPath, settings, log, new DeviceRuntimeSessionRegistry()))
            {
                foreach (var theme in new[] { AppTheme.Light, AppTheme.Dark })
                {
                    using (var form = new FileTransferStatusForm(coordinator, theme, "Galaxy S26 Ultra"))
                    {
                        foreach (var stage in new[] { FileTransferStage.Transferring, FileTransferStage.Completed, FileTransferStage.Failed })
                        {
                            form.UpdateProgress(PcProgress(stage)); Capture(form, "PC-" + theme + "-" + stage);
                        }
                        Check(((Label)Field(form, "_statusLabel")).Font.SizeInPoints * 96F / 72F >= 19, "PC transfer heading uses the Store20px size");
                        var method = typeof(FileTransferStatusForm).GetProperty("ShowWithoutActivation", Hidden);
                        Check((bool)method.GetValue(form, null), "PC transfer window retains non-activating display");
                    }
                    using (var form = new PhoneTransferStatusForm(theme, "Galaxy S26 Ultra"))
                    {
                        var batch = Guid.NewGuid();
                        for (var i = 0; i < 5; i++) form.UpdateProgress(PhoneProgress(batch, PhoneTransferStage.Receiving, "사진 " + (i + 1) + " · 한글 파일.jpg", i));
                        Capture(form, "Phone-" + theme + "-Receiving");
                        form.UpdateProgress(PhoneProgress(batch, PhoneTransferStage.Completed, "", 5)); Capture(form, "Phone-" + theme + "-Completed");
                        form.UpdateProgress(PhoneProgress(batch, PhoneTransferStage.Failed, "", 5)); Capture(form, "Phone-" + theme + "-Failed");
                        Check(((Label)Field(form, "_statusLabel")).Font.SizeInPoints * 96F / 72F >= 19, "phone transfer heading uses the Store20px size");
                        var bar = Field(form, "_progressBar");
                        Check((int)bar.GetType().GetProperty("Value").GetValue(bar, null) == 0, "failure resets actual progress");
                    }
                }
            }
            Console.WriteLine("PASS transfer UI/management integration: " + checks);
            return 0;
        }
        catch (Exception ex) { Console.WriteLine(ex); return 1; }
    }
}
