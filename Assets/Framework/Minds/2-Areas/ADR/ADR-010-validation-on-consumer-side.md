---
id: ADR-010
title: 谁使用谁校验（Component 不做参数校验，下沉 Manager 层）
status: accepted
date: 2026-05-14
summary: 参数校验下沉到真正使用参数的 Manager
category: quality
aliases:
  - ADR-010
keywords: [ADR-010, 谁使用谁校验（Component 不做参数校验，下沉 Manager 层）]
tags: [adr, nova, validation, component, manager]
supersedes: []
superseded-by: []
related:
  - "[[ADR-001-component-manager-three-layer|ADR-001]]"
  - "[[PAT-08-architecture-antipatterns]]"
---

# ADR-010：谁使用谁校验（Component 不做参数校验，下沉 Manager 层）

## 背景

Nova 的 Component 本质上是门面层，真正消费参数并决定行为的是 Manager。  
如果 Component 和 Manager 同时校验，容易出现：

- 双层重复校验
- 不同模块错误处理风格不一致
- 接口扩展时需要双处同步

## 决策

**参数校验责任下沉到 Manager；Component 只做透传。**

责任划分：

| 层 | 职责 | 校验责任 |
|---|---|---|
| `{Xxx}Component` | 对外门面、生命周期承接 | 不做参数校验 |
| `{Xxx}Manager` | 实际业务实现与状态判断 | 负责参数、状态和合法性校验 |

统一原则：

- 谁真正使用参数，谁负责校验。
- Component 不做重复的空值、范围、状态判断。
- 校验失败优先走 Nova 既有风格：记录日志并返回安全值，而不是在门面层抢先抛异常。

## 后果

### 正面

- Component 更薄，职责更稳定。
- 校验逻辑只维护一处。
- 调用方对同类 API 的失败语义更容易形成统一预期。

### 负面

- 调用方需要习惯“日志 + 安全返回值”这一风格，而不是依赖门面层异常。
- 如果 Manager 的错误语义设计不好，返回值可能不够区分失败原因。

## 例外

- 编译期即可保证的类型约束，不属于本决策讨论范围。
- 生命周期未就绪这类问题，本质仍属于 Manager 自身状态校验，应在 Manager 内处理。

## 验证方式

- 检查 Component 是否只做透传。
- 检查 Manager 是否承担真实参数和状态校验。
- 检查同一条校验逻辑是否在门面层与实现层重复出现。

## 关联

- [[ADR-001-component-manager-three-layer|ADR-001]]
- [[ADR-002-manager-priority-system|ADR-002]]
- [[PAT-08-architecture-antipatterns]]
