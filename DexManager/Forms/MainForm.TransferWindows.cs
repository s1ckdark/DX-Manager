using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using DexManager.Models;
using DexManager.Services;
using DexManager.Utils;

namespace DexManager.Forms
{
    public sealed partial class MainForm
    {
        private const int TransferWindowGap = 8;
        private const int TransferWindowMargin = 16;
        private bool _transferPositionFailureLogged;

        private IntPtr PositionTransferStatusWindow(
            Form window,
            Form otherWindow,
            IntPtr preferredTarget)
        {
            return PositionTransferStatusWindow(
                window,
                otherWindow,
                preferredTarget,
                true);
        }

        private IntPtr PositionTransferStatusWindow(
            Form window,
            Form otherWindow,
            FileTransferCoordinator coordinator,
            string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId) || coordinator == null)
            {
                return PositionTransferStatusWindow(
                    window,
                    otherWindow,
                    IntPtr.Zero,
                    true);
            }

            int processId;
            IntPtr boundHandle;
            var hasTarget = coordinator.TryGetScrcpyWindowTarget(
                sessionId,
                out processId,
                out boundHandle);
            var statusWindow = window as FileTransferStatusForm;
            if (!hasTarget)
            {
                // Late progress after a session ended must not be rebound to
                // another phone or mode that happens to own a live window.
                if (statusWindow != null &&
                    statusWindow.HasResolvedTargetWindow)
                {
                    return IntPtr.Zero;
                }
                return PositionTransferStatusWindow(
                    window,
                    otherWindow,
                    IntPtr.Zero,
                    false);
            }

            if (statusWindow != null)
                statusWindow.SetScrcpyTarget(processId, IntPtr.Zero);
            var targetHandle = ResolveWindowForProcess(
                processId,
                boundHandle);
            if (statusWindow != null)
            {
                statusWindow.SetScrcpyTarget(processId, targetHandle);
                if (targetHandle == IntPtr.Zero &&
                    statusWindow.HasResolvedTargetWindow)
                {
                    // Keep the last placement while the same Scrcpy window
                    // is temporarily unavailable; do not jump to a corner.
                    return IntPtr.Zero;
                }
            }

            return PositionTransferStatusWindow(
                window,
                otherWindow,
                targetHandle,
                false);
        }

        private IntPtr PositionTransferStatusWindow(
            Form window,
            Form otherWindow,
            IntPtr preferredTarget,
            bool allowFallback)
        {
            if (window == null || window.IsDisposed) return IntPtr.Zero;

            var targetHandle = preferredTarget;
            if (targetHandle == IntPtr.Zero && allowFallback)
                targetHandle = ResolveTransferTarget(IntPtr.Zero);
            var barBounds = Rectangle.Empty;
            Rectangle workArea;
            var x = 0;
            var y = 0;
            var hasBar = targetHandle != IntPtr.Zero &&
                _miniControlBarManager.TryGetBarBounds(
                    targetHandle,
                    out barBounds);
            if (!hasBar && allowFallback)
            {
                hasBar = _miniControlBarManager.TryGetActiveBarBounds(
                    out barBounds);
            }
            if (hasBar)
            {
                workArea = Screen.FromRectangle(barBounds).WorkingArea;
                x = _settings.Features.MiniControlBarSide ==
                    MiniControlBarSide.Left
                    ? barBounds.Right - window.Width
                    : barBounds.Left;
                y = barBounds.Bottom + TransferWindowGap;
            }
            else if (TryPositionBesideTarget(
                window,
                targetHandle,
                out workArea,
                out x,
                out y))
            {
            }
            else
            {
                workArea = Screen.FromControl(this).WorkingArea;
                x = workArea.Right - window.Width -
                    TransferWindowMargin;
                y = workArea.Top + TransferWindowMargin;
            }

            x = Math.Max(
                workArea.Left,
                Math.Min(workArea.Right - window.Width, x));
            y = Math.Max(
                workArea.Top,
                Math.Min(workArea.Bottom - window.Height, y));

            var candidate = new Rectangle(
                x,
                y,
                window.Width,
                window.Height);
            if (otherWindow != null &&
                !otherWindow.IsDisposed &&
                otherWindow.Visible &&
                candidate.IntersectsWith(otherWindow.Bounds))
            {
                var below = otherWindow.Bottom + TransferWindowGap;
                if (below + window.Height <= workArea.Bottom)
                {
                    y = below;
                }
                else
                {
                    y = Math.Max(
                        workArea.Top,
                        otherWindow.Top - window.Height -
                        TransferWindowGap);
                }
            }

            window.Location = new Point(x, y);
            return targetHandle;
        }

        private static IntPtr ResolveWindowForProcess(
            int expectedProcessId,
            IntPtr preferredHandle)
        {
            if (WindowBelongsToProcess(preferredHandle, expectedProcessId))
                return preferredHandle;
            if (expectedProcessId <= 0) return IntPtr.Zero;

            try
            {
                using (var process = Process.GetProcessById(expectedProcessId))
                {
                    process.Refresh();
                    var handle = process.MainWindowHandle;
                    return WindowBelongsToProcess(handle, expectedProcessId)
                        ? handle
                        : IntPtr.Zero;
                }
            }
            catch (ArgumentException)
            {
                return IntPtr.Zero;
            }
            catch (InvalidOperationException)
            {
                return IntPtr.Zero;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return IntPtr.Zero;
            }
        }

        private static bool WindowBelongsToProcess(
            IntPtr windowHandle,
            int expectedProcessId)
        {
            if (windowHandle == IntPtr.Zero ||
                expectedProcessId <= 0 ||
                !NativeMethods.IsWindow(windowHandle))
            {
                return false;
            }

            uint processId;
            NativeMethods.GetWindowThreadProcessId(
                windowHandle,
                out processId);
            return processId == (uint)expectedProcessId;
        }

        private void FileTransferStatusForm_PositionUpdateRequested(
            object sender,
            EventArgs e)
        {
            var window = sender as FileTransferStatusForm;
            if (window == null || window.IsDisposed ||
                _exitInProgress || IsDisposed)
            {
                return;
            }

            try
            {
                PositionTransferStatusWindow(
                    window,
                    _phoneTransferStatusForm,
                    _fileTransferStatusSource,
                    window.SessionId);
                _transferPositionFailureLogged = false;
            }
            catch (Exception ex)
            {
                if (_transferPositionFailureLogged) return;
                _transferPositionFailureLogged = true;
                _logService.Warning(
                    "Could not follow the active Scrcpy transfer window: " +
                    ex.Message);
            }
        }

        private static void ShowTransferStatusWindow(
            Form window,
            IntPtr targetHandle)
        {
            if (window == null || window.IsDisposed || window.Visible)
                return;
            if (targetHandle != IntPtr.Zero &&
                NativeMethods.IsWindow(targetHandle))
            {
                window.Show(new WindowHandleOwner(targetHandle));
                return;
            }
            window.Show();
        }

        private IntPtr ResolveTransferTarget(IntPtr preferredTarget)
        {
            if (preferredTarget != IntPtr.Zero &&
                NativeMethods.IsWindow(preferredTarget))
            {
                return preferredTarget;
            }

            var foreground = NativeMethods.GetForegroundWindow();
            if (foreground != IntPtr.Zero &&
                (foreground == _scrcpyService.MainWindowHandle ||
                 _singleWindowService.ContainsWindowHandle(foreground)))
            {
                return foreground;
            }

            var dexHandle = _scrcpyService.MainWindowHandle;
            if (dexHandle != IntPtr.Zero &&
                NativeMethods.IsWindow(dexHandle))
            {
                return dexHandle;
            }

            foreach (var handle in _singleWindowService.GetWindowHandles())
            {
                if (handle != IntPtr.Zero && NativeMethods.IsWindow(handle))
                    return handle;
            }
            return IntPtr.Zero;
        }

        private static bool TryPositionBesideTarget(
            Form window,
            IntPtr targetHandle,
            out Rectangle workArea,
            out int x,
            out int y)
        {
            workArea = Rectangle.Empty;
            x = 0;
            y = 0;
            NativeRect targetBounds;
            if (targetHandle == IntPtr.Zero ||
                !NativeMethods.IsWindow(targetHandle) ||
                NativeMethods.IsIconic(targetHandle) ||
                !NativeMethods.GetWindowRect(
                    targetHandle,
                    out targetBounds))
            {
                return false;
            }

            workArea = Screen.FromHandle(targetHandle).WorkingArea;
            x = targetBounds.Right + TransferWindowGap;
            y = targetBounds.Top;
            if (x + window.Width > workArea.Right)
                x = targetBounds.Left - window.Width - TransferWindowGap;
            if (x < workArea.Left)
            {
                x = Math.Max(
                    workArea.Left,
                    Math.Min(
                        targetBounds.Left,
                        workArea.Right - window.Width));
                y = targetBounds.Bottom + TransferWindowGap;
            }
            return true;
        }
    }
}
