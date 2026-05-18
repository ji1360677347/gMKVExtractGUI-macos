using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using gMKVToolNix.UI.Services;
using gMKVToolNix.UI.Views;

namespace gMKVToolNix.UI;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // 应用启动时加载持久化设置
        SettingsService.Instance.Load();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();

            // 应用退出时保存设置
            desktop.Exit += (_, _) => SettingsService.Instance.Save();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
