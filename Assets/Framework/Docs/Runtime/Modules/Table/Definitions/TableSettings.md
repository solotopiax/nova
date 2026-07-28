# TableSettings

`TableSettings` 分为 Player 使用的 Runtime Bindings 和 Editor 使用的 Luban 导出预设，两者互不绑定。

## Player 配置

`TableRuntimeSettings.Bindings` 是 `TableRuntimeBindingSetting` 列表，每一项包含：

| 字段 | 说明 |
|---|---|
| `BindingTypeName` | 实现 `ILubanTableBinding` 的生成类型或业务类型全名 |
| `DataAssetLocationPrefix` | 该组 `output_data_file` 对应的资源地址前缀，可为空 |

一个构建可以配置多条 Binding，因此可同时加载多个 Luban Project、多个 Tables 容器或多种 Codec 的生成结果。直接调用 `RegisterTables` 时也可以不配置 Binding。

## Editor Project

`TableProjectSettings` 仅在 `UNITY_EDITOR` 下存在：

| 字段 | 说明 |
|---|---|
| `ConfigPath` | 正式 `luban.conf` |
| `Target` | `luban.conf` 中的 target |
| `Profiles` | 可组合使用的导出预设 |

每个 `TableExportProfileSetting` 支持：

- `Enabled`：是否参与无参数批量导出，可同时选择任意多个；
- Luban `-c` / `-d` 目标列表；
- 独立的代码和数据发布目录；
- `-i`、`-e`、`--variant`；
- 多个 `--customTemplateDir`；
- 任意 `-x name=value`。

Profile 不是 Player 格式选择器。Nova 不校验 target 名称或 Codec 枚举，JSON、Binary、Protobuf、MsgPack 和自定义 Luban 输出目标使用同一套透传机制。

## 多语言

Table 的语言列、语言表和区域差异继续使用 Luban 原生 target、tag、variant、模板与扩展参数表达；Table 模块不接管 Localization 的专用导出链。

## 关联文档

- [TableManager.md](../TableManager.md)
- [TableComponent.md](../TableComponent.md)
- [TableComponentInspector.md](../../../../Editor/Inspectors/TableComponentInspector/TableComponentInspector.md)
