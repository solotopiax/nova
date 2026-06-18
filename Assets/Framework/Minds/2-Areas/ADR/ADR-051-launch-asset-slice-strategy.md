---
id: ADR-051
title: 启动期资源切片策略 A/B 二选一，框架 API 不绑产品决策
summary: 整包差异 XOR 切片增量二选一，框架透传不选策略
category: hotfix
status: accepted
date: 2026-05-30
aliases:
  - ADR-051-launch-asset-slice-strategy
keywords:
  - ADR-051
  - 启动期资源切片策略 A/B 二选一，框架 API 不绑产品决策
  - ADR-051-launch-asset-slice-strategy
tags: [adr, nova, asset, hotfix]
supersedes: []
superseded-by: []
related:
  - "[[ADR-013-hotfix-master-switch|ADR-013]]"
  - "[[ADR-025-yooasset-url-template-placeholders|ADR-025]]"
  - "[[ADR-042-assetmanager-load-api-all-return-handle|ADR-042]]"
  - "[[MOC-Asset]]"
  - "[[MOC-HybridCLR]]"
---

# ADR-051：启动期资源切片策略 A/B 二选一，框架 API 不绑产品决策

## 背景（Context）

Nova 的 Asset 模块需同时支持三层资源诉求：包内资源（StreamingAssets 内置）、启动期 CDN 热更、运行时 CDN 增量。调研中发现一个反复纠缠的认知陷阱：

> "如果启动时本地 manifest 与云端 manifest 整包对比，把所有差异 bundle 都拉下来了，那运行时增量更新就不需要了？"

这暴露了「启动整包差异」与「运行时增量」**本质是二选一（XOR）而非叠加**。框架若硬编码其中一种，会让另一类项目无法接入：
- 包小的单机/工具型项目想要"启动一次下完"，运行时增量 API 对它是多余复杂度
- CDN 资源 > 500MB 的重度项目想要"启动只下核心 + 进场景再补"，强制整包差异会导致首启等待 10 分钟、流失率爆炸

当前 `IAssetManager.CreateDownloader` 只有整包全量一个入口，YooAsset 原生的 `CreateResourceDownloader(tags)` / `CreateBundleDownloader(locations)` 未透传，重度项目无法落地策略 B。

## 决策（Decision）

### 1. 框架透出三入口，业务自由选策略

| 入口 | 方法 | 用途 |
|---|---|---|
| 整包差异 | `CreateDownloader(package, concurrency, retry)`（已有，保留） | 策略 A：全集比对 |
| tag 切片 | `CreateDownloaderByTags(string[] tags, package, concurrency, retry)`（新增） | 策略 B：启动只下核心 + 运行时按业务切片 |
| location 切片 | `CreateDownloaderByLocations(string[] locations, package, concurrency, retry)`（新增） | 精确预下载已知 Asset 地址 |

空 tags 数组语义等价整包（`CreateResourceDownloader` 无参）。底层复用 `ResolvePackageName` / `GetPackage`。

### 2. 两种策略对照

| 策略 | 启动期 | 运行时 | 适用 |
|---|---|---|---|
| A · 一次性同步 | 整包差异全拉 | 无下载，纯 LoadAsync | 包小 / 单机 / 工具型 |
| B · 按需切片 | 只下 launch tag | 进战斗/章节按 tag 增量 | 中重度 / 有 DLC / CDN > 500MB |

### 3. 启动期切片配置下沉 AssetComponent Inspector

`AssetComponent` 新增 `[SerializeField] private List<string> m_LaunchHotfixTags`（List 形式，有几个 tag 加几个元素），经五段透传链进 `AssetManagerConfig.LaunchHotfixTags`。`ProcedureHotfix` 读取：空列表→整包差异（策略 A）；非空→`CreateDownloaderByTags`（策略 B）。配套首包构建 `BundledCopyOption=ClearAndCopyByTags`。与 ADR-013 一致，该开关是项目级编译期决策，下沉 Inspector 抗远端下发。

### 4. 关键认知：无需维护本地 tag 列表

YooAsset 缓存是 **bundle 文件级 + manifest hash 寻址**，tag 只是"本次下载筛哪些 bundle"的过滤器，**不落盘、不持久化**。版本检查永远比对本地缓存物理现状：

```
下载列表 = 新 manifest bundle  ⊖  本地沙盒已缓存且 hash 一致的 bundle
```

因此策略 B 下，某 tag（如 A）资源的更新由"进 A 场景"这个业务事件天然携带的 `"A"` tag 去补——`"A"` 硬编码在业务场景代码里，不是需要运行期动态维护的列表。

## 后果（Consequences）

### 正面
- 框架不替业务定产品策略，A/B 两类项目同一套 API 都能落地
- 向后兼容：现有无参 `CreateDownloader()` 保留，老项目零改动
- 业务侧零认知负担：不需要维护任何持久化 tag 列表

### 负面
- 业务需理解 A/B 差异并正确配置 `LaunchHotfixTags` + `BundledCopyOption` 组合，配错会导致启动期下载量不符预期
- 存在一种别扭的中间策略（"启动增量更新曾下过的若干 tag 但不做整包差异"）会逼业务维护持久化列表——文档需明确劝退此策略

## 被排除的方案（Alternatives）

| 方案 | 否决理由 |
|---|---|
| 框架硬编码"启动整包差异" | 重度项目首启等待爆炸，无法接入 |
| 框架硬编码"启动 tag 切片" | 单机/工具项目被迫维护无意义 tag |
| 让业务维护本地已下 tag 列表做增量 | YooAsset 缓存 bundle 文件级寻址，版本检查比对物理现状，列表是冗余且易漏的复杂度 |

## 验证依据（Verification）
- 源码：`IAssetManager.cs`（新增 ByTags/ByLocations）、`AssetManager.cs`（实现）、`AssetComponent.Visitors.cs`（m_LaunchHotfixTags List）、`ProcedureHotfix.cs`（整包/切片二分）
- grep 关键词：`CreateDownloaderByTags` / `LaunchHotfixTags` / `CreateResourceDownloader`
- 审查要点：空 tags 必须等价整包；List 透传走五段链不直接序列化 Config

## 关联
- 规范落点：热更链路约束
- 相关 ADR：[[ADR-013-hotfix-master-switch|ADR-013]]（EnableHotfix 总开关，本 ADR 在其子链路内细分）、[[ADR-025-yooasset-url-template-placeholders|ADR-025]]（CDN 寻址）、[[ADR-042-assetmanager-load-api-all-return-handle|ADR-042]]（Handle 体系，下载器同样调用方持有）
- 相关 Pattern：运行时增量三步走（[[PAT-125-runtime-incremental-three-steps|PAT-125]]）
- MOC：[[MOC-Asset]]、[[MOC-HybridCLR]]

---
