---
id: MOC-App
title: App 模块图谱
summary: 大版本检查与更新路由的模块边界速查
category: arch
status: active
date: 2026-06-05
aliases:
  - MOC-App
  - App模块图谱
  - 启动期图谱
tags: [moc, nova, app, launch, splash, procedure]
keywords: [App, AppComponent, AppManager, IAppManager, AppVersionResult, AppManagerConfig, ProcedureCheckVersion, ProcedureAppDownload]
related:
  - "[[ADR-013-hotfix-master-switch|ADR-013]]"
  - "[[ADR-015-merge-launch-into-splash|ADR-015]]"
  - "[[ADR-024-launch-to-app-rename|ADR-024]]"
  - "[[ADR-033-maindemo-isolated-topology|ADR-033]]"
  - "[[PAT-43-optional-remote-check-tolerance|PAT-43]]"
  - "[[PAT-44-procedure-checkversion-two-stage|PAT-44]]"
---

# MOC-App：App 模块图谱

## 一句话

App 模块只管“App 大版本检查与更新路由”，不管资源补丁，不管启动 FSM 本体，也不等于启动期 UI。

## 何时查这页

- 要改大版本检查、强更、推荐更新
- 要分清 `App`、`Asset`、`Procedure`、`LauncherUI`
- 要确认 `Launch -> App` 改名后的当前口径

## 当前结构

```text
Nova.App
  -> AppComponent
  -> IAppManager
  -> AppManagerBase
  -> AppManager
```

关键类型：

- `AppVersionResult`：`NoDownload / RecommendedDownload / ForcedDownload`
- `AppManagerConfig`
- `AppDownloadRule`
- `AppDownloadRoute`

## 关键边界与接口面

- `CheckAsync(CancellationToken)`：返回大版本检查结果
- `DownloadAsync(CancellationToken)`：执行 APK 下载，返回本地路径
- `OpenStoreAsync(CancellationToken)`：跳应用商店
- `MatchedRule / TargetStoreUrl / TargetDownloadUrl`
- App 只管大版本检查与更新路由
- 资源补丁、Manifest、热更开关属于 `Asset`
- 启动跳转和 `ProcedureAppDownload` 属于 `Procedure`
- `LauncherUI` 是启动期 UI，不是 App 模块本体
- `EnableHotfix` 在 `AssetComponent / AssetManagerConfig`，不在 App 模块
- `ProcedureCheckVersion` 始终先做 App 大版本判断，再按 `EnableHotfix` 决定是否做资源补丁判断
- `AppDownloadCheckUrl` 为空时，App 检查直接降级为 `NoDownload` 并继续启动链
- `ProcedureAppDownload` 是 Procedure，不是 App 模块内部类

- 当前统一使用 `App`，不再使用 `Launch` 作为模块名
- `LauncherUIController`、`LauncherDialogPanel` 这类 `Launcher*` 仍属于启动期 UI 命名，不等于旧 `Launch` 模块残留

## 导航提醒

- `RecommendedDownload` 已是框架内建弹窗决策点，但取消后仍回到原有热更 / 启动链。
- `EnableHotfix=false` 不会跳过 App 大版本检测；它只会跳过 Asset 热更检查。
- 具体实现完备度、下载链路状态与平台差异，应回到 `Docs` 与源码确认。
- 这页只回答边界与入口，不回答“当前业务是否已经接完”。

## 常见误区

- 把 `LauncherUI` 归到 App 模块
- 把 `EnableHotfix` 放回 App 语义里理解
- 用旧术语 `LaunchManager` / `Nova.Launch`
- 忽略 `RecommendedDownload` 取消后仍需回到 `ProcedureHotfix / ProcedureLoadDll` 的后续分流

## 先往哪看

- 改命名和术语：[[ADR-024-launch-to-app-rename]]
- 改启动阶段协作：[[PAT-44-procedure-checkversion-two-stage]]
- 改热更总开关边界：[[ADR-013-hotfix-master-switch]]

## 关联

- 图谱：[[MOC-Procedure]]、[[MOC-Manager]]
