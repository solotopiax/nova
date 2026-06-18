---
id: PAT-70
title: YooAsset PackRule 选型与 PackTopDirectory 陷阱
type: pattern
status: active
summary: PackRule 按粒度选；TopDir 需子目录
category: asset
date: 2026-05-23
aliases:
  - PAT-70-yooasset-packrule-selection
keywords:
  - PAT-70
  - PAT-70-yooasset-packrule-selection
tags:
  - pattern
  - nova
  - yooasset
  - build
---

# PAT-70：YooAsset PackRule 选型与 PackTopDirectory 陷阱

## 适用场景

在 Nova 工程中为 YooAsset Bundle Collector 配置 `IPackRule` 时，需要按 Collector 根目录结构选择正确的打包粒度。

## 核心做法

按 Collector 根目录结构与加载粒度选择。三条易混规则的精确语义：

- **`PackSeparately`**：**文件级**。无论资源在 Collector 下几层，**每个文件**单独成包，bundle name = 资源路径去扩展名。
- **`PackDirectory`**：**目录级**。bundle name = 资源**直属父目录**（`Path.GetDirectoryName(AssetPath)`）。无论嵌套多深，每个含资源的父目录单独成包；若 Collector 根目录直接平铺资源，则 Collector 自身也会形成一个 bundle。空目录不计。
- **`PackTopDirectory`**：**一级子目录级**。取 Collector 根下相对路径的**第一段**作为 bundle 名。第一段必须是子目录（非文件）——即 Collector 根下必须至少存在一层子目录；若资源直挂根目录，构建期抛 `Root directory not found`。

| 规则 | 语义 | 适用 |
|------|------|------|
| `PackSeparately` | 每个文件一个 Bundle | dll 等需独立热更的资源 |
| `PackDirectory` | 每个含资源的父目录一个 Bundle | 模块化 UI，每个 View 一个 Bundle |
| `PackTopDirectory` | 按 Collector 根的**一级子目录**打（必须有子目录） | 资源散落多层但希望按一级模块整体加载 |
| `PackCollector` | 整个 Collector 一个 Bundle | 根目录下直接平铺资源（Configs / Jsons / 平铺 Fonts） |
| `PackGroup` | 同 Group 合一个 Bundle | 跨目录共享聚合 |

Nova 当前推荐配置：
- `Configs` / `Jsons` / 平铺 `Fonts` → `PackCollector`
- `Dlls` → `PackSeparately`
- `UIs` → `PackDirectory`
- 仅当 Collector 根下确实存在 ≥1 层子目录划分时才用 `PackTopDirectory`

TMP Default Font Asset 由 TMP Settings 硬引用走主包，**不要**加入 YooAsset Collector，避免热更链路冲突。

## 反模式

- **Collector 根直接挂资源 + `PackTopDirectory`**：YooAsset 取「根的下一级目录名」失败，构建必报 `Root directory not found: 'xxx.asset'`。
- **`PackTopDirectory` 配在大模块根**（如 `Assets/UI/`）：会被压成一个巨型 Bundle，首次加载几十 MB、改一份资源整包重下，热更代价爆炸。粒度建议落在「单个功能模块」级。
- **大量小图标用 `PackDirectory`**：产生过多小 Bundle，不如合并到 `PackTopDirectory`。
- **字体放 `Resources/` 又同时进 Collector**：与 YooAsset 热更链路冲突。
