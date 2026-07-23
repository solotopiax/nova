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

public static bool ExportSupportedLanguages(string sourceDirPath, string exportPath);
```

## 全量导出顺序

```text
获取 SourceDir 重入门
  -> 从 Settings.Units 构建并完整验证 LocalizationExcelPreFilter.SourceModel
  -> 清理旧 _temp
  -> 逐语言生成 CSV 并把 JSON 暂存到 _temp/_publish/data
  -> 生成代码到 Unity 忽略的 _temp/_publish/code~
  -> 使用暂存 JSON 与暂存代码生成、验证 Map 属性
  -> 暂存支持语言 JSON
  -> 注册精确的旧语言 JSON 删除项
  -> Luban.GeneratedOutput 为 C# 写入第一行单行所有权标记
  -> FileSystem.OutputApplier 一次应用全部替换与删除
  -> finally 清理 _temp 并刷新 AssetDatabase
```

任一语言、Luban、代码、Map 或产物验证失败都会立即终止；调用 `Apply` 前不会修改正式 JSON、C# 或支持语言列表。应用中途失败时，已替换和已删除文件按逆序从备份恢复；回滚也失败会抛出聚合异常并由门面记录一次失败。

## 路径和清理契约

- `_temp` 在 Excel 源目录下；导出开始前和结束后都清理。
- CSV 保留配置源的相对 stem，避免不同子目录同名 xlsx 互相覆盖。
- 暂存 `.cs` 放在 `code~`，避免正式应用前被 Unity 导入编译。
- 旧语言清理只展开每个 Unit 的 `DatasExportPath.Replace("{0}", language)` 精确路径，不使用通配删除。
- 独立 `ExportSupportedLanguages` 只发布语言列表，不删除任何语言数据；因其 public API 没有 Settings 参数，语言发现仍走兼容递归扫描。
- `_configs` 是 Luban 可再生工作缓存，不属于正式产物应用批次；`_temp`、`.tmp` 和备份均不得残留。
- `_configs/output-manifests/localization-text.json` 只记录最近一次全量代码产物清单；它可以重建，不参与过期文件删除判断。

## 契约影响

public API、Runtime API、Inspector 序列化字段均未变化。行为变化只发生在 Editor 导出内部：配置外 xlsx 不再参与完整导出，失败改为 fail-fast，正式产物改为成批发布。

## 关联文档

- [LocalizationExcelPreFilter.md](../../DataPipeline/Implements/Localizations/LocalizationExcelPreFilter.md)
- [LocalizationTextExporter.md](../../DataPipeline/Implements/Localizations/LocalizationTextExporter.md)
- [EditorUtil.FileSystem.OutputApplier.md](../EditorUtil.FileSystem/EditorUtil.FileSystem.OutputApplier.md)
- [EditorUtil.Luban.GeneratedOutput.md](../EditorUtil.Luban/EditorUtil.Luban.GeneratedOutput.md)
- [EditorUtil.Luban.Pipeline.md](../EditorUtil.Luban/EditorUtil.Luban.Pipeline.md)
- [EditorUtil.Luban.SchemaManifest.md](../EditorUtil.Luban/EditorUtil.Luban.SchemaManifest.md)
