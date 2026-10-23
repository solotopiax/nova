# UIExporter

**类签名**：`internal static class UIExporter`

**命名空间**：`NovaFramework.Editor`

`UIExporter` 是 UI 模块的内部导出编排器。它校验 UISettings 和 UI Excel，构造 Luban 暂存输出，验证完整性后直接通过通用 `FileSystem.OutputApplier` 发布当前格式数据/C# 文件。

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
  -> Luban 生成当前格式数据、C# 和 Map 属性
  -> 验证暂存产物并登记替换、精确删除项
  -> FileSystem.OutputApplier.Apply()
  -> 持有工作区租约清理 _temp 并刷新 AssetDatabase，最后释放租约
```

所有 Unit 必须使用同一个 `ClassesExportPath`，因为 `UITables.cs` 是 UI 模块的全局注册表类型。单文件导出必须精确匹配 Unit，不会退化成全量正式发布。

## 职责边界

负责：

- UI Settings、Excel 契约和输出路径校验。
- Luban 导出上下文及暂存路径构造。
- 当前格式数据、C#、Map 和预期文件完整性验证。
- 决定本批次需要替换或删除的正式文件。

不负责：

- 对外提供稳定 API；该职责属于 `EditorUtil.UI.Exporter`。
- 实现文件备份、替换和失败回滚；该职责属于 `FileSystem.OutputApplier`。
- Runtime UI 加载、UIGroup 或视图实例生命周期。

## 失败语义

- 工作区租约获取失败：返回失败，不清理已有 `_temp` 或 `.meta`，不运行 Pipeline 或刷新；避免删除当前租约持有者的暂存产物。
- 成功取得租约后，生成、发布、清场和刷新均在同一租约内；最外层 `finally` 释放租约，即使刷新抛错也不会遗留租约。只保护当前 Editor 进程，不提供跨进程锁或文件系统事务。
- 应用前失败：正式数据/C# 保持不变。
- Luban Pipeline 在应用正式数据/C# 前同步源目录 `_configs`；该配置同步不在 OutputApplier 正式产物事务内，失败时可能已更新配置文件，不能据 `false` 宣称整个工作区不变。
- 应用中失败：`FileSystem.OutputApplier` 按逆序恢复已执行项。
- 全量代码清理只删除 Profile、Source 和正文 Hash 均匹配的过期 `.cs` 及其 `.meta`；其余文件保留并报警。
- `_temp`、`_publish`、备份和目标旁 `.tmp` 都是短生命周期临时内容。

## 测试替换点

嵌套 `ExportOperations` 只用于 Editor 测试替换 Excel 校验、Luban Pipeline 和 AssetDatabase 刷新，不保存业务状态，也不构成额外生产抽象层。

受控自然语言导出复用 `nova.project.ui.export`，准确范围与编译后 Receipt 验证见公共门面文档；内部 ExportOperations 不作为任意执行或反射入口。

## 关联文档

- [EditorUtil.FileSystem.OutputApplier.md](../../../EditorUtil/EditorUtil.FileSystem/EditorUtil.FileSystem.OutputApplier.md)
- [EditorUtil.Luban.GeneratedOutput.md](../../../EditorUtil/EditorUtil.Luban/EditorUtil.Luban.GeneratedOutput.md)
- [EditorUtil.UI.Exporter.md](../../../EditorUtil/EditorUtil.UI/EditorUtil.UI.Exporter.md)
- [DataPipeline.md](../../DataPipeline.md)
- [EditorUtil.Luban.Pipeline.md](../../../EditorUtil/EditorUtil.Luban/EditorUtil.Luban.Pipeline.md)

## 源、目标与校验范围

所有范围包括单文件仍校全部 Unit/Excel；Name 跨文件/Sheet 精确唯一，必需列和 bool 解析按当前 UI schema，但不核 row0/row3 标记、资源真实存在、组或 Prefab 绑定，空数据也可能通过。注释行并无统一跳过，重复表头取首列。

数据/类路径仅文本规范及忽略大小写比较，a/../b 与 b 等真实同目标可延后到 Apply 才冲突；SourcePath 重复取首，不把纯文本比较当物理身份。单代码可用显式类目录但仍拒多配置类目录；单数据也受全表/Asset地址约束。克隆只映射当前必要字段，context 使用显式源根和固定 UI Profile，不消费 clone 的旧 TemplatePath。新注册 JSON Map 依旧数据种子，原 API 不保证完整，Action 才有更严格门。
