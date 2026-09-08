using Avalonia.Styling;
using DexManager.Desktop;
using DexManager.Models;
using Xunit;

namespace DexManager.Desktop.Tests;

/// <summary>
/// ThemeApplier.MapToVariant의 AppTheme → ThemeVariant 매핑을 검증한다.
/// Application.Current를 만들지 않고도 닿을 수 있는 이 순수 정적 매핑만
/// 여기서 고정한다(Application.Current 접근부는 실행 확인 대상).
/// </summary>
public class ThemeApplierTests
{
    [Fact]
    public void MapToVariant_Auto_ReturnsDefault()
    {
        Assert.Same(ThemeVariant.Default, ThemeApplier.MapToVariant(AppTheme.Auto));
    }

    [Fact]
    public void MapToVariant_Light_ReturnsLight()
    {
        Assert.Same(ThemeVariant.Light, ThemeApplier.MapToVariant(AppTheme.Light));
    }

    [Fact]
    public void MapToVariant_Dark_ReturnsDark()
    {
        Assert.Same(ThemeVariant.Dark, ThemeApplier.MapToVariant(AppTheme.Dark));
    }
}
