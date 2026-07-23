# EnvironmentWindow

## §1 文件头

```csharp
// 当前仓库中并不存在名为 EnvironmentWindow 的类。
// 目录 Assets/Framework/Scripts/Editor/Windows/EnvironmentWindow/ 为空目录（仅有 .meta 占位）。
// namespace: -
// 菜单路径: -（无独立菜单入口）
```

`EnvironmentWindow` 在当前源码中**没有实现**——既没有 `.cs` 文件、也没有 `[MenuItem]` 入口、也没有任何类继承 `EditorWindow` 叫这个名字。它被框架文档体系列出来，是因为「环境检测」这一能力面在 Nova 编辑器里客观存在，但实际承载形式是：

- **UI 宿主**：内嵌在 `ConfigWindow`（`Nova/Open Config`）的左侧树「环境检测」分组下，提供 Luban / Python3 / HybridCLR 三个环境检测面板。
- **检测引擎**：`EditorUtil.Environment` 命名空间下的三套静态检查器（`LubanChecker` / `Python3Checker` / `HybridCLRChecker`），与窗口解耦，可被任意调用方使用。

因此本文档定位为：**解释"环境检测"功能在 Nova 编辑器里是如何呈现的、从哪打开、能做什么、未就绪时怎么办**。所有操作都发生在 `ConfigWindow` 内。

---

## §2 文件表

`EnvironmentWindow/` 目录本身**没有任何 `.cs` 文件**（空目录，仅含 `.meta`）。

「环境检测」功能实际由以下文件承载：

| 文件 | 类 | 说明 |
|------|----|------|
| `Editor/Windows/ConfigWindow/ConfigWindow.cs` | `ConfigWindow` | 窗口主类；`Open()` 菜单入口 `Nova/Open Config`；`OpenLubanSection(result)` 供外部管线调用并自动导航到 Luban 面板 |
| `Editor/Windows/ConfigWindow/ConfigWindow.Visitors.cs` | `ConfigWindow` | 字段：环境检测相关字段 `m_LubanCheckResult` / `m_Python3CheckResult` / `m_HybridCLRCheckResult`；`LeftTreeItem.LubanEnv / Python3Env / HybridCLREnv` 三个二级树节点枚举 |
| `Editor/Windows/ConfigWindow/ConfigWindow.LeftTree.cs` | `ConfigWindow` | 左树：绘制「环境检测」一级组 + 三个二级条目（Luban 环境检测 / Python3 环境检测 / HybridCLR 环境检测） |
| `Editor/Windows/ConfigWindow/ConfigWindow.RightPanel.Luban.cs` | `ConfigWindow` | Luban 面板：`DrawLubanSection` / `DrawLubanStatusAndButtons` / `DrawLubanWindowsExportWarning` / `DrawLubanInstallGuide` / `ResolveDotnetStatusText` / `ResolveLubanDllStatusText` / `IsDotnetReady` |
| `Editor/Windows/ConfigWindow/ConfigWindow.RightPanel.Python3.cs` | `ConfigWindow` | Python3 面板：`DrawPython3Section` / `DrawPython3StatusAndButtons` / `ResolvePython3StatusText` |
| `Editor/Windows/ConfigWindow/ConfigWindow.RightPanel.HybridCLREnv.cs` | `ConfigWindow` | HybridCLR 面板：`DrawHybridCLREnvSection` / `DrawHybridCLREnvStatusAndButtons` / `ResolveHybridCLREnvStatusText` / `ResolveHybridCLRGuideMessage` |
| `Editor/Windows/ConfigWindow/ConfigWindow.Methods.cs` | `ConfigWindow` | 检测入口：`RunLubanCheck` / `RunPython3Check` / `RunHybridCLRCheck`（窗口打开时各调一次 `Check()`） |
| `Editor/EditorUtil/EditorUtil.Environment/EditorUtil.Environment.cs` | `EditorUtil.Environment` | 空 partial 命名空间容器，聚合三套检查器 |
| `Editor/EditorUtil/EditorUtil.Environment/EditorUtil.Environment.LubanChecker.cs` | `EditorUtil.Environment.LubanChecker` | `[InitializeOnLoad]` 启动时静默检测；`Check()` / `Recheck()`；dotnet 路径+版本区间 `[8.0.127, 10.0.203]` 校验 + Luban.dll 检测 |
| `Editor/EditorUtil/EditorUtil.Environment/EditorUtil.Environment.Python3.cs` | `EditorUtil.Environment.Python3Checker` | `Check()` / `Recheck()`；5 策略多路径探测（ExplicitPath → PATH → PyLauncher → Where → PythonFallback） |
| `Editor/EditorUtil/EditorUtil.Environment/EditorUtil.Environment.HybridCLRChecker.cs` | `EditorUtil.Environment.HybridCLRChecker` | `Check()` / `Recheck()`；双条件：包是否安装 + Installer 是否已跑过 |

---

## §3 继承关系

`EnvironmentWindow` 类不存在，无继承链。功能实际由 `ConfigWindow` 承载：

```
UnityEditor.EditorWindow
  └── ConfigWindow (internal sealed partial)

EditorUtil.Environment (public static partial class)
  ├── LubanChecker       (internal static, [InitializeOnLoad])
  ├── Python3Checker     (public static)
  └── HybridCLRChecker   (public static)
```

---

## §5 公开 API

`EnvironmentWindow` 本身没有公开 API。「环境检测」对外的入口分两层：

**窗口层（ConfigWindow）：**

```csharp
// 菜单入口：Nova/Open Config
[MenuItem("Nova/Open Config")]
public static void Open()

// 供 EditorUtil.Luban.Pipeline 调用：打开窗口并自动导航到 Luban 面板
public static void OpenLubanSection(EnvironmentCheckResult result)
```

**检查器层（EditorUtil.Environment.*，可被任意 Editor 代码直接调用）：**

```csharp
// Luban 环境检查（结果走 SessionState 缓存）
EditorUtil.Environment.LubanChecker.EnvironmentCheckResult r1 =
    EditorUtil.Environment.LubanChecker.Check();

// 强制重检（忽略 SessionState 缓存）
EditorUtil.Environment.LubanChecker.EnvironmentCheckResult r2 =
    EditorUtil.Environment.LubanChecker.Recheck();

// Python3 / HybridCLR 同理：Check() / Recheck()
EditorUtil.Environment.Python3Checker.Python3CheckResult      p = EditorUtil.Environment.Python3Checker.Check();
EditorUtil.Environment.HybridCLRChecker.HybridCLRCheckResult  h = EditorUtil.Environment.HybridCLRChecker.Check();
```

---

## §5·1 打开方式（必读）

`EnvironmentWindow` **没有独立菜单**。所有环境检测 UI 都在 `ConfigWindow` 内：

```
Unity 菜单栏 → Nova → Open Config → 左侧树「环境检测」分组 → 三个二级条目
  ├─ Luban 环境检测
  ├─ Python3 环境检测
  └─ HybridCLR 环境检测
```

或代码调用：

```csharp
ConfigWindow.Open();                       // 打开后默认落在 Luban 环境检测面板
ConfigWindow.OpenLubanSection(result);     // Pipeline 内部使用：直接跳到 Luban 面板并预填检测结果
```

**首次打开默认选中**：`LeftTreeItem.LubanEnv`（`m_SelectedItem` 默认值），即窗口一打开就落在 Luban 环境检测面板。

---

## §5·2 Luban 环境检测面板

| 区块 / 控件 | 行为 |
|------------|------|
| 标题 | `Luban 环境检测`（`m_SectionTitleStyle`） |
| .NET SDK 状态块 | 图标 ✓/✗ + `.NET SDK` + 状态描述；`IsDotnetReady()` 判定（Issue 不属于 4 种 dotnet 异常即视为就绪） |
| Luban.dll 状态块 | 图标 ✓/✗ + `Luban.dll` + 状态描述；与 .NET SDK 状态块同行，用 `\|` 分隔；仅在 Issue 为 `LubanDllNotFound` 时显示"未找到，请确认 com.solotopia.luban 包已安装"，dotnet 未就绪时显示"（待检测）" |
| 「重新检测」按钮 | 调 `EditorUtil.Environment.LubanChecker.Recheck()` 忽略 SessionState 缓存强刷，结果回写 `m_LubanCheckResult` 并 `Repaint()`；宽 80f，右对齐 |
| 「打开官网」按钮 | `Application.OpenURL("https://dotnet.microsoft.com/download/dotnet/10.0")`；宽 80f |
| Windows 平台警告 | 仅 `RuntimePlatform.WindowsEditor` 显示，HelpBox 提示 Win11 智能应用控制可能拦截 Luban 导出，给出「设置 → 隐私和安全性 → Windows 安全中心 → 应用和浏览器控制 → 智能应用控制 → 关闭」操作路径 |
| 安装指南区 | 仅 dotnet 未就绪时显示（`DotnetNotFound` / `VersionTooLow` / `VersionTooHigh`）；按当前平台显示对应安装命令 HelpBox + 「复制」按钮（写入 `EditorGUIUtility.systemCopyBuffer`） |
| macOS / Linux 安装命令 | `curl -sSL https://dot.net/v1/dotnet-install.sh \| bash -s -- --version 10.0.203` |
| Windows 安装命令 | `&([scriptblock]::Create((irm https://dot.net/v1/dotnet-install.ps1))) -Version 10.0.203` |
| 建议版本 | `建议版本：8.0.127 ~ 10.0.203`（拼 `LubanChecker.c_MinDotnetVersion` / `c_MaxDotnetVersion`） |

**未就绪时的状态文案**（`ResolveDotnetStatusText`）：

| `EnvironmentIssue` | 显示文本 |
|--------------------|---------|
| `DotnetNotFound` | `未找到 dotnet，请安装 8.0.127 ~ 10.0.203` |
| `DotnetVersionTooLow` | `版本过低（当前 {ver}，需要 8.0.127 ~ 10.0.203）` |
| `DotnetVersionTooHigh` | `版本过高（当前 {ver}，需要 8.0.127 ~ 10.0.203）` |
| `DotnetNotExecutable` | `dotnet 执行失败，请检查安装是否完整` |
| `None` 且版本非空 | `就绪（{ver}）` |

---

## §5·3 Python3 环境检测面板

| 区块 / 控件 | 行为 |
|------------|------|
| 标题 | `Python3 环境检测` |
| 状态行 | 图标 ✓/✗ + `Python3` + 状态文本（宽 200f）；不可用时显示 `未找到 python3，请安装 Python 3.x`，可用时显示 `就绪（{Version}）` |
| 「重新检测」按钮 | 调 `EditorUtil.Environment.Python3Checker.Recheck()` 强刷并 `Repaint()`；右对齐 |

**检测策略**（Python3Checker 内部 5 步，首个命中即返回）：

1. **ExplicitPath**：按平台硬编码候选路径逐一尝试（macOS 5 条：Homebrew Apple Silicon/Intel、`/usr/bin/python3`、python.org Framework、pyenv；Windows 5 版本 × 5 安装位置共 25 条；Linux 3 条）
2. **PATH**：执行 `python3 --version`
3. **PyLauncher**（仅 Windows）：`py -3 --version`
4. **Where**（仅 Windows）：`where python3` 取首行再 `--version`
5. **PythonFallback**：`python --version`，但输出必须以 `Python 3.` 开头（防 Python 2 误判）

每次外部命令超时上限 3 秒（`c_ProbeTimeoutMs = 3000`）。

---

## §5·4 HybridCLR 环境检测面板

| 区块 / 控件 | 行为 |
|------------|------|
| 标题 | `HybridCLR 环境检测` |
| 状态行 | 图标 ✓/✗ + `HybridCLR` + 状态文本（宽 260f）；就绪时显示 `就绪（{PackageVersion}）`，未就绪时按 Issue 显示 `未安装包` / `Installer 未运行` / `未就绪` |
| 「重新检测」按钮 | 调 `EditorUtil.Environment.HybridCLRChecker.Recheck()` 强刷并 `Repaint()`；右对齐 |
| 操作指引 HelpBox | 仅在未就绪时显示；`PackageNotFound` → "请确认 Packages/manifest.json 中引用了 com.solotopia.hybridclr"；`InstallerNotRun` → "请点击菜单 HybridCLR > Installer... 按引导完成安装" |

**双条件就绪判定**（HybridCLRChecker 内部）：

1. `Packages/com.solotopia.hybridclr/package.json` 存在（不存在则 `PackageNotFound`）
2. `HybridCLRData/LocalIl2CppData-{platform}/il2cpp/libil2cpp/hybridclr/` 目录存在（不存在则 `InstallerNotRun`）

版本号通过 `JsonUtility.FromJson<PackageJsonDto>` 从 `package.json` 的 `version` 字段解析，解析失败仅置 null，不阻断就绪判定。

---

## §6 布局结构

环境检测在 `ConfigWindow` 内的位置：

```
ConfigWindow (Nova · Config)
  DrawMainTitle()
  DrawTopBar()
  Layout.Horizontal
    DrawLeftTree()
      └─ 「环境检测」一级组 (m_GroupExpandedEnvironment，默认展开)
          ├─ Luban 环境检测      ← 默认选中
          ├─ Python3 环境检测
          └─ HybridCLR 环境检测
      └─ 「通用配置」一级组
      └─ 「Kit 配置」一级组
      └─ 「SDK 配置」一级组
    DrawRightPanel()
      switch (m_SelectedItem)
        case LeftTreeItem.LubanEnv:      DrawLubanSection()
        case LeftTreeItem.Python3Env:    DrawPython3Section()
        case LeftTreeItem.HybridCLREnv:  DrawHybridCLREnvSection()
        ...
```

**注意**：未绑定 `ConfigMasterSO` 资产时（`m_Master == null`），左树只显示「环境检测」一组 + 绑定引导，其它三组隐藏——即环境检测是 ConfigWindow 的**兜底可用功能**，不依赖任何资产绑定。

---

## §7 状态字段

环境检测相关状态全部集中在 `ConfigWindow.Visitors.cs`：

| 字段 | 类型 | 说明 |
|------|------|------|
| `m_GroupExpandedEnvironment` | `bool` | 左树「环境检测」一级组折叠状态，默认 `true`（展开） |
| `m_LubanCheckResult` | `LubanChecker.EnvironmentCheckResult` | Luban 检测结果缓存；`OnEnable` → `RunLubanCheck()` 初始化 |
| `m_Python3CheckResult` | `Python3Checker.Python3CheckResult` | Python3 检测结果缓存；`OnEnable` → `RunPython3Check()` 初始化 |
| `m_HybridCLRCheckResult` | `HybridCLRChecker.HybridCLRCheckResult` | HybridCLR 检测结果缓存；`OnEnable` → `RunHybridCLRCheck()` 初始化 |
| `m_SelectedItem` | `LeftTreeItem` | 左树当前选中项，默认 `LeftTreeItem.LubanEnv` |

三套检查器内部的 SessionState 缓存键（编辑器会话内不重复检测）：

| 检查器 | SessionState 键前缀 |
|--------|---------------------|
| LubanChecker | `Nova.Luban.EnvCheckReady` / `EnvCheckDotnetPath` / `EnvCheckDotnetVersion` / `EnvCheckErrorMessage` / `EnvCheckIssue` |
| Python3Checker | `Nova.Python3.EnvCheckCached` / `EnvCheckAvailable` / `EnvCheckVersion` / `EnvCheckDetectedPath` / `EnvCheckDetectedVia` |
| HybridCLRChecker | `Nova.HybridCLR.EnvCheckCached` / `EnvCheckReady` / `EnvCheckIssue` / `EnvCheckVersion` / `EnvCheckError` |

---

## §8 初始化时序

**编辑器启动时（Luban 专属）：**

```
[InitializeOnLoad] LubanChecker 静态构造
  → EditorApplication.delayCall += RunSilentCheck
  → 首次 update 时静默调 Check()
  → 若不就绪：Log.Warning("Luban 环境未就绪：…")
     （仅日志，不弹窗，不自动打开 ConfigWindow）
```

**ConfigWindow 打开时：**

```
ConfigWindow.Open() / OpenLubanSection(result)
  → GetWindow<ConfigWindow>(utility=true)
  → OnEnable
      → RunLubanCheck()       → m_LubanCheckResult      = LubanChecker.Check()
      → RunPython3Check()     → m_Python3CheckResult    = Python3Checker.Check()
      → RunHybridCLRCheck()   → m_HybridCLRCheckResult  = HybridCLRChecker.Check()
  → OnGUI → 默认选中 LeftTreeItem.LubanEnv → DrawLubanSection()
```

`Check()` 走 SessionState 缓存：同一会话第二次调用直接读缓存，不重新执行外部命令。

---

## §9 关键算法

### dotnet 版本闭区间校验（LubanChecker）

`EditorUtil.Environment.LubanChecker.DoCheck()` 依次执行：

1. **解析 dotnet 路径**：`Luban.CliRunner.ResolveDotnetPath()`，失败 → `DotnetNotFound`
2. **执行 `dotnet --version`**：超时或失败 → `DotnetNotExecutable`
3. **版本解析**：剥离 `-preview` / `-rc` 后缀后 `Version.TryParse`，失败 → `DotnetNotExecutable`
4. **区间校验**：与 `[c_MinDotnetVersion=8.0.127, c_MaxDotnetVersion=10.0.203]` 比对；低于下限 → `DotnetVersionTooLow`，高于上限 → `DotnetVersionTooHigh`（两侧都硬阻断）
5. **Luban.dll 检测**：`Luban.CliRunner.GetLubanDllPath()` + `File.Exists`，失败 → `LubanDllNotFound`
6. 全部通过 → `EnvironmentIssue.None`，`IsReady = true`

### Python3 五策略探测（Python3Checker）

依次尝试 5 条策略，**首个命中即短路返回**，全部失败才返回 `NotFound`：

```
ExplicitPath → PATH → PyLauncher (Win) → Where (Win) → PythonFallback
```

所有外部命令统一通过 `EditorUtil.ProcessRunner.RunSync(exe, args, 3000)` 执行；版本号用 `^Python 3\.\d+` 正则匹配输出第一行（`python --version` 部分旧版本输出到 stderr，已兼容）。

### HybridCLR 双条件判定（HybridCLRChecker）

```
package.json 存在？            ── 否 → PackageNotFound
  ↓ 是
libil2cpp/hybridclr 目录存在？  ── 否 → InstallerNotRun
  ↓ 是
IsReady = true
```

版本号从 `package.json` 的 `version` 字段经 `JsonUtility.FromJson<PackageJsonDto>` 解析；解析失败置 null，不影响就绪判定。

---

## §10 常见误区

- **误区 1：去找一个叫 `EnvironmentWindow` 的菜单或类**
  仓库中不存在 `EnvironmentWindow` 类，也没有 `Nova/Open Environment` 之类的菜单。所有环境检测 UI 都在 `Nova/Open Config` 打开的 `ConfigWindow` 里。`Scripts/Editor/Windows/EnvironmentWindow/` 是空目录（仅占位 `.meta`），未来如果独立成窗体会占用该目录。

- **误区 2：以为 `Nova/Luban 环境检查` 是一个真实菜单**
  `LubanChecker.RunSilentCheck` 里的 Warning 日志写的是「请通过 Nova/Luban 环境检查 打开引导窗口」，但源码里**不存在**这个菜单项，是历史遗留文案。正确路径是 `Nova/Open Config` → 左树 → `Luban 环境检测`。

- **误区 3：以为窗口每次打开都会重新执行外部命令**
  三套检查器都把结果写进了 `SessionState`，同一编辑器会话内 `Check()` 直接读缓存，不会反复 `dotnet --version` / `python3 --version`。只有「重新检测」按钮（调 `Recheck()`）才会清缓存并真跑一遍。

- **误区 4：在 ConfigWindow 里改完系统环境后忘记点「重新检测」**
  用户在系统层面装好 dotnet / Python3 后，如果 ConfigWindow 已经开着，状态不会自动刷新——必须点对应面板的「重新检测」按钮触发 `Recheck()`。

- **误区 5：以为 Luban 环境不就绪会弹窗打断工作流**
  `RunSilentCheck` 只 `Log.Warning` 不弹窗。真正会"打断"的是 `EditorUtil.Luban.Pipeline.Export*` 三个入口：它们在执行前先调 `LubanChecker.Check()`，不就绪时自动调 `ConfigWindow.OpenLubanSection(result)` 打开窗口并提前返回 `false`，属于**业务管线的守卫**，不是检查器自身的行为。

- **误区 6：Python3 Fallback 接受 Python 2**
  策略 E（`python --version`）会用 `^Python 3\.` 正则强校验；`Python 2.x` 输出不会被误判为可用。

---

## §12 注意事项

- **未绑 Master 也能用环境检测**：`ConfigWindow` 未绑定 `ConfigMasterSO` 时，左树只渲染「环境检测」一组；环境检测面板不依赖任何资产。这意味着**首次安装 Nova、还没创建任何配置资产时就能打开窗口完成环境检查**。

- **「重新检测」按钮无副作用**：仅清当前检查器的 SessionState 缓存再跑一次 `DoCheck()`，不改文件、不动 Master、不触发 dirty。三个面板各自独立，互不影响。

- **所有外部命令走 `EditorUtil.ProcessRunner.RunSync`**：不要在检查器里直接 `Process.Start`，否则会绕过统一的超时与输出捕获机制。

- **新增环境检测面板的接入步骤**（供框架维护者参考）：
  1. 在 `EditorUtil.Environment` 下新增 `XxxChecker` 静态类（`Check()` / `Recheck()` + SessionState 缓存）
  2. `ConfigWindow.Visitors.cs` 的 `LeftTreeItem` 枚举追加 `XxxEnv`，并加 `m_XxxCheckResult` 字段
  3. `ConfigWindow.LeftTree.cs` 的「环境检测」分组里追加 `DrawLeftTreeItem("Xxx 环境检测", LeftTreeItem.XxxEnv, null)`
  4. 新增 `ConfigWindow.RightPanel.Xxx.cs` 实现 `DrawXxxSection()`
  5. `ConfigWindow.Methods.cs` 的 `OnEnable` 链路追加 `RunXxxCheck()`
  6. `ConfigWindow.RightPanel.cs` 的 `DrawRightPanel` switch 追加 `case LeftTreeItem.XxxEnv`

---

## §11 使用示例

**用户路径（日常最常见）：**

```
1. Unity 菜单栏 → Nova → Open Config
2. 窗口打开后默认落在「Luban 环境检测」面板
3. 若 .NET SDK 显示 ✗，按面板下方「安装指南」的 HelpBox 命令复制到终端执行
4. 安装完成后回到 Unity，点「重新检测」按钮刷新状态
5. 同样方式检查 Python3 / HybridCLR
```

**业务管线调用（自动守卫）：**

```csharp
// EditorUtil.Luban.Pipeline.ExportData / ExportCode / ExportAll 内部：
var envResult = EditorUtil.Environment.LubanChecker.Check();
if (!envResult.IsReady)
{
    ConfigWindow.OpenLubanSection(envResult);  // 自动打开窗口并跳到 Luban 面板
    return false;
}
```

**Editor 代码直接查询环境状态（不打开窗口）：**

```csharp
var py = EditorUtil.Environment.Python3Checker.Check();
if (py.IsAvailable)
{
    Log.Debug(LogTag.Editor, "Python3 OK: {0} via {1}", py.Version, py.DetectedVia);
}
```

---

## §13 关联文档

- [Editor.md](../Editor.md)
- [ConfigWindow.md](ConfigWindow.md)（环境检测面板的实际宿主窗口）
- [EditorUtil.Environment.md](../EditorUtil/EditorUtil.Environment/EditorUtil.Environment.md)
- [EditorUtil.Environment.Python3.md](../EditorUtil/EditorUtil.Environment/EditorUtil.Environment.Python3.md)
- [EditorUtil.Environment.LubanChecker.md](../EditorUtil/EditorUtil.Environment/EditorUtil.Environment.LubanChecker.md)
