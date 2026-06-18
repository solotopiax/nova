---
id: ADR-014
title: AssetPlayMode 拆分为 EditorPlayMode + RuntimePlayMode（删 LaunchConfig.DefaultPlayMode）
status: accepted
date: 2026-05-16
summary: AssetPlayMode拆分编辑期与运行期独立
category: asset
aliases:
  - ADR-014-playmode-split-editor-runtime
keywords: [ADR-014, ADR-014-playmode-split-editor-runtime]
tags: [adr, nova, asset, hotfix, hybridclr]
supersedes: []
superseded-by: []
related:
  - "[[ADR-013-hotfix-master-switch|ADR-013]]"
  - "[[ADR-007-procedure-tier-split|ADR-007]]"
  - "[[ADR-010-validation-on-consumer-side|ADR-010]]"
  - "[[ADR-012-third-party-info-isolation|ADR-012]]"
  - "[[PAT-09-inspector-config-i18n|PAT-09]]"
  - "[[PAT-10-imgui-popup-horizontal-wrap|PAT-10]]"
---

# ADR-014：AssetPlayMode 拆分为 EditorPlayMode + RuntimePlayMode（删 LaunchConfig.DefaultPlayMode）

## 背景（Context）

EnableHotfix 总开关落地后，YooAsset `AssetPlayMode` 在 Editor / Player 两个环境下的语义需求不一致：

- **Editor**：开发期希望随意切换 `EditorSimulateMode`（直接读 Editor 资源，零网络）/ `OfflinePlayMode` / `HostPlayMode` / `WebPlayMode`，4 选 1
- **Player（终端发布）**：`EditorSimulateMode` 在 Player 下根本无法工作（依赖 AssetDatabase），必须**禁选**；只允许 `OfflinePlayMode` / `HostPlayMode` / `WebPlayMode`，3 选 1
- **联动语义**：EnableHotfix=false 时终端必须走 `OfflinePlayMode`（不连服）；EnableHotfix=true 时终端必须走 `HostPlayMode` 或 `WebPlayMode`（联机热更）

旧设计：`LaunchConfig.DefaultPlayMode`（单字段）+ `AssetManager.BuildPlayModeOptions` 内**运行时强制覆盖**。

旧设计的问题：

1. **Inspector 显示值 ≠ 实际运行值**：开发者在 Inspector 看到的是 `HostPlayMode`，但 Player 启动时被覆盖为 `OfflinePlayMode`。调试时面板与日志撕裂，必须翻代码才能理解
2. **Editor / Player 共享一个字段**：开发期想短暂跑 OfflinePlayMode 验证一些逻辑，会污染 Player 的默认值
3. **覆盖逻辑下沉到 Manager**：违反 [[ADR-010-validation-on-consumer-side|ADR-010]] 谁使用谁校验——Inspector 是用户的"数据生产端"，校验应在 Inspector 编辑期就拦住非法组合
4. **`Log.Warning` 体感弱**：发布到 Player 后用户看不到 Editor 日志，覆盖动作完全静默

不做决策的代价是开发期面板与运行行为继续撕裂，后续再加新 PlayMode 时还会继续叠运行时覆盖。

## 决策（Decision）

**拆 1 字段为 2 字段，删除单字段总入口，把联动校验全部上移到 Inspector 编辑期。**

### 一、字段拆分

`AssetManagerConfig.cs` 新增 2 字段（**[Serializable] 业务范畴独立成簇**，不属于"热更配置"）：

```csharp
public AssetPlayMode EditorPlayMode  = AssetPlayMode.EditorSimulateMode;
public AssetPlayMode RuntimePlayMode = AssetPlayMode.HostPlayMode;
```

`LaunchConfig.cs` **彻底删除** `DefaultPlayMode` 字段：

- C# 字段移除
- `Assets/Resources/BuiltIn/Jsons/LaunchConfig.json` 同步删除 `"DefaultPlayMode": 2` 键
- `Test.cs` `expectedFields` 移除 `"DefaultPlayMode"`

### 二、Manager 简化

`AssetManager.BuildPlayModeOptions` 改为：

```csharp
AssetPlayMode effectiveMode = Application.isEditor
    ? m_Config.EditorPlayMode
    : m_Config.RuntimePlayMode;
```

**删除所有运行时强制覆盖逻辑**（含 `EnableHotfix=false` 触发的 `Log.Warning`）。Manager 只读不修，校验责任全部上移。

### 三、Inspector 编辑期双向联动

`AssetComponentInspector` 实现「`EnableHotfix` ⇔ `RuntimePlayMode`」双向联动（不联动 `EditorPlayMode`）：

| 触发字段 | 触发后值 | 连锁动作 |
|---|---|---|
| `EnableHotfix` | `false` | `RuntimePlayMode` → `OfflinePlayMode` |
| `EnableHotfix` | `true` | 若 `RuntimePlayMode==OfflinePlayMode` → `HostPlayMode`，否则保持 |
| `RuntimePlayMode` | `OfflinePlayMode` | `EnableHotfix` → `false` |
| `RuntimePlayMode` | `HostPlayMode/WebPlayMode` | `EnableHotfix` → `true` |

落地见 `AssetComponentInspector.Methods.cs::DrawConfigs` + `DrawRuntimePlayModePopup`，详细范式见 [[PAT-09-inspector-config-i18n|PAT-09]] §九。

### 四、Inspector 字段范畴归属

EditorPlayMode/RuntimePlayMode **不属于「热更配置」业务范畴**——即使 EnableHotfix=false 仍要决定加载方式（OfflinePlayMode 是"不连服的本地资源加载"，照样要这两个字段）。

Inspector 中两字段独立成第 2 顶层子簇，置于 `TypesSelector` HelpBox 之后、`DefaultPackageName` 之前，用 `Line()` 上下隔开，**不进 Foldout**（仅 2 字段不构成"折叠多字段以聚焦"动机）。

### 五、范围

- **本决策只覆盖 AssetPlayMode 拆分与联动语义**，不重新讨论 EnableHotfix 总开关本身（该决策走 [[ADR-013-hotfix-master-switch|ADR-013]]）
- **不影响 ProcedureSplash 二分跳转**（也归 ADR-DRAFT 2026-05-15）

## 后果（Consequences）

### 正面

- **Inspector 即所见即所得**：面板值就是运行值。
- **Editor / Player 互不污染**：开发期切 Editor 模式不影响 Player 默认。
- **Manager 简化**：`BuildPlayModeOptions` 收敛为一行三元表达式。
- **校验上移符合分层**：Inspector 负责拦截非法组合，Manager 只读不改。
- **联动语义可视化**：4 条规则表格化，新人更容易读懂。

### 负面

- **JSON 兼容性损失**：旧版本 `LaunchConfig.json` 含 `DefaultPlayMode` 键，新版本反序列化时该键会被忽略（Util.Json 的 unknown field 容忍策略），但**该字段的语义已永久丢失**——升级路径单向，回退需要手动恢复 LaunchConfig.cs 字段。本项目目前无版本兼容需求，故接受
- **Inspector 联动代码量增加**：`DrawConfigs` 多了 2 个 `BeginChangeCheck` 块 + `DrawRuntimePlayModePopup` 自定义 IntPopup（约 30 行）。维护成本上升一点，但换来"Inspector 即所见即所得"的清晰度
- **EditorUtil.Draw 缺 IntPopup 限制选项集封装**：临时用裸 `EditorGUILayout.IntPopup`，违背 `csharp-code-style.md §四·一` 但有合理理由（EnumPopup 用 enumValueIndex，无法表达"显示 3 项但值为 1/2/3 的非连续整数集"）。后续应考虑给 EditorUtil.Draw 加 IntPopup 限制选项集封装
- **新人理解门槛**：必须同时理解"PlayMode 拆分动机"+ "Inspector 编辑期双向联动模式"+ "范畴归属判据"。已通过 [[PAT-09-inspector-config-i18n|PAT-09]] §七~九 沉淀模式

## 被排除的方案（Alternatives）

| 方案 | 否决理由 |
|---|---|
| **方案 A：保留单字段 + 运行时分流（旧设计）** | Inspector 显示值 ≠ 实际运行值；调试撕裂；不满足 [[ADR-010-validation-on-consumer-side|ADR-010]] |
| **方案 B：拆 2 字段但保留 LaunchConfig.DefaultPlayMode 作为兜底** | 多一个数据源 = 多一处可能不一致；用户最终决议「拆分方案 1（删 LaunchConfig.DefaultPlayMode）」彻底拆分，牺牲 JSON 兼容性换语义清晰 |
| **方案 C：联动放 Manager.Initialize 期间检测覆盖** | 等价于旧设计；Inspector 仍是脏数据 |
| **方案 D：EditorPlayMode 也参与 EnableHotfix 联动** | 用户决议「EditorPlayMode 不参与联动」——Editor 是开发环境，应允许任意切换以便调试，不应被 EnableHotfix 强制约束 |
| **方案 E：只拆字段不删 LaunchConfig.DefaultPlayMode** | 数据冗余，等价于方案 B 的弱化版 |

## 验证依据（Verification）

- **代码路径**：
  - `Assets/Framework/Scripts/Runtime/Modules/Asset/Managers/AssetManager/Definitions/AssetManagerConfig.cs`（新增 EditorPlayMode/RuntimePlayMode）
  - `Assets/Framework/Scripts/Runtime/Modules/Asset/Managers/AssetManager/Implements/AssetManager.Methods.cs::BuildPlayModeOptions`（三元表达式简化）
  - `Assets/Framework/Scripts/Runtime/Modules/Launch/Definitions/LaunchConfig.cs`（删 DefaultPlayMode）
  - `Assets/Framework/Scripts/Editor/Inspectors/AssetComponentInspector/AssetComponentInspector.Methods.cs::DrawConfigs`（双向联动 + DrawRuntimePlayModePopup）
- **审查要点**：
  - grep `DefaultPlayMode` 全工程零命中
  - grep `EnableHotfix` 在 Manager 层只读不写
  - 双向联动两个 ChangeCheck 必须并列不嵌套
- **测试用例**：
  - `Assets/Game/Scripts/Runtime/Test/TestPlayModeSplitAndLinkage_20260515.cs`（TC-21~TC-27 共 7 用例）
  - 覆盖：字段存在、旧字段已删、4 条联动规则各 1 用例
