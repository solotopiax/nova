---
id: ADR-080
title: 资源下载按文件独立执行完整主备轮次与重试
summary: 每个 Asset 文件独立冻结候选计划并按 C×R×(K+1) 执行
category: hotfix
status: accepted
date: 2026-08-14
aliases:
  - ADR-080-asset-package-sticky-fallback-routing
keywords:
  - 资源下载主备切换
  - AssetDownloadUrlPolicy
  - per-file fallback
  - 完整轮次
  - RetryDownloadCount
  - 最近成功域名
tags: [adr, nova, asset, hotfix, yooasset, cdn]
supersedes: []
superseded-by: []
related:
  - "[[ADR-076-startup-whitelist-metadata-routing|ADR-076]]"
  - "[[ADR-083-uwr-primary-fallback-network|ADR-083]]"
  - "[[PAT-37-no-yooasset-outside-asset-module|PAT-37]]"
  - "[[MOC-Asset]]"
---

# ADR-080：资源下载按文件独立执行完整主备轮次与重试

## 背景（Context）

旧方案让同一 YooAsset Package 内的并发 Bundle 共享粘滞游标：某个文件切到备用地址后，后续新文件只能从备用地址开始；备用失败也不会回到主地址。这能抑制并发失败造成的游标抖动，但会让文件失去完整尝试主备域名的机会，也无法准确表达“全部候选走完算一轮、全部轮次走完才消耗一次重试”的产品语义。

App、Asset 与业务协议现在需要共享同一种候选规划机制，同时仍保留各模块自己的地址来源、错误分类和配置。

## 决策（Decision）

### 1. 每个文件独立冻结执行计划

- `AssetManager` 仍为每个 YooAsset Package 持有一个 `AssetDownloadUrlPolicy`，但每个文件拥有独立的 `HttpFallbackExecutionPlan` 与 `HttpFallbackExecutionCursor`。
- A/B/C 等并发 Bundle 互不推进游标；某个文件失败不会替另一个文件消耗候选、轮次或重试。
- 新文件会读取当前最近成功域名并调整单轮起点，但它的计划仍包含全部候选，不会失去尝试另一域名的机会。

### 2. 轮次与重试语义统一

去重后的候选数为 `C`，每个重试周期内的完整轮数为 `R`，下载重试次数为 `K`：

```text
最大物理尝试数 = C × R × (K + 1)
```

- 主、备候选全部走一遍才算一轮。
- 所有轮次全部失败后才消耗一次 `RetryDownloadCount`。
- `RetryDownloadCount=3` 表示首次完整执行后，最多再执行 3 次相同的完整轮次组合。
- 后续轮次和重试允许回到计划首候选，确保主备在每个完整执行周期中都有机会。

### 3. Asset 自己裁决失败是否继续

- 无 HTTP 响应、内容校验失败、HTTP `404`、`408`、`416`、`429` 与 `5xx`：继续计划中的下一候选。
- HTTP `401`、`403` 及其他 `4xx`：立即终止该文件请求链。
- 调用方取消：中止当前请求并停止整条候选链。
- 每次物理请求独立使用自己的超时；不存在整条链路总超时。

### 4. 最近成功域名按 Package 和请求类型隔离

- Bundle 与版本元数据分别维护最近成功域名，避免不同地址族相互污染。
- 启动白名单文件按 Package 单独维护偏好，并使用白名单专属的轮数、重试、最近成功、埋点与超时配置。
- 完整失败不清除已有偏好；配置候选不再包含旧域名或 Manager 关闭时才失效。

### 5. 保持 YooAsset 边界

- 不修改 YooAsset 源码，不关闭其按需下载。
- Nova 同时实现 YooAsset 的 `IDownloadUrlPolicy` 与 `IDownloadRetryPolicy`，把逻辑轮次和重试数换算为 YooAsset 所需的物理重试数。
- 显式 `ResourceDownloaderOperation` 通过 `AssetDownloader` 登记当前下载操作和逻辑重试配置；普通异步按需加载使用 Asset Inspector 的默认配置。
- 同步加载行为保持不变；未缓存资源能否同步取得继续遵循 YooAsset 原有机制。

### 6. 超时与埋点

- 启动白名单使用独立的 `StartupWhitelistCheckTimeout`；`.version` 使用普通 Asset `CheckTimeout`。
- `.hash/.bytes` Manifest 使用独立的 `ManifestRequestTimeout`，默认 60 秒。
- 非 WebGL Bundle 使用 `IdleTimeout`（Inspector 中文名称为“单文件字节流入超时”）；WebGL WebNetwork 无可靠字节流入看门狗，改用独立的 `WebGLBundleRequestTimeout` 单次物理请求总超时，默认 300 秒。Inspector 按当前 BuildTarget 互斥启用两项配置。
- 开启 `EnableUWRTracks` 后，每个文件按 `1 uwr_request_start → 0～N uwr_request_error → 1 uwr_request_end` 上报；显式下载器内多个文件通过 `uwr_download_operation_id` 聚合。
- 非 WebGL HostPlayMode 的缓存下载可在文件校验完成后闭环；WebGL HostPlayMode 的 WebNetwork 内存 Bundle 可能在内容校验前收到成功回调，校验重试会在同一 download operation 下产生新的 UWR chain。

## 后果（Consequences）

### 正面

- 每个文件都能按配置完整尝试主备域名，不再受其他并发下载的失败顺序影响。
- App、Asset、业务协议共享同一套候选、轮次、重试和最近成功算法，配置与失败规则仍各自独立。
- YooAsset 显式下载和普通异步按需下载均受 Nova 策略约束，无需维护第三方 fork。

### 代价与限制

- 已知主域名故障时，不同文件仍可能各自命中该域名；最近成功优先用于降低重复失败，但不会删除另一候选。
- `C × R × (K + 1)` 会线性放大最坏请求耗时，配置时必须结合每次物理请求超时评估。
- WebGL HostPlayMode 的 WebNetwork 路径无法可靠把底层 HTTP 成功与最终内容校验合并成唯一 UWR chain，只能使用 download operation 关联。

## 被排除的方案（Alternatives）

| 方案 | 否决理由 |
|---|---|
| Package 共享粘滞游标并禁止回绕 | 新文件会失去完整尝试主备的机会，并发文件会互相影响请求计划 |
| 每个失败都推进共享取模游标 | 并发失败顺序会改变其他文件的候选，行为不可预测 |
| 仅重试最后失败域名 | 不符合“每次重试重新执行全部轮次组合”的定义 |
| 修改 YooAsset 下载器源码 | 扩大第三方 fork 影响面；现有 URL 与 Retry Policy 已能承载 Nova 规则 |
| 关闭 YooAsset 按需下载 | 会改变普通异步加载的既有使用方式，无必要 |

## 验证依据（Verification）

- 实现：`HttpFallbackPlanner`、`HttpFallbackExecutionPlan`、`HttpFallbackExecutionCursor`、`HttpFallbackPreferenceStore`。
- Asset 适配：`AssetDownloadUrlPolicy.SelectUrl`、`OnRequestFailed`、`IsRetryableError`、`CompleteMetadataRequest`。
- 显式下载：`AssetManager.CreateDownloader*` 与 `AssetDownloader.RegisterDownloaderFile`。
- 真实 UWR loopback 覆盖断连重放、HTTP 503 止链和取消止链；相关 7 个 EditMode 测试类最终通过 `121/121`。
- Unity 6000.4.2f1、Android active target 下刷新编译完成，Console 编译错误为 0。

## 关联

- 白名单元数据边界：[[ADR-076-startup-whitelist-metadata-routing|ADR-076]]
- 统一 UWR 主备机制：[[ADR-083-uwr-primary-fallback-network|ADR-083]]
- YooAsset 模块边界：[[PAT-37-no-yooasset-outside-asset-module|PAT-37]]
- Asset 模块入口：[[MOC-Asset]]
