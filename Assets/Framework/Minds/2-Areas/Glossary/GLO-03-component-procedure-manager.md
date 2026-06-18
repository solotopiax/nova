---
id: GLO-03
title: Component / Procedure / Manager 职责边界
type: glossary
status: active
date: 2026-05-14
summary: 三者职责分工
category: arch
keywords: [Component, Component / Procedure / Manager 职责边界, FSM, GLO-03, Manager 职责, Procedure]
tags: [glossary, nova, architecture, procedure, component]
aliases:
  - Component
  - Procedure
  - Manager 职责
related:
  - "[[ADR-001-component-manager-three-layer]]"
  - "[[ADR-007-procedure-tier-split]]"
  - "[[GLO-02-framework-manager-tiers]]"
---

# Component / Procedure / Manager 职责边界

## 一句话定义

Component 是 Unity 入口，Manager 是纯 C# 实现，Procedure 是流程编排者。

## 边界

| 角色 | 本质 | 主要职责 |
|---|---|---|
| Component | `MonoBehaviour` | 生命周期入口、门面、持有接口 |
| Manager | 纯 C# | 模块的真实业务实现 |
| Procedure | FSM 状态 | 组织步骤、等待结果、切换流程 |

## 判断口诀

- Component 负责“接进来”
- Manager 负责“真正做”
- Procedure 负责“按顺序串起来”

## 常见错误

- 把业务逻辑直接写进 Component
- 让 Procedure 去实现具体资源加载、网络请求或 SDK 细节
- 让 Manager 反过来承担流程编排职责

## 关联

- [[ADR-001-component-manager-three-layer]]
- [[ADR-007-procedure-tier-split]]
- [[GLO-02-framework-manager-tiers]]
