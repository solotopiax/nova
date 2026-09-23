---
id: PAT-167
title: ThirdPay 支付页终态与验单打点分层
summary: 成功 callback 与关闭终态互斥，验单和删单独立记录
category: module
type: pattern
status: active
date: 2026-09-20
source: cur-session
aliases:
  - PAT-167-thirdpay-payment-terminal-and-validation-tracking
keywords:
  - PAT-167
  - ThirdPay
  - pay_callback
  - nova_iap_local_pay_success
  - nova_iap_third_pay_close_order
  - nova_iap_third_pay_order_removed
  - nova_iap_validate_fail
tags: [pattern, sdk, iap, thirdpay, track]
related:
  - "[[PAT-136-symptom-driven-debug-trap|PAT-136]]"
  - "[[PAT-153-third-party-callback-response-materialization|PAT-153]]"
  - "[[PAT-165-mobile-iap-pay-failure-track-boundary|PAT-165]]"
---

# PAT-167：ThirdPay 支付页终态与验单打点分层

## 适用场景

- 维护 ThirdPay 的 WebView、iOS Safe Browsing、Android 外部浏览器或系统浏览器支付链路。
- 调整 `pay_callback`、支付页关闭、返回 App 兜底验单、启动补单和本地删单埋点。
- 判断 `nova_iap_local_pay_success`、`nova_iap_local_pay_fail`、`nova_iap_third_pay_close_order`、验单事件和删单事件之间是否应该重复上报。

## 核心做法

1. 支付页会话只产生一个页面终态：有效成功 `pay_callback` 上报 `nova_iap_local_pay_success`；已建立会话但未收到成功 callback 的关闭、失败、未知状态或异常上报 `nova_iap_third_pay_close_order`。
2. `nova_iap_local_pay_fail` 只覆盖支付会话建立前的失败。页面终态已经确定后，后续验单失败不得再补报该事件。
3. 验单事件独立描述服务端确认过程。每次可重试失败上报 `nova_iap_validate_fail`，达到上限或不可重试时上报 `nova_iap_validate_fail_finish`，成功上报 `nova_iap_validate_success`。
4. Deep Link 使用 Unity `Application.deepLinkActivated` 作为 Android 与 iOS 的统一入口。处理 `pay_callback` 前必须校验订单号；无效参数、错单回调和无活动会话只记录日志，不推进当前会话。
5. 本地订单只有在持久层实际删除成功后才上报 `nova_iap_third_pay_order_removed`，并同时携带删除原因、验单场景、协议码、服务端状态、客户端状态和双订单号。
6. 原因与状态使用独立属性。关闭原因、支付失败原因、建单失败原因、删单原因、客户端订单状态和验单场景不得混写到同一个枚举属性；`nova_reason_detail` 必须与当前事件的专属枚举一一对应。
7. ThirdPay 订单生命周期事件只使用 `nova_client_order_id` 和 `nova_server_order_id`，不发送阶段语义不稳定的 `nova_order_id`；尚未取得服务端订单号时对应字段为空。完整事件属性由 `ThirdPayStore.Track.cs` 构造，父包不持有渠道开关或 ThirdPay 专属事件。其他 Store 保留各自订单号契约。

## 为什么这样做

支付页结果和服务端订单状态属于不同阶段。成功 callback 说明支付平台已经返回成功，应立即留下本地成功证据；验单则负责确认服务端状态、重试和补单。若验单失败再次上报本地支付失败，或关闭与成功同时上报，后台会把一次支付误算成多个相互冲突的终态。

订单号校验用于隔离并发或迟到 Deep Link。删单事件放在实际删除之后，才能区分“决定删除”和“确实删除”，并用协议码与双端状态还原删除依据。ThirdPay 的客户端订单号在造单时产生，服务端订单号在验单响应中确认，两者不能再压缩到同一个会随阶段改变含义的字段。

## 与 Mobile IAP 的边界

`[[PAT-165-mobile-iap-pay-failure-track-boundary|PAT-165]]` 描述 Unity IAP 官方回调与 `PayAsync` 返回边界：活跃支付在返回边界统一上报一次，没有返回链路的迟到回调才兜底上报。ThirdPay 有独立的支付页会话终态，同样要求一次会话只有一个页面终态事件。

## 反模式

- 收到成功 `pay_callback` 后等待返回 App 倒计时才首次验单或打成功事件。
- 页面已经上报成功或关闭终态后，再因验单失败补报 `nova_iap_local_pay_fail`。
- 将关闭原因、删单原因、客户端状态等不同枚举都写入 `nova_reason`，导致相同整数无法判断所属枚举域。
- 收到其他订单的 `pay_callback` 后推进或结束当前支付会话。
- 在调用持久层删除前上报删单事件，或删除未发生时仍上报。
- 为 Android 或 iOS 另造原生 Deep Link 回调链，绕开 Unity 已统一提供的 `Application.deepLinkActivated`。

## 防复发检查

- 成功 `pay_callback` 只进入一次 `nova_iap_local_pay_success`，并立即触发对应场景的验单。
- 页面会话终态使用原子门禁，成功与关闭事件不能同时出现。
- `nova_iap_validate_fail`、`nova_iap_validate_fail_finish`、`nova_iap_validate_success` 和删单事件均携带场景、协议码、服务端状态、客户端状态及客户端/服务端订单号。
- ThirdPay 创建成功、支付页终态、首次验单、常规验单和删单事件均不包含 `nova_order_id`；需要关联订单时只读取双订单号。
- 所有删单入口统一经过“实际删除成功后上报”的收口方法。
- Android Java 层不转发或重复解释支付 Deep Link。

## 来源与验证依据

- 代码事实：
  - `UPMPackages/com.solotopia.nova.framework.sdk.iap.thirdpay/Nova/Scripts/Runtime/ThirdPayStore.ExternalBrowser.cs`
  - `UPMPackages/com.solotopia.nova.framework.sdk.iap.thirdpay/Nova/Scripts/Runtime/ThirdPayStore.Track.cs`
  - `UPMPackages/com.solotopia.nova.framework.sdk.iap.thirdpay/Nova/Scripts/Runtime/Services/Order/ThirdPayOrderValidationService.cs`
  - `UPMPackages/com.solotopia.nova.framework.sdk.iap.thirdpay/Nova/Scripts/Runtime/Data/ThirdPayTrackTypes.cs`
- 当前文档：
  - `UPMPackages/com.solotopia.nova.framework.sdk.iap.thirdpay/Nova/Doc/ThirdPayStore.md`
  - `UPMPackages/com.solotopia.nova.framework.sdk.iap/Nova/Tracks/Tracks.xlsx`
- 回归依据：
  - `Assets/Tests/Editor/IAP/ThirdPayExternalBrowserFlowTests.cs`
  - `Assets/Tests/Editor/IAP/ThirdPayPolicyAndStatusTests.cs`
  - `Assets/Tests/Editor/IAP/ThirdPayWebViewContractTests.cs`
