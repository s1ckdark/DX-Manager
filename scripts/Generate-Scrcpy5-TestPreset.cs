using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Json;
using DexManager.Models;
using DexManager.Services;
class PresetBootstrap {
 static int Main(string[] args) {
  AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender,ResolveEventArgs e) {
   return new AssemblyName(e.Name).Name == "DXManager" ? Assembly.LoadFrom(args[0]) : null;
  };
  try { Presets.Run(args[1]); return 0; } catch(Exception e) { Console.Error.WriteLine(e.Message); return 1; }
 }
}
class Presets {
 internal static void Run(string mode) {
  if(mode!="auto" && mode!="disabled")throw new ArgumentException("Expected auto or disabled.");
  var settings=AppSettings.CreateDefault();
  settings.Paths.PhoneToPcReceiveFolder="receive";
  settings.Scrcpy.AdditionalArguments="--hwdec="+mode;
  foreach(var slot in settings.SingleWindowSlots)slot.AdditionalArguments="--hwdec="+mode;
  ScrcpyService.ValidateAdditionalArguments(settings.Scrcpy.AdditionalArguments);
  var profile=settings.GetOrCreateDeviceRunSettings("preset-check");
  if(profile.Scrcpy.AdditionalArguments!=settings.Scrcpy.AdditionalArguments)throw new Exception("DeX template did not propagate.");
  foreach(var slot in profile.SingleWindowSlots)if(slot.AdditionalArguments!=settings.Scrcpy.AdditionalArguments)throw new Exception("Single-window template did not propagate.");
  settings.DeviceRunSettingsProfiles.Clear();
  using(var buffer=new MemoryStream()) {
   new DataContractJsonSerializer(typeof(AppSettings)).WriteObject(buffer,settings);
   Console.WriteLine(Convert.ToBase64String(buffer.ToArray()));
  }
 }
}