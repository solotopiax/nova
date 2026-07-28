# EditorUtil.Luban.JsonMerger

**类签名**：`public static class EditorUtil.Luban.JsonMerger`
**命名空间**：`NovaFramework.Editor`
**一行描述**：Luban JSON 合并器 — 将 Luban 按表导出的 JSON 合并为按 Excel 文件的 Nova 格式 JSON。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|------|------|
| `EditorUtil.Luban.JsonMerger.cs` | `EditorUtil.Luban.JsonMerger` | __tables__.xml 解析 + JSON 文件读取合并 + 输出写入 |

---

## §3 继承关系

```
EditorUtil (public static partial class)
  └── Luban (public static partial class)
        └── JsonMerger (public static class)
```

> 该工具保留给仍采用聚合 JSON 的专用导出链；Table 已改为直接发布 Luban 原始单表文件。

---

## §4 关键字段

无公开或静态字段。所有状态均为方法局部变量。

---

## §5 Internal API

```csharp
/// <summary>
/// 合并指定 Excel 文件的 Luban 导出 JSON。
/// </summary>
/// <param name="lubanOutputDir">Luban 临时输出目录。</param>
/// <param name="tablesXmlPath">__tables__.xml 文件路径。</param>
/// <param name="unit">本次 manifest 中的目标单元。</param>
/// <returns>是否成功。</returns>
internal static bool MergeForUnit(string lubanOutputDir, string tablesXmlPath, LubanSchemaUnit unit, Dictionary<string, int> deferredResults = null)

/// <summary>
/// 批量合并所有 Excel 文件的 Luban 导出 JSON。
/// </summary>
/// <param name="lubanOutputDir">Luban 临时输出目录。</param>
/// <param name="tablesXmlPath">__tables__.xml 文件路径。</param>
/// <param name="manifest">本次导出的完整 manifest 快照。</param>
/// <returns>是否全部成功。</returns>
internal static bool MergeAll(string lubanOutputDir, string tablesXmlPath, LubanSchemaManifest manifest, Dictionary<string, int> deferredResults = null)
```

### 私有方法

| 方法 | 签名 | 说明 |
|------|------|------|
| `ParseTablesXmlForUnit` | `Dictionary<string, string> ParseTablesXmlForUnit(string tablesXmlPath, string unitLubanInputPath)` | 解析 __tables__.xml，提取指定单元关联的表名到 Sheet 名映射 |
| `BuildMergedJson` | `JObject BuildMergedJson(string lubanOutputDir, Dictionary<string, string> tableToSheet)` | 读取各表 JSON 并合并为单个 JObject |
| `NormalizePath` | `string NormalizePath(string path)` | 规范化路径（Trim + 反斜杠转正斜杠） |

---

## §9 关键算法

若 manifest/XML 声明的任一 Luban 中间 JSON 缺失，合并立即失败，不会写出部分或空的最终 JSON。

### ParseTablesXmlForUnit — 表名匹配

1. 加载 __tables__.xml，遍历所有 `<table>` 节点
2. 对每个节点，解析 `input` 属性：`@` 前为 sheetName，`@` 后为 filePart
3. 将 filePart 与 `unitLubanInputPath` 做规范化路径比较（统一正斜杠后字符串相等）
4. 匹配成功时收集 `name` 属性（如 `"TbTestAAA"`）→ sheetName（如 `"TestAAA"`）

> Table 模块的 `LubanInputPath` 等于 `SourcePath`（完整相对路径），Config 模块的 `LubanInputPath` 带 `_temp/` 前缀。统一用文件名匹配可同时兼容两种格式。

### BuildMergedJson — 文件合并

1. 遍历 `tableToSheet` 映射，拼接文件名：`{tableName小写}.json`
2. 从 `lubanOutputDir` 读取对应 JSON 文件内容
3. 解析为 `JToken`，以原始 Sheet 名（保持大小写）为 key 写入合并后的 `JObject`

### MergeForUnit 完整流程

```
MergeForUnit(lubanOutputDir, tablesXmlPath, unit)
  ├── ParseTablesXmlForUnit → tableToSheet 映射
  ├── 映射为空 → 返回 true（无需合并）
  ├── BuildMergedJson → 合并 JObject
  ├── 确保输出目录存在
  └── 写入 unit.DatasExportPath（UTF-8，Indented 格式）
```

---

## §11 使用示例

```csharp
// 合并单个 Excel 文件的导出数据
string lubanTempDir = "/path/to/_luban_temp_abc12345";
string tablesXmlPath = "/path/to/_configs/__tables__.xml";
LubanSchemaUnit unit = ctx.SchemaManifest.ResolveUnit(ctx.TargetUnit.SourcePath);

bool success = EditorUtil.Luban.JsonMerger.MergeForUnit(lubanTempDir, tablesXmlPath, unit);

// 批量合并所有 Excel 文件
bool allSuccess = EditorUtil.Luban.JsonMerger.MergeAll(lubanTempDir, tablesXmlPath, ctx.SchemaManifest);
```

> 通常不直接调用 JsonMerger，而是通过 `Pipeline.ExportData` / `Pipeline.ExportAll` 间接调用。

---

## §13 关联文档

- [EditorUtil.Luban.Pipeline.md](EditorUtil.Luban.Pipeline.md)
- [EditorUtil.Luban.ConfigSyncer.md](EditorUtil.Luban.ConfigSyncer.md)
- [EditorUtil.md](../EditorUtil.md)
- [EditorUtil.Luban.SchemaManifest.md](EditorUtil.Luban.SchemaManifest.md)
