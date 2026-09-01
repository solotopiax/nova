---
id: PAT-164
title: Firebase 默认 Topic 分层同步与差异存档
type: pattern
status: active
date: 2026-08-17
summary: 默认Topic分层同步并存档差异
category: module
aliases:
  - PAT-164-firebase-default-topic-sync
  - Firebase 默认 Topic 同步
  - Firebase 默认推送 Topic
keywords:
  - PAT-164
  - FirebaseDefaultTopics
  - Firebase Topic
  - FCM Topic
  - top_debug_all
  - top_release_all
  - top_debug_lang
  - top_release_lang
  - top_debug_timezone
  - top_release_timezone
  - top_debug_country
  - top_release_country
tags:
  - pattern
  - nova
  - sdk
  - firebase
  - push
  - topic
related:
  - "[[PAT-33-sdk-plugin-sop|PAT-33]]"
  - "[[PAT-41-upm-package-layout-and-manifest|PAT-41]]"
---

# PAT-164：Firebase 默认 Topic 分层同步与差异存档

## 适用场景

- SDK 包需要在第三方初始化完成后自动订阅一组默认推送 Topic。
- Topic 维度来自多个异步数据源，例如本地化、广告国家码或平台宏。
- 旧订阅必须在语言、国家、时区等维度变化后被退订，避免用户长期留在错误分群。
- 启动早期存在“真实值暂不可用”的窗口，不能因为临时空值或占位值误删上一次有效订阅。

## 核心做法

1. 默认 Topic 使用统一业务前缀根和环境分群前缀，例如 Firebase 当前按 `IConfigManager.DevelopMode` 使用 `top_debug_` 或 `top_release_`；Config Manager 不存在或未加载完成时只按 Debug 处理，避免误订阅正式分群。
2. Topic 按数据源就绪语义拆分同步链路，而不是塞进一个一次性初始化函数：
   - 基础状态：全量、语言、平台、时区。
   - 国家状态：广告模块异步返回的最终国家码。
3. 基础状态在 Firebase 初始化完成后启动；全量、平台、时区可立即同步，语言只在 `Nova.Localization.Language` 已有有效值或收到 `LocalizationRefreshEventData` 后进入新状态。
4. 国家状态通过 AD 模块的异步国家码接口获取最终值；等待、超时和上次成功缓存兜底由 AD 模块负责，Firebase 不直接等待广告数据槽位，也不使用系统区域兜底。
5. 需要进入 Topic 名称的动态片段必须先清洗为 Firebase Topic 安全字符；协议上报格式和 Topic 格式分开，例如服务端时区可用 `+08:00`，Topic 使用 `utc_plus_08`。
6. 上一次成功订阅状态通过 `IFileFragmentManager` 持久化，按来源拆分 item，例如 `FirebaseDefaultTopics/BaseState` 与 `FirebaseDefaultTopics/CountryState`。
7. 每次同步先构建当前状态，再和旧存档计算差异；旧状态独有 Topic 先退订，新状态独有 Topic 再订阅。
8. 只有所有退订和订阅操作都成功后才覆盖保存新状态；若 Firebase 调用失败、取消或持久化管理器不可用，不应伪造成功存档。
9. 对同一持久化 item 的同步需要串行化，避免启动同步和语言刷新同时读写同一状态。

## 为什么这样定

- Firebase 初始化、本地化初始化、广告国家码发布不是同一生命周期；强行一次性同步会把启动空值固化成错误 Topic。
- 语言和国家属于用户分群条件，旧 Topic 不退订会让推送触达长期偏离当前用户状态。
- 国家码中的 `IV` 是未知占位，不是有效国家；把它当 Topic 会污染服务端分群。
- 时区和国家存在非整点、跨区域等真实场景，Topic 命名必须兼顾 Firebase 字符限制和可读性。
- 持久化只记录“上一次成功完成的订阅状态”，可以在下次启动时做幂等差异同步，减少重复调用并避免半成功状态覆盖事实。

## 反模式

- Firebase 初始化回调里直接订阅所有 Topic，不等待本地化或广告国家码真实值。
- 把 `+08:00`、`+05:30` 这类协议时区字符串直接拼进 Firebase Topic。
- 只订阅新语言或新国家 Topic，不退订旧 Topic。
- 把空国家、`IV` 或 `Language.Unspecified` 写入 Topic 或覆盖旧存档。
- 订阅操作失败后仍保存当前状态，导致下次启动误以为已经同步成功。
- 把所有默认 Topic 存在同一个未分层状态里，导致国家等待超时影响基础 Topic。

## 来源与验证

- 当前实现：`UPMPackages/com.solotopia.nova.framework.sdk.firebase/Nova/Scripts/Runtime/Topics/FirebaseDefaultTopicBuilder.cs` 负责 Topic 构建、安全字符清洗、国家码规范化和订阅差异计算。
- 当前实现：`UPMPackages/com.solotopia.nova.framework.sdk.firebase/Nova/Scripts/Runtime/Topics/FirebasePlugin.DefaultTopics.cs` 负责初始化后启动、监听 `LocalizationRefreshEventData`、通过 AD 模块获取最终国家码、持久化状态并应用差异。
- 当前文档：`UPMPackages/com.solotopia.nova.framework.sdk.firebase/Nova/Doc/FirebasePlugin.md` 已记录默认 Topic 类型、示例、语言等待、国家码获取和存档语义。
- 当前测试：`UPMPackages/com.solotopia.nova.framework.sdk.firebase/Tests/Editor/FirebaseDefaultTopicBuilderTests.cs` 覆盖 `top_debug_` / `top_release_` 环境前缀、旧 `top_` 存档迁移、`utc_plus_05_30` / `utc_minus_03_30` 非整点时区、`IV` 国家跳过和差异计算。

## 关联

- SDK Plugin 接入流程：[[PAT-33-sdk-plugin-sop|PAT-33]]
- UPM 包结构：[[PAT-41-upm-package-layout-and-manifest|PAT-41]]
