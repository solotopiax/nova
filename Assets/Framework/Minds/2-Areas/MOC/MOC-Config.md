---
id: MOC-Config
title: 配置系统图谱
summary: Config主数据保存维度整理运行快照与资源密钥导出边界
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
  - "[[ADR-090-resource-keys-before-config-loading|ADR-090]]"
  - "[[PAT-161-configwindow-new-panel-dimension-header|PAT-161]]"
  - "[[MOC-Asset]]"
---

# MOC-Config：配置系统图谱

## 一句话

Config 的核心分工是：`ConfigMasterSO` 负责编辑期全量主数据，`ConfigRuntimeSO` 负责运行时单切片数据；运行时只读导出物，不回头依赖编辑期结构。

## 何时查这页

- 要改配置加载、导出、读取边界
- 要分清 `ConfigMasterSO` 和 `ConfigRuntimeSO`
- 要确认 SDK / Kit / HybridCLR / 维度掩码配置落在哪一层

## 双 SO 分工

- `ConfigMasterSO`：编辑期主 SO，承载全量矩阵、启用清单、掩码、Override、导出目标及资源密钥配置
- `ConfigRuntimeSO`：运行时导出物，承载当前平台/渠道/模式下的已解析切片

运行时高频读取面：

- `IsLoadOver`
- `DevelopMode`
- `AppConfigs`
- `Namespace`
- `HybridConfigs`
- `Custom`
- `Platform / Channel`
- `GetSDKPluginConfig<T>()`
- `GetKitConfig<T>()`

入口与 DLL 列表在 `HybridConfigs.GameEntranceProcedureName / AotMetadataDlls / StartupGameDlls`，不是 `IConfigManager` 顶层字段。

## 关键边界

- `LoadAsync()` 由框架启动链路调用
- `ProcedureLoadDll` 在加载业务 DLL 前会先确保 Config 已加载
- 业务层日常读取走 `Nova.Config`，不应自己重新驱动加载流程
- 运行时真实数据源是 `ConfigRuntimeSO`
- `ConfigManager` 不额外维护一套平行配置缓存，而是围绕导出物做读取
- `Namespace` 是业务代码命名空间，与 asmdef 的程序集名、生成 DLL 文件名分别核对
- `HybridConfigs.GameEntranceProcedureName` 是相对类型名，由 `ProcedureLoadDll` 和 `Namespace` 拼出完整入口
- AOT metadata 与业务 DLL 列表也由 Config 导出结果提供

## 维度系统

编辑期维度化入口集中在 `ConfigMasterSO`：

- 矩阵面板：`AppConfigsMask / PrivacyConfigsMask / SDKMasks / KitMasks`
- 顶层面板：`NamespaceMask / HybridEditorConfigsMask / YooAssetEditorConfigsMask / CDNEditorConfigsMask`，分别对应 `NamespaceOverrides / HybridEditorConfigsOverrides / YooAssetEditorConfigsOverrides / CDNEditorConfigsOverrides`
- 资源密钥：`ResourceEncryption.DimensionMask` 与各 Package 的 `ActiveKeyOverrides`，只区分当前加密编号，`Keys` 全量共用
- 掩码和 Override 只属于编辑期组织方式；运行时看到的是导出后的有效切片
- CDN 部署配置 `CDNEditorConfigs` 为 Editor-only，整套按维度快照，不导出 Runtime

## 保存与导出

- ConfigWindow 编辑 WorkingCopy，字段变化统一点亮顶部保存；保存按当前掩码自动清理无效分支并原子写回 Master，正常整理不再要求确认。
- 未勾选轴共用值，已勾选轴逐组保留。矩阵保留完整网格并同步共用组，顶层 Override 清理旧分支，资源密钥字典不随减维删除。
- 顶部导出和 Pipify Config 导出读取已保存的 Master：生成明确坐标的 ConfigRuntime，并独立生成所选 Assets 目录中的 `ResourceKeys.Generated.cs`。保存不生成源码；导出只读校验，不隐式修正 Master。
- 资源密钥源码属于启动可用的 Player 程序集，在 ConfigRuntime 资源加载前注册提供器；密钥不写入 ConfigRuntime，也不从独立 JSON 或环境变量取得第二来源。
- 编辑平台是窗口状态，生产导出平台以 Active BuildTarget 为约束；Channel/DevelopMode 由 Master 保存。不得把编辑坐标直接当作已经生效的构建平台。

## 与其他模块的关系

- `Procedure`：读取 `Namespace / HybridConfigs.GameEntranceProcedureName / HybridConfigs 的 DLL 列表`
- `SDK`：按类型读取 `EnabledSDKConfigs`
- `Kit`：按类型读取 `EnabledKitConfigs`
- `Asset`：通过 ConfigRuntime 加载运行配置；解密所需密钥先由独立生成提供器可用，避免循环依赖

## 常见误区

- 把 `ConfigMasterSO` 当运行时数据源
- 在业务代码里主动调用 `IConfigManager.LoadAsync()`
- 忘记 `GameEntranceProcedureName` 是相对类型名，不是完整全名
- 以为维度掩码会在运行时继续参与复杂解析
- 以为取消“分别配置”仍应保留失效旧分支，或把清理解释为删除仍有效的物理格
- 以为保存会自动生成客户端密钥源码，或把源码放进热更程序集即可满足启动解密

## 先往哪看

- 改 Namespace 口径：[[ADR-005-hybridclr-namespace-single-write-path]]
- 改访问边界：[[ADR-016-framework-vs-business-access]]
- 改 Kit 三维配置：[[ADR-054-kit-config-three-dim-matrix]]
- 改面板级维度化：[[ADR-058-per-panel-dimension-mask]]
- 改资源密钥配置与启动来源：[[ADR-090-resource-keys-before-config-loading]]
- 新增配置页：[[PAT-161-configwindow-new-panel-dimension-header]]

## 来源

- `Assets/Framework/Scripts/Editor/Config/ConfigMasterSO.cs`
- `Assets/Framework/Scripts/Runtime/Modules/Config/Managers/Interfaces/IConfigManager.cs`
- `Assets/Framework/Docs/Editor/Config/ConfigMasterSO.md`、`Assets/Framework/Docs/Runtime/Modules/Asset/ResourceEncryption.md`

## 关联

- 图谱：[[MOC-HybridCLR]]、[[MOC-SDK]]、[[MOC-Procedure]]、[[MOC-Asset]]
