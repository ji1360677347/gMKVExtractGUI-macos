# gMKVExtractGUI macOS 端口·当前进度（Round 0 完成）

> 调度中枢首轮启动前先读这份。Round 0 由会话内 Opus 直接完成，没走 briefing/handoff。

---

## 项目背景

- 原项目：`.NET 4.0 WinForms`，仅 Windows 可运行
- 目标：`.NET 9 + Avalonia 11`，macOS 原生（同时 Windows/Linux 可继续编译）
- 业务库（gMKVToolNix.dll）几乎完全跨平台，唯一硬依赖是 `gMKVHelper.GetMKVToolnixPathViaRegistry()`，已经被 `PlatformExtensions.IsOnLinux` 调用方拦住

## Round 0 已完成（不要重做）

### 项目骨架
```
/Applications/gMKVExtractGUI/
  gMKVExtractGUI.macOS.sln                       ← 新 sln（macOS 用）
  gMKVExtractGUI.sln                             ← 旧 sln（Windows 用，硬禁区）
  src/
    gMKVToolNix/                                 ← 旧业务库（共享源）
    gMKVToolNix.Core/                            ← 新 .NET 9 SDK 项目
      gMKVToolNix.Core.csproj                    ← <Compile Include="..\gMKVToolNix\**\*.cs" /> 共享源
    gMKVExtractGUI.Avalonia/                     ← 新 UI 项目
      gMKVExtractGUI.Avalonia.csproj             ← Avalonia 11.2.3 + ProjectReference Core
      app.manifest
      Program.cs
      App.axaml + App.axaml.cs
      Themes/Macaron.axaml                       ← 马卡龙资源字典 + 控件样式
      Views/MainWindow.axaml + .cs               ← 主窗口骨架（毛玻璃 + 拖入）
      ViewModels/MainWindowViewModel.cs          ← 简易 ViewModel + RelayCommand
    gMKVExtractGUI/                              ← 旧 WinForms 项目（硬禁区）
  .orchestration/
    PLAN.md                                      ← 12 轮计划
    PROGRESS.md                                  ← 本文件
    briefings/, handoffs/, logs/                 ← 空目录待用
  night_runner_macos.sh                          ← 调度脚本
```

### 关键设计决策（worker 必须遵守）

1. **业务库共享源不 fork**：`gMKVToolNix.Core.csproj` 用 `<Compile Include="..\gMKVToolNix\**\*.cs" />` 引用原 cs 文件。两套 csproj 编译同一份源。修改原文件时务必保证 .NET 4.0 与 .NET 9 都能编译（用 `#if NETFRAMEWORK` 隔离仅老版需要的代码）。
2. **Microsoft.Win32.Registry NuGet 包**：在 .NET 9 的 macOS runtime 上，调用 Registry 会抛 `PlatformNotSupportedException`，由 `PlatformExtensions.IsOnLinux` 拦截避免实际进入。**不要**为了"跨平台"删 Registry 代码——Windows 仍需要。
3. **Avalonia 真毛玻璃**：`MainWindow.axaml` 使用 `TransparencyLevelHint="AcrylicBlur,Mica,Blur,Transparent"` + `ExperimentalAcrylicBorder`，macOS 上对应 NSVisualEffectView。**不要**改成 `Background="White"` 之类破坏透明效果。
4. **本地化 JSON 复用**：csproj 已通过 `<None Include="..\gMKVExtractGUI\gmkvextract-*.json" Link="Localization\..." />` 把 17 个 JSON 复制到输出目录。**不要**复制粘贴 JSON 副本——就近读 link 文件。

### 已知 Round 1 必须解决的问题

- `gMKVToolNix.Core` 共享源中可能有 `.NET Framework`-only API（如某些 String / Encoding 方法签名差异），首次 `dotnet build` 会暴露——claude-code 修
- `MainWindow.axaml` 中 `RootNamespace` 是 `gMKVToolNix.UI`，但 `<StyleInclude Source="avares://gMKVExtractGUI/Themes/Macaron.axaml" />` 中的 assembly 名是 csproj 的 `<AssemblyName>gMKVExtractGUI</AssemblyName>` —— 如果实际编译失败，claude-code-2 调整路径
- `DataFormats.Files` 是 Avalonia 11 API，确认版本兼容

### Round 1 启动条件

两个 worker 都要：
1. 装 .NET 9 SDK（`brew install --cask dotnet-sdk` 或 https://dotnet.microsoft.com/download/dotnet/9.0）
2. 第一次执行前先 `dotnet --version` 确认 >= 9.0

如果 SDK 未装，handoff 写 `status: blocked`，把"装 SDK"作为前置任务报给调度中枢。

---

## 用户期望

1. **马卡龙糖果配色**：粉/薄荷/薰衣草/奶黄/天空蓝；窗口背景三段竖向渐变（粉→紫→薄荷）
2. **真毛玻璃**：macOS 上是 NSVisualEffectView material，不是渐变模拟
3. **拖入文件**：拖到主窗口任何位置都能加载 MKV；拖到不同区域可有不同行为（append vs select）
4. **保留所有原功能**：MKV 提取（轨道/章节/标签/附件）、Job Manager、Log、Options、TranslationEditor、17 语言本地化

## 用户不期望

1. 重构业务逻辑——只是平台移植
2. 自创主题色之外的功能

## 最终交付（Round 12 后）只保留 macOS 相关产物

### 必删（Windows-only）

- `src/gMKVExtractGUI/`（旧 WinForms UI 项目，含本会话阶段 A 的 Macaron 改动）
- `src/gMKVToolNix/`（业务源在 Round 11 物理移到 `gMKVToolNix.Core/` 后的空壳）
- `gMKVExtractGUI.sln`（旧 sln，Round 12 由 `gMKVExtractGUI.macOS.sln` 改名顶替）
- `packages/`（NuGet packages.config 缓存）
- 散落的 `*.suo` / `bin/` / `obj/` Windows 构建残留

### 必留（跨平台 / macOS）

- `src/gMKVToolNix.Core/`（含 Round 11 搬入的全部业务源）
- `src/gMKVExtractGUI.Avalonia/`（Avalonia UI）
- `src/gMKVToolNix.Translator.Console/`（跨平台 console，Round 12 升级为 SDK net9.0）
- `tests/`（单元测试，Round 12 升级为 SDK net9.0）
- `docs/`、`README.md`、`LICENSE`、`.gitignore`
- `night_runner_macos.sh` + `.orchestration/`（夜跑设施）

### Round 12 完成判据
- `git grep -i "System.Windows.Forms"` 0 命中
- `git grep -i "Microsoft.Win32"` 仅在 `OperatingSystem.IsWindows()` 守卫内
- `dotnet build` / `dotnet test` 0 error
- `gMKVExtractGUI.app` 双击启动并能完整提取一次

### 阶段 A 改动的归宿

本会话开始时为旧 WinForms 加的 Macaron 主题（`src/gMKVExtractGUI/Theming/MacaronTheme.cs`、`ThemeManager.cs` 改动、`gSettings.cs`、`frmMain2.cs`）会**随 `src/gMKVExtractGUI/` 一起删除**——不要花精力维护它。如果用户想保留这部分作为 Windows 版本，应在 Round 12 前从 git 历史 `git checkout` 出来另存分支。
