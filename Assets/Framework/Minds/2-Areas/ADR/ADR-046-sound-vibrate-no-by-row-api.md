---
id: ADR-046
title: Sound/Vibrate 模块对外 API 不暴露按行重载
summary: Sound/Vibrate 对外 API 仅按名重载
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

- 仅保留按名重载。
- 删除 by-row 重载和对应调用点。
- 不保留 `[Obsolete]` 兼容壳。

## 影响

- 对外 API 更窄，调用链更简单。
- 业务侧如果持有 row，先取 name 再调用。
- 这是破坏性变更，需一次性迁移。
