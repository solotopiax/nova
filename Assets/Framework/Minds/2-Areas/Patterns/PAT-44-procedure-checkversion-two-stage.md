---
id: PAT-44
title: ProcedureCheckVersion 两阶段编排：App 大版本 → Asset 资源差异，ForcedDownload 短路
type: pattern
status: active
date: 2026-05-19
summary: ProcedureCheckVersion两阶段拆分
category: hotfix
aliases:
  - PAT-44-procedure-checkversion-two-stage
keywords:
  - PAT-44
  - PAT-44-procedure-checkversion-two-stage
tags:
  - pattern
  - procedure
  - hotupdate
  - orchestration
related: []
---

# PAT-44：ProcedureCheckVersion 两阶段编排：App 大版本 → Asset 资源差异，ForcedDownload 短路

## 适用场景

启动流程需要独立判断 App 大版本更新、常规资源补丁和 WebGL 启动资源。三者合并决定后续路由。

## 核心做法

### 启动检查分两阶段

1. 先判断 App 大版本更新。强更结果直接进入 `ProcedureAppDownload`。WebGL 的 App 阶段采用 `NoDownload`。
2. 计算本次需要的启动资源工作。`EnableHotfix` 控制常规资源检查和下载。WebGL 按资源策略另行决定是否加载清单并预热，选用 `TagsOnLaunch` 或 `AllOnLaunch` 时，即使关闭常规资源热更也可进入启动资源流程。

### 路由条件

| AppVersionResult | RequiresStartupAssetWork | 下一流程 |
|---|---|---|
| `ForcedDownload` | 任意 | `ProcedureAppDownload` |
| `RecommendedDownload` | 任意 | `ProcedureAppDownload` |
| `NoDownload` | `true` | `ProcedureHotfix` |
| `NoDownload` | `false` | `ProcedureLoadDll` |

推荐更新取消后也按 `RequiresStartupAssetWork` 继续到 `ProcedureHotfix` 或 `ProcedureLoadDll`。`ProcedureCheckVersion` 将 `AppVersionResult`、`HasAssetPatch` 和 `RequiresStartupAssetWork` 分开记录。`HasAssetPatch` 表示常规补丁判断结果，后续启动路由读取 `RequiresStartupAssetWork`。

### 接口边界

- `IAppManager.CheckAsync` 的缺配和远端失败处理见 [[PAT-43-optional-remote-check-tolerance|PAT-43]]。
- `IAssetManager.LoadManifestAsync` 在启动阶段被重复调用时复用已就绪的清单。
- 浏览器 WebGL 使用独立的启动预热判断，平台适配可能提供自己的预下载策略，见 [[ADR-085-webgl-asset-strategies-and-warmup-group|ADR-085]] 和 [版本检查](../../../Docs/Runtime/Modules/Procedure/ProcedureCheckVersion.md)。

## 为什么这么做

App 更新和资源工作有各自的目的。强更优先可省去本轮资源检查，推荐更新则需要保留后续分流信息。独立记录补丁结果与资源工作需求，可以表达 WebGL 无常规补丁但仍需预热的场景。

## 反模式（Anti-patterns）

```csharp
// ❌ 用单枚举压扁两类语义
public enum LaunchCheckResult { SkipRequired, HotfixRequired, DownloadRequired, Failed }

// ❌ 没短路，强更也跑资源检查
var app = await CheckAsync();
var hasPatch = await HasPatchAsync();   // ForcedDownload 时这一步浪费
if (app == ForcedDownload) ChangeState<AppDownload>();

// ❌ LoadManifestAsync 不幂等，前移后下游重复调用拉两次清单
await LoadManifestAsync();  // 在 CheckVersion
// ...
await LoadManifestAsync();  // 在 LoadDll —— 第二次重新发网络请求

// ❌ 让 RecommendedDownload 在做完热更检查前就直接短路
//    这样会丢失用户取消后应回到 Hotfix / LoadDll 的分流信息

// ❌ 用 EnableHotfix 决定是否进入 ProcedureCheckVersion
//    这会把 App 大版本检测和资源热更链路重新耦合
```

## 跨项目复用提示

可复用范式："分阶段检查 + 高优先级短路 + 黑板写多键 + 真值表路由"。需要按项目调整：
- 检查阶段数（≥2）
- 短路条件
- 路由真值表
