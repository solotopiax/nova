# EditorUtil.Luban.DataTypeNameHelper

**类签名**：`public static class DataTypeNameHelper`（`EditorUtil.Luban` 嵌套静态类）
**命名空间**：`NovaFramework.Editor`
**全局访问**：`EditorUtil.Luban.DataTypeNameHelper`

数据类型名称刷新工具：读取数据源文件提取有效 Sheet 名称并填充 DataTypeNames。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `EditorUtil.Luban.DataTypeNameHelper.cs` | `EditorUtil.Luban.DataTypeNameHelper` | 全部方法：单文件刷新、全量刷新、Sheet 名称填充（两个重载） |

---

## §3 继承关系

```
EditorUtil (static partial)
  └── EditorUtil.Luban (static partial)
        └── EditorUtil.Luban.DataTypeNameHelper (static)
```

---

## §4 关键字段表

此类仅包含静态方法，无字段。

---

## §5 完整公开 API

```csharp
// 读取单个数据源文件，提取有效 Sheet 名称并填充 DataTypeNames，最后调用 ApplyModifiedProperties
// 默认使用 EditorUtil.Excel.ReadAllSheets（多文件合并模式）
public static void DoRefreshDataTypeNames(
    string filePath,
    SerializedProperty dataTypeNamesProp,
    SerializedObject serializedObject,
    int minHeaderRowCount = 5)

// 遍历所有已配置的数据源文件，依次刷新每个条目的 DataTypeNames
// doRefreshSingle 为 null 时使用 DoRefreshSingleDataTypeNames 默认实现
public static void DoRefreshAllDataTypeNames(
    string directoryPath,
    SerializedProperty sourceUnitsSettingsProperty,
    SerializedObject serializedObject,
    int minHeaderRowCount = 5,
    Action<string, SerializedProperty> doRefreshSingle = null)

// 刷新单个文件的 DataTypeNames（供循环内调用，不额外 ApplyModifiedProperties）
public static void DoRefreshSingleDataTypeNames(
    string fullPath,
    SerializedProperty dataTypeNamesProp,
    int minHeaderRowCount = 5)

// 根据 Sheet 字典（List 版本）填充 DataTypeNames 属性
// 筛选条件：非 # 开头且行数 >= minHeaderRowCount 的有效 Sheet
public static void PopulateDataTypeNames(
    Dictionary<string, List<IReadOnlyList<string>>> sheets,
    SerializedProperty dataTypeNamesProp,
    int minHeaderRowCount)

// 根据 Sheet 字典（IReadOnlyList 版本）填充 DataTypeNames 属性
// Table 模块通过 EditorUtil.Excel 读取后的 RawContent 为此类型
public static void PopulateDataTypeNames(
    Dictionary<string, IReadOnlyList<IReadOnlyList<string>>> sheets,
    SerializedProperty dataTypeNamesProp,
    int minHeaderRowCount)
```

---

## §9 关键算法

### DoRefreshAllDataTypeNames 流程

```
DoRefreshAllDataTypeNames(directoryPath, unitsProp, serializedObject, ...)
  ├── rootDir = directoryPath.Replace('\\', '/').TrimEnd('/')
  ├── for i in [0, unitsProp.arraySize)：
  │     elem = unitsProp.GetArrayElementAtIndex(i)
  │     sourcePathProp = elem.FindPropertyRelative("SourcePath")
  │     dataTypeNamesProp = elem.FindPropertyRelative("DataTypeNames")
  │     跳过：SourcePath 为空 / 文件不存在 / dataTypeNamesProp 为 null
  │     fullPath = Path.Combine(rootDir, sourcePathProp.stringValue)
  │     doRefreshSingle != null ? doRefreshSingle(fullPath, dataTypeNamesProp)
  │                             : DoRefreshSingleDataTypeNames(fullPath, dataTypeNamesProp, minHeaderRowCount)
  └── serializedObject.ApplyModifiedProperties()
```

### PopulateDataTypeNames 筛选逻辑

- Sheet 名不以 `#` 开头
- Sheet 行数 `>= minHeaderRowCount`（默认 5，用于过滤无效/注释 Sheet）
- 先 `ClearArray()` 清空旧值，再按顺序写入有效 Sheet 名

---

## §10 常见误区

| 误区 | 正确做法 |
|------|----------|
| 导出前不刷新 DataTypeNames，导致生成文件名集合为空 | 在导出按钮回调中先调用 `DoRefreshDataTypeNames` 或 `DoRefreshAllDataTypeNames` |
| `DoRefreshDataTypeNames` 与 `DoRefreshSingleDataTypeNames` 混用 | 独立单文件入口用 `DoRefreshDataTypeNames`（含 Apply）；循环内批量用 `DoRefreshSingleDataTypeNames`（不 Apply），最后统一 Apply |

---

## §11 使用示例

```csharp
// 单文件导出前刷新该文件的 DataTypeNames
void DoExportDataForFile(string filePath, string dataExportPath, SerializedProperty detailProp)
{
    SerializedProperty dataTypeNamesProp = detailProp?.FindPropertyRelative("DataTypeNames");
    EditorUtil.Luban.DataTypeNameHelper.DoRefreshDataTypeNames(
        filePath, dataTypeNamesProp, serializedObject);

    // ... 执行实际导出
}

// 全量导出前刷新所有文件的 DataTypeNames
void DoExportAll()
{
    EditorUtil.Luban.DataTypeNameHelper.DoRefreshAllDataTypeNames(
        m_SourceDirPath.stringValue,
        m_UnitsSettings,
        serializedObject);

    // ... 执行全量导出
}
```

---

## §13 关联文档

- [EditorUtil.Luban.ExportHelper.md](EditorUtil.Luban.ExportHelper.md)
- [EditorUtil.Luban.Pipeline.md](EditorUtil.Luban.Pipeline.md)
- [EditorUtil.Excel.md](../EditorUtil.Excel/EditorUtil.Excel.md)
- [EditorUtil.Draw.SourceFileTree.md](../EditorUtil.Draw/EditorUtil.Draw.SourceFileTree.md)
