---
id: ADR-057
title: 基础网络编排层从独立 Kit 包下沉进框架 Network 模块
summary: 基础网络编排下沉框架，删 Kit 基础包
category: arch
status: accepted
date: 2026-06-02
aliases:
  - ADR-057-network-kit-base-sink-into-framework
keywords:
  - ADR-057
  - 基础网络编排层从独立 Kit 包下沉进框架 Network 模块
  - ADR-057-network-kit-base-sink-into-framework
tags: [adr, nova, network, kit]
supersedes: []
superseded-by: []
related:
  - "[[ADR-020-assembly-dependency-direction|ADR-020]]"
  - "[[ADR-044-network-kit-dual-overload|ADR-044]]"
  - "[[PAT-108-upm-kit-public-api-collapse|PAT-108]]"
  - "[[MOC-Network|MOC-Network]]"
---

# ADR-057：基础网络编排层从独立 Kit 包下沉进框架 Network 模块

## 背景

基础网络编排原本独立成一个 Kit 包，但它本质上是框架原生能力，不应该多包维护。

## 决策

- 只下沉基础包 `kit.network`，login / gamesave 仍保留独立业务包。
- API 形态不改，`Kit<T>()` 和相关门面调用方式继续保留。
- 旧基础包删除，namespace 统一回归框架程序集。

## 影响

- 少一个 UPM 包，维护面更小。
- 跨包桥接的必要性消失，但兼容形态保留。
- 业务协议层和框架编排层边界更清楚。
