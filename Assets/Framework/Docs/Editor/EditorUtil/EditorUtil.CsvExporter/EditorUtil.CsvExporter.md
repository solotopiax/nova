# EditorUtil.CsvExporter

`NovaFramework.Editor.EditorUtil.CsvExporter` 是交互式 CSV 诊断导出工具，当前只有两个入口：

```csharp
string Export(string title, string header, IEnumerable<string> rows);
string EscapeField(string field);
```

Export 打开固定“导出 CSV 数据”保存面板，默认文件名由 title 与本地时间组成。取消返回 null；选择路径后以 UTF-8 BOM 覆盖写入完整 header，再惰性枚举已经格式化的 rows。它不接收二维数组或 DataTable，也不提供显式 outputPath 的无界面重载。

写入或枚举异常在本函数记录日志，可能留下部分文件，仍返回用户选定的 path；因此非 null 路径不等于导出成功，调用方应检查实际文件。目录、凭据或完整事务不由本函数冻结。

EscapeField 对 null/空返回空，对含逗号、引号或换行的单个字段加双引号并倍写内部引号，其余原样。Export 不自动逐字段调用它，调用方必须先正确拼出表头/行；它不处理 Excel 公式起始符等业务策略。对象池和运行列表等诊断导出须另外确认快照范围与特殊名称转义。

## 关联文档

- [EditorUtil](../EditorUtil.md)
- [FileSystem](../EditorUtil.FileSystem/EditorUtil.FileSystem.md)
