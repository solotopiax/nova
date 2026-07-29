# TableSettings

`TableSettings` 同时保存 Editor 使用的多个 Luban Project，以及 Player 使用的加载描述。Nova 直接切换到这套结构，不保留旧版单 Project 与资源前缀配置。

## Luban Projects

`Projects` 仅在 `UNITY_EDITOR` 下存在。每个 `TableLubanProjectSetting` 包含：

- `Id`、`Name`：Nova Inspector 标识与显示名；
- `ConfigPath`：正式 `luban.conf` 的项目根相对路径；
- `ExportDescriptions`：当前 Project 的任意数量导出描述。

`luban.conf`、schema 和 Excel 仍是 Luban 真相源。Inspector 解析它们，只读展示目录、Excel、Sheet 与 Luban 表的树形清单，不生成 Catalog，也不改写 Luban 配置文件。

## 导出描述

`TableExportDescriptionSetting` 对应一次 Luban CLI 调用：

| 字段 | Inspector 名称 | 说明 |
|---|---|---|
| `Name` | 名称 | 自定义显示名称 |
| `Enabled` | 启用 | 是否参与无参数批量导出 |
| `Target` | Target目标 | `luban.conf` 中的 target |
| `Format` | 导出方式 | JSON、Binary、Protobuf Binary、Protobuf JSON、MsgPack 预设 |
| `CodeTargets` | 代码Targets | 重复的 Luban `-c` |
| `DataTargets` | 数据Targets | 重复的 Luban `-d` |
| `OutputScope` | 输出表格范围 | 全部表格或指定表格 |
| `OutputTables` | 表格清单 | 指定模式下重复的 `-o`，保存 Luban 表完整名 |
| `CodeOutputPath` | 代码输出目录 | 项目根相对发布目录 |
| `DataOutputPath` | 数据输出目录 | 项目根相对发布目录 |
| `IncludeTags` | 包含Tags清单 | 重复的 `-i` |
| `ExcludeTags` | 排除Tags清单 | 重复的 `-e`，不能与包含清单同时配置 |
| `FieldVariants` | 字段变体 | 重复的 `--variant` |
| `CustomTemplateDirs` | 自定义模板目录 | 重复的 `--customTemplateDir` |
| `AdvancedArguments` | 高级Luban参数 | 任意 `-x name=value` |

指定表格可以按 Excel 或按 Luban 表选择，两种视图最终都只保存 Luban 表完整名。五种方式只是快捷预设，Targets 和高级参数仍可继续编辑，Nova 不维护 Codec 安装白名单。

## 加载描述

`TableRuntimeSettings.LoadDescriptions` 可同时加载任意多个生成结果。每条 `TableLoadDescriptionSetting` 保存：

- 所属 Project、导出描述和运行时 DataTarget；
- Inspector 根据 `luban.conf` 的 `manager + topModule` 内部解析的 Binding 类型；
- 每个 Luban `output_data_file` 到 YooAsset `Asset 地址` 的显式映射；
- 对应 Unity `AssetPath`，用于按默认资源包的 Collector/AddressRule 自动刷新地址。

Binding 类型不在 Inspector 暴露，资源包也不单独选择；默认资源包来自 `AssetComponent`。

## 开发态与消费态路径

Table 的工程配置、导出目录和加载描述中的 `AssetPath` 都保存项目根相对路径。Nova 发版时统一扫描 `Nova.prefab` 中所有以 `Assets/Samples/` 开头的字符串路径，并作为 Sample Scene Override 注入；Sample 导入消费工程后，现有 `SamplePathManifest` 会把开发态 Sample 根替换为真实导入目录。因此 `ConfigPath`、代码/数据输出目录、自定义模板目录和 `AssetPath` 共用同一条纠正链，不维护 Table 专属字段白名单。

`Assets/Framework/Templates/Luban/...` 属于框架包模板，不参与 Sample 根替换。执行导出时会根据当前安装形态解析为开发态 `Assets/Framework` 或消费态 Package resolved path。`AssetAddress`、`DataFile` 和 Binding 类型不是文件路径，不参与自动纠正。

## 多语言

语言表、语言列和区域差异继续使用 Luban 原生 Target、Tags、字段变体、自定义模板与高级参数表达。Table 不限制这些机制，也不接管 Localization 模块自己的专用数据链。

## 关联文档

- [TableManager.md](../TableManager.md)
- [TableComponentInspector.md](../../../../Editor/Inspectors/TableComponentInspector/TableComponentInspector.md)
- [EditorUtil.Table.Exporter.md](../../../../Editor/EditorUtil/EditorUtil.Table/EditorUtil.Table.Exporter.md)
