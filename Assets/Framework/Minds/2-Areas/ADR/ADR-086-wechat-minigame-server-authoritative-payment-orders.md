---
id: ADR-086
title: 微信小游戏虚拟支付采用客户端订单生命周期与服务端验单
status: accepted
date: 2026-09-22
summary: 客户端持久化订单并发货，服务端负责签名验单
category: arch
aliases:
  - wechat-minigame-client-order-lifecycle
  - 微信小游戏客户端订单生命周期
keywords:
  - ADR-086
  - 微信小游戏
  - 虚拟支付
  - outTradeNo
  - 本地未完成订单
  - 幂等发货
  - 服务端验单
tags:
  - nova
  - sdk
  - wechat
  - payment
related:
  - "[[ADR-022-sdk-plugin-architecture|ADR-022]]"
  - "[[ADR-077-mobile-iap-order-key-tableid-receiptparam|ADR-077]]"
  - "[[ADR-087-wechat-minigame-plugin-facade-and-capability-baseline|ADR-087]]"
---

# ADR-086：微信小游戏虚拟支付采用客户端订单生命周期与服务端验单

## 背景

微信小游戏同时提供游戏币支付与道具直购。项目希望 Nova 固定客户端订单、持久化、补单和发货接口，避免各项目重复实现；服务端团队只承担必须依赖服务端秘密或微信可信查询的校验职责。Solar 已有客户端订单与补单实践，但其回调网络、散落存储键和平台分支不适合作为 Nova 公共契约直接复制。

平台拉起成功或失败只描述客户端交互结果，不能作为到账或发货依据。客户端本地方案也无法天然抵抗卸载、清缓存、存档损坏和篡改，且业务资产写入与订单删除之间没有跨存储事务。

## 决策

- 客户端生成满足微信约束的最终 `outTradeNo`，在调用微信前将完整订单按业务 UID 写入 Nova Persist；本地只保留未完成订单。
- Nova 统一管理 `Created -> AwaitingSignature -> ReadyForPayment -> PaymentSubmitted -> PendingValidation -> ReadyToDeliver -> Delivering` 状态，项目不得另建一套微信订单状态机。
- 游戏币和道具支付复用同一编排。道具 `signData` 由客户端按固定契约创建，但 `paySig/signature` 必须由服务端校验后使用私密密钥生成。
- 平台 `success/fail/cancel/timeout` 不决定支付终态。除明确未提交且不支持支付外，客户端保留订单并调用单笔服务端验单。
- 服务端不创建、不保存 Nova 客户端订单，也不维护客户端发货状态；它负责登录 code 校验、道具签名、向微信验单、当前用户全量订单只读查询、订阅结果登记和文本安全检查。
- 服务端必须把客户端提交的 UID、商品、数量、单价、环境、OfferId、ZoneId 和 Payload 视为不可信输入，结合认证会话、服务端商品配置和微信查询结果返回 `Status/CanDeliver`。
- 只有 `CanDeliver=true` 时，Nova 才调用项目的 `IWeChatMiniGameFulfillmentHandler`。项目业务存档必须以 `OrderId` 幂等：同一订单重复调用时不得重复增加资产，并应返回成功。
- 发货成功，或服务端确认 `Closed/Refunded/Failed` 后删除本地订单；网络失败、待定状态和发货失败保留，并在 UID 与发货器就绪后逐笔补单。
- `Payload` 只允许小型、非敏感透传数据。AppSecret、session_key、access_token、Midas 密钥和私钥不得进入客户端配置、订单、响应或日志。
- Android 与 iOS 共享流程；iOS 不使用客服会话充值，只允许现网并以运行时 `allow_pay` 为准。
- 支付公开入口固定为 `PurchaseAndVerifyAsync`、`PurchaseAsync`、`VerifyPaymentOrderAsync`、`VerifyAllPaymentOrdersAsync`、`QueryCurrentUserPaymentOrdersAsync`。前三类分别表达完整支付、纯拉起、单笔验单；后两类分别表达本地未完成订单补单和服务端全订单只读查询，不得混为同一行为。
- 游戏币支付由框架在 `RequestMidasPaymentOption` 固定写入 `mode = "game"`；道具直购的签名原文固定写入 `mode = "goods"`，并调用独立的 `RequestMidasPaymentGameItem`。业务只选择 `WeChatMiniGamePaymentKind`，不直接拼微信支付模式。
- 本包尚未发布，旧命名直接删除并同步调用方，不保留 `[Obsolete]` 或转发兼容层。

## 后果

### 正面

- Nova 对项目组提供完整、固定的客户端支付调用面，不再要求每个项目自行拼接订单存储和补单流程。
- 客户端崩溃、网络中断或验单响应丢失后，可从本地未完成订单继续逐笔验单和发货。
- 服务端接口收敛为六项明确职责，客户端不持有 AppSecret、access token 或支付签名密钥。
- 游戏币与道具、Android 与 iOS 共享同一订单状态机和错误语义。

### 代价

- 清缓存、卸载、存档损坏或篡改会破坏本地恢复依据；客户端订单不能成为可信财务凭证。
- “业务资产已经写入、订单尚未删除”之间可能退出，因此业务发货器必须将订单去重与资产变更作为同一业务存档操作。
- 跨设备恢复不由当前客户端本地订单方案保证；服务端仍需保留微信支付审计、退款和对账能力。
- 真正支付正确性仍需正式 AppID、MidasOfferId、业务服务端及 Android/iOS 真机联合验收。

## 被排除方案

| 方案 | 否决理由 |
|---|---|
| 完全照搬 Solar `WXHelper` | 回调网络、散落存储键、平台条件和业务表耦合不适合作为 Nova 公共 API |
| 服务端创建并持久化全部订单 | 与本次确定的服务端职责边界不符，也让项目仍依赖服务端实现整套订单生命周期 |
| 以平台 `success` 回调直接发货 | 客户端结果不是微信服务端支付状态，无法覆盖迟到回调、超时和进程中断 |
| 发货后直接删除且业务不按订单去重 | 在资产写入与删除订单之间退出会导致补单时重复发货 |
| 客户端保存 AppSecret 或 Midas 密钥 | 客户端无法安全保密，必须仅存在于服务端 |
| 合并批量本地补单与服务端全订单查询 | 两者输入、是否触发发货和失败语义不同，合并会隐藏安全边界 |

## 验证依据

- Runtime：`WeChatMiniGamePaymentModels`、`WeChatMiniGamePaymentOrderRepository`、`WeChatMiniGameBackend`、`WeChatMiniGameCommerce`、`WeChatMiniGamePlugin.Payment`。
- 协议与配置：六项 `PbNetWeChat*` 协议、六项 NetCmd、`WeChatMiniGamePluginConfig` 与 ConfigRuntime。
- Demo：`WechatMiniGameDemo` 覆盖游戏币、道具、本地订单读取、单笔验单、全部补单和项目发货器接入点。
- Docs：微信小游戏包 `RUNTIME_API.md`、`SERVER_CONTRACT.md`、`CAPABILITY_MATRIX.md`。

## 关联

- SDK 独立 UPM 与纯 C# Plugin 边界：[[ADR-022-sdk-plugin-architecture|ADR-022]]。
- Mobile IAP 本地未完成订单和稳定订单键先例：[[ADR-077-mobile-iap-order-key-tableid-receiptparam|ADR-077]]。
- 微信能力统一门面与 Solar 行为覆盖基线：[[ADR-087-wechat-minigame-plugin-facade-and-capability-baseline|ADR-087]]。
