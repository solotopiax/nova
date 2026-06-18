---
id: ADR-040
title: 跨 asmdef 写共享状态禁绕跳板字段
summary: 跨 asmdef 写共享状态走静态门面禁多层跳板
category: arch
status: accepted
date: 2026-05-26
aliases:
  - ADR-040
  - ADR-040-cross-asmdef-no-jump-board-state
keywords:
  - ADR-040
  - 跨 asmdef 写共享状态禁绕跳板字段
  - ADR-040-cross-asmdef-no-jump-board-state
tags: [adr, nova, arch, network, kit]
supersedes: []
superseded-by: []
related:
  - "[[ADR-016-framework-vs-business-access|ADR-016]]"
  - "[[ADR-020-assembly-dependency-direction|ADR-020]]"
  - "[[PAT-08-architecture-antipatterns|PAT-08]]"
---

# ADR-040：跨 asmdef 写共享状态禁绕跳板字段

## 背景

Kit 包之间的共享状态写入，曾经通过基类跳板、主框架转发和静态门面三层绕来绕去，链路长且容易出错。

## 决策

- 共享状态字段由拥有它的 Kit 包以静态属性 + 静态写入方法直接持有。
- 其他 Kit 包需要写入时，直接调用目标门面，不经过中间转发。
- 禁止在 Service 基类上加 `protected static UpdateXxx` 这类跳板。

## 影响

- 写入路径变短，基类更干净。
- 新增共享状态字段不需要改基类。
- 状态一致性更可控。
