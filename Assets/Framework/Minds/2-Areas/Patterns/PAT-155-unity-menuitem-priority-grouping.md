---
id: PAT-155
title: Unity Editor 菜单分组按父菜单首项并预留 11 级差
type: pattern
status: active
date: 2026-08-06
summary: 顶层子菜单继承首项优先级，分割线统一预留 11 级差
category: editor
aliases:
  - PAT-155-unity-menuitem-priority-grouping
  - Unity 菜单优先级分组
keywords:
  - PAT-155
  - MenuItem
  - priority
  - separator
  - 菜单分割线
  - 父菜单优先级
tags:
  - pattern
  - editor
  - unity
  - menu
related:
  - "[[MOC-Inspector|MOC-Inspector]]"
  - "[[PAT-59-ai-research-conclusion-staleness|PAT-59]]"
---

# PAT-155：Unity Editor 菜单分组按父菜单首项并预留 11 级差

## 适用场景

- 在 Unity 顶部菜单中插入新的命令或子菜单，并要求它与相邻项同组或由分割线隔开。
- 菜单项的直接前后项包含父菜单，例如 `Open Folder/`、`Enable Logs/`。
- 源码 priority 看似满足文档描述，但实际 Editor 菜单没有出现预期分割线。

## 核心做法

### 1. 顶层父菜单继承第一个子项的 priority

判断顶层菜单顺序和分组时，不能拿父菜单文字路径猜值。Unity 的父菜单会从其第一个定义的子项派生 priority；因此 `Open Folder/` 的顶层 priority 是其首个 `Data Path` 的值，`Enable Logs/` 则取 `Disable All Logs` 的值。

同一父菜单内部的后续子项继续逐项 `+1`，保持稳定顺序。

### 2. 组界统一预留 11，而不是卡边界 10

Unity 官方菜单项构造函数文档正文称 priority 比前项高 10 或以上会出现分割线，但同页示例使用 `101 → 112`，即差 11。Nova 在 Unity `6000.4.2f1` 的实际菜单验证也表明：差 10 没有出现分割线，差 11 才稳定出现。

因此 Nova Editor 菜单统一采用以下保守规则：

- 同组：相邻顶层项差值不大于 10。
- 分组：下一项至少比前一项大 11。

不要依赖文档阈值的边界值 10；实际 Editor 行为优先于文字推断。

### 3. 先算顶层组，再分配子项

本次菜单分组基线：

```text
Open IDE Project       1010
---------------------------
Open Folder            1021  <- 首个子项 Data Path
Clean Hotfix Caches    1031  <- 与 Open Folder 差 10，同组
---------------------------
Enable Logs            1042  <- 首个子项 Disable All Logs，与前项差 11
```

`Open Folder` 的其他子项使用 `1022–1027`；`Enable Logs` 的其他子项使用 `1043–1048`。这样组前、组内和组后三种关系都由明确数值表达。

验证函数使用 `[MenuItem(path, true)]` 即可；Unity 不使用验证函数上的 priority，不要为同一路径的 validate attribute 重复填写数值。

## 反模式

- 只调整中间菜单项，不同时计算前一个父菜单和后一个父菜单派生出的顶层 priority。
- 根据文档一句“10 or greater”把组界恰好设为 10，却不在目标 Unity 版本中观察实际菜单。
- 为获得分割线使用空白伪菜单项或特殊字符，而不使用 priority 分组。
- 平移父菜单首项后遗漏其余子项，导致子菜单内部顺序或分组漂移。
- 把 Runtime Manager 的 `Priority` 规则与 Editor 菜单 priority 混为同一契约。

## 验证依据

- Unity 官方文档：[菜单项构造函数](https://docs.unity3d.com/ScriptReference/MenuItem-ctor.html)
- Nova 菜单实现：`Assets/Framework/Scripts/Editor/Menus/FolderMenuItems.cs`、`AssetCacheMenuItems.cs`、`EnableLogsMenuItems.cs`
- Unity `6000.4.2f1` 实际观察：差 10 时未出现分割线；调整为差 11 后形成预期组界。
- 文档核对点：官方同页示例用 priority `101` 与 `112` 创建 divider。

## 关联

- Editor 菜单与工具入口：[[MOC-Inspector|MOC-Inspector]]
- 官方说明与实际行为冲突时的复核原则：[[PAT-59-ai-research-conclusion-staleness|PAT-59]]
