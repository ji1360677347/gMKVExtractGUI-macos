namespace gMKVToolNix.MkvExtract;

/// <summary>
/// 提取模式（与原 WinForms 项目的 FormMkvExtractionMode 等价；Round 11 后该项目会被删除，此 enum 为唯一来源）。
/// </summary>
public enum ExtractionMode
{
    Tracks,
    Cue_Sheet,
    Tags,
    Timecodes,
    Tracks_And_Timecodes,
    Cues,
    Tracks_And_Cues,
    Tracks_And_Cues_And_Timecodes,
}
