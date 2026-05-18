using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace gMKVToolNix.UI.Services;

/// <summary>
/// 应用程序设置持久化服务（单例）。
/// 负责将 <see cref="AppSettings"/> 以 JSON 格式读写到磁盘，
/// 并在设置变更时发出通知事件。
/// </summary>
public sealed class SettingsService
{
    // ── 单例 ─────────────────────────────────────────────────────────────────

    private static readonly Lazy<SettingsService> _lazy =
        new(() => new SettingsService(), isThreadSafe: true);

    /// <summary>全局单例实例</summary>
    public static SettingsService Instance => _lazy.Value;

    // ── JSON 序列化选项 ───────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // 反序列化时忽略未知属性（向前兼容）
        UnknownTypeHandling = JsonUnknownTypeHandling.JsonNode,
        // 容错：允许注释、尾随逗号
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        // 枚举序列化为字符串
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    // ── 状态 ──────────────────────────────────────────────────────────────────

    private AppSettings _current = new();
    private readonly object _saveLock = new();

    // ── 公开属性 ──────────────────────────────────────────────────────────────

    /// <summary>当前生效的设置实例</summary>
    public AppSettings Current => _current;

    /// <summary>
    /// 设置文件完整路径。
    /// macOS/Linux: <c>~/.config/gMKVExtractGUI/settings.json</c><br/>
    /// Windows:     <c>%AppData%\gMKVExtractGUI\settings.json</c>
    /// </summary>
    public string SettingsFilePath { get; } =
        Path.Combine(GetSettingsDirectory(), "settings.json");

    // ── 事件 ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// 当通过 <see cref="NotifyChanged"/> 触发变更通知时触发。
    /// 参数为变更的属性名（可为空字符串表示批量变更）。
    /// </summary>
    public event Action<string>? SettingChanged;

    // ── 构造函数（私有，防止外部实例化）─────────────────────────────────────

    private SettingsService() { }

    // ── 核心方法 ──────────────────────────────────────────────────────────────

    /// <summary>
    /// 从磁盘加载设置。
    /// 文件不存在或解析失败时均使用默认值，不会抛出异常。
    /// </summary>
    public void Load()
    {
        if (!File.Exists(SettingsFilePath))
        {
            _current = new AppSettings();
            return;
        }

        try
        {
            string json = File.ReadAllText(SettingsFilePath);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);
            _current = loaded ?? new AppSettings();
        }
        catch (Exception ex)
        {
            // 解析失败：记录警告，使用默认值，不崩溃
            Console.Error.WriteLine(
                $"[SettingsService] 加载设置失败，将使用默认值。原因: {ex.Message}");
            _current = new AppSettings();
        }
    }

    /// <summary>
    /// 将当前设置原子写入磁盘（先写临时文件，再重命名替换，防止断电损坏）。
    /// </summary>
    public void Save()
    {
        lock (_saveLock)
        {
            try
            {
                // 确保目录存在
                string directory = Path.GetDirectoryName(SettingsFilePath)!;
                Directory.CreateDirectory(directory);

                // 序列化到字符串
                string json = JsonSerializer.Serialize(_current, _jsonOptions);

                // 写入临时文件（与目标文件同目录，保证跨磁盘移动为原子重命名）
                string tempPath = SettingsFilePath + ".tmp";
                File.WriteAllText(tempPath, json, System.Text.Encoding.UTF8);

                // 原子替换：在同一文件系统分区上 Move 是原子操作
                File.Move(tempPath, SettingsFilePath, overwrite: true);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"[SettingsService] 保存设置失败。原因: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 通知 ViewModel 层指定属性已变更。
    /// </summary>
    /// <param name="propertyName">变更的属性名；传空字符串表示批量变更。</param>
    public void NotifyChanged(string propertyName = "")
    {
        SettingChanged?.Invoke(propertyName);
    }

    // ── 辅助方法 ──────────────────────────────────────────────────────────────

    /// <summary>
    /// 返回设置文件所在目录（跨平台）。
    /// </summary>
    private static string GetSettingsDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "gMKVExtractGUI");
        }
        else
        {
            // macOS / Linux: ~/.config/gMKVExtractGUI
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config",
                "gMKVExtractGUI");
        }
    }
}
