---
id: ADR-073
title: Excel 导出业务边界：仅 Table 通用，其他模块专用
summary: 仅 Table 通用，其他模块按业务语义定制
category: editor
status: accepted
date: 2026-07-17
aliases:
  - ADR-073-excel-export-business-boundary
keywords:
  - ADR-073
  - Excel 导出业务边界
  - Table 通用表格导出
  - 模块专用 Excel 导出
  - LocalizationExcelPreFilter
tags: [adr, nova, editor, excel, table, localization, data-pipeline]
supersedes: []
superseded-by: []
related:
  - "[[ADR-054-kit-config-three-dim-matrix|ADR-054]]"
  - "[[ADR-055-excel-source-into-demo-copies|ADR-055]]"
---

# ADR-073：Excel 导出业务边界：仅 Table 通用，其他模块专用

## 背景

Nova 的多个模块都使用 Excel 作为编辑期输入，但表格承载的业务语义不同。Table 面向常规数据表；Localization 需要解释语言列并投影按语言数据；Network 和 Config 也分别受自己的配置模型与导出契约约束。如果仅因输入格式相同就统一业务导出层，会隐藏模块规则、增加抽象理解成本，并让一个模块的变化扩散到其他模块。

## 决策

- 只有 Table 模块提供通用表格导出能力。
- Localization、Network、Config 等其他模块的 Excel 均视为模块专用输入，由对应模块负责解释列与 Sheet、执行校验、完成必要投影并定义产物。
- 模块之间可以共享无业务语义的基础设施，例如 Excel 读写、Luban 调用、SchemaManifest 和底层文件操作。
- 不把某个模块的 PreFilter、Exporter、OutputApplier 或临时目录约定提升为全模块必须接入的通用业务框架。
- Localization 的 `PreFilter -> Exporter -> Applier` 是本地化文本导出的内部职责划分，不是 Nova Excel 导出的统一分层。

## 后果

- 每个模块的导出链能够直接表达自身业务规则，开发者无需先理解一套跨模块抽象。
- 相似流程可能保留少量有意重复；只有确认不包含模块语义的能力才下沉共享。
- 新增非 Table 的 Excel 输入时，应先在所属模块建立专用契约，再选择性复用现有基础设施。
- 对 Table 通用导出能力的修改，不应默认改变 Localization、Network 或 Config 的行为。

## 被排除方案

- 建立所有模块共同继承或注册的通用 Excel 导出框架：输入格式相同不足以证明业务契约相同，统一后会把模块差异变成隐式分支。
- 直接复用 Localization 的三段式类型作为其他模块模板：这些类型的职责边界、暂存范围和失败语义均由本地化产物集合决定。
- 完全禁止共享：Excel 读写、Luban 和结构快照等无业务语义能力仍应复用，避免重复维护底层机制。

## 验证依据

- `Assets/Framework/Scripts/Editor/DataPipeline/AGENTS.md` 已明确 DataPipeline 不是大一统导出框架，并区分 Localization、Network 与 Config 的当前职责。
- `Assets/Framework/Docs/Editor/DataPipeline/DataPipeline.md` 记录各模块当前不同的数据流和产物边界。
- `ADR-054` 已证明 Network 的维度语义应回归 Config 模型，而不是长期固化在共享式 PreFilter 中。
- 本决策由 Nova 项目维护者于 2026-07-17 明确确认。
