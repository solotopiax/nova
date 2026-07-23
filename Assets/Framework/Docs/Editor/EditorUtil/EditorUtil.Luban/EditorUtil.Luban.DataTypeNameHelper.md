# EditorUtil.Luban.DataTypeNameHelper

**类签名**：`internal static class DataTypeNameHelper`（嵌套于 `EditorUtil.Luban`）
**命名空间**：`NovaFramework.Editor`

Editor-only 的纯 Excel Sheet 扫描器。它读取一个数据源文件并返回有效值类型名称，不读取或修改 `SerializedProperty`，也不负责保存 Inspector 数据。

---

## 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `EditorUtil.Luban.DataTypeNameHelper.cs` | `EditorUtil.Luban.DataTypeNameHelper` | 读取 Excel、筛选有效 Sheet，并返回稳定顺序的名称列表 |

---

## Internal API

```csharp
internal static IReadOnlyList<string> ScanValueTypes(
    string filePath,
    int minHeaderRowCount)

internal static IReadOnlyList<string> ExtractValueTypes(
    IEnumerable<KeyValuePair<string, IReadOnlyList<IReadOnlyList<string>>>> sheets,
    int minHeaderRowCount)
```

`ScanValueTypes` 调用 `EditorUtil.Excel.ReadAllSheets(filePath)`，再把结果交给 `ExtractValueTypes`。返回顺序与读取到的 Sheet 顺序一致。

---

## 筛选规则

Sheet 同时满足以下条件才会进入结果：

1. 名称非空。
2. 名称不以 `#` 开头。
3. Sheet 内容不为 `null`。
4. 行数不少于调用方传入的 `minHeaderRowCount`。

Table Profile 传入 4，其他内置 Profile 默认传入 5。扫描器不拼接 `Tb` 前缀；`LubanSchemaManifestBuilder` 负责把值类型投影为 manifest 中的 `tables[].name` 和 `tables[].valueType`。

---

## 失败行为

- 文件不存在：抛出带文件路径的 `FileNotFoundException`。
- Excel 无法读取或返回异常数据：抛出带文件路径上下文的 `InvalidDataException`。
- `sheets` 为 `null` 或最小行数小于 0：抛出参数异常。

异常会中止 manifest 构建，因此 Pipeline 不会继续调用 Luban。扫描器不会用空结果掩盖缺失或损坏的源文件。

---

## 调用关系

```text
Pipeline.SyncSchema
  -> ConfigSyncer.SyncFromInspector
  -> LubanSchemaManifestBuilder.Build
  -> DataTypeNameHelper.ScanValueTypes
  -> EditorUtil.Excel.ReadAllSheets
  -> DataTypeNameHelper.ExtractValueTypes
```

业务 Inspector 无需直接调用本类；点击导出后由 Pipeline 统一扫描一次。

---

## 关联文档

- [EditorUtil.Luban.SchemaManifest.md](EditorUtil.Luban.SchemaManifest.md)
- [EditorUtil.Luban.ConfigSyncer.md](EditorUtil.Luban.ConfigSyncer.md)
- [EditorUtil.Luban.Pipeline.md](EditorUtil.Luban.Pipeline.md)
- [EditorUtil.Excel.md](../EditorUtil.Excel/EditorUtil.Excel.md)
