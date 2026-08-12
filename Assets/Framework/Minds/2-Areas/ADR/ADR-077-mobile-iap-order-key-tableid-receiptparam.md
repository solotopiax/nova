---
id: ADR-077
title: Mobile IAP 未完成订单身份采用 tableId+ReceiptParam
status: accepted
summary: Mobile订单键含ReceiptParam
category: arch
date: 2026-08-11
aliases:
  - ADR-077-mobile-iap-order-key-tableid-receiptparam
keywords:
  - ADR-077
  - Mobile IAP
  - OrderRecordsByKey
  - ReceiptParam
  - PaySuccess
  - 补单
  - 订单键
tags:
  - nova
  - sdk
  - iap
  - mobile
related:
  - "[[ADR-022-sdk-plugin-architecture|ADR-022]]"
  - "[[ADR-072-iap-mobile-passthrough-param-layout|ADR-072]]"
  - "[[PAT-160-mobile-iap-product-fetch-background-task-boundary|PAT-160]]"
---

# ADR-077：Mobile IAP 未完成订单身份采用 tableId+ReceiptParam

## 背景

Mobile IAP 旧版本地未完成订单仓库使用 `tableId` 作为唯一键。这个设计在每个业务商品都对应独立支付 SKU 时足够，但无法覆盖“多个业务商品共用少量支付 SKU”的项目。

典型场景是业务存在数千个消费礼包，但平台支付 SKU 只有数百个。此时 `tableId` 只表达商品配置行或支付 SKU，不能单独确定具体业务订单；同一个 `tableId` 下的不同业务礼包需要结合 `ReceiptParam` 才能唯一定位。

如果继续只用 `tableId` 做本地仓库键，会出现以下问题：

- 同 SKU 多笔未完成订单互相覆盖，导致补单时只剩最后一笔。
- 平台迟到成功 / 失败回调、服务端 `QueryPendingOrder` 和本地扫描交错时，可能误删或误判另一笔订单。
- `PaySuccess` 运行期去重按 `tableId` 判断时，会把不同 `ReceiptParam` 的业务订单当成同一笔。
- 服务端验单响应目前主要按 `tableId` 返回结果，同一批次存在重复 `tableId` 时，客户端无法安全把响应归属到具体 `ReceiptParam` 订单。

## 决策

- Mobile IAP 未完成订单身份统一改为 `tableId + ReceiptParam` 组成的订单键。
- 本地存档新增 `OrderRecordsByKey: Dictionary<string, MobileOrderRecord>`，作为唯一写入仓库；旧 `OrderRecords: Dictionary<long, MobileOrderRecord>` 只保留反序列化和迁移用途。
- 旧 `OrderRecords` 迁移时按空 `ReceiptParam` 生成订单键，保持未使用 `ReceiptParam` 的旧项目语义不变。
- `PendingOrder` 内存映射、登录前暂存、验单队列、`AwaitingConfirm` 收尾、平台购买失败清理、`PaySuccess` 去重都按订单键处理。
- `QueryPendingOrder` 合并时优先使用服务端 `table_id` 确定商品行，再从 `parameter` 解码 `ReceiptParam` 参与订单键；解不出 `ReceiptParam` 时按空透传兼容旧协议。
- Restore 准备的订阅 / 非消耗品订单使用空 `ReceiptParam`，避免把平台权益刷新误拆成业务消费订单。
- 当同一验单批次里存在重复 `tableId` 时，客户端拆成单笔验单请求，避免服务端响应仍按 `tableId` 匹配时发生同 SKU 多订单错配。

## 后果

### 正面

- 同一支付 SKU 可承载多条业务订单，补单、迟到回调和本地失败清理不会互相覆盖。
- 未使用 `ReceiptParam` 的项目继续得到旧版 tableId-only 行为，旧存档迁移成本低。
- `PaySuccess` 去重边界从“同一个支付 SKU”收窄到“同一个业务订单”，消费型礼包发货更符合业务预期。
- `OrderRecordsByKey` 明确是未完成订单仓库，不是订单历史；正常支付完成后仍会删除，避免长期堆积。

### 代价

- 开发者若希望同一 SKU 区分多个业务商品，必须稳定传入 `ReceiptParam`，且遵循 ADR-072 的平台透传参数槽位约束：空值或 1-16 位十六进制，非空值不能以 `0` 开头。
- 服务端验单响应仍按 `tableId` 匹配时，重复 `tableId` 批次需要拆单，牺牲部分批量效率换取归属正确性。
- 平台回调必须尽量保留 ADR-072 的透传 UUID；缺少透传参数的历史单只能按空 `ReceiptParam` 兼容，无法凭空还原业务订单身份。

## 被排除方案

| 方案 | 否决理由 |
|---|---|
| 继续只用 `tableId` 作为仓库键 | 无法支持同 SKU 多业务商品，会覆盖未完成订单 |
| 改用平台 `TransactionId` 或 Google `purchase token` 做业务键 | 这些字段由平台生成，发起支付前不可用，且 Google `TransactionId` 不持久化，不能解决 Purchasing 占位和服务端未完成订单合并 |
| 把 `CustomData` 纳入订单键 | `CustomData` 只保证本地往返，补单 / 恢复 / 服务端 QueryPendingOrder 场景可能为空，不适合作为跨重启业务身份 |
| 要求服务端响应也按 `ReceiptParam` 精确返回后再改客户端 | 客户端本地仓库覆盖问题已经存在；先按订单键修正客户端状态机，并对重复 `tableId` 批次拆单，可以在不等待协议升级的前提下降低错配风险 |

## 验证依据

- Runtime：`MobileOrderKey`、`MobileStorePersistData.OrderRecordsByKey`、`MobileValidationService`、`MobilePurchaseService`。
- Docs：`com.solotopia.nova.framework.sdk.iap.mobile/Nova/DOCS/MobileStore.md`、`MobileIAP-Architecture.md`、`MobileUtils.md`。
- 测试：`MobileOrderKeyPersistDataTests` 覆盖订单键生成、旧存档迁移和同 `tableId` 多 `ReceiptParam` 共存；`MobileValidationInternalSplitTests` 覆盖重复 `tableId` 验单批次拆单。

## 关联

- 平台透传参数布局：[[ADR-072-iap-mobile-passthrough-param-layout|ADR-072]]
- SDK 插件架构：[[ADR-022-sdk-plugin-architecture|ADR-022]]
- 商品拉取后台任务边界：[[PAT-160-mobile-iap-product-fetch-background-task-boundary|PAT-160]]
