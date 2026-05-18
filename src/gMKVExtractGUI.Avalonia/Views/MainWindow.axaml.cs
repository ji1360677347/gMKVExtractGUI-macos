using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using gMKVToolNix.UI.ViewModels;

namespace gMKVToolNix.UI.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _subscribedViewModel;

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        SubscribeViewModel(Vm);
        AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private MainWindowViewModel? Vm => DataContext as MainWindowViewModel;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        SubscribeViewModel(Vm);
    }

    private void SubscribeViewModel(MainWindowViewModel? vm)
    {
        if (_subscribedViewModel == vm)
        {
            return;
        }

        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.UnsupportedInputFilesRejected -= OnUnsupportedInputFilesRejected;
            _subscribedViewModel.ExtractionCompleted -= OnExtractionCompleted;
        }

        if (vm is not null)
        {
            vm.UnsupportedInputFilesRejected += OnUnsupportedInputFilesRejected;
            vm.ExtractionCompleted += OnExtractionCompleted;
        }

        _subscribedViewModel = vm;
    }

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
            .Select(f => f.Path.LocalPath)
            .Where(p => !string.IsNullOrEmpty(p))
            .Cast<string>()
            .ToList();

        if (paths.Count > 0)
        {
            Vm.AddInputFiles(paths);
        }
    }

    private async void OnUnsupportedInputFilesRejected(IReadOnlyList<string> paths)
    {
        await ShowUnsupportedFilesDialogAsync(paths);
    }

    private async void OnExtractionCompleted(int fileCount, int trackCount)
    {
        await ShowExtractionCompletedDialogAsync(fileCount, trackCount);
    }

    private async Task ShowUnsupportedFilesDialogAsync(IReadOnlyList<string> paths)
    {
        var shownFiles = paths
            .Take(6)
            .Select(System.IO.Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        var fileList = string.Join(Environment.NewLine, shownFiles);
        if (paths.Count > shownFiles.Count)
        {
            fileList += $"{Environment.NewLine}…另有 {paths.Count - shownFiles.Count} 个文件";
        }

        var dialog = new Window
        {
            Title = "不支持的文件格式",
            Width = 460,
            SizeToContent = SizeToContent.Height,
            MinHeight = 220,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Brushes.White,
        };

        dialog.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(22),
            Spacing = 14,
            Children =
            {
                new TextBlock
                {
                    Text = "已跳过不支持的文件",
                    FontSize = 18,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x4A, 0x4A, 0x5A)),
                },
                new TextBlock
                {
                    Text = "仅支持 Matroska/WebM 文件：.mkv、.mka、.mks、.webm",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x78, 0x78, 0x8A)),
                },
                new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(0xFC, 0xFC, 0xFE)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0xDC, 0xC8, 0xDA)),
                    BorderThickness = new Avalonia.Thickness(1),
                    CornerRadius = new Avalonia.CornerRadius(8),
                    Padding = new Avalonia.Thickness(12, 10),
                    Child = new TextBlock
                    {
                        Text = fileList,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = new SolidColorBrush(Color.FromRgb(0x4A, 0x4A, 0x5A)),
                    },
                },
                CreateCloseDialogButton(dialog),
            },
        };

        await dialog.ShowDialog(this);
    }

    private async Task ShowExtractionCompletedDialogAsync(int fileCount, int trackCount)
    {
        var dialog = new Window
        {
            Title = "提取完成",
            Width = 380,
            SizeToContent = SizeToContent.Height,
            MinHeight = 180,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Brushes.White,
        };

        dialog.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(22),
            Spacing = 14,
            Children =
            {
                new TextBlock
                {
                    Text = "提取完成",
                    FontSize = 18,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x4A, 0x4A, 0x5A)),
                },
                new TextBlock
                {
                    Text = $"已成功提取 {fileCount} 个文件、{trackCount} 个轨道。",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x78, 0x78, 0x8A)),
                },
                CreateCloseDialogButton(dialog),
            },
        };

        await dialog.ShowDialog(this);
    }

    private static Button CreateCloseDialogButton(Window dialog)
    {
        var button = new Button
        {
            Content = "知道了",
            HorizontalAlignment = HorizontalAlignment.Right,
            Padding = new Avalonia.Thickness(18, 8),
            Background = new SolidColorBrush(Color.FromRgb(0xC4, 0xE8, 0xD5)),
            Foreground = new SolidColorBrush(Color.FromRgb(0x4A, 0x4A, 0x5A)),
            BorderThickness = new Avalonia.Thickness(0),
            CornerRadius = new Avalonia.CornerRadius(10),
        };

        button.Click += (_, _) => dialog.Close();

        return button;
    }
}
