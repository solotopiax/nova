---
id: ADR-039
title: BaseDemoView 废弃 SetApiHint 改双接口就近拆解
summary: 废 SetApiHint；改 hint 就近双色拆解
category: inspector
status: accepted
date: 2026-05-26
aliases:
  - ADR-039-base-demo-view-api-hint-split
keywords:
  - ADR-039
  - BaseDemoView 废弃 SetApiHint 改双接口就近拆解
  - ADR-039-base-demo-view-api-hint-split
tags: [adr, nova, sample, demo, ui]
supersedes: []
superseded-by: []
related: []
---

# ADR-039：BaseDemoView 废弃 SetApiHint 改双接口就近拆解

## 背景

标题区单行 ApiHintText 装不下多个 API，也看不出哪条 API 对应哪个交互元素。

## 决策

- 删除标题区 `ApiHintText` 节点与 `SetApiHint(string)` 接口。
- 改用 `SetButtonApiHint(Button, string)` 和 `SetFieldApiHint(TMP_Text, string)`。
- 按钮和字段分别就近挂 hint，保持一接口一提示。

## 影响

- API hint 不再被截断，页面也更干净。
- 纯快照 view 不必再强行显示 hint。
- 旧 view 需要一次性迁移。
