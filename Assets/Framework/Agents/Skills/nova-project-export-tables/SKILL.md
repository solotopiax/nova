---
name: nova-project-export-tables
description: Use when 项目组只需要从当前有效 TableSettings 导出全部或指定的 Table 代码、数据，并核验生成结果时使用。
---

# Nova 导出 Tables

触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。

## 渐进式披露

先读取 `references/contract.json`，再通过 `nova_project_action` 对 `nova.project.table.export` 执行 `describe`。仅在 Action 报告配置或范围不明确时，才读取 `Docs/Editor/EditorUtil/EditorUtil.Table/EditorUtil.Table.Exporter.md` 和相关 TableSettings 文档；不要为一次明确导出重新审计完整表接入链。

## 直接执行

- 用户说“导出 Tables”“生成 Table 数据和类型”且未限定范围时，`scope=all`，`projectId` 与 `descriptionIds` 留空，表示导出当前有效 TableSettings 中全部已启用描述。
- 用户明确只导出代码或数据时，分别使用 `scope=code` 或 `scope=data`。
- 用户指定 Project 或 Description 时原样传入，不猜测 ID。
- 调用固定链路：`describe -> plan -> execute -> verify`。写入前展示 Plan 的实际输出范围；确认后执行。
- 不退化为任意 C#、反射或临时脚本。Action 未注册、未开放或无法唯一解析 TableSettings 时返回 `blocked`。

## 完成标准

`verify` 必须确认计划内代码/数据输出及摘要一致；包含代码导出时还要等待 Unity 稳定并确认无编译错误。这里只验收导出，不强制进入 Play Mode，也不把它说成完整的 Table 运行时接入成功。
