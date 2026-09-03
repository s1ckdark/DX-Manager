using DexManager.Platform;

namespace DexManager.Tests.FakePlatform;

public sealed class FakePlatformService : IPlatformService
{
    public bool IsAdministrator() => false;

    public string GetOperatingSystemDisplayName() => "Fake macOS";

    public Version GetOperatingSystemVersion() => new Version(14, 0);

    public bool RequiresLegacyAdb => false;

    public bool IsWindow(IntPtr handle) => handle != IntPtr.Zero;

    public bool IsWindowVisible(IntPtr handle) => handle != IntPtr.Zero;

    public bool IsIconic(IntPtr handle) => false;

    public void ShowWindow(IntPtr handle, bool restore) { }

    public void SetForegroundWindow(IntPtr handle) { }

    public IntPtr GetForegroundWindow() => IntPtr.Zero;

    public bool GetClientRect(
        IntPtr handle, out int left, out int top, out int right, out int bottom)
    {
        left = 0; top = 0; right = 0; bottom = 0;
        return false;
    }

    public bool GetWindowRect(
        IntPtr handle, out int left, out int top, out int right, out int bottom)
    {
        left = 0; top = 0; right = 0; bottom = 0;
        return false;
    }

    public void SuppressNativeCrashDialogs() { }

    public bool IsDirectoryInProcessPath(string directory) => false;

    public bool IsDirectoryInSystemPath(string directory) => false;

    public bool TryRegisterDirectoryInSystemPath(string directory) => false;
}
