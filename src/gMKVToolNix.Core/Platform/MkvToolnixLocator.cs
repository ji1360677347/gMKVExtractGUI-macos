using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace gMKVToolNix.Platform;

/// <summary>
/// 跨平台 mkvtoolnix 路径探测。Windows 仍走原 Registry（gMKVHelper.GetMKVToolnixPathViaRegistry），
/// macOS / Linux 走本类的 PATH + 常见安装路径搜索。
/// </summary>
public static class MkvToolnixLocator
{
    private static readonly string ExeName =
        OperatingSystem.IsWindows() ? "mkvmerge.exe" : "mkvmerge";

    /// <summary>
    /// 返回包含 mkvmerge 的目录，找不到返回 null。
    /// </summary>
    public static string? Locate()
    {
        // 1. 走 PATH
        string? viaPath = ResolveViaPath();
        if (!string.IsNullOrEmpty(viaPath))
        {
            return viaPath;
        }

        // 2. 平台默认路径
        foreach (string candidate in GetCandidateDirectories())
        {
            if (DirectoryHasMkvMerge(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? ResolveViaPath()
    {
        // `which mkvmerge` / `where mkvmerge.exe`
        string finder = OperatingSystem.IsWindows() ? "where" : "which";
        try
        {
            var psi = new ProcessStartInfo(finder, ExeName)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (p == null) return null;
            string output = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit(2000);
            if (p.ExitCode != 0 || string.IsNullOrEmpty(output)) return null;

            // where 在 Windows 可能输出多行；取第一行
            string firstLine = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
            if (File.Exists(firstLine))
            {
                return Path.GetDirectoryName(firstLine);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MkvToolnixLocator] which/where failed: {ex.Message}");
        }
        return null;
    }

    private static IEnumerable<string> GetCandidateDirectories()
    {
        if (OperatingSystem.IsMacOS())
        {
            // Homebrew arm64 / Intel
            yield return "/opt/homebrew/bin";
            yield return "/usr/local/bin";

            // 官方 .app
            foreach (var dir in new[]
            {
                "/Applications/MKVToolNix.app/Contents/MacOS",
                "/Applications/MKVToolNix-64bit.app/Contents/MacOS",
            })
            {
                yield return dir;
            }

            // 用户 Applications
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            yield return Path.Combine(home, "Applications/MKVToolNix.app/Contents/MacOS");
        }
        else if (OperatingSystem.IsLinux())
        {
            yield return "/usr/bin";
            yield return "/usr/local/bin";
            yield return "/snap/bin";
        }
        else if (OperatingSystem.IsWindows())
        {
            // 让调用方走 gMKVHelper.GetMKVToolnixPathViaRegistry()，但作为兜底也列几条
            yield return @"C:\Program Files\MKVToolNix";
            yield return @"C:\Program Files (x86)\MKVToolNix";
        }
    }

    private static bool DirectoryHasMkvMerge(string dir)
    {
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return false;
        return File.Exists(Path.Combine(dir, ExeName));
    }
}
