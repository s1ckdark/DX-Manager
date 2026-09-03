using DexManager.Platform;

namespace DexManager.Tests.FakePlatform;

public sealed class FakeAutoStartService : IAutoStartService
{
    private bool _registered;

    public bool IsRegistered() => _registered;

    public void Apply(bool enabled) => _registered = enabled;

    public void Register() => _registered = true;

    public void Unregister() => _registered = false;
}
