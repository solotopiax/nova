# EditorUtil.Table.Exporter

`EditorUtil.Table.Exporter` 直接消费一个或多个正式 `luban.conf + schema + data`，为全部已启用导出描述建立独立工作区，并通过 `OutputApplier` 发布。

## API

```csharp
public static bool ExportAll(TableSettings settings);
public static bool ExportCode(TableSettings settings);
public static bool ExportData(TableSettings settings);

public static bool ExportAll(TableSettings settings, params string[] descriptionIds);
public static bool ExportCode(TableSettings settings, params string[] descriptionIds);
public static bool ExportData(TableSettings settings, params string[] descriptionIds);
```

无 ID 入口处理全部 Project 中已启用的导出描述。精确调用推荐传 `ProjectId/DescriptionId`；仅传描述 ID 时会匹配所有同名描述。

## Luban 参数

Exporter 结构化传递：

- 重复的 `-c`、`-d` 和指定表格模式下重复的 `-o`；
- `--conf`、`-t`；
- 重复的 `-i`、`-e`、`--variant`、`--customTemplateDir`；
- 任意数量的 `-x name=value`。

五种预设只是创建描述时的默认值。用户可以继续修改 Targets、模板和高级参数，Nova 不维护 Codec 安装白名单，也不限制 Luban 自定义 Target。

自定义模板目录传给 Luban 前会经过 `EditorUtil.Luban.ExportHelper`：Nova 框架模板的逻辑路径会自动解析到当前安装形态的真实物理目录，项目自定义模板路径保持原值。这样同一份 Table 配置可同时用于开发仓和通过 UPM 安装的消费工程。

## 导出流程

1. 解析全部 Project 与目标导出描述，校验 ID、Target、范围和输出目录。
2. 每个描述使用独立 `Library/Nova/TableExport/<guid>` 工作区。
3. Luban 只接收该描述声明的原生参数。
4. 包含 `protobuf3 + cs-newtonsoft-json` 时生成 proto、调用 `protoc`，并追加 Nova Protobuf Tables Binding；其他代码 Targets 仍继续透传。
5. 代码和数据分别发布到描述指定目录。
6. 多个描述共用目录时，只替换本次生成的同名产物，不清理其他文件。
7. 全部任务成功后刷新 AssetDatabase；Inspector 随后可根据实际数据文件刷新加载描述。

导出链不生成 Catalog。单表清单和 Codec 构造方式由生成 Binding 提供。

## 关联文档

- [TableSettings.md](../../../Runtime/Modules/Table/Definitions/TableSettings.md)
- [TableComponentInspector.md](../../Inspectors/TableComponentInspector/TableComponentInspector.md)
- [PipifySteps.md](../EditorUtil.Pipify/PipifySteps.md)
