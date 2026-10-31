using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using DexManager.Models;
using DexManager.Services;
namespace DexManager.Forms {
 public sealed partial class SettingsForm {
  private bool _loadingStoreValues;
  private void SaveStoreScope(Action<AppSettings> change,bool apply=true) {
   if(!_storeUiReady || _loadingStoreValues || IsDisposed)return;
   try {_settingsService.UpdateAndSave(_settings,change); if(apply && _settingsChanged!=null)_settingsChanged(false);}
   catch(Exception ex){ShowStoreFailure(ex.Message);}
  }
  private void ShowStoreFailure(string message) {ShowSaveStatus(message,Color.Firebrick,60000);}
  private void SaveStorePaths() {
   if(!_storeUiReady || _loadingStoreValues)return;
   var screenshot=ToConfiguredPath(_screenshotFolderBox.Text);var receive=ToConfiguredPath(_phoneToPcReceiveFolderBox.Text);
   try {
    var phone=NormalizeDeviceFolder(_deviceScreenshotFolderBox.Text,false);var target=NormalizeFileTransferTargetFolder(_fileTransferTargetFolderBox.Text);
    if(string.IsNullOrWhiteSpace(receive))throw new InvalidOperationException(LocalizationService.Get("Settings.PhoneToPcReceiveFolderRequired"));
    SaveStoreScope(s=>{s.Paths.ScreenshotFolder=screenshot;s.Paths.DeviceScreenshotFolder=phone;s.Paths.FileTransferTargetFolder=target;s.Paths.PhoneToPcReceiveFolder=receive;});
   } catch(Exception ex){ShowStoreFailure(ex.Message);}
  }
  private void SaveStoreConnectionDraft() {
   if(!_storeUiReady || _loadingStoreValues || _loadingWirelessDevice)return;
   SaveLoadedWirelessProfileToMemory();SaveStoreScope(s=>{},false);
  }
  private void BindStoreToggle(CheckBox box,Action<AppSettings,bool> change,string group=null) {
   box.CheckedChanged+=delegate {
    if(!_storeUiReady || _loadingStoreValues)return;
    if(group!=null){StoreSettingCard card;if(_storeCards.TryGetValue(group,out card))card.Expanded=box.Checked;}
    bool value=box.Checked;SaveStoreScope(s=>change(s,value));UpdateStoreDependencies();
   };
  }
  private void AttachStoreBindings() {
   BindStoreToggle(_startWithWindowsBox,(s,v)=>s.Features.StartWithWindows=v);
   BindStoreToggle(_startMinimizedBox,(s,v)=>s.Features.StartMinimizedToTray=v);
   BindStoreToggle(_miniControlBarBox,(s,v)=>s.Features.MiniControlBarEnabled=v,"GeneralMiniBarExpander");
   BindStoreToggle(_autoHideBox,(s,v)=>s.Features.AutoHideEnabled=v,"GeneralAutoHideExpander");
   BindStoreToggle(_autoStartDexBox,(s,v)=>s.Features.AutoStartDexOnDeviceConnected=v,"GeneralAutoStartDexExpander");
   BindStoreToggle(_phoneToPcTransferBox,(s,v)=>s.Features.PhoneToPcTransferEnabled=v,"PathsPhoneToPcExpander");
   BindStoreToggle(_convertHangulBox,(s,v)=>s.KeyMappings.ConvertKoreanEnglishKey=v,"InputHangulExpander");
   BindStoreToggle(_rightWindowsBox,(s,v)=>s.KeyMappings.HandleRightWindowsKey=v);
   BindStoreToggle(_ignoreShiftSpaceBox,(s,v)=>s.KeyMappings.IgnoreShiftSpace=v);
   BindStoreToggle(_convertEnterBox,(s,v)=>s.KeyMappings.ConvertEnterToShiftEnter=v,"InputEnterExpander");
   BindStoreToggle(_keyboardDiagnosticsBox,(s,v)=>s.KeyMappings.LogKeyboardDiagnostics=v);
   _languageBox.SelectionChangeCommitted+=delegate {var item=_languageBox.SelectedItem as LanguageOption;if(item!=null)SaveStoreScope(s=>s.Language=item.Value,false);};
   _themeBox.SelectionChangeCommitted+=delegate {var item=_themeBox.SelectedItem as ThemeOption;if(item==null)return;SaveStoreScope(s=>s.Theme=item.Value,false);if(_settings.Theme==item.Value && _applyTheme!=null)_applyTheme(item.Value);};
   _miniControlBarSideBox.SelectionChangeCommitted+=delegate {var item=_miniControlBarSideBox.SelectedItem as MiniControlBarSideOption;if(item!=null)SaveStoreScope(s=>s.Features.MiniControlBarSide=item.Value);};
   _keyInputModeBox.SelectionChangeCommitted+=delegate {var v=ReadStoreInputMode(_keyInputModeBox);SaveStoreScope(s=>s.KeyMappings.KoreanEnglishInputMode=v);};
   _enterInputModeBox.SelectionChangeCommitted+=delegate {var v=ReadStoreInputMode(_enterInputModeBox);SaveStoreScope(s=>s.KeyMappings.EnterInputMode=v);};
   _autoHideSecondsBox.Leave+=delegate {var v=(int)_autoHideSecondsBox.Value;SaveStoreScope(s=>s.Timing.AutoHideIdleSeconds=v);};
   _connectedStartDelayBox.Leave+=delegate {var v=(int)_connectedStartDelayBox.Value*1000;SaveStoreScope(s=>s.Timing.ConnectedStartDelayMs=v);};
   foreach(var field in new[]{_captureHotkeyBox,_exitHotkeyBox}) {
    var captured=field;
    field.Enter+=delegate {if(_hotkeyEditing!=null)_hotkeyEditing(true);};
    field.Leave+=delegate {
     if(!_storeUiReady || _loadingStoreValues)return;
     var capture=_captureHotkeyBox.Text.Trim();var exit=_exitHotkeyBox.Text.Trim();
     if(!HotkeyService.IsValidShortcut(capture)||!HotkeyService.IsValidShortcut(exit)||HotkeyService.ShortcutsConflict(capture,exit,_lowLevelHotkeyBox.Checked)) {
      ShowStoreFailure(LocalizationService.Get("Settings.InvalidHotkey"));captured.Text=captured==_captureHotkeyBox?_settings.KeyMappings.CaptureHotkey:_settings.KeyMappings.ExitHotkey;
     } else SaveStoreScope(s=>{s.KeyMappings.CaptureHotkey=capture;s.KeyMappings.ExitHotkey=exit;});
     if(_hotkeyEditing!=null)_hotkeyEditing(false);
    };
   }
   _wirelessHostBox.Leave+=delegate {SaveStoreConnectionDraft();};_wirelessPortBox.Leave+=delegate {SaveStoreConnectionDraft();};_wirelessAutoReconnectBox.CheckedChanged+=delegate {SaveStoreConnectionDraft();};
   _fileTransferTargetFolderBox.Leave+=delegate {SaveStorePaths();};
   _usbConnectionBox.CheckedChanged+=StoreConnectionModeChanged;_wirelessConnectionBox.CheckedChanged+=StoreConnectionModeChanged;
   FormClosing+=delegate {SaveStoreConnectionDraft();SaveStorePaths();};
   FormClosed+=delegate {if(_hotkeyEditing!=null)_hotkeyEditing(false);};
   UpdateStoreDependencies();
  }
  private async void StoreConnectionModeChanged(object sender,EventArgs e) {
   UpdateWirelessControls();if(!_storeUiReady||_loadingStoreValues||_loadingWirelessDevice)return;
   if(!(sender is RadioButton) || !((RadioButton)sender).Checked)return;
   var option=GetSelectedWirelessDeviceOption();if(option==null)return;
   var previous=_settings.FindDeviceWirelessConnection(option.DeviceIdentity);bool wasWireless=previous!=null && previous.Mode==AdbConnectionMode.Wireless;
   SaveStoreConnectionDraft();StoreSettingCard group;if(_storeCards.TryGetValue("ConnectionWirelessExpander",out group))group.Expanded=_wirelessConnectionBox.Checked;
   if(_usbConnectionBox.Checked && wasWireless){string identity=option.DeviceIdentity;await RunWirelessOperationAsync(()=>_wirelessAdbService.DisconnectForDevice(identity));}
   if(IsDisposed)return;RefreshDisplayCleanupStatusAsync();
  }
  private void UpdateStoreDependencies() {
   _miniControlBarSideBox.Enabled=_miniControlBarBox.Checked;_autoHideSecondsBox.Enabled=_autoHideBox.Checked;_connectedStartDelayBox.Enabled=_autoStartDexBox.Checked;
   _keyInputModeBox.Enabled=_convertHangulBox.Checked;_rightWindowsBox.Enabled=_convertHangulBox.Checked;_ignoreShiftSpaceBox.Enabled=_convertHangulBox.Checked;_enterInputModeBox.Enabled=_convertEnterBox.Checked;
  }
  private async void RefreshStoreEnvironment() {
   var text=_storeEditors["DiagnosticEnvironmentText"] as Label;
   if(text!=null)text.Text=LocalizationService.IsKorean?"검사 중…":"Checking…";
   try {
    if(_runStoreEnvironment==null){if(_showEnvironmentCheck!=null)_showEnvironmentCheck();return;}
    var entries=await Task.Run(_runStoreEnvironment).ConfigureAwait(false);
    Action apply=delegate {
     if(IsDisposed || Disposing)return;
     if(text!=null)text.Text=string.Join("\n",System.Linq.Enumerable.Select(entries,item=>item.Name+": "+item.Message));
     StoreSettingCard card;if(text!=null && _storeCards.TryGetValue("DiagnosticsEnvironmentCard",out card)) {
      var refresh=_storeEditors["RefreshDiagnosticsButton"];refresh.Parent=card.HeaderSurface;refresh.Anchor=AnchorStyles.Top|AnchorStyles.Right;refresh.Bounds=new Rectangle(card.Width-178,14,160,40);
      card.WideContent=true;text.Location=Point.Empty;card.Reflow();
     }
    };
    if(!IsDisposed && IsHandleCreated) {if(InvokeRequired)BeginInvoke(apply);else apply();}
   }catch(Exception ex){
    if(!IsDisposed && IsHandleCreated) {
     Action failure=delegate {if(!IsDisposed)ShowStoreFailure(ex.Message);};
     try {if(InvokeRequired)BeginInvoke(failure);else failure();}catch(InvalidOperationException){}
    }
   }
  }
  internal string GetSettingsSelectedSerial() {return ResolveStoreTargetSerial();}
  private void RestoreStoreExpansion() {
   SetStoreExpansion("GeneralMiniBarExpander",_miniControlBarBox.Checked);SetStoreExpansion("GeneralAutoHideExpander",_autoHideBox.Checked);SetStoreExpansion("GeneralAutoStartDexExpander",_autoStartDexBox.Checked);
   SetStoreExpansion("InputHangulExpander",_convertHangulBox.Checked);SetStoreExpansion("InputEnterExpander",_convertEnterBox.Checked);SetStoreExpansion("ConnectionWirelessExpander",_wirelessConnectionBox.Checked);SetStoreExpansion("PathsPhoneToPcExpander",_phoneToPcTransferBox.Checked);
   SetStoreExpansion("CompanionStatusExpanderText",true);
   UpdateStoreDependencies();SynchronizeStoreSelectors();
  }
  private void SetStoreExpansion(string key,bool value){StoreSettingCard card;if(_storeCards.TryGetValue(key,out card))card.Expanded=value;}
 }
}