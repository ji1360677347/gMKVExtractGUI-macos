# gMKVExtractGUI macOS 端口·开发计划（调度中枢活动版本）

> 目标：把 .NET 4.0 WinForms 项目移植到 .NET 9 + Avalonia 11，macOS 原生可运行，UI 用马卡龙糖果配色 + 真毛玻璃。
> **最终交付只保留 macOS 相关产物，所有 Windows-only 代码与构建产物在 Round 12 删除**（详见 Round 12 清单）。
> Round 0 已完成项目骨架（见 PROGRESS.md），下面是 12 轮推进计划。

---

## 文件管辖（硬约束 - 越界 = 当轮回退 + handoff 记「越界尝试」）

### claude-code（业务库 + 跨平台）

**白名单**：
- `src/gMKVToolNix.Core/`
- `src/gMKVToolNix/**/*.cs`（共享源；仅做跨平台修复，禁止删除已有功能）
- `gMKVExtractGUI.macOS.sln` 中业务库相关行

**职责**：
- 跨平台 mkvtoolnix CLI 探测（PATH、`/opt/homebrew/bin`、`/usr/local/bin`、`/Applications/`）
- 修复 .NET 9 编译错误（Newtonsoft / Process / Path / Encoding 兼容）
- gSettings 路径：macOS 用 `~/Library/Application Support/gMKVExtractGUI/`
- 单元测试 / 集成 smoke

### claude-code-2（Avalonia UI）

**白名单**：
- `src/gMKVExtractGUI.Avalonia/`（所有 axaml / cs / csproj / manifest / 子目录）

**职责**：
- 各窗口实现（Main / Jobs / Log / Options / TranslationEditor）
- 马卡龙主题打磨（毛玻璃、圆角、阴影、悬停态）
- ViewModel / Command / Binding
- 本地化（复用 `src/gMKVExtractGUI/gmkvextract-*.json`）
- macOS .app bundle 打包

### 共同硬禁区（Round 1-11 绝对不动；Round 12 由调度中枢显式授权清理）

- `src/gMKVExtractGUI/`（旧 WinForms 项目，**Round 12 删**）
- `gMKVExtractGUI.sln`（旧 sln，**Round 12 删**）
- `packages/`（NuGet packages.config 缓存，**Round 12 删**）
- `night_runner_macos.sh`
- `.orchestration/PLAN.md`（只能读）
- `LOCALIZATION_*.md` / `README.md` / `docs/`（**Round 12 由 claude-code-2 改写为 macOS 文档**）

**说明**：Round 1-11 期间禁止删除/修改上述项，确保旧项目持续可参考。Round 12 由调度中枢在 briefing 中明确放开权限。

---

## 12 轮拓扑

### Round 1：业务库可编译 + UI 可启动

| Worker | Task | 验证 |
|--------|------|------|
| claude-code | 跑 `dotnet build src/gMKVToolNix.Core/`，修所有编译错误（namespace、Microsoft.Win32 在非 Windows 抛 PNS、字符串 API 差异）；保证 macOS 上 build 0 error 0 warning（已 NoWarn 的不算） | `dotnet build` exit=0 |
| claude-code-2 | 跑 `dotnet build src/gMKVExtractGUI.Avalonia/`，修 axaml 引用错误；`dotnet run` 弹出主窗口（毛玻璃 + 拖入提示），不闪退 | `dotnet run` 启动后窗口可见 |

### Round 2：mkvtoolnix 路径探测 + 真实轨道渲染

| Worker | Task |
|--------|------|
| claude-code | 写 `MkvToolnixLocator`：先查 PATH（`mkvextract --version`），再查 `/opt/homebrew/bin/`、`/usr/local/bin/`、`/Applications/MKVToolNix*/Contents/MacOS/`；返回路径或 null。Windows 沿用 Registry。提供 `gSettings` 跨平台路径（macOS: `~/Library/Application Support/gMKVExtractGUI/gMKVExtractGUI.ini`） |
| claude-code-2 | 在 ViewModel 调用 `gMKVMerge.GetMKVSegments` 解析真实轨道；UI 列出 video/audio/subtitle/chapter/attachment；CheckBox 选中状态可改；空状态/错误态都有友好提示 |

### Round 3：mkvextract 调用 + 进度回传

| Worker | Task |
|--------|------|
| claude-code | 让 `gMKVExtract.ExtractMKVSegmentsThreaded` 在 macOS 上工作（Process 启动、stdout 解析、cancel）；进度通过事件/回调暴露 |
| claude-code-2 | 提取按钮 → 异步调用业务层；进度条 + 实时输出区；取消按钮；完成后状态条提示 |

### Round 4：Job Manager 窗口

| Worker | Task |
|--------|------|
| claude-code | `gMKVJob` 队列在 macOS 上跑通，序列化到 `~/Library/Application Support/gMKVExtractGUI/jobs.json` |
| claude-code-2 | `JobManagerWindow.axaml` + ViewModel：DataGrid 显示队列、进度、操作按钮；马卡龙风格 |

### Round 5：Options 窗口 + Settings 持久化

| Worker | Task |
|--------|------|
| claude-code | `gSettings` 跨平台 ini 路径修复；新加 `ThemeMode` 字段（Light/Dark/Macaron）持久化 |
| claude-code-2 | `OptionsWindow.axaml` + ViewModel：MkvToolnix 路径浏览、输出文件名 patterns、主题选择、Culture 选择 |

### Round 6：本地化集成

| Worker | Task |
|--------|------|
| claude-code | 让 `JsonLocalizationService` 在 .NET 9 / macOS 加载 17 个 JSON；`LocalizedFontResolver` 字体回退（macOS 用 Helvetica/PingFang） |
| claude-code-2 | App 启动读 Settings.Culture，所有窗口的字符串走 `LocalizationManager.GetString`；切换语言不重启即生效 |

### Round 7：Log 窗口

| Worker | Task |
|--------|------|
| claude-code | `gMKVLogger` 输出可订阅事件（已有 → 验证 macOS 工作） |
| claude-code-2 | `LogWindow.axaml` + ViewModel：实时滚动、清空、保存、过滤 |

### Round 8：TranslationEditor

| Worker | Task |
|--------|------|
| claude-code | `TranslationFileService` / `TranslationMaintenanceService` 在 macOS 上跑通 |
| claude-code-2 | `TranslationEditorWindow.axaml` + ViewModel：双列对照、JSON 加载/保存、标记缺失 |

### Round 9：拖拽完善

| Worker | Task |
|--------|------|
| claude-code | 业务侧：批量加载、文件夹递归扫描、失败收集 |
| claude-code-2 | UI：append vs replace 模式、拖到不同区域不同行为、视觉反馈（粉色发光边框） |

### Round 10：错误处理 + 启动恢复

| Worker | Task |
|--------|------|
| claude-code | mkvtoolnix 缺失/版本过低提示；崩溃日志 |
| claude-code-2 | Window 大小/位置持久化；首次启动引导（让用户装 mkvtoolnix） |

### Round 11：macOS 打包 + 业务源就地化

| Worker | Task |
|--------|------|
| claude-code | (1) **物理移动**业务源：`git mv src/gMKVToolNix/**/*.cs src/gMKVToolNix.Core/`（保留 `Chapters/matroskachapters.xsd`、`.dtd` 一并搬过来）；(2) 改 `gMKVToolNix.Core.csproj` 去掉 `<Compile Include="..\gMKVToolNix\**\*.cs" />`，让 SDK 默认 glob 接管；(3) `dotnet publish -c Release -r osx-arm64 --self-contained` + `osx-x64` 双 RID 出包 |
| claude-code-2 | `.app` bundle 装配：`Info.plist`、`icon.icns`（从原 `Images/gMkvExtractGuiIcon.ico` 转换）、CFBundleDocumentTypes 注册 `.mkv/.mka/.mks/.webm` 文件关联、`dotnet publish` 后用 shell 脚本组装成 `gMKVExtractGUI.app`，可双击启动、可拖到 `/Applications` |

### Round 12：最终清理 + 测试 + 文档（**唯一可碰硬禁区的轮次**）

| Worker | Task |
|--------|------|
| claude-code | **(A) 集成测试**：用样例 MKV 跑"加载 → 选轨道 → 提取 → 检查输出文件"；迁移 `tests/gMKVToolnix.Unit.Tests` 到 SDK 风格 `net9.0` + xUnit/NUnit；保证 `dotnet test` 通过。<br/>**(B) Windows 残留删除**：`git rm -rf src/gMKVExtractGUI/` `src/gMKVToolNix/`（已搬空）`src/gMKVToolNix.Translator.Console/Properties/` 等不需要的旧 csproj 风格残留；`git rm gMKVExtractGUI.sln`；`git rm -rf packages/`；把 `src/gMKVToolNix.Translator.Console/` 升级为 SDK 风格 `net9.0` 跨平台 console（保留——它是给翻译者用的工具）。<br/>**(C) 重命名** `gMKVExtractGUI.macOS.sln` → `gMKVExtractGUI.sln`（最终唯一 sln） |
| claude-code-2 | **(A) 文档**：写 `README.md`（覆盖原英文 README）：macOS 安装步骤、`brew install mkvtoolnix`、首次启动、马卡龙主题截图、已知限制；删除 `LOCALIZATION_IMPLEMENTATION_REPORT.md` / `LOCALIZATION_STRINGS_MANIFEST.md`（如果内容已过期）或更新到 macOS 现状。<br/>**(B) UI 终验**：在 macOS 上手动跑通主流程，screenshot 全部 5 个窗口；修任何 UI 残缺。<br/>**(C) `.gitignore` 整理**：去掉只对 Windows 有意义的条目（如 `bin/Debug/`、`obj/Debug/` 这类规则保留即可，但 `*.suo`、`*.vcxproj` 之类特别 VS 的可清理）。 |

#### Round 12 完成判据
- 仓库根只剩：`src/gMKVToolNix.Core/`、`src/gMKVExtractGUI.Avalonia/`、`src/gMKVToolNix.Translator.Console/`、`tests/`、`docs/`、`.orchestration/`、`README.md`、`gMKVExtractGUI.sln`、`night_runner_macos.sh`、`.gitignore`、`LICENSE`（如有）
- `dotnet build gMKVExtractGUI.sln` 在 macOS 上 0 error
- `dotnet test` 通过
- `gMKVExtractGUI.app` 双击可启动，能完成一次完整提取
- `git grep -i "winforms\|System.Windows.Forms"` 0 命中（除注释提到历史外）
- `git grep -i "Microsoft.Win32"` 仅在 Windows-runtime 检查代码内出现（被 `OperatingSystem.IsWindows()` 守卫）

---

## 调度中枢行为准则

1. 每轮先看 handoff 判断上轮成败：成功 → 推进；失败 → 重派同任务（briefing 加上诊断提示）
2. 任务有依赖时，依赖未完成本轮跳过给「无任务」
3. 任何 worker 修改了别人白名单的文件 → 下一轮 briefing 中明确要求**回滚**该改动
4. 每轮 worker briefing 必须包含「自验证」command（dotnet build / dotnet run）
5. 项目完成的判定：Round 12 两个 worker 都完成且 .app bundle 能在 macOS 上双击启动
