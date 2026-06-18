---
id: ADR-032
title: 放弃 NovaBehaviour 桥接，回归 HybridCLR 原生 MonoBehaviour
summary: 热更脚本回归原生 MonoBehaviour
category: hotfix
status: accepted
date: 2026-06-05
aliases:
  - ADR-032-drop-novabehaviour-bridge
keywords: [ADR-032, HybridCLR, MonoBehaviour, NovaBehaviour]
tags: [adr, nova, hybridclr, hotfix, monobehaviour]
supersedes:
  - "[[ADR-006-novabehaviour-ibaselife-replace-monobehaviour|ADR-006]]"
  - "[[ADR-009-uimanager-no-addcomponent-fallback|ADR-009]]"
related:
  - "[[ADR-028-hybridclr-copy-aot-after-buildplayer|ADR-028]]"
  - "[[ADR-007-procedure-tier-split|ADR-007]]"
---

# ADR-032：放弃 NovaBehaviour 桥接，回归 HybridCLR 原生 MonoBehaviour

## 背景

Nova 曾经走过一套“业务热更脚本不直接继承 `MonoBehaviour`，而是通过桥接层代理 Unity 生命周期”的方案。  
这套方案的前提是：认为 HybridCLR 不适合直接承载热更 `MonoBehaviour` 与 Prefab 直挂。

后续验证表明，这个前提不成立。继续保留桥接层，只会把业务开发强行拉离 Unity / HybridCLR 的主流用法。

## 决策

Nova 不再要求业务热更脚本通过 `NovaBehaviour` 一类桥接层接入。  
当前基线是：

1. 业务热更行为类可以直接继承 `MonoBehaviour`
2. Prefab 可以直接挂载业务热更脚本
3. 运行时允许使用原生的 `AddComponent<T>()`、`Instantiate(prefab)` 等工作流
4. 热更 DLL 的加载顺序由框架启动链路保证，而不是由桥接层保证

## 当前代码落点

- `ProcedureLoadDll` 负责加载 AOT 元数据与业务 DLL
- `ProcedureManager.RegisterAdditionalProcedures()` 负责把额外流程注册进当前流程系统
- `Util.HybridCLR.LoadAotMetadataAsync()` 与 `LoadGameAssemblyAsync()` 是 DLL 链路的核心入口

## 后果

### 正面

- 业务脚本写法回到 Unity / HybridCLR 主流工作流
- Prefab、Scene、组件挂载关系更直观
- 减少字符串映射、桥接代理、反射中转带来的维护成本

### 负面

- 框架必须更严格地保证 DLL 加载早于业务侧热更对象消费
- 启动链路、流程切换和资源装配的时序要求更明确

## 不再保留的旧认知

- “热更脚本不能直接挂 Prefab”
- “热更行为必须先过桥接层”
- “业务层不能按原生 Unity 方式添加热更组件”

## 关联

- [[ADR-028-hybridclr-copy-aot-after-buildplayer|ADR-028]]
- [[ADR-007-procedure-tier-split|ADR-007]]
