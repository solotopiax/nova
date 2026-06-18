---
id: PAT-05
title: L0/L1/L2 三级文档体系 + S 级标准
type: pattern
status: active
date: 2026-05-14
summary: Docs 采用 L0/L1/L2 分层并保持互不重复
category: docs
aliases:
  - PAT-05
keywords:
  - PAT-05
  - L0/L1/L2 三级文档体系 + S 级标准
tags:
  - pattern
  - methodology
  - documentation
related:
  - "[[PAT-04-read-what-you-change|PAT-04]]"
  - "[[GLO-04-utility-classes|GLO-04]]"
---

# PAT-05：L0/L1/L2 三级文档体系 + S 级标准

## 适用场景

- 代码规模已大到不能只靠源码导航
- 需要给实现、Review 和日常调用提供稳定入口
- 希望 `Docs` 始终保持“当前事实层”而不是散笔记集合

## 核心结构

| 层级 | 文件 | 作用 |
|---|---|---|
| `L0` | `ARCHITECTURE.md` | 框架总览 |
| `L1` | `Runtime.md` / `Editor.md` | Runtime / Editor 导航 |
| `L2` | 各类 `<ClassName>.md` | 类、接口、窗口、组件事实说明 |
| 入口 | `INDEX.md` | 全局入口与任务导航 |

## 核心原则

- 上层不展开下层细节。
- 下层不重复上层背景。
- `Docs` 只写当前事实，不写历史决策。
- 新建、删除、重命名类时，`INDEX` 与父级导航一起同步。

## L2 文档要求

复杂类型保留较完整结构，至少覆盖：

- 文件拆分
- 公开 API
- 关键字段或关键状态
- 使用示例
- 关联文档

小类型允许极简，不强行凑满大模板。

## 反模式

- 每篇文档都重复同一套背景
- 小枚举和小接口硬套重模板
- 改代码不改文档
- 删代码却把文档留在 `Docs` 里当历史资料

## 关联

- [[PAT-04-read-what-you-change|PAT-04]]
- [[GLO-05-three-tier-docs]]
