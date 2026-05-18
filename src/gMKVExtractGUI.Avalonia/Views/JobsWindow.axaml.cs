using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using gMKVToolNix.UI.ViewModels;

namespace gMKVToolNix.UI.Views;

public partial class JobsWindow : Window
{
    public JobsWindow()
    {
        AvaloniaXamlLoader.Load(this);
        // 绑定到全局单例 ViewModel，而不是 XAML 中的局部实例
        DataContext = JobsWindowViewModel.Instance;
    }

    /// <summary>
    /// 显示窗口；若已可见则将其激活到前台。
    /// </summary>
    public void ShowOrActivate()
    {
        if (!IsVisible)
            Show();
        Activate();
    }
}
