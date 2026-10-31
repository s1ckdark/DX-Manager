using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using DexManager.Models;
using DexManager.Services;
using DexManager.Utils;

namespace DexManager.Forms
{
    public sealed partial class SettingsForm : Form, IMessageFilter
    {
        private void BrowseDeviceFolder(ThemedTextControl targetBox)
        {
            var serial = _getSelectedDeviceSerial();
            if (string.IsNullOrWhiteSpace(serial) ||
                !_adbService.IsAuthorizedDeviceConnected(serial))
            {
                DesktopDialogs.Show(
                    this,
                    LocalizationService.Get("DeviceFolder.NoDevice"),
                    LocalizationService.Get("DeviceFolder.Title"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            using (var dialog = new DeviceFolderBrowserForm(
                _adbService,
                serial,
                targetBox.Text,
                targetBox == _deviceScreenshotFolderBox ? (bool?)_pushCaptureBox.Checked : null))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK &&
                    !string.IsNullOrWhiteSpace(dialog.SelectedPath))
                {
                    targetBox.Text = dialog.SelectedPath;
                    if (targetBox == _deviceScreenshotFolderBox)
                    {
                        _pushCaptureBox.Checked = dialog.CaptureTransferEnabled;
                        SaveStoreScope(settings => settings.Features.PushCaptureToDevice = dialog.CaptureTransferEnabled);
                    }
                }
            }
        }

    }
}
