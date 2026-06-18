---
id: PAT-64
title: 业务侧 UI 类一律 View 后缀（XxxView）
summary: 业务侧 UI 类一律 View 后缀
category: naming
type: pattern
status: active
date: 2026-05-22
aliases:
  - PAT-64-business-ui-view-suffix
keywords:
  - PAT-64
  - PAT-64-business-ui-view-suffix
  - 业务侧 UI 类一律 View 后缀（XxxView）
tags:
  - pattern
  - naming
  - ui
  - sample
related:
  - "[[PAT-42-naming-concrete-deduplicate|PAT-42]]"
  - "[[ADR-033-maindemo-isolated-topology|ADR-033]]"
---

# PAT-64：业务侧 UI 类一律 View 后缀（XxxView）

## 适用场景（When）

- 业务侧（含 sample）新建任何"显示内容"的类，无论是全屏入口还是节点控件
- code-reviewer 审查 sample / 业务模块 UI 命名时
- 起草新 sample（MainDemo / SdkDemo / KitDemo 等）UI 设计方案时
- 触发信号：类名含 `Panel` / `Window` / `Dialog` / `Form` / `Page` / `Frame` / `Screen` 等任何"显示容器"语义后缀

## 核心做法（What & How）

| 维度 | 规则 |
|---|---|
| **后缀强制** | 业务侧所有显示类一律 `XxxView`，无例外 |
| **粒度无关** | 全屏入口（`MainMenuView`）、节点控件（`DemoNavTreeNodeView`）、卡片 / 弹窗 / Toast 全部 `View` 结尾 |
| **基类对齐** | 继承 framework `UIView` 的命名为主 View；独立 `MonoBehaviour` 节点视图同样 `View` 结尾以保持术语一致 |
| **prefab 命名** | 与脚本类名同名：`DemoNavTreeView.cs` → `DemoNavTreeView.prefab` |
| **Address 命名** | YooAsset `AddressByFileName` → address = 文件名去后缀，等同 View 类名 |
| **GameObject 名** | prefab 根 GameObject 命名同类名（便于 Hierarchy 查找） |

**禁用后缀**：`Panel` / `Window` / `Dialog` / `Form` / `Page` / `Frame` / `Screen` / `UI`（裸 UI 后缀）/ `Ctrl` / `Controller`（业务 UI 控制类一律融入 View 内部）

**例外**：framework 内部既有公共类（如 framework 的 `IUIView` / `UIView` 基类）属于 framework 范畴，不受本 Pattern 约束；本 Pattern 只约束**业务侧**新增类。

## 为什么这么做（Why）

- **概念统一**：framework 已用 `IUIView` / `UIView` 作为 UI 基类；业务侧若混用 `Panel`，就出现"框架叫 View，业务叫 Panel，运行时同一个对象"的语义撕裂
- **搜索效率**：grep `View\b` 一次性命中所有 UI 类；混用后缀必须四五个关键字组合才能扫全
- **避免 Panel 二义**：Unity / UGUI 里 `Panel` 既指"UGUI Image 子组件"也指"UI 容器"，业务侧再用 `Panel` 后缀会与 UGUI 内置概念叠加歧义
- **跨 sample 一致**：MainDemo / SdkDemo / KitDemo 等多 sample 并行开发时，统一术语避免各自风格漂移
- **代码评审锚点**：code-reviewer 看到 `XxxPanel` 立即可判定违规，无需理解上下文

## 反模式（Anti-patterns）

- **混用后缀**：同一 sample 里既有 `MainMenuView` 又有 `SettingsPanel`，新人一眼看不出哪个是 framework 标准 UI、哪个是私货 → 应统一 `View`
- **复合后缀**：`MainMenuViewPanel` 或 `SettingsPanelView` —— 双后缀堆叠，违反命名具体化原则（[[PAT-42-naming-concrete-deduplicate|PAT-42]]）→ 单一 `View` 后缀
- **裸 UI 后缀**：`MainMenuUI` / `SettingsUI` 模糊（既可能是脚本也可能是 prefab 类型）→ 改 `MainMenuView` / `SettingsView`
- **MVC 风格的 `Controller` 后缀**：业务 UI 把"事件处理"独立成 `MainMenuController`，导致 `View + Controller` 分文件而行为强耦合 → 业务 UI 走 framework `UIView` 一体化模型，不引入 Controller 类
- **prefab / GameObject 与脚本不同名**：脚本叫 `DemoNavTreeView`，prefab 叫 `DemoTree.prefab`，address `tree_main` —— 三者各起一名，address 维护成本飙升 → 三处同名

## 跨项目复用提示

- 任何带 UI 概念的 Unity 项目（含 framework 自研体系）适用
- 若已有项目大量历史代码用 `Panel` 后缀，**禁止全量改名**——只对新增类生效，老代码保留（破坏性变更得不偿失）
- 在统一工程约束或同等位置加一行硬约束 + 静态审查自检触发器，可机器化拦截
- 适配 MVC / MVP 项目时：本 Pattern 只约束"显示层"命名，业务逻辑层按各自架构（不强行 View 后缀）

## 关联

- 规范落点：待补充至统一工程约束的 UI 命名规范章节
- 相关 Pattern：[[PAT-42-naming-concrete-deduplicate|PAT-42]]（命名具体化原则——View 后缀也要具体）
- 相关 ADR：[[ADR-033-maindemo-isolated-topology|ADR-033]]（MainDemo 演示拓扑——首次落实本规范的 sample）
