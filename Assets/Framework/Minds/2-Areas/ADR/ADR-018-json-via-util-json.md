---
id: ADR-018
title: JSON 序列化统一走 Util.Json，类型定义限用 Newtonsoft.Json
status: accepted
date: 2026-05-16
summary: JSON 读写统一走 Util.Json
category: core
aliases:
  - ADR-018-json-via-util-json
keywords: [ADR-018, ADR-018-json-via-util-json]
tags: [adr, nova, framework, serialization, json]
supersedes: []
superseded-by: []
related:
  - "[[PAT-08-architecture-antipatterns|PAT-08]]"
---

# ADR-018：JSON 序列化统一走 Util.Json，类型定义限用 Newtonsoft.Json

## 背景

Nova 在 Runtime、Editor、配置导出、存档和热更新链路里都要处理 JSON。  
如果不同位置各用一套 JSON 入口，行为差异和排障成本都会迅速上升。

## 决策

### 1. JSON 读写统一走 `Util.Json`

- 序列化：`Util.Json`
- 反序列化：`Util.Json`

### 2. JSON 节点类型只允许使用 `Newtonsoft.Json` 的定义

例如：

- `JObject`
- `JArray`
- `JToken`

### 3. 其他直接 JSON 读写入口在框架层不作为默认方案

目标不是禁止一切第三方节点类型，而是把“真正的序列化策略”收口到 `Util.Json`。

## 后果

### 正面

- Runtime 与 Editor 的 JSON 行为更一致。
- JSON 策略可以集中演化，而不是散落在各模块。
- Review 时更容易判断是否绕开了统一入口。

### 负面

- 新成员需要先理解 `Util.Json` 这套封装，而不是直接按个人习惯选库。

## 验证方式

- 检查 JSON 读写是否统一收口到 `Util.Json`。
- 检查是否有人直接在框架代码里散落多套 JSON 序列化入口。

## 关联

- [[PAT-08-architecture-antipatterns|PAT-08]]
- [[GLO-04-utility-classes]]
