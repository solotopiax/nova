---
id: ADR-065
title: 启动期清单三级离线回退仅接受本地可启动版本
summary: 远端失败优先复用启动范围完整的本地版本
category: hotfix
status: accepted
date: 2026-06-16
aliases:
  - ADR-065-asset-manifest-three-tier-offline-fallback
  - LastBootableVersion
  - 本地可启动版本
keywords:
  - ADR-065
  - LastBootableVersion
  - CommitBootableVersion
  - 本地可启动版本
  - 三级离线回退
  - 启动范围完整性
tags: [adr, nova, asset, hotfix, offline, yooasset]
supersedes: []
superseded-by: []
related:
  - "[[ADR-025-yooasset-url-template-placeholders|ADR-025]]"
  - "[[ADR-051-launch-asset-slice-strategy|ADR-051]]"
  - "[[ADR-052-asset-cache-two-layer-cleanup|ADR-052]]"
  - "[[ADR-076-startup-whitelist-metadata-routing|ADR-076]]"
  - "[[MOC-Asset]]"
---

# ADR-065：启动期清单三级离线回退仅接受本地可启动版本

## 背景（Context）

YooAsset 的 `RequestPackageVersionAsync` 在 HostPlayMode 下需要远端 `.version` 文件。网络不可达时，客户端无法取得版本号；直接回退随包内置版本虽然能启动，却会丢弃玩家已下载的有效增量缓存。

旧方案在远端 Manifest 激活后立即把版本号写入 `Persist/Asset/CachedVersion/{package}.version`。该标记只能证明版本元数据加载成功，不能证明当前启动所需的 Bundle 已完整缓存：

- 整包更新可能尚未下载完。
- `CreateDownloaderByTags` 只要求启动 Tag 范围，不等价于整包完整。
- 下载失败、取消或用户跳过后，Manifest 仍可能已经激活。
- 下次离线直接加载该版本时，可能找不到启动必须的 Bundle。

因此，离线回退版本必须从“最近激活版本”收紧为“最近确认可完成当前启动范围的版本”。

## 决策（Decision）

### 1. 使用 LastBootableVersion 语义，不再使用 CachedVersion

Nova 为每个 YooAsset 包维护一份本地可启动版本记录：

```text
persistentDataPath/Asset/{package}.version
```

路径根由 Unity `Application.persistentDataPath` 决定，使用 `System.IO` 直接读写，不依赖 `Nova.Persist` 生命周期。旧目录 `persistentDataPath/Persist/Asset/CachedVersion/` 不再读取，也不自动迁移；旧标记的证明强度不足，迁移会重新引入不完整版本漏洞。

### 2. 只有启动下载范围完整时才能提交版本

`LoadManifestAsync` 成功只激活 Manifest，不写版本记录。版本推进统一通过 `IAssetManager.CommitBootableVersion`：

- `LaunchHotfixTags` 为空：用整包 Downloader 检查全部 Bundle，下载数为 0 才允许提交。
- `LaunchHotfixTags` 非空：用相同 Tag 创建 Downloader，只检查启动范围，下载数为 0 才允许提交。
- `ProcedureCheckVersion` 确认无补丁时提交。
- `ProcedureHotfix` 确认 Downloader 为空或差异 Bundle 全部下载成功后提交。
- 下载失败、取消、用户跳过、Manifest 无效或离线恢复路径一律不提交。

记录使用整体覆盖写入，`version` 为空或写入失败时仅跳过或告警，不阻断启动。

### 3. 非 WebGL 三级回退链统一验证可启动性

该回退链依赖 Builtin/Sandbox 文件系统及本地文件 IO，仅适用于非 WebGL 平台。WebGL 不进入本地可启动版本或 Sandbox 回退，使用下节的首包元数据回退。

远端版本请求或 Manifest 加载失败后，`TryRecoverManifestAsync` 按以下顺序处理：

| 级别 | 行为 | 接受条件 |
|---|---|---|
| ① 当前已激活清单 | 保留当前包和 Manifest | `PackageValid` 且当前启动范围完整 |
| ② 本地可启动版本 | 读取 `{package}.version`，在当前 Host 包加载缓存 Manifest | Manifest 加载成功且按当前 `LaunchHotfixTags` 重建的启动范围下载数为 0 |
| ③ 随包内置清单 | 销毁 Host 包，改用 `OfflinePlayMode` 初始化 | 内置版本与 Manifest 可用 |

整体优先级为：远端最新清单 → 当前已激活可启动清单 → 本地可启动版本 → 随包内置版本 → 抛出原始远端错误。

本地记录只是候选索引，不是无条件信任凭证。即使文件存在，也必须重新加载缓存 Manifest 并验证当前启动范围；缓存被清理、Tag 配置变化或 Manifest 损坏都会使该级失败并继续回退。

### 4. WebGL 保持 Host 拓扑并临时回退首包元数据

WebGL Host 使用 `WebServer + WebNetwork` 文件系统。远端元数据候选全部失败后，Nova 不销毁 Host 包，也不切换到依赖本地文件 IO 的 Offline/Sandbox 链，而是临时把 `.version/.hash/.bytes` 路由到 `StreamingAssets/{YooFolder}/{Package}`：

- 首包元数据仍通过 YooAsset 与 UnityWebRequest 加载，不使用 `System.IO`。
- 临时路由只影响版本元数据；Bundle 始终保持常规远端主备地址。
- 首包内已有的 Bundle 由 WebServer 命中；未内置 Bundle 在网络恢复后仍可由 WebNetwork 按需加载。
- 无论回退成功或失败，元数据路由都在 `finally` 中恢复为远端候选。
- 回退成功的包加入本次启动离线恢复集合，跳过本轮远端热更；它不推进 `LastBootableVersion`。

因此 WebGL 的优先级是：远端最新清单 → 首包清单 → 抛出原始远端错误。WebNetwork 的 `IsDownloadRequired()` 固定返回 false，不能用它证明已有 Manifest 的启动范围完整，所以 WebGL 不复用非 WebGL 的“当前已激活清单”分支。首包按 Tag 构建时，`LaunchHotfixTags` 必须覆盖启动必须资源；该能力不等价于浏览器冷离线启动，HTML/WASM/StreamingAssets 仍需由站点或 Service Worker 提供。

### 5. 启动 Tag 是完整性边界，不是业务层硬编码补丁

Nova 不要求“全量 Bundle 都缓存”才记录版本，而是以框架已有 `LaunchHotfixTags` 定义启动可用范围：

- 整包项目的可启动版本代表全量 Bundle 完整。
- 切片项目的可启动版本代表启动 Tag 范围完整；非启动资源继续在运行时按需增量下载。

这样既避免切片项目永远无法晋升版本，也不额外引入“启动必须 Tag”之类偏业务的新概念。

### 6. 白名单路由不绕过可启动版本门

启动白名单命中只切换版本元数据候选地址，不直接写本地版本。无论 Manifest 来自常规还是白名单元数据根，都必须经过同一 Downloader 完整性检查与 `CommitBootableVersion` 门，详见 [[ADR-076-startup-whitelist-metadata-routing|ADR-076]]。

## 后果（Consequences）

### 正面

- 断网启动只复用已证明满足当前启动范围的版本，不会因“只激活 Manifest”误判可启动。
- 整包与 Tag 切片共用一套提交规则，框架不绑定具体业务 Tag。
- 不读取 YooAsset 内部沙盒结构，不侵入第三方源码。
- 本地缓存清理或 Tag 配置变化会在回退时再次校验并安全降级。
- 白名单灰度版本与常规版本遵守同一完整性门。
- WebGL 首包清单回退不改变 Bundle 远端地址，网络恢复后仍保留按需加载能力。

### 负面

- 每个包多一份 Nova 自管的版本字符串文件。
- 旧 CachedVersion 不迁移，升级后的第一次离线启动可能直接回退内置版本；需先完成一次正常在线启动才能生成新记录。
- `LaunchHotfixTags` 发生变化后，旧记录可能因新启动范围不完整而失效。
- 版本文件存在不代表一定回退成功，缓存 Manifest 与对应启动 Bundle 仍可能被系统或清理逻辑移除。
- WebGL 首包按 Tag 内置只能保证该 Tag 范围；若配置遗漏启动资源，Manifest 回退成功后仍可能加载失败。

## 被排除的方案（Alternatives）

| 方案 | 否决理由 |
|---|---|
| Manifest 激活成功立即记录版本 | 不能证明启动所需 Bundle 已下载完整，是旧 CachedVersion 漏洞根因 |
| 永远要求全量 Bundle 完整 | 切片项目可能长期无法晋升，违背启动 Tag + 运行时增量策略 |
| 新增业务“启动必须 Tag”配置 | `LaunchHotfixTags` 已准确表达启动范围，重复配置会漂移 |
| 允许 `CreateDownloaderByTags` 成功后不记录 | 下一次断网缺少最新可启动候选，会无谓回退首包 |
| 扫描 YooAsset 沙盒反查最新版本 | 强耦合第三方内部路径，且无法单凭文件存在证明当前启动范围完整 |
| 迁移旧 CachedVersion 文件 | 旧文件只证明 Manifest 曾激活，不能安全升级为可启动证明 |
| 改 YooAsset 源码实现回退 | 增加第三方升级成本，破坏 Asset 模块封装边界 |

## 验证依据（Verification）

- Runtime：`AssetManager.CommitBootableVersion`、`IsLaunchScopeReady`、`TryFallbackToLocalBootableManifestAsync`、`TryFallbackToWebGLBuiltinManifestAsync`、`TryRecoverManifestAsync`。
- Procedure：`ProcedureCheckVersion` 在无补丁时提交；`ProcedureHotfix` 在无差异或下载成功后提交。
- 存储 helper：`GetLocalBootableVersionFilePath`、`SaveLocalBootableVersion`、`TryLoadLocalBootableVersion`。
- 契约测试：`AssetLocalBootableVersionTests`、`AssetManagerManifestFallbackRegressionTests`、`AssetStartupWhitelistTests`、`YooAsset305UpgradeContractTests`。
- 当前实现与文档提交：`e03e08d8e`。

## 关联

- 启动整包与 Tag 切片：[[ADR-051-launch-asset-slice-strategy|ADR-051]]
- 磁盘缓存清理：[[ADR-052-asset-cache-two-layer-cleanup|ADR-052]]
- URL 模板与远端寻址：[[ADR-025-yooasset-url-template-placeholders|ADR-025]]
- 启动设备白名单：[[ADR-076-startup-whitelist-metadata-routing|ADR-076]]
- 模块入口：[[MOC-Asset]]

---
