using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Media;
using Avalonia.Threading;
using gMKVToolNix.MkvExtract;

namespace gMKVToolNix.UI.ViewModels;

// ─────────────────────────────────────────────────────────────────────────────
//  JobStatus
// ─────────────────────────────────────────────────────────────────────────────
public enum JobStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Skipped,
    Paused,
}

// ─────────────────────────────────────────────────────────────────────────────
//  JobItem — 单条任务数据模型
// ─────────────────────────────────────────────────────────────────────────────
public sealed class JobItem : INotifyPropertyChanged
{
    // ---- 只读业务属性 --------------------------------------------------------
    public string FileName { get; }
    public int TrackCount { get; }
    public gMKVExtractSegmentsParameters ExtractionParameters { get; }
    public ExtractionMode ExtractionMode { get; }
    public string MkvToolnixPath { get; }

    // ---- 可变状态属性 -------------------------------------------------------
    private JobStatus _status = JobStatus.Pending;
    public JobStatus Status
    {
        get => _status;
        set
        {
            if (_status == value) return;
            _status = value;
            Notify();
            Notify(nameof(StatusLabel));
            Notify(nameof(StatusColor));
            Notify(nameof(IsRunning));
        }
    }

    private int _progress;
    public int Progress
    {
        get => _progress;
        set
        {
            if (_progress == value) return;
            _progress = value;
            Notify();
            Notify(nameof(ProgressLabel));
        }
    }

    private TimeSpan? _duration;
    public TimeSpan? Duration
    {
        get => _duration;
        set { _duration = value; Notify(); Notify(nameof(DurationLabel)); }
    }

    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        set { _errorMessage = value; Notify(); }
    }

    // ---- 派生显示属性 -------------------------------------------------------
    public string StatusLabel => _status switch
    {
        JobStatus.Pending   => "等待",
        JobStatus.Running   => "运行中",
        JobStatus.Completed => "完成",
        JobStatus.Failed    => "失败",
        JobStatus.Skipped   => "已跳过",
        JobStatus.Paused    => "已暂停",
        _                   => _status.ToString(),
    };

    public IBrush StatusColor => _status switch
    {
        JobStatus.Pending   => new SolidColorBrush(Color.FromRgb(0xCD, 0xE4, 0xF4)), // sky
        JobStatus.Running   => new SolidColorBrush(Color.FromRgb(0xC4, 0xE8, 0xD5)), // mint
        JobStatus.Completed => new SolidColorBrush(Color.FromRgb(0xC4, 0xE8, 0xD5)), // mint
        JobStatus.Failed    => new SolidColorBrush(Color.FromRgb(0xFF, 0xD1, 0xDC)), // pink
        JobStatus.Skipped   => new SolidColorBrush(Color.FromRgb(0xFF, 0xF0, 0xC0)), // lemon
        JobStatus.Paused    => new SolidColorBrush(Color.FromRgb(0xDC, 0xD0, 0xF0)), // lavender
        _                   => new SolidColorBrush(Colors.LightGray),
    };

    public string ProgressLabel => _status == JobStatus.Running
        ? $"{_progress}%"
        : (_status == JobStatus.Completed ? "100%" : "—");

    public string DurationLabel => _duration.HasValue
        ? $"{(int)_duration.Value.TotalMinutes:D2}:{_duration.Value.Seconds:D2}"
        : "—";

    public bool IsRunning => _status == JobStatus.Running;

    // ---- 内部计时 -----------------------------------------------------------
    internal DateTime? StartTime { get; set; }

    // ---- 构造 ---------------------------------------------------------------
    public JobItem(
        string fileName,
        int trackCount,
        gMKVExtractSegmentsParameters extractionParameters,
        ExtractionMode extractionMode,
        string mkvToolnixPath)
    {
        FileName = fileName;
        TrackCount = trackCount;
        ExtractionParameters = extractionParameters;
        ExtractionMode = extractionMode;
        MkvToolnixPath = mkvToolnixPath;
    }

    // ---- INotifyPropertyChanged --------------------------------------------
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName] string? prop = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
}

// ─────────────────────────────────────────────────────────────────────────────
//  JobsWindowViewModel — 队列调度核心
// ─────────────────────────────────────────────────────────────────────────────
public sealed class JobsWindowViewModel : INotifyPropertyChanged
{
    // ---- 单例 ---------------------------------------------------------------
    public static JobsWindowViewModel Instance { get; } = new();

    // ── 任务集合 ─────────────────────────────────────────────────────────────
    public ObservableCollection<JobItem> Jobs { get; } = new();

    private JobItem? _selectedJob;
    public JobItem? SelectedJob
    {
        get => _selectedJob;
        set => SetField(ref _selectedJob, value);
    }

    // ── 进度属性 ─────────────────────────────────────────────────────────────
    private int _overallProgress;
    public int OverallProgress
    {
        get => _overallProgress;
        private set => SetField(ref _overallProgress, value);
    }

    private int _currentTaskProgress;
    public int CurrentTaskProgress
    {
        get => _currentTaskProgress;
        private set => SetField(ref _currentTaskProgress, value);
    }

    private string _statusText = "队列就绪";
    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

    // ── 运行状态标志 ──────────────────────────────────────────────────────────
    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            SetField(ref _isRunning, value);
            Notify(nameof(CanRunAll));
            Notify(nameof(CanPause));
            Notify(nameof(CanAbort));
        }
    }

    private bool _isPaused;
    public bool IsPaused
    {
        get => _isPaused;
        private set
        {
            SetField(ref _isPaused, value);
            Notify(nameof(CanPause));
            Notify(nameof(CanResume));
        }
    }

    public bool CanRunAll   => !_isRunning && Jobs.Any(j => j.Status == JobStatus.Pending);
    public bool CanPause    => _isRunning && !_isPaused;
    public bool CanResume   => _isRunning && _isPaused;
    public bool CanAbort    => _isRunning;
    public bool HasJobs     => Jobs.Count > 0;
    public bool IsEmpty     => Jobs.Count == 0;

    // ── 完成摘要属性 ──────────────────────────────────────────────────────────
    private bool _isSummaryVisible;
    public bool IsSummaryVisible
    {
        get => _isSummaryVisible;
        private set => SetField(ref _isSummaryVisible, value);
    }

    private int _completedCount;
    public int CompletedCount { get => _completedCount; private set => SetField(ref _completedCount, value); }

    private int _failedCount;
    public int FailedCount { get => _failedCount; private set => SetField(ref _failedCount, value); }

    private int _skippedCount;
    public int SkippedCount { get => _skippedCount; private set => SetField(ref _skippedCount, value); }

    private string _totalDurationLabel = "0:00";
    public string TotalDurationLabel { get => _totalDurationLabel; private set => SetField(ref _totalDurationLabel, value); }

    // ── 命令 ──────────────────────────────────────────────────────────────────
    public ICommand RunAllCommand     { get; }
    public ICommand PauseCommand      { get; }
    public ICommand ResumeCommand     { get; }
    public ICommand AbortAllCommand   { get; }
    public ICommand RetryFailedCommand{ get; }
    public ICommand RemoveSelectedCommand { get; }
    public ICommand ClearCommand      { get; }
    public ICommand DismissSummaryCommand { get; }

    // ── 内部状态 ─────────────────────────────────────────────────────────────
    private bool _isAborted;
    private readonly SemaphoreSlim _pauseGate = new(1, 1);
    private gMKVExtract? _activeExtractor;

    // ── 构造函数 ─────────────────────────────────────────────────────────────
    private JobsWindowViewModel()
    {
        Jobs.CollectionChanged += (_, _) =>
        {
            Notify(nameof(HasJobs));
            Notify(nameof(IsEmpty));
            Notify(nameof(CanRunAll));
        };

        RunAllCommand      = new RelayCommand(async () => await RunAllAsync());
        PauseCommand       = new RelayCommand(Pause);
        ResumeCommand      = new RelayCommand(Resume);
        AbortAllCommand    = new RelayCommand(AbortAll);
        RetryFailedCommand = new RelayCommand(RetryFailed);
        RemoveSelectedCommand = new RelayCommand(RemoveSelected);
        ClearCommand       = new RelayCommand(ClearAll);
        DismissSummaryCommand = new RelayCommand(() => IsSummaryVisible = false);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  公共 API — 添加任务
    // ─────────────────────────────────────────────────────────────────────────
    public void AddJob(JobItem item)
    {
        // 重复检查：同文件 + 同提取模式 + 相同参数
        if (Jobs.Any(j =>
            j.FileName == item.FileName &&
            j.ExtractionMode == item.ExtractionMode &&
            j.ExtractionParameters.Equals(item.ExtractionParameters)))
        {
            return;
        }
        Jobs.Add(item);
        IsSummaryVisible = false;
    }

    public void AddJobs(IEnumerable<JobItem> items)
    {
        foreach (var item in items) AddJob(item);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  公共 API — 移除
    // ─────────────────────────────────────────────────────────────────────────
    private void RemoveSelected()
    {
        if (SelectedJob is null || SelectedJob.Status == JobStatus.Running) return;
        Jobs.Remove(SelectedJob);
        SelectedJob = null;
    }

    private void ClearAll()
    {
        if (_isRunning) return;
        Jobs.Clear();
        IsSummaryVisible = false;
        StatusText = "已清空队列";
        OverallProgress = 0;
        CurrentTaskProgress = 0;
    }

    private void RetryFailed()
    {
        foreach (var job in Jobs.Where(j => j.Status == JobStatus.Failed))
        {
            job.Status = JobStatus.Pending;
            job.Progress = 0;
            job.ErrorMessage = null;
            job.Duration = null;
        }
        IsSummaryVisible = false;
        Notify(nameof(CanRunAll));
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  顺序执行引擎
    // ─────────────────────────────────────────────────────────────────────────
    private async Task RunAllAsync()
    {
        var pending = Jobs.Where(j => j.Status == JobStatus.Pending).ToList();
        if (pending.Count == 0) return;

        IsRunning   = true;
        _isAborted  = false;
        IsSummaryVisible = false;
        var totalCount = pending.Count;
        int doneCount  = 0;
        var wallStart  = Stopwatch.StartNew();

        for (int i = 0; i < pending.Count; i++)
        {
            if (_isAborted) break;

            // 暂停等待
            await WaitIfPausedAsync(pending[i]);
            if (_isAborted) break;

            var job = pending[i];
            job.Status   = JobStatus.Running;
            job.Progress = 0;
            job.StartTime = DateTime.Now;
            CurrentTaskProgress = 0;
            StatusText = $"正在提取 {i + 1}/{totalCount}：{job.FileName}";

            bool success = await RunSingleJobAsync(job, i, totalCount, doneCount);

            job.Duration = DateTime.Now - job.StartTime.Value;

            if (success)
            {
                job.Status   = JobStatus.Completed;
                job.Progress = 100;
            }
            else if (_isAborted && job.Status == JobStatus.Running)
            {
                job.Status = JobStatus.Skipped;
                job.ErrorMessage = "用户中止";
            }

            doneCount++;
            OverallProgress = (int)((double)doneCount / totalCount * 100);
        }

        // 将剩余因 abort 而 Pending 的任务标为 Skipped
        foreach (var j in pending.Where(j => j.Status == JobStatus.Pending))
            j.Status = JobStatus.Skipped;

        wallStart.Stop();
        IsRunning = false;
        IsPaused  = false;
        _activeExtractor = null;

        BuildSummary(pending, wallStart.Elapsed);
    }

    private async Task<bool> RunSingleJobAsync(JobItem job, int index, int total, int doneSoFar)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(job.ExtractionParameters.OutputDirectory))
            {
                Directory.CreateDirectory(job.ExtractionParameters.OutputDirectory);
            }

            var extractor = new gMKVExtract(job.MkvToolnixPath);
            _activeExtractor = extractor;

            extractor.MkvExtractProgressUpdated += progress =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    job.Progress        = progress;
                    CurrentTaskProgress = progress;
                    OverallProgress = (int)(((double)doneSoFar + progress / 100.0) / total * 100);
                });
            };

            extractor.MkvExtractTrackUpdated += (_, trackName) =>
            {
                Dispatcher.UIThread.Post(() =>
                    StatusText = $"[{index + 1}/{total}] {trackName} — {job.FileName}");
            };

            await Task.Run(() => RunExtraction(extractor, job));

            if (extractor.ThreadedException is not null)
            {
                throw extractor.ThreadedException;
            }
            if (extractor.Abort && !_isAborted)
            {
                // 单条 abort → 跳过当前
                job.Status       = JobStatus.Skipped;
                job.ErrorMessage = "用户中止";
                return false;
            }
            return !extractor.Abort;
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() =>
            {
                job.Status       = JobStatus.Failed;
                job.ErrorMessage = ex.Message;
            });
            return false;
        }
    }

    private static void RunExtraction(gMKVExtract extractor, JobItem job)
    {
        var p = job.ExtractionParameters;
        switch (job.ExtractionMode)
        {
            case ExtractionMode.Tracks:
                extractor.ExtractMKVSegmentsThreaded(p);
                break;
            case ExtractionMode.Timecodes:
                extractor.ExtractMKVTimecodesThreaded(p);
                break;
            case ExtractionMode.Cues:
                extractor.ExtractMKVCuesThreaded(p);
                break;
            case ExtractionMode.Cue_Sheet:
                extractor.ExtractMkvCuesheetThreaded(p);
                break;
            case ExtractionMode.Tags:
                extractor.ExtractMkvTagsThreaded(p);
                break;
            case ExtractionMode.Tracks_And_Timecodes:
                extractor.ExtractMKVSegmentsThreaded(p);
                if (!extractor.Abort) extractor.ExtractMKVTimecodesThreaded(p);
                break;
            case ExtractionMode.Tracks_And_Cues:
                extractor.ExtractMKVSegmentsThreaded(p);
                if (!extractor.Abort) extractor.ExtractMKVCuesThreaded(p);
                break;
            case ExtractionMode.Tracks_And_Cues_And_Timecodes:
                extractor.ExtractMKVSegmentsThreaded(p);
                if (!extractor.Abort) extractor.ExtractMKVCuesThreaded(p);
                if (!extractor.Abort) extractor.ExtractMKVTimecodesThreaded(p);
                break;
            default:
                throw new NotSupportedException($"Unsupported extraction mode: {job.ExtractionMode}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  暂停 / 恢复 / 中止
    // ─────────────────────────────────────────────────────────────────────────
    private void Pause()
    {
        if (!_isRunning || _isPaused) return;
        _pauseGate.Wait();  // 占用令牌，RunAllAsync 会在下次 WaitIfPausedAsync 时阻塞
        IsPaused = true;
        StatusText = "已暂停";
    }

    private void Resume()
    {
        if (!_isPaused) return;
        _pauseGate.Release();
        IsPaused = false;
        StatusText = "已恢复";
    }

    private void AbortAll()
    {
        _isAborted = true;
        if (_activeExtractor is not null)
        {
            _activeExtractor.Abort    = true;
            _activeExtractor.AbortAll = true;
        }
        // 如果当前处于暂停，需要先释放门控以便循环能退出
        if (_isPaused)
        {
            _pauseGate.Release();
            IsPaused = false;
        }
        StatusText = "正在中止…";
    }

    private async Task WaitIfPausedAsync(JobItem job)
    {
        if (_isPaused)
        {
            job.Status = JobStatus.Paused;
        }
        // 等待令牌可用（Pause 会占用，Resume 会释放）
        await _pauseGate.WaitAsync();
        _pauseGate.Release();          // 立即归还，仅用于阻塞
        if (job.Status == JobStatus.Paused)
            job.Status = JobStatus.Pending;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  完成摘要
    // ─────────────────────────────────────────────────────────────────────────
    private void BuildSummary(List<JobItem> processed, TimeSpan total)
    {
        CompletedCount = processed.Count(j => j.Status == JobStatus.Completed);
        FailedCount    = processed.Count(j => j.Status == JobStatus.Failed);
        SkippedCount   = processed.Count(j => j.Status == JobStatus.Skipped);

        var m = (int)total.TotalMinutes;
        var s = total.Seconds;
        TotalDurationLabel = $"{m}:{s:D2}";

        if (_isAborted)
            StatusText = $"已中止 — 完成 {CompletedCount}，失败 {FailedCount}，跳过 {SkippedCount}";
        else if (FailedCount > 0)
            StatusText = $"完成（有失败）— 成功 {CompletedCount}，失败 {FailedCount}";
        else
            StatusText = $"全部完成 ✓ — 共 {CompletedCount} 个任务";

        IsSummaryVisible = true;
        OverallProgress  = 100;
        Notify(nameof(CanRunAll));
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  INotifyPropertyChanged
    // ─────────────────────────────────────────────────────────────────────────
    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? prop = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Notify(prop);
        return true;
    }

    private void Notify([CallerMemberName] string? prop = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop ?? string.Empty));
}
