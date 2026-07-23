# LocalizationTextExporter

**类签名**：`internal static class LocalizationTextExporter`

**命名空间**：`NovaFramework.Editor`

`LocalizationTextExporter` 是 Localization 文本导出的内部流程编排器。它决定各阶段的执行顺序和失败边界，把 Excel 解析与 CSV 投影交给 `LocalizationExcelPreFilter`，把正式文件替换、删除和回滚直接交给通用 `FileSystem.OutputApplier`。

这条 `PreFilter -> Exporter -> Applier` 调用链只服务本地化文本导出。它不是 Nova 的通用 Excel 导出分层；其他模块应保留自己的表格语义和导出流程，只复用无业务语义的底层能力。

## 调用关系

```text
EditorUtil.Localization.TextExporter（公共门面）
  -> LocalizationTextExporter（流程编排）
  -> LocalizationExcelPreFilter（加载、校验、投影 CSV）
  -> Luban.GeneratedOutput（C# 所有权与安全清理）
  -> FileSystem.OutputApplier（应用正式产物、失败回滚）
```

Exporter 是唯一知道完整导出顺序的组件；两个基础设施只接收路径和已确定的产物集合，不解释本地化业务。

## 内部入口

```csharp
internal static bool ExportAll(...);
internal static bool ExportCode(...);
internal static bool ExportData(...);
internal static bool ExportSupportedLanguages(...);
```

公开 API 仍由 `EditorUtil.Localization.TextExporter` 提供，以上入口不构成 Runtime 或外部程序集契约。

## 全量导出流程

```text
取得源目录重入门
  -> 加载并验证 Settings 配置的 Excel
  -> 按语言投影 _temp CSV
  -> Luban 生成各语言数据到 _temp/_publish
  -> Luban 生成代码到 _temp/_publish/code~
  -> 生成并验证 Map 属性
  -> 暂存支持语言列表
  -> 登记过期语言文件的精确删除项
  -> FileSystem.OutputApplier.Apply()
  -> finally 清理 _temp 并刷新 AssetDatabase
```

完整导出必须先验证所有暂存产物，再调用 `Apply()`。因此预过滤、Luban、代码或 Map 阶段失败时，不会修改正式产物。

## 职责边界

负责：

- 编排数据、代码、Map、语言列表和旧语言清理的先后顺序。
- 为 Luban 构造暂存路径和导出上下文。
- 验证 Luban 输出、Map Key 和预期文件是否完整。
- 在所有阶段成功后登记并应用正式文件变更。
- 维护 `_temp` 生命周期和同一源目录的重入边界。

不负责：

- 解析 Excel 单元格、判断语言表头是否合法或生成 CSV 行。
- 实现文件备份、原子单文件替换或失败回滚。
- Runtime 加载本地化数据。

## 失败语义

- `Apply()` 前失败：正式 JSON、C#、Map 和支持语言列表保持不变。
- `Apply()` 中失败：由 `FileSystem.OutputApplier` 逆序恢复已经执行的替换和删除。
- 回滚也失败：异常继续向公共门面传播并记录，正式目录可能处于部分恢复状态。
- `finally` 始终尝试清理 `_temp`；清理失败不会被静默忽略。

## 测试替换点

嵌套类型 `ExportOperations` 只为 Editor 测试替换 Excel 加载、Luban、Map 生成和 AssetDatabase 刷新调用。它不保存导出状态，也不包含业务规则，因此不形成第四个生产脚本或公共抽象层。

## 关联文档

- [LocalizationExcelPreFilter.md](LocalizationExcelPreFilter.md)
- [EditorUtil.FileSystem.OutputApplier.md](../../../EditorUtil/EditorUtil.FileSystem/EditorUtil.FileSystem.OutputApplier.md)
- [EditorUtil.Luban.GeneratedOutput.md](../../../EditorUtil/EditorUtil.Luban/EditorUtil.Luban.GeneratedOutput.md)
- [EditorUtil.Localization.TextExporter.md](../../../EditorUtil/EditorUtil.Localization/EditorUtil.Localization.TextExporter.md)
- [EditorUtil.Luban.Pipeline.md](../../../EditorUtil/EditorUtil.Luban/EditorUtil.Luban.Pipeline.md)
