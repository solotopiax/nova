# EditorUtil.TrackRegistry

**类签名**：`public static partial class TrackRegistry`（`EditorUtil` 的嵌套 partial）
**命名空间**：`NovaFramework.Editor`
**全局访问**：`EditorUtil.TrackRegistry`

打点 Excel 汇总工具。扫描 Framework 公共打点工作簿与各包内模块打点工作簿，合并生成一份本地汇总 xlsx，供 SDK Inspector 一键打开查看；不包含注册表/打点在代码里的定义，仅以 Excel 文件为唯一事实源。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Editor/EditorUtil/EditorUtil.TrackRegistry/EditorUtil.TrackRegistry.cs` | `EditorUtil.TrackRegistry` | 汇总入口：扫描/读取/重名校验/规范化行/调度写盘 |
| `Editor/EditorUtil/EditorUtil.TrackRegistry/EditorUtil.TrackRegistry.XlsxWriter.cs` | `EditorUtil.TrackRegistry.XlsxWriter`（private） | 最小 OpenXML xlsx 写入器：样式、列宽、行高、合并单元格 |

---

## §5 完整公开 API

```csharp
// 生成当前工程的打点汇总表
// 扫描 Framework 与各包 Nova/Tracks/Tracks.xlsx，合并写入 Library/Nova/Tracks/Tracks.generated.xlsx
// <param name="projectRoot">工程根目录；null 或空时抛 ArgumentException</param>
// <returns>生成的 xlsx 绝对路径</returns>
// 未找到任何打点 Excel 时抛 InvalidOperationException
// 同名 Sheet 出现在不同来源时抛 InvalidOperationException（携带来源 A/B 路径）
public static string Generate(string projectRoot);
```

内部 `XlsxWriter` 为 `private`，不对外公开。

---

## 关键行为

### 扫描来源（固定顺序）

1. Framework 公共表：`Assets/Framework/Tracks/Tracks.xlsx`（若存在，其所有 Sheet 标记为 Framework 说明页）
2. 包内模块表，依次扫描三个根目录，按包目录名 ordinal 排序：
   - `UPMPackages/*/Nova/Tracks/Tracks.xlsx`
   - `Packages/*/Nova/Tracks/Tracks.xlsx`
   - `Library/PackageCache/*/Nova/Tracks/Tracks.xlsx`

### 输出位置

`Library/Nova/Tracks/Tracks.generated.xlsx`（不入库，属本地缓存）

### Sheet 合并规则

- 读取每份工作簿的全部 Sheet，按来源追加进汇总列表
- 同名 Sheet 跨来源出现立即抛 `InvalidOperationException`，错误信息携带 Sheet 名与两个来源工作簿路径
- 每行末尾的空单元格在写出前裁剪；空引用统一转为空字符串

### 生成 xlsx 的样式规则（XlsxWriter）

- 单元格统一使用 `inlineStr` 内联字符串，不生成 sharedStrings
- 列宽：按内容展示宽度估算（中文按 2 字符宽），最小 10、最大 55
- 行高：按内容行数与列宽估算换行，最小 18、最大 150；超过 18 才写 `customHeight`
- Framework 说明 Sheet 首行：列宽固定 32、行高固定 48，A1 用标题样式
- 模块打点 Sheet 从第 2 行起（索引 1）按"首列事件名分组"交替底色：首列非空即开启新分组；前 3 列与末 3 列视为分组合并列（连续非空区段纵向合并）
- 特殊 Sheet 名 `$注意事项`（含乱码兼容 `$娉ㄦ剰浜嬮」`）在 Framework 表内时，B 列宽度按内容宽度 ×1.5，上限 200

### 触发方式

不在 Editor 启动/域重载时自动跑，仅由 `SDKComponentInspector` 上的"生成汇总表"按钮显式触发；失败后只记 `Log.Warning` 不抛给 UI。

---

## §11 使用示例

```csharp
// 业务侧通常无需手动调用——SDK Inspector 面板上的按钮已封装 try/catch 与 OpenFile
// 仅在脚本化流水线里需要主动刷新汇总表时手动调用
string projectRoot = Directory.GetParent(Application.dataPath).FullName;
string generatedPath = EditorUtil.TrackRegistry.Generate(projectRoot);
EditorUtil.FileSystem.OpenFile(generatedPath);
```

---

## §13 关联文档

- [Editor.md](../../Editor.md)
- [EditorUtil.md](../EditorUtil.md)
- [EditorUtil.Excel.md](../EditorUtil.Excel/EditorUtil.Excel.md)（读取源工作簿 `ReadAllSheets` 的底层入口）
- [SDKComponentInspector.md](../../Inspectors/SDKComponentInspector/SDKComponentInspector.md)（主要调用方：触发 `Generate` 并打开产物）
