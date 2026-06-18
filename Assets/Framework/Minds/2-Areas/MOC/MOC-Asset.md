---
id: MOC-Asset
title: Asset 模块图谱
summary: Asset 模块边界、对象关系与常见误区
category: arch
status: active
date: 2026-06-05
aliases:
  - MOC-Asset
  - Asset 模块图谱
tags:
  - moc
  - nova
  - asset
  - runtime
keywords:
  - Asset
  - AssetManager
  - Asset 地址
  - 资源系统
  - Handle
related:
  - "[[ADR-011-load-unload-and-ireference-pairing|ADR-011]]"
  - "[[ADR-042-assetmanager-load-api-all-return-handle|ADR-042]]"
  - "[[ADR-025-yooasset-url-template-placeholders|ADR-025]]"
  - "[[GLO-07-asset-location|GLO-07]]"
---

# MOC-Asset：Asset 模块图谱

## 模块职责

`Asset` 模块负责统一处理资源定位、加载、预加载、释放、下载与场景加载，是框架内所有资源访问的主入口。

## 关键对象

| 对象 | 作用 |
|------|------|
| `AssetComponent` | Unity 侧入口，向外暴露加载与释放 API |
| `IAssetManager` | 对外契约 |
| `AssetManagerBase` / `AssetManager` | 纯 C# 实现层 |
| `IAssetHandle` / `ISubAssetsHandle` / `IAllAssetsHandle` / `IRawFileHandle` / `ISceneHandle` | 各类资源句柄 |
| `AssetManagerConfig` | 模块配置 |
| `AssetRemoteService` | 远端资源寻址服务 |

## 外部可见边界

- 模块外通过 `Nova.Asset` 或相关公开接口访问资源能力
- 模块外应关注“Asset 地址”和句柄生命周期，不应依赖底层第三方资源框架类型
- 场景加载、原始文件加载、子资源批量加载都统一走句柄模型

## 长期约束

- 加载与释放必须成对出现
- 公开 API 以句柄为核心，不把底层实现细节暴露到模块外
- 模块外讨论资源定位时统一使用“Asset 地址”这一术语
- 句柄语义以 `ADR-042` 为当前口径，不回退到旧裸资源心智

## 常见误区

| 误区 | 正确做法 |
|------|----------|
| 把单个资源当成独立生命周期对象处理 | 跟随对应句柄的生命周期管理 |
| 在模块外直接依赖底层资源框架类型 | 通过 `Asset` 模块公开接口访问 |
| 只关注加载，不关注释放 | 设计接口和调用链时把释放路径一起考虑 |

## 推荐阅读顺序

1. [GLO-07](../Glossary/GLO-07-asset-location.md)
2. `Assets/Framework/Docs/Runtime/Modules/Asset/AssetComponent.md`
3. `Assets/Framework/Docs/Runtime/Modules/Asset/AssetManager/Interfaces/IAssetManager.md`
4. 如需追溯历史决策，再看相关 `ADR`
