# EditorUtil.Table.Exporter

`EditorUtil.Table.Exporter` 直接消费正式 `luban.conf + schema + data`，在隔离工作区生成一个或多个 Profile，并通过 `OutputApplier` 发布。

## API

```csharp
public static bool ExportAll(TableSettings settings);
public static bool ExportCode(TableSettings settings);
public static bool ExportData(TableSettings settings);

public static bool ExportAll(TableSettings settings, params string[] profileIds);
public static bool ExportCode(TableSettings settings, params string[] profileIds);
public static bool ExportData(TableSettings settings, params string[] profileIds);
```

无 Profile ID 的入口处理全部 `Enabled` Profile；显式重载可一次指定任意一个或多个 Profile。
`ExportAll` 会按每个 Profile 实际配置的目标生成代码、数据或两者，因此代码专用和数据专用 Profile 可以同时参与批量导出。

## Luban 参数

Exporter 原样构建以下参数：

- 重复的 `-c` 与 `-d`；
- `--conf` 与 `-t`；
- 重复的 `-i`、`-e`、`--variant`、`--customTemplateDir`；
- 任意数量的 `-x name=value`。

Nova 不维护 Codec 安装表或格式枚举。内置预设覆盖 JSON、Binary、Protobuf Binary、Protobuf JSON 与 MsgPack，自定义 target 使用相同入口。

## 导出流程

1. 校验 Project、Profile ID、目标列表和对应输出目录。
2. 每个 Profile 使用独立 `Library/Nova/TableExport/<guid>` 工作区。
3. Luban 只接收 Profile 声明的目标和原生参数。
4. 包含 `protobuf3` 代码目标时，追加 `protoc` 与 Nova Protobuf Tables 适配模板。
5. 代码和数据分别发布到 Profile 指定目录。
6. 多个 Profile 共用目录时只替换本次同名产物，不清理其他 Profile 文件。
7. 全部指定 Profile 成功后刷新 AssetDatabase。

表清单与解码方式由 Luban 生成 Binding 提供，导出链不生成额外运行时目录文件。

## 关联文档

- [TableSettings.md](../../../Runtime/Modules/Table/Definitions/TableSettings.md)
- [TableComponentInspector.md](../../Inspectors/TableComponentInspector/TableComponentInspector.md)
- [PipifySteps.md](../EditorUtil.Pipify/PipifySteps.md)
