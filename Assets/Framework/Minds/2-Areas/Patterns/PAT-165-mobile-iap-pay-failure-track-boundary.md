---
id: PAT-165
title: Mobile IAP 支付失败打点边界
summary: PayAsync统一失败打点，无返回链路的回调兜底
category: module
type: pattern
status: active
date: 2026-08-17
source: cur-session
aliases:
  - PAT-165-mobile-iap-pay-failure-track-boundary
keywords:
  - PAT-165
  - Mobile IAP
  - 支付失败打点
  - nova_iap_local_pay_fail
  - PayAsync
  - PayGuardAsync
  - OnPurchaseFailed
  - OnPurchaseConfirmed
tags: [pattern, sdk, iap, mobile, track]
related:
  - "[[ADR-072-iap-mobile-passthrough-param-layout|ADR-072]]"
  - "[[ADR-077-mobile-iap-order-key-tableid-receiptparam|ADR-077]]"
  - "[[PAT-116-cs-doc-mirror-sync|PAT-116]]"
  - "[[PAT-160-mobile-iap-product-fetch-background-task-boundary|PAT-160]]"
---

# PAT-165：Mobile IAP 支付失败打点边界

## 适用场景

- 维护 `com.solotopia.nova.framework.sdk.iap` 与 `com.solotopia.nova.framework.sdk.iap.mobile` 的支付失败链路。
- 排查 `nova_iap_local_pay_fail`、`PayAsync` 返回失败、`PayGuardAsync` 前置失败、Unity IAP 官方购买失败回调或确认失败回调。
- 调整 `IAPResult.ErrorSource` / `ErrorCode` 到 `IAPMobileErrorCode` 的映射。
- 判断某个失败入口应由 `PayAsync` 返回边界上报，还是由无返回链路的回调兜底。

## 核心做法

1. `MobileStore.PayAsync` 必须接住 `PayGuardAsync` 或移动支付核心流程返回的 `IAPResult`，在返回调用方前调用 `TrackReturnedPayFailureInternal(result)`。
2. `TrackReturnedPayFailureInternal` 只过滤 `null` 和成功结果；只要 `IAPResult.IsSuccess == false`，就必须上报 `nova_iap_local_pay_fail`。
3. `PayGuardAsync` 产生的禁用、未初始化、重入、商品缺失等失败结果，在 Mobile 中不由基类直接打点；`ShouldTrackPayGuardFailure` 固定返回 `false`，让返回边界统一映射为 `IAPMobileErrorCode` 后上报。
4. 当失败来源是 `IAPErrorSource.PluginRouter` 时，必须把 `IAPPluginErrorCode` 映射到 Mobile 错误码域，并在 `nova_reason_detail` 保留原始 `ErrorSource:ErrorCode` 和可读描述，避免把不同枚举域的相同整数误读为同一语义。
5. `OnPurchaseFailed` / `HandlePurchaseFailed` 必须把 `PurchaseFailureReason` 与 `FailedOrder.Details` 写入失败 `IAPResult`。存在活跃 `PayAsync` 等待方时，回调不得直接上报，由返回边界统一产生一条 `nova_iap_local_pay_fail`。
6. 没有 `PayAsync` 返回链路但仍关联有效本地订单的迟到 `OnPurchaseFailed`，允许在回调内兜底上报一次；`OnPurchaseConfirmed(FailedOrder)` 是独立的平台确认失败通知，仍直接上报。
7. 不得为这条链路引入运行期失败 key 或 callback-to-return 事后去重状态。支付成功订单仍可按平台交易号独立去重，这与失败打点无关。

## 为什么这样做

一次主动支付失败应对应一个业务终态事件。Unity IAP 的官方回调是构造失败结果的输入，`PayAsync` 返回边界才是活跃支付统一完成和上报的位置；回调提供的 `PurchaseFailureReason` 与 `FailedOrder.Details` 必须随 `IAPResult` 传到该边界，不能为了去重而丢失详情。

只有平台迟到回调已经没有对应 `PayAsync` 等待方、但仍能关联有效本地订单时，才需要在回调内兜底上报。这样既避免用户取消一次出现两条相同事件，也不会漏掉没有返回消费者的有效失败。

## 反模式

- `PayAsync` 直接 `return PayGuardAsync(...)`，导致返回边界没有机会统一补齐失败打点。
- `ShouldTrackPayGuardFailure` 返回 `true` 后又在 Mobile 返回边界打点，导致 guard 失败先按 `IAPPluginErrorCode` 整数上报，再按 `IAPMobileErrorCode` 上报，污染 `nova_reason` 枚举域。
- 活跃 `OnPurchaseFailed` 回调与 `PayAsync` 返回边界分别上报，导致一次用户取消产生两条相同事件。
- 用 `receiptParam`、`customData`、`tableId`、`ErrorCode` 等字段拼接失败去重 key，掩盖两个出口并存的问题。
- `TrackLocalPayFailInternal` 同时承载打点和去重登记副作用，导致调用方难以判断某次失败是否真的上传。
- 只打 `RaisePayFailed` 或业务事件，不补齐 `nova_iap_local_pay_fail`，使本地支付失败原因无法在打点后台定位。

## 防复发检查

- 检查 `MobileStore.PayAsync` 中 `TrackReturnedPayFailureInternal(result)` 位于 `PayGuardAsync` 之后、`return result` 之前。
- 检查 `TrackReturnedPayFailureInternal` 未调用任何 `TryConsume*`、`Mark*Failure*` 或失败 key 去重方法。
- 检查 `HandlePurchaseFailed` 仅在 `payTcs == null`、即不存在 `PayAsync` 返回链路时调用 `TrackLocalPayFailInternal`。
- 检查 `HandleConfirmFailed` 仍直接调用 `TrackLocalPayFailInternal`，并与普通购买失败回调分开处理。
- 检查 `TrackLocalPayFailInternal` 在 `MobileStore.Track.cs` 构造 Mobile 失败字段并直接发送事件，不登记运行期失败去重状态。
- 检查源码契约测试覆盖“活跃回调不直接打点”“返回边界统一上报”和“无返回链路回调兜底”。
- 检查 `nova_reason_detail` 对非 Mobile 错误源保留原始 `ErrorSource:ErrorCode`。

## 来源与验证依据

- 代码事实：
  - `UPMPackages/com.solotopia.nova.framework.sdk.iap.mobile/Nova/Scripts/Runtime/MobileStore.cs`
  - `UPMPackages/com.solotopia.nova.framework.sdk.iap.mobile/Nova/Scripts/Runtime/MobileStore.Track.cs`
  - `UPMPackages/com.solotopia.nova.framework.sdk.iap.mobile/Nova/Scripts/Runtime/Services/Purchase/MobilePurchaseService.Methods.cs`
  - `UPMPackages/com.solotopia.nova.framework.sdk.iap/Nova/Scripts/Runtime/Internal/IAPStoreBase.cs`
  - `UPMPackages/com.solotopia.nova.framework.sdk.iap/Nova/Scripts/Runtime/Internal/IAPStoreBase.Track.cs`
- 文档事实：
  - `UPMPackages/com.solotopia.nova.framework.sdk.iap.mobile/Nova/DOCS/MobileStore.md`
  - `UPMPackages/com.solotopia.nova.framework.sdk.iap/Nova/Doc/IAPPlugin.md`
- 回归依据：
  - `Assets/Tests/Editor/IAP/IAPPayFailureTrackContractTests.cs`
- 关联约束：
  - 平台透传参数布局：[[ADR-072-iap-mobile-passthrough-param-layout|ADR-072]]
  - Mobile 订单键：[[ADR-077-mobile-iap-order-key-tableid-receiptparam|ADR-077]]
  - Docs 同步：[[PAT-116-cs-doc-mirror-sync|PAT-116]]
  - 商品拉取与后台任务边界：[[PAT-160-mobile-iap-product-fetch-background-task-boundary|PAT-160]]
