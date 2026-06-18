---
id: ADR-008
title: ManagerBase 一律 internal abstract（含 ProcedureManagerBase）
status: accepted
date: 2026-05-14
summary: 所有 ManagerBase 对外隐藏，只保留接口为公开入口
category: arch
aliases:
  - ADR-008
keywords: [ADR-008]
tags: [adr, nova, manager, encapsulation]
supersedes: []
superseded-by: []
related:
  - "[[ADR-001-component-manager-three-layer|ADR-001]]"
  - "[[GLO-02-framework-manager-tiers]]"
---

# ADR-008：ManagerBase 一律 internal abstract（含 ProcedureManagerBase）

## 背景

如果 `ManagerBase` 对外公开，业务层就可能直接依赖抽象骨架，绕过本该稳定的接口层。  
这样会让框架升级、继承链调整和内部重构都变得更脆弱。

## 决策

**所有 `{Xxx}ManagerBase` 一律使用 `internal abstract`；对外只暴露 `public interface I{Xxx}Manager`。**

包括：

- 常规模块 ManagerBase
- `ProcedureManagerBase`

## 后果

### 正面

- 对外访问路径收敛到接口层。
- 抽象骨架可以在框架内部自由演化。
- 三层结构的边界更清晰。

### 负面

- 测试和扩展不能再依赖派生 `ManagerBase` 这一条路，只能走接口替身或框架内部实现。

## 验证方式

- `ManagerBase` 不应以 `public abstract` 暴露。
- `ProcedureManagerBase` 不应成为例外。
- 业务代码不应直接触达 `ManagerBase`。

## 关联

- [[ADR-001-component-manager-three-layer|ADR-001]]
- [[GLO-02-framework-manager-tiers]]
