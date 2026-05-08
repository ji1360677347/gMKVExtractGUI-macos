using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using gMKVToolNix.UI.ViewModels;

namespace gMKVToolNix.UI.Views;

public partial class LogWindow : Window
{
    private LogWindowViewModel? _vm;

    public LogWindow()
    {
        AvaloniaXamlLoader.Load(this);
        _vm = new LogWindowViewModel();
        _vm.LogAppended += OnLogAppended;
        DataContext = _vm;
        Closed += (_, _) =>
        {
            if (_vm is not null) _vm.LogAppended -= OnLogAppended;
            _vm?.Dispose();
        };
    }

    private void OnLogAppended()
    {
        if (_vm?.AutoScroll != true) return;
        Dispatcher.UIThread.Post(() =>
        {
            var sv = this.FindControl<ScrollViewer>("LogScrollViewer");
            sv?.ScrollToEnd();
        });
    }
}
