using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace DexManager.Desktop.Views;

/// <summary>
/// 조립 루트가 실패했을 때 <see cref="MainWindow"/> 대신 띄우는 창.
/// GUI에는 콘솔이 없으므로, 창이 하나도 뜨지 않고 죽는 대신 원인을 보여 준다.
/// </summary>
/// <remarks>
/// 터미널 UI(<c>DexManager.Mac/Program.cs</c>)가 시작 실패를
/// <c>Fatal error: {message}</c>로 알리는 것과 같은 의도다. 여기서 문제를
/// 되돌리려 하지 않는다 — adb가 정말 없으면 앱은 동작할 수 없다.
/// </remarks>
internal static class StartupErrorWindow
{
    public static Window Create(Exception error)
    {
        var message = error?.Message ?? "Unknown error.";

        var heading = new TextBlock
        {
            Text = "DX Manager could not start.",
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };

        // 사용자가 원인을 그대로 복사해 보고할 수 있어야 한다.
        var detail = new SelectableTextBlock
        {
            Text = $"Fatal error: {message}",
            TextWrapping = TextWrapping.Wrap
        };

        return new Window
        {
            Title = "DX Manager",
            Width = 560,
            Height = 220,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(24),
                Spacing = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { heading, detail }
            }
        };
    }
}
