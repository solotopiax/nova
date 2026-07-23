# NetworkExcelPreFilter

**类签名**：`internal static class NetworkExcelPreFilter`

**命名空间**：`NovaFramework.Editor`

`NetworkExcelPreFilter` 把 Network 专用 Excel 转成 Luban 当前约定的 `_temp` CSV 输入。HostKeys 与 NetCmds 使用不同规则，不能当作通用表格预处理。

## HostKeys 规则

每个有效 Sheet 都必须严格使用以下名称之一：

```text
xxxxx-Debug
xxxxx-Release
```

相同 `xxxxx` 的 Debug 与 Release 必须同时存在。导出器从当前已导出的 `ConfigRuntimeSO.DevelopMode` 取得模式，预处理器只选择对应 Sheet，并在临时 CSV 名中去掉模式后缀：

```text
NetworkHostKeys-Debug   --DevelopMode.Debug-->   _temp/NetworkHostKeys/NetworkHostKeys.csv
NetworkHostKeys-Release --DevelopMode.Release--> _temp/NetworkHostKeys/NetworkHostKeys.csv
```

所有工作簿完成命名与配对校验后才开始写 `_temp`。任一工作簿缺少配对、没有基础名或存在无模式后缀的有效 Sheet，整次导出都会终止。

## NetCmds 规则

NetCmds 没有环境维度定制。预处理只跳过 `#` 开头的 Sheet、少于 5 行的无效 Sheet、`_configs`、`_temp` 与 `~$` 临时文件，其余 Sheet 名称和内容保持不变。

## 内部入口

```csharp
internal static void FilterHostKeys(string sourceDirPath, string tempDirPath, DevelopMode mode);
internal static void FilterNetCmds(string sourceDirPath, string tempDirPath);
```

`FilterHostKeyFile`、`FilterNetCmdFile` 及可替换 reader/writer 的重载仅用于内部投影和定向测试，不是公共 API。

## 职责边界

- 负责 Network Sheet 命名、配对、模式选择和 `_temp` 输入生成。
- 不读取 ConfigRuntime；DevelopMode 由 `NetworkExporter` 提供。
- 不调用 Luban，也不替换正式 JSON/C# 产物。
- `_temp` 位于对应 Network Excel 源目录下，只在单次导出期间存在。

## 关联文档

- [NetworkExporter.md](NetworkExporter.md)
- [DataPipeline.md](../../DataPipeline.md)
- [NetworkSettings.md](../../../../Runtime/Modules/Network/Definitions/NetworkSettings.md)
