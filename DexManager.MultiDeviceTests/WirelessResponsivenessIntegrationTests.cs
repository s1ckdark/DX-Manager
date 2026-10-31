using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DexManager.Models;
using DexManager.Services;
using DexManager.Utils;

internal static class WirelessResponsivenessIntegrationTests
{
    private static int checks;
    private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; Console.WriteLine("PASS " + name); }
    private static int FakeAdb(string[] args)
    {
        if (args[0] == "devices") { Console.WriteLine("List of devices attached"); return 0; }
        if (args[0] == "mdns") { Console.WriteLine("List of discovered mdns services"); return 0; }
        if (args[0] == "connect")
        {
            using (var started = EventWaitHandle.OpenExisting(Environment.GetEnvironmentVariable("DXM_WIRELESS_TEST_EVENT"))) started.Set();
            Thread.Sleep(700);
            Console.Error.WriteLine("fixture connection refused"); return 1;
        }
        return 1;
    }
    private static int Main(string[] args)
    {
        if (args.Length > 0) return FakeAdb(args);
        var eventName = "Local\\DXManager-WirelessFixture-" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable("DXM_WIRELESS_TEST_EVENT", eventName);
        var runner = new ProcessRunner(new LogService());
        try
        {
            using (var started = new EventWaitHandle(false, EventResetMode.AutoReset, eventName))
            {
                var settings = AppSettings.CreateDefault();
                settings.Connection.Mode = AdbConnectionMode.Wireless;
                settings.Connection.WirelessHost = "192.0.2.45";
                settings.Connection.WirelessPort = 37201;
                settings.Connection.AutoReconnect = true;
                settings.GetOrCreateDeviceWirelessConnection("fixture-A", true);
                var log = new LogService(); var store = new SettingsService(log);
                // Every subprocess is this fixture; it cannot reach a phone or an ADB server.
                var adb = new AdbService(Assembly.GetExecutingAssembly().Location, 15000, runner, log);
                var wifi = new WirelessAdbService(adb, store, settings, log);
                wifi.InitializeTarget();
                var reconnect = Task.Run(() => wifi.TryReconnect(false));
                Check(started.WaitOne(5000), "actual auto reconnect reaches the simulated slow ADB command");
                var elapsed = Stopwatch.StartNew();
                var serial = wifi.SelectedSerial;
                var profile = wifi.GetDeviceProfile("fixture-A", false);
                wifi.IsTransitionGenerationCurrent(0);
                elapsed.Stop();
                Check(elapsed.ElapsedMilliseconds < 250, "UI state/profile reads do not wait for the network operation");
                Check(serial == "192.0.2.45:37201" && profile.DeviceIdentity == "fixture-A", "read state remains scoped to the selected phone");
                Check(reconnect.Wait(8000) && !reconnect.Result, "failed reconnect completes without a spurious connection");
                var due = (DateTime)typeof(WirelessAdbService).GetField("_nextReconnectAttemptUtc", Hidden).GetValue(wifi);
                Check((due - DateTime.UtcNow).TotalSeconds >= 4, "five-second retry interval starts after failure completes");
                Check(!wifi.TryReconnect(false) && !started.WaitOne(0), "cooldown prevents immediate repeat connect commands");

                wifi.SynchronizeTargetWithSettings();
                reconnect = Task.Run(() => wifi.TryReconnect(false));
                Check(started.WaitOne(5000), "explicit settings synchronization allows a fresh connection attempt");
                settings.Connection.Mode = AdbConnectionMode.Usb;
                profile.Mode = AdbConnectionMode.Usb;
                elapsed.Restart(); wifi.SynchronizeTargetWithSettings(); elapsed.Stop();
                Check(elapsed.ElapsedMilliseconds < 250 && wifi.SelectedSerial.Length == 0,
                    "USB selection updates immediately during the slow reconnect");
                Check(reconnect.Wait(8000) && !reconnect.Result, "old connection attempt cannot restore the cleared target");
                Check(wifi.SelectedSerial.Length == 0 && profile.Mode == AdbConnectionMode.Usb,
                    "failed background work preserves the changed USB policy");
            }
            Console.WriteLine("PASS wireless responsiveness integration: " + checks); return 0;
        }
        catch (Exception ex) { Console.WriteLine(ex); return 1; }
        finally { runner.BeginShutdown(); Environment.SetEnvironmentVariable("DXM_WIRELESS_TEST_EVENT", null); }
    }
}
