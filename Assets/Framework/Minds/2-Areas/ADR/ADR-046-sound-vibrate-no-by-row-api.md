---
id: ADR-046
title: Sound/Vibrate 模块对外 API 不暴露按行重载
summary: 表配置按名称访问，替代输入显式命名
category: module
status: accepted
date: 2026-05-27
aliases:
  - ADR-046
keywords:
  - ADR-046
  - Sound/Vibrate 模块对外 API 不暴露按行重载
tags: [adr, nova, sound, vibrate, api]
supersedes: []
superseded-by: []
related:
  - "[[ADR-016-framework-vs-business-access|ADR-016]]"
  - "[[PAT-104-no-obsolete-shim-rule|PAT-104]]"
  - "[[PAT-111-api-naming-avoid-host-module-literal|PAT-111]]"
---

# ADR-046：Sound/Vibrate 模块对外 API 不暴露按行重载

## 背景

Sound / Vibrate 走的是私有 Luban 词典，业务侧未必持有 row，本来就不适合暴露 by-row API。

## 决策

- 表配置入口按表内 `Name` 暴露，不要求业务方持有 row。Sound 的地址入口明确命名 `ByAsset`，只能唯一反查已加载表行，不能绕过表。
- Vibrate 的手工数值入口明确命名 `ByParams`，与按名称播放表中序列区分；不把手工默认值套到表行。
- 删除 by-row 重载和对应调用点。
- 不保留 `[Obsolete]` 兼容壳。

## 影响

- 名称入口与替代输入的含义由方法名区分；表内字段仍由表决定，运行时控制不覆盖表内配置。
- 业务侧如果持有 row，先取 name 再调用。
- 这是破坏性变更，需一次性迁移。
