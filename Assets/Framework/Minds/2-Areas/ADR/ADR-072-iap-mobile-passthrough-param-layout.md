---
id: ADR-072
title: Mobile IAP 平台透传参数编码 uid+tableId+receiptParam（8/8/16 布局）
status: accepted
summary: 平台账号字段 GUID 内定长打包 uid/tableId/receiptParam，uid 与 receiptParam 为字符串槽位，tableId 为数值槽位
category: arch
date: 2026-07-14
aliases:
  - ADR-072-iap-mobile-passthrough-param-layout
keywords:
  - ADR-072
  - MobileStoreParameterCodec
  - ReceiptParam
  - ObfuscatedAccountId
  - AppAccountToken
tags:
  - nova
  - sdk
  - iap
  - mobile
related:
  - "[[ADR-022-sdk-plugin-architecture|ADR-022]]"
  - "[[ADR-062-proto-header-namespace-convention|ADR-062]]"
---

# ADR-072：Mobile IAP 平台透传参数编码 uid+tableId+receiptParam（8/8/16 布局）

## 背景

Mobile 内购把上下文编码进平台账号字段（Android `ObfuscatedAccountId`/`ObfuscatedProfileId`、iOS `AppAccountToken`），随平台票据回传，用于回调 / 补单 / 恢复时精确路由订单。原布局只打包 `uid + tableId`（各 16 hex），且 uid 从不解回。

新增业务诉求：需要一个能随票据往返、跨重启不丢的业务透传参数（`ReceiptParam`）。`CustomData` 只走本地存档，补单 / 恢复场景可能丢失或为 null，无法满足该诉求。平台字段是 128 位 GUID（16 字节），容量固定。

## 决策

- 平台参数 GUID（32 hex = 16 字节）按 **8/8/16** 定长打包三值：
  - `[0,8)` **uid**：8 字符，字符串原文左补 0（支持字母数字，业务约束 ≤8 字符）。
  - `[8,16)` **tableId**：4 字节，数值左补 0（业务约束正数、≤8 位十进制）。
  - `[16,32)` **receiptParam**：16 字符，字符串原文左补 0（支持字母数字，业务约束 ≤16 字符）。
- uid / receiptParam 不要求能解析为 `long`。它们可能包含字母，只校验字符串长度；tableId 仍按数值编码。
- `ReceiptParam` 作为独立字段新增在 `IAPRequest` / `IAPResult`（`string` 类型），与自由格式、仅本地往返的 `CustomData` 分离；`IAPResult.ReceiptParam` 为只读、经构造函数回填。
- 范围 / 长度校验统一在 `MobilePurchaseService.TryValidatePassthroughParams`，由 `PayAsync` 入口调用。tableId 越界、uid 超 8 字符、receiptParam 超 16 字符都直接拒绝支付（`IAPMobileErrorCode.InvalidPassthroughParam`）。`ApplyPurchaseContext` / codec 只做纯编码，不重复校验。
- 完整 uid 仍由服务端另行同步；透传参数内的 uid 只是 8 字符槽位的最佳努力携带，客户端不依赖解码出的 uid。

## 影响

- `ReceiptParam` 在支付 / 补单 / 恢复的 `IAPResult` 中均可带回业务（跨重启不丢），补齐了 `CustomData` 的短板。
- 这是破坏性 on-wire 变更：布局由旧 `uid16+tableId16` 改为 8/8/16，升级前发起、升级后回来的在途单可能解错 tableId；靠 `ResolveTableIdFromTable`（productId 反查）+ 服务端优先 `serverTableId` 兜底，灰度期需关注在途单。
- 字段容量上限为：uid ≤8 字符、tableId ≤8 位十进制、receiptParam ≤16 字符。业务侧如需更长的值，需要另开字段或另走服务端映射。
- 其余 store（ThirdPay / Voucher）不参与本编码；`IAPResult.ReceiptParam` 对它们保持默认 null。
- 实现事实见 `com.solotopia.nova.framework.sdk.iap.mobile/Nova/DOCS/MobileUtils.md` 的 `MobileStoreParameterCodec` 段。
