using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DexManager.Desktop.Views;
using DexManager.Hosting;
using DexManager.Mac.Platform;
using DexManager.ViewModels;

namespace DexManager.Desktop;

public partial class App : Application
{
    private ShellViewModel _shell;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var pathProvider = new MacPathProvider();

            var host = new ApplicationHost(
                new MacPlatformService(),
                pathProvider,
                new MacCaptureService(pathProvider.DefaultScreenshotFolder),
                new MacKeyboardService(),
                new MacAutoStartService());

            _shell = new ShellViewModel(host, new AvaloniaUiDispatcher());

            desktop.MainWindow = new MainWindow { DataContext = _shell };
            desktop.ShutdownRequested += (_, _) => _shell?.Dispose();

            _shell.Start();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
