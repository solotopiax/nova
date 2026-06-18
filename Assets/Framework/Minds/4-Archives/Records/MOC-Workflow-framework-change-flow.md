---
id: MOC-Workflow
title: Framework 变更工作流图谱（归档）
summary: 旧版框架变更流程图谱
category: workflow
status: archived
date: 2026-06-05
archived-date: 2026-06-08
aliases:
  - MOC-Workflow
  - Framework 变更工作流
  - 框架工作流图谱
tags:
  - moc
  - nova
  - workflow
keywords:
  - Framework 变更工作流
  - public API
  - 序列化
  - 生命周期
  - 文档同步
related:
  - "[[PAT-30-framework-usage-redlines|PAT-30]]"
  - "[[PAT-31-inspector-sop|PAT-31]]"
  - "[[PAT-32-runtime-module-sop|PAT-32]]"
---

# MOC-Workflow：Framework 变更工作流图谱

本图谱只描述 Nova Framework 自身的工程变更流程，不描述任何特定协作工具、自动化触发器或本地工作流。

该条目已由仓库级 `.nova/WORKFLOW.md` 替代，保留在归档区只作为旧入口设计的历史参考。

## 适用场景

- 修改框架公开 API
- 修改 Inspector 字段或序列化结构
- 修改生命周期语义、初始化顺序、模块边界
- 新建或重构 Runtime / Editor 模块
- 做模块级审计、复盘或归档

## 标准流程

1. 先确认变更落点，判断属于 Runtime、Editor、Config、UI、Asset、SDK、Procedure 还是 HybridCLR。
2. 先查 `Docs`，确认当前版本的模块事实、公开 API、字段和流程。
3. 如果改动涉及术语、架构、跨模块契约或历史争议，再查 `Minds`。
4. 按现有结构实施改动，避免在同类模块中引入新的结构风格。
5. 运行最小验证：编译、受影响 Inspector、受影响流程或示例。
6. 同步文档；只有在产生新决策、新术语或长期模式时，才同步 `Minds`。

## 变更分级

| 类型 | 主要依据 | 默认动作 |
|------|----------|----------|
| 局部实现改动 | 不改 public API、不改序列化、不改生命周期 | 查 `Docs` 后直接实施 |
| 契约改动 | 改 public API、Inspector 字段、配置结构、模块对外行为 | 先查 `Docs`，再查相关 `ADR` / `Glossary` / `MOC` |
| 架构改动 | 改模块边界、初始化顺序、依赖方向、长期结构 | 必查 `Minds`，必要时沉淀新 `ADR` |
| 历史审计 | 对照旧快照、归档、复盘记录 | 查 `Snapshots` 与 `4-Archives/` |

## 同步原则

- 当前行为变化，先同步 `Docs`
- 长期知识变化，再同步 `Minds`
- 普通小修复不新增 `ADR`、`PAT`、`GLO`
- 会话过程、工具过程、排查中间态不沉淀到 `Minds`

## 验证基线

- 编译无新增错误
- 受影响 Inspector 正常显示
- 受影响模块入口、配置、流程或示例可运行
- 对外行为变化已经同步到对应文档
