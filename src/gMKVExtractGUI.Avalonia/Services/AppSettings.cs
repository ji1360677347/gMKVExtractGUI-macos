using System.Text.Json.Serialization;

namespace gMKVToolNix.UI.Services;

/// <summary>
/// 输出目录策略
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OutputDirectoryStrategy
{
    /// <summary>输出到源文件所在目录</summary>
    SourceDirectory,
    /// <summary>输出到统一的自定义目录</summary>
    CustomDirectory,
    /// <summary>每个 MKV 文件在输出目录下创建子目录: {OutputDir}/{FilenameNoExt}/</summary>
    SubdirectoryPerFile
}

/// <summary>
/// 文件覆盖策略
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OverwriteStrategy
{
    /// <summary>覆盖已有文件</summary>
    Overwrite,
    /// <summary>跳过已存在的文件</summary>
    Skip,
    /// <summary>自动添加 _1, _2 后缀避免冲突</summary>
    Rename
}

/// <summary>
/// 应用程序持久化设置模型
/// </summary>
public class AppSettings
{
    // ── 输出目录策略 ─────────────────────────────────────────────────────────

    /// <summary>决定提取文件输出到哪里</summary>
    public OutputDirectoryStrategy OutputStrategy { get; set; } = OutputDirectoryStrategy.SourceDirectory;

    /// <summary>当 OutputStrategy == CustomDirectory 时使用此路径</summary>
    public string CustomOutputDirectory { get; set; } = "";

    // ── 文件名模板 ────────────────────────────────────────────────────────────

    /// <summary>视频轨道输出文件名模板</summary>
    public string VideoTrackPattern { get; set; } = "{FilenameNoExt}_track{TrackNumber}_[{Language}]";

    /// <summary>音频轨道输出文件名模板（含延迟信息）</summary>
    public string AudioTrackPattern { get; set; } = "{FilenameNoExt}_track{TrackNumber}_[{Language}]_DELAY {EffectiveDelay}ms";

    /// <summary>字幕轨道输出文件名模板</summary>
    public string SubtitleTrackPattern { get; set; } = "{FilenameNoExt}_track{TrackNumber}_[{Language}]";

    /// <summary>章节文件输出文件名模板</summary>
    public string ChapterPattern { get; set; } = "{FilenameNoExt}_chapters";

    /// <summary>附件输出文件名模板</summary>
    public string AttachmentPattern { get; set; } = "{AttachmentFilename}";

    /// <summary>标签文件输出文件名模板</summary>
    public string TagsPattern { get; set; } = "{FilenameNoExt}_tags";

    // ── 覆盖策略 ──────────────────────────────────────────────────────────────

    /// <summary>输出文件已存在时的处理方式</summary>
    public OverwriteStrategy OverwriteStrategy { get; set; } = OverwriteStrategy.Overwrite;

    // ── 提取选项 ──────────────────────────────────────────────────────────────

    /// <summary>上次使用的 MKVToolNix 工具目录</summary>
    public string LastMkvToolnixPath { get; set; } = "";

    /// <summary>上次使用的输出目录</summary>
    public string LastOutputDirectory { get; set; } = "";

    /// <summary>是否使用源文件所在目录作为输出目录</summary>
    public bool UseSourceDirectory { get; set; } = true;

    // ── 窗口状态 ──────────────────────────────────────────────────────────────

    /// <summary>主窗口 X 坐标；-1 表示由系统决定</summary>
    public double WindowX { get; set; } = -1;

    /// <summary>主窗口 Y 坐标；-1 表示由系统决定</summary>
    public double WindowY { get; set; } = -1;

    /// <summary>主窗口宽度</summary>
    public double WindowWidth { get; set; } = 900;

    /// <summary>主窗口高度</summary>
    public double WindowHeight { get; set; } = 650;

    // ── 批量模式 ──────────────────────────────────────────────────────────────

    /// <summary>是否默认启用批量模式</summary>
    public bool AutoBatchMode { get; set; } = true;
}
