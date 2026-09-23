---
id: ADR-087
title: 微信小游戏能力统一收口 Plugin 并以 Solar 公开行为为覆盖基线
status: accepted
date: 2026-09-22
summary: 微信能力统一由Plugin稳定封装
category: module
aliases:
  - wechat-minigame-plugin-facade
  - 微信小游戏统一客户端门面
keywords:
  - ADR-087
  - WeChatMiniGamePlugin
  - IWeChatMiniGameBackend
  - Solar WXHelper
  - NOVA_WECHAT_SDK_AVAILABLE
  - WeChatWASM
tags:
  - nova
  - sdk
  - wechat
  - api
related:
  - "[[ADR-022-sdk-plugin-architecture|ADR-022]]"
  - "[[ADR-086-wechat-minigame-server-authoritative-payment-orders|ADR-086]]"
  - "[[PAT-104-no-obsolete-shim-rule|PAT-104]]"
  - "[[PAT-141-vendor-source-readonly|PAT-141]]"
  - "[[PAT-143-vendor-sdk-missing-api-nova-layer-fill|PAT-143]]"
---

# ADR-087：微信小游戏能力统一收口 Plugin 并以 Solar 公开行为为覆盖基线

## 背景

微信小游戏原厂 SDK 的 API 数量多、回调模型不统一，并直接暴露 `WeChatWASM` 类型。若各项目自行包装登录、支付、订阅、隐私、文件、广告和监听能力，会重复处理取消、超时、主线程、对象释放、平台判断和安全边界，且不同项目会形成不兼容的调用方式。

Solar `WXHelper` 已验证了一批真实项目需要的行为，但它同时包含旧框架耦合、散落状态、硬编码测试参数和历史平台分支。Solar 适合作为最低行为清单，不适合作为 Nova 的代码模板或公共类型契约。

## 决策

- 项目业务统一从 `Nova.SDK.Get<WeChatMiniGamePlugin>()` 获取能力。登录、支付、订阅、隐私等高频行为直接由 Plugin 提供；有状态能力由 Plugin 返回 Nova 包装对象。
- Nova 公共 API 的参数、返回值、事件、基类、接口、泛型约束和委托签名不得暴露 `WeChatWASM` 类型。原厂 DTO 和回调只允许存在于包内适配实现。
- Solar `WXHelper` 已公开的每一种业务行为都是 Nova 的最低覆盖基线，必须存在可直接调用的等价实现和自动化映射测试；等价指行为可达，不要求复刻旧命名、旧流程或旧缺陷。
- `IWeChatMiniGameBackend` 是框架定义的服务端能力契约，包内 `WeChatMiniGameBackend` 提供 Nova Network + Protobuf 的默认客户端实现。项目只配置 HostKey/NetCmd 路由和实现业务服务端，不重复编写客户端 Backend。
- 服务端契约固定覆盖登录校验、道具签名、单笔验单、当前用户全订单查询、订阅结果登记和文本安全六项。AppSecret、session_key、access_token、Midas 密钥和私钥只存在于服务端。
- 有状态原厂对象由 Nova 包装并实现 `IDisposable`，监听必须使用同一委托对称注册与注销；文件系统包装器本身不持有独占对象，但打开的文件描述符必须显式关闭。
- Nova 适配层统一记录微信异步 API 的完成、失败、超时和取消结果，持续监听事件使用 Debug 级别；日志只保留 API 名和脱敏摘要，不输出登录 code、身份、用户内容、token、路径、订单号或签名原文。
- `NOVA_WECHAT_SDK_AVAILABLE` 只是各源码文件内部的条件编译别名，不是 Unity 全局宏。`UNITY_WEBGL`、`UNITY_EDITOR` 是 Unity 内置符号；三种微信符号由不同微信导出器或工具链提供，Nova 仅兼容识别。编译可用不等于运行在微信容器，运行时仍由 `WeChatMiniGameEnvironment.IsMiniGame` 裁决。
- 同类文件进入以能力命名的目录。公开入口位于对应主文件并按重要性、调用热度从上到下排列；私有方法进入该子模块的 `.Methods.cs`，成员字段进入该子模块的 `.Visitors.cs`。Config 同样按 Account、Payment、Social、Security、Ads 等职责拆分。
- 本包尚未发布时，公开 API 直接收敛到最终命名并同步全部调用方，不保留旧名或 `[Obsolete]` 过渡层。

## 关键命名语义

| API | 稳定语义 |
|---|---|
| `LoginAndVerifyAsync` | 微信登录并交给默认 Backend 校验身份 |
| `PurchaseAndVerifyAsync` | 建单、拉起支付、验单并在允许时发货 |
| `PurchaseAsync` | 只建单和拉起支付，不验单、不发货 |
| `VerifyPaymentOrderAsync` | 手动校验一笔本地订单并尝试发货 |
| `VerifyAllPaymentOrdersAsync` | 遍历当前 UID 的本地未完成订单逐笔验单 |
| `QueryCurrentUserPaymentOrdersAsync` | 通过独立协议只读查询服务端可见的当前用户全部订单 |

## 后果

### 正面

- 全公司项目获得一致、可发现、可测试的微信小游戏调用面，不需要重复造客户端适配轮子。
- 原厂 SDK 升级、平台宏变化和回调差异被限制在单一 UPM 包内，业务代码保持稳定。
- Solar 行为映射测试与厂商类型隔离测试可以阻止能力回退和原厂类型泄漏。

### 代价

- Nova 需要持续维护较大的平台能力面，并在微信官方 SDK 升级时复核每个包装器的行为和生命周期。
- 编译期兼容多个微信符号会提高条件分支数量，因此必须同时保留运行时容器检测，不能以宏单独判断真实运行环境。
- 长尾原厂 API 不自动成为公共接口；新增接口必须证明有跨项目复用价值，并补齐取消、超时、线程和释放语义。

## 被排除方案

| 方案 | 否决理由 |
|---|---|
| 项目直接调用 `WeChatWASM` | 厂商类型渗透业务层，升级成本和误用风险扩散到所有项目 |
| 每个项目自行实现 `IWeChatMiniGameBackend` | 重复 Nova Network、Protobuf、错误转换和生命周期代码，无法形成公司统一标准 |
| 完整复制微信全部 API | 公共面无限膨胀，长尾能力缺少统一语义和维护收益 |
| 逐行照搬 Solar `WXHelper` | 会继承旧框架耦合、历史平台分支、硬编码参数和不对称监听等问题 |
| 只凭 `NOVA_WECHAT_SDK_AVAILABLE` 判断运行环境 | 该符号只说明当前源码可引用适配层，Editor 和普通 WebGL 也可能满足编译条件 |
| 为旧命名保留兼容方法 | 本包尚未发布，没有外部兼容成本，双入口只会制造歧义 |

## 验证依据

- `WeChatMiniGameContractTests.SolarWxHelperPublicBehaviors_HaveNovaEquivalents` 固定 Solar 公开行为映射。
- `RuntimePublicApi_DoesNotExposeVendorTypes` 递归检查公开类型、接口、基类、泛型和委托边界。
- `StatefulWrappers_AreDisposable` 固定有状态包装器的释放契约。
- 微信专项 EditMode 包内契约与消费端契约共 52 项通过；Unity Console 无编译错误。
- 当前事实文档：微信小游戏包 `ARCHITECTURE.md`、`RUNTIME_API.md`、`SERVER_CONTRACT.md` 和 `CAPABILITY_MATRIX.md`。

## 关联

- SDK Plugin 总体边界：[[ADR-022-sdk-plugin-architecture|ADR-022]]。
- 微信支付订单与服务端验单：[[ADR-086-wechat-minigame-server-authoritative-payment-orders|ADR-086]]。
- 未发布接口不留兼容层：[[PAT-104-no-obsolete-shim-rule|PAT-104]]。
