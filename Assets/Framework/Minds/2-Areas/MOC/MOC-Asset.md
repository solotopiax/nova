---
id: MOC-Asset
title: Asset 模块图谱
summary: Asset资源句柄边界与加解密配置构建启动来源
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
  - "[[ADR-090-resource-keys-before-config-loading|ADR-090]]"
  - "[[MOC-Config]]"
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

## 资源加解密边界

- Config 管理各 YooAsset Package 的 keyId、密钥和源码导出目录；YooAsset Bundle Builder 与 Pipify 选择方案，共用三个构建字段的候选与排序。同一次 Package 构建统一选一种 Bundle 方案，当前没有按文件大小自动分配。
- Editor 从激活且已保存的 ConfigMaster 取密钥，AES/XOR 清单分别匹配同格式构建解密器；客户端通过生成代码注册 `ResourceKeyRegistry.Provider`，AssetManager 自动注入解密适配，无需业务另选构建解密器。
- 原始文件与 `.encrypt` 共存于构建缓存；版本目录的正式资源复制密文并按 FileNameStyle 命名，不能根据 `.bundle` 后缀判断是否加密。
- 原生 AssetBundle 默认走自动解密流，AES memory/XOR 仍先整包还原；AES stream 分块认证读取。RawBundle 是单文件，ArchiveBundle 是多文件归档，两者均非 Unity AssetBundle，解密仍需整包内存。
- 包名/keyId 决定解密密钥；当前编号只用于构建。保留仍被首包、缓存和热更资源使用的旧密钥，源码在 ConfigRuntime 资源加载前可用，避免循环依赖。
- Offset/XOR 是混淆，AES 带完整性认证；客户端自带密钥可被逆向提取，内存与流式不是强弱等级。

当前格式、平台限制与操作入口见 `Assets/Framework/Docs/Runtime/Modules/Asset/ResourceEncryption.md`；来源与发布约束见 [[ADR-090-resource-keys-before-config-loading|ADR-090]]。

## 常见误区

| 误区 | 正确做法 |
|------|----------|
| 把单个资源当成独立生命周期对象处理 | 跟随对应句柄的生命周期管理 |
| 在模块外直接依赖底层资源框架类型 | 通过 `Asset` 模块公开接口访问 |
| 只关注加载，不关注释放 | 设计接口和调用链时把释放路径一起考虑 |
| 把构建清单解密类型再配给客户端 | 使用 Nova 默认运行时适配，构建与客户端密钥来源保持独立 |
| 从 ConfigRuntime 读取解密它本身的密钥 | 将密钥提供器生成到启动时可用的 Player 程序集 |

## 推荐阅读顺序

1. [GLO-07](../Glossary/GLO-07-asset-location.md)
2. `Assets/Framework/Docs/Runtime/Modules/Asset/AssetComponent.md`
3. `Assets/Framework/Docs/Runtime/Modules/Asset/AssetManager/Interfaces/IAssetManager.md`
4. 如需追溯历史决策，再看相关 `ADR`

## 关联

- [[ADR-090-resource-keys-before-config-loading|ADR-090]]：密钥管理、源码导出与启动时可用。
- [[MOC-Config]]：保存自动整理、运行快照和 Editor-only 配置分工。
