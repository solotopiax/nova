# DataPipeline — Excel 预处理与导出编排

**命名空间**：`NovaFramework.Editor`

Nova 的非 Table Excel 由各模块维护专用导出链。Localization 负责语言投影；UI 负责 UI 表契约；Network 负责 HostKeys 模式投影；Sound 与 Vibrate 保持 Luban 原生表结构，但负责自己的暂存、产物验证与安全发布。Config 预过滤器已移除，Config 导出由 ConfigWindow 与 `EditorUtil.Config.Exporter` 负责。

## 设计边界

Nova 只把 Table 模块的表格导出定义为通用表格导出。Localization、Network、Config 等其他模块使用的 Excel 都是模块专用输入：列、Sheet 和路径的含义由对应模块决定，导出前的校验与转换也由对应模块负责。

各模块可以复用不包含业务含义的基础设施，例如 `EditorUtil.Excel`、Luban Pipeline 和 SchemaManifest；不应为了统一形式，把不同模块的 PreFilter、Exporter 或产物发布流程抽成一套通用业务框架。

| 层级 | 是否共享 | 示例 |
|---|---|---|
| 通用表格业务 | 仅 Table 模块 | 常规 Excel/CSV 表结构与数据导出 |
| 模块专用导出 | 各模块独立 | Localization 语言列投影、UI 契约校验、Network HostKeys 模式投影、Sound/Vibrate 分模块暂存发布、Config 配置快照导出 |
| 无业务语义的基础设施 | 可以共享 | Excel 读写、Luban 调用、结构快照、`Luban.GeneratedOutput`、`FileSystem.OutputApplier`、临时工作区租约 |

## 实现概览

| 实现 | 调用方 | 职责 |
|---|---|---|
| `LocalizationExcelPreFilter` | `LocalizationTextExporter` | 只加载 Settings 配置的 xlsx，一次完成校验并投影按语言的 Name/Value 临时 CSV |
| `LocalizationTextExporter` | `EditorUtil.Localization.TextExporter` | fail-fast 编排数据、代码、Map 与语言列表，验证后统一应用 |
| `UIExporter` | `EditorUtil.UI.Exporter` | 校验 UI 表格与路径，编排全量/单文件 Luban 暂存导出 |
| `NetworkExcelPreFilter` | `NetworkExporter` | HostKeys 校验 Debug/Release 配对并选择当前模式；NetCmds 原样投影有效 Sheet |
| `NetworkExporter` | Network 公共门面与 Inspector | 编排全量/单文件 Luban 暂存导出，并通过通用 OutputApplier 发布 |
| `EditorUtil.Sound.Exporter` | Sound Inspector 与 Pipify | 保持 Sound API，编排全量/单文件暂存生成、验证和发布 |
| `EditorUtil.Vibrate` 导出方法 | Vibrate Inspector 与 Pipify | 保持 Emphasis/Custom 双轨，分别暂存并发布各区域产物 |

各模块分别维护自己的专用导出链，不要求接入同一套 PreFilter 或 Exporter 骨架。Sound/Vibrate 没有 Excel 结构投影，因此直接在现有 Exporter 中编排；它们也没有特殊文件应用语义，因此不增加只转发调用的模块级 OutputApplier。

这些链只共享无业务语义的基础设施：`Luban.GeneratedOutput` 根据 SchemaManifest 登记带所有权标记的 C# 产物并安全清理过期文件，`FileSystem.OutputApplier` 负责批量应用和失败恢复，`AcquireWorkspace` 防止同一临时目录重入。模块仍决定“导出什么”和“何时发布”。

## Localization 数据流

```text
Settings.Units 中配置的 xlsx
  -> LocalizationExcelPreFilter.SourceModel（每个文件只读取一次并完整校验）
  -> _temp/{language}/{relative-stem}/*.csv
  -> Luban Pipeline 输出到 _temp/_publish
  -> Map 与产物完整性验证
  -> FileSystem.OutputApplier 一次性应用正式 JSON / C# / 语言列表
  -> 清理 _temp
```

`_temp` 位于 Localization Excel 源目录下，是单次导出的短生命周期工作区；`_configs` 是 Luban 可再生但需保留的 Editor 配置缓存。

## UI 数据流

```text
EditorUtil.UI.Exporter
  -> UIExporter 校验 Settings、路径和非 # Sheet
  -> Luban Pipeline 输出到源目录 _temp/_publish
  -> JSON / C# / Map 完整性验证
  -> FileSystem.OutputApplier 一次性应用正式产物
  -> 清理 _temp
```

UI 的 `_temp` 同样位于 Excel 源目录下，只在单次导出期间存在。它不承担 Excel 结构转换，只隔离尚未完整验证的生成产物。

## Network 数据流

```text
HostKeys: ConfigRuntime.DevelopMode -> 校验 xxxxx-Debug/Release 配对 -> 选择并去掉后缀
NetCmds:  跳过注释/无效 Sheet -> 保持名称和内容
  -> _temp/{工作簿名}/{Sheet名}.csv
  -> Luban Pipeline 输出到 _temp/_publish
  -> NetworkExporter 验证 JSON / C# / SchemaManifest
  -> FileSystem.OutputApplier 一次性应用正式产物
  -> 清理 _temp
```

HostKeys 的 DevelopMode 只来自当前已导出的 ConfigRuntime；缺失时 Network 导出终止，不使用默认值。

## Sound / Vibrate 数据流

```text
Sound Settings、Vibrate 单区域 Settings 或 Localization Font Settings
  -> 模块 Exporter 把 JSON/C# 输出改写到源目录 _temp/_publish
  -> Luban Pipeline 生成数据、类型和 SchemaManifest
  -> 模块验证预期 JSON/C# 完整性
  -> FileSystem.OutputApplier 批量应用正式产物
  -> 清理 _temp
```

Vibrate Emphasis 与 Custom 使用不同源目录和事务，互不影响。Localization 字体也先在 `_temp/_publish` 完成生成和验证，再统一发布。Sound/Vibrate 的 Pipify 数据或类型 Step 每个区域只调用一次批次入口，返回 `false` 时立即抛出并终止流水线。

所有非 Table 模块的正式生成 C# 第一行都写入单行 `<nova-generated ... />` 标记。全量发布只删除 Profile、Source 与正文 Hash 均匹配且不在本次 SchemaManifest 期望集合内的文件；无标记、归属不同或正文被修改的文件保留并报警。`_configs/output-manifests/{ProfileId}.json` 只是可重建清单缓存，不参与删除裁决。

## 关联文档

- [LocalizationExcelPreFilter.md](Implements/Localizations/LocalizationExcelPreFilter.md)
- [LocalizationTextExporter.md](Implements/Localizations/LocalizationTextExporter.md)
- [UIExporter.md](Implements/UIs/UIExporter.md)
- [EditorUtil.FileSystem.OutputApplier.md](../EditorUtil/EditorUtil.FileSystem/EditorUtil.FileSystem.OutputApplier.md)
- [NetworkExcelPreFilter.md](Implements/Networks/NetworkExcelPreFilter.md)
- [NetworkExporter.md](Implements/Networks/NetworkExporter.md)
- [EditorUtil.Sound.Exporter.md](../EditorUtil/EditorUtil.Sound/EditorUtil.Sound.Exporter.md)
- [EditorUtil.Vibrate.Exporter.md](../EditorUtil/EditorUtil.Vibrate/EditorUtil.Vibrate.Exporter.md)
- [EditorUtil.Localization.TextExporter.md](../EditorUtil/EditorUtil.Localization/EditorUtil.Localization.TextExporter.md)
- [EditorUtil.Localization.FontExporter.md](../EditorUtil/EditorUtil.Localization/EditorUtil.Localization.FontExporter.md)
- [EditorUtil.Luban.GeneratedOutput.md](../EditorUtil/EditorUtil.Luban/EditorUtil.Luban.GeneratedOutput.md)
- [EditorUtil.Luban.Pipeline.md](../EditorUtil/EditorUtil.Luban/EditorUtil.Luban.Pipeline.md)
