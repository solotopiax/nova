---
id: ADR-024
title: Launch 模块整体重命名为 App 并以 AppVersionResult 三档替代 LaunchCheckResult
status: accepted
date: 2026-05-19
summary: Launch模块改名App并以三档枚举替代
category: module
aliases:
  - ADR-024-launch-to-app-rename
keywords: [ADR-024, ADR-024-launch-to-app-rename]
tags: [adr, nova, app, procedure]
supersedes: []
superseded-by: []
related:
  - "[[ADR-015-merge-launch-into-splash|ADR-015]]"
---

# ADR-024：Launch 模块整体重命名为 App 并以 AppVersionResult 三档替代 LaunchCheckResult

## 背景（Context）

原 `Launch` 模块实际只做应用大版本检查、强更弹窗、商店跳转和 APK 下载，和启动期 UI 概念重叠；旧枚举还把大版本与资源补丁混在一起，未配置 URL 时会短路绕过资源差异检查。

## 决策（Decision）

1. **模块整体重命名 Launch → App**（Runtime + Editor + 文档）
   - 类、LogTag、全局入口、Inspector、目录全部同步改名。

2. **`Launcher*` 保持原名不改**
   - `LauncherUIController` / `LauncherDialogType` 等是 Procedure 模块内的启动期 UI 概念，与"App 大版本"模块语义不同，不卷入本次重命名。

3. **删除 `LaunchCheckResult`，引入 `AppVersionResult`（三档）**
   ```csharp
   public enum AppVersionResult { NoDownload, RecommendedDownload, ForcedDownload }
   ```
   - `RecommendedDownload` 含义：服务端有新版本，框架会弹出可跳过的大版本更新提示
   - 资源差异检查不再混进枚举，由 `ProcedureCheckVersion` 用 `(AppVersionResult, bool hasPatch)` 元组写黑板路由（详见 [[PAT-44-procedure-checkversion-two-stage|PAT-44]]）

4. **接口签名同步**
   - `IAppManager.CheckAsync` 返回 `UniTask<AppVersionResult>`。
   - `IAppManager.DownloadAppAsync` → `DownloadAsync`。
   - `IAssetManager.PrepareAsync` → `LoadManifestAsync`，并新增 `HasPatchAsync`。

## 后果（Consequences）

### 正面
- 模块语义边界清晰。
- 三档枚举正交，路由不再歧义。
- 未配置 `AppDownloadCheckUrl` 不再短路资源检查。

### 负面
- 场景里旧脚本引用改名时仍需检查。
- 文档里的 Launch 字样要持续清理。

## 被排除的方案（Alternatives）

| 方案 | 否决理由 |
|---|---|
| 保留 Launch 名，仅改 LaunchCheckResult 枚举 | 模块名与"启动 UI"概念重叠未解决，新人仍会被误导 |
| 把 Launcher\* 一并改名为 AppLauncher\* | Launcher 是启动期 UI，与 App 大版本模块不同语义，强行同步只会污染 Procedure 模块 |
| `RecommendedDownload` 也走 ProcedureAppDownload（弹推荐弹窗） | 当前框架已明确把推荐更新纳入统一启动弹窗链路，复用既有下载提示流程，不新增独立 Procedure |

## 验证依据（Verification）

- 代码：`Assets/Framework/Scripts/Runtime/Modules/App/`（整目录）
- 接口：`Assets/Framework/Scripts/Runtime/Modules/App/Managers/AppManager/Interfaces/IAppManager.cs`
- 编排：`Assets/Framework/Scripts/Runtime/Modules/Procedure/Procedures/ProcedureCheckVersion.cs`
- grep 残留检查：`LaunchComponent | LaunchManager | LaunchCheckResult | DownloadAppAsync | PrepareAsync | m_PreparedPackages` 全工程零命中（doc-writer 报告）
- qa-reviewer 三路径运行时 [PASS]：URL 留空降级、LoadManifestAsync 调用成功、清单幂等
