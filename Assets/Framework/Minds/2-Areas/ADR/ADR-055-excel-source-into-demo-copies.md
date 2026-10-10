---
id: ADR-055
title: Excel 源数据迁入各 Demo 独立副本，路径生命周期收敛单链
summary: Excel 源移进各 Demo 副本，删 Docs 搬运
category: asset
status: accepted
date: 2026-05-31
aliases:
  - ADR-055-excel-source-into-demo-copies
keywords:
  - ADR-055
  - Excel 源数据迁入各 Demo 独立副本，路径生命周期收敛单链
  - ADR-055-excel-source-into-demo-copies
tags: [adr, nova, sample, publish, asset]
supersedes: []
superseded-by: []
related:
  - "[[ADR-033-maindemo-isolated-topology|ADR-033]]"
  - "[[ADR-048-nova-prefab-follow-framework|ADR-048]]"
  - "[[PAT-121-publish-sample-rewrite-symmetric|PAT-121]]"
  - "[[PAT-63-upm-sample-readonly-prefab-path-override|PAT-63]]"
---

# ADR-055：Excel 源数据迁入各 Demo 独立副本，路径生命周期收敛单链

## 背景

Excel 源数据过去放在 repo-root 的 Docs 目录，发版链路里还要再搬运到 sample 包，路径层级太长。

## 决策

- Excel 源数据迁到各 Demo 的私有 `Assets/Samples/<Demo>/Excels/` 副本。
- `Docs/Designs` 和旧的 Proto 源目录删除。
- Nova.prefab 默认值直接指向 MainDemo 副本，其他 Demo 用 scene override。
- 发版时只保留一条路径生命周期：默认值 -> override -> import 重写。
- 独立副本不仅隔离路径，也必须隔离业务表内容。单主题 Demo 的 UI Excel、导出数据、生成表属性、场景分组与入口 Prefab 保持一致；不能仅修正 JSON 而保留 MainDemo 的 Excel 条目。

## 影响

- 各 Demo 可以独立编辑自己的表数据。
- 发版链路从双机制收敛成单链。
- 多 Demo 共享同一份表时会出现有意冗余。

## 来源与验证依据

- `Assets/Framework/Docs/Onboarding/RESOURCE_WORKFLOW.md` 的 Sample UI 表源与导出约定。
- AppsflyerDemo 的历史错配说明了边界：源表长期保留 MainDemo 条目，即使初始 JSON 正确，后续正式导出仍会覆盖它。修复须从 `Assets/Samples/AppsflyerDemo/Excels/UIs/UIs.xlsx` 向下同步正式数据和生成代码。
- UI 专用正式导出入口：`Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.UI/EditorUtil.UI.Exporter.cs`。
