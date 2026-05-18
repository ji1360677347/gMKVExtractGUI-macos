using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using gMKVToolNix.UI.ViewModels;

namespace gMKVToolNix.UI.Views;

public partial class OptionsWindow : Window
{
    private OptionsWindowViewModel? _viewModel;

    public OptionsWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        // 取消订阅旧的 ViewModel
        if (_viewModel is not null)
        {
            _viewModel.CloseRequested -= OnCloseRequested;
            _viewModel.PlaceholderInsertRequested -= OnPlaceholderInsertRequested;
        }

        _viewModel = DataContext as OptionsWindowViewModel;

        if (_viewModel is not null)
        {
            _viewModel.CloseRequested += OnCloseRequested;
            _viewModel.PlaceholderInsertRequested += OnPlaceholderInsertRequested;
        }
    }

    private void OnCloseRequested(bool saved)
    {
        Close(saved);
    }

    private void OnPlaceholderClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Content is string placeholder)
        {
            OnPlaceholderInsertRequested(placeholder);
        }
    }

    private void OnPlaceholderInsertRequested(string placeholder)
    {
        // 找到当前 TabControl 中可见的 TextBox 并插入占位符
        var tabControl = this.FindControl<TabControl>(nameof(TabControl));
        if (tabControl is null) return;

        // 获取当前选中的 TabItem 内容
        if (tabControl.SelectedItem is TabItem selectedTab &&
            selectedTab.Content is TextBox textBox)
        {
            var text = textBox.Text ?? "";
            var caretIndex = textBox.CaretIndex;

            // 在光标位置插入占位符
            var newText = text.Substring(0, caretIndex) + placeholder + text.Substring(caretIndex);
            textBox.Text = newText;
            textBox.CaretIndex = caretIndex + placeholder.Length;

            // 触发绑定更新（Avalonia 不需要手动 UpdateSource，双向绑定自动更新）
        }
    }
}
