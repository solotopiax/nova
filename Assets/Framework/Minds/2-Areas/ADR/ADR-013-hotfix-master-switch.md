---
id: ADR-013
title: 热更总开关 EnableHotfix 与启动流程二分
status: accepted
date: 2026-05-15
summary: EnableHotfix作为热更总开关收敛入口
category: hotfix
aliases:
  - ADR-013-hotfix-master-switch
keywords: [ADR-013, ADR-013-hotfix-master-switch, 热更总开关 EnableHotfix 与启动流程二分]
tags: [adr, nova, asset, procedure, launch]
supersedes: []
superseded-by: []
related:
  - "[[ADR-001-component-manager-three-layer|ADR-001]]"
  - "[[ADR-007-procedure-tier-split|ADR-007]]"
  - "[[ADR-012-third-party-info-isolation|ADR-012]]"
  - "[[ADR-014-playmode-split-editor-runtime|ADR-014]]"
  - "[[PAT-09-inspector-config-i18n|PAT-09]]"
---

# ADR-013：热更总开关 EnableHotfix 与启动流程二分

## 修订注记（2026-06-09）

本 ADR 中“`EnableHotfix=false` 时 `ProcedureSplash` 直接跳 `ProcedureLoadDll`，且启动期不发 App 版本检查 HTTP 请求”的描述，已不再代表当前实现。

当前代码基线改为：

- `ProcedureSplash` 始终进入 `ProcedureCheckVersion`
- `ProcedureCheckVersion` 始终先执行 App 大版本检测
- `EnableHotfix` 只控制 Asset 资源热更检查 / 下载链路
- `AppDownloadCheckUrl` 为空时，App 检查降级为 `NoDownload`，继续后续启动流程

因此，本 ADR 保留其“`EnableHotfix` 归属 Asset 配置域”的决策背景，但启动链分流细节应以 [[PAT-44-procedure-checkversion-two-stage|PAT-44]] 和当前 `Docs` 为准。

## 背景（Context）

Nova Framework 是面向"主流游戏（含热更）"设计的 Unity 框架，当前启动流程固化为：

```
ProcedureLaunch → ProcedureSplash → ProcedureCheckVersion
                                     ├─ HotfixRequired   → ProcedureHotfix    → ProcedureLoadDll
                                     ├─ DownloadRequired → ProcedureAppDownload → (跳商店或退出)
                                     ├─ SkipRequired     → ProcedureLoadDll
                                     └─ Failed           → ProcedureLoadDll
```

但落地中发现两类需求并存：

1. **重度游戏项目**：必须有完整热更（版本检查 / 资源补丁 / 强更下载）
2. **轻量项目 / 工具型项目 / 单机项目**：完全不需要热更，只想直接启动业务

无法关闭热更时，轻量项目接入框架要付出额外代价：必须配置一个永远返回 `SkipRequired` 的版本检查 URL、必须打 OfflinePlayMode 时仍走 `LaunchManager.CheckAsync` 的网络请求路径、必须配置一个永远不会触发的强更地址。这些"假配置"既污染项目又增加启动期网络风险。

### 核心约束

- **不破坏既有项目**：升级框架后默认行为不变
- **远端不可覆盖本地**：`LaunchConfig` 走 persistentDataPath 可远端下发，但"是否启用热更"是项目级编译期决策，远端不能改
- **AssetManager YooAsset PlayMode 与开关强耦合**：`HostPlayMode` / `WebPlayMode` 必须有远端清单，没热更就拿不到清单——逻辑上 `EnableHotfix=false ⇒ PlayMode ∈ {EditorSimulate, Offline}` 是硬数学约束

## 决策（Decision）

### 1. 单字段总开关

在 `AssetManagerConfig` 新增 `public bool EnableHotfix = true;`，与 `AutoHotfix` / `QuitOnFailedOrCancel` / `MaxDownloadConcurrency` 等热更字段聚合。

- **位置**：`Assets/Framework/Scripts/Runtime/Modules/Asset/Managers/AssetManager/Definitions/AssetManagerConfig.cs`
- **默认值**：`true`（既有项目零改动）
- **读取入口**：`AssetComponent.EnableHotfix` 公开属性 + `Nova.Asset.EnableHotfix` 业务层只读暴露
- **配置层归属**：Inspector 序列化字段（Foldout「热更配置」首位），抗远端下发

### 2. ProcedureSplash 出口二分

`ProcedureSplash.OnUpdate` 闪屏时长到达后：

```text
if (EnableHotfix)
    ChangeState<ProcedureCheckVersion>
else
    ChangeState<ProcedureLoadDll>
```

`GetNextProcedureType` 同步对齐二分。`ProcedureLoadDll` 不消费 `ProcedureDataKeys.LaunchCheckResult`（grep 验证），故 `EnableHotfix=false` 路径**不写入** `SkipRequired` 占位（写入即死写）。

### 3. AssetManager PlayMode 强制覆盖

`AssetManager.BuildPlayModeOptions` 内 switch 之前覆写：

```text
effectiveMode = EnableHotfix
    ? m_Launch.DefaultPlayMode
    : (Application.isEditor ? EditorSimulateMode : OfflinePlayMode)
```

与远端 `m_Launch.DefaultPlayMode` 不一致时 `Log.Warning` 一条「热更已禁用，PlayMode 强制覆盖为 X，远端下发的 Y 被忽略」。

### 适用范围

- 框架层启动流程（`ProcedureSplash` / `ProcedureCheckVersion` / `ProcedureHotfix` / `ProcedureAppDownload`）
- 框架层资源加载（`AssetManager.BuildPlayModeOptions`）
- **不影响**业务层 `Nova.Asset.LoadAsync` 等 API 行为

## 后果（Consequences）

### 正面

- **轻量项目零网络启动**：开关关闭时，启动期不发任何业务网络请求，不读 `AppDownloadCheckUrl`，离线友好
- **抗远端配置爆炸**：`LaunchConfig` 远端下发被服务器宕机或配置错误污染时，关闭开关的项目仍能启动
- **PlayMode 一刀切**：开发者改一个开关即获得"非热更行为"完整链路（流程 + Asset），不需要"两处都改"
- **既有项目零改动升级**：默认 `true` 保证升级框架后行为不变
- **架构边界清晰**：开关在 Asset 模块（与同族 `AutoHotfix` 聚合），不抬到 Launch 模块（避免 sibling 概念错位）

### 负面

- **闪屏 → LoadDll 之间空窗**：开关关闭时跳过 CheckVersion / Hotfix UI 节拍，闪屏结束到业务入口期间无进度条；可接受（LoadDll 通常 < 200ms）
- **AOT metadata DLL 仍需打包**：HybridCLR 约束，即使关闭热更，AOT metadata DLL 仍要内置在 StreamingAssets；这是 HybridCLR 限制，不是开关 bug
- **PlayMode 远端下发被静默覆盖**：开发者若设置远端 `HostPlayMode` 但本地 `EnableHotfix=false`，远端配置被忽略；通过 `Log.Warning` 弥补排查难度
- **既有 6 个 LaunchCheckResult 分支永远不走"关闭"路径的 Failed 分支**：当 `Failed` 走 LoadDll 与 `EnableHotfix=false` 走 LoadDll 时，二者代码路径合流，但语义不同，需在文档说明

## 被排除的方案（Alternatives）

| 方案 | 否决理由 |
|---|---|
| **多个细粒度开关（CheckVersion / Hotfix / AppDownload 各一个）** | 调参复杂度上升 3 倍；业务上"只想关版本检查不关资源补丁"的场景几乎不存在；违反"一个开关解决一类问题"原则 |
| **开关放在 LaunchConfig 远端 JSON** | "本地是否启用热更"是项目级编译期决策，远端可改等于让线上配置反向决定本地代码路径；服务器宕机或配置错乱将导致项目无法启动 |
| **开关放在 LaunchComponent.Inspector** | LaunchComponent 承担"版本检查 / 双地址下载 / 跳商店"，与"是否启用整套热更"是 sibling 概念不是 parent；和同族字段（`AutoHotfix`）分裂，违反"相关字段聚合"原则 |
| **默认值取 false** | 既有项目升级框架后默认行为大变（启动直跳 LoadDll），破坏既有 CI / 灰度链路，P0 风险 |
| **保留 CheckVersion 但内部短路返回 SkipRequired** | 仍执行无意义的 Manager.CheckAsync 调用、UI 显示 CheckVersion 阶段进度面板；既浪费启动期 100-300ms 又污染 UI 节拍 |
| **按 Application.isEditor 静态决定（不引入 Inspector 字段）** | 项目接入方无法在打包后切换；Editor 与 Player 行为分裂导致 Editor 调试不能复现 Player 真实路径 |

## 验证依据（Verification）

### 静态验证（grep / 文件路径）

- `AssetManagerConfig.EnableHotfix` 字段定义：`Assets/Framework/Scripts/Runtime/Modules/Asset/Managers/AssetManager/Definitions/AssetManagerConfig.cs`
- `AssetComponent.EnableHotfix` 属性暴露：`Assets/Framework/Scripts/Runtime/Modules/Asset/AssetComponent.cs`
- `Nova.Asset.EnableHotfix` 业务层暴露：`Nova` 聚合器（具体路径由实施时确认）
- `ProcedureSplash` 二分跳转：`Assets/Framework/Scripts/Runtime/Modules/Procedure/Procedures/ProcedureSplash.cs`
- `AssetManager.BuildPlayModeOptions` PlayMode 覆盖：`Assets/Framework/Scripts/Runtime/Modules/Asset/Managers/AssetManager/Implements/AssetManager.Methods.cs`

### 运行时验证（Play Mode 双路径）

| 路径 | 配置 | 期望 |
|---|---|---|
| 默认热更链路 | `EnableHotfix=true` | 启动经 Splash → CheckVersion → ...，Test.cs 全 PASS |
| 关闭热更直跳 | `EnableHotfix=false`（Inspector 切换后入 Play Mode） | 启动经 Splash → LoadDll，业务 Procedure 正常进入；`Nova.Asset.LoadAsync` 正常返回；启动期不发版本检查 HTTP 请求 |

### code-reviewer 审查要点

- `ProcedureSplash.GetNextProcedureType` 与 `OnUpdate` 的跳转目标必须一致（FSM 静态可达性）
- `EnableHotfix` 字段不在 `IAssetManager` 接口暴露（保持接口纯粹）
- `Nova.Asset.EnableHotfix` 仅暴露 getter，不允许 setter（运行时切换破坏 PlayMode 一致性）

## 关联

- **实现落点**：Asset 模块配置层、`ProcedureSplash` 分流逻辑、`AssetManager.BuildPlayModeOptions`
- **相关 ADR**：
  - [[ADR-001-component-manager-three-layer|ADR-001]]（Component + Manager 三层继承链，本 ADR 不冲击）
  - [[ADR-007-procedure-tier-split|ADR-007]]（Procedure 分档：框架内置 vs 业务延迟注册，本 ADR 只动框架内置 Procedure 内部跳转）
  - [[ADR-012-third-party-info-isolation|ADR-012]]（`EnableHotfix` 字段名不带 YooAsset / HybridCLR 等三方品牌名，合规）
- **相关 Pattern**：[[PAT-09-inspector-config-i18n|PAT-09]]（Inspector 字段定义 = 重要程度排序，本 ADR 落地时 `AssetManagerConfig` 重排顺序按其规范）
- **关联开放问题（已敲定，落地时按下表）**：

| OQ | 决议 |
|---|---|
| OQ-1 闪屏→LoadDll 空窗 UI | 不补 UI（LoadDll 通常 < 200ms） |
| OQ-2 LoadDll 跳转预写 SkipRequired | 写入 |
| OQ-3 BootstrapAsync 调用归属 | 已确认在 `ProcedureLoadDll.Methods.cs:52`，不在被跳过路径，无需迁移 |
| OQ-4 Nova.Asset 暴露 EnableHotfix | 暴露只读属性 |
| OQ-5 关闭热更路径自动化测试 | 当前采用人工切换 Inspector 验证 |
