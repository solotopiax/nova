---
id: GLO-06
title: Nova 框架设计模式映射
type: glossary
status: active
date: 2026-06-05
summary: 用通用设计模式理解 Nova 的主要结构与机制
category: arch
aliases:
  - design-patterns-map
keywords:
  - GLO-06
  - Nova 框架设计模式映射
  - design-patterns-map
tags:
  - glossary
  - nova
  - framework
  - design-patterns
related:
  - "[[ADR-001-component-manager-three-layer]]"
  - "[[ADR-002-manager-priority-system]]"
---

# Nova 框架设计模式映射

## 用途

- 给新成员一个“Nova 主要结构对应哪些通用模式”的速查表
- 帮助判断新机制是沿用旧模式，还是引入了新的结构
- 统一讨论框架结构时的抽象语言，减少“同一结构多套说法”

## 映射表

| 模式 | 在 Nova 中的体现 |
|---|---|
| Strategy | Manager 通过接口与类型注入切换实现 |
| Facade | `Nova` 把各模块聚合为统一静态入口 |
| Observer | Event 模块的订阅、派发与事件池 |
| Object Pool | `ReferencePool` 与 `IObjectPool<T>` |
| Factory | 通过统一创建入口获取实现或池化对象 |
| Plugin Registry | SDK 模块的插件发现、启用与初始化 |

## 使用方式

- 解释现有结构时，优先映射到这张表中的既有模式
- 如果一个新机制明显不属于现有映射，再考虑是否需要单独形成新的长期结构
- 讨论实际规则时，仍应回到对应 ADR / PAT，而不是只停留在模式名

## 注意

- 这张表是理解框架的辅助抽象，不是要求每个类都硬贴某个 GoF 标签
- 具体代码真相仍以 `Docs` 与源码为准
- 如果某个模式映射长期失真，应修正这页，而不是继续叠加口头例外
