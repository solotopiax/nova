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
keywords: [GLO-11, HybridCLR, GameDlls, AotMetadataDlls, ProcedureLoadDll, EnableHotfix, AOT, 热更, 热修复]
tags: [glossary, nova, terminology, hybridclr, hotfix]
related:
  - "[[ADR-005-hybridclr-namespace-single-write-path|ADR-005]]"
  - "[[ADR-013-hotfix-master-switch|ADR-013]]"
  - "[[ADR-028-hybridclr-copy-aot-after-buildplayer|ADR-028]]"
  - "[[MOC-HybridCLR|MOC-HybridCLR]]"
---

# GLO-11：HybridCLR 业务 DLL 热更

## 定义

HybridCLR 是 Nova 的热更运行时方案，用于在 IL2CPP 下运行时加载业务 DLL。业务 DLL 清单（`ConfigRuntimeSO.HybridConfigs.GameDlls`）、AOT 元数据清单（`ConfigRuntimeSO.HybridConfigs.AotMetadataDlls`）与业务入口（`Namespace + HybridConfigs.GameEntranceProcedureName`）全部由 `ConfigRuntimeSO` 配置驱动。

## 边界

- 启动必经 `ProcedureSplash -> ProcedureCheckVersion`；版本检查随后按结果进入 `ProcedureAppDownload`、`ProcedureHotfix` 或直接进入 `ProcedureLoadDll`，检查异常时也降级进入 `ProcedureLoadDll`。`ProcedureLoadDll` 是业务 DLL 加载的唯一入口。
- `EnableHotfix` 位于 `AssetComponent / AssetManagerConfig`，是热更主开关（ADR-013）：false ⇔ `OfflinePlayMode`，true ⇔ `HostPlayMode`；WebGL 由平台适配层选择 Web 文件系统。
- `Util.HybridCLR` 是运行时加载工具；`EditorUtil.HybridCLR` 是编辑器复制与校验工具，两者职责不混用。
- AOT metadata 的复制发生在 BuildPlayer 之后（ADR-028）；热更程序集命名空间遵循单一写入路径（ADR-005）。

## 易混淆项

- HybridCLR 热更的是 C# 业务 DLL，不是 AssetBundle/资源热更（资源走 YooAsset，见 GLO-10）。
- AOT metadata DLL 不是业务 DLL：它是为热更代码补充泛型/反射元数据，先于 GameDlls 加载。
- 不要绕过 `ProcedureLoadDll` 自建 DLL 加载入口，也不要在业务侧硬编码程序集名。

## 示例

```text
ProcedureLoadDll:
  Asset Bootstrap + Manifest
  -> Config.LoadAsync
  -> 加载 AOT metadata（ConfigRuntimeSO.HybridConfigs.AotMetadataDlls）
  -> 加载业务 DLL（ConfigRuntimeSO.HybridConfigs.GameDlls）
  -> RefreshAssemblies -> RegisterAdditionalProcedures
  -> ChangeState(业务入口 Procedure)
```

## 来源与验证

- `Assets/Framework/Minds/2-Areas/MOC/MOC-HybridCLR.md`：当前加载链与配置驱动口径。
- `Assets/Framework/Scripts/Runtime/Modules/Asset/AssetComponent.cs` 与 `AssetManagerConfig.cs`：`EnableHotfix` 定义与 PlayMode 双向联动注释。
- ADR-005 / ADR-013 / ADR-028 对应决策正文。
