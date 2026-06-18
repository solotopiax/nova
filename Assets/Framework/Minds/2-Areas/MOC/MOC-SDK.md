---
id: MOC-SDK
title: SDK 第三方接入图谱
summary: SDK 插件容器、配置注入与生命周期边界速查
category: module
status: active
date: 2026-06-05
aliases:
  - MOC-SDK
  - SDK图谱
  - 第三方接入图谱
tags: [moc, nova, sdk, plugin, runtime]
keywords: [SDKComponent, SDKManager, ISDKManager, ISDKPlugin, SDKPluginBase, SDKPluginEntry, InitializeTask, Get, GetAll]
related:
  - "[[ADR-001-component-manager-three-layer|ADR-001]]"
  - "[[ADR-002-manager-priority-system|ADR-002]]"
  - "[[ADR-008-managerbase-internal-abstract|ADR-008]]"
  - "[[ADR-012-third-party-info-isolation|ADR-012]]"
  - "[[ADR-022-sdk-plugin-architecture|ADR-022]]"
  - "[[PAT-33-sdk-plugin-sop|PAT-33]]"
---

# MOC-SDK：SDK 第三方接入图谱

## 一句话

SDK 模块是“插件容器 + 生命周期编排层”：业务通过 `Nova.SDK` 使用抽象插件，不直接把第三方 SDK 类型扩散到框架外。

## 何时查这页

- 要接入或下线某个第三方 SDK
- 要判断配置注入、初始化和查询入口应该放在哪里
- 要确认插件边界，而不是去找某一家厂商的具体实现细节

## 当前结构

```text
Nova.SDK
  -> SDKComponent
  -> ISDKManager
  -> SDKManagerBase
  -> SDKManager

插件侧：
ISDKPlugin
  -> SDKPluginBase
  -> 各业务接口（IAuthPlugin / IAdPlugin / IPushPlugin / ITrackPlugin ...）
```

## 当前使用面

- `await Nova.SDK.InitializeTask`
- `Nova.SDK.Get<TPlugin>()`
- `Nova.SDK.TryGet<TPlugin>(out plugin)`
- `Nova.SDK.GetAll<TInterface>()`
- `Nova.SDK.Login(userId)`

## 配置注入的真实边界

当前代码不是“业务侧手工注入配置”的模型，而是：

1. `SDKComponent` 在 `Start()` 把 `SDKPluginEntry` 列表交给 `SDKManager.Initialize`
2. `SDKManager.InitializeAsync()` 按优先级初始化插件
3. `SDKManager` 根据 `SDKPluginBase.RequiredConfigType`
4. 通过 `IConfigManager.GetSDKPluginConfig(type)` 自动拉取对应配置
5. 把配置传给插件 `InitializeAsync`

这意味着：

- SDK 配置来源在 `Config`
- SDK 模块负责装配与调度
- 业务层不应再维护一套独立的手工配置注入协议

## 插件侧应该长什么样

- 纯 C# 类，不挂 `MonoBehaviour`
- 继承 `SDKPluginBase`
- 通过 `Priority` 参与初始化顺序
- 需要配置时声明 `RequiredConfigType`
- 初始化成功后由基类管理 `IsAvailable`

## 与其他模块的关系

- `Config`：提供插件配置
- `Event`：`Login` 会向事件系统发送 `SDKEventData.UserLogin`
- `Procedure / 业务入口`：通常在启动后等待 `InitializeTask` 再使用具体插件

## 常见误区

- 继续按旧认知保留一套业务侧手工配置注入协议
- 在业务代码里直接 `using` 第三方 SDK 命名空间
- 把插件写成 `MonoBehaviour` 或挂到场景对象上
- 跳过 `InitializeTask` 直接 `Get<T>()`
- 让子包之间互相耦合，而不是只依赖核心 SDK 抽象

## 先往哪看

- 改 SDK 架构边界：[[ADR-022-sdk-plugin-architecture]]
- 改第三方信息隔离：[[ADR-012-third-party-info-isolation]]
- 接入 SOP：[[PAT-33-sdk-plugin-sop]]

## 关联

- 图谱：[[MOC-Config]]、[[MOC-Manager]]
