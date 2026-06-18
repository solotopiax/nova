# EditorUtil.Excel

**类签名**：`public static class Excel`（嵌套于 `public static partial class EditorUtil`）
**命名空间**：`NovaFramework.Editor`

Excel 读写工具，基于 `ExcelDataReader` 读取（支持 .xlsx / .xls）和 `EPPlus` 写入，提供 Sheet 级别的行列字符串数据接口，用于各类预过滤流程的临时 Excel 生成与数据读取。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `EditorUtil/EditorUtil.Excel/EditorUtil.Excel.cs` | `EditorUtil.Excel`（嵌套静态类） | 全部方法定义，`ExcelDataReader` 读取 + `EPPlus` 写入 |

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

    /// 创建新 Excel 文件并写入多个 Sheet 数据（覆盖已有文件）
    /// 异常：filePath 为空抛 ArgumentException，sheets 为空抛 ArgumentException
    public static void Write(string filePath, Dictionary<string, List<IReadOnlyList<string>>> sheets);

    /// 创建新 Excel 文件，写入单个 Sheet（重载）
    /// 异常：sheetName 为空抛 ArgumentException
    public static void Write(string filePath, string sheetName, List<IReadOnlyList<string>> rows);

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
Write(filePath, sheets)
  ├── Util.SysIO.Directory.CreateIfNotExist(directory)          （ExcelPackage.LicenseContext 在静态构造器中已设置）
  ├── new ExcelPackage()
  ├── foreach (sheetName, rows) in sheets:
  │     WriteSheet(package, sheetName, rows)
  │       └── package.Workbook.Worksheets.Add(sheetName)
  │           EPPlus 行列索引从 1 开始，rowIndex+1 / colIndex+1
  └── package.SaveAs(new FileStream(filePath, Create, Write))
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

// 写入临时 Excel（预过滤输出）
var outputSheets = new Dictionary<string, List<IReadOnlyList<string>>>();
outputSheets["AdMob"] = new List<IReadOnlyList<string>>
{
    new List<string> { "AdMob" },
    new List<string> { "Name", "Value" },
    new List<string> { "string", "string" },
    new List<string> { "配置名", "值" },
    new List<string> { "AdMobAppId", "ca-app-pub-xxx" },
};
EditorUtil.Excel.Write("Assets/.../Temp/Configs_Global_AdMob.xlsx", outputSheets);

// 格式检查
bool valid = EditorUtil.Excel.IsExcelFile("Configs.xlsx");   // true
```

---

## §12 注意事项

- `ReadAllSheets` 使用 `FileShare.ReadWrite` 打开文件，允许在 Excel 应用程序打开时读取，但写入锁定的文件仍会失败
- `ExcelPackage.LicenseContext = NonCommercial` 在静态构造函数中设置，表明使用 EPPlus 的非商业许可；商业项目需配置正确许可
- 行列数据均以 `string` 存储（`cell.ToString()`），数值精度由 Excel 原始格式决定
- `IReadOnlyList<string>` 接口防止外部修改读取到的行数据，但不阻止 `List<string>` 强制转型

---

## §13 关联文档

- [EditorUtil.md](../EditorUtil.md)（EditorUtil 静态工具类概览）
- [DataPipeline.md](../../DataPipeline/DataPipeline.md)（预过滤总览）
- [ConfigComponentInspector.md](../../Inspectors/ConfigComponentInspector/ConfigComponentInspector.md)（调用方：读取 Sheet 名填充 DataTypeNames）
