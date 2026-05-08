using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using gMKVToolNix.MkvExtract;
using gMKVToolNix.MkvMerge;
using gMKVToolNix.Platform;
using gMKVToolNix.Segments;
using gMKVToolNix.UI.Views;

namespace gMKVToolNix.UI.ViewModels;

public class MainWindowViewModel : INotifyPropertyChanged
{
    public ObservableCollection<InputFileItem> InputFiles { get; } = new();
    public ObservableCollection<TrackItem> Tracks { get; } = new();
    public ObservableCollection<string> ChapterTypes { get; }
    public ObservableCollection<string> ExtractionModes { get; }

    private InputFileItem? _selectedInputFile;
    public InputFileItem? SelectedInputFile
    {
        get => _selectedInputFile;
        set { if (SetField(ref _selectedInputFile, value)) _ = ReloadTracksForSelectedFileAsync(); }
    }

    private string _mkvToolnixPath = string.Empty;
    public string MkvToolnixPath
    {
        get => _mkvToolnixPath;
        set => SetField(ref _mkvToolnixPath, value);
    }

    private string _outputDirectory = string.Empty;
    public string OutputDirectory
    {
        get => _outputDirectory;
        set => SetField(ref _outputDirectory, value);
    }

    private bool _useSourceDirectory = true;
    public bool UseSourceDirectory
    {
        get => _useSourceDirectory;
        set => SetField(ref _useSourceDirectory, value);
    }

    private bool _appendOnDragAndDrop;
    public bool AppendOnDragAndDrop
    {
        get => _appendOnDragAndDrop;
        set => SetField(ref _appendOnDragAndDrop, value);
    }

    private bool _overwriteExistingFiles;
    public bool OverwriteExistingFiles
    {
        get => _overwriteExistingFiles;
        set => SetField(ref _overwriteExistingFiles, value);
    }

    private bool _disableTooltips;
    public bool DisableTooltips
    {
        get => _disableTooltips;
        set => SetField(ref _disableTooltips, value);
    }

    private bool _showPopup = true;
    public bool ShowPopup
    {
        get => _showPopup;
        set => SetField(ref _showPopup, value);
    }

    // ========== 文件名 pattern（供 Options 窗口编辑） ==========
    private string _videoPattern = "{FilenameNoExt}_track{TrackNumber}_[{Language}]";
    public string VideoPattern { get => _videoPattern; set => SetField(ref _videoPattern, value); }

    private string _audioPattern = "{FilenameNoExt}_track{TrackNumber}_[{Language}]_DELAY {EffectiveDelay}ms";
    public string AudioPattern { get => _audioPattern; set => SetField(ref _audioPattern, value); }

    private string _subtitlePattern = "{FilenameNoExt}_track{TrackNumber}_[{Language}]";
    public string SubtitlePattern { get => _subtitlePattern; set => SetField(ref _subtitlePattern, value); }

    private string _chapterPattern = "{FilenameNoExt}_chapters";
    public string ChapterPattern { get => _chapterPattern; set => SetField(ref _chapterPattern, value); }

    private string _attachmentPattern = "{AttachmentFilename}";
    public string AttachmentPattern { get => _attachmentPattern; set => SetField(ref _attachmentPattern, value); }

    private string _tagsPattern = "{FilenameNoExt}_tags";
    public string TagsPattern { get => _tagsPattern; set => SetField(ref _tagsPattern, value); }

    // ========== Raw 提取模式 ==========
    private bool _disableBomForTextFiles;
    public bool DisableBomForTextFiles { get => _disableBomForTextFiles; set => SetField(ref _disableBomForTextFiles, value); }

    private bool _useRawExtractionMode;
    public bool UseRawExtractionMode { get => _useRawExtractionMode; set => SetField(ref _useRawExtractionMode, value); }

    private bool _useFullRawExtractionMode;
    public bool UseFullRawExtractionMode { get => _useFullRawExtractionMode; set => SetField(ref _useFullRawExtractionMode, value); }

    private string _selectedChapterType = "XML";
    public string SelectedChapterType
    {
        get => _selectedChapterType;
        set => SetField(ref _selectedChapterType, value);
    }

    private string _selectedExtractionMode = "Tracks";
    public string SelectedExtractionMode
    {
        get => _selectedExtractionMode;
        set => SetField(ref _selectedExtractionMode, value);
    }

    private string _statusText = "就绪。";
    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

    private string _tracksSummary = string.Empty;
    public string TracksSummary
    {
        get => _tracksSummary;
        set => SetField(ref _tracksSummary, value);
    }

    private bool _isDragOver;
    public bool IsDragOver
    {
        get => _isDragOver;
        set => SetField(ref _isDragOver, value);
    }

    private bool _isExtracting;
    public bool IsExtracting
    {
        get => _isExtracting;
        set => SetField(ref _isExtracting, value);
    }

    private double _progressValue;
    public double ProgressValue
    {
        get => _progressValue;
        set => SetField(ref _progressValue, value);
    }

    private string _progressText = string.Empty;
    public string ProgressText
    {
        get => _progressText;
        set => SetField(ref _progressText, value);
    }

    public bool HasFiles => InputFiles.Count > 0;
    public bool HasNoFiles => InputFiles.Count == 0;

    public ICommand BrowseInputCommand { get; }
    public ICommand BrowseOutputCommand { get; }
    public ICommand BrowseMkvToolnixPathCommand { get; }
    public ICommand AutoDetectMkvToolnixCommand { get; }
    public ICommand SelectTracksCommand { get; }
    public ICommand RemoveSelectedFileCommand { get; }
    public ICommand ClearInputFilesCommand { get; }
    public ICommand ExtractCommand { get; }
    public ICommand AddJobCommand { get; }
    public ICommand AbortCommand { get; }
    public ICommand AbortAllCommand { get; }
    public ICommand ShowLogCommand { get; }
    public ICommand ShowJobsCommand { get; }
    public ICommand ShowOptionsCommand { get; }

    private gMKVExtract? _extractor;
    private LogWindow? _logWindow;
    private JobsWindow? _jobsWindow;

    public MainWindowViewModel()
    {
        ChapterTypes = new ObservableCollection<string>(Enum.GetNames(typeof(MkvChapterTypes)));
        ExtractionModes = new ObservableCollection<string>(Enum.GetNames(typeof(ExtractionMode)));

        InputFiles.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasFiles));
            OnPropertyChanged(nameof(HasNoFiles));
        };

        BrowseInputCommand = new RelayCommand(async () => await BrowseInputAsync());
        BrowseOutputCommand = new RelayCommand(async () => await BrowseOutputAsync());
        BrowseMkvToolnixPathCommand = new RelayCommand(async () => await BrowseMkvToolnixPathAsync());
        AutoDetectMkvToolnixCommand = new RelayCommand(AutoDetectMkvToolnix);
        SelectTracksCommand = new RelayCommand(SelectAllTracks);
        RemoveSelectedFileCommand = new RelayCommand(RemoveSelectedFile);
        ClearInputFilesCommand = new RelayCommand(ClearInputFiles);
        ExtractCommand = new RelayCommand(async () => await ExtractAsync());
        AddJobCommand = new RelayCommand(() => StatusText = "加入队列：尚未实现（Round 4）");
        AbortCommand = new RelayCommand(Abort);
        AbortAllCommand = new RelayCommand(AbortAll);
        ShowLogCommand = new RelayCommand(ShowLog);
        ShowJobsCommand = new RelayCommand(ShowJobs);
        ShowOptionsCommand = new RelayCommand(ShowOptions);

        // 启动时自动探测一次
        AutoDetectMkvToolnix();
    }

    public void AddInputFiles(IEnumerable<string> paths)
    {
        if (!AppendOnDragAndDrop)
        {
            InputFiles.Clear();
        }

        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            if (InputFiles.Any(f => f.FullPath == path)) continue;
            if (!File.Exists(path) && !Directory.Exists(path)) continue;

            if (Directory.Exists(path))
            {
                foreach (var f in Directory.EnumerateFiles(path, "*.*", SearchOption.TopDirectoryOnly))
                {
                    var ext = Path.GetExtension(f).ToLowerInvariant();
                    if (ext is ".mkv" or ".mka" or ".mks" or ".webm")
                    {
                        if (!InputFiles.Any(x => x.FullPath == f))
                            InputFiles.Add(new InputFileItem(f));
                    }
                }
            }
            else
            {
                InputFiles.Add(new InputFileItem(path));
            }
        }

        if (SelectedInputFile is null && InputFiles.Count > 0)
        {
            SelectedInputFile = InputFiles[0];
        }
        StatusText = $"已加载 {InputFiles.Count} 个文件";
    }

    private void AutoDetectMkvToolnix()
    {
        var dir = MkvToolnixLocator.Locate();
        if (!string.IsNullOrEmpty(dir))
        {
            MkvToolnixPath = dir;
            StatusText = $"探测到 MKVToolnix：{dir}";
        }
        else
        {
            StatusText = "未找到 MKVToolnix。请安装（macOS: brew install mkvtoolnix）后点击「自动探测」或手动浏览。";
        }
    }

    private async Task BrowseMkvToolnixPathAsync()
    {
        var top = GetTopLevel();
        if (top is null) return;
        var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择 MKVToolnix 安装目录",
            AllowMultiple = false
        });
        var dir = folders.FirstOrDefault()?.Path.LocalPath;
        if (!string.IsNullOrEmpty(dir)) MkvToolnixPath = dir!;
    }

    private async Task BrowseInputAsync()
    {
        var top = GetTopLevel();
        if (top is null) return;
        var picker = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择 MKV 文件",
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType("Matroska") { Patterns = new[] { "*.mkv", "*.mka", "*.mks", "*.webm" } }
            ]
        });
        var paths = picker.Select(f => f.Path.LocalPath)
                          .Where(p => !string.IsNullOrEmpty(p))
                          .ToList();
        if (paths.Count > 0) AddInputFiles(paths);
    }

    private async Task BrowseOutputAsync()
    {
        var top = GetTopLevel();
        if (top is null) return;
        var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择输出目录",
            AllowMultiple = false
        });
        var dir = folders.FirstOrDefault()?.Path.LocalPath;
        if (!string.IsNullOrEmpty(dir))
        {
            OutputDirectory = dir!;
            UseSourceDirectory = false;
        }
    }

    private void SelectAllTracks()
    {
        foreach (var t in Tracks) t.IsSelected = true;
    }

    private void RemoveSelectedFile()
    {
        if (SelectedInputFile is null) return;
        var idx = InputFiles.IndexOf(SelectedInputFile);
        InputFiles.Remove(SelectedInputFile);
        if (InputFiles.Count > 0)
        {
            SelectedInputFile = InputFiles[Math.Min(idx, InputFiles.Count - 1)];
        }
        else
        {
            SelectedInputFile = null;
            Tracks.Clear();
            TracksSummary = string.Empty;
        }
    }

    private void ClearInputFiles()
    {
        InputFiles.Clear();
        SelectedInputFile = null;
        Tracks.Clear();
        TracksSummary = string.Empty;
        StatusText = "已清空文件列表";
    }

    private async Task ReloadTracksForSelectedFileAsync()
    {
        Tracks.Clear();
        TracksSummary = string.Empty;
        if (SelectedInputFile is null || !File.Exists(SelectedInputFile.FullPath))
        {
            return;
        }
        if (string.IsNullOrEmpty(MkvToolnixPath))
        {
            StatusText = "未配置 MKVToolnix 路径，无法解析轨道";
            return;
        }

        var path = SelectedInputFile.FullPath;
        StatusText = $"正在解析：{Path.GetFileName(path)}…";

        try
        {
            var segments = await Task.Run(() =>
            {
                var merge = new gMKVMerge(MkvToolnixPath);
                return merge.GetMKVSegments(path);
            });

            int v = 0, a = 0, s = 0, c = 0, att = 0;
            foreach (var seg in segments)
            {
                if (seg is gMKVTrack tr)
                {
                    var item = new TrackItem
                    {
                        IsSelected = true,
                        Display = BuildTrackDisplay(tr),
                        TypeLabel = TrackTypeLabel(tr.TrackType),
                        TypeColor = TrackTypeBrush(tr.TrackType),
                        TrackNumber = tr.TrackNumber,
                        TrackId = tr.TrackID,
                        Segment = tr,
                    };
                    Tracks.Add(item);

                    switch (tr.TrackType)
                    {
                        case MkvTrackType.video: v++; break;
                        case MkvTrackType.audio: a++; break;
                        case MkvTrackType.subtitles: s++; break;
                    }
                }
                else if (seg is gMKVChapter ch)
                {
                    Tracks.Add(new TrackItem
                    {
                        IsSelected = false,
                        Display = $"章节（{ch.ChapterCount} 个）",
                        TypeLabel = "章节",
                        TypeColor = new SolidColorBrush(Color.FromRgb(0xFF, 0xF0, 0xC0)),
                        Segment = ch,
                    });
                    c++;
                }
                else if (seg is gMKVAttachment at)
                {
                    Tracks.Add(new TrackItem
                    {
                        IsSelected = false,
                        Display = $"附件：{at.Filename}（{at.MimeType}）",
                        TypeLabel = "附件",
                        TypeColor = new SolidColorBrush(Color.FromRgb(0xCD, 0xE4, 0xF4)),
                        Segment = at,
                    });
                    att++;
                }
            }

            TracksSummary = $"视频 {v} · 音频 {a} · 字幕 {s} · 章节 {c} · 附件 {att}";
            StatusText = $"已解析：{Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            StatusText = $"解析失败：{ex.Message}";
        }
    }

    private static string BuildTrackDisplay(gMKVTrack tr)
    {
        var parts = new List<string>();
        parts.Add($"#{tr.TrackNumber}");
        parts.Add(tr.CodecID ?? "?");
        if (!string.IsNullOrWhiteSpace(tr.Language)) parts.Add($"[{tr.Language}]");
        if (!string.IsNullOrWhiteSpace(tr.TrackName)) parts.Add(tr.TrackName);
        if (!string.IsNullOrWhiteSpace(tr.ExtraInfo)) parts.Add($"({tr.ExtraInfo})");
        return string.Join("  ", parts);
    }

    private static string TrackTypeLabel(MkvTrackType t) => t switch
    {
        MkvTrackType.video => "视频",
        MkvTrackType.audio => "音频",
        MkvTrackType.subtitles => "字幕",
        _ => t.ToString(),
    };

    private static IBrush TrackTypeBrush(MkvTrackType t) => t switch
    {
        MkvTrackType.video => new SolidColorBrush(Color.FromRgb(0xFF, 0xD1, 0xDC)),     // pink
        MkvTrackType.audio => new SolidColorBrush(Color.FromRgb(0xC4, 0xE8, 0xD5)),     // mint
        MkvTrackType.subtitles => new SolidColorBrush(Color.FromRgb(0xDC, 0xD0, 0xF0)), // lavender
        _ => new SolidColorBrush(Color.FromRgb(0xFD, 0xF6, 0xF0)),
    };

    private async Task ExtractAsync()
    {
        if (SelectedInputFile is null)
        {
            StatusText = "请先选择一个输入文件";
            return;
        }
        if (string.IsNullOrEmpty(MkvToolnixPath))
        {
            StatusText = "未配置 MKVToolnix 路径";
            return;
        }
        var selectedTracks = Tracks.Where(t => t.IsSelected).ToList();
        if (selectedTracks.Count == 0)
        {
            StatusText = "请勾选至少一个轨道";
            return;
        }

        IsExtracting = true;
        ProgressValue = 0;
        ProgressText = "0%";
        StatusText = "开始提取…";

        try
        {
            var inputPath = SelectedInputFile.FullPath;
            var outputDir = UseSourceDirectory
                ? Path.GetDirectoryName(inputPath) ?? string.Empty
                : OutputDirectory;

            if (string.IsNullOrEmpty(outputDir))
            {
                StatusText = "未设置输出目录";
                return;
            }
            Directory.CreateDirectory(outputDir);

            _extractor = new gMKVExtract(MkvToolnixPath);
            _extractor.MkvExtractProgressUpdated += progress =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    ProgressValue = progress;
                    ProgressText = $"{progress}%";
                });
            };
            _extractor.MkvExtractTrackUpdated += (filename, trackName) =>
            {
                Dispatcher.UIThread.Post(() => StatusText = $"正在提取：{trackName}");
            };

            var segments = selectedTracks
                .Select(t => t.Segment)
                .Where(s => s is not null)
                .Cast<gMKVSegment>()
                .ToList();

            var chapterType = Enum.TryParse<MkvChapterTypes>(SelectedChapterType, out var ct) ? ct : MkvChapterTypes.XML;
            var extractionMode = Enum.TryParse<ExtractionMode>(SelectedExtractionMode, out var em) ? em : ExtractionMode.Tracks;

            var parameters = new gMKVExtractSegmentsParameters
            {
                MKVFile = inputPath,
                MKVSegmentsToExtract = segments,
                OutputDirectory = outputDir,
                ChapterType = chapterType,
                TimecodesExtractionMode = TimecodesExtractionMode.NoTimecodes,
                CueExtractionMode = CuesExtractionMode.NoCues,
                FilenamePatterns = new gMKVExtractFilenamePatterns
                {
                    VideoTrackFilenamePattern = VideoPattern,
                    AudioTrackFilenamePattern = AudioPattern,
                    SubtitleTrackFilenamePattern = SubtitlePattern,
                    ChapterFilenamePattern = ChapterPattern,
                    AttachmentFilenamePattern = AttachmentPattern,
                    TagsFilenamePattern = TagsPattern,
                },
                OverwriteExistingFile = OverwriteExistingFiles,
                DisableBomForTextFiles = DisableBomForTextFiles,
                UseRawExtractionMode = UseRawExtractionMode,
                UseFullRawExtractionMode = UseFullRawExtractionMode,
            };

            await Task.Run(() =>
            {
                switch (extractionMode)
                {
                    case ExtractionMode.Tracks:
                        _extractor.ExtractMKVSegmentsThreaded(parameters);
                        break;
                    case ExtractionMode.Timecodes:
                        _extractor.ExtractMKVTimecodesThreaded(parameters);
                        break;
                    case ExtractionMode.Cues:
                        _extractor.ExtractMKVCuesThreaded(parameters);
                        break;
                    case ExtractionMode.Cue_Sheet:
                        _extractor.ExtractMkvCuesheetThreaded(parameters);
                        break;
                    case ExtractionMode.Tags:
                        _extractor.ExtractMkvTagsThreaded(parameters);
                        break;
                    case ExtractionMode.Tracks_And_Timecodes:
                        _extractor.ExtractMKVSegmentsThreaded(parameters);
                        if (!_extractor.Abort) _extractor.ExtractMKVTimecodesThreaded(parameters);
                        break;
                    case ExtractionMode.Tracks_And_Cues:
                        _extractor.ExtractMKVSegmentsThreaded(parameters);
                        if (!_extractor.Abort) _extractor.ExtractMKVCuesThreaded(parameters);
                        break;
                    case ExtractionMode.Tracks_And_Cues_And_Timecodes:
                        _extractor.ExtractMKVSegmentsThreaded(parameters);
                        if (!_extractor.Abort) _extractor.ExtractMKVCuesThreaded(parameters);
                        if (!_extractor.Abort) _extractor.ExtractMKVTimecodesThreaded(parameters);
                        break;
                }
            });

            if (_extractor.ThreadedException is not null)
            {
                StatusText = $"提取失败：{_extractor.ThreadedException.Message}";
            }
            else if (_extractor.Abort)
            {
                StatusText = "已中止";
            }
            else
            {
                StatusText = "提取完成 ✓";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"错误：{ex.Message}";
        }
        finally
        {
            IsExtracting = false;
        }
    }

    private void ShowLog()
    {
        if (_logWindow is null || !_logWindow.IsVisible)
        {
            _logWindow = new LogWindow();
            _logWindow.Closed += (_, _) => _logWindow = null;
            _logWindow.Show();
        }
        else
        {
            _logWindow.Activate();
        }
    }

    private void ShowJobs()
    {
        if (_jobsWindow is null || !_jobsWindow.IsVisible)
        {
            _jobsWindow = new JobsWindow();
            _jobsWindow.Closed += (_, _) => _jobsWindow = null;
            _jobsWindow.Show();
        }
        else
        {
            _jobsWindow.Activate();
        }
    }

    private void ShowOptions()
    {
        var owner = GetTopLevel() as Window;
        if (owner is null) return;
        var win = new OptionsWindow { DataContext = this };
        _ = win.ShowDialog(owner);
    }

    private void Abort()
    {
        if (_extractor is not null) _extractor.Abort = true;
    }

    private void AbortAll()
    {
        if (_extractor is not null)
        {
            _extractor.Abort = true;
            _extractor.AbortAll = true;
        }
    }

    private static TopLevel? GetTopLevel()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }
        return null;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? prop = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(prop);
        return true;
    }
    private void OnPropertyChanged([CallerMemberName] string? prop = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop ?? string.Empty));
}

public class InputFileItem
{
    public string FullPath { get; }
    public string DisplayName { get; }
    public InputFileItem(string fullPath)
    {
        FullPath = fullPath;
        DisplayName = Path.GetFileName(fullPath);
    }
    public override string ToString() => DisplayName;
}

public class TrackItem : INotifyPropertyChanged
{
    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }
    public string Display { get; set; } = string.Empty;
    public string TypeLabel { get; set; } = string.Empty;
    public IBrush TypeColor { get; set; } = new SolidColorBrush(Colors.LightGray);
    public int TrackNumber { get; set; }
    public int TrackId { get; set; }
    public gMKVToolNix.Segments.gMKVSegment? Segment { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
}

internal sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    public RelayCommand(Action execute) { _execute = execute; }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute();
    public event EventHandler? CanExecuteChanged { add { } remove { } }
}
