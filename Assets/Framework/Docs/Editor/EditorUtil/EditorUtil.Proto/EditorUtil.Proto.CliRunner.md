# EditorUtil.Proto.CliRunner

**类签名**：`public static class EditorUtil.Proto.CliRunner`
**命名空间**：`NovaFramework.Editor`
**一行描述**：protoc CLI 外部进程调用器 — 封装 protoc 命令行调用（Mac + Win 跨平台）。

---

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `EditorUtil.Proto.CliRunner.cs` | `EditorUtil.Proto.CliRunner` | protoc 路径解析 + 参数拼接 + 进程启动 + Luban→protoc 闭环管线 |

---

## §3 继承关系

```
EditorUtil (public static partial class)
  └── Proto (public static partial class)
        └── CliRunner (public static class)
```

---

## §4 关键字段

| 字段 | 类型 | 修饰符 | 说明 |
|---|---|---|---|
| `c_PackageName` | `string` | `private const` | UPM 包名：`"com.solotopia.luban"` |
| `c_ProtocRelPathMac` | `string` | `private const` | protoc 在 UPM 包内的相对路径（Mac）：`"Core/Tools~/protoc/bin/protoc"` |
| `c_ProtocRelPathWin` | `string` | `private const` | protoc 在 UPM 包内的相对路径（Windows）：`"Core/Tools~/protoc/bin/protoc.exe"` |
| `c_ProtocIncludeRelPath` | `string` | `private const` | protoc include 目录在 UPM 包内的相对路径：`"Core/Tools~/protoc/include"` |
| `c_SearchPattern` | `string` | `private const` | .proto 文件搜索模式：`"*.proto"` |

---

## §5 公开 API

```csharp
/// <summary>
/// 获取 protoc 可执行文件完整路径。
/// </summary>
/// <returns>protoc 路径，不存在时返回 null。</returns>
public static string GetProtocPath()

/// <summary>
/// 获取 protoc include 目录完整路径。
/// </summary>
/// <returns>include 目录路径，不存在时返回 null。</returns>
public static string GetProtocIncludePath()

/// <summary>
/// 编译单个 .proto 文件为 C# 代码。
/// </summary>
/// <param name="protoFilePath">.proto 文件完整路径。</param>
/// <param name="protoRootDir">.proto 根目录（protoc -I 参数）。</param>
/// <param name="csharpOutDir">C# 输出目录。</param>
/// <returns>是否成功。</returns>
public static bool CompileSingle(string protoFilePath, string protoRootDir, string csharpOutDir)

/// <summary>
/// 编译目录下所有 .proto 文件为 C# 代码。
/// </summary>
/// <param name="protoRootDir">.proto 根目录。</param>
/// <param name="csharpOutDir">C# 输出目录。</param>
/// <returns>是否成功。</returns>
public static bool CompileAll(string protoRootDir, string csharpOutDir)

/// <summary>
/// 扫描目录获取所有 .proto 文件路径。
/// </summary>
/// <param name="directoryPath">目录路径。</param>
/// <returns>所有 .proto 文件的完整路径数组，目录不存在时返回空数组。</returns>
public static string[] GetProtoFiles(string directoryPath)

/// <summary>
/// 执行 Luban → protoc 闭环管线：Luban 从 Excel 生成 .proto → protoc 编译为 C#。
/// </summary>
/// <param name="lubanConfPath">luban.conf 文件路径。</param>
/// <param name="lubanTargetName">Luban target 名称。</param>
/// <param name="lubanCustomTemplateDirs">Luban 自定义模板目录列表（可为 null）。按优先级排序，Luban 依次查找。</param>
/// <param name="protoExportDir">Luban 生成 .proto 的输出目录（同时作为 protoc -I 根目录）。</param>
/// <param name="csharpOutDir">protoc 生成 C# 的输出目录。</param>
/// <returns>是否成功。</returns>
public static bool RunLubanProtoPipeline(string lubanConfPath, string lubanTargetName, string[] lubanCustomTemplateDirs, string protoExportDir, string csharpOutDir)
```

### 私有方法

| 方法 | 签名 | 说明 |
|---|---|---|
| `FixImportPaths` | `void FixImportPaths(string protoRootDir)` | 扫描根目录内的普通 import，按文件名查找唯一候选；需要修正时直接回写 `.proto` 源文件，具体限制见 §9 |
| `AppendIncludePaths` | `void AppendIncludePaths(StringBuilder args, string protoRootDir)` | 拼接 `-I` include 路径参数：Proto 根目录 + protoc 内置 include（来自 UPM 包） |
| `EnsureDirectoryExists` | `void EnsureDirectoryExists(string directoryPath)` | 若目录不存在则创建 |
| `RunProtoc` | `bool RunProtoc(string arguments)` | 执行 protoc 命令，创建 Process 并等待退出，返回 ExitCode == 0 |

---

## §7 线程模型

- 所有方法在编辑器主线程同步执行
- `RunProtoc` 通过 `ProcessRunner.RunSync` 调用 `protoc` 进程，阻塞直到进程退出或超时
- stdout/stderr 由 `ProcessRunner` 内部异步读取，调用方无需处理线程问题
- 不建议在大规模 .proto 场景下频繁调用，长时间阻塞会冻结 Unity 编辑器

---

## §9 关键算法

### GetProtocPath

1. 通过 `Path.GetFullPath("Packages/com.solotopia.luban")` 获取 UPM 包路径
2. 按 `Application.platform == RuntimePlatform.WindowsEditor` 选择 Mac 或 Win 的相对路径
3. 拼接完整路径，文件存在则返回；否则打印 Error 返回 null

### RunLubanProtoPipeline

1. 验证 `lubanConfPath` 非空且文件存在，否则打印 Error 并返回 false
2. 对非空的 `protoExportDir`、`csharpOutDir` 调用 `EnsureDirectoryExists`；未在此处统一校验其他参数
3. 调用 `Luban.CliRunner.RunProtoSchemaGen(lubanConfPath, lubanTargetName, protoExportDir, lubanCustomTemplateDirs)` — Luban 以 `-c protobuf3` 生成 .proto
4. 调用 `CompileAll(protoExportDir, csharpOutDir)` — protoc 批量编译所有生成的 .proto 为 C#
5. Luban 或 `CompileAll` 返回 false 时立即返回 false；两者返回 true 时返回 true。`CompileAll` 允许空目录，因此 true 不证明生成了协议代码

当前仓内 C# 源码未发现此方法的直接调用方。它是可供显式调用的组合 API；当前 `NetworkComponentInspector` 的“导出所有协议类型”按钮调用 `ProtoExporter.ExportAllProtos`，不会调用本管线。此方法不执行 `AssetDatabase.Refresh()`，也不回滚已生成或已修改的文件；目录和文件操作异常可向调用方传播。

### CompileSingle

1. `protoFilePath` 为空或文件不存在时打印 Error 并返回 false
2. 确保非空的 `csharpOutDir` 存在，然后调用 `FixImportPaths(protoRootDir)`；该修正扫描整个根目录，并非只修改当前文件
3. 以 `protoRootDir` 和包内可用的 include 目录构建 `-I`，以 `csharpOutDir` 构建 `--csharp_out`，编译指定文件
4. 返回 `RunProtoc` 的结果；源文件以外的根目录、输出参数未在此方法中完成统一校验，相关错误可能由文件操作或 protoc 报出

当前生产调用来自 `NetworkComponentInspector` 的单文件编译按钮和 `ProtoExporter.ExportAllProtos`。方法本身不刷新 AssetDatabase、不验证 Unity 编译结果，也不回滚产物。

### CompileAll

1. 验证 protoRootDir 目录存在
2. 递归扫描 `*.proto` 文件；无文件时打印 Warning 返回 true（空目录不视为错误）
3. 调用 `FixImportPaths` 尝试按唯一同名文件修正普通 import 路径，并可能回写源文件
4. 逐文件构建 protoc 参数：`-I"<protoRootDir>"` + 可选内置 include + `--csharp_out="<targetDir>"` + 文件路径，保留相对目录层级
5. 逐文件调用 `RunProtoc`，任意文件失败立即返回 false

当前生产调用方包含 `EditorUtil.Table.Exporter.CompileProtobuf`；`RunLubanProtoPipeline` 也组合调用此方法。`csharpOutDir` 是统一输出根目录，实际按源文件相对目录创建子目录。失败前已写入的文件保留，文件操作异常不转换为 false；方法不刷新 AssetDatabase。

### FixImportPaths

1. 根目录为空或不存在时直接返回；否则递归读取根目录中的 `*.proto`
2. 正则只匹配普通 `import "路径";` 行；`import public`、`import weak` 和带行尾注释的写法不在该匹配范围内
3. 取 import 的文件名，调用 `Util.SysIO.Directory.GetFiles(protoRootDir, fileName, AllDirectories)` 搜索；该封装委托 `System.IO.Directory.GetFiles`，没有显式忽略大小写的比较，匹配行为受平台和文件系统影响
4. 零个或多个候选时打印 Warning 并保留原语句；唯一候选时，将其相对根目录的路径转换为 `/`，与原路径按 `StringComparison.Ordinal` 比较，不同则替换
5. 文件内容有变化时直接以 UTF-8 回写原 `.proto` 文件。方法返回 void，不提供修正完成状态，读写异常可向调用方传播

此方法没有先检查原 import 路径是否有效，搜索也不使用原目录来消除同名歧义；不能保证跨平台修复所有大小写差异。protoc 的包内 include 目录不在候选搜索范围内，标准 import 可能触发“找不到”警告却仍被 protoc 正常解析。`CompileSingle` 和 `CompileAll` 会自动调用它，执行前应将根目录内的源文件回写纳入任务范围。

---

## §11 使用示例

```csharp
// 在 Inspector 中单独编译某个 .proto 文件
string protocPath = EditorUtil.Proto.CliRunner.GetProtocPath();
if (protocPath != null)
{
    bool ok = EditorUtil.Proto.CliRunner.CompileSingle(
        "/path/to/my.proto",
        "/path/to/proto/root",
        "/path/to/csharp/output"
    );
    if (ok) AssetDatabase.Refresh();
}

// 显式调用组合 API（Excel → .proto → C#）；当前 Inspector 不调用此方法
bool pipelineOk = EditorUtil.Proto.CliRunner.RunLubanProtoPipeline(
    "/project/excel/_configs/luban.conf",
    "proto",
    lubanCustomTemplateDirs: null,
    protoExportDir: "/project/proto",
    csharpOutDir: "/project/Assets/Scripts/Proto"
);
if (pipelineOk) AssetDatabase.Refresh();
```

> 常规 Network 协议导出使用 `NetworkComponentInspector` 的“导出所有协议类型”按钮，或 `export.network.proto` Pipify Step；二者调用 `ProtoExporter.ExportAllProtos`。自动化入口 `nova.project.network.export` 的 `proto` 目标也复用该导出器，其当前可调用性需由运行中的 MCP 确认。以上入口均不调用 `RunLubanProtoPipeline`。

---

## §13 关联文档

- [EditorUtil.Luban.CliRunner.md](../EditorUtil.Luban/EditorUtil.Luban.CliRunner.md)
- [NetworkComponentInspector.md](../../Inspectors/NetworkComponentInspector/NetworkComponentInspector.md)
- [ProtoSettings.md](../../../Runtime/Modules/Network/Definitions/ProtoSettings.md)
- [EditorUtil.md](../EditorUtil.md)

## 输入范围与结果解释

CompileSingle 的三个路径分别为目标协议、扫描/import 根、代码输出目录，不能互相代替；即使只编译一个文件，仍可能改写根下其他 proto 的 import。所有改写和前面已生成文件在后续失败时保留，不删除旧 C#，不自动 Refresh。CompileAll 空源返回 true；GetProtoFiles 缺目录与空目录同为空数组且不排序。

-I 参数总包含源根，可选包 include 缺失不单独阻断。参数构造没有对路径内引号等任意字符做转义。protoc 默认等待 120 秒，stderr 非空只告警，成功依据退出码和未超时，不核产物集合。当前路径选择仅 Windows 与其他平台两分，Linux 也选 Mac binary；存在性不证明可执行位、版本或架构匹配。
