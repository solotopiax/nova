---
id: MOC-Procedure
title: Procedure & FSM 图谱
summary: 启动流程、Procedure 分档与 FSM 守卫速查
category: arch
status: active
date: 2026-06-05
aliases:
  - MOC-Procedure
  - Procedure图谱
  - FSM图谱
tags: [moc, nova, procedure, fsm, hybridclr, splash]
keywords: [Procedure, ProcedureManager, ProcedureBase, Fsm, ProcedureSplash, ProcedureCheckVersion, ProcedureHotfix, ProcedureAppDownload, ProcedureLoadDll]
related:
  - "[[ADR-007-procedure-tier-split|ADR-007]]"
  - "[[ADR-013-hotfix-master-switch|ADR-013]]"
  - "[[ADR-015-merge-launch-into-splash|ADR-015]]"
  - "[[ADR-024-launch-to-app-rename|ADR-024]]"
  - "[[PAT-44-procedure-checkversion-two-stage|PAT-44]]"
  - "[[GLO-03-component-procedure-manager|GLO-03]]"
---

# MOC-Procedure：Procedure & FSM 图谱

## 一句话

Procedure 模块负责 Nova 启动链路和流程状态切换；框架内置 Procedure 在 AOT 层，业务 Procedure 在 DLL 加载后再注册进入同一个 FSM。

## 何时查这页

- 要改启动顺序或新增 Procedure
- 要理解 `Splash / CheckVersion / Hotfix / AppDownload / LoadDll`
- 要判断某个 Procedure 应该属于框架层还是业务层

## 当前结构

```text
Nova.Procedure
  -> ProcedureComponent
  -> IProcedureManager
  -> ProcedureManagerBase
  -> ProcedureManager
     -> Fsm<IProcedureManager>
```

## 启动主链

```text
ProcedureSplash
  -> ProcedureCheckVersion

ProcedureCheckVersion
  -> ForcedDownload ? ProcedureAppDownload
  -> RecommendedDownload ? ProcedureAppDownload
  -> !EnableHotfix ? ProcedureLoadDll
  -> hasPatch ? ProcedureHotfix
  -> else ProcedureLoadDll

ProcedureHotfix
  -> ProcedureLoadDll

ProcedureLoadDll
  -> 注册业务 Procedure
  -> 跳业务入口 Procedure
```

- `ProcedureAppDownload` 在推荐更新取消且存在补丁时，会回到 `ProcedureHotfix`
- `ProcedureAppDownload` 在推荐更新取消且无补丁时，会回到 `ProcedureLoadDll`
- `Splash` 跨框架启动链路存活，正常情况下由 `ProcedureLoadDll.OnLeave` 销毁

## 分档边界

- 框架 Procedure：AOT / `NovaFramework.Runtime`
- 业务 Procedure：业务 DLL，`ProcedureLoadDll` 扫描后批量注册
- 框架启动链路不能依赖业务 DLL 才存在
- 业务入口 Procedure 不能提前塞进 AOT 层冒充框架流程

## 需要记住的守卫

- `OnEnter` / `OnLeave` 的同步栈内不能直接做 `AddStates`
- `ProcedureLoadDll` 先 `await UniTask.Yield()`，就是为了避开 `m_IsChangingState` 守卫
- `ProcedureLoadDll` 用 `FrameworkManagersGroup.GetManager<IConfigManager>()`
- 这里不直接走 `Nova.Config` 门面

## 与其他模块的关系

- `App`：`ProcedureCheckVersion` 调 `IAppManager.CheckAsync()`
- `Asset`：`EnableHotfix` 开关和补丁判断都在 Asset 侧，但它只控制资源热更检查，不控制 App 大版本检测
- `HybridCLR`：`ProcedureLoadDll` 负责 AOT metadata、业务 DLL、程序集刷新和业务入口定位
- `LauncherUI`：是启动链路 UI，不是独立流程系统

## 常见误区

- 在 `OnEnter` / `OnLeave` 同步加状态
- 把业务 Procedure 放到框架程序集里
- 认为 `ProcedureLoadDll` 只是“加载 DLL”，忽略它还负责注册业务 Procedure 和跳业务入口
- 把 `LauncherUI` 的生命周期和 `Procedure` 生命周期混为一谈

## 先往哪看

- 改分档与注册时序：[[ADR-007-procedure-tier-split]]
- 改热更开关分流：[[ADR-013-hotfix-master-switch]]
- 改版本检查路由：[[PAT-44-procedure-checkversion-two-stage]]

## 关联

- 图谱：[[MOC-App]]、[[MOC-HybridCLR]]、[[MOC-Manager]]
