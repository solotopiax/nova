---
id: ADR-012
title: 三方插件信息禁止越层暴露
status: accepted
date: 2026-05-15
summary: 需要隔离的第三方品牌信息不得穿透到对外层
category: arch
aliases:
  - ADR-012-third-party-info-isolation
keywords: [ADR-012, ADR-012-third-party-info-isolation, 三方插件信息禁止越层暴露]
tags: [adr, nova, architecture, isolation]
supersedes: []
superseded-by: []
related:
  - "[[ADR-001-component-manager-three-layer|ADR-001]]"
---

# ADR-012：三方插件信息禁止越层暴露

## 背景

Nova 的三层结构本来就用于隔离实现细节。  
如果第三方品牌名、第三方类型或第三方概念词出现在对外层，调用方就会对底层实现形成隐性绑定。

这里的“越层暴露”不只指代码签名，也包括：

- 注释和 XML 文档
- 配置命名
- Inspector 标签和枚举命名

## 决策

**需要隔离的第三方信息，不得穿透到非实现层。**

当前已明确纳入隔离范围的第三方品牌：

- `YooAsset`

允许出现的位置：

- 对应模块 Manager 的实现层
- 必须直接调用该第三方 API 的实现代码

禁止出现的位置：

- `I{Xxx}Manager` 等对外接口层
- `{Xxx}Component`
- 共享 `Definitions`
- 面向框架使用者的对外文档与术语层

不在本 ADR 直接管控范围内的内容：

- Unity 引擎核心 API 与 `com.unity.*` 官方概念
- Nova 自身概念
- 其他未被显式纳入隔离清单的第三方库

## 后果

### 正面

- 更换底层库时，影响范围更集中在实现层。
- 对外暴露的术语更稳定，业务侧记住的是能力，不是厂商名。

### 负面

- 需要为底层实现设计更语义化的对外命名。
- Review 时不能只看编译通过，还要看术语与注释是否泄漏实现细节。

## 验证方式

- grep 被隔离品牌名，应只在实现层命中。
- Review 接口、配置、注释与文档时，要把第三方品牌泄漏视为结构性问题，而不是普通命名问题。

## 关联

- [[ADR-001-component-manager-three-layer|ADR-001]]
