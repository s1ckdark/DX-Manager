using DexManager.Models;
using DexManager.Platform;

namespace DexManager.Tests.FakePlatform;

public sealed class FakeCaptureService : ICaptureService
{
    public CaptureResult CaptureWindow(IntPtr windowHandle, string serial) =>
        new CaptureResult(string.Empty, "fake", false);

    public CaptureResult CaptureScreenRectangle(
        int x, int y, int width, int height, string prefix, string serial) =>
        new CaptureResult(string.Empty, "fake", false);

    public Task<CaptureResult> CaptureWindowAsync(IntPtr windowHandle, string serial) =>
        Task.FromResult(CaptureWindow(windowHandle, serial));

    public Task<CaptureResult> CaptureScreenRectangleAsync(
        int x, int y, int width, int height, string prefix, string serial) =>
        Task.FromResult(CaptureScreenRectangle(x, y, width, height, prefix, serial));
}
