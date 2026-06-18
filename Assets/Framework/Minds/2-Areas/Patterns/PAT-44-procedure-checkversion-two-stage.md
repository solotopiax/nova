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

## 适用场景（When）

启动流程中既有"App 大版本检查"又有"资源补丁检查"，二者**正交但有优先级**：

- App 强更生效时不应再做资源差异检查（用户都得跳商店了，资源差异无意义）
- App 推荐更新或无更新时，是否执行资源差异检查由 `AssetComponent.EnableHotfix` 决定
- 两条结论需要联合决定下一个 Procedure 跳转目标

## 核心做法（What & How）

### 编排骨架

```csharp
private async UniTaskVoid RunCheckAsync(ProcedureOwner procedureOwner, CancellationToken ct)
{
    var appManager = FrameworkManagersGroup.GetManager<IAppManager>();

    // 阶段 1：App 大版本检查（异常宽容，详见 [[PAT-43-optional-remote-check-tolerance|PAT-43]]）
    AppVersionResult appResult = await appManager.CheckAsync(ct);

    // 短路：强更直接跳下载，不查资源差异
    if (appResult == AppVersionResult.ForcedDownload)
    {
        SetData(ProcedureDataKeys.AppVersionResult, appResult);
        SetData(ProcedureDataKeys.HasAssetPatch, false);
        m_NextState = typeof(ProcedureAppDownload);
        m_CheckComplete = true;
        return;
    }

    // 阶段 2：EnableHotfix 只控制 Asset 资源清单加载 + 差异检查
    var assetComponent = FrameworkComponentsGroup.GetComponent<AssetComponent>();
    if (!assetComponent.EnableHotfix)
    {
        SetData(ProcedureDataKeys.AppVersionResult, appResult);
        SetData(ProcedureDataKeys.HasAssetPatch, false);
        m_NextState = appResult == AppVersionResult.RecommendedDownload
            ? typeof(ProcedureAppDownload)
            : typeof(ProcedureLoadDll);
        m_CheckComplete = true;
        return;
    }

    var assetManager = FrameworkManagersGroup.GetManager<IAssetManager>();
    await assetManager.LoadManifestAsync(null, ct);
    bool hasPatch = await assetManager.HasPatchAsync(null, ct);

    SetData(ProcedureDataKeys.AppVersionResult, appResult);
    SetData(ProcedureDataKeys.HasAssetPatch, hasPatch);

    m_NextState = appResult == AppVersionResult.RecommendedDownload
        ? typeof(ProcedureAppDownload)
        : hasPatch ? typeof(ProcedureHotfix) : typeof(ProcedureLoadDll);
    m_CheckComplete = true;
}
```

### 路由真值表（5 种情况）

| AppVersionResult | HasAssetPatch | 下一 Procedure |
|---|---|---|
| ForcedDownload | (任意) | ProcedureAppDownload |
| RecommendedDownload | true | ProcedureAppDownload |
| RecommendedDownload | false | ProcedureAppDownload |
| NoDownload | true | ProcedureHotfix |
| NoDownload | false | ProcedureLoadDll |

> `RecommendedDownload` 现在也是框架内建弹窗分支。若 `EnableHotfix=true`，用户取消后按 `HasAssetPatch` 继续既有启动链；若 `EnableHotfix=false`，则直接续到 `ProcedureLoadDll`。

### 黑板写两键，不写枚举

旧实现把"App 大版本"和"资源补丁"两类语义压扁进单枚举（`LaunchCheckResult.HotfixRequired`），下游 Procedure 必须二次拆包。**新做法用两键互不干扰**：

```csharp
// ProcedureDataKeys.cs
public static readonly string AppVersionResult = "ProcedureDataKeys.AppVersionResult";
public static readonly string HasAssetPatch = "ProcedureDataKeys.HasAssetPatch";
```

下游想用哪个用哪个。例如 ProcedureAppDownload 需要 `AppVersionResult` 决定弹窗类型，ProcedureHotfix 不需要 `AppVersionResult`。

### 接口契约要求

- `IAppManager.CheckAsync` 必须**异常宽容**（参 [[PAT-43-optional-remote-check-tolerance|PAT-43]]）
- `IAssetManager.LoadManifestAsync` 必须**幂等**（HashSet 守护已加载包）
  - 否则下一个 Procedure（ProcedureLoadDll）再次调用会触发二次网络请求
- `IAssetManager.HasPatchAsync` 通过 `CreateResourceDownloader().TotalDownloadCount > 0` 判定差异

## 为什么这么做（Why）

1. **职责分离**：App 大版本（更换二进制） vs Asset 资源（替换 AB）是两类完全不同的更新；编排层用枚举混合二者必然回头要拆包。
2. **强更短路效率**：用户已确定跳商店时，资源差异检查浪费一次网络请求 + 清单加载时间。
3. **两阶段保持独立**：`EnableHotfix` 只控制 Asset 检查阶段，不反向决定 App 大版本检测是否执行。
4. **两键黑板**：避免下游 Procedure 必须知道枚举内部布局（耦合）；每个 Procedure 只关心自己需要的键。
5. **接口幂等是编排可移动的前提**：LoadManifestAsync 从原本只在 LoadDll 调用，前移到 CheckVersion 后，仍然能保证 LoadDll 不二次拉清单——这是把 LoadManifestAsync 设计为幂等的根本原因。

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
