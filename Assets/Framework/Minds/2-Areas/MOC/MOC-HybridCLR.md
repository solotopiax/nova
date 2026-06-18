---
id: MOC-HybridCLR
title: HybridCLR / 热更链路图谱
summary: ProcedureSplash 到 ProcedureLoadDll 的热更加载与入口定位速查
category: hotfix
status: active
date: 2026-06-05
aliases:
  - MOC-HybridCLR
  - 热更链路图谱
  - HybridCLR 图谱
tags: [moc, nova, hybridclr, hotfix, procedure, runtime]
keywords: [HybridCLR, Hotfix, Procedure, ProcedureSplash, ProcedureCheckVersion, ProcedureLoadDll, AOT, GameDlls, AotMetadataDlls, GameEntranceProcedureName, Namespace, 热更, 热修复, 补丁]
related:
  - "[[ADR-005-hybridclr-namespace-single-write-path|ADR-005]]"
  - "[[ADR-007-procedure-tier-split|ADR-007]]"
  - "[[ADR-013-hotfix-master-switch|ADR-013]]"
  - "[[ADR-028-hybridclr-copy-aot-after-buildplayer|ADR-028]]"
  - "[[ADR-032-drop-novabehaviour-bridge|ADR-032]]"
---

# MOC-HybridCLR：热更链路图谱

## 一句话

Nova 当前热更链路是“`ProcedureSplash -> ProcedureCheckVersion? -> ProcedureLoadDll` + `ConfigRuntimeSO` 配置驱动的 DLL 列表与入口定位”，不是旧版固定文件或固定程序集名的说明书。

## 业务语言入口

| 用户说 | 等同 |
|---|---|
| 热更 / 热修复 / 差量更新 | HybridCLR + Hotfix + ProcedureLoadDll |
| 业务 dll | `ConfigRuntimeSO.GameDlls` |
| AOT metadata | `ConfigRuntimeSO.AotMetadataDlls` |
| 业务入口 | `ConfigRuntimeSO.Namespace + GameEntranceProcedureName` |
| 启动流程 | `ProcedureSplash -> ProcedureCheckVersion? -> ProcedureLoadDll` |

## 当前结构

```text
ProcedureSplash
  -> ProcedureCheckVersion   (仅 EnableHotfix=true 时进入)
  -> ProcedureLoadDll
      -> Asset Bootstrap + Manifest
      -> Config.LoadAsync
      -> Load AOT metadata
      -> Load Game DLLs
      -> RefreshAssemblies
      -> RegisterAdditionalProcedures
      -> ChangeState(业务入口 Procedure)
```

关键边界：

- `EnableHotfix` 在 `AssetComponent / AssetManagerConfig`
- `ProcedureLoadDll` 是业务 DLL 加载唯一入口
- DLL 清单、入口 Procedure、业务程序集名都由 `ConfigRuntimeSO` 提供
- `Util.HybridCLR` 是运行时加载工具；`EditorUtil.HybridCLR` 是编辑器复制与校验工具

## 关联 ADR

| ADR | 标题 | 一句要点 | status |
|---|---|---|---|
| [[ADR-005-hybridclr-namespace-single-write-path\|ADR-005]] | namespace == asmdef 唯一写入路径 | ConfigRuntimeSO.Namespace 只能由 ConfigWindow 写 | accepted |
| [[ADR-007-procedure-tier-split\|ADR-007]] | Procedure 分档（框架 vs 业务） | 框架 Procedure 在 NovaFramework.Runtime；业务 Procedure 走热更程序集 | accepted |
| [[ADR-013-hotfix-master-switch\|ADR-013]] | Hotfix 总开关 | EnableHotfix 不在 App，而在 Asset | accepted |
| [[ADR-028-hybridclr-copy-aot-after-buildplayer\|ADR-028]] | BuildPlayer 后拷 AOT | 拷贝时机硬约束 | accepted |
| [[ADR-032-drop-novabehaviour-bridge\|ADR-032]] | 弃用 NovaBehaviour 桥接 | 业务直接继承 MonoBehaviour | accepted |
| [[ADR-019-yooasset-release-mandatory\|ADR-019]] | YooAsset 资源 | dll 字节作为 manifest 资源加载 | accepted |
| [[ADR-020-assembly-dependency-direction\|ADR-020]] | 程序集依赖方向 | 业务程序集只单向依赖框架 | accepted |
| [[ADR-050-hotfix-batch-fail-user-decision\|ADR-050]] | Hotfix 失败用户决策 | 失败重试与用户决策分层处理 | accepted |
| [[ADR-051-launch-asset-slice-strategy\|ADR-051]] | 启动切片策略 A/B | API 不绑具体切片策略 | accepted |

## 当前事实提醒

- `ProcedureLaunch` 已合并进 `ProcedureSplash`，不要再写旧文件名
- “业务 DLL = `Game.Runtime`” 不是硬编码事实；当前是 `GameDlls + Namespace` 配置驱动
- `ProcedureLoadDll` 不只做 DLL 加载，还负责 `Yield` 脱栈、Config 幂等加载、程序集刷新、扫描注册业务 Procedure、跳转入口
- `Util.HybridCLR` 当前路径在 `Runtime/Core/Util/Util.HybridCLR/Util.HybridCLR.cs`
- 编辑器侧 HybridCLR 配置与导出入口在 `Editor/Windows/ConfigWindow`、`Editor/EditorUtil/EditorUtil.HybridCLR`

## 反模式

| 反模式 | 处理 |
|---|---|
| 业务侧直接 `Assembly.Load` | 走 `ProcedureLoadDll` |
| 框架 Procedure 放进业务热更程序集 | 框架流程留在 `NovaFramework.Runtime` |
| 在 Fsm OnEnter / OnLeave 同步栈内注册业务 Procedure | 先 `await UniTask.Yield()` 脱栈 |
| 业务 Prefab 在 DLL 完成前提前加载 | 延后到 `ProcedureLoadDll` 完成之后 |
| 引入 NovaBehaviour / IBaseLife / `m_ScriptName` 字符串桥接 | 已废止 |
| 把补丁 DLL 和资源流水线拆成两套 | 保持 Pipify 原子构建 |

## 模块文件入口

```text
Assets/Framework/Scripts/Runtime/Modules/Procedure/Procedures/
├── ProcedureSplash.cs
├── ProcedureCheckVersion.cs
├── ProcedureHotfix.cs
├── ProcedureAppDownload.cs
└── ProcedureLoadDll.cs

Assets/Framework/Scripts/Runtime/Core/Util/Util.HybridCLR/
└── Util.HybridCLR.cs

Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.HybridCLR/
└── EditorUtil.HybridCLR.cs / .Methods.cs / .Visitors.cs
```

## 必走流程

1. 读本 MOC
2. 涉及 Procedure 分档 → [[ADR-007-procedure-tier-split|ADR-007]]
3. 涉及程序集 / namespace → [[ADR-005-hybridclr-namespace-single-write-path|ADR-005]] + [[ADR-020-assembly-dependency-direction|ADR-020]]
4. 涉及热更开关 → [[ADR-013-hotfix-master-switch|ADR-013]]
5. 涉及 DLL 加载链路 → `ProcedureLoadDll.cs`

## 关联

- 图谱：[[MOC-Asset]]、[[MOC-Procedure]]、[[MOC-Manager]]
