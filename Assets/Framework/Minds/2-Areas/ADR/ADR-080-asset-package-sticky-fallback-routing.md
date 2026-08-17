---
id: ADR-080
title: 资源下载按包粘滞切换备用地址且禁止候选回绕
summary: 主地址失败后同包后续下载粘在备用，并发失败只切一次
category: hotfix
status: accepted
date: 2026-08-14
aliases:
  - ADR-080-asset-package-sticky-fallback-routing
keywords:
  - 资源下载主备切换
  - AssetDownloadUrlPolicy
  - package sticky fallback
  - 并发下载失败
tags: [adr, nova, asset, hotfix, yooasset, cdn]
supersedes: []
superseded-by: []
related:
  - "[[ADR-076-startup-whitelist-metadata-routing|ADR-076]]"
  - "[[PAT-37-no-yooasset-outside-asset-module|PAT-37]]"
  - "[[MOC-Asset]]"
---

# ADR-080：资源下载按包粘滞切换备用地址且禁止候选回绕

## 背景（Context）

YooAsset 可同时下载同一资源包内的多个 Bundle。旧策略用一个共享整数游标取模选择候选地址，每个失败都会让游标加一，因此两个仍在飞的主地址请求同时失败时，可能连续推进两次并重新绕回主地址。这样既重复访问已知异常的地址，也让后续资源的实际下载域名不可预测。

## 决策（Decision）

1. `AssetManager` 继续为每个 YooAsset package 独立持有一份 `AssetDownloadUrlPolicy`。
2. 主地址失败后，同一 package 后续尚未开始的资源选择备用地址；已经在飞的请求不改写 URL。
3. 每次选址记录其候选位置。同一候选位置上的并发失败只允许第一次推进，晚到的相同位置失败不得再次跳过候选。
4. 候选选择使用边界粘滞，不使用取模。到达当前候选列表末项后，即使末项失败也保持在末项，不重新绕回已失败的首项。
5. 两候选 Bundle 链在备用地址失败后保持备用；该失败不能暗中推进更长的元数据候选链。
6. `.version`、`.hash`、`.bytes` 仍遵循 [[ADR-076-startup-whitelist-metadata-routing|ADR-076]] 的候选顺序；Bundle 仍只使用常规 CDN 主备地址。
7. 策略继续由 Nova Asset 模块实现，并通过 YooAsset `IDownloadUrlPolicy` 接入，不修改 YooAsset 下载器源码。

## 后果（Consequences）

### 正面

- 主地址已经异常时，后续 Bundle 不再逐个重复命中主地址。
- 并发失败不会把 package 状态从备用地址推回主地址。
- 元数据四级候选与 Bundle 两级候选共用状态时，不会因短链末项反复失败而跳过长链候选。
- YooAsset 仍负责实际下载、重试和回调，Nova 只维护候选选择规则。

### 负面

- 当前 Editor 生命周期内不会自动探测主地址是否恢复；恢复主地址需要重新建立 package 策略。
- 已经开始的并发请求不会被中途迁移，切换只影响之后创建的请求。

## 被排除的方案（Alternatives）

| 方案 | 否决理由 |
|---|---|
| 每个资源独立从主地址开始 | 已知主地址异常时，每个 Bundle 都会额外失败一次，放大启动耗时和错误量 |
| 每个失败都推进共享取模游标 | 并发失败会连续推进并绕回主地址，实际路由不可预测 |
| 修改 YooAsset 下载器实现主备 | 扩大第三方 fork 影响面；现有 `IDownloadUrlPolicy` 已能承载 Nova 策略 |
| 失败时改写已在飞请求 | YooAsset 请求已经创建，强行替换会引入取消、进度与文件写入一致性风险 |

## 验证依据（Verification）

- 实现：`AssetDownloadUrlPolicy.SelectUrl`、`OnRequestFailed`、`AdvanceAfterOperationFailure`。
- 接入：`AssetManager.GetOrCreateDownloadUrlPolicy`、`BuildHostOptions`、`BuildWebOptions`。
- 契约测试：`AssetStartupWhitelistTests.PackageDownloadFallback_ConcurrentFailuresSwitchOnceAndNeverWrapToPrimary`。
- 回归测试：`AssetStartupWhitelistTests` 13/13、`AssetManagerManifestFallbackRegressionTests` 3/3。
- Inspector：热更地址 HelpBox 明确包级切换、在飞请求和禁止回绕语义。

## 关联

- 白名单元数据边界：[[ADR-076-startup-whitelist-metadata-routing|ADR-076]]
- YooAsset 模块边界：[[PAT-37-no-yooasset-outside-asset-module|PAT-37]]
- Asset 模块入口：[[MOC-Asset]]
