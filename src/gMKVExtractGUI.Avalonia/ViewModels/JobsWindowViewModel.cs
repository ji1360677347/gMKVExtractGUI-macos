using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Media;

namespace gMKVToolNix.UI.ViewModels;

public class JobsWindowViewModel : INotifyPropertyChanged
{
    public ObservableCollection<JobItem> Jobs { get; } = new();

    private JobItem? _selectedJob;
    public JobItem? SelectedJob
    {
        get => _selectedJob;
        set => SetField(ref _selectedJob, value);
    }

    private string _statusText = "队列就绪";
    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

    public bool HasJobs => Jobs.Count > 0;
    public bool IsEmpty => Jobs.Count == 0;

    public ICommand RunAllCommand { get; }
    public ICommand RemoveSelectedCommand { get; }
    public ICommand ClearCommand { get; }

    public JobsWindowViewModel()
    {
        Jobs.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasJobs));
            OnPropertyChanged(nameof(IsEmpty));
        };

        RunAllCommand = new RelayCommand(() =>
        {
            StatusText = "执行队列：尚未实现（队列调度将在后续轮次完成）";
        });
        RemoveSelectedCommand = new RelayCommand(() =>
        {
            if (SelectedJob is not null)
            {
                Jobs.Remove(SelectedJob);
                StatusText = "已移除选中作业";
            }
        });
        ClearCommand = new RelayCommand(() =>
        {
            Jobs.Clear();
            StatusText = "已清空队列";
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? prop = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(prop);
        return true;
    }
    private void OnPropertyChanged([CallerMemberName] string? prop = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop ?? string.Empty));
}

public class JobItem : INotifyPropertyChanged
{
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;

    private string _stateLabel = "排队";
    public string StateLabel
    {
        get => _stateLabel;
        set
        {
            if (_stateLabel == value) return;
            _stateLabel = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StateLabel)));
        }
    }

    private IBrush _stateColor = new SolidColorBrush(Color.FromRgb(0xCD, 0xE4, 0xF4));
    public IBrush StateColor
    {
        get => _stateColor;
        set
        {
            _stateColor = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StateColor)));
        }
    }

    private string _progressLabel = "—";
    public string ProgressLabel
    {
        get => _progressLabel;
        set
        {
            if (_progressLabel == value) return;
            _progressLabel = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProgressLabel)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
