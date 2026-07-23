# EditorUtil.Luban.ConfigSyncer

**类签名**：`public static class EditorUtil.Luban.ConfigSyncer`
**命名空间**：`NovaFramework.Editor`
**一行描述**：Luban 配置同步器 — 管理 `_configs/`，在导出前生成 schema manifest、`luban.conf` 和 `__tables__.xml`。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|------|------|
| `EditorUtil.Luban.ConfigSyncer.cs` | `EditorUtil.Luban.ConfigSyncer` | `_configs/` 目录管理 + `luban.conf` 读写 + 从 manifest 生成 `__tables__.xml` |
| `EditorUtil.Luban.SchemaManifest.cs` | manifest 模型、校验、存储和构建器 | 扫描 Excel，生成并原子保存本次导出的结构快照 |

---

## §3 继承关系

```
EditorUtil (public static partial class)
  └── Luban (public static partial class)
        └── ConfigSyncer (public static class)
```

> 统一了原 `LubanConfigManager`（Table 模块）和 `LubanConfigConfigManager`（Config 模块）的配置同步逻辑，通过 `IDataTableSettings` / `IDataTableUnitSetting` 接口实现参数化。

---

## §4 关键字段

| 字段 | 类型 | 修饰符 | 说明 |
|------|------|--------|------|
| `s_Utf8NoBom` | `System.Text.UTF8Encoding` | `private static readonly` | UTF-8 无 BOM 编码（避免 Luban CLI 解析 JSON/XML 时因 BOM 出错） |
| `c_ConfigsDirName` | `string` | `private const` | `"_configs"` |
| `c_LubanConfFileName` | `string` | `internal const` | `"luban.conf"` |
| `c_TablesXmlFileName` | `string` | `internal const` | `"__tables__.xml"` |

---

## §5 公开 API

```csharp
/// <summary>
/// 获取 _configs/ 目录完整路径。
/// </summary>
/// <param name="sourceDirPath">数据源目录路径。</param>
/// <returns>_configs/ 目录完整路径。</returns>
public static string GetConfigDirPath(string sourceDirPath)

/// <summary>
/// 检查 _configs/ 目录是否存在。
/// </summary>
/// <param name="sourceDirPath">数据源目录路径。</param>
/// <returns>是否存在。</returns>
public static bool IsConfigDirExists(string sourceDirPath)

/// <summary>
/// 清理指定临时目录。
/// </summary>
/// <param name="tempDirPath">临时目录完整路径。</param>
public static void CleanTempDir(string tempDirPath)
```

### 私有方法

| 方法 | 签名 | 说明 |
|------|------|------|
| `WriteDefaultLubanConf` | `void WriteDefaultLubanConf(string path, string targetName, string managerName, string topModule)` | 写入默认 luban.conf（dataDir=".."，schemaFiles 指向 __tables__.xml） |
| `UpdateLubanConfTopModule` | `void UpdateLubanConfTopModule(string confPath, string targetName, string managerName, string topModule)` | 更新 luban.conf 中 targets[0] 的 name/manager/topModule |
| `WriteEmptyTablesXml` | `void WriteEmptyTablesXml(string path)` | 写入空的 `<module/>` XML 文件 |
| `GenerateTablesXml` | `void GenerateTablesXml(string xmlPath, LubanSchemaManifest manifest)` | 从已验证的 manifest 快照生成完整 `__tables__.xml` |

---

## §9 关键算法

### GenerateTablesXml

遍历 manifest 的 `units[].tables`，为每个扫描出的表生成 `<table>` 元素：

1. 校验并标准化完整 manifest。
2. 不读取旧 XML；输出完全由当前 manifest 重建，避免生成缓存混入不可追踪的手写定义。
3. 使用 unit 的 `lubanInputPath`、`mode` 和 `indexField`，不再读取 Runtime 单元中的 Sheet 名列表。
4. 生成 `<table>` 元素：
   - `name="{table.name}"`
   - `value="{table.valueType}"`
   - Excel 输入为 `input="{table.valueType}@{unit.lubanInputPath}"`
   - 非 Excel 输入为 `input="{unit.lubanInputPath}/{table.valueType}.csv"`
   - `mode="{mode}"`
   - Map 模式且 IndexField 非空时追加 `index="{indexField}"`
   - `readSchemaFromFile="true"`
   - `comment="{table.valueType}"`

### SyncFromInspector 流程

```
SyncFromInspector
  ├── RuntimeProvider.GetNamespace() 取 topModule（从 AssetDatabase 读取 ConfigRuntimeSO.Common.Namespace）
  ├── _configs/ 不存在 → InitializeConfigDir
  ├── UpdateLubanConfTopModule（更新 luban.conf）
  ├── LubanSchemaManifestBuilder.Build
  │     ├── 默认扫描当前 Profile 的所有 Excel；提供 scanValueTypes 时扫描模块已投影的实际输入
  │     ├── 在内存中构建并验证完整快照
  │     └── 原子保存 _configs/nova-export-manifest.json
  ├── GenerateTablesXml（只读取该快照）
  └── 返回 LubanSchemaManifest 给 Pipeline 后续阶段复用
```

---

## §10 常见误区

| 误区 | 说明 |
|------|------|
| luban.conf 的 dataDir | 配置为 `".."`（即 _configs 的父目录 = sourceDirPath），使 __tables__.xml 中的路径相对于 sourceDirPath 解析 |
| 编码问题 | 所有文件写入使用 `s_Utf8NoBom`（UTF-8 无 BOM），避免 Luban CLI 解析失败 |
| 扫描失败 | 缺失或不可读的源文件会中止同步；旧 manifest 和旧 `__tables__.xml` 保持不变，Luban 不会被调用 |
| 预处理后的类型名 | 模块若改变了 Sheet/CSV 名，必须通过 Pipeline 上下文提供 `SchemaValueTypeScanner`；否则默认扫描原始 Excel，manifest 会与实际输入不一致 |
| manifest 归属 | `_configs/nova-export-manifest.json` 是 Editor-only、可由 Excel 重建的派生文件；整个 `_configs/` 被忽略且不承载手写定义 |
| 是否删除 `_configs/` | 可以手工删除并在下次导出时重建，但日常应保留：`luban.conf` / `__tables__.xml` 是 CLI 输入，manifest 还供 Table Inspector 诊断读取 |
| 陈旧缓存 | 每次 Pipeline 调用 Luban 前都会重新扫描 Excel 并原子替换 XML 与 manifest，旧缓存不会直接参与新一轮导出 |

---

## §11 使用示例

```csharp
// Inspector 只需要判断并监听本地工作目录；创建和同步由 Pipeline 内部完成。
bool initialized = EditorUtil.Luban.ConfigSyncer.IsConfigDirExists(sourceDirPath);
string configDir = EditorUtil.Luban.ConfigSyncer.GetConfigDirPath(sourceDirPath);
```

> 通常不直接调用 ConfigSyncer，而是通过 `Pipeline.ExportData` / `Pipeline.ExportCode` / `Pipeline.ExportAll` 间接调用。

---

## §13 关联文档

- [EditorUtil.Luban.Pipeline.md](EditorUtil.Luban.Pipeline.md)
- [EditorUtil.Luban.SchemaManifest.md](EditorUtil.Luban.SchemaManifest.md)
- [EditorUtil.Luban.CliRunner.md](EditorUtil.Luban.CliRunner.md)
- [EditorUtil.Luban.JsonMerger.md](EditorUtil.Luban.JsonMerger.md)
- [EditorUtil.Config.RuntimeProvider.md](../EditorUtil.Config/EditorUtil.Config.RuntimeProvider.md)
- [EditorUtil.md](../EditorUtil.md)
- [IDataTableSettings.md](../../../Runtime/Core/Table/IDataTableSettings.md)
- [IDataTableUnitSetting.md](../../../Runtime/Core/Table/IDataTableUnitSetting.md)
