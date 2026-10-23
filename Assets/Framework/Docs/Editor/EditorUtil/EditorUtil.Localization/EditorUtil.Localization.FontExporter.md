# EditorUtil.Localization.FontExporter

**类签名**：`public static class FontExporter`（嵌套于 `EditorUtil.Localization`）

**命名空间**：`NovaFramework.Editor`

本地化字体 Excel 使用 Luban 原生表结构，不需要文本导出的多语言 PreFilter；但它与其他非 Table 模块一样，先在源目录的 `_temp/_publish` 完成生成和验证，再一次性发布正式数据与 C#。

## 公开 API

```csharp
public static bool ExportFontAll(
    LocalizationSettings settings,
    string sourceDirPath,
    string classExportPath);

public static bool ExportFontCode(
    LocalizationSettings settings,
    string sourceDirPath,
    string classExportPath);

public static bool ExportFontData(
    LocalizationSettings settings,
    string sourceDirPath);
```

公开签名和 Inspector/Pipify 调用方式未变化。`ExportFontAll` 在 `classExportPath` 为空时只导出数据。

## 导出流程

```text
校验 FontUnitsSettings、源目录和输出路径
  -> 获取源目录 _temp 工作区租约并清理旧临时内容
  -> 克隆字体 Settings，把数据/C# 输出改写到 _temp/_publish
  -> 仅代码导出时复制正式数据，供 Map 属性生成读取
  -> Luban Pipeline 生成数据、类型和 SchemaManifest
  -> 验证全部预期数据/C#
  -> Luban.GeneratedOutput 写入第一行单行所有权标记
  -> 登记同名反格式数据及其 .meta 删除项
  -> FileSystem.OutputApplier 一次发布全部正式产物
  -> 持有工作区租约清理 _temp 并刷新 AssetDatabase，最后释放租约
```

Pipeline、产物验证或应用任一步失败时，正式产物在 Apply 前保持不变，应用失败时尝试补偿恢复。全量代码清理只删除 Profile、Source 和正文 Hash 均匹配的过期文件；无标记、跨归属或人工修改文件保留并报警。

字体数据采用 JSON/Binary 二选一发布。成功生成 `.json` 时，同一事务删除同名 `.bytes` 及其 `.meta`；成功生成 `.bytes` 时反向删除同名 `.json` 及其 `.meta`。仅代码导出不改变正式数据文件。

源目录的 `_temp` 租约获取失败时，返回失败，不删除已有临时内容或 `.meta`，也不运行 Pipeline 和刷新。成功取得租约后，生成、发布、清场与刷新都由当前持有者完成，最后才释放；刷新异常也会释放租约。此保护只覆盖当前 Editor 进程，IO 删除失败仍可能留下临时文件。

## Agent Action 来源与验收

`nova.project.localization.export` 的 `target=font` 继续复用本门面，支持 `all/code/data`，不为字体或参数新增 Action。

- 三种范围均经 `BuildExportContext` 读取激活 Master 的顶层 Namespace，并同步源目录持久 `_configs/luban.conf`、`__tables__.xml`、`nova-export-manifest.json`。新 Plan 要求持久 Master/Globals 绑定、GUID、路径、Namespace 与原字节稳定；不把派生 Runtime Namespace 当生成来源。明确空字符串 Namespace 合法，缺失或未保存 Master 会写前阻断。
- 输入冻结包含已保存场景与组件、精确字体 Unit Excel、既有配置和实际 Luban 工具成员。`all/code` 额外冻结实际默认/字体模板的完整目录成员；`code` 还冻结 `SeedStagedData` 会读取的现有正式数据及其缺失状态。`data` 不强加代码模板或已加载类型验收。
- WriteSet 完整包含数据与反格式文件及其 `.meta`、代码目录内替换和安全清理、持久三配置、代码范围的 `_configs/output-manifests/localization-font.json`、`_temp` 与可能补建的目录元文件。反格式删除项无论当时文件是否存在都进入计划。
- 新 Receipt `sourceContractVersion=1` 先重新解析当前活动场景的唯一组件，核对场景、组件身份、实时 JSON Hash 和设置；脏组件或活动场景变化返回 partial，保存文件的 SHA 不能替代实时绑定。随后按冻结设置反推完整输入、输出和配置范围，核源、工具和模板未漂移，再核 Sync 后真实配置 target/manager/topModule、Manifest、配置产物摘要及生成代码正文所有权。配置旧字节是写前门，Sync 后使用执行后摘要，不把合法配置更新判为源漂移。
- 新 `font/all/code` 收据必须通过唯一 `compilationToken` 对应的本次真实编译与脚本重载，再按生成 `.cs` 所属 owner assembly 精确查询类型，核对 Tables 的 `ILubanTables` 和表的 `ITable`，不实例化业务。单一编译记录只保留最近一次，必须逐个导出、编译、Verify；代码成功保留 Static 并增加 Compile + Artifact，满足既有描述符要求；数据仍为 Static + Artifact。
- 旧收据继续摘要 success 并附缺新来源/编译证据的 warning；显式 null、损坏版本或不完整的新字段不允许退回旧语义。导出和类型验收不证明字体资源加载、语言切换或真实 UI 显示，业务接入仍需对应运行证据。

## 关联文档

- [EditorUtil.Localization.TextExporter.md](EditorUtil.Localization.TextExporter.md)
- [EditorUtil.Luban.Pipeline.md](../EditorUtil.Luban/EditorUtil.Luban.Pipeline.md)
- [EditorUtil.Luban.GeneratedOutput.md](../EditorUtil.Luban/EditorUtil.Luban.GeneratedOutput.md)
- [EditorUtil.FileSystem.OutputApplier.md](../EditorUtil.FileSystem/EditorUtil.FileSystem.OutputApplier.md)
- [PipifySteps.md](../EditorUtil.Pipify/PipifySteps.md)

## 字体参数与对象边界

FontData 没有代码目录参数；FontAll 的空 classExportPath 表示仅数据，FontCode 则要求代码目录。预校验主要检查 settings、Unit/source 非空及按格式匹配后缀，不完整校验路径归属、重复源、数据碰撞或枚举域；Binary 之外按 Json 分支，真实文件检查由后续流程负责。

暂存 Unit 复制必要字段，以索引前缀避免同名暂存数据碰撞；代码模式直接复制旧正式数据，缺数据可跳过并导致 Map 不完整。调用者仍持有原 settings/列表，发布登记会再读取，不能把普通 bool API 当冻结快照。路径前置异常和 Refresh 可外抛，清理失败可留临时文件；工作区租约仍 finally 释放，回滚不完整的限制见 OutputApplier。
