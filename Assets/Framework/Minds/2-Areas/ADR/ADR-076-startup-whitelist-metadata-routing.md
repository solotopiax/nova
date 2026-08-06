---
id: ADR-076
title: 启动设备白名单仅切换版本元数据路由，失败不阻断常规热更新
summary: 白名单命中仅切换元数据地址，Bundle 仍走常规 CDN
category: hotfix
status: accepted
date: 2026-08-06
aliases:
  - ADR-076-startup-whitelist-metadata-routing
keywords:
  - ADR-076
  - 启动设备白名单
  - VersionsCheckWhiteList.json
  - asset-check-device-id.dat
  - 白名单版本元数据路由
  - ADR-076-startup-whitelist-metadata-routing
tags: [adr, nova, asset, hotfix, whitelist, cdn]
supersedes: []
superseded-by: []
related:
  - "[[ADR-013-hotfix-master-switch|ADR-013]]"
  - "[[ADR-025-yooasset-url-template-placeholders|ADR-025]]"
  - "[[ADR-051-launch-asset-slice-strategy|ADR-051]]"
  - "[[ADR-065-asset-manifest-three-tier-offline-fallback|ADR-065]]"
  - "[[PAT-43-optional-remote-check-tolerance|PAT-43]]"
  - "[[PAT-137-startup-bootstrap-no-hotfix-resource-backref|PAT-137]]"
  - "[[MOC-Asset]]"
---

# ADR-076：启动设备白名单仅切换版本元数据路由，失败不阻断常规热更新

## 背景（Context）

Nova 需要让少量测试设备在正式用户之前验证一套候选 YooAsset 版本文件，同时满足以下边界：

- 白名单判断发生在包初始化和版本请求之前，不能依赖 `ProcedurePreload` 才加载完成的 `Nova.Persist`。
- 灰度验证只应改变候选 Manifest，不应为同一批 Bundle 再维护一套下载域名和缓存体系。
- 白名单服务属于启动期可选远端检查；关闭、未配置、弱网或响应损坏都不能阻断常规热更新。
- Debug 与 Release 环境必须分别配置主备地址，并继续遵守既有 URL 模板和 DoH 规则。

## 决策（Decision）

### 1. 运行门控与环境选择

启动白名单仅在以下条件全部满足时执行：

1. `EnableHotfix=true`。
2. `EnableStartupWhitelist=true`。
3. 当前有效模式为 `HostPlayMode` 或 `WebPlayMode`。
4. 当前 `DevelopMode` 对应的白名单配置文件和版本元数据根地址至少各有一个有效 URL。
5. 本地已有稳定 DeviceID 缓存。

`DevelopMode.Debug` 选择 Debug 主备配置，`DevelopMode.Release` 选择 Release 主备配置。所有地址支持既有 `{Platform}`、`{Channel}`、`{Package}`、`{Version}` 占位符，并进入 Asset 模块现有 DoH detect-only 与原域名回退链路。

### 2. DeviceID 在 SDK 初始化后直写启动专用文件

稳定设备标识统一来自 `IDeviceIdProvider.GetDeviceID()`。SDK 插件正常初始化后，通过 `IAssetManager.SaveAssetCheckDeviceId` 将非空值以 UTF-8 明文写入：

```text
persistentDataPath/Asset/asset-check-device-id.dat
```

该文件使用 `System.IO` 独立读写，不依赖尚未执行 `Nova.Persist.LoadAsync()` 的持久化模块。首次正常启动没有缓存时自动跳过白名单；从下一次启动开始，缓存值才能参与当次启动判断。文件缺失、为空或读写失败均按无缓存降级。

### 3. 白名单文件是 DeviceID JSON 字符串数组

云端文件名固定为 `VersionsCheckWhiteList.json`，根结构为 JSON 字符串数组。客户端按主地址、备用地址顺序下载并做精确字符串匹配；空响应、请求失败、超时、非法 JSON、空数组或未命中都继续使用常规资源路由。

真实的外部取消仍向上传播，避免把用户或生命周期取消误判为普通弱网。

### 4. 命中仅切换版本元数据，Bundle 地址保持不变

命中后，白名单版本元数据根地址只参与以下三个 YooAsset 文件的候选请求：

- `{package}.version`
- `{package}.hash`
- `{package}.bytes`

请求顺序为白名单主备候选优先、常规主备候选随后。传输失败以及 HTTP 成功但内容非法或损坏都会推进到下一候选；全部失败后进入既有三级离线回退。

Bundle 下载始终使用常规 Host/Web 主备地址，不因白名单命中切换。这样候选 Manifest 可以引用常规 CDN 中已部署的 Bundle，并继续复用 YooAsset 的 bundle hash 缓存。

### 5. 白名单命中不等于版本已经可启动

白名单只决定本次请求哪套版本元数据，不推进本地可启动版本记录。只有当前 Manifest 对应的启动下载范围已经完整可用时，`CommitBootableVersion` 才能写入 `persistentDataPath/Asset/{package}.version`：

- `LaunchHotfixTags` 为空时检查整包范围。
- `LaunchHotfixTags` 非空时只检查启动 Tag 范围。
- 下载失败、取消、用户跳过或离线恢复都不得推进记录。

因此，灰度路由、资源下载完整性和离线可启动回退保持三层独立职责。

### 6. 编辑器与 Pipify 使用同一部署产物契约

Config 的“白名单部署”和 Pipify Step `cdn.whitelist.deploy` 使用同一上传计划，最多部署四个文件：

1. `VersionsCheckWhiteList.json`
2. 版本文件 `.version`
3. Manifest 哈希 `.hash`
4. Manifest 数据 `.bytes`

设备 ID 在生成 JSON 前去除空项、Trim 并按首次出现顺序去重。配置文件上传到独立的配置文件云端文件位置（包含 `.json` 文件名），三个版本文件上传到版本文件云端目录；配置文件位置为空或非法时跳过 JSON，不回退到版本文件目录，也不阻断三个版本文件的部署。Pipify 参数只覆盖当次执行快照，不回写 `ConfigMasterSO`。

## 后果（Consequences）

### 正面

- 测试设备可在正式切换版本入口前验证候选 Manifest 和启动资源范围。
- 白名单服务故障不会扩大为启动故障，未命中用户完全沿用原热更新链路。
- Bundle 仍走常规 CDN，避免重复部署、重复缓存和双套域名治理。
- DeviceID 启动可用性不依赖 Persist 生命周期，首启与后续启动语义明确。
- Inspector、Config 部署和 Pipify Batch 共享同一文件与路由契约。

### 负面

- 测试设备必须先正常启动一次，白名单无法覆盖首次安装的第一次启动。
- DeviceID 以明文保存在应用持久化目录，只适合作为灰度路由标识，不应当作鉴权凭证或秘密。
- 运维必须保证候选 Manifest 引用的 Bundle 已存在于常规 CDN，否则命中设备仍会在资源下载阶段失败。
- 白名单主备、常规主备和三级离线回退形成多级候选链，日志必须保留候选推进原因以便排障。
- 启动白名单诊断使用 `Log.Debug` 输出门控状态、配置文件主备拉取结果、命中结果，以及 `.version` / `.hash` / `.bytes` 的实际请求结果；命中时明确打印完整 DeviceID，Bundle 不进入这组元数据日志。

## 被排除的方案（Alternatives）

| 方案 | 否决理由 |
|---|---|
| 启动时直接读取 `Nova.Persist` | `Nova.Persist.LoadAsync()` 在 `ProcedurePreload` 执行，晚于版本检查与包初始化，形成生命周期倒置 |
| 第一次启动临时生成随机 ID | 云端无法在当次启动前预知该值，不能满足“当次命中” |
| 白名单命中后同时切换 Bundle 主机 | 引入双套 Bundle 部署、缓存与域名治理；候选 Manifest 本身已足以表达版本差异 |
| 白名单请求失败时阻断启动 | 灰度检查是可选优化，不是启动必需依赖；会把配置或网络故障放大为全量事故 |
| Manifest 激活后立即记录可启动版本 | 激活只证明元数据可用，不证明整包或启动 Tag 范围的 Bundle 已完整缓存 |
| 扫描 YooAsset 沙盒反查最新版本 | 依赖第三方内部目录规则，且无法可靠证明当前启动范围完整可用 |

## 验证依据（Verification）

- Runtime：`AssetManager.CheckStartupWhitelistAsync`、`CanCheckStartupWhitelist`、`SaveAssetCheckDeviceId`、`CommitBootableVersion`、`AssetDownloadUrlPolicy`。
- SDK：`SDKManager` 在插件初始化完成后读取 `IDeviceIdProvider` 并写入启动缓存。
- Editor：`AssetComponentInspector` 的“启用白名单”配置组、`ConfigWindow` 的“白名单部署”、`EditorUtil.CDN` 的独立上传计划。
- Pipify：`cdn.whitelist.deploy` Step 及其 DeviceID、三个版本文件、配置文件位置与版本文件目录参数。
- 契约测试：`AssetStartupWhitelistTests`、`AssetLocalBootableVersionTests`、`AssetManagerManifestFallbackRegressionTests`、`CdnDeploymentPlannerTests`、`CdnPipifyStepTests`。
- 当前实现提交：`e03e08d8e`。

## 关联

- 热更新总开关：[[ADR-013-hotfix-master-switch|ADR-013]]
- URL 模板：[[ADR-025-yooasset-url-template-placeholders|ADR-025]]
- 启动整包与 Tag 切片：[[ADR-051-launch-asset-slice-strategy|ADR-051]]
- 离线清单回退：[[ADR-065-asset-manifest-three-tier-offline-fallback|ADR-065]]
- 可选远端检查容错：[[PAT-43-optional-remote-check-tolerance|PAT-43]]
- 启动前依赖边界：[[PAT-137-startup-bootstrap-no-hotfix-resource-backref|PAT-137]]
- 模块入口：[[MOC-Asset]]

---
