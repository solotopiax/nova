---
id: MOC-Vibrate
title: 震动系统图谱
summary: Vibrate 模块入口、数据加载与触觉边界速查
category: module
status: active
date: 2026-06-05
aliases:
  - MOC-Vibrate
  - 震动系统图谱
  - 触觉反馈图谱
  - 振动系统图谱
tags: [moc, nova, vibrate, haptic, runtime]
keywords: [VibrateComponent, IVibrateManager, VibrateManager, VibrateType, PlayCustom, PlayEmphasis, StopAll]
related:
  - "[[ADR-001-component-manager-three-layer|ADR-001]]"
  - "[[ADR-010-validation-on-consumer-side|ADR-010]]"
  - "[[ADR-011-load-unload-and-ireference-pairing|ADR-011]]"
  - "[[ADR-012-third-party-info-isolation|ADR-012]]"
  - "[[ADR-042-assetmanager-load-api-all-return-handle|ADR-042]]"
  - "[[PAT-28-luban-load-release-symmetric|PAT-28]]"
---

# MOC-Vibrate：震动系统图谱

## 一句话

Vibrate 模块是 Nova 的统一触觉入口；业务只通过 `Nova.Vibrate` 表达“播什么震动”，不直接感知底层三方触觉实现。

## 何时查这页

- 要改震动能力或震动数据加载
- 要分清预设震动、自定义震动、强调震动
- 要确认三方触觉能力是否越过了 Nova 边界

## 当前结构

```text
Nova.Vibrate
  -> VibrateComponent
  -> IVibrateManager
  -> VibrateManagerBase
  -> VibrateManager
```

组件入口：

- `Start()` 用 `VibrateSettings` 初始化 Manager
- `LoadSync / LoadAsync` 负责装载震动数据
- 对外高频入口是 `Play / Play(type) / PlayCustom / PlayEmphasis / StopAll`

## 高频入口

- `Play()`
- `Play(VibrateType type)`
- `PlayCustom(string name | float intensity, float sharpness, float preDuration, float duration)`
- `PlayEmphasis(string name | float amplitude, float frequency, float preDuration, float interval)`
- `StopAll()`
- `Enable`
- `IsSupported`

## 模块边界

- 业务层只关心 `VibrateType`、命名组和简单参数
- `VibrateManager` 内部可以接三方能力，但接口层和知识层不应扩散三方类型
- 数据加载属于模块自身准备过程，但底层资源加载仍要遵守统一的 Load/Release 配对

## 与其他模块的关系

- `Asset / Table`：震动配置数据加载遵守统一数据接收与释放模式
- `Sound`：两者都是“表现反馈模块”，但声音与触觉的资源和接口边界不同，不要混成一个反馈总线

## 导航提醒

- 组件对外方法名是 `LoadSync / LoadAsync`，Manager 内部真实方法名是 `LoadVibrateDataSync / LoadVibrateDataAsync`
- 名称型 `PlayCustom(name)` / `PlayEmphasis(name)` 依赖已加载的数据表
- 具体优先级、平台支持细节与资源释放路径，以 `Docs` 和源码为准。

## 常见误区

- 在业务层直接绑定某个三方触觉 SDK
- 把触觉数据加载写成说明书式大流程，而忽略真正的公开入口
- 把 `Enable` 和“设备是否支持”混成一个概念
- 忘记名称型播放依赖数据已先完成加载

## 先往哪看

- 改结构：[[ADR-001-component-manager-three-layer]]
- 改三方隔离：[[ADR-012-third-party-info-isolation]]
- 改数据加载对称性：[[PAT-28-luban-load-release-symmetric]]

## 关联

- 图谱：[[MOC-Sound]]、[[MOC-Manager]]
