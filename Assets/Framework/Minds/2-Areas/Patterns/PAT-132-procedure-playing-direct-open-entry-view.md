---
id: PAT-132
title: 单主题 sample 用 ProcedurePlaying 直开入口 View
summary: 单主题 demo OnEnter 直开入口 View
category: workflow
type: pattern
status: active
date: 2026-06-01
aliases:
  - PAT-132-procedure-playing-direct-open-entry-view
keywords:
  - PAT-132
  - 单主题 sample 用 ProcedurePlaying 直开入口 View
  - PAT-132-procedure-playing-direct-open-entry-view
tags: [pattern, methodology, sample, demo, procedure, ui]
related:
  - "[[PAT-77-base-demo-view-three-zone-template|PAT-77]]"
  - "[[PAT-78-sample-demo-full-flow-sop|PAT-78]]"
  - "[[ADR-007-procedure-tier-split|ADR-007]]"
  - "[[ADR-053-kit-config-templating|ADR-053]]"
---

# PAT-132：单主题 sample 用 ProcedurePlaying 直开入口 View

## 适用场景（When）

- Nova kit / sdk 的 sample 工程**只演示一个核心主题**（如 LoginDemo 只演示 Network Login），不存在多模块横向铺开的 demo 矩阵。
- 该 sample 没有、也不需要 MainDemo 那套 `DemoNavTreeView` 树形导航目录。
- 业务 Procedure 链路已走到 `ProcedurePlaying`（游戏主循环阶段），需要一个确定性的"打开入口演示页"动作。

> 反向信号（不适用）：sample 要演示 ≥3 个并列主题、需要用户在多个 demo 间跳转 → 仍应走 MainDemo 的 `DemoNavTreeView` 导航树范式。

## 核心做法（What & How）

`ProcedurePlaying.OnEnter` 直接打开唯一入口 View，后续所有子 demo 沿用同一收口点：

```csharp
protected override void OnEnter(ProcedureOwner procedureOwner)
{
    base.OnEnter(procedureOwner);
    Log.Debug(LogTag.Procedure, "ProcedurePlaying — 进入游戏主循环。");

    // 以后所有子 demo 都在此处直接打开对应 DemoXXXView
    int serialID = Nova.UI.OpenUIViewAsync<DemoLoginView>();
    if (serialID < 0)
    {
        Log.Error(LogTag.UI, "ProcedurePlaying — DemoLoginView 打开失败。");
    }
}
```

配套约束：

| 项 | 约束 |
|---|---|
| 入口 View 基类 | 继承 `BaseDemoView` 三段式（标题/交互/反馈），见 [[PAT-77-base-demo-view-three-zone-template\|PAT-77]] |
| UI 注册 | UIs.xlsx 加行 → Pipify 导出 UIs.json；`AssetLocation` = 类名（裸文件名），三处同名强一致（脚本/prefab/AssetLocation） |
| **UIGroupName 取值** | **必须跟随该场景 `UIComponent` 实际保留的分组**——不能照搬 MainDemo 用 `Menu`。各 sample 场景常对 `UIComponent.m_UIGroups` 做 prefab override 精简分组 |
| 业务 API 调用 | 走 Nova 门面极简入口，如 `Nova.Network.Kit<Login>().Async(openId, forceNewAccount)`（[[ADR-053-kit-config-templating\|ADR-053]]，禁 ADR-044 cmdName 双重载） |
| Procedure 归属 | 业务 Procedure 在 `Game.Runtime` 层延迟注册（[[ADR-007-procedure-tier-split\|ADR-007]]） |

落地顺序仍遵循 [[PAT-78-sample-demo-full-flow-sop|PAT-78]]（数据→码→Prefab→验证→文档），本范式是其"入口打开"环节的单 View 裁剪版。

## 为什么这么做（Why）

- **单主题 demo 的导航树是纯开销**：只有一个核心页时，`DemoNavTreeView` + 树数据 + 叶子派发是为零收益付的复杂度税，ProcedurePlaying 直开是最短路径。
- **收口点统一**：把"打开演示页"固定在 `ProcedurePlaying.OnEnter` 一处，后续该 sample 新增子 demo 时入口位置可预测，不必每次重新设计跳转。
- **UIGroupName 跟随场景的代价是硬的**：sample 场景对 `UIComponent.m_UIGroups` 做了 override 精简（如 LoginDemo 只留 `Demo` 组），若 View 注册成场景里不存在的分组名，运行时 `UIManager` 直接抛 `InvalidOperationException: 视图分组 'Menu' 不存在`，`OpenUIViewAsync` 返回 -1，演示页打不开——这是真实 P1，编译/绑定全绿也照样炸。

## 反模式（Anti-patterns）

- **照搬 MainDemo 把入口 View 注册成 `UIGroupName=Menu`**：LoginDemo 场景 `UIComponent` 经 prefab override 只剩 `Demo` 分组，注册 `Menu` 后 Play Mode 立即抛"视图分组 'Menu' 不存在"，`serialID<0`，页面静默打不开。必须先用 UnityMCP 查该场景 `m_UIGroups` 实际有哪些分组再填。
- **单主题 sample 强行搭 DemoNavTreeView 导航树**：为一个演示页建树数据 + 叶子节点 + `MakeViewLeaf<T>()` 派发，复杂度全是浪费，且空树/单叶子的导航交互体验更差。
- **入口 View 不继承 BaseDemoView 自起炉灶**：丢掉三段式骨架与反馈区 API 前缀日志（PAT-77），演示页失去"按钮点击 ↔ API 调用"的视觉绑定教学价值。
- **演示里用 ADR-044 旧的 cmdName 双重载**：`Nova.Network.Kit<Login>().Async("GameLogin", channel, openId)` 已被 ADR-053 推翻删除，照抄旧注释会编译失败或误导。

## 跨项目复用提示

- "单一入口主题 → 启动流程末态直开入口页，不建导航"是通用 sample 设计取舍，不限 Nova / Unity。
- "UI 分组注册值必须跟随宿主容器实际存在的分组、不能照搬模板"是 UGUI / 任何带 UI 分层管理框架的通用坑——注册前查容器实际分组。
- Procedure / FSM 末态作为业务入口收口点的思路，可映射到任何状态机驱动的启动流程。

## 关联

- 规范落点：候选补充至统一 Sample 覆盖规范（与 PAT-77 / PAT-78 同源）
- 相关 ADR：[[ADR-007-procedure-tier-split|ADR-007]]（业务 Procedure 分档）、[[ADR-053-kit-config-templating|ADR-053]]（Kit 入口极简形态）
- 相关 Pattern：[[PAT-77-base-demo-view-three-zone-template|PAT-77]]（BaseDemoView 三段式）、[[PAT-78-sample-demo-full-flow-sop|PAT-78]]（Sample 18 步 SOP）
- 相关原则：Playing 流程允许直接打开入口 View
