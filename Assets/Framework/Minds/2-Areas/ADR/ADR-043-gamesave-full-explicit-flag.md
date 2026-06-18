---
id: ADR-043
title: GameSave 全量/非全量由 proto full 字段显式区分
summary: 协议 full=true 是全量唯一信号，废弃 keys 空判
category: module
status: accepted
date: 2026-05-27
aliases:
  - ADR-043
keywords:
  - ADR-043
  - GameSave 全量/非全量由 proto full 字段显式区分
tags: [adr, nova, network, gamesave, protobuf]
supersedes: []
superseded-by: []
related: []
---

# ADR-043：GameSave 全量/非全量由 proto full 字段显式区分

## 背景

原先靠 keys/datas 为空隐式判断全量，语义和校验都不清楚。

## 决策

- 协议层新增 `bool full` 字段。
- API 层拆出全量和非全量入口。
- 非全量调用必须先校验参数，不通过就直接失败。
- 删除旧的隐式回退和兼容壳。

## 影响

- 全量语义更明确。
- 调用签名更直接，减少误用。
- 这是协议级破坏性变更，需配套升级。
