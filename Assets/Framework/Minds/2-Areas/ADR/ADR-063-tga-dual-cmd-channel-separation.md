---
id: ADR-063
title: TGA 插件 ServerCmdName 与 ReportCmdName 双通道不可合并
status: accepted
summary: TGA 埋点上报与账号绑定双通道禁合并
category: module
date: 2026-06-03
aliases:
  - ADR-063-tga-dual-cmd-channel-separation
keywords:
  - ADR-063
  - TGA 插件 ServerCmdName 与 ReportCmdName 双通道不可合并
  - ADR-063-tga-dual-cmd-channel-separation
tags:
  - nova
  - tga
  - sdk
  - network
---

# ADR-063：TGA 插件 ServerCmdName 与 ReportCmdName 双通道不可合并

## 背景

TGA 同时有“埋点上报地址”和“账号绑定上报协议名”两条链路，字段名又都长得像 `xxxCmdName`，容易误判成同一条配置。

## 决策

- `ServerUrl` 重命名为 `ServerCmdName`，由网络指令名解析真实 URL。
- `ReportCmdName` 保持独立，不和 `ServerCmdName` 合并。
- 不保留旧字段兼容壳。

## 影响

- 埋点上报和账号绑定彻底分流。
- 命名更贴近实际调用链。
- 旧序列化值需要在配置面板里重填。
