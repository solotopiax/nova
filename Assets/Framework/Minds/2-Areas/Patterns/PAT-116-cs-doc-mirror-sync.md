---
id: PAT-116
title: cs↔Docs 镜像同步铁律
summary: 代码事实变化时必须同轮刷新 Docs 镜像
category: docs
type: pattern
status: active
date: 2026-05-27
aliases:
  - PAT-116
keywords:
  - PAT-116
  - cs↔Docs 镜像同步铁律
tags: [pattern, methodology, docs, refactor]
related:
  - "[[PAT-62-readme-changelog-dual-sync|PAT-62]]"
  - "[[PAT-109-upm-package-docs-mandatory|PAT-109]]"
---

# PAT-116：cs↔Docs 镜像同步铁律

## 适用场景

- 删类、删接口、删目录
- 重命名类、命名空间、路径或 asmdef
- 改继承链、公开 API、字段表、生命周期
- 模块目录重构

## 核心原则

`Assets/Framework/Scripts/` 与 `Assets/Framework/Docs/` 是**当前事实镜像**。  
只要代码事实变了，同一轮就必须把对应 `Docs` 刷新到一致；不存在“先改代码，文档下轮再补”的合法窗口。

## 最小执行要求

每次命中上述场景，至少要做三件事：

1. 更新对应类文档或模块文档
2. 清理旧类名、旧路径、旧术语在 `Docs` 中的残留
3. 修复 `INDEX`、模块索引、交叉链接

## 验收口径

- 旧类名和旧路径不再残留在 `Docs` 主工作面
- 被删文档不再被 `ARCHITECTURE.md`、`INDEX.md` 或兄弟模块引用
- `Docs` 目录结构与 `Scripts` 结构保持可追踪映射
- 空目录和无效入口被一并清理

## 这样做的原因

- `Docs` 一旦滞后，就会把下一轮实现工作拖回“重新读源码”的高成本模式。
- 失效链接和旧术语会污染检索，放大上下文噪音。
- 同轮同步最省成本，因为改动边界和影响面此刻最清楚。

## 反模式

- 先改代码，文档留到下个任务
- 只改当前类文档，不扫索引和交叉链接
- 把已删除代码的文档留在 `Docs` 里当历史资料
- 文档改写脱离代码事实，只按记忆补

## 关联

- [[PAT-62-readme-changelog-dual-sync|PAT-62]]
- [[PAT-109-upm-package-docs-mandatory|PAT-109]]
