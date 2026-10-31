using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DexManager.Forms;
using DexManager.Models;
using DexManager.Services;
using DexManager.Utils;

internal static class ScrcpyRuntimeIntegrationTests
{
    private static int checks;
    private static void Check(bool value, string name)
    {
        if (!value) throw new Exception(name);
        checks++; Console.WriteLine("PASS " + name);
    }
    private static int Count(string text, string marker)
    {
        int count = 0, offset = 0;
        while ((offset = text.IndexOf(marker, offset, StringComparison.Ordinal)) >= 0)
        { count++; offset += marker.Length; }
        return count;
    }
    private static int Main(string[] args)
    {
        DeviceRuntimeServiceSet runtime = null;
        try
        {
            LocalizationService.Apply(AppLanguage.Korean);
            var scrcpy = args[0];
            var log = new LogService();
            var runner = new ProcessRunner(log);
            var adb = new AdbService(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "not-a-phone-adb.exe"), 1000, runner, log);
            var settings = AppSettings.CreateDefault();
            var store = new SettingsService(log);
            var path = new PathService(store, log, runner);
            var launch = new ScrcpyLaunchCoordinator();
            var factory = new DeviceRuntimeServiceFactory(scrcpy, adb.AdbPath, 3000,
                runner, adb, launch, store, settings, log, new DeviceRuntimeSessionRegistry());
            runtime = factory.Create();
            Check(runtime.Scrcpy.RuntimeInfo.MajorVersion == 5 &&
                runtime.Scrcpy.RuntimeInfo.ReportedVersion == "5.0.1",
                "actual product detects the selected scrcpy full version 5.0.1");
            Check(runtime.Scrcpy.ReadReportedVersion() == "5.0.1",
                "diagnostics read the selected executable rather than a hard-coded version");
            var dex = runtime.Scrcpy.BuildArguments(settings.Scrcpy, 31, "fixture-phone");
            Check(Count(dex, "--hwdec=auto") == 1, "DeX explicitly selects automatic hardware decoding once");
            var method = typeof(SingleWindowService).GetMethod("BuildArguments",
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                new[] { typeof(int), typeof(SingleWindowSlotSettings), typeof(string) }, null);
            for (var slot = 1; slot <= 3; slot++)
            {
                var options = settings.SingleWindowSlots[slot - 1];
                options.StartAppPackage = "com.android.settings";
                var command = (string)method.Invoke(runtime.SingleWindows,
                    new object[] { slot, options, "fixture-phone" });
                Check(Count(command, "--hwdec=auto") == 1,
                    "single-window " + slot + " explicitly selects auto decoding once");
                options.AdditionalArguments = "--hwdec=disabled";
                command = (string)method.Invoke(runtime.SingleWindows,
                    new object[] { slot, options, "fixture-phone" });
                Check(!command.Contains("--hwdec=auto") && Count(command, "--hwdec=disabled") == 1,
                    "single-window " + slot + " preserves an explicit software-decoding override");
            }
            settings.Scrcpy.AdditionalArguments = "--hwdec=d3d11va";
            dex = runtime.Scrcpy.BuildArguments(settings.Scrcpy, 31, "fixture-phone");
            Check(!dex.Contains("--hwdec=auto") && Count(dex, "--hwdec=d3d11va") == 1,
                "DeX preserves an explicit decoder without duplicate arguments");
            settings.Scrcpy.AdditionalArguments = "--hwdec disabled";
            dex = runtime.Scrcpy.BuildArguments(settings.Scrcpy, 31, "fixture-phone");
            Check(!dex.Contains("--hwdec=auto") && dex.Contains("--hwdec disabled"),
                "space-separated explicit decoder overrides are also respected");
            var environment = new EnvironmentCheckService(adb, runtime.Scrcpy, path, log,
                store, settings, () => string.Empty);
            var entries = new List<EnvironmentCheckItem>();
            typeof(EnvironmentCheckService).GetMethod("AddScrcpyVersionCheck",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(environment, new object[] { entries });
            Check(entries.Count == 1 && entries[0].Message == "5.0.1" &&
                entries[0].Status == EnvironmentCheckStatus.Passed,
                "environment inspection includes the actual complete scrcpy version");
            var field = typeof(ScrcpyService).GetField("_scrcpyPath",
                BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(runtime.Scrcpy, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "missing-scrcpy.exe"));
            entries.Clear();
            typeof(EnvironmentCheckService).GetMethod("AddScrcpyVersionCheck",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(environment, new object[] { entries });
            Check(entries.Count == 1 && entries[0].Status == EnvironmentCheckStatus.Warning &&
                entries[0].Message.Contains("실패"),
                "an unreadable executable reports version failure without inventing a version");
            field.SetValue(runtime.Scrcpy, scrcpy);
            Console.WriteLine("PASS scrcpy runtime integration: " + checks);
            return 0;
        }
        catch (Exception ex) { Console.WriteLine(ex); return 1; }
        finally { if (runtime != null) { runtime.Scrcpy.Dispose(); runtime.SingleWindows.Dispose(); runtime.FileTransfers.Dispose(); runtime.PhoneTransfers.Dispose(); runtime.CompanionGuardian.Dispose(); } }
    }
}
