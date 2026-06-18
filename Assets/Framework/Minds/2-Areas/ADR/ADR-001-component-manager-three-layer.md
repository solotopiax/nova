---
id: ADR-001
title: Component + Manager 三层继承链选型
status: accepted
date: 2026-05-14
summary: 固定 Component+Manager 三层结构
category: arch
aliases:
  - ADR-001
keywords: [ADR-001, Component + Manager 三层继承链选型]
tags: [adr, nova, architecture, component, manager]
supersedes: []
superseded-by: []
related:
  - "[[ADR-002-manager-priority-system|ADR-002]]"
  - "[[ADR-008-managerbase-internal-abstract|ADR-008]]"
  - "[[GLO-02-framework-manager-tiers]]"
  - "[[GLO-03-component-procedure-manager]]"
---

# ADR-001：Component + Manager 三层继承链选型

## 背景

Nova 需要一套稳定的运行时模块组织方式，同时满足：

- Unity 生命周期要有 `MonoBehaviour` 入口
- 业务逻辑要能脱离 `MonoBehaviour`
- 外部访问要面向接口而不是面向具体实现
- 具体实现要能被框架内部收口

## 决策

Nova 运行时模块采用固定三层：

```text
{Xxx}Component
  -> I{Xxx}Manager
  -> {Xxx}ManagerBase
  -> {Xxx}Manager
```

约束如下：

| 类型 | 典型修饰符 | 职责 |
|---|---|---|
| `I{Xxx}Manager` | `public interface` | 对外契约 |
| `{Xxx}ManagerBase` | `internal abstract` | 抽象骨架 |
| `{Xxx}Manager` | `internal sealed partial` | 唯一实现 |
| `{Xxx}Component` | `public sealed partial` | Unity 入口与门面 |

补充规则：

- `Component` 负责创建和持有接口，不直接承载业务实现。
- `Manager` 负责真实业务。
- `Manager` 由 `Util.TypeCreator.Create<I{Xxx}Manager>(...)` 创建，不直接 `new`。
- 跨模块访问优先走接口或 `Nova.Xxx` 入口。

## 后果

### 正面

- Unity 入口、业务实现、对外契约三者分离。
- 具体实现被框架内部收口，外部依赖更稳定。
- 新模块有统一模板，Review 更容易识别偏航。

### 负面

- 文件数量变多，模板成本更高。
- 新人需要理解三层职责差异，不能把逻辑随手塞进 Component。

## 验证方式

- 看模块是否同时具备 `Component / Interface / ManagerBase / Manager` 这组结构。
- 看 Component 是否只做门面和生命周期承接。
- 看 Manager 是否承担真实业务实现。

## 关联

- [[ADR-002-manager-priority-system|ADR-002]]
- [[ADR-008-managerbase-internal-abstract|ADR-008]]
- [[GLO-02-framework-manager-tiers]]
- [[GLO-03-component-procedure-manager]]
