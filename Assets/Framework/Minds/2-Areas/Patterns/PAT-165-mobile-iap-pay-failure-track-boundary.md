---
id: PAT-165
title: Mobile IAP 支付失败打点边界
summary: PayAsync失败补打，官方回调直接打点
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
- 判断某个失败入口是否应该重复上传，而不是被运行期去重抑制。

## 核心做法

1. `MobileStore.PayAsync` 必须接住 `PayGuardAsync` 或移动支付核心流程返回的 `IAPResult`，在返回调用方前调用 `TrackReturnedPayFailureInternal(result)`。
2. `TrackReturnedPayFailureInternal` 只过滤 `null` 和成功结果；只要 `IAPResult.IsSuccess == false`，就必须上报 `nova_iap_local_pay_fail`。
3. `PayGuardAsync` 产生的禁用、未初始化、重入、商品缺失等失败结果，在 Mobile 中不由基类直接打点；`ShouldTrackPayGuardFailure` 固定返回 `false`，让返回边界统一映射为 `IAPMobileErrorCode` 后上报。
4. 当失败来源是 `IAPErrorSource.PluginRouter` 时，必须把 `IAPPluginErrorCode` 映射到 Mobile 错误码域，并在 `nova_reason_detail` 保留原始 `ErrorSource:ErrorCode` 和可读描述，避免把不同枚举域的相同整数误读为同一语义。
5. Unity IAP 官方失败回调也必须直接上报：
   - `OnPurchaseFailed` / `HandlePurchaseFailed`
   - `OnPurchaseConfirmed(FailedOrder)` / `HandleConfirmFailed`
6. 官方失败回调的直接上报不得抑制 `PayAsync` 返回边界上报。同一次失败流程出现多条 `nova_iap_local_pay_fail` 是允许的，用于完整反映平台回调、业务返回和本地失败链路。
7. 不得为本地支付失败引入运行期去重集合、失败 key 消费或 callback-to-return 去重逻辑。支付成功订单仍可按平台交易号独立去重，这与失败打点无关。

## 为什么这样做

本地支付失败的排查目标不是只统计唯一失败订单，而是还原玩家本机支付流程中每个失败触发点。若平台官方回调已经提示失败，但 `PayAsync` 返回边界又因为去重被抑制，后台只能看到一部分链路；反过来，如果只依赖返回边界，某些官方回调细节也会丢失。

因此 Mobile IAP 将失败打点分成两个互不抑制的来源：

- 官方平台失败回调：尽早记录 Unity IAP 给出的 `PurchaseFailureReason` 和 `FailedOrder.Details`。
- `PayAsync` 返回边界：兜住所有最终返回调用方的失败 `IAPResult`，包括 guard 失败、平台失败结果和本地构造失败。

这会产生重复事件，但重复本身是支付失败流程的一部分。后台需要完整链路时，宁可保留多条同流程失败事件，也不能因为去重丢掉某个失败入口。

## 反模式

- `PayAsync` 直接 `return PayGuardAsync(...)`，导致返回边界没有机会统一补齐失败打点。
- `ShouldTrackPayGuardFailure` 返回 `true` 后又在 Mobile 返回边界打点，导致 guard 失败先按 `IAPPluginErrorCode` 整数上报，再按 `IAPMobileErrorCode` 上报，污染 `nova_reason` 枚举域。
- 在官方失败回调上报后写入 `m_RuntimeHandledLocalPayFailureKeys`，并在返回边界消费该 key 抑制打点。
- 用 `receiptParam`、`customData`、`tableId`、`ErrorCode` 等字段拼接失败去重 key，将同一次支付失败链路压缩成一条事件。
- `TrackLocalPayFailInternal` 同时承载打点和去重登记副作用，导致调用方难以判断某次失败是否真的上传。
- 只打 `RaisePayFailed` 或业务事件，不补齐 `nova_iap_local_pay_fail`，使本地支付失败原因无法在打点后台定位。

## 防复发检查

- 检查 `MobileStore.PayAsync` 中 `TrackReturnedPayFailureInternal(result)` 位于 `PayGuardAsync` 之后、`return result` 之前。
- 检查 `TrackReturnedPayFailureInternal` 未调用任何 `TryConsume*`、`Mark*Failure*` 或失败 key 去重方法。
- 检查 `HandlePurchaseFailed` 与 `HandleConfirmFailed` 仍直接调用 `TrackLocalPayFailInternal`。
- 检查 `TrackLocalPayFailInternal` 仅转发 `TrackLocalPayFail`，不登记运行期失败去重状态。
- 检查源码契约测试覆盖“官方回调直接打点”和“返回边界不去重”。
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
