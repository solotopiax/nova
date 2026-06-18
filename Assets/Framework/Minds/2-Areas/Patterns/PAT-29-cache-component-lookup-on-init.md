---
id: PAT-29
title: FrameworkComponentsGroup.GetComponent<T> 必须缓存到成员，禁止热路径反复调用
type: pattern
status: active
date: 2026-05-17
summary: Component依赖在Init一次缓存禁运行查找
category: runtime
aliases:
  - PAT-29-cache-component-lookup-on-init
keywords:
  - PAT-29
  - PAT-29-cache-component-lookup-on-init
tags:
  - pattern
  - nova
  - performance
  - procedure
  - component
related:
  - "[[ADR-001-component-manager-three-layer|ADR-001]]"
---

# PAT-29：FrameworkComponentsGroup.GetComponent<T> 必须缓存到成员，禁止热路径反复调用

## 适用场景（When）

任何 Procedure / Manager / Component 内需要多次访问 `FrameworkComponentsGroup.GetComponent<XXXComponent>()` / `FrameworkManagersGroup.GetManager<IXxxManager>()` 的代码。

典型命中点：

- Procedure 在 `OnEnter` / `OnUpdate` / `OnLeave` 等多次回调中访问同一 Component
- Manager 在 `Update` 等高频路径访问跨模块 Manager
- Component 内部多个方法都要拿同一目标 Component

## 核心做法（What & How）

**初始化时一次性缓存到成员字段，后续路径只读字段，不再调 `Get*` 入口。**

Procedure 模板（推荐位置：`OnInit`）：

```csharp
public sealed class ProcedureXxx : ProcedureBase
{
    private ProcedureComponent m_ProcedureComponent;
    private AssetComponent m_AssetComponent;
    private LaunchComponent m_LaunchComponent;

    protected internal override void OnInit(ProcedureOwner procedureOwner)
    {
        base.OnInit(procedureOwner);
        m_ProcedureComponent = FrameworkComponentsGroup.GetComponent<ProcedureComponent>();
        m_AssetComponent = FrameworkComponentsGroup.GetComponent<AssetComponent>();
        m_LaunchComponent = FrameworkComponentsGroup.GetComponent<LaunchComponent>();
    }

    protected internal override void OnEnter(ProcedureOwner procedureOwner)
    {
        // 直接用 m_LaunchComponent / m_AssetComponent，不再调 GetComponent
    }
}
```

Manager 模板：在 `Initialize` 中缓存（构造器无参不允许访问跨模块）。

## 为什么这么做（Why）

- `FrameworkComponentsGroup.GetComponent<T>` 内部走 `Dictionary<Type, FrameworkComponent>` 查找，单次开销不大但**累计在 OnUpdate 这种每帧调用的路径上是浪费**。
- **可读性**：OnEnter 顶部一连串 `var x = GetComponent<X>()` 的体力活会淹没本流程的真实业务逻辑。集中到 `OnInit` 后，业务方法只见 `m_X` 字段，意图清晰。
- **失败提前暴露**：如果某个目标 Component 缺失，`OnInit` 期间就报，不是要等到 OnEnter/OnUpdate 真正用到才报。

## 反模式（Anti-patterns）

| 反模式 | 后果 |
|---|---|
| `OnUpdate` 里反复 `FrameworkComponentsGroup.GetComponent<XxxComponent>()` | 每帧字典查找；意图被淹没 |
| 把 Component 缓存到 static 字段 | 跨场景/重启不重置，容易引用悬空 |
| 缓存"派生值"（例如 `int m_RetryCount = component.RetryCount`） | 派生值脱离源头，源头变更时不会同步；本次会话明确"缓存 component 实例本身，不缓存派生值"（用户原话） |
| Awake 里访问其他 Component | Unity 不保证其他 Component 的 Awake 已完成；应在 Start/OnInit 中访问 |

## 跨项目复用提示

通用 Unity 模式，只要项目用「全局 Component 注册表 + 反射查询」就适用。具体到 Nova，承载者有两个：`FrameworkComponentsGroup`（Component）+ `FrameworkManagersGroup`（Manager），同一约束。

