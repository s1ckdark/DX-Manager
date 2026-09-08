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
    // Error.Path.ManualAdbUnavailable처럼 절대 경로를 두 개 담는 메시지는
    // 예전 고정 220px 창(가운데 정렬 + 스크롤 없음)에서는 위아래로 잘려
    // 나갔다 - 하필 복구 방법을 설명하는 마지막 문장이 잘리는 경우가
    // 많았다. 이 창이 뜬다는 건 앱이 시작조차 못 했다는 뜻이라, 사용자가
    // 읽을 수 있는 마지막 채널이다. 그래서 고정 Height 대신
    // SizeToContent + MaxHeight를 쓰고, 그래도 넘치면 ScrollViewer가
    // 받는다 - 짧은 메시지는 예전과 거의 같아 보이고, 긴 메시지는 잘리는
    // 대신 스크롤된다.
    private const double MaxWindowHeight = 480;

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

        var content = new StackPanel
        {
            Margin = new Avalonia.Thickness(24),
            Spacing = 12,
            Children = { heading, detail }
        };

        return new Window
        {
            Title = "DX Manager",
            Width = 560,
            MinHeight = 220,
            MaxHeight = MaxWindowHeight,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = new ScrollViewer
            {
                Content = content
            }
        };
    }
}
