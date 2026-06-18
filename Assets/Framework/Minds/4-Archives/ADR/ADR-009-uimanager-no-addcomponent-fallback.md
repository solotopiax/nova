---
id: ADR-009
title: UIManager 取消 AddComponent 兜底（Prefab 必须预挂 NovaBehaviour）
status: superseded
date: 2026-05-14
archived-date: 2026-05-23
summary: UIManager禁AddComponent兜底必须显式
category: runtime
aliases:
  - ADR-009
tags: [adr, nova, hybridclr, uimanager, novabehaviour]
supersedes: []
superseded-by:
  - "[[ADR-032-drop-novabehaviour-bridge|ADR-032]]"
related:
  - "[[ADR-005-hybridclr-namespace-single-write-path|ADR-005]]"
  - "[[ADR-006-novabehaviour-ibaselife-replace-monobehaviour|ADR-006]]"
  - "[[GLO-01-novabehaviour]]"
---

# ADR-009：UIManager 取消 AddComponent 兜底（Prefab 必须预挂 NovaBehaviour）

## 背景（Context）

旧 UIManager（HybridCLR 改造前）在 `CreateUIView` 内部有兜底逻辑：

```csharp
var view = prefab.GetComponent<UIView>();
if (view == null)
    view = go.AddComponent(viewType); // 兜底：动态挂业务 UIView 类
```

问题随 [[ADR-006-novabehaviour-ibaselife-replace-monobehaviour|ADR-006]] NovaBehaviour 改造暴露：

- HybridCLR IL2CPP 模式下 `AddComponent(viewType)` 若 `viewType` 是业务 DLL 类型，AOT 端无法识别，挂载失败。
- 字符串路径 `OpenUIView(string viewName)` 与泛型路径 `OpenUIView<T>` 行为不一致：泛型可触发兜底，字符串则报错——非对称差异让业务作者反复踩坑。
- `OpenUIViewSyncWithType` / `OpenUIViewAsyncWithType` 重载试图弥补差异，反而让 API 表面更复杂。

Nova × HybridCLR 承载方案 v2 R11 Wave 已完成 `viewType` 兜底回退，本 ADR 沉淀最终决策。

## 决策（Decision）

**UIManager 不再有 `AddComponent(viewType)` 兜底；Prefab 必须预挂 `NovaBehaviour` 并配置 `ScriptName`。**

### 1. UIView 承载约束

- Prefab 必须预挂 `NovaBehaviour` + 配置 `ScriptName`（业务行为类全类型名）
- `UIManager.CreateUIView` 通过 `Prefab.GetComponent<UIView>()` 取已挂组件
- Prefab **未挂** `UIView` 组件时：`Log.Error` + 返回 `null`（不再 AddComponent 兜底）

### 2. OpenUIView 重载收敛

- `OpenUIViewSync<T>` / `OpenUIViewAsync<T>` 泛型重载**保留**，但内部等价字符串重载（仅作调用方便）
- `OpenUIViewInfo` **不再**持有 `m_ViewType` 字段
- `OpenUIViewSyncWithType` / `OpenUIViewAsyncWithType` 重载**已删除**

### 3. 字符串与泛型路径等价

两条路径行为完全一致——不再存在「泛型兜底 / 字符串禁兜底」的非对称差异。业务作者可任选其一。

## 后果（Consequences）

### 正面

- `AddComponent(viewType)` 在 HybridCLR IL2CPP 模式下的失败风险根除。
- API 表面收敛：`OpenUIView` 重载从 4 种缩到 2 种（Sync/Async × 泛型/字符串），心智模型清晰。
- 字符串与泛型等价，业务作者无需担心选错路径。
- `OpenUIViewInfo` 数据结构精简，不再持有 `m_ViewType` 这一冗余字段。
- 与 [[ADR-006-novabehaviour-ibaselife-replace-monobehaviour|ADR-006]] NovaBehaviour 设计完全一致，UIView 不再是特例。

### 负面

- 历史代码若依赖兜底（Prefab 未挂 UIView，运行时挂载），升级时 `Log.Error` + 返回 null，需逐个 Prefab 修补预挂 NovaBehaviour。
- Prefab 预挂的强制约束需要美术/UI 工作流配合，UI 编辑期校验工具（编辑器扩展）成为必备。
- `OpenUIViewSyncWithType` / `OpenUIViewAsyncWithType` 删除是 break change，存量调用代码需迁移到泛型或字符串重载。

## 被排除的方案（Alternatives）

| 方案 | 否决理由 |
|---|---|
| 保留 `AddComponent(viewType)` 兜底 | HybridCLR IL2CPP 模式下业务类型动态挂载失败；本 ADR 根因要解决 |
| 仅保留泛型路径，删除字符串路径 | 业务层有从配置/服务器拿到字符串类型名的场景，删除字符串路径破坏热更可配置性 |
| 仅保留字符串路径，删除泛型路径 | 业务层泛型有编译期类型检查，对调用方更友好 |
| 兜底但只在 AOT 层类型生效 | 增加运行时分支，违反"两路径行为一致"目标 |
| Prefab 未挂 UIView 时抛异常而非返回 null | 异常会中断 OpenUIView 调用方流程；返回 null 让调用方决定降级策略 |

## 验证依据（Verification）

- 规则文件：`.claude/rules/framework-hotupdate-constraints.md` §八、UIManager 业务 UIView 约束
- Grep 关键词（应零命中）：`AddComponent(viewType)`、`OpenUIViewSyncWithType`、`OpenUIViewAsyncWithType`、`OpenUIViewInfo.m_ViewType`
- 实现位置：`UIManager.CreateUIView` 内 `GetComponent<UIView>()` + null check + `Log.Error`
- Prefab 巡检：所有 UI Prefab 根节点应挂 `NovaBehaviour` + 配置 `ScriptName`

## 关联

- 规则文件：`.claude/rules/framework-hotupdate-constraints.md` §8
- 相关 ADR：[[ADR-005-hybridclr-namespace-single-write-path|ADR-005]]（程序集与命名空间）、[[ADR-006-novabehaviour-ibaselife-replace-monobehaviour|ADR-006]]（NovaBehaviour 替代业务 MB）
- 相关 Glossary：[[GLO-01-novabehaviour]]
