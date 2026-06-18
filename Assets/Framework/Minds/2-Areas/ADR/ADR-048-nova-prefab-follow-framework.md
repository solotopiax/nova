---
id: ADR-048
title: Nova.prefab 跟主框架版本走（amends ADR-033）
summary: sample scene 引用主框架包内 prefab 实例
category: module
status: accepted
date: 2026-05-28
aliases:
  - ADR-048-nova-prefab-follow-framework
  - ADR-048
keywords:
  - ADR-048-nova-prefab-follow-framework
  - Nova.prefab 跟主框架版本走（amends ADR-033）
  - ADR-048
tags: [adr, nova, sample, upm, prefab]
supersedes: []
superseded-by: []
amends:
  - "[[ADR-033-maindemo-isolated-topology|ADR-033]]"
related:
  - "[[ADR-033-maindemo-isolated-topology|ADR-033]]"
  - "[[PAT-63-upm-sample-readonly-prefab-path-override|PAT-63]]"
---

# ADR-048：Nova.prefab 跟主框架版本走（amends ADR-033）

## 背景

每个 sample 自带一份 Nova.prefab 会让代码 / 序列化字段漂移成 N 份，重构成本过高。

## 决策

- Nova.prefab 只保留在主框架包内。
- sample scene 直接引用主框架包内 prefab GUID。
- sample 差异通过 PrefabInstance Override 表达。

## 影响

- 代码 / 配置升级回到单一来源。
- 16 份副本不再同时维护。
- sample 脱离主框架后不再具备独立运行能力。
