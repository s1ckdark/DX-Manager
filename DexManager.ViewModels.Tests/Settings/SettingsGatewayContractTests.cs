using Xunit;
using DexManager.Models;

namespace DexManager.ViewModels.Tests.Settings;

/// <summary>
/// ISettingsGateway의 계약을 검증한다. 모든 구현이 만족해야 하는
/// 요구사항을 확인한다.
/// </summary>
public class SettingsGatewayContractTests
{
    [Fact]
    public void GetRunProfile_SameIdentity_ReturnsSameProfile()
    {
        // 같은 device identity로 두 번 조회하면 같은 profile을 받는다.
        var gateway = new FakeSettingsGateway();
        const string deviceIdentity = "device-123";

        var profile1 = gateway.GetRunProfile(deviceIdentity);
        var profile2 = gateway.GetRunProfile(deviceIdentity);

        Assert.Same(profile1, profile2);
    }

    [Fact]
    public void Update_AppliesMutation()
    {
        // Update가 mutate를 적용한다.
        var gateway = new FakeSettingsGateway();
        var originalLanguage = gateway.Current.Language;
        var newLanguage = originalLanguage == AppLanguage.English
            ? AppLanguage.Korean
            : AppLanguage.English;

        gateway.Update(settings => settings.Language = newLanguage);

        Assert.Equal(newLanguage, gateway.Current.Language);
    }
}
