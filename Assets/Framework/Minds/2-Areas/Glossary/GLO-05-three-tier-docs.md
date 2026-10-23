---
id: GLO-05
title: 三级文档体系（L0 / L1 / L2 + INDEX）
type: glossary
status: active
date: 2026-05-14
summary: Docs 由 L0、L1、L2 与 INDEX 组成
category: docs
keywords: [ARCHITECTURE.md, Editor.md, GLO-05, INDEX.md, L0, L1, L2, Runtime.md, 文档同步]
tags: [glossary, nova, docs, architecture]
aliases:
  - L0
  - L1
  - L2
  - ARCHITECTURE.md
  - Runtime.md
  - Editor.md
  - INDEX.md
related:
  - "[[PAT-04-read-what-you-change]]"
  - "[[PAT-05-l0-l1-l2-docs]]"
  - "[[GLO-02-framework-manager-tiers]]"
---

# 三级文档体系（L0 / L1 / L2 + INDEX）

## 一句话定义

`Assets/Framework/Docs/` 采用 `L0 → L1 → L2 + INDEX` 的事实层结构。

这里的 `L0 / L1 / L2` 表示文档层级，与工程工作流使用的同名风险等级分别判断。

## 层级定义

| 层级 | 文件 | 职责 |
|---|---|---|
| `L0` | `ARCHITECTURE.md` | 框架总览与核心关系图 |
| `L1` | `Runtime.md` / `Editor.md` | Runtime / Editor 层级导航 |
| `L2` | 各类 `<ClassName>.md` | 类、接口、窗口、组件的事实说明 |
| `INDEX` | `INDEX.md` | 全局检索入口与任务导航 |

## 分工边界

- `Docs` 只描述当前实现事实。
- `Minds` 只沉淀长期决策、术语、模式和历史结论。
- `L0` 不替代 `L1`，`L1` 不替代 `L2`，`L2` 不去重写整套架构背景。
- 源码/XML、Docs、执行契约和自动索引各有唯一职责，分工见 [[PAT-05-l0-l1-l2-docs|PAT-05]]；不由人工函数知识文件复写。

## 使用规则

- 改大型类前，先看对应 `L2`。
- 只调用某模块时，优先看 `L2` 的公开 API 与示例。
- 文档失真时，以代码为准并回刷 `Docs`。
- 新增、删除、重命名类时，要同步更新 `INDEX` 和父级导航。

## 常见误解

- `Docs` 不是历史归档区；值得长期保留的决策与经验按 `Minds` 边界沉淀。历史验收结果、进度与临时日志不因此获得归档理由。
- `L2` 不是逐行翻译代码；它应该服务于定位和理解事实。
- `INDEX` 不是可有可无的目录页；它是默认入口的一部分。
- 自动索引、文档存在和源码指纹匹配不等于人工已审或运行通过。

## 来源与影响面

- [自然语言开发入口](../../../Docs/Onboarding/NATURAL_LANGUAGE_DEVELOPMENT.md)：当前文档、源码/XML、任务契约和定位索引的分工。
- 本条解释分层术语；实施规则由 [[PAT-05-l0-l1-l2-docs|PAT-05]] 与 [[PAT-116-cs-doc-mirror-sync|PAT-116]] 承接。

## 关联

- [[PAT-04-read-what-you-change]]
- [[PAT-05-l0-l1-l2-docs]]
- [[GLO-02-framework-manager-tiers]]
