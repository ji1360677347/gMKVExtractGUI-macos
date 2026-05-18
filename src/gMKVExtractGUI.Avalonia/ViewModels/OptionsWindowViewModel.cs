using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using gMKVToolNix.MkvExtract;
using gMKVToolNix.UI.Services;

namespace gMKVToolNix.UI.ViewModels;

public class OptionsWindowViewModel : INotifyPropertyChanged
{
    // ── 输出目录策略 ─────────────────────────────────────────────────────────

    private OutputDirectoryStrategy _outputStrategy = OutputDirectoryStrategy.SourceDirectory;
    public OutputDirectoryStrategy OutputStrategy
    {
        get => _outputStrategy;
        set { if (SetField(ref _outputStrategy, value)) { OnPropertyChanged(nameof(IsSourceDirectory)); OnPropertyChanged(nameof(IsCustomDirectory)); OnPropertyChanged(nameof(IsSubdirectoryPerFile)); OnPropertyChanged(nameof(ShowCustomDirectoryPicker)); UpdatePreview(); } }
    }

    public bool IsSourceDirectory
    {
        get => OutputStrategy == OutputDirectoryStrategy.SourceDirectory;
        set { if (value) OutputStrategy = OutputDirectoryStrategy.SourceDirectory; }
    }

    public bool IsCustomDirectory
    {
        get => OutputStrategy == OutputDirectoryStrategy.CustomDirectory;
        set { if (value) OutputStrategy = OutputDirectoryStrategy.CustomDirectory; }
    }

    public bool IsSubdirectoryPerFile
    {
        get => OutputStrategy == OutputDirectoryStrategy.SubdirectoryPerFile;
        set { if (value) OutputStrategy = OutputDirectoryStrategy.SubdirectoryPerFile; }
    }

    public bool ShowCustomDirectoryPicker => OutputStrategy is OutputDirectoryStrategy.CustomDirectory or OutputDirectoryStrategy.SubdirectoryPerFile;

    private string _customOutputDirectory = "";
    public string CustomOutputDirectory
    {
        get => _customOutputDirectory;
        set { if (SetField(ref _customOutputDirectory, value)) UpdatePreview(); }
    }

    // ── 文件名模板 ────────────────────────────────────────────────────────────

    private string _videoTrackPattern = "{FilenameNoExt}_track{TrackNumber}_[{Language}]";
    public string VideoTrackPattern
    {
        get => _videoTrackPattern;
        set { if (SetField(ref _videoTrackPattern, value)) UpdatePreview(); }
    }

    private string _audioTrackPattern = "{FilenameNoExt}_track{TrackNumber}_[{Language}]_DELAY {EffectiveDelay}ms";
    public string AudioTrackPattern
    {
        get => _audioTrackPattern;
        set { if (SetField(ref _audioTrackPattern, value)) UpdatePreview(); }
    }

    private string _subtitleTrackPattern = "{FilenameNoExt}_track{TrackNumber}_[{Language}]";
    public string SubtitleTrackPattern
    {
        get => _subtitleTrackPattern;
        set { if (SetField(ref _subtitleTrackPattern, value)) UpdatePreview(); }
    }

    private string _chapterPattern = "{FilenameNoExt}_chapters";
    public string ChapterPattern
    {
        get => _chapterPattern;
        set { if (SetField(ref _chapterPattern, value)) UpdatePreview(); }
    }

    private string _attachmentPattern = "{AttachmentFilename}";
    public string AttachmentPattern
    {
        get => _attachmentPattern;
        set { if (SetField(ref _attachmentPattern, value)) UpdatePreview(); }
    }

    private string _tagsPattern = "{FilenameNoExt}_tags";
    public string TagsPattern
    {
        get => _tagsPattern;
        set { if (SetField(ref _tagsPattern, value)) UpdatePreview(); }
    }

    // ── 覆盖策略 ──────────────────────────────────────────────────────────────

    private OverwriteStrategy _overwriteStrategy = OverwriteStrategy.Overwrite;
    public OverwriteStrategy OverwriteStrategy
    {
        get => _overwriteStrategy;
        set { if (SetField(ref _overwriteStrategy, value)) { OnPropertyChanged(nameof(IsOverwriteOverwrite)); OnPropertyChanged(nameof(IsOverwriteSkip)); OnPropertyChanged(nameof(IsOverwriteRename)); } }
    }

    public bool IsOverwriteOverwrite
    {
        get => OverwriteStrategy == OverwriteStrategy.Overwrite;
        set { if (value) OverwriteStrategy = OverwriteStrategy.Overwrite; }
    }

    public bool IsOverwriteSkip
    {
        get => OverwriteStrategy == OverwriteStrategy.Skip;
        set { if (value) OverwriteStrategy = OverwriteStrategy.Skip; }
    }

    public bool IsOverwriteRename
    {
        get => OverwriteStrategy == OverwriteStrategy.Rename;
        set { if (value) OverwriteStrategy = OverwriteStrategy.Rename; }
    }

    // ── 预览 ──────────────────────────────────────────────────────────────────

    private string _patternPreview = "";
    public string PatternPreview
    {
        get => _patternPreview;
        set => SetField(ref _patternPreview, value);
    }

    private int _selectedPatternTab;
    public int SelectedPatternTab
    {
        get => _selectedPatternTab;
        set { if (SetField(ref _selectedPatternTab, value)) UpdatePreview(); }
    }

    // ── 占位符列表 ────────────────────────────────────────────────────────────

    public List<PlaceholderGroup> PlaceholderGroups { get; } = new()
    {
        new PlaceholderGroup("通用", new List<PlaceholderInfo>
        {
            new("{FilenameNoExt}", "不含扩展名的源文件名"),
            new("{TrackNumber}", "轨道编号"),
            new("{TrackNumber:00}", "轨道编号（两位补零）"),
            new("{Language}", "轨道语言代码"),
            new("{TrackName}", "轨道名称"),
            new("{CodecID}", "编解码器 ID"),
        }),
        new PlaceholderGroup("视频专用", new List<PlaceholderInfo>
        {
            new("{PixelWidth}", "视频像素宽"),
            new("{PixelHeight}", "视频像素高"),
        }),
        new PlaceholderGroup("音频专用", new List<PlaceholderInfo>
        {
            new("{EffectiveDelay}", "有效延迟（ms）"),
            new("{Channels}", "声道数"),
        }),
        new PlaceholderGroup("附件专用", new List<PlaceholderInfo>
        {
            new("{AttachmentFilename}", "附件原始文件名"),
        }),
    };

    // ── 命令 ──────────────────────────────────────────────────────────────────

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ResetDefaultsCommand { get; }
    public ICommand BrowseDirectoryCommand { get; }
    public ICommand InsertPlaceholderCommand { get; }
    public ICommand ApplyPresetCommand { get; }

    // ── 事件 ──────────────────────────────────────────────────────────────────

    /// <summary>请求关闭窗口（参数: true=保存并关闭, false=直接关闭）</summary>
    public event Action<bool>? CloseRequested;

    /// <summary>当用户插入占位符时触发，参数为占位符字符串</summary>
    public event Action<string>? PlaceholderInsertRequested;

    // ── 构造 ──────────────────────────────────────────────────────────────────

    public OptionsWindowViewModel()
    {
        SaveCommand = new RelayCommand(() => Save());
        CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(false));
        ResetDefaultsCommand = new RelayCommand(() => ResetDefaults());
        BrowseDirectoryCommand = new RelayCommand(async () => await BrowseDirectoryAsync());
        InsertPlaceholderCommand = new RelayCommandWithParam(p => InsertPlaceholder(p?.ToString() ?? ""));
        ApplyPresetCommand = new RelayCommandWithParam(p => ApplyPreset(p?.ToString() ?? ""));

        LoadFromSettings();
        UpdatePreview();
    }

    // ── 方法 ──────────────────────────────────────────────────────────────────

    private void LoadFromSettings()
    {
        var s = SettingsService.Instance.Current;
        OutputStrategy = s.OutputStrategy;
        CustomOutputDirectory = s.CustomOutputDirectory;
        VideoTrackPattern = s.VideoTrackPattern;
        AudioTrackPattern = s.AudioTrackPattern;
        SubtitleTrackPattern = s.SubtitleTrackPattern;
        ChapterPattern = s.ChapterPattern;
        AttachmentPattern = s.AttachmentPattern;
        TagsPattern = s.TagsPattern;
        OverwriteStrategy = s.OverwriteStrategy;
    }

    private void Save()
    {
        var s = SettingsService.Instance.Current;
        s.OutputStrategy = OutputStrategy;
        s.CustomOutputDirectory = CustomOutputDirectory;
        s.VideoTrackPattern = VideoTrackPattern;
        s.AudioTrackPattern = AudioTrackPattern;
        s.SubtitleTrackPattern = SubtitleTrackPattern;
        s.ChapterPattern = ChapterPattern;
        s.AttachmentPattern = AttachmentPattern;
        s.TagsPattern = TagsPattern;
        s.OverwriteStrategy = OverwriteStrategy;
        SettingsService.Instance.Save();
        CloseRequested?.Invoke(true);
    }

    private void ResetDefaults()
    {
        var defaults = new AppSettings();
        OutputStrategy = defaults.OutputStrategy;
        CustomOutputDirectory = defaults.CustomOutputDirectory;
        VideoTrackPattern = defaults.VideoTrackPattern;
        AudioTrackPattern = defaults.AudioTrackPattern;
        SubtitleTrackPattern = defaults.SubtitleTrackPattern;
        ChapterPattern = defaults.ChapterPattern;
        AttachmentPattern = defaults.AttachmentPattern;
        TagsPattern = defaults.TagsPattern;
        OverwriteStrategy = defaults.OverwriteStrategy;
    }

    private async System.Threading.Tasks.Task BrowseDirectoryAsync()
    {
        var top = GetTopLevel();
        if (top is null) return;
        var folders = await top.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
        {
            Title = "选择输出目录",
            AllowMultiple = false
        });
        var dir = folders.FirstOrDefault()?.Path.LocalPath;
        if (!string.IsNullOrEmpty(dir))
            CustomOutputDirectory = dir!;
    }

    private void InsertPlaceholder(string placeholder)
    {
        PlaceholderInsertRequested?.Invoke(placeholder);
    }

    private void ApplyPreset(string presetName)
    {
        switch (presetName)
        {
            case "compact":
                VideoTrackPattern = "{FilenameNoExt}_{TrackNumber}";
                AudioTrackPattern = "{FilenameNoExt}_{TrackNumber}_{Language}";
                SubtitleTrackPattern = "{FilenameNoExt}_{TrackNumber}_{Language}";
                ChapterPattern = "{FilenameNoExt}_chapters";
                AttachmentPattern = "{AttachmentFilename}";
                TagsPattern = "{FilenameNoExt}_tags";
                break;
            case "detailed":
                VideoTrackPattern = "{FilenameNoExt}_track{TrackNumber}_[{Language}]_{CodecID}_{PixelWidth}x{PixelHeight}";
                AudioTrackPattern = "{FilenameNoExt}_track{TrackNumber}_[{Language}]_DELAY{EffectiveDelay}ms_{Channels}ch";
                SubtitleTrackPattern = "{FilenameNoExt}_track{TrackNumber}_[{Language}]";
                ChapterPattern = "{FilenameNoExt}_chapters";
                AttachmentPattern = "{AttachmentFilename}";
                TagsPattern = "{FilenameNoExt}_tags";
                break;
            case "compatible":
                VideoTrackPattern = "{FilenameNoExt}.video{TrackNumber:00}";
                AudioTrackPattern = "{FilenameNoExt}.audio{TrackNumber:00}_{Language}";
                SubtitleTrackPattern = "{FilenameNoExt}.sub{TrackNumber:00}_{Language}";
                ChapterPattern = "{FilenameNoExt}.chapters";
                AttachmentPattern = "{AttachmentFilename}";
                TagsPattern = "{FilenameNoExt}.tags";
                break;
        }
    }

    private void UpdatePreview()
    {
        // 示例数据
        const string sampleFilename = "MyMovie";
        const int sampleTrackNum = 3;
        const string sampleLang = "jpn";
        const string sampleCodec = "V_MPEG4/ISO/AVC";
        const int sampleDelay = -23;
        const int sampleChannels = 6;
        const int sampleWidth = 1920;
        const int sampleHeight = 1080;
        const string sampleAttachment = "cover.jpg";

        string template = SelectedPatternTab switch
        {
            0 => VideoTrackPattern,
            1 => AudioTrackPattern,
            2 => SubtitleTrackPattern,
            3 => ChapterPattern,
            4 => AttachmentPattern,
            5 => TagsPattern,
            _ => VideoTrackPattern,
        };

        string preview = template
            .Replace("{FilenameNoExt}", sampleFilename)
            .Replace("{TrackNumber}", sampleTrackNum.ToString())
            .Replace("{TrackNumber:00}", sampleTrackNum.ToString("00"))
            .Replace("{TrackNumber:0}", sampleTrackNum.ToString("0"))
            .Replace("{Language}", sampleLang)
            .Replace("{TrackName}", "Main")
            .Replace("{CodecID}", sampleCodec)
            .Replace("{EffectiveDelay}", sampleDelay.ToString())
            .Replace("{Delay}", sampleDelay.ToString())
            .Replace("{Channels}", sampleChannels.ToString())
            .Replace("{PixelWidth}", sampleWidth.ToString())
            .Replace("{PixelHeight}", sampleHeight.ToString())
            .Replace("{AttachmentFilename}", sampleAttachment);

        PatternPreview = preview;
    }

    private static Avalonia.Controls.TopLevel? GetTopLevel()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;
        return null;
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────────────

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

// ── 占位符数据模型 ────────────────────────────────────────────────────────────

public class PlaceholderGroup
{
    public string GroupName { get; }
    public List<PlaceholderInfo> Placeholders { get; }

    public PlaceholderGroup(string groupName, List<PlaceholderInfo> placeholders)
    {
        GroupName = groupName;
        Placeholders = placeholders;
    }
}

public class PlaceholderInfo
{
    public string Token { get; }
    public string Description { get; }

    public PlaceholderInfo(string token, string description)
    {
        Token = token;
        Description = description;
    }
}

// ── 带参数的 RelayCommand ─────────────────────────────────────────────────────

internal sealed class RelayCommandWithParam : ICommand
{
    private readonly Action<object?> _execute;
    public RelayCommandWithParam(Action<object?> execute) { _execute = execute; }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute(parameter);
    public event EventHandler? CanExecuteChanged { add { } remove { } }
}
