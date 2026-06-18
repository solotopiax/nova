---
id: PAT-125
title: 运行时资源增量更新三步走与 YooAsset 缓存寻址认知
type: pattern
summary: 刷清单按tag下载运行三步；tag不落盘无需维护列表
category: asset
status: active
date: 2026-05-30
aliases:
  - PAT-125-runtime-incremental-three-steps
keywords:
  - PAT-125
  - 运行时资源增量更新三步走与 YooAsset 缓存寻址认知
  - PAT-125-runtime-incremental-three-steps
tags: [pattern, nova, asset, hotfix]
related:
  - "[[ADR-051-launch-asset-slice-strategy|ADR-051]]"
  - "[[ADR-052-asset-cache-two-layer-cleanup|ADR-052]]"
  - "[[MOC-Asset]]"
---

# PAT-125：运行时资源增量更新三步走与 YooAsset 缓存寻址认知

## 模式（Pattern）

运行时按需拉增量资源，标准三步：

```csharp
// ① 强刷清单：拿服务器最新版本号（绕过 LoadManifestAsync 幂等守卫）
await Nova.Asset.RefreshManifestAsync("DLC1");

// ② 按 tag 或 location 创建切片下载器
var downloader = Nova.Asset.CreateDownloaderByTags(new[] { "battle", "boss_1" });
downloader.OnProgress += (fc, tc, fb, tb) => view.Refresh(fc, tc, fb, tb, downloader.DownloadSpeed);

// ③ 运行并等待
if (!await downloader.RunAsync(ct)) { /* 弹"网络异常，是否重试" */ }
```

DLC 整包切换只是在下载器和 Load 地址上换成同一 tag / 包名。

## 核心认知：无需维护本地 tag 列表

- 业务侧不需要持久化“我下过哪些 tag”。
- YooAsset 比对的是 bundle hash 和本地缓存，不是 tag 列表。
- tag 只是本次下载的过滤器，应该跟着业务事件走。

## 适用场景

- 策略 B（启动 tag 切片 + 运行时增量）项目的运行时补包
- 进入新关卡/章节/DLC 前的预下载
- 不适用：策略 A（启动整包差异已覆盖一切）项目运行时无需此模式

## 反模式（Anti-pattern）

| 反模式 | 后果 | 正解 |
|---|---|---|
| 业务维护持久化“已下 tag 列表” | 冗余且重复缓存模型 | 不存列表，靠业务事件携带 tag |
| Refresh 前不 Cancel 进行中下载器 | 清单和下载器状态不一致 | 先 Cancel 再 Refresh |
| 把“版本检查漏 tag”与“旧 bundle 占磁盘”混为一谈 | 错误归因 | 前者看 hash，后者做清理 |

## 关联
- 决策依据：[[ADR-051-launch-asset-slice-strategy|ADR-051]]（启动切片策略 A/B）
- 配套清理：[[ADR-052-asset-cache-two-layer-cleanup|ADR-052]]（缓存两层清理）
- MOC：[[MOC-Asset]]

---
