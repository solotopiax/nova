---
id: MOC-HybridCLR
title: HybridCLR / 热更链路图谱
summary: HybridCLR加载链与入口速查
category: hotfix
status: active
date: 2026-06-05
aliases:
  - MOC-HybridCLR
  - 热更链路图谱
  - HybridCLR 图谱
tags: [moc, nova, hybridclr, hotfix, procedure, runtime]
keywords: [HybridCLR, Hotfix, Procedure, ProcedureSplash, ProcedureCheckVersion, ProcedureHotfix, ProcedureLoadDll, AOT, StartupGameDlls, AotMetadataDlls, GameEntranceProcedureName, Namespace, 热更, 热修复, 补丁]
related:
  - "[[ADR-005-hybridclr-namespace-single-write-path|ADR-005]]"
  - "[[ADR-007-procedure-tier-split|ADR-007]]"
  - "[[ADR-013-hotfix-master-switch|ADR-013]]"
  - "[[ADR-028-hybridclr-copy-aot-after-buildplayer|ADR-028]]"
  - "[[ADR-032-drop-novabehaviour-bridge|ADR-032]]"
---

# MOC-HybridCLR：热更链路图谱

## 一句话

Nova 启动先检查版本和资源工作，随后由 `ProcedureLoadDll` 按 `ConfigRuntimeSO` 的配置加载启动 DLL、注册流程并跳转业务入口。

## 业务语言入口

| 用户说 | 等同 |
|---|---|
| 热更 / 热修复 / 差量更新 | HybridCLR + Hotfix + ProcedureLoadDll |
| 启动业务 DLL | `ConfigRuntimeSO.HybridConfigs.StartupGameDlls` |
| AOT metadata | `ConfigRuntimeSO.HybridConfigs.AotMetadataDlls` |
| 业务入口 | `ConfigRuntimeSO.Namespace` 和 `ConfigRuntimeSO.HybridConfigs.GameEntranceProcedureName` |
| 启动流程 | [内置流程](../../../Docs/Runtime/Modules/Procedure/BuiltInProcedures.md) |

## 当前结构

```text
ProcedureSplash
  -> ProcedureCheckVersion
      根据检查结果处理 App 更新或启动资源工作
  -> ProcedureLoadDll（继续启动时）
      -> 准备 Asset Bootstrap 和 Manifest
      -> Config.LoadAsync
      -> Load AOT metadata
      -> Load Game DLLs
      -> RefreshAssemblies
      -> RegisterAdditionalProcedures
      -> ChangeState(业务入口 Procedure)
```

关键边界：

- `AssetComponent / AssetManagerConfig` 的 `EnableHotfix` 控制常规资源热更。启动检查和 WebGL 预热按 [版本检查](../../../Docs/Runtime/Modules/Procedure/ProcedureCheckVersion.md) 分流。
- `ProcedureLoadDll` 编排启动 DLL 加载，运行时按需加载业务程序集使用 `Util.HybridCLR.LoadGameAssemblyAsync`。
- DLL 清单、入口 Procedure、业务程序集名都由 `ConfigRuntimeSO` 提供
- `Util.HybridCLR` 是运行时加载工具；`EditorUtil.HybridCLR` 是编辑器复制与校验工具

## 关联 ADR

| ADR | 标题 | 一句要点 | status |
|---|---|---|---|
| [[ADR-005-hybridclr-namespace-single-write-path\|ADR-005]] | namespace == asmdef 唯一写入路径 | 设计态保存到 Master，再导出运行时的 Namespace | accepted |
| [[ADR-007-procedure-tier-split\|ADR-007]] | Procedure 分档（框架 vs 业务） | 框架 Procedure 在 NovaFramework.Runtime；业务 Procedure 走热更程序集 | accepted |
| [[ADR-013-hotfix-master-switch\|ADR-013]] | Hotfix 总开关 | EnableHotfix 不在 App，而在 Asset | accepted |
| [[ADR-028-hybridclr-copy-aot-after-buildplayer\|ADR-028]] | BuildPlayer 后拷 AOT | 拷贝时机硬约束 | accepted |
| [[ADR-032-drop-novabehaviour-bridge\|ADR-032]] | 弃用 NovaBehaviour 桥接 | 业务直接继承 MonoBehaviour | accepted |
| [[ADR-042-assetmanager-load-api-all-return-handle\|ADR-042]] | 资源句柄所有权 | DLL 资源也沿加载句柄管理生命周期 | accepted |
| [[ADR-020-assembly-dependency-direction\|ADR-020]] | 程序集依赖方向 | 业务程序集只单向依赖框架 | accepted |
| [[ADR-050-hotfix-batch-fail-user-decision\|ADR-050]] | Hotfix 失败用户决策 | 失败重试与用户决策分层处理 | accepted |
| [[ADR-051-launch-asset-slice-strategy\|ADR-051]] | 启动切片策略 A/B | API 不绑具体切片策略 | accepted |

## 当前事实提醒

- `ProcedureLaunch` 已合并进 `ProcedureSplash`，不要再写旧文件名
- 启动 DLL 清单取自 `HybridConfigs.StartupGameDlls`，业务程序集按 `Namespace` 定位。
- `ProcedureLoadDll` 不只做 DLL 加载，还负责 `Yield` 脱栈、Config 幂等加载、程序集刷新、扫描注册业务 Procedure、跳转入口
- `Util.HybridCLR` 当前路径在 [Runtime/Core/Util/Util.HybridCLR/Util.HybridCLR.cs](../../../Scripts/Runtime/Core/Util/Util.HybridCLR/Util.HybridCLR.cs)
- 编辑器侧 HybridCLR 配置与导出入口在 [Editor/Windows/ConfigWindow](../../../Scripts/Editor/Windows/ConfigWindow/)、`Editor/EditorUtil/EditorUtil.HybridCLR`

## 反模式

| 反模式 | 处理 |
|---|---|
| 业务侧直接 `Assembly.Load` | 启动由 `ProcedureLoadDll` 编排，运行时按需加载走 `Util.HybridCLR.LoadGameAssemblyAsync` |
| 框架 Procedure 放进业务热更程序集 | 框架流程留在 `NovaFramework.Runtime` |
| 在 Fsm OnEnter / OnLeave 同步栈内注册业务 Procedure | 先 `await UniTask.Yield()` 脱栈 |
| 业务 Prefab 在 DLL 完成前提前加载 | 延后到 `ProcedureLoadDll` 完成之后 |
| 引入 NovaBehaviour / IBaseLife / `m_ScriptName` 字符串桥接 | 已废止 |
| 把补丁 DLL 和资源流水线拆成两套 | 保持 Pipify 原子构建 |

## 模块文件入口

- [框架启动流程](../../../Scripts/Runtime/Modules/Procedure/Procedures/)：[ProcedureSplash.cs](../../../Scripts/Runtime/Modules/Procedure/Procedures/ProcedureSplash.cs)、[ProcedureCheckVersion.cs](../../../Scripts/Runtime/Modules/Procedure/Procedures/ProcedureCheckVersion.cs)、[ProcedureHotfix.cs](../../../Scripts/Runtime/Modules/Procedure/Procedures/ProcedureHotfix.cs)、[ProcedureAppDownload.cs](../../../Scripts/Runtime/Modules/Procedure/Procedures/ProcedureAppDownload.cs)、[ProcedureLoadDll.cs](../../../Scripts/Runtime/Modules/Procedure/Procedures/ProcedureLoadDll.cs)。
- [运行时 HybridCLR 工具](../../../Scripts/Runtime/Core/Util/Util.HybridCLR/)：[Util.HybridCLR.cs](../../../Scripts/Runtime/Core/Util/Util.HybridCLR/Util.HybridCLR.cs)。
- [编辑器 HybridCLR 工具](../../../Scripts/Editor/EditorUtil/EditorUtil.HybridCLR/)：[EditorUtil.HybridCLR.cs](../../../Scripts/Editor/EditorUtil/EditorUtil.HybridCLR/EditorUtil.HybridCLR.cs)、[EditorUtil.HybridCLR.Methods.cs](../../../Scripts/Editor/EditorUtil/EditorUtil.HybridCLR/EditorUtil.HybridCLR.Methods.cs)、[EditorUtil.HybridCLR.Visitors.cs](../../../Scripts/Editor/EditorUtil/EditorUtil.HybridCLR/EditorUtil.HybridCLR.Visitors.cs)。

## 必走流程

1. 读本 MOC
2. 涉及 Procedure 分档 → [[ADR-007-procedure-tier-split|ADR-007]]
3. 涉及程序集 / namespace → [[ADR-005-hybridclr-namespace-single-write-path|ADR-005]] + [[ADR-020-assembly-dependency-direction|ADR-020]]
4. 涉及热更开关 → [[ADR-013-hotfix-master-switch|ADR-013]]
5. 涉及 DLL 加载链路 → [ProcedureLoadDll.cs](../../../Scripts/Runtime/Modules/Procedure/Procedures/ProcedureLoadDll.cs)

## 关联

- 图谱：[[MOC-Asset]]、[[MOC-Procedure]]、[[MOC-Manager]]
