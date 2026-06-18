---
id: ADR-045
title: SetFullAsync 全量上传 value 复用 datas[0].Value（Key 空串）
summary: 全量上传载荷复用 datas[0]，Key 留空串
category: module
status: accepted
date: 2026-05-27
aliases:
  - ADR-045
keywords:
  - ADR-045
  - SetFullAsync 全量上传 value 复用 datas[0].Value（Key 空串）
tags: [adr, nova, network, gamesave, protobuf]
supersedes: []
superseded-by: []
related: []
---

# ADR-045：SetFullAsync 全量上传 value 复用 datas[0].Value（Key 空串）

## 背景

全量上传需要一个顶层载荷，但现有 proto 只有 `datas`，没有单独 `value` 字段。

## 决策

- `SetFullAsync` 复用 `datas[0].Value` 作为全量载荷。
- `full == true` 时只读这个值，`value` 不允许为空。
- 非全量仍走原 `datas` 列表。

## 影响

- 不改 proto schema。
- 协议字段最少。
- 服务端需要按约定只读 `Value`。
