---
id: MOC-Config
title: 配置系统图谱
summary: ConfigMasterSO 与 ConfigRuntimeSO 的分工与运行时入口速查
category: runtime
status: active
date: 2026-06-05
aliases:
  - MOC-Config
  - 配置系统图谱
  - Config图谱
tags: [moc, nova, config, runtime, hybridclr, sdk]
keywords:
  - ConfigManager
  - ConfigComponent
  - ConfigRuntimeSO
  - ConfigMasterSO
  - PanelDimensionMask
  - Namespace
  - GameEntranceProcedureName
  - EnabledSDKConfigs
  - EnabledKitConfigs
related:
  - "[[ADR-001-component-manager-three-layer|ADR-001]]"
  - "[[ADR-005-hybridclr-namespace-single-write-path|ADR-005]]"
  - "[[ADR-010-validation-on-consumer-side|ADR-010]]"
  - "[[ADR-013-hotfix-master-switch|ADR-013]]"
  - "[[ADR-016-framework-vs-business-access|ADR-016]]"
  - "[[ADR-022-sdk-plugin-architecture|ADR-022]]"
  - "[[ADR-047-editor-active-master-anchor|ADR-047]]"
  - "[[ADR-054-kit-config-three-dim-matrix|ADR-054]]"
  - "[[ADR-058-per-panel-dimension-mask|ADR-058]]"
  - "[[PAT-27-config-no-serialize|PAT-27]]"
---

# MOC-Config：配置系统图谱

## 一句话

Config 的核心分工是：`ConfigMasterSO` 负责编辑期全量主数据，`ConfigRuntimeSO` 负责运行时单切片数据；运行时只读导出物，不回头依赖编辑期结构。

## 何时查这页

- 要改配置加载、导出、读取边界
- 要分清 `ConfigMasterSO` 和 `ConfigRuntimeSO`
- 要确认 SDK / Kit / HybridCLR / 维度掩码配置落在哪一层

## 双 SO 分工

- `ConfigMasterSO`：编辑期主 SO，承载全量矩阵、启用清单、掩码、Override、导出目标
- `ConfigRuntimeSO`：运行时导出物，承载当前平台/渠道/模式下的已解析切片

运行时高频读取面：

- `IsLoadOver`
- `DevelopMode`
- `Common`
- `Namespace`
- `Platform / Channel`
- `GameEntranceProcedureName`
- `AotMetadataDlls / GameDlls`
- `GetSDKPluginConfig<T>()`
- `GetKitConfig<T>()`

## 关键边界

- `LoadAsync()` 由框架启动链路调用
- `ProcedureLoadDll` 在加载业务 DLL 前会先确保 Config 已加载
- 业务层日常读取走 `Nova.Config`，不应自己重新驱动加载流程
- 运行时真实数据源是 `ConfigRuntimeSO`
- `ConfigManager` 不额外维护一套平行配置缓存，而是围绕导出物做读取
- `Namespace` 是业务程序集命名空间口径
- `GameEntranceProcedureName` 是相对类型名，由 `ProcedureLoadDll` 和 `Namespace` 拼出完整入口
- AOT metadata 与业务 DLL 列表也由 Config 导出结果提供

## 维度系统

编辑期维度化入口集中在 `ConfigMasterSO`：

- `CommonMask / SDKMasks / KitMasks`
- `NamespaceMask / HybridCLRMask / YooAssetMask`
- 掩码和 Override 只属于编辑期组织方式；运行时看到的是导出后的有效切片

## 与其他模块的关系

- `Procedure`：读取 `Namespace / GameEntranceProcedureName / DLL 列表`
- `SDK`：按类型读取 `EnabledSDKConfigs`
- `Kit`：按类型读取 `EnabledKitConfigs`
- `Asset`：热更开关和资源运行模式不归 Config 模块本体管理

## 常见误区

- 把 `ConfigMasterSO` 当运行时数据源
- 在业务代码里主动调用 `IConfigManager.LoadAsync()`
- 忘记 `GameEntranceProcedureName` 是相对类型名，不是完整全名
- 以为维度掩码会在运行时继续参与复杂解析

## 先往哪看

- 改 Namespace 口径：[[ADR-005-hybridclr-namespace-single-write-path]]
- 改访问边界：[[ADR-016-framework-vs-business-access]]
- 改 Kit 三维配置：[[ADR-054-kit-config-three-dim-matrix]]
- 改面板级维度化：[[ADR-058-per-panel-dimension-mask]]

## 关联

- 图谱：[[MOC-HybridCLR]]、[[MOC-SDK]]、[[MOC-Procedure]]
