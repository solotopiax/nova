---
id: ADR-015
title: 合并 ProcedureLaunch 进 ProcedureSplash 并约定 UI 层级
status: accepted
date: 2026-05-16
summary: Launch合并到Splash简化启动编排
category: module
aliases:
  - ADR-015-merge-launch-into-splash
keywords: [ADR-015, ADR-015-merge-launch-into-splash]
tags:
  - adr
  - nova
  - procedure
  - launch
  - ui
supersedes: []
superseded-by: []
related:
  - "[[ADR-007-procedure-tier-split|ADR-007]]"
  - "[[ADR-013-hotfix-master-switch|ADR-013]]"
---

# ADR-015：合并 ProcedureLaunch 进 ProcedureSplash 并约定 UI 层级

## 背景

原启动流程为 `ProcedureLaunch → ProcedureSplash → ProcedureCheckVersion → (Hotfix/AppDownload) → ProcedureLoadDll → 业务入口`。`ProcedureLaunch` 只做 LaunchConfig 异步加载，与 `ProcedureSplash` 职责重叠；`LauncherUIController.LoadPanel` 也没显式控制 `Canvas.sortingOrder`，跨 Procedure 存活的 Splash 可能被覆盖。

## 决策

1. 删除 `ProcedureLaunch.cs`，把 LaunchConfig 异步加载迁入 `ProcedureSplash`。
2. `SplashDuration` 只做保底最短时间，跳转必须等初始化完成。
3. Splash 的销毁责任只收口到 `ProcedureLoadDll.OnLeave`。
4. `LauncherUIController.LoadPanel` 强制写入 `Canvas.sortingOrder`：`Splash=0 / Progress=10 / Dialog=20`。
5. 业务 Procedure 必须在 OnEnter 当帧接管首屏。

## 替代方案

- **保留 ProcedureLaunch**：职责与 Splash 高度重叠，徒增节点数与维护成本，弃用。
- **Splash 在自身 OnLeave 销毁**：会导致 CheckVersion/Hotfix/AppDownload 之间出现黑场，弃用。
- **依赖 Prefab 美术维护 sortingOrder**：无法在代码侧校验，跨人协作易回退，改为 `LoadPanel` 强制赋值常量。
- **Visitors.cs 注释承诺的旧入口名 "自动 remap"**：代码未实现，删除注释 + 直接改 Prefab 值，避免误导。

## 后果

- 启动序列简化为 `ProcedureSplash → ProcedureCheckVersion → (Hotfix/AppDownload) → ProcedureLoadDll → 业务入口`。
- Splash 持续显示，并被 Progress/Dialog 通过 sortingOrder 强制覆盖。
- 业务侧新增显式契约：必须当帧接管首屏。
- `LauncherUIController.LoadPanel` 的 sortingOrder 改为代码常量统一控制。
