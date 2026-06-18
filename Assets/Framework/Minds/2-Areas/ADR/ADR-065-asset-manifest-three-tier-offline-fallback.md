---
id: ADR-065
title: 启动期清单加载三级离线回退，远端不可达优先复用玩家本地缓存版本
summary: 远端不可达先复用本地缓存版本，降级内置兜底
category: hotfix
status: accepted
date: 2026-06-16
aliases:
  - ADR-065-asset-manifest-three-tier-offline-fallback
keywords:
  - ADR-065
  - 启动期清单加载三级离线回退，远端不可达优先复用玩家本地缓存版本
  - ADR-065-asset-manifest-three-tier-offline-fallback
tags: [adr, nova, asset, hotfix]
supersedes: []
superseded-by: []
related:
  - "[[ADR-051-launch-asset-slice-strategy|ADR-051]]"
  - "[[ADR-052-asset-cache-two-layer-cleanup|ADR-052]]"
  - "[[ADR-025-yooasset-url-template-placeholders|ADR-025]]"
  - "[[MOC-Asset]]"
---

# ADR-065：启动期清单加载三级离线回退，远端不可达优先复用玩家本地缓存版本

## 背景（Context）

YooAsset 原生在启动期 `RequestPackageVersionAsync`（HostPlayMode/SandboxFileSystem → `RequestRemotePackageVersionOperation`）是**纯远程 HTTP 拉 `.version` 文件，失败即 `SetError`，无任何本地回退**。即云端不可达（弱网 / DNS 异常 / 服务器宕机）时拿不到版本号，启动流程卡死或抛错。

旧的兜底 `TryFallbackToBuiltinManifestAsync` 在远端失败时销毁包 → 切 `OfflinePlayMode` → 回退到**随包内置（首包）版本**，代价是**丢弃玩家已下载的全部增量缓存**，体验差：一个已热更到很新版本的玩家，断网启动会被打回首包资源。

关键事实：YooAsset 清单加载 `LoadPackageManifestAsync(version)` 的 `DownloadPackageHashOperation` / `DownloadPackageManifestOperation` 第一步都是 `CheckExists`——**本地沙盒已缓存该版本则直接成功、零网络**。所以离线复用玩家最新缓存清单只缺“版本号”这一个字符串。但 YooAsset 不提供“从本地缓存反查最新版本号”的 API（`GetPackageVersion()` 依赖已加载的 `ActiveManifest`，启动探测期不可用）。

## 决策（Decision）

### 1. Nova 自记录版本号，零侵入补齐缺口

每次远端正常加载清单成功后，用 `pkg.GetPackageVersion()` 取当前激活版本号写入 `persistentDataPath/Persist/Asset/CachedVersion/{package}.version`（复用 `Path.Persistent`，与 `Persist/FileFragment`、`Persist/SQLite` 同体系）。写失败仅告警不中断启动。三个 helper 为 `private static` 纯函数：`GetLocalCachedVersionFilePath` / `SaveLocalCachedVersion` / `TryLoadLocalCachedVersion`。

### 2. 三级回退链（`TryRecoverManifestAsync` 统一编排）

`LoadManifestAsync` 的版本请求与清单下载**两处失败**都走同一编排，逐级尝试：

| 级 | 方法 | 行为 |
|---|---|---|
| ① 沿用当前清单 | `PackageValid` 分支 | 包已加载过清单（如 `RefreshManifestAsync` 弱网）直接复用 |
| ② 本地缓存版本 | `TryFallbackToLocalCachedManifestAsync` | 读本地记录版本号 → 当前 Host 包上 `LoadPackageManifestAsync(localVersion)` 离线命中沙盒。**不销毁包、不切模式，保留增量** |
| ③ 内置首包 | `TryFallbackToBuiltinManifestAsync` | 销毁包 → `OfflinePlayMode` → 内置清单（丢弃增量，最终兜底） |

整体优先级：远端最新清单 → 已激活清单 → 本地缓存版本清单 → 内置清单 → 抛出原始远端错误。

### 3. 门控随 HostPlayMode 默认开启，不加开关

本地缓存版本回退复用现有 `CanFallbackToBuiltinManifest()`（仅 HostPlayMode 生效），**不新增 Config 开关、不动 `AssetManagerConfig` / `AssetComponent` Inspector / 五段透传链**。理由：纯增益、命中才用、不命中自动降级，无副作用，加开关只是徒增配置复杂度。

## 后果（Consequences）

### 正面
- 断网启动优先复用玩家最新缓存版本，保留全部增量，不再被打回首包
- 零侵入 YooAsset 源码，全部公开 API；版本记录文件 Nova 自管，不耦合 YooAsset 沙盒路径规则
- 编排集中在 `TryRecoverManifestAsync`，版本/清单两处失败复用同一恢复路径

### 负面
- 多一个 Nova 自管文件需随缓存生命周期理解（首次安装即断网无记录 → 该级失效降级内置，符合预期）
- 记录的是“上次成功启动版本”，非“沙盒中物理存在的最新版本”；若该版本缓存被外部清理，② 级 `LoadPackageManifestAsync` 会失败并自动降级 ③，不会误判

## 被排除的方案（Alternatives）

| 方案 | 否决理由 |
|---|---|
| 扫描 YooAsset 沙盒目录反解最新版本号 | `GetDefaultCacheRoot()` / `GetCacheManifestFilesRoot()` 均 internal；不改源码就要复刻 YooAsset 路径推导规则，强耦合、包升级易裂；版本号比较 + 半下载/损坏文件判定不鲁棒 |
| 改 YooAsset 源码让 `RequestPackageVersion` 失败回退本地 | 侵入第三方源码，升级成本高，违背封装原则 |
| 新增 Config 开关控制本回退 | 纯增益功能无需开关，徒增 Inspector + 五段透传链改动面 |

## 验证依据（Verification）
- 源码：`AssetManager.cs`（`TryRecoverManifestAsync` / `TryFallbackToLocalCachedManifestAsync` / 重构后的 `TryFallbackToBuiltinManifestAsync` / `LoadManifestAsync` 写记录）、`AssetManager.Methods.cs`（三个 static helper）
- grep 关键词：`TryFallbackToLocalCachedManifestAsync` / `SaveLocalCachedVersion` / `CachedVersion`
- 单测：`AssetLocalCachedVersionTests`（Save→Load 往返 / 缺失 / 空内容，反射静态方法，3/3 通过）
- 真机三场景：联网成功写版本文件；有缓存后断网重启走 ② 级保留增量；首次安装即断网走 ③ 级内置兜底
- 审查要点：本地回退不得销毁包/切模式；记录只在远端正常成功路径写；门控不引入新开关

## 关联
- 规范落点：热更链路 / 启动容错
- 相关 ADR：[[ADR-051-launch-asset-slice-strategy|ADR-051]]（版本检查比对本地缓存物理现状，本 ADR 补齐“远端不可达时的版本号来源”）、[[ADR-052-asset-cache-two-layer-cleanup|ADR-052]]（磁盘缓存清理分工，影响 ② 级缓存是否存在）、[[ADR-025-yooasset-url-template-placeholders|ADR-025]]（CDN 寻址）
- MOC：[[MOC-Asset]]

---
