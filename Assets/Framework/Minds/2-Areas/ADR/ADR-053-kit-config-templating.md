---
id: ADR-053
title: Kit 配置模板化（IKitConfig + ConfigWindow Kit 一级组 + Kit 入口极简收口）
summary: Kit 固有配置模板化，入口收口为极简形态
category: module
status: accepted
date: 2026-05-31
aliases:
  - ADR-053
keywords:
  - ADR-053
  - Kit 配置模板化（IKitConfig + ConfigWindow Kit 一级组 + Kit 入口极简收口）
tags: [adr, nova, config, kit, network]
supersedes:
  - "[[ADR-044-network-kit-dual-overload|ADR-044]]"
superseded-by: []
related:
  - "[[ADR-022-sdk-plugin-architecture]]"
  - "[[ADR-043-gamesave-full-explicit-flag]]"
  - "[[ADR-044-network-kit-dual-overload]]"
---

# ADR-053：Kit 配置模板化

## 背景

Kit 入口原本每次调用都要显式传 `cmdName` / `channel`，这些值其实是固有配置，不该反复出现在业务调用面。

## 决策

- 新增独立 `IKitConfig` 体系，和 `ISDKPluginConfig` 保持同形但不合并。
- Kit 配置类落各 Kit 子包，由 ConfigWindow 统一扫描、展示和导出。
- `ConfigMasterSO` / `ConfigRuntimeSO` 增加 Kit 配置与启用白名单，运行时通过 `GetKitConfig<T>()` 读取。
- Login / GameSave 收口为极简 API，cmdName 与 channel 由配置层提供。

## 影响

- 业务调用从 `Async("GameLogin", channel, openId)` 收口到 `Async(openId)`。
- Kit 与 SDK 的配置体验对称，后续扩展更统一。
- Kit 配置类型名变更是破坏性变更，不能当作无成本重命名处理。

## 关联

- supersedes `ADR-044`
- 相关 ADR：`ADR-043`、`ADR-054`
