---
name: nova-project-integrate-table
description: Use when 项目组要接入或调整已确认的 Luban 表源数据、追加普通字段，并完成对应导出、加载配置与游戏读取验证时使用。
---

# Nova 接入数据表

触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。

## 渐进式披露

再读取 `references/contract.json`。仅在当前决策分支读取下列一页：确认 Project、导出描述或加载描述时读 `Docs/Runtime/Modules/Table/Definitions/TableSettings.md` 和 `Docs/Editor/Inspectors/TableComponentInspector/TableComponentInspector.md`；执行导出时直接调用 `nova.project.table.export`，只有 Action 报告配置问题时才读 `Docs/Editor/EditorUtil/EditorUtil.Table/EditorUtil.Table.Exporter.md`；排查或验证运行时加载时读 `Docs/Runtime/Modules/Table/TableComponent.md` 与 `Docs/Runtime/Modules/Table/TableManager.md`。不要递归加载 Table、Luban 或 Pipify 的全部文档。

用户要修改已有 Excel/CSV 的数据，或给已自描述 xlsx 追加普通字段时，读取[按行键编辑表源](references/source-edit.md)，复用随包 `Agents/Tools/nova_table_source.py` 的 `inspect → plan → execute → verify`；不为每次改数值临时重写表格脚本。

需要设置 Project、导出/加载描述或业务读取入口时，读[设置与实际消费绑定](references/settings-and-consumption.md)，复用已有 `settings.inspect-target/patch-target` 或共享 Unity MCP 的精确字段/保存路线，并在业务源码中复用公开加载查询 API。Inspector 类型和 C# API 仅说明底层事实，不是可直接调用的方法工具。

## 冻结输入与决策门

先冻结正式 `luban.conf`、Schema、Excel 数据源、目标 `ProjectId/DescriptionId`、代码/数据 Target、输出目录、运行时 `DataTarget`、每个 `output_data_file` 的 Asset 地址，以及可复现的目标表类型和读取样例。不要从 Sample、文件名或第一张 Sheet 猜测这些值。

这些身份、路径和当前数据先从项目有效 TableSettings、`luban.conf`、Schema 与实际表源读取；让新手只说明业务目标和无法消歧的决策。已有数据修改按真实行键/字段定位，冻结原值及整文件摘要，不按 Excel 行号猜目标。

- 表源、Schema、输出目录、DataTarget、Asset 地址或加载时序有多个合理候选时，先请求选择；无已确认的 Nova 根节点、`TableComponent` 或运行时加载描述时保持 `blocked`。
- 仅修改数据时，不重导代码；新增/修改表结构、Target、格式或代码输出时，才导出对应代码与数据。生成物不是手工编辑目标。
- 需要创建或更新 `TableSettings`、Prefab、场景引用或 Asset 地址时，只能通过 Unity Editor 自动化通道 和 TableComponent Inspector 写入，绝不手写 Prefab、Scene 或其他 Unity YAML。

## Input → Action Adapter → Artifact → Evidence

| Input | 现有 Action Adapter | Artifact | Evidence |
|---|---|---|---|
| 已冻结的行键、字段、类型、原值、文件摘要和输出边界 | 随包 `nova_table_source.py` 处理已有标量或 `layout=table-fields` 严格普通字段追加；复杂结构使用已确认 Schema 入口 | 指定表源 | CLI Verify 精确值/文件摘要一致；不伪造正式导出 |
| 已冻结的 Luban Project、描述和表源 | 受控 settings Action 或已声明共享 Unity MCP 精确字段及单文件保存，详 settings-and-consumption reference | `TableSettings` 中的 Project、导出描述、加载描述与显式 Asset 地址映射 | 独立读回目标设置和落盘文件；不调用私有 Inspector 方法 |
| 已冻结的 `ProjectId/DescriptionId` 与输出范围 | `nova.project.table.export` | 该描述的代码和数据正式产物 | Action Verify 成功，且只出现 Plan 范围内的产物变化 |
| 已冻结的加载时序、表类型和读取样例 | 授权业务源码复用既有启动链；共享 Unity 工具运行已保存入口并观察 `LoadAsync/LoadSync`、`HasTable/GetTable` | 可查询的目标表 | Play Mode 中加载成功、存在目标类型并读取到指定行/字段；不是任意 C# 即时执行 |

只要求导出、不要求完整接入和 Play 验证时，直接转入 `nova-project-export-tables`，不要执行本 Skill 的完整接入流程。

## 执行与验证边界

按“定向源编辑 → 设置 → 选定导出 → Unity 编译 → Play 读取”执行，任一阶段失败立即停止。Luban Exporter 会在独立工作区发布产物；不要为保险清理其他描述、其他格式或未确认目录。

最低成功证据是 Play Mode 中实际完成目标加载，并以 `Nova.Table.HasTable<T>()` 和 `GetTable<T>()` 读取到冻结的样例。只有导出或编译证据时返回 `partial`；表源、加载描述、Asset 地址或读取样例不明确时返回 `blocked`。不把静态文件存在、Luban 成功或 `Count` 非零单独称为接入成功。
