---
id: GLO-11
title: HybridCLR 业务 DLL 热更
type: glossary
status: active
date: 2026-08-05
summary: HybridCLR 承载业务 DLL 热更
category: hotfix
source: docs-and-source-verification
aliases:
  - GLO-11-hybridclr-hotfix-dll
  - HybridCLR
  - hybridclr
keywords: [GLO-11, HybridCLR, StartupGameDlls, AotMetadataDlls, ProcedureLoadDll, EnableHotfix, AOT, 热更, 热修复]
tags: [glossary, nova, terminology, hybridclr, hotfix]
related:
  - "[[ADR-005-hybridclr-namespace-single-write-path|ADR-005]]"
  - "[[ADR-013-hotfix-master-switch|ADR-013]]"
  - "[[ADR-028-hybridclr-copy-aot-after-buildplayer|ADR-028]]"
  - "[[MOC-HybridCLR|MOC-HybridCLR]]"
---

# GLO-11：HybridCLR 业务 DLL 热更

## 定义

HybridCLR 用于在 IL2CPP Player 中加载业务 DLL。`ConfigRuntimeSO.HybridConfigs.StartupGameDlls` 决定启动业务 DLL 清单，`HybridConfigs.AotMetadataDlls` 提供 AOT 元数据清单。业务入口由 `ConfigRuntimeSO.Namespace` 和 `HybridConfigs.GameEntranceProcedureName` 定位。

## 边界

- `ProcedureLoadDll` 编排启动 DLL 加载，运行时按需加载由 `Util.HybridCLR.LoadGameAssemblyAsync` 提供。启动路由见[内置流程](../../../Docs/Runtime/Modules/Procedure/BuiltInProcedures.md)。
- `AssetComponent / AssetManagerConfig` 的 `EnableHotfix` 控制常规资源热更。Inspector 编辑期将关闭热更与 `OfflinePlayMode` 联动、将开启热更与 `HostPlayMode` 联动。WebGL 启动预热由资源策略决定。
- `Util.HybridCLR` 是运行时加载工具；`EditorUtil.HybridCLR` 是编辑器复制与校验工具，两者职责不混用。
- AOT metadata 的复制发生在 BuildPlayer 之后（ADR-028）；热更程序集命名空间遵循单一写入路径（ADR-005）。

## 易混淆项

- HybridCLR 热更的是 C# 业务 DLL，不是 AssetBundle/资源热更（资源走 YooAsset，见 GLO-10）。
- AOT metadata DLL 为热更代码补充泛型和反射元数据，先于 `StartupGameDlls` 加载。
- 启动业务程序集由配置提供，运行时按需加载复用 HybridCLR 入口。

## 示例

```text
ProcedureLoadDll:
  Asset Bootstrap + Manifest
  -> Config.LoadAsync
  -> 加载 AOT metadata（ConfigRuntimeSO.HybridConfigs.AotMetadataDlls）
  -> 加载业务 DLL（ConfigRuntimeSO.HybridConfigs.StartupGameDlls）
  -> RefreshAssemblies -> RegisterAdditionalProcedures
  -> ChangeState(业务入口 Procedure)
```

## 来源与验证

- [Minds/2-Areas/MOC/MOC-HybridCLR.md](../MOC/MOC-HybridCLR.md)：当前加载链与配置驱动口径。
- [Scripts/Runtime/Modules/Asset/AssetComponent.cs](../../../Scripts/Runtime/Modules/Asset/AssetComponent.cs) 与 [AssetManagerConfig.cs](../../../Scripts/Runtime/Modules/Asset/Managers/AssetManager/Definitions/AssetManagerConfig.cs)：`EnableHotfix` 定义与 PlayMode 双向联动注释。
- ADR-005 / ADR-013 / ADR-028 对应决策正文。
