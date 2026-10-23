# EditorUtil.Localization.TextExporter

**类签名**：`public static class TextExporter`（嵌套于 `EditorUtil.Localization`）

**命名空间**：`NovaFramework.Editor`

公共门面签名保持不变；内部由 `LocalizationExcelPreFilter` 负责源加载、校验和 CSV 投影，`LocalizationTextExporter` 负责编排，通用 `FileSystem.OutputApplier` 负责正式文件应用与失败回滚。

上述三段式结构是 Localization 对多语言 Excel 的专用导出实现，不定义其他模块的通用表格导出方式。Nova 的通用表格导出只属于 Table 模块；这里仅复用 Excel 读写、Luban 等基础设施。

## 公开 API

```csharp
public static bool ExportTextAll(
    LocalizationSettings settings,
    string sourceDirPath,
    string classExportPath,
    string[] customTemplateDirs,
    string supportedLanguagesExportPath);

public static bool ExportTextCode(
    LocalizationSettings settings,
    string sourceDirPath,
    string classExportPath,
    string[] customTemplateDirs);

public static bool ExportTextData(LocalizationSettings settings, string sourceDirPath);

public static bool ExportSupportedLanguages(string sourceDirPath, string exportPath,
    LubanDataFormat dataFormat = LubanDataFormat.Json);
```

## 全量导出顺序

```text
获取 SourceDir 重入门
  -> 从 Settings.Units 构建并完整验证 LocalizationExcelPreFilter.SourceModel
  -> 清理旧 _temp
  -> 逐语言生成 CSV 并把 JSON 暂存到 _temp/_publish/data
  -> 生成代码到 Unity 忽略的 _temp/_publish/code~
  -> 使用暂存 JSON 与暂存代码生成、验证 Map 属性
  -> 通过独立 Luban target 暂存支持语言表
  -> 注册精确的旧语言 JSON 删除项
  -> Luban.GeneratedOutput 为 C# 写入第一行单行所有权标记
  -> FileSystem.OutputApplier 一次应用全部替换与删除
  -> finally 清理 _temp 并刷新 AssetDatabase
```

任一语言、Luban、代码、Map 或产物验证失败都会立即终止；调用 `Apply` 前不会修改正式数据、C# 或支持语言列表。应用中途失败时，已替换和已删除文件按逆序从备份恢复；回滚也失败会抛出聚合异常并由门面记录一次失败。

## 路径和清理契约

- `_temp` 在 Excel 源目录下；导出开始前和结束后都清理。
- CSV 保留配置源的相对 stem，避免不同子目录同名 xlsx 互相覆盖。
- 暂存 `.cs` 放在 `code~`，避免正式应用前被 Unity 导入编译。
- 旧语言清理只展开每个 Unit 的 `DatasExportPath.Replace("{0}", language)` 精确路径，不使用通配删除。
- 独立 `ExportSupportedLanguages` 只发布语言列表，不删除任何语言数据；因其 public API 没有 Settings 参数，语言发现仍走兼容递归扫描。
- `_configs` 是 Luban 可再生工作缓存，不属于正式产物应用批次；`_temp`、`.tmp` 和备份均不得残留。
- `_configs/output-manifests/localization-text.json` 只记录最近一次全量代码产物清单；它可以重建，不参与过期文件删除判断。
- 文本 `code/all` 同时生成 `LocalizationSupportedLanguagesTables`、`TbLocalizationSupportedLanguages`、`LocalizationSupportedLanguage`。该独立 Profile 的 Luban 配置位于 `_temp/_supported_languages/config`，但 `GeneratedOutput` 仍发布持久 `_configs/output-manifests/localization-supported-languages.json`；它也必须列入 Action 写集和产物证据。

## Agent Action 来源与验收

消费端继续复用 `nova.project.localization.export`，不新增模板、语言或字段对应的 Action。`target=text` 支持 `all/code/data`；`target=languages` 仅支持 `data`。

- 文本所有范围都通过 `GetTopModule` 读取 `RuntimeProvider.GetNamespace()`，并同步源目录 `_configs/luban.conf`、`__tables__.xml`、`nova-export-manifest.json`。新 Plan 冻结持久 Master 的 GUID、路径、Namespace、原文件及 `Globals.json`；任何缺失、未保存或漂移均写前阻断。明确配置的空 Namespace 合法，生成来源不使用派生 Runtime 配置。
- Plan 冻结唯一已保存场景的组件快照、配置 Unit 对应的 Excel 字节、现存配置，以及实际读取的 Luban 工具目录。代码范围额外冻结文本和支持语言模板的完整成员、顺序来源；JSON `code` 还冻结第一语言的现有正式数据，用于 Map 属性生成。数据范围不把未读取的模板成员当成必要输入。
- 独立 `languages/data` 按既有递归扫描规则发现 Excel，排除 `_configs`、`_temp` 和 Office 锁文件；重新计算源集合与摘要，能发现源增删。它也调用 `GetTopModule`，但配置只写临时目录、不生成 C#，因此不强制 Master，也不声称验证代码 Namespace。
- WriteSet 包含正式数据、反格式文件及其 `.meta`（即使当时不存在）、代码目录内替换和清理、持久配置与两份代码清单，以及 `_temp` 和可能补建的目录元文件。`Execute` 只调用一个既有公开 Exporter，不复制算法或自动重放。
- 新 Receipt `sourceContractVersion=1` 同时携带设置、源摘要、输入快照、Namespace 来源、配置范围和执行后配置摘要。`Verify` 先重新解析当前活动场景的唯一组件，精确核对场景、组件身份、实时 JSON Hash 和设置；脏组件或切换活动场景会返回 partial，保存场景文件的 SHA 不能替代实时绑定。随后反推完整输入与输出集合，检查未被 Sync 改写的源、真实 target/manager/topModule、Manifest 及生成代码所有权；配置文件由 Sync 改写后核对新产物，不再与执行前旧字节比较。
- `text/code/all` 另核对支持语言三类型和独立输出清单。新代码收据必须通过其 `compilationToken` 对应的本次真实编译与脚本重载，再按生成 `.cs` 的实际 owner assembly 查精确类型，核对 `ILubanTables`/`ITable`，不实例化业务。单一 Editor 只保留最近一次编译记录，逐个执行、编译、Verify，不能积压收据。
- 旧 Receipt 缺全部新来源字段时继续只读摘要验收并返回明确边界 warning；出现新字段但值为 null、版本损坏或范围缺失时不能降级旧成功。纯代码导出成功证据保留 Static，并增加 Compile + Artifact，满足既有描述符要求；数据仍为 Static + Artifact。两者均不证明运行时本地化显示或字体适配。

## 契约影响

public API、Runtime API、Inspector 序列化字段均未变化。Action 新来源契约会阻断以前可在缺 Master、源或模板漂移时执行的文本导出；独立语言数据维持无 Master 的既有兼容边界。旧收据仅保留摘要含义，不继承新代码验收。

## 关联文档

- [本地化文本新增 Key 与语言](../../../Onboarding/LOCALIZATION_SOURCE_EDIT.md)：随包 CLI 专用源结构分支，正式导出继续复用本门面对应 Action。
- [LocalizationExcelPreFilter.md](../../DataPipeline/Implements/Localizations/LocalizationExcelPreFilter.md)
- [LocalizationTextExporter.md](../../DataPipeline/Implements/Localizations/LocalizationTextExporter.md)
- [EditorUtil.FileSystem.OutputApplier.md](../EditorUtil.FileSystem/EditorUtil.FileSystem.OutputApplier.md)
- [EditorUtil.Luban.GeneratedOutput.md](../EditorUtil.Luban/EditorUtil.Luban.GeneratedOutput.md)
- [EditorUtil.Luban.Pipeline.md](../EditorUtil.Luban/EditorUtil.Luban.Pipeline.md)
- [EditorUtil.Luban.SchemaManifest.md](../EditorUtil.Luban/EditorUtil.Luban.SchemaManifest.md)

## 文本门面的输入边界

DataTableSettingsAdapter 复制列表容器，Unit 对象仍共享；null 列表转空列表，getter 不生成或发布。文本 All 的空代码目录允许只输出数据与语言列表；Code 要求代码目录并可从旧正式数据生成 Map，缺种子不证明 Map 完整。文本 Data 不生成语言列表或类型。独立 ExportSupportedLanguages 默认 Json，Binary 要求 bytes，按兼容递归扫描发现语言，备份源也可能贡献语言；它没有 Settings 参数，不能继承按 Unit 冻结的含义。

门面原样传配置、源根、模板与路径，只有内部模型和受控 Action 才提供更严格核验；bool false 不保证此前从未更新工作配置，清理/IO 异常也可能外抛。
