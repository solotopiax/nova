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
| `c_ProtocRelPathMac` | `string` | `private const` | protoc 在 UPM 包内的相对路径（Mac）：`"Tools~/protoc/bin/protoc"` |
| `c_ProtocRelPathWin` | `string` | `private const` | protoc 在 UPM 包内的相对路径（Windows）：`"Tools~/protoc/bin/protoc.exe"` |
| `c_ProtocIncludeRelPath` | `string` | `private const` | protoc include 目录在 UPM 包内的相对路径：`"Tools~/protoc/include"` |
| `c_SearchPattern` | `string` | `public const` | .proto 文件搜索模式：`"*.proto"` |

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

1. 验证 `lubanConfPath` 存在，不存在返回 false
2. 调用 `EnsureDirectoryExists` 确保 protoExportDir 和 csharpOutDir 存在
3. 调用 `Luban.CliRunner.RunProtoSchemaGen(lubanConfPath, lubanTargetName, protoExportDir, lubanCustomTemplateDirs)` — Luban 以 `-c protobuf3` 生成 .proto
4. 调用 `CompileAll(protoExportDir, csharpOutDir)` — protoc 批量编译所有生成的 .proto 为 C#
5. 全流程成功返回 true

### CompileAll

1. 验证 protoRootDir 目录存在
2. 递归扫描 `*.proto` 文件；无文件时打印 Warning 返回 true（空目录不视为错误）
3. 调用 `FixImportPaths` 修正 import 路径大小写
4. 逐文件构建 protoc 参数：`-I"<protoRootDir>"` + 可选内置 include + `--csharp_out="<targetDir>"` + 文件路径，保留相对目录层级
5. 逐文件调用 `RunProtoc`，任意文件失败立即返回 false

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

// 使用 Luban 闭环管线（Excel → .proto → C#）
bool pipelineOk = EditorUtil.Proto.CliRunner.RunLubanProtoPipeline(
    "/project/excel/_configs/luban.conf",
    "proto",
    lubanCustomTemplateDirs: null,
    protoExportDir: "/project/proto",
    csharpOutDir: "/project/Assets/Scripts/Proto"
);
if (pipelineOk) AssetDatabase.Refresh();
```

> 通常通过 `NetworkComponentInspector` 中的"Luban 生成 Proto → 编译 C#"按钮间接调用，无需手动调用。

---

## §13 关联文档

- [EditorUtil.Luban.CliRunner.md](../EditorUtil.Luban/EditorUtil.Luban.CliRunner.md)
- [NetworkComponentInspector.md](../../Inspectors/NetworkComponentInspector/NetworkComponentInspector.md)
- [ProtoSettings.md](../../../Runtime/Modules/Network/Definitions/ProtoSettings.md)
- [EditorUtil.md](../EditorUtil.md)
