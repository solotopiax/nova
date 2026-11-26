---
id: PAT-109
title: 独立 UPM Kit/SDK 的文档入口与归属
summary: 独立包文档从实际解析版本的索引进入
category: docs
type: pattern
status: active
date: 2026-05-26
aliases:
  - PAT-109
  - PAT-109-upm-package-docs-mandatory
  - UPM Kit/SDK 包必备 Nova/Docs 文档目录
keywords:
  - PAT-109
  - 包文档发现
  - Nova/Doc/INDEX.md
tags: [pattern, docs, upm, kit, sdk, workflow]
related:
  - "[[PAT-41-upm-package-layout-and-manifest|PAT-41]]"
  - "[[PAT-05-l0-l1-l2-docs|PAT-05]]"
  - "[[ADR-031-upm-three-piece-mandatory|ADR-031]]"
---

# PAT-109：独立 UPM Kit/SDK 的文档入口与归属

## 适用场景

- 新建或维护 Nova 自有 Kit / SDK 包
- 定位现有项目实际使用的包文档
- 审查独立包的文档入口与公开能力是否一致

## 核心做法

### 1. 包内索引跟随版本

项目从实际解析的包根读文档。Unity 已注册包按 `PackageInfo.resolvedPath` 定位，尚未注册的本地包按有效 `file:` 绑定定位。先查 `Nova/Doc/INDEX.md`，兼容既有 `Nova/Docs/INDEX.md` 和 `Nova/DOCS/INDEX.md`。完整规则见 [SDK / Kit 包文档发现](../../../Docs/Runtime/Modules/SDK/INDEX.md#sdk--kit-包文档发现)。

### 2. 从索引找到用法

包内 `INDEX.md` 链接公开能力的接口、配置、示例与错误处理。文档位置和用法随包版本一起维护。当前实例是 `com.solotopia.nova.framework.sdk.tga` 的 `Nova/Doc/INDEX.md`，它链接到 `TGAPlugin.md` 和 `TGAPluginConfig.md`，分别说明埋点入口和配置。

### 3. 主框架与独立包的职责

| 位置 | 记录内容 |
|---|---|
| [主框架文档索引](../../../Docs/INDEX.md) | 主框架接口、共享约束和独立包发现入口 |
| 独立包内的索引与正文 | 包自己的公开能力、平台配置、示例与排障 |
| 包级 README、CHANGELOG 与 LICENSE | 包的介绍、版本变化和许可，详见 [[ADR-031-upm-three-piece-mandatory|ADR-031]] |
| 包内原厂文档 | 第三方的原始说明，目录归属见 [[PAT-41-upm-package-layout-and-manifest|PAT-41]] |

核对时沿包内索引打开受影响的文档，按实际包版本检查示例和声明。跨包引用给出准确包名和包内路径，并链接包发现入口。

## 为什么这样做

项目可能同时存在开发仓、嵌入包和 Unity 缓存副本。跟随实际解析版本可以让接入说明与代码配套。把 API 和配置说明留在所属包，也能在包单独升级或搬迁时保留可用入口。

## 反模式

- 只看已下载目录或历史缓存来推断当前包版本
- 在主框架索引逐包挂开发仓路径
- 包内正文脱离索引，项目组无法从入口找到用法
- 接口、参数和平台条件已经变化，文档仍沿用旧版本

## 来源

- [SDK / Kit 包文档发现](../../../Docs/Runtime/Modules/SDK/INDEX.md#sdk--kit-包文档发现)
- [NA-015 源码、文档、知识说明和 Agent 暴露同轮一致](../../../Docs/Onboarding/DEVELOPMENT_CONSTRAINTS.md#na-015源码文档知识说明和-agent-暴露同轮一致)

## 关联

- [[PAT-41-upm-package-layout-and-manifest|PAT-41]]
- [[PAT-05-l0-l1-l2-docs|PAT-05]]
- [[ADR-031-upm-three-piece-mandatory|ADR-031]]
