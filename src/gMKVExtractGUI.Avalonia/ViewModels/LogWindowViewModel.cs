using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using gMKVToolNix.Log;

namespace gMKVToolNix.UI.ViewModels;

public class LogWindowViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly StringBuilder _buffer = new();

    private string _logText = string.Empty;
    public string LogText
    {
        get => _logText;
        private set
        {
            if (_logText == value) return;
            _logText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LogText)));
        }
    }

    private bool _autoScroll = true;
    public bool AutoScroll
    {
        get => _autoScroll;
        set
        {
            if (_autoScroll == value) return;
            _autoScroll = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AutoScroll)));
        }
    }

    private string _statusText = "正在监听 gMKVLogger 事件…";
    public string StatusText
    {
        get => _statusText;
        set
        {
            if (_statusText == value) return;
            _statusText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusText)));
        }
    }

    public ICommand ClearCommand { get; }
    public ICommand CopyAllCommand { get; }
    public ICommand SaveAsCommand { get; }

    public event Action? LogAppended;

    public LogWindowViewModel()
    {
        // 装入历史日志
        _buffer.Append(gMKVLogger.LogText);
        LogText = _buffer.ToString();

        gMKVLogger.LogLineAdded += OnLogLineAdded;

        ClearCommand = new RelayCommand(() =>
        {
            gMKVLogger.Clear();
            _buffer.Clear();
            LogText = string.Empty;
            StatusText = "已清空";
        });
        CopyAllCommand = new RelayCommand(async () => await CopyAllAsync());
        SaveAsCommand = new RelayCommand(async () => await SaveAsAsync());
    }

    private void OnLogLineAdded(string line, DateTime when)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _buffer.AppendLine(line);
            LogText = _buffer.ToString();
            LogAppended?.Invoke();
        });
    }

    private async Task CopyAllAsync()
    {
        var top = GetTopLevel();
        var clipboard = top?.Clipboard;
        if (clipboard is null) return;
        await clipboard.SetTextAsync(LogText);
        StatusText = "已复制到剪贴板";
    }

    private async Task SaveAsAsync()
    {
        var top = GetTopLevel();
        if (top is null) return;
        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "保存日志",
            SuggestedFileName = $"gMKVExtractGUI-{DateTime.Now:yyyyMMdd-HHmmss}.log",
            DefaultExtension = "log",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("日志文件") { Patterns = new[] { "*.log", "*.txt" } }
            }
        });
        if (file is null) return;
        try
        {
            var path = file.Path.LocalPath;
            await File.WriteAllTextAsync(path, LogText, Encoding.UTF8);
            StatusText = $"已保存：{path}";
        }
        catch (Exception ex)
        {
            StatusText = $"保存失败：{ex.Message}";
        }
    }

    private static TopLevel? GetTopLevel()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.Windows.OfType<Views.LogWindow>().FirstOrDefault() ?? desktop.MainWindow;
        }
        return null;
    }

    public void Dispose()
    {
        gMKVLogger.LogLineAdded -= OnLogLineAdded;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
