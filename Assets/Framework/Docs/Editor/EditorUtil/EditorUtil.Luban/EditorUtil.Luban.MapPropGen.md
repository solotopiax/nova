# EditorUtil.Luban.MapPropGen

**类签名**：`public static class EditorUtil.Luban.MapPropGen`
**命名空间**：`NovaFramework.Editor`
**一行描述**：Map 模式属性生成器 — 为 TbXxx.cs 追加 partial class 属性访问器。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|------|------|
| `EditorUtil.Luban.MapPropGen.cs` | `EditorUtil.Luban.MapPropGen` | JSON 键值提取 + partial class 代码生成 + 旧生成块替换 |

---

## §3 继承关系

```
EditorUtil (public static partial class)
  └── Luban (public static partial class)
        └── MapPropGen (public static class)
              └── MapKeyEntry (private readonly struct)
```

> 统一了原 `LubanMapPropertyGenerator`（Table 模块）和 `LubanConfigMapPropertyGenerator`（Config 模块）的属性生成逻辑。

---

## §4 关键字段

| 字段 | 类型 | 修饰符 | 说明 |
|------|------|--------|------|
| `c_RegionBegin` | `string` | `private const` | 自动生成代码块起始标记：`"// --- AUTO-GENERATED MAP PROPERTIES BEGIN ---"` |
| `c_RegionEnd` | `string` | `private const` | 自动生成代码块结束标记：`"// --- AUTO-GENERATED MAP PROPERTIES END ---"` |
| `s_Utf8NoBom` | `UTF8Encoding` | `private static readonly` | UTF-8 无 BOM 编码 |

### 私有嵌套类型

```csharp
/// <summary>
/// Map 键条目（键值 + 注释描述）。
/// </summary>
private readonly struct MapKeyEntry
{
    /// <summary>
    /// 键值（将成为 C# 属性名）。
    /// </summary>
    public readonly string Key;

    /// <summary>
    /// 描述注释。
    /// </summary>
    public readonly string Desc;

    public MapKeyEntry(string key, string desc);
}
```

---

## §5 公开 API

```csharp
/// <summary>
/// 为所有 Map 模式的单元批量生成属性访问器。
/// </summary>
/// <param name="unitSettings">全部单元设置列表。</param>
/// <param name="topModule">顶层命名空间（如 "Game.Runtime"）。</param>
public static void GenerateAll(IReadOnlyList<IDataTableUnitSetting> unitSettings, string topModule)

/// <summary>
/// 为单个 Map 模式的单元生成属性访问器。
/// </summary>
/// <param name="unitSetting">单元设置。</param>
/// <param name="topModule">顶层命名空间。</param>
public static void GenerateForUnit(IDataTableUnitSetting unitSetting, string topModule)
```

### 私有方法

| 方法 | 签名 | 说明 |
|------|------|------|
| `ExtractMapKeys` | `List<MapKeyEntry> ExtractMapKeys(JArray dataArray, string indexField)` | 从 JSON 数据数组提取所有 Map 键（去重），同时读取 Desc 字段作为注释 |
| `AppendMapProperties` | `void AppendMapProperties(string filePath, string tableName, string dataTypeName, string topModule, List<MapKeyEntry> keys)` | 在 TbXxx.cs 末尾追加 partial class 属性块（先移除旧块） |
| `RemoveOldGeneratedBlock` | `string RemoveOldGeneratedBlock(string content)` | 移除 `c_RegionBegin` 到 `c_RegionEnd` 之间的内容 |
| `IsValidCSharpIdentifier` | `bool IsValidCSharpIdentifier(string name)` | 检查字符串是否为合法 C# 标识符（首字符为字母或下划线，后续字符为字母数字或下划线） |

---

## §9 关键算法

### GenerateForUnit 流程

```
GenerateForUnit(unitSetting, topModule)
  ├── Mode != Map → 跳过
  ├── 读取 JSON 文件（unitSetting.DatasExportPath）
  ├── 遍历 DataTypeNames（跳过空值和 # 开头）
  │     ├── 提取 sheetName（取最后一个 . 后部分）
  │     ├── 从 JSON 中取 rootJson[sheetName] 作为 JArray
  │     ├── ExtractMapKeys → 提取 indexField 字段值作为键，Desc 字段值作为注释
  │     └── AppendMapProperties → 写入 TbXxx.cs
  └── 完成
```

### AppendMapProperties 生成的代码结构

```csharp
// --- AUTO-GENERATED MAP PROPERTIES BEGIN ---

namespace Game.Runtime
{
public partial class TbSystemConfig
{
    /// <summary>
    /// 系统版本号
    /// </summary>
    public DtSystemConfig Version => GetOrDefault("Version");

    /// <summary>
    /// 服务器地址
    /// </summary>
    public DtSystemConfig ServerUrl => GetOrDefault("ServerUrl");

}
}

// --- AUTO-GENERATED MAP PROPERTIES END ---
```

- 每次生成前先调用 `RemoveOldGeneratedBlock` 移除旧块，确保幂等
- 非合法 C# 标识符的键（如数字开头）自动跳过
- IndexField 默认为 `"ID"`（当 `unitSetting.IndexField` 为空时）

---

## §11 使用示例

```csharp
// 为单个 Map 表生成属性
IDataTableUnitSetting unit = tableSettings.Units[0];
EditorUtil.Luban.MapPropGen.GenerateForUnit(unit, "Game.Runtime");

// 批量为所有 Map 表生成属性（自动跳过 List/One 模式）
EditorUtil.Luban.MapPropGen.GenerateAll(tableSettings.Units, "Game.Runtime");
```

> 通常不直接调用 MapPropGen，而是通过 `Pipeline.ExportData` / `Pipeline.ExportCode` / `Pipeline.ExportAll` 间接调用。

---

## §13 关联文档

- [EditorUtil.Luban.Pipeline.md](EditorUtil.Luban.Pipeline.md)
- [EditorUtil.Luban.JsonMerger.md](EditorUtil.Luban.JsonMerger.md)
- [EditorUtil.md](../EditorUtil.md)
- [IDataTableUnitSetting.md](../../../Runtime/Core/Table/IDataTableUnitSetting.md)
