# EditorUtil.Excel

**类签名**：`public static class Excel`（嵌套于 `public static partial class EditorUtil`）
**命名空间**：`NovaFramework.Editor`

表格数据读取与 CSV 写出工具：使用 `ExcelDataReader` 读取 .xlsx / .xls 的 Sheet，两个 `Write` 重载则将行列字符串写为 CSV 文件，供预过滤流程使用。本类不创建或修改 .xlsx 工作簿，也不写入样式或公式元数据。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `EditorUtil/EditorUtil.Excel/EditorUtil.Excel.cs` | `EditorUtil.Excel`（嵌套静态类） | 全部方法定义，`ExcelDataReader` 读取 .xlsx / .xls + UTF-8 CSV 写出 |

---

## §3 继承关系

```
EditorUtil (public static partial class)
  └── Excel (public static class)    嵌套静态类
```

---

## §4 关键字段表

| 字段 | 类型 | 值 | 说明 |
|------|------|----|------|
| `s_ValidExtensions` | `private static readonly HashSet<string>` | `{ ".xlsx", ".xls" }` | 有效 Excel 文件扩展名集合（忽略大小写） |
| `c_SearchPattern` | `public const string` | `"*.xlsx"` | Excel 源文件搜索模式（供 PreFilter 使用） |
| `c_ExcludePrefix` | `public const string` | `"~$"` | 需要排除的 Excel 临时文件前缀（Office 打开时生成） |

---

## §5 完整公开 API

```csharp
public static class Excel
{
    /// 读取 Excel 文件所有 Sheet 数据
    /// 返回 Sheet 名 → 行列字符串数据；Sheet 名为空或无行数据时跳过
    /// 异常：文件路径为空抛 ArgumentException，文件不存在抛 FileNotFoundException
    public static Dictionary<string, List<IReadOnlyList<string>>> ReadAllSheets(string filePath);

    /// 读取 Excel 文件指定 Sheet 数据
    /// 异常：sheetName 为空抛 ArgumentException，Sheet 不存在抛 KeyNotFoundException
    public static List<IReadOnlyList<string>> ReadSheet(string filePath, string sheetName);

    /// 获取 Excel 文件的所有 Sheet 名称列表
    /// 异常：文件路径为空抛 ArgumentException，文件不存在抛 FileNotFoundException
    public static List<string> GetSheetNames(string filePath);

    /// 将多个 Sheet 分别写入 {dirPath}/{sheetName}.csv，覆盖同名 CSV
    /// 异常：dirPath 为空、sheets 为 null 或无项时抛 ArgumentException
    public static void Write(string dirPath, Dictionary<string, List<IReadOnlyList<string>>> sheets);

    /// 写入单个 {dirPath}/{sheetName}.csv，复用多 Sheet 重载
    /// 异常：sheetName 为空抛 ArgumentException；目录/数据/IO 失败沿用委托调用
    public static void Write(string dirPath, string sheetName, List<IReadOnlyList<string>> rows);

    /// 检查文件是否为有效 Excel 格式（.xlsx / .xls，忽略大小写）
    public static bool IsExcelFile(string filePath);
}
```

---

## §9 关键算法

### ReadAllSheets 读取流程

```
ReadAllSheets(filePath)
  ├── 参数校验：路径非空，文件存在
  ├── Util.SysIO.File.Open(filePath, Open, Read, ReadWrite)   （共享读，允许 Excel 打开时读取）
  ├── ExcelReaderFactory.CreateReader(stream)
  ├── reader.AsDataSet({ UseHeaderRow = false })               （不自动使用第一行为列名）
  └── foreach DataTable in dataSet.Tables:
        跳过：TableName 为空 / Rows.Count == 0
        ConvertTableToRows：每行 → List<string>（cell.ToString()，null 转空字符串）
```

### Write 写入流程

```
Write(dirPath, sheets)
  ├── 参数校验：目录非空，sheets 非 null 且至少一项
  ├── Util.SysIO.Directory.CreateIfNotExist(dirPath)
  └── foreach (sheetName, rows) in sheets:
        WriteCsvFile(Path.Combine(dirPath, sheetName + ".csv"), rows)
          ├── null 单元格转空字符串
          ├── 含逗号/双引号/换行的单元格加双引号，内部双引号写为两个
          ├── 行内以逗号分隔，每行追加换行
          └── Util.SysIO.File.WriteAllTextSync(..., Encoding.UTF8)
```

---

## §11 使用示例

```csharp
// 读取所有 Sheet
Dictionary<string, List<IReadOnlyList<string>>> allSheets =
    EditorUtil.Excel.ReadAllSheets("Assets/Game/Configs/Global/Configs_Global_AdMob.xlsx");

foreach (var kvp in allSheets)
{
    string sheetName = kvp.Key;   // 如 "AdMob"
    var rows = kvp.Value;         // 行列字符串数据
    // rows[0] 为第 0 行（Sheet 标题行）
    // rows[1] 为列名行
}

// 读取指定 Sheet
var adMobRows = EditorUtil.Excel.ReadSheet(filePath, "AdMob");

// 获取 Sheet 名称列表
var names = EditorUtil.Excel.GetSheetNames(filePath);

// 写入预过滤 CSV：下面会生成 Assets/Game/Temp/AdMob.csv
var outputSheets = new Dictionary<string, List<IReadOnlyList<string>>>();
outputSheets["AdMob"] = new List<IReadOnlyList<string>>
{
    new List<string> { "AdMob" },
    new List<string> { "Name", "Value" },
    new List<string> { "string", "string" },
    new List<string> { "配置名", "值" },
    new List<string> { "AdMobAppId", "ca-app-pub-xxx" },
};
EditorUtil.Excel.Write("Assets/Game/Temp", outputSheets);

// 格式检查
bool valid = EditorUtil.Excel.IsExcelFile("Configs.xlsx");   // true
```

---

## §12 注意事项

- `ReadAllSheets` 使用 `FileShare.ReadWrite` 打开文件，允许在 Excel 应用程序打开时读取，但写入锁定的文件仍会失败
- `Write` 的首参数是输出目录，不能传 .xlsx 文件路径；每个 Sheet 名直接作为 CSV 文件名。多个文件逐一写入，中途失败不会回滚已写文件；调用方需保证文件名与行集合有效。
- `IsExcelFile` 只检查扩展名，不读取文件内容；本类读取入口不支持将上述 CSV 作为 Excel 工作簿读取。
- 行列数据均以 `string` 存储（`cell.ToString()`），数值精度由 Excel 原始格式决定
- `IReadOnlyList<string>` 接口防止外部修改读取到的行数据，但不阻止 `List<string>` 强制转型

---

## §13 关联文档

- [EditorUtil.md](../EditorUtil.md)（EditorUtil 静态工具类概览）
- [DataPipeline.md](../../DataPipeline/DataPipeline.md)（预过滤总览）
- [EditorUtil.Luban.DataTypeNameHelper.md](../EditorUtil.Luban/EditorUtil.Luban.DataTypeNameHelper.md)（调用方：导出前读取并筛选 Sheet 名）

## 调用与失败边界

读取使用 FileShare.ReadWrite，UseHeaderRow=false，保持原行列顺序并把所有单元格转字符串；不保留公式类型、格式或样式。ReadSheet 按 Ordinal 精确名称选择，缺 Sheet/无数据抛 KeyNotFoundException；IsExcelFile 只认 .xls/.xlsx 后缀，不证明文件存在、可读，也不排除 ~$ 临时文件。

Write 的输出是一个 Sheet 对应一个 CSV，不能作为权威 xlsx 编辑器。多 Sheet 逐文件覆盖没有总事务，Sheet 名直接用于输出文件名，不负责路径安全；null 行/枚举/IO 错误可能留部分结果。CSV 写入按单元格做 RFC4180 引号转义，但不能借此保留原工作簿元数据或样式。
