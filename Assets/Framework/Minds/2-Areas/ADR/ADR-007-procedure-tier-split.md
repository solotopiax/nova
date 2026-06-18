---
id: ADR-007
title: Procedure 分档（框架内置 vs 业务 DLL 延迟注册）
status: accepted
date: 2026-05-14
summary: Procedure按AOT/业务DLL分档注册
category: hotfix
aliases:
  - ADR-007
keywords: [ADR-007, Procedure 分档（框架内置 vs 业务 DLL 延迟注册）]
tags: [adr, nova, hybridclr, procedure, fsm]
supersedes: []
superseded-by: []
related:
  - "[[ADR-005-hybridclr-namespace-single-write-path|ADR-005]]"
  - "[[ADR-006-novabehaviour-ibaselife-replace-monobehaviour|ADR-006]]"
  - "[[GLO-03-component-procedure-manager]]"
---

# ADR-007：Procedure 分档（框架内置 vs 业务 DLL 延迟注册）

## 背景（Context）

HybridCLR 改造后，Procedure 状态机面临两难：

- **框架内置 Procedure**（Launch/Splash/CheckVersion/Hotfix/LoadDll）必须在业务 DLL 加载之前就能运行，因为 LoadDll 自身就是其中一环——这些 Procedure 必须放在 AOT 层。
- **业务 Procedure**（Preload/Login/Playing 等）由业务团队迭代，必须放在业务 DLL 层以支持热更。
- `ProcedureComponent` 启动时通过程序集扫描注册 Procedure。如果业务 DLL 还没加载，扫描就拿不到业务 Procedure；如果延迟扫描，框架内置 Procedure 又会等不到。

附加约束：FSM 的 `Fsm.AddStates` 内有 `m_IsChangingState` 守卫，在 `OnLeave/OnEnter` 内部同步调用会抛 `InvalidOperationException`——而注册业务 Procedure 的最佳时机正是在 `ProcedureLoadDll.OnEnter` 内（DLL 已加载完成时刻）。

## 决策（Decision）

**Procedure 分两档，注册时机不同**。

### 1. 分档归属

| 分类 | 所在程序集 | Procedure 列表 | 注册方式 |
|---|---|---|---|
| 框架内置 Procedure | `NovaFramework.Runtime` | Launch / Splash / CheckVersion / Hotfix / LoadDll | `ProcedureComponent` 启动时程序集扫描 |
| 业务 Procedure | `Game.Runtime`（业务 DLL） | Preload / Login / Playing 等 | `ProcedureLoadDll` 在 DLL 加载完成后通过 `RegisterAdditionalProcedures` 延迟注册 |

**禁止混放**：

- 框架内置 Procedure 不得放入业务 DLL 层
- 业务 Procedure 不得放入 AOT 层程序集

### 2. UniTask.Yield() 脱栈方案

`Fsm.AddStates` 在 `OnLeave/OnEnter` 内同步调用会抛 `InvalidOperationException`，因此 `ProcedureLoadDll.OnEnter` 必须先脱栈：

```csharp
public override async UniTaskVoid OnEnter(IFsm<IProcedureManager> fsm)
{
    base.OnEnter(fsm);
    await LoadDll();
    await UniTask.Yield(); // 让 m_IsChangingState 回落
    RegisterAdditionalProcedures(); // 此时安全调用 AddStates
    // ...切到下一状态
}
```

`await UniTask.Yield()` 强制推迟到下一帧，确保 `m_IsChangingState` 已回落。

### 3. ProcedureLoadDll 配套约束

- `ProcedureLoadDll` 属于框架层（`NovaFramework.Runtime`），允许通过 `FrameworkManagersGroup.GetManager<IConfigManager>().LoadAsync()` 直接加载配置
- `IConfigManager.LoadAsync` 幂等（`m_IsLoadOver` 短路），业务层后续 `Nova.Config.LoadAsync()` 复用同一份已加载实例
- 旧的“配置预加载后再回写”链路已删除，配置由 `IConfigManager.LoadAsync()` 直接承担
- **禁止**在 `ProcedureLoadDll` 中使用 `Nova.Config.LoadAsync()`（`Nova.XXX` 是业务层公开入口，框架层走 `FrameworkManagersGroup`）

## 后果（Consequences）

### 正面

- 框架内置 Procedure 在 AOT 层，启动期立即可用，承担 LoadDll 自举工作。
- 业务 Procedure 留在业务 DLL，热更时随 DLL 一起更新。
- 延迟注册通过 `RegisterAdditionalProcedures` 显式调用，注册时机可追踪可调试。
- `UniTask.Yield()` 脱栈方案统一可复用，新增延迟注册场景照搬即可。

### 负面

- 业务作者需理解 Procedure 注册时机分档，新增 Procedure 时必须明确放哪一层。
- `ProcedureLoadDll` 与业务 DLL 之间存在隐式契约（业务 DLL 内必须有可被反射注册的 Procedure 类型），契约破坏只能运行时发现。
- 启动期框架层用 `FrameworkManagersGroup.GetManager<IConfigManager>()`、业务期用 `Nova.Config`——两条访问路径并存，新人易困惑（详见框架/业务访问分层铁律，[[ADR-008-managerbase-internal-abstract|ADR-008]] 配套约束）。

## 被排除的方案（Alternatives）

| 方案 | 否决理由 |
|---|---|
| 全部 Procedure 放 AOT 层 | 业务 Procedure 无法热更，违背改造初衷 |
| 全部 Procedure 放业务 DLL 层 | LoadDll 自身需要先于业务 DLL 运行，循环依赖 |
| 启动期一次性扫描所有程序集（含业务 DLL） | 业务 DLL 还未加载，扫描结果空 |
| `ProcedureLoadDll.OnEnter` 同步调用 `AddStates` | `m_IsChangingState` 守卫抛 `InvalidOperationException`，FSM 状态污染 |
| 用 `Task.Delay(0)` 代替 `UniTask.Yield()` | UniTask 是项目统一异步原语；混用 Task 不合规 |
| 通过事件回调延迟注册 | 增加事件耦合，注册时序不显式 |

## 验证依据（Verification）

- Grep 关键词：`RegisterAdditionalProcedures`、`UniTask.Yield()`、`ProcedureLoadDll`
- AOT 层 Procedure 巡检：`Assets/Framework/Scripts/Runtime/**/Procedures/` 应只保留框架启动链所需状态
- 业务 Procedure 巡检：业务 DLL 项目下 Procedure 类应通过 `RegisterAdditionalProcedures` 延迟注册，而非在框架启动阶段直接扫描

## 关联

- 相关 ADR：[[ADR-005-hybridclr-namespace-single-write-path|ADR-005]]（程序集与命名空间）、[[ADR-006-novabehaviour-ibaselife-replace-monobehaviour|ADR-006]]（MonoBehaviour 直挂替代旧桥接）
- 相关 Glossary：[[GLO-03-component-procedure-manager]]
