using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using gMKVToolNix.UI.ViewModels;

namespace gMKVToolNix.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private MainWindowViewModel? Vm => DataContext as MainWindowViewModel;

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
        {
            e.DragEffects = DragDropEffects.Copy;
            if (Vm is not null) Vm.IsDragOver = true;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.Contains(DataFormats.Files)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private void OnDragLeave(object? sender, RoutedEventArgs e)
    {
        if (Vm is not null) Vm.IsDragOver = false;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (Vm is null) return;
        Vm.IsDragOver = false;

        if (!e.Data.Contains(DataFormats.Files)) return;

        var files = e.Data.GetFiles();
        if (files is null) return;

        var paths = files
            .Select(f => f.Path.IsFile ? f.Path.LocalPath : null)
            .Where(p => !string.IsNullOrEmpty(p))
            .Cast<string>()
            .ToList();

        if (paths.Count > 0)
        {
            Vm.AddInputFiles(paths);
        }
    }
}
