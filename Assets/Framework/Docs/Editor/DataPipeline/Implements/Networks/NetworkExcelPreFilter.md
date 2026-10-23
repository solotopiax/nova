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

### HostKey 字段输入约束

- `Value` 表示主域名，列必须存在且有效数据行必须填写；缺列时在写 `_temp` 前报告工作簿、Sheet 与表头行。
- `FallbackValue` 表示备用域名，可以为空。旧表未声明此列时按空备用处理，仍完整校验主域名，不替旧表新增列。
- 非空地址必须是有效的 HTTP 或 HTTPS 绝对地址，首尾不能有空格，末尾不能有斜杠。
- 主、备用域名同时填写时，必须使用相同协议。

`Templates/NetworkHostKeysTemplate.xlsx` 的 Debug/Release 两个 Sheet 均提供可选 `FallbackValue` 列，默认为空。`XXXXX` 是模板占位符，使用前必须将主域名替换为实际 HTTP(S) 地址。模板本身没有项目导出配置；复制为项目正式表源并接入 `HostKeySettings` 后，才执行正式导出与运行时验证。

## NetCmds 规则

NetCmds 没有环境维度定制。预处理只跳过 `#` 开头的 Sheet、少于 5 行的无效 Sheet、`_configs`、`_temp` 与 `~$` 临时文件，其余 Sheet 名称和内容保持不变。

`Path` 可以为空；非空时必须以 `/` 开头。这样 HostKey 基础地址与 Path 可以直接且唯一地拼接。

## 内部入口

```csharp
internal static void FilterHostKeys(string sourceDirPath, string tempDirPath, DevelopMode mode);
internal static void FilterNetCmds(string sourceDirPath, string tempDirPath);
```

`FilterHostKeyFile`、`FilterNetCmdFile` 及可替换 reader/writer 的重载仅用于内部投影和定向测试，不是公共 API。

## 职责边界

- 负责 Network Sheet 命名、配对、模式选择和 `_temp` 输入生成。
- 不读取 ConfigRuntime；DevelopMode 由 `NetworkExporter` 提供。
- 不调用 Luban，也不替换正式数据/C# 产物。
- `_temp` 位于对应 Network Excel 源目录下，只在单次导出期间存在。

## 关联文档

- [NetworkExporter.md](NetworkExporter.md)
- [DataPipeline.md](../../DataPipeline.md)
- [NetworkSettings.md](../../../../Runtime/Modules/Network/Definitions/NetworkSettings.md)

## 发现与字段解析的精度

目录入口递归发现全部 xlsx，不只 Settings.Units，排 _temp/_configs 与锁文件但备份仍参与；以 workbook basename 建临时目录，不保源子目录，同名 stem 可碰撞。单文件入口不套目录排除。先验证所有投影，再顺序写 CSV，写中失败可留部分临时文件。

第2行关键列精确匹配首项，不 Trim/#兼容；HostKey Value 必需、FallbackValue 可缺，Name 存在且空则跳行，#Name 未专门跳。所有有效 Debug/Release Sheet 在选择前都校 URL，非 Debug 枚举走 Release。URL 不完整禁止 userinfo/query/fragment/path；NetCmd 缺 Path 列跳校验，空 Path 合法，非空只要求 / 开头，不限制 //、空白或查询语义。错误辅助标签可能仍显示 HostKeys，须结合真实源位置判断。
