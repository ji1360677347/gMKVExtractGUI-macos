using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace gMKVToolNix.UI.Views;

public partial class OptionsWindow : Window
{
    public OptionsWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
