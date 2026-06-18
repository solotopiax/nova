---
id: ADR-054
title: Kit 配置进三维矩阵 + Network 表删维度列 PreFilter 降级纯拷贝（反转 ADR-053 决策 3）
summary: 环境差异上移 Config 三维矩阵，网络表降纯拷贝
category: module
status: accepted
date: 2026-05-31
aliases:
  - ADR-054
keywords:
  - ADR-054
  - Kit 配置进三维矩阵 + Network 表删维度列 PreFilter 降级纯拷贝（反转 ADR-053 决策 3）
tags:
  - adr
  - nova
  - config
  - kit
  - network
supersedes: []
superseded-by: []
related:
  - "[[ADR-053-kit-config-templating|ADR-053]]"
  - "[[ADR-022-sdk-plugin-architecture|ADR-022]]"
  - "[[MOC-Config|MOC-Config]]"
  - "[[MOC-Network|MOC-Network]]"
---

# ADR-054：Kit 配置进 PlatformxChannelxDevelopMode 三维矩阵

## 背景

Network 源表删掉平台 / 渠道等维度列后，Kit 的 `cmdName` 也开始依赖三维组合，不再适合继续做全局单份。

## 决策

- `KitConfigs` 从全局单份下沉到 `PlatformChannelEntry`，按 `DevelopMode` 分格存储。
- `KitConfigScanner`、`Exporter`、`Validator`、`ConfigWindow` 全部改为和 SDK 同构的三维矩阵写法。
- 运行时 `GetKitConfig` 链路不改，导出物仍是单格快照。
- 旧的全局 `KitConfigs` 数据不迁移，改完后重新填写。
- `NetworkExcelPreFilter` 降级为纯拷贝，保留 `_temp` 链路，不再做表层过滤。

## 影响

- Kit 配置表达力与 Network 表语义重新对齐。
- Kit / SDK 的存储与导出路径统一。
- 现有全局 Kit 数据作废，属于一次性破坏性调整。

## 关联

- 反转 `ADR-053` 的决策 3
- 相关 ADR：`ADR-053`、`ADR-058`
