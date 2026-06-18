---
id: ADR-050
title: Hotfix 整批失败的用户决策与两层重试机制
summary: 整批失败弹窗重试，单文件与用户重试两层独立计数
category: hotfix
status: accepted
date: 2026-05-29
aliases:
  - ADR-050-hotfix-batch-fail-user-decision
keywords:
  - ADR-050
  - Hotfix 整批失败的用户决策与两层重试机制
  - ADR-050-hotfix-batch-fail-user-decision
tags:
  - nova
  - procedure
  - asset
  - hotfix
---

# ADR-050：Hotfix 整批失败的用户决策与两层重试机制

## 背景

热更下载同时存在单文件失败和整批失败，两者的重试语义不能混在一起。

## 决策

- 单文件自动重试与用户手动重试分开计数。
- 整批失败后弹出重试 / 取消决策。
- 取消时按 `QuitOnFailedOrCancel` 决定退出还是跳过。
- 不保留额外兼容壳。

## 影响

- 用户决策更清晰。
- 重试逻辑不会和内部下载重试互相干扰。
- 失败时的退出 / 继续路径更明确。
