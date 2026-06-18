---
id: PAT-08
title: 架构反模式红旗清单
type: pattern
status: active
date: 2026-05-14
summary: 用红旗清单快速识别 Nova 架构偏航
category: arch
aliases:
  - PAT-08
keywords:
  - PAT-08
  - 架构反模式红旗清单
tags:
  - pattern
  - methodology
  - architecture
  - anti-pattern
related:
  - "[[ADR-008-managerbase-internal-abstract|ADR-008]]"
---

# PAT-08：架构反模式红旗清单

## 适用场景

- 评审设计方案
- 评审 PR
- 模块重构前做自检

## 红旗清单

| 红旗 | 典型信号 |
|---|---|
| God Object | 一个类同时承担过多职责 |
| Tight Coupling | 模块直接依赖具体实现而不是接口 |
| Logic In MonoBehaviour | 业务逻辑直接堆进 Component / MonoBehaviour |
| Cross-Layer Leak | 实现细节或第三方品牌信息穿透到对外层 |
| Runtime -> Editor | Runtime 代码反向依赖 Editor |
| Magic Global | 依赖未声明、规则靠隐式约定维持 |
| Copy-Paste | 相似逻辑靠复制扩散 |
| Async Disorder | 异步链路没有清晰的等待、取消和失败语义 |

## Nova 里的重点观察项

- 是否破坏 `Component -> Interface -> Manager` 结构
- 是否直接 `new` 具体 Manager
- 是否让 Component 承担真实业务
- 是否把第三方品牌名、类型名带到对外层
- 是否让 Runtime 触达 Editor API

## 使用方式

- 看到红旗，不把它当“风格差异”，而要当结构风险。
- 发现红旗后，要同时给出替代写法，而不是只报错不指路。

## 关联

- [[ADR-001-component-manager-three-layer|ADR-001]]
- [[ADR-008-managerbase-internal-abstract|ADR-008]]
- [[ADR-012-third-party-info-isolation|ADR-012]]
