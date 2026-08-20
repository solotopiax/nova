---
name: nova-project-integrate-table
description: Use when 项目组要把已确认的 Luban 表源接入现有 Nova 项目，完成精确导出、运行时加载配置并在 Play Mode 读取目标表时使用。
---

# Nova 接入数据表

触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。

## 渐进式披露

再读取 `references/contract.json`。仅在当前决策分支读取下列一页：确认 Project、导出描述或加载描述时读 `Docs/Runtime/Modules/Table/Definitions/TableSettings.md` 和 `Docs/Editor/Inspectors/TableComponentInspector/TableComponentInspector.md`；执行导出时读 `Docs/Editor/EditorUtil/EditorUtil.Table/EditorUtil.Table.Exporter.md`；排查或验证运行时加载时读 `Docs/Runtime/Modules/Table/TableComponent.md` 与 `Docs/Runtime/Modules/Table/TableManager.md`；复用既有 Batch 时才读 `Docs/Editor/EditorUtil/EditorUtil.Pipify/PipifySteps.md`。不要递归加载 Table、Luban 或 Pipify 的全部文档。

## 冻结输入与决策门

先冻结正式 `luban.conf`、Schema、Excel 数据源、目标 `ProjectId/DescriptionId`、代码/数据 Target、输出目录、运行时 `DataTarget`、每个 `output_data_file` 的 Asset 地址，以及可复现的目标表类型和读取样例。不要从 Sample、文件名或第一张 Sheet 猜测这些值。

- 表源、Schema、输出目录、DataTarget、Asset 地址或加载时序有多个合理候选时，先请求选择；无已确认的 Nova 根节点、`TableComponent` 或运行时加载描述时保持 `blocked`。
- 仅修改数据时，不重导代码；新增/修改表结构、Target、格式或代码输出时，才导出对应代码与数据。生成物不是手工编辑目标。
- 需要创建或更新 `TableSettings`、Prefab、场景引用或 Asset 地址时，只能通过 Unity Editor 自动化通道 和 TableComponent Inspector 写入，绝不手写 Prefab、Scene 或其他 Unity YAML。

## Input → Action Adapter → Artifact → Evidence

| Input | 现有 Action Adapter | Artifact | Evidence |
|---|---|---|---|
| 已冻结的 Schema、Excel 与表范围 | 项目现有且已确认的 Luban 数据源编辑入口 | 指定表源与 Schema | 变更只覆盖确认的表、Sheet、字段与行范围 |
| 已冻结的 Luban Project、描述和表源 | TableComponent Inspector 的 Luban Project / 导出描述 / 加载描述编辑入口 | `TableSettings` 中的 Project、导出描述、加载描述与显式 Asset 地址映射 | Unity 中目标设置与选定输入一致 |
| 已冻结的 `ProjectId/DescriptionId` 与输出范围 | `EditorUtil.Table.Exporter.ExportCode`、`ExportData` 或 `ExportAll`；复用已确认 Batch 时才用 `export.table.code` / `export.table.data` | 该描述的代码和数据正式产物 | Exporter 成功，且只出现本次确认范围的产物变化 |
| 已冻结的加载时序、表类型和读取样例 | 既有启动链调用 `Nova.Table.LoadAsync()` 或 `LoadSync()`，再用 `HasTable<T>()` / `GetTable<T>()` | 可查询的目标表 | Play Mode 中加载成功、存在目标类型并读取到指定行/字段 |

Pipify 的原子 Step 只处理所有已启用描述，不能代替精确 `ProjectId/DescriptionId` 选择；Batch 也是顺序执行，不能据此假定其他表链已经正确。

## 执行与验证边界

按“定向源编辑 → 设置 → 选定导出 → Unity 编译 → Play 读取”执行，任一阶段失败立即停止。Luban Exporter 会在独立工作区发布产物；不要为保险清理其他描述、其他格式或未确认目录。

最低成功证据是 Play Mode 中实际完成目标加载，并以 `Nova.Table.HasTable<T>()` 和 `GetTable<T>()` 读取到冻结的样例。只有导出或编译证据时返回 `partial`；表源、加载描述、Asset 地址或读取样例不明确时返回 `blocked`。不把静态文件存在、Luban 成功或 `Count` 非零单独称为接入成功。
