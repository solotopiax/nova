# UIExporter

**类签名**：`internal static class UIExporter`

**命名空间**：`NovaFramework.Editor`

`UIExporter` 是 UI 模块的内部导出编排器。它校验 UISettings 和 UI Excel，构造 Luban 暂存输出，验证完整性后直接通过通用 `FileSystem.OutputApplier` 发布正式 JSON/C# 文件。

UI Excel 已经采用 Luban 原生表结构，因此不需要 Localization 的 `ExcelPreFilter`。UI 专用校验由 `UIExporter.UIExcelValidator` 完成，不额外增加没有结构投影职责的脚本。

## 调用关系

```text
Inspector / Pipify
  -> EditorUtil.UI.Exporter（公共门面）
  -> UIExporter（校验与流程编排）
  -> Luban.GeneratedOutput（C# 所有权与安全清理）
  -> FileSystem.OutputApplier（正式产物应用与失败回滚）
```

## 内部入口

```csharp
internal static bool ExportAll(...);
internal static bool ExportCode(...);
internal static bool ExportData(...);
internal static bool ExportCodeForFile(...);
internal static bool ExportDataForFile(...);
```

公开 API 仍由 `EditorUtil.UI.Exporter` 提供，以上入口只服务 Editor 内部编排和测试。

## 全量流程

```text
校验 Unit、输出路径和 Asset 地址
  -> 校验全部非 # Sheet 的必需列、类型、值和 Name 唯一性
  -> 将数据与代码输出改写到源目录 _temp/_publish
  -> Luban 生成 JSON、C# 和 Map 属性
  -> 验证暂存产物并登记替换、精确删除项
  -> FileSystem.OutputApplier.Apply()
  -> finally 清理 _temp 并刷新 AssetDatabase
```

所有 Unit 必须使用同一个 `ClassesExportPath`，因为 `UITables.cs` 是 UI 模块的全局注册表类型。单文件导出必须精确匹配 Unit，不会退化成全量正式发布。

## 职责边界

负责：

- UI Settings、Excel 契约和输出路径校验。
- Luban 导出上下文及暂存路径构造。
- JSON、C#、Map 和预期文件完整性验证。
- 决定本批次需要替换或删除的正式文件。

不负责：

- 对外提供稳定 API；该职责属于 `EditorUtil.UI.Exporter`。
- 实现文件备份、替换和失败回滚；该职责属于 `FileSystem.OutputApplier`。
- Runtime UI 加载、UIGroup 或视图实例生命周期。

## 失败语义

- 应用前失败：正式 JSON/C# 保持不变。
- 应用中失败：`FileSystem.OutputApplier` 按逆序恢复已执行项。
- 全量代码清理只删除 Profile、Source 和正文 Hash 均匹配的过期 `.cs` 及其 `.meta`；其余文件保留并报警。
- `_temp`、`_publish`、备份和目标旁 `.tmp` 都是短生命周期临时内容。

## 测试替换点

嵌套 `ExportOperations` 只用于 Editor 测试替换 Excel 校验、Luban Pipeline 和 AssetDatabase 刷新，不保存业务状态，也不构成额外生产抽象层。

## 关联文档

- [EditorUtil.FileSystem.OutputApplier.md](../../../EditorUtil/EditorUtil.FileSystem/EditorUtil.FileSystem.OutputApplier.md)
- [EditorUtil.Luban.GeneratedOutput.md](../../../EditorUtil/EditorUtil.Luban/EditorUtil.Luban.GeneratedOutput.md)
- [EditorUtil.UI.Exporter.md](../../../EditorUtil/EditorUtil.UI/EditorUtil.UI.Exporter.md)
- [DataPipeline.md](../../DataPipeline.md)
- [EditorUtil.Luban.Pipeline.md](../../../EditorUtil/EditorUtil.Luban/EditorUtil.Luban.Pipeline.md)
