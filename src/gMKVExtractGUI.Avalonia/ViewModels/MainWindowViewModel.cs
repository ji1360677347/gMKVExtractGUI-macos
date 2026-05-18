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
using gMKVToolNix.UI.Services;
using gMKVToolNix.UI.ViewModels;
using gMKVToolNix.UI.Views;

namespace gMKVToolNix.UI.ViewModels;

public class MainWindowViewModel : INotifyPropertyChanged
{
    private static readonly HashSet<string> SupportedInputExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mkv",
        ".mka",
        ".mks",
        ".webm",
    };

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
        set
        {
            if (SetField(ref _useSourceDirectory, value))
            {
                if (value)
                {
                    _outputDirectoryStrategy = OutputDirectoryStrategy.SourceDirectory;
                }
                else if (_outputDirectoryStrategy == OutputDirectoryStrategy.SourceDirectory)
                {
                    _outputDirectoryStrategy = OutputDirectoryStrategy.CustomDirectory;
                }
            }
        }
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

    private string _checkAllTracksHeader = "勾选全部轨道 (0/0)";
    public string CheckAllTracksHeader { get => _checkAllTracksHeader; set => SetField(ref _checkAllTracksHeader, value); }

    private string _uncheckAllTracksHeader = "取消全部轨道 (0/0)";
    public string UncheckAllTracksHeader { get => _uncheckAllTracksHeader; set => SetField(ref _uncheckAllTracksHeader, value); }

    private string _checkVideoTracksHeader = "勾选视频轨道... (0/0)";
    public string CheckVideoTracksHeader { get => _checkVideoTracksHeader; set => SetField(ref _checkVideoTracksHeader, value); }

    private string _uncheckVideoTracksHeader = "取消视频轨道... (0/0)";
    public string UncheckVideoTracksHeader { get => _uncheckVideoTracksHeader; set => SetField(ref _uncheckVideoTracksHeader, value); }

    private string _checkAudioTracksHeader = "勾选音频轨道... (0/0)";
    public string CheckAudioTracksHeader { get => _checkAudioTracksHeader; set => SetField(ref _checkAudioTracksHeader, value); }

    private string _uncheckAudioTracksHeader = "取消音频轨道... (0/0)";
    public string UncheckAudioTracksHeader { get => _uncheckAudioTracksHeader; set => SetField(ref _uncheckAudioTracksHeader, value); }

    private string _checkSubtitleTracksHeader = "勾选字幕轨道... (0/0)";
    public string CheckSubtitleTracksHeader { get => _checkSubtitleTracksHeader; set => SetField(ref _checkSubtitleTracksHeader, value); }

    private string _uncheckSubtitleTracksHeader = "取消字幕轨道... (0/0)";
    public string UncheckSubtitleTracksHeader { get => _uncheckSubtitleTracksHeader; set => SetField(ref _uncheckSubtitleTracksHeader, value); }

    private string _checkChapterTracksHeader = "勾选章节轨道... (0/0)";
    public string CheckChapterTracksHeader { get => _checkChapterTracksHeader; set => SetField(ref _checkChapterTracksHeader, value); }

    private string _uncheckChapterTracksHeader = "取消章节轨道... (0/0)";
    public string UncheckChapterTracksHeader { get => _uncheckChapterTracksHeader; set => SetField(ref _uncheckChapterTracksHeader, value); }

    private string _checkAttachmentTracksHeader = "勾选附件轨道... (0/0)";
    public string CheckAttachmentTracksHeader { get => _checkAttachmentTracksHeader; set => SetField(ref _checkAttachmentTracksHeader, value); }

    private string _uncheckAttachmentTracksHeader = "取消附件轨道... (0/0)";
    public string UncheckAttachmentTracksHeader { get => _uncheckAttachmentTracksHeader; set => SetField(ref _uncheckAttachmentTracksHeader, value); }

    private string _allVideoTracksHeader = "全部视频轨道 (0/0)";
    public string AllVideoTracksHeader { get => _allVideoTracksHeader; set => SetField(ref _allVideoTracksHeader, value); }

    private string _allAudioTracksHeader = "全部音频轨道 (0/0)";
    public string AllAudioTracksHeader { get => _allAudioTracksHeader; set => SetField(ref _allAudioTracksHeader, value); }

    private string _allSubtitleTracksHeader = "全部字幕轨道 (0/0)";
    public string AllSubtitleTracksHeader { get => _allSubtitleTracksHeader; set => SetField(ref _allSubtitleTracksHeader, value); }

    private string _allChapterTracksHeader = "全部章节轨道 (0/0)";
    public string AllChapterTracksHeader { get => _allChapterTracksHeader; set => SetField(ref _allChapterTracksHeader, value); }

    private string _allAttachmentTracksHeader = "全部附件轨道 (0/0)";
    public string AllAttachmentTracksHeader { get => _allAttachmentTracksHeader; set => SetField(ref _allAttachmentTracksHeader, value); }

    private string _uncheckedVideoTracksHeader = "全部视频轨道 (0/0)";
    public string UncheckedVideoTracksHeader { get => _uncheckedVideoTracksHeader; set => SetField(ref _uncheckedVideoTracksHeader, value); }

    private string _uncheckedAudioTracksHeader = "全部音频轨道 (0/0)";
    public string UncheckedAudioTracksHeader { get => _uncheckedAudioTracksHeader; set => SetField(ref _uncheckedAudioTracksHeader, value); }

    private string _uncheckedSubtitleTracksHeader = "全部字幕轨道 (0/0)";
    public string UncheckedSubtitleTracksHeader { get => _uncheckedSubtitleTracksHeader; set => SetField(ref _uncheckedSubtitleTracksHeader, value); }

    private string _uncheckedChapterTracksHeader = "全部章节轨道 (0/0)";
    public string UncheckedChapterTracksHeader { get => _uncheckedChapterTracksHeader; set => SetField(ref _uncheckedChapterTracksHeader, value); }

    private string _uncheckedAttachmentTracksHeader = "全部附件轨道 (0/0)";
    public string UncheckedAttachmentTracksHeader { get => _uncheckedAttachmentTracksHeader; set => SetField(ref _uncheckedAttachmentTracksHeader, value); }

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
    public ICommand CheckAllTracksCommand { get; }
    public ICommand UncheckAllTracksCommand { get; }
    public ICommand CheckVideoTracksCommand { get; }
    public ICommand UncheckVideoTracksCommand { get; }
    public ICommand CheckAudioTracksCommand { get; }
    public ICommand UncheckAudioTracksCommand { get; }
    public ICommand CheckSubtitleTracksCommand { get; }
    public ICommand UncheckSubtitleTracksCommand { get; }
    public ICommand CheckChapterTracksCommand { get; }
    public ICommand UncheckChapterTracksCommand { get; }
    public ICommand CheckAttachmentTracksCommand { get; }
    public ICommand UncheckAttachmentTracksCommand { get; }

    public event Action<IReadOnlyList<string>>? UnsupportedInputFilesRejected;
    public event Action<int, int>? ExtractionCompleted;

    private gMKVExtract? _extractor;
    private LogWindow? _logWindow;
    private OutputDirectoryStrategy _outputDirectoryStrategy = OutputDirectoryStrategy.SourceDirectory;

    // 全局单例 JobsWindow 实例（与 JobsWindowViewModel.Instance 配套）
    private static JobsWindow? _sharedJobsWindow;

    public MainWindowViewModel()
    {
        ChapterTypes = new ObservableCollection<string>(Enum.GetNames(typeof(MkvChapterTypes)));
        ExtractionModes = new ObservableCollection<string>(Enum.GetNames(typeof(ExtractionMode)));

        InputFiles.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasFiles));
            OnPropertyChanged(nameof(HasNoFiles));
            RefreshTrackSelectionSummary();
        };

        BrowseInputCommand = new RelayCommand(async () => await BrowseInputAsync());
        BrowseOutputCommand = new RelayCommand(async () => await BrowseOutputAsync());
        BrowseMkvToolnixPathCommand = new RelayCommand(async () => await BrowseMkvToolnixPathAsync());
        AutoDetectMkvToolnixCommand = new RelayCommand(AutoDetectMkvToolnix);
        SelectTracksCommand = new RelayCommand(SelectAllTracks);
        RemoveSelectedFileCommand = new RelayCommand(RemoveSelectedFile);
        ClearInputFilesCommand = new RelayCommand(ClearInputFiles);
        ExtractCommand = new RelayCommand(async () => await ExtractAsync());
        AddJobCommand = new RelayCommand(async () => await AddCurrentSelectionToQueueAsync());
        AbortCommand = new RelayCommand(Abort);
        AbortAllCommand = new RelayCommand(AbortAll);
        ShowLogCommand = new RelayCommand(ShowLog);
        ShowJobsCommand = new RelayCommand(ShowJobs);
        ShowOptionsCommand = new RelayCommand(ShowOptions);
        CheckAllTracksCommand = new RelayCommand(() => SetTracksChecked(_ => true, true));
        UncheckAllTracksCommand = new RelayCommand(() => SetTracksChecked(_ => true, false));
        CheckVideoTracksCommand = new RelayCommand(() => SetTracksChecked(IsVideoTrack, true));
        UncheckVideoTracksCommand = new RelayCommand(() => SetTracksChecked(IsVideoTrack, false));
        CheckAudioTracksCommand = new RelayCommand(() => SetTracksChecked(IsAudioTrack, true));
        UncheckAudioTracksCommand = new RelayCommand(() => SetTracksChecked(IsAudioTrack, false));
        CheckSubtitleTracksCommand = new RelayCommand(() => SetTracksChecked(IsSubtitleTrack, true));
        UncheckSubtitleTracksCommand = new RelayCommand(() => SetTracksChecked(IsSubtitleTrack, false));
        CheckChapterTracksCommand = new RelayCommand(() => SetTracksChecked(IsChapterTrack, true));
        UncheckChapterTracksCommand = new RelayCommand(() => SetTracksChecked(IsChapterTrack, false));
        CheckAttachmentTracksCommand = new RelayCommand(() => SetTracksChecked(IsAttachmentTrack, true));
        UncheckAttachmentTracksCommand = new RelayCommand(() => SetTracksChecked(IsAttachmentTrack, false));

        SyncFromSettings();

        // 启动时仅在没有持久化路径时自动探测
        if (string.IsNullOrEmpty(MkvToolnixPath))
        {
            AutoDetectMkvToolnix();
        }
        else
        {
            StatusText = $"已恢复设置：{MkvToolnixPath}";
        }
    }

    public void AddInputFiles(IEnumerable<string> paths)
    {
        var rejectedFiles = new List<string>();
        var addedFiles = new List<InputFileItem>();

        if (!AppendOnDragAndDrop)
        {
            InputFiles.Clear();
            Tracks.Clear();
            TracksSummary = string.Empty;
            SelectedInputFile = null;
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
                    if (IsSupportedInputFile(f))
                    {
                        if (!InputFiles.Any(x => x.FullPath == f))
                        {
                            var item = new InputFileItem(f);
                            InputFiles.Add(item);
                            addedFiles.Add(item);
                        }
                    }
                    else
                    {
                        rejectedFiles.Add(f);
                    }
                }
            }
            else
            {
                if (!IsSupportedInputFile(path))
                {
                    rejectedFiles.Add(path);
                    continue;
                }

                var item = new InputFileItem(path);
                InputFiles.Add(item);
                addedFiles.Add(item);
            }
        }

        if (SelectedInputFile is null && InputFiles.Count > 0)
        {
            SelectedInputFile = InputFiles[0];
        }
        if (rejectedFiles.Count > 0)
        {
            UnsupportedInputFilesRejected?.Invoke(rejectedFiles);
            StatusText = $"已跳过 {rejectedFiles.Count} 个不支持的文件，当前已加载 {InputFiles.Count} 个文件";
        }
        else
        {
            StatusText = $"已加载 {InputFiles.Count} 个文件";
        }

        if (addedFiles.Count > 0)
        {
            _ = LoadTracksForFilesAsync(addedFiles);
        }
    }

    private static bool IsSupportedInputFile(string path)
        => SupportedInputExtensions.Contains(Path.GetExtension(path));

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
            _outputDirectoryStrategy = OutputDirectoryStrategy.CustomDirectory;
        }
    }

    private void SelectAllTracks()
    {
        SetTracksChecked(_ => true, true);
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

        var file = SelectedInputFile;
        var path = file.FullPath;
        StatusText = $"正在解析：{Path.GetFileName(path)}…";

        try
        {
            await LoadTracksForFileAsync(file);
            ShowTracksForFile(file);
            StatusText = $"已解析：{Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            StatusText = $"解析失败：{ex.Message}";
        }
    }

    private async Task LoadTracksForFilesAsync(IEnumerable<InputFileItem> files)
    {
        foreach (var file in files.ToList())
        {
            if (!InputFiles.Contains(file)) continue;
            await LoadTracksForFileAsync(file);
        }

        if (SelectedInputFile is not null)
        {
            ShowTracksForFile(SelectedInputFile);
        }
    }

    private async Task LoadTracksForFileAsync(InputFileItem file)
    {
        if (file.TracksLoaded || file.IsLoadingTracks || !File.Exists(file.FullPath))
        {
            return;
        }
        if (string.IsNullOrEmpty(MkvToolnixPath))
        {
            StatusText = "未配置 MKVToolnix 路径，无法解析轨道";
            return;
        }

        file.IsLoadingTracks = true;
        StatusText = $"正在解析：{Path.GetFileName(file.FullPath)}…";

        try
        {
            var segments = await Task.Run(() =>
            {
                var merge = new gMKVMerge(MkvToolnixPath);
                return merge.GetMKVSegments(file.FullPath);
            });

            file.Tracks.Clear();
            int v = 0, a = 0, s = 0, c = 0, att = 0;
            foreach (var seg in segments)
            {
                if (seg is gMKVTrack tr)
                {
                    file.Tracks.Add(new TrackItem
                    {
                        IsSelected = false,
                        Display = BuildTrackDisplay(tr),
                        TypeLabel = TrackTypeLabel(tr.TrackType),
                        TypeColor = TrackTypeBrush(tr.TrackType),
                        TrackNumber = tr.TrackNumber,
                        TrackId = tr.TrackID,
                        Segment = tr,
                    });

                    switch (tr.TrackType)
                    {
                        case MkvTrackType.video: v++; break;
                        case MkvTrackType.audio: a++; break;
                        case MkvTrackType.subtitles: s++; break;
                    }
                }
                else if (seg is gMKVChapter ch)
                {
                    file.Tracks.Add(new TrackItem
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
                    file.Tracks.Add(new TrackItem
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

            file.TracksSummary = $"视频 {v} · 音频 {a} · 字幕 {s} · 章节 {c} · 附件 {att}";
            file.TracksLoaded = true;
            foreach (var track in file.Tracks)
            {
                track.SelectionChanged = RefreshTrackSelectionSummary;
            }
            RefreshTrackSelectionSummary();
        }
        catch (Exception ex)
        {
            file.TracksSummary = $"解析失败：{ex.Message}";
            StatusText = file.TracksSummary;
        }
        finally
        {
            file.IsLoadingTracks = false;
        }
    }

    private void ShowTracksForFile(InputFileItem file)
    {
        Tracks.Clear();
        foreach (var track in file.Tracks)
        {
            Tracks.Add(track);
        }
        RefreshTrackSelectionSummary();
    }

    private void SetTracksChecked(Func<TrackItem, bool> predicate, bool isSelected)
    {
        var changed = 0;
        foreach (var track in InputFiles.SelectMany(f => f.Tracks).Where(predicate))
        {
            if (track.IsSelected != isSelected)
            {
                track.IsSelected = isSelected;
                changed++;
            }
        }

        RefreshTrackSelectionSummary();
        StatusText = $"{(isSelected ? "已勾选" : "已取消")} {changed} 个轨道";
    }

    private void RefreshTrackSelectionSummary()
    {
        var allTracks = InputFiles.SelectMany(f => f.Tracks).ToList();
        var checkedAllTracksCount = allTracks.Count(t => t.IsSelected);
        var allTracksCount = allTracks.Count;

        var video = CountTracks(allTracks, IsVideoTrack);
        var audio = CountTracks(allTracks, IsAudioTrack);
        var subtitle = CountTracks(allTracks, IsSubtitleTrack);
        var chapter = CountTracks(allTracks, IsChapterTrack);
        var attachment = CountTracks(allTracks, IsAttachmentTrack);

        TracksSummary = $"{InputFiles.Count} 文件 · 已勾选 {checkedAllTracksCount}/{allTracksCount} 轨道";
        StatusText = allTracksCount == 0 ? StatusText : $"已勾选 {checkedAllTracksCount} 个轨道";

        CheckAllTracksHeader = $"勾选全部轨道 ({checkedAllTracksCount}/{allTracksCount})";
        UncheckAllTracksHeader = $"取消全部轨道 ({allTracksCount - checkedAllTracksCount}/{allTracksCount})";

        CheckVideoTracksHeader = $"勾选视频轨道... ({video.Checked}/{video.Total})";
        CheckAudioTracksHeader = $"勾选音频轨道... ({audio.Checked}/{audio.Total})";
        CheckSubtitleTracksHeader = $"勾选字幕轨道... ({subtitle.Checked}/{subtitle.Total})";
        CheckChapterTracksHeader = $"勾选章节轨道... ({chapter.Checked}/{chapter.Total})";
        CheckAttachmentTracksHeader = $"勾选附件轨道... ({attachment.Checked}/{attachment.Total})";

        UncheckVideoTracksHeader = $"取消视频轨道... ({video.Total - video.Checked}/{video.Total})";
        UncheckAudioTracksHeader = $"取消音频轨道... ({audio.Total - audio.Checked}/{audio.Total})";
        UncheckSubtitleTracksHeader = $"取消字幕轨道... ({subtitle.Total - subtitle.Checked}/{subtitle.Total})";
        UncheckChapterTracksHeader = $"取消章节轨道... ({chapter.Total - chapter.Checked}/{chapter.Total})";
        UncheckAttachmentTracksHeader = $"取消附件轨道... ({attachment.Total - attachment.Checked}/{attachment.Total})";

        AllVideoTracksHeader = $"全部视频轨道 ({video.Checked}/{video.Total})";
        AllAudioTracksHeader = $"全部音频轨道 ({audio.Checked}/{audio.Total})";
        AllSubtitleTracksHeader = $"全部字幕轨道 ({subtitle.Checked}/{subtitle.Total})";
        AllChapterTracksHeader = $"全部章节轨道 ({chapter.Checked}/{chapter.Total})";
        AllAttachmentTracksHeader = $"全部附件轨道 ({attachment.Checked}/{attachment.Total})";

        UncheckedVideoTracksHeader = $"全部视频轨道 ({video.Total - video.Checked}/{video.Total})";
        UncheckedAudioTracksHeader = $"全部音频轨道 ({audio.Total - audio.Checked}/{audio.Total})";
        UncheckedSubtitleTracksHeader = $"全部字幕轨道 ({subtitle.Total - subtitle.Checked}/{subtitle.Total})";
        UncheckedChapterTracksHeader = $"全部章节轨道 ({chapter.Total - chapter.Checked}/{chapter.Total})";
        UncheckedAttachmentTracksHeader = $"全部附件轨道 ({attachment.Total - attachment.Checked}/{attachment.Total})";
    }

    private static (int Checked, int Total) CountTracks(IEnumerable<TrackItem> tracks, Func<TrackItem, bool> predicate)
    {
        var filtered = tracks.Where(predicate).ToList();
        return (filtered.Count(t => t.IsSelected), filtered.Count);
    }

    private static bool IsVideoTrack(TrackItem item)
        => item.Segment is gMKVTrack { TrackType: MkvTrackType.video };

    private static bool IsAudioTrack(TrackItem item)
        => item.Segment is gMKVTrack { TrackType: MkvTrackType.audio };

    private static bool IsSubtitleTrack(TrackItem item)
        => item.Segment is gMKVTrack { TrackType: MkvTrackType.subtitles };

    private static bool IsChapterTrack(TrackItem item)
        => item.Segment is gMKVChapter;

    private static bool IsAttachmentTrack(TrackItem item)
        => item.Segment is gMKVAttachment;

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
        if (InputFiles.Count == 0)
        {
            StatusText = "请先添加输入文件";
            return;
        }
        if (string.IsNullOrEmpty(MkvToolnixPath))
        {
            StatusText = "未配置 MKVToolnix 路径";
            return;
        }

        await LoadTracksForFilesAsync(InputFiles);

        var selectedFiles = InputFiles
            .Select(file => new
            {
                File = file,
                Tracks = file.Tracks.Where(t => t.IsSelected).ToList(),
            })
            .Where(item => item.Tracks.Count > 0)
            .ToList();

        if (selectedFiles.Count == 0)
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
            SyncToSettings();

            var chapterType = Enum.TryParse<MkvChapterTypes>(SelectedChapterType, out var ct) ? ct : MkvChapterTypes.XML;
            var extractionMode = Enum.TryParse<ExtractionMode>(SelectedExtractionMode, out var em) ? em : ExtractionMode.Tracks;
            var completedFiles = 0;
            var completedTracks = 0;

            foreach (var selectedFile in selectedFiles)
            {
                var inputPath = selectedFile.File.FullPath;
                var outputDir = ResolveOutputDirectory(inputPath);

                if (string.IsNullOrEmpty(outputDir))
                {
                    StatusText = "未设置输出目录";
                    return;
                }
                Directory.CreateDirectory(outputDir);

                var segments = selectedFile.Tracks
                    .Select(t => t.Segment)
                    .Where(s => s is not null)
                    .Cast<gMKVSegment>()
                    .ToList();

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
                    ExistingFileHandling = ResolveExistingFileHandling(),
                    DisableBomForTextFiles = DisableBomForTextFiles,
                    UseRawExtractionMode = UseRawExtractionMode,
                    UseFullRawExtractionMode = UseFullRawExtractionMode,
                };

                _extractor = CreateExtractorForCurrentRun();
                StatusText = $"开始提取：{Path.GetFileName(inputPath)}";
                await Task.Run(() => RunExtraction(_extractor, parameters, extractionMode));

                if (_extractor.ThreadedException is not null)
                {
                    StatusText = $"提取失败：{_extractor.ThreadedException.Message}";
                    return;
                }
                if (_extractor.Abort)
                {
                    StatusText = "已中止";
                    return;
                }

                completedFiles++;
                completedTracks += selectedFile.Tracks.Count;
            }

            StatusText = $"提取完成：{completedFiles} 个文件，{completedTracks} 个轨道 ✓";
            if (ShowPopup)
            {
                ExtractionCompleted?.Invoke(completedFiles, completedTracks);
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

    private gMKVExtract CreateExtractorForCurrentRun()
    {
        var extractor = new gMKVExtract(MkvToolnixPath);
        extractor.MkvExtractProgressUpdated += progress =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                ProgressValue = progress;
                ProgressText = $"{progress}%";
            });
        };
        extractor.MkvExtractTrackUpdated += (filename, trackName) =>
        {
            Dispatcher.UIThread.Post(() => StatusText = $"正在提取：{trackName}");
        };
        return extractor;
    }

    private static void RunExtraction(gMKVExtract extractor, gMKVExtractSegmentsParameters parameters, ExtractionMode extractionMode)
    {
        switch (extractionMode)
        {
            case ExtractionMode.Tracks:
                extractor.ExtractMKVSegmentsThreaded(parameters);
                break;
            case ExtractionMode.Timecodes:
                extractor.ExtractMKVTimecodesThreaded(parameters);
                break;
            case ExtractionMode.Cues:
                extractor.ExtractMKVCuesThreaded(parameters);
                break;
            case ExtractionMode.Cue_Sheet:
                extractor.ExtractMkvCuesheetThreaded(parameters);
                break;
            case ExtractionMode.Tags:
                extractor.ExtractMkvTagsThreaded(parameters);
                break;
            case ExtractionMode.Tracks_And_Timecodes:
                extractor.ExtractMKVSegmentsThreaded(parameters);
                if (!extractor.Abort) extractor.ExtractMKVTimecodesThreaded(parameters);
                break;
            case ExtractionMode.Tracks_And_Cues:
                extractor.ExtractMKVSegmentsThreaded(parameters);
                if (!extractor.Abort) extractor.ExtractMKVCuesThreaded(parameters);
                break;
            case ExtractionMode.Tracks_And_Cues_And_Timecodes:
                extractor.ExtractMKVSegmentsThreaded(parameters);
                if (!extractor.Abort) extractor.ExtractMKVCuesThreaded(parameters);
                if (!extractor.Abort) extractor.ExtractMKVTimecodesThreaded(parameters);
                break;
        }
    }

    private async Task AddCurrentSelectionToQueueAsync()
    {
        if (InputFiles.Count == 0)
        {
            StatusText = "请先添加输入文件";
            return;
        }
        if (string.IsNullOrEmpty(MkvToolnixPath))
        {
            StatusText = "未配置 MKVToolnix 路径";
            return;
        }

        await LoadTracksForFilesAsync(InputFiles);

        var selectedFiles = InputFiles
            .Select(file => new
            {
                File = file,
                Tracks = file.Tracks.Where(t => t.IsSelected).ToList(),
            })
            .Where(item => item.Tracks.Count > 0)
            .ToList();

        if (selectedFiles.Count == 0)
        {
            StatusText = "请勾选至少一个轨道";
            return;
        }

        SyncToSettings();

        var chapterType = Enum.TryParse<MkvChapterTypes>(SelectedChapterType, out var ct) ? ct : MkvChapterTypes.XML;
        var extractionMode = Enum.TryParse<ExtractionMode>(SelectedExtractionMode, out var em) ? em : ExtractionMode.Tracks;
        var jobsAdded = 0;
        var trackCount = 0;

        foreach (var selectedFile in selectedFiles)
        {
            var inputPath = selectedFile.File.FullPath;
            var outputDir = ResolveOutputDirectory(inputPath);

            if (string.IsNullOrEmpty(outputDir))
            {
                StatusText = "未设置输出目录";
                return;
            }

            var segments = selectedFile.Tracks
                .Select(t => t.Segment)
                .Where(s => s is not null)
                .Cast<gMKVToolNix.Segments.gMKVSegment>()
                .ToList();

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
                    VideoTrackFilenamePattern    = VideoPattern,
                    AudioTrackFilenamePattern    = AudioPattern,
                    SubtitleTrackFilenamePattern = SubtitlePattern,
                    ChapterFilenamePattern       = ChapterPattern,
                    AttachmentFilenamePattern    = AttachmentPattern,
                    TagsFilenamePattern          = TagsPattern,
                },
                ExistingFileHandling     = ResolveExistingFileHandling(),
                DisableBomForTextFiles   = DisableBomForTextFiles,
                UseRawExtractionMode     = UseRawExtractionMode,
                UseFullRawExtractionMode = UseFullRawExtractionMode,
            };

            var jobItem = new JobItem(
                fileName:             System.IO.Path.GetFileName(inputPath),
                trackCount:           selectedFile.Tracks.Count,
                extractionParameters: parameters,
                extractionMode:       extractionMode,
                mkvToolnixPath:       MkvToolnixPath);

            JobsWindowViewModel.Instance.AddJob(jobItem);
            jobsAdded++;
            trackCount += selectedFile.Tracks.Count;
        }

        StatusText = $"已加入队列：{jobsAdded} 个文件，{trackCount} 个轨道";

        // 自动打开作业队列窗口
        ShowJobs();
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
        if (_sharedJobsWindow is null || !_sharedJobsWindow.IsVisible)
        {
            _sharedJobsWindow ??= new JobsWindow();
            _sharedJobsWindow.Closed += (_, _) => _sharedJobsWindow = null;
            _sharedJobsWindow.Show();
        }
        else
        {
            _sharedJobsWindow.Activate();
        }
    }

    private void ShowOptions()
    {
        var owner = GetTopLevel() as Window;
        if (owner is null) return;

        // 在打开设置窗口前，同步当前状态到 SettingsService
        SyncToSettings();

        var vm = new OptionsWindowViewModel();
        var win = new OptionsWindow { DataContext = vm };

        win.Closed += (_, _) =>
        {
            // 设置窗口关闭后，从 SettingsService 同步回来
            SyncFromSettings();
        };

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

    // ── 输出路径决策逻辑 ────────────────────────────────────────────────────

    /// <summary>
    /// 根据当前设置策略，解析输入文件对应的输出目录。
    /// </summary>
    private string ResolveOutputDirectory(string inputFilePath)
    {
        return _outputDirectoryStrategy switch
        {
            OutputDirectoryStrategy.SourceDirectory =>
                Path.GetDirectoryName(inputFilePath) ?? string.Empty,
            OutputDirectoryStrategy.CustomDirectory =>
                OutputDirectory,
            OutputDirectoryStrategy.SubdirectoryPerFile =>
                Path.Combine(
                    string.IsNullOrEmpty(OutputDirectory)
                        ? Path.GetDirectoryName(inputFilePath) ?? string.Empty
                        : OutputDirectory,
                    Path.GetFileNameWithoutExtension(inputFilePath)),
            _ => Path.GetDirectoryName(inputFilePath) ?? string.Empty,
        };
    }

    /// <summary>
    /// 根据覆盖策略解析输出文件路径。返回 null 表示跳过。
    /// </summary>
    public static string? ResolveOutputFilePath(string outputPath, OverwriteStrategy strategy)
    {
        if (!File.Exists(outputPath) || strategy == OverwriteStrategy.Overwrite)
            return outputPath;

        if (strategy == OverwriteStrategy.Skip)
            return null; // null 表示跳过

        // Rename: 添加后缀
        string dir = Path.GetDirectoryName(outputPath)!;
        string nameNoExt = Path.GetFileNameWithoutExtension(outputPath);
        string ext = Path.GetExtension(outputPath);
        int counter = 1;
        string newPath;
        do
        {
            newPath = Path.Combine(dir, $"{nameNoExt}_{counter}{ext}");
            counter++;
        } while (File.Exists(newPath));
        return newPath;
    }

    private gMKVToolNix.ExistingFileHandling ResolveExistingFileHandling()
    {
        return SettingsService.Instance.Current.OverwriteStrategy switch
        {
            OverwriteStrategy.Overwrite => gMKVToolNix.ExistingFileHandling.Overwrite,
            OverwriteStrategy.Skip => gMKVToolNix.ExistingFileHandling.Skip,
            OverwriteStrategy.Rename => gMKVToolNix.ExistingFileHandling.Rename,
            _ => OverwriteExistingFiles
                ? gMKVToolNix.ExistingFileHandling.Overwrite
                : gMKVToolNix.ExistingFileHandling.Rename,
        };
    }

    /// <summary>
    /// 从 SettingsService 同步设置到 ViewModel 属性。
    /// </summary>
    public void SyncFromSettings()
    {
        var s = SettingsService.Instance.Current;
        MkvToolnixPath = s.LastMkvToolnixPath;
        _outputDirectoryStrategy = s.OutputStrategy;
        OutputDirectory = !string.IsNullOrEmpty(s.CustomOutputDirectory)
            ? s.CustomOutputDirectory
            : s.LastOutputDirectory;
        UseSourceDirectory = s.OutputStrategy == OutputDirectoryStrategy.SourceDirectory;
        VideoPattern = s.VideoTrackPattern;
        AudioPattern = s.AudioTrackPattern;
        SubtitlePattern = s.SubtitleTrackPattern;
        ChapterPattern = s.ChapterPattern;
        AttachmentPattern = s.AttachmentPattern;
        TagsPattern = s.TagsPattern;
        OverwriteExistingFiles = s.OverwriteStrategy == OverwriteStrategy.Overwrite;
    }

    /// <summary>
    /// 将 ViewModel 属性同步回 SettingsService（不保存到磁盘）。
    /// </summary>
    public void SyncToSettings()
    {
        var s = SettingsService.Instance.Current;
        s.LastMkvToolnixPath = MkvToolnixPath;
        s.OutputStrategy = _outputDirectoryStrategy;
        s.UseSourceDirectory = _outputDirectoryStrategy == OutputDirectoryStrategy.SourceDirectory;
        s.LastOutputDirectory = OutputDirectory;
        s.CustomOutputDirectory = OutputDirectory;
        s.VideoTrackPattern = VideoPattern;
        s.AudioTrackPattern = AudioPattern;
        s.SubtitleTrackPattern = SubtitlePattern;
        s.ChapterPattern = ChapterPattern;
        s.AttachmentPattern = AttachmentPattern;
        s.TagsPattern = TagsPattern;
        if (OverwriteExistingFiles)
        {
            s.OverwriteStrategy = OverwriteStrategy.Overwrite;
        }
        else if (s.OverwriteStrategy == OverwriteStrategy.Overwrite)
        {
            s.OverwriteStrategy = OverwriteStrategy.Rename;
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

public class InputFileItem : INotifyPropertyChanged
{
    public string FullPath { get; }
    public string DisplayName { get; }
    public ObservableCollection<TrackItem> Tracks { get; } = new();

    private bool _tracksLoaded;
    public bool TracksLoaded
    {
        get => _tracksLoaded;
        set
        {
            if (_tracksLoaded == value) return;
            _tracksLoaded = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TracksLoaded)));
        }
    }

    private bool _isLoadingTracks;
    public bool IsLoadingTracks
    {
        get => _isLoadingTracks;
        set
        {
            if (_isLoadingTracks == value) return;
            _isLoadingTracks = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLoadingTracks)));
        }
    }

    private string _tracksSummary = string.Empty;
    public string TracksSummary
    {
        get => _tracksSummary;
        set
        {
            if (_tracksSummary == value) return;
            _tracksSummary = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TracksSummary)));
        }
    }

    public InputFileItem(string fullPath)
    {
        FullPath = fullPath;
        DisplayName = Path.GetFileName(fullPath);
    }
    public override string ToString() => DisplayName;

    public event PropertyChangedEventHandler? PropertyChanged;
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
            SelectionChanged?.Invoke();
        }
    }
    public string Display { get; set; } = string.Empty;
    public string TypeLabel { get; set; } = string.Empty;
    public IBrush TypeColor { get; set; } = new SolidColorBrush(Colors.LightGray);
    public int TrackNumber { get; set; }
    public int TrackId { get; set; }
    public gMKVToolNix.Segments.gMKVSegment? Segment { get; set; }
    public Action? SelectionChanged { get; set; }

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
