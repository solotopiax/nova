---
id: ADR-060
title: YooAssetSettings 只保留 Editor 权威源并在 Player 构建期临时 staging
status: accepted
summary: 构建期临时生成唯一 YooAssetSettings 副本
category: asset
date: 2026-06-02
aliases:
  - ADR-060-yooasset-settings-global-resources-copy
  - 本工程直出包时放全局 YooAssetSettings 副本松绑 ADR-049
keywords:
  - ADR-060
  - YooAssetSettings 构建期 staging
  - ADR-060-yooasset-settings-global-resources-copy
tags:
  - nova
  - asset
  - yooasset
  - config
---

# ADR-060：YooAssetSettings 只保留 Editor 权威源并在 Player 构建期临时 staging

## 背景

`YooAssetConfiguration` 的 Player 运行时仍通过 `Resources.Load<YooAssetSettings>("YooAssetSettings")` 取得配置，但仓库中常驻副本会与 `ConfigMasterSO` 的三维路径发生漂移。多 Sample 共存时，每份 Resources 副本还会重新引入不确定命中；UPM 发布期复制只能固定发布瞬间的内容，也无法代表消费工程当前激活的 ConfigMaster。

## 决策

- `Editor/YooAssetSettings.asset` 是 Sample 内唯一权威源；仓库和 UPM 发布产物都不保存常驻 `Resources/YooAssetSettings.asset`。
- 正式 Player 构建前，以 `WorkspaceActive.Get()` 的当前 `ConfigMasterSO` 为锚点，按 Platform / Channel / DevelopMode 调用 `DimensionalResolver.ResolveYooAsset`，从解析结果取得权威源路径。
- 开发态临时副本放在当前 Demo 根目录 `Resources`。UPM Sample 消费态只在当前 Demo 目录树内由浅到深选择已有 `Resources`，同层按路径稳定排序；无候选时创建 Demo 根目录 `Resources`。
- 构建前若发现任何其他常驻运行时副本，直接中止，不覆盖用户资产。临时副本通过 `AssetDatabase.CopyAsset` 生成独立 GUID。
- 构建结束、失败或取消后清理；使用 `Library/Nova/YooAssetRuntimeSettingsStaging.json` 记录所有权并校验正文哈希，覆盖域重载、Editor 退出和下次启动恢复。内容已被外部修改时保留文件，禁止误删。
- Editor Play Mode 不生成临时副本；在 `BeforeSceneLoad` 按当前三维坐标重新注入 Editor 权威源。
- HybridCLR `StrippedAOTDllsTempProj` 内部 Player 构建跳过该流程。

## 影响

- Player 产物仍包含 YooAsset 所需的唯一 Resources 配置，但工作树和 UPM Sample 不再承担易漂移副本。
- Build Profiles、Pipify、Debug Inspector 和 CLI 等所有正式 `BuildPipeline.BuildPlayer` 入口共享同一构建回调。
- 构建依赖当前场景能解析出激活 ConfigMaster；路径缺失、资产缺失或常驻副本冲突会 fail-fast。
- ADR-049 的 ConfigMaster 显式路径继续是唯一配置选择契约，本决策不再对其松绑。

## 被排除方案

- 工程根永久全局副本：无法表达当前 ConfigMaster 三维坐标，且容易忘记同步。
- 每个 Sample 永久副本：`Resources.Load` 无目录维度，多份同名资产会重新产生不确定性。
- UPM 发布时永久复制：发布时配置不能代表消费工程的当前选择，并把冗余带入所有导入工程。
- 只依赖构建后处理清理：构建取消、回调异常、域重载或进程崩溃时缺少恢复保证。

## 验证依据

- EditMode 契约测试覆盖开发态与消费态目标选择、HybridCLR 跳过、冲突拒绝、复制/正常清理和内容变化时不误删。
- 正式 Player 构建需验证产物能加载当前 Settings，且构建后工作树无临时资产与 marker 残留。

## 关联

- [[ADR-049-yooasset-settings-via-configmaster|ADR-049]]
