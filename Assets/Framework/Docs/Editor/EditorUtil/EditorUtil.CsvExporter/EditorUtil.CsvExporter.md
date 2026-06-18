# EditorUtil.CsvExporter

**类签名**：`public static class EditorUtil.CsvExporter`
**命名空间**：`NovaFramework.Editor`

将结构化数据（二维数组 / DataTable）导出为 CSV 文件，供调试或外部工具分析使用。

---

## 文件

| 文件 | 说明 |
|------|------|
| `EditorUtil.CsvExporter.cs` | 导出方法实现 |

---

## 完整 API 签名

```csharp
// 将二维数组（行×列）导出为 CSV 文件
// data：string[,] 行列数据
// outputPath：输出文件路径（绝对路径，如 "C:/output/hero.csv"）
void EditorUtil.CsvExporter.Export(string[,] data, string outputPath)

// 将 DataTable 导出为 CSV 文件（含表头行）
void EditorUtil.CsvExporter.Export(DataTable table, string outputPath)
```

---

## 使用示例

```csharp
// 导出运行时对象池状态快照为 CSV（调试用）
var infos = Nova.ObjectPool.GetAllObjectPoolInfos();
var rows = new string[infos.Length + 1, 4];
rows[0, 0] = "Name"; rows[0, 1] = "Count"; rows[0, 2] = "Capacity"; rows[0, 3] = "ExpireTime";
for (int i = 0; i < infos.Length; i++)
{
    rows[i + 1, 0] = infos[i].Name;
    rows[i + 1, 1] = infos[i].Count.ToString();
    rows[i + 1, 2] = infos[i].Capacity.ToString();
    rows[i + 1, 3] = infos[i].ExpireTime.ToString();
}
EditorUtil.CsvExporter.Export(rows, "C:/debug/object_pools.csv");
```

---

## 关联文档

- [EditorUtil.md](../EditorUtil.md)
- [EditorUtil.FileSystem.md](../EditorUtil.FileSystem/EditorUtil.FileSystem.md)
