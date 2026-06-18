---
id: ADR-062
title: Proto 引用公共 header 统一用 NovaFramework.Runtime
status: accepted
summary: Proto 公共 header 引用统一去 Kit 段
category: arch
date: 2026-06-03
aliases:
  - ADR-062-proto-header-namespace-convention
keywords:
  - ADR-062
  - Proto 引用公共 header 统一用 NovaFramework.Runtime
  - ADR-062-proto-header-namespace-convention
tags:
  - nova
  - network
  - proto
---

# ADR-062：Proto 引用公共 header 统一用 NovaFramework.Runtime

## 背景

重跑 protoc 时发现公共 header 的引用路径还带着旧的 `Kit.Network` 段，和实际 package namespace 不一致。

## 决策

- 所有 .proto 公共 header 引用统一写 `NovaFramework.Runtime`。
- `channel` 等公共字段收口到 `PbNetReqHeader`，由 header 构建逻辑统一注入。
- UPM 副本只保留 import 占位，不重复生成代码。

## 影响

- 重新生成时不会再因为 namespace 写错而报错。
- 业务侧公开 API 不变。
- UPM 副本和主份 proto 需要手工保持同步。
