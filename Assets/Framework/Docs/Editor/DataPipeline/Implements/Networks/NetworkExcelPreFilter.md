# NetworkExcelPreFilter

**类签名**：`internal static class NetworkExcelPreFilter`
**命名空间**：`NovaFramework.Editor`

Network 模块 Excel 预处理器（纯拷贝）。将源目录下的 xlsx 文件原样搬运到 `_temp/` 目录，供 Luban CLI 读取。源表已删除环境维度列（Platform/Channel/DevelopValue/PublishValue），过滤/合并逻辑随之整体删除；环境差异上移到 Config 三维矩阵承载（ADR-054 决策 8）。

---

## § 2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `NetworkExcelPreFilter.cs` | `static NetworkExcelPreFilter` | 完整实现：FilterAll / FilterFile 公开入口（纯拷贝） |

---

## § 3 继承关系

```
NetworkExcelPreFilter（internal static class，无继承）
```

---

## § 4 关键字段表

| 字段 | 类型 | 值 | 说明 |
|---|---|---|---|
| `c_SearchPattern` | `const string` | `"*.xlsx"` | 源文件搜索模式 |
| `c_ExcludePrefix` | `const string` | `"~$"` | 排除的临时文件前缀 |
| `c_ConfigsDirName` | `const string` | `"_configs"` | 需跳过的 _configs 子目录名 |
| `c_TempDirName` | `const string` | `"_temp"` | 需跳过的 _temp 子目录名 |

---

## § 5 完整公开 API

```csharp
// 对整个目录批量预处理（纯拷贝）
// 排除 _configs/ 和 _temp/ 子目录及 ~$ 开头的临时文件
public static void FilterAll(string sourceDirPath, string tempDirPath)

// 对单个 Excel 文件预处理（纯拷贝）
// 读取所有 Sheet，跳过 # 开头 Sheet 与不足 5 行的 Sheet，其余原样写入 tempDirPath
public static void FilterFile(string excelFilePath, string tempDirPath)
```

---

## § 9 关键算法

### FilterFile 纯拷贝流程

```
FilterFile(excelFilePath, tempDirPath)
  │
  ├─ 读取所有 Sheet → allSheets
  ├─ 逐 Sheet 过滤（只跳过，不改内容）：
  │   sheetName.StartsWith("#") → 跳过（注释 Sheet）
  │   rows == null || rows.Count < 5 → 跳过（无效 Sheet）
  │   其余 → 原样加入 outputSheets
  ├─ outputSheets 为空 → Debug 日志 + return
  ├─ 确保 tempDirPath 存在
  └─ EditorUtil.Excel.Write(outputPath, outputSheets)
```

> Luban 读取 schema 时会自行跳过 `#` 前缀隐藏列，无需在此剥列。

---

## § 10 常见误区

| 误区 | 正确理解 |
|---|---|
| 期望 PreFilter 按平台/渠道过滤行 | ADR-054 决策 8：源表已删环境维度列，过滤逻辑整体删除，现为纯拷贝 |
| 对 _temp/ 目录下的文件执行 FilterAll | FilterAll 内部自动跳过 _configs/ 和 _temp/ 子目录，不会循环处理临时文件 |
| Sheet 名以 # 开头时被跳过 | 属于预期行为（注释 Sheet） |

---

## § 11 使用示例

```csharp
// 由 NetworkComponentInspector 的导出按钮触发
// sourceDirPath = NetworkSettings.HostKeySettings.SourceDirPath 或 NetCmdSettings.SourceDirPath
string tempDir = System.IO.Path.Combine(sourceDirPath, "_temp");
NetworkExcelPreFilter.FilterAll(sourceDirPath, tempDir);
// 搬运完成后交给 Luban CLI 读取 _temp/ 目录下的临时 Excel
```

---

## § 13 关联文档

- [DataPipeline.md](../../DataPipeline.md)
- [NetworkSettings.md](../../../../Runtime/Modules/Network/Definitions/NetworkSettings.md)
- [NetworkComponentInspector.md](../../../Inspectors/NetworkComponentInspector/NetworkComponentInspector.md)
- [DataPipeline.md](../../DataPipeline.md)
