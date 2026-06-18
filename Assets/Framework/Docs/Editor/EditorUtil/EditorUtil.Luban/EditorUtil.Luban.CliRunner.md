# EditorUtil.Luban.CliRunner

**类签名**：`public static class EditorUtil.Luban.CliRunner`
**命名空间**：`NovaFramework.Editor`
**一行描述**：Luban CLI 外部进程调用器 — 封装 dotnet Luban.dll 命令行调用。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|------|------|
| `EditorUtil.Luban.CliRunner.cs` | `EditorUtil.Luban.CliRunner` | Luban CLI 进程管理（路径解析 + 参数拼接 + 进程启动） |

---

## §3 继承关系

```
EditorUtil (public static partial class)
  └── Luban (public static partial class)
        └── CliRunner (public static class)
```

---

## §4 关键字段

| 字段 | 类型 | 修饰符 | 说明 |
|------|------|--------|------|
| `c_PackageName` | `string` | `private const` | UPM 包名：`"com.solotopia.luban"` |
| `c_LubanDllRelPath` | `string` | `private const` | Luban.dll 在 UPM 包内的相对路径：`"Core/Tools~/Luban/Luban.dll"` |
| `s_DotnetFileName` | `string` | `private static readonly` | 当前平台的 dotnet 可执行文件名（Windows 为 `"dotnet.exe"`，其他为 `"dotnet"`） |
| `s_DotnetSearchPaths` | `string[]` | `private static readonly` | 各平台 dotnet 可执行文件的常见安装路径列表（Unity 进程不继承 shell PATH，需手动探测） |

---

## §5 公开 API

```csharp
/// <summary>
/// 获取 Luban.dll 完整路径。
/// </summary>
/// <returns>Luban.dll 路径，不存在时返回 null。</returns>
public static string GetLubanDllPath()

/// <summary>
/// 运行代码生成。
/// </summary>
/// <param name="confPath">luban.conf 文件路径。</param>
/// <param name="targetName">target 名称（如 "table"）。</param>
/// <param name="outputCodeDir">生成代码输出目录。</param>
/// <param name="customTemplateDirs">自定义模板目录列表（可为 null，使用内置模板）。按优先级排序，Luban 依次查找。</param>
/// <returns>是否成功。</returns>
public static bool RunCodeGen(string confPath, string targetName, string outputCodeDir, string[] customTemplateDirs)

/// <summary>
/// 运行数据导出。
/// </summary>
/// <param name="confPath">luban.conf 文件路径。</param>
/// <param name="targetName">target 名称。</param>
/// <param name="outputDataDir">导出数据输出目录。</param>
/// <returns>是否成功。</returns>
public static bool RunDataExport(string confPath, string targetName, string outputDataDir)

/// <summary>
/// 同时运行代码生成与数据导出。
/// </summary>
/// <param name="confPath">luban.conf 文件路径。</param>
/// <param name="targetName">target 名称。</param>
/// <param name="outputCodeDir">生成代码输出目录。</param>
/// <param name="outputDataDir">导出数据输出目录。</param>
/// <param name="customTemplateDirs">自定义模板目录列表（可为 null，使用内置模板）。按优先级排序，Luban 依次查找。</param>
/// <returns>是否成功。</returns>
public static bool RunAll(string confPath, string targetName, string outputCodeDir, string outputDataDir, string[] customTemplateDirs)

/// <summary>
/// 运行 Protobuf3 Schema 生成：调用 Luban 以 `-c protobuf3` 从 Excel 表生成 .proto 文件。
/// </summary>
/// <param name="confPath">luban.conf 文件路径。</param>
/// <param name="targetName">target 名称。</param>
/// <param name="outputCodeDir">生成 .proto 文件的输出目录。</param>
/// <param name="customTemplateDirs">自定义模板目录列表（可为 null，使用内置模板）。按优先级排序，Luban 依次查找。</param>
/// <returns>是否成功。</returns>
public static bool RunProtoSchemaGen(string confPath, string targetName, string outputCodeDir, string[] customTemplateDirs)
```

### internal 方法（供框架内部调用）

| 方法 | 签名 | 说明 |
|------|------|------|
| `ResolveDotnetPath` | `internal static string ResolveDotnetPath()` | 获取 dotnet 可执行文件路径：优先 PATH 查找，失败则逐一探测 `s_DotnetSearchPaths`（供 `LubanChecker` 调用） |
| `GetGeneratedCodeFiles` | `internal static Dictionary<string, string> GetGeneratedCodeFiles(string outputCodeDir, HashSet<string> relevantFileNames = null)` | 扫描输出目录 .cs 文件，返回文件名 → 相对路径字典（Pipeline 日志使用） |

### 私有方法

| 方法 | 签名 | 说明 |
|------|------|------|
| `BuildDotnetSearchPaths` | `string[] BuildDotnetSearchPaths()` | 按当前平台构建 dotnet 候选路径列表 |
| `RunLuban` | `bool RunLuban(string arguments)` | 拼接 dllPath 后调用 `ProcessRunner.RunSync`，检查 ExitCode 和超时 |

---

## §7 线程模型

- 所有方法在编辑器主线程同步执行
- `RunLuban` 通过 `ProcessRunner.RunSync` 调用 `dotnet` 进程，阻塞直到进程退出或超时
- stdout/stderr 由 `ProcessRunner` 内部通过 `BeginOutputReadLine` / `BeginErrorReadLine` 异步读取，调用方无需处理线程问题
- 不建议在大规模数据量下频繁调用，长时间阻塞会冻结 Unity 编辑器

---

## §9 关键算法

### ResolveDotnetPath

Unity 编辑器以 GUI 应用方式启动，**不继承 shell 登录态 PATH**（macOS 下 `.zshrc` / `.bash_profile` 中配置的路径在 Unity 进程中均不可见），因此不能单纯依赖环境变量 PATH 定位 `dotnet`，必须额外探测常见安装位置作为兜底。

解析分两段串行执行，任一命中即立即返回：

**第一段：扫描当前进程 PATH 环境变量**

1. 读取 `System.Environment.GetEnvironmentVariable("PATH")`，按 `Path.PathSeparator` 拆分目录列表
2. 逐目录拼接平台对应可执行文件名（Windows：`dotnet.exe`；其他：`dotnet`），调用 `File.Exists` 检查
3. 首个命中即返回完整路径

**第二段：遍历平台候选安装路径（`s_DotnetSearchPaths`）**

PATH 未命中时，遍历由 `BuildDotnetSearchPaths()` 构建的平台候选列表（`File.Exists` 逐一探测，首个命中即返回）：

| 平台 | 候选路径 |
|------|---------|
| macOS / Linux | `/usr/local/share/dotnet/dotnet` |
| macOS / Linux | `/usr/local/bin/dotnet` |
| macOS / Linux | `/opt/homebrew/bin/dotnet`（Homebrew on Apple Silicon） |
| macOS / Linux | `~/.dotnet/dotnet`（用户目录，`Environment.SpecialFolder.UserProfile`） |
| Windows | `%ProgramFiles%\dotnet\dotnet.exe`（`SpecialFolder.ProgramFiles`） |
| Windows | `~\.dotnet\dotnet.exe`（`SpecialFolder.UserProfile`） |

两段全部未命中时返回 `null`；调用方（`LubanChecker.DoCheck` / `RunLuban`）负责打印错误日志。

### RunLuban

1. 调用 `GetLubanDllPath()` 获取 dll 路径，null 则返回 false
2. 调用 `ResolveDotnetPath()` 获取 dotnet 路径，null 则打印 Error 返回 false
3. 拼接完整参数：`"<dllPath>" <arguments>`
4. 调用 `ProcessRunner.RunSync(dotnetPath, fullArgs)`
5. 检查结果：`TimedOut` → Error 返回 false；`!Success` → Error（含 ExitCode + Stderr）返回 false；有 Stderr 但成功 → Warning

---

## §11 使用示例

```csharp
// 在 Inspector 中调用代码生成（通常通过 Pipeline 间接调用）
string confPath = EditorUtil.Luban.ConfigSyncer.GetConfigDirPath(sourceDirPath) + "/luban.conf";
string dllPath = EditorUtil.Luban.CliRunner.GetLubanDllPath();
if (dllPath != null)
{
    bool success = EditorUtil.Luban.CliRunner.RunCodeGen(
        confPath,
        "table",
        outputCodeDir,
        customTemplateDirs: null
    );
}
```

> 通常不直接调用 CliRunner，而是通过 `Pipeline.ExportCode` / `Pipeline.ExportData` / `Pipeline.ExportAll` 间接调用。

---

## §13 关联文档

- [EditorUtil.Luban.Pipeline.md](EditorUtil.Luban.Pipeline.md)
- [EditorUtil.Luban.ConfigSyncer.md](EditorUtil.Luban.ConfigSyncer.md)
- [EditorUtil.Environment.LubanChecker.md](../EditorUtil.Environment/EditorUtil.Environment.LubanChecker.md)
- [EditorUtil.ProcessRunner.md](../EditorUtil.ProcessRunner/EditorUtil.ProcessRunner.md)
- [EditorUtil.Proto.CliRunner.md](../EditorUtil.Proto/EditorUtil.Proto.CliRunner.md)
- [EditorUtil.md](../EditorUtil.md)
