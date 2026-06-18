---
id: PAT-34
title: Minds 只沉淀 Nova Framework 本体长期知识
type: pattern
status: active
date: 2026-05-18
summary: Minds 只收录框架长期知识
category: governance
aliases:
  - PAT-34-minds-scope-nova-only
keywords:
  - PAT-34
  - Minds 作用域
  - Nova Framework 长期知识
tags:
  - pattern
  - governance
  - scope
related:
  - "[[PAT-05-l0-l1-l2-docs|PAT-05]]"
  - "[[PAT-30-framework-usage-redlines|PAT-30]]"
---

# PAT-34：Minds 只沉淀 Nova Framework 本体长期知识

## 适用场景

- 判断某条内容该写进 `Assets/Framework/Minds/` 还是只留在 `Docs/`、代码注释或历史归档
- 评估一篇旧文档是否仍应停留在 `2-Areas/`
- 处理 `Docs`、代码、知识条目三者边界时

## 核心规则

`Minds` 只收录 **Nova Framework 本体** 的长期知识，包括：

- 架构决策
- 强制术语
- 跨模块模式与反模式
- 对框架演进长期有效的审计结论

`Minds` 默认**不**收录：

- 当前版本 API 细节
- 局部实现说明
- 会话过程记录
- 协作工具操作说明
- 个人环境、命中策略、调度流程

## 判定标准

如果一条内容回答的是下面任一问题，就适合进入 `Minds`：

- 为什么框架要这样设计？
- 哪些术语必须统一？
- 哪些跨模块边界长期成立？
- 哪些模式或反模式值得反复复用？

如果一条内容回答的是下面问题，就不应进入 `Minds`：

- 这个类当前有哪些字段和方法？
- 这个版本的目录结构是什么？
- 这次会话具体怎么操作？
- 某个工具、脚本、命中机制该怎么跑？

## 写入边界

应写入 `Minds` 的内容：

- `NovaFramework.*` 范围内的长期规则
- 跨 Runtime / Editor / Procedure / SDK 的长期边界
- 已确认稳定的架构经验

不应写入 `Minds` 的内容：

- 业务侧自治规则
- 某一代协作工具专用流程
- 自动草稿、会话日志、命中痕迹
- 只对当前版本代码生效的类级说明

## 反模式

- 把当前 API 手册写进 `Minds`
- 把协作工具流程写成长期模式
- 把业务侧约束倒灌成框架长期规则
- 把历史归档继续停留在 `2-Areas/`

## 关联

- [[PAT-05-l0-l1-l2-docs|PAT-05]]：当前事实应主要落在 `Docs`
- [[PAT-30-framework-usage-redlines|PAT-30]]：框架边界类长期红线
