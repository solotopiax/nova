---
id: PAT-160
title: Mobile IAP 商品拉取与后台任务边界
summary: 商品拉取成功态单向收敛，后台任务只做取消与异常收口
category: module
type: pattern
status: active
date: 2026-08-11
aliases:
  - PAT-160-mobile-iap-product-fetch-background-task-boundary
keywords:
  - PAT-160
  - Mobile IAP
  - 商品拉取
  - ProductFetch
  - RunBackgroundTask
  - 不可用 SKU
  - UniTask<T>
tags: [pattern, sdk, iap, mobile, product-fetch, background-task]
related:
  - "[[ADR-072-iap-mobile-passthrough-param-layout|ADR-072]]"
  - "[[GLO-12-unitask-async-await|GLO-12]]"
  - "[[PAT-116-cs-doc-mirror-sync|PAT-116]]"
  - "[[PAT-134-intermittent-network-fail-diagnosis|PAT-134]]"
---

# PAT-160：Mobile IAP 商品拉取与后台任务边界

## 适用场景

- Mobile IAP 对接 Unity IAP 的商店连接、商品拉取、Restore、已有购买拉取和权益刷新链路。
- 网络抖动、Google Play / App Store 连接慢、商品拉取部分成功或迟到失败回调并存的场景。
- Store 内部需要把补单扫描、权益刷新、验单队列或支付验单桥接挂到后台生命周期入口。
- 拆分 Mobile 内部服务时，需要判断某个对象是 service 还是某个 service 内部协调器。

## 核心做法

1. 初始化只等待商店连接成功，不等待商品信息拉取完成。
2. 商品拉取状态由 `MobileInitService` 内部协调器收口；`MobileProductFetchCoordinator` 放在 `Services/Init`，不是独立 service。
3. 商品整体失败才进入自动重试；重试延迟由 `MobileStoreConfig.ProductFetchRetryDelaysMs` 配置，默认 2s / 5s / 10s。
4. 任一轮收到成功商品，或失败数量小于本轮请求数量时，立即进入成功态并停止后续重试。
5. 成功态是单向收敛态：迟到失败回调不能把状态回退为失败，不能重复触发 Restore / FetchPurchases，也不能重新污染已成功商品。
6. 不可用 SKU 集合只表达“当前 `StoreController` 仍查不到”的商品，不保存 Unity IAP 原始失败列表。成功回调和失败回调都必须按 Controller 当前事实校正。
7. 商品首次进入成功态后，才触发平台 `RestoreTransactions` 与 `FetchPurchases`；这两个平台动作只唤起平台侧订单补全，完整补单仍走统一补单入口串行执行。
8. Store 内部后台动作统一经 `MobileServiceHub.RunBackgroundTask`，接入运行期取消令牌和异常日志收口。
9. 后台入口委托固定为 `Func<CancellationToken, UniTask>`；返回 `UniTask<T>` 的方法必须通过 lambda 或无返回包装方法显式 `await` 并丢弃结果，不能直接作为方法组传入。

## 为什么这样做

Unity IAP 的连接成功、商品拉取成功、商品拉取失败和已有购买回调并不一定按业务期望的严格顺序到达。网络抖动时可能出现“先失败、再成功、再迟到失败”的组合；如果失败回调直接回退初始化结果或无条件写入不可用 SKU，会造成已成功商品被拦截、补单重复触发、权益刷新结果和支付状态交错。

把商品拉取做成“成功态单向收敛 + 失败态有限重试 + 不可用 SKU 按 Controller 事实校正”，可以同时覆盖全失败、部分成功和迟到失败三类场景。后台任务入口只承担取消和异常收口，可以避免补单、Restore 和权益刷新各自裸 `Forget()`，也避免有返回值方法被当成无返回后台任务直接传入导致编译或语义错误。

## 反模式

- 把 `OnProductsFetchFailed` 当作初始化失败，导致商店已连接但商品慢返回时整体 Store 不可用。
- 把 Unity IAP 失败回调原始 SKU 列表直接写入不可用集合，不检查 `StoreController` 当前是否已有商品。
- 成功后迟到失败仍调度重试或重复触发 Restore / FetchPurchases。
- 每次商品回调都直接启动补单扫描，绕过统一补单入口的串行保护。
- 在 `FetchPurchases`、权益刷新或验单桥接里直接裸 `Forget()`，不接入 Store 运行期取消令牌。
- 将返回 `UniTask<IReadOnlyList<IAPResult>>` 的权益刷新方法直接方法组传入 `RunBackgroundTask`。
- 为商品拉取协调器单独建立 `Services/ProductFetch` 目录，破坏 Mobile 当前“Services 目录只放 service”的目录语义。

## 目录和职责边界

`MobileProductFetchCoordinator` 是 Init 内部状态机协作对象，不是 service。它负责商品拉取状态、重试、部分成功判定、迟到失败短路和不可用 SKU 校正；对外生命周期仍由 `MobileInitService` 和 `MobileServiceHub` 管理。

`MobileServiceHub.RunBackgroundTask` 是 Store 内部后台任务统一入口，不是业务调度器。它不决定补单次数、不吞掉业务返回、不派发结果，只负责运行期取消令牌、取消日志和异常日志收口。

## 防复发检查

- 检查商品拉取成功态后是否还存在把状态回退为失败的路径。
- 检查写入不可用 SKU 前是否查询 `StoreController` 当前事实。
- 检查 Restore / FetchPurchases 是否只在商品链路首次进入成功态时触发。
- 检查补单扫描是否仍由统一补单入口串行执行，扫描中重复触发只标记下一轮补跑。
- 检查 `RunBackgroundTask` 调用点是否都传入 `Func<CancellationToken, UniTask>`，返回 `UniTask<T>` 的方法是否显式包装。
- 检查 Mobile 子包 Docs 与源码同步，符合 [[PAT-116-cs-doc-mirror-sync|PAT-116]]。

## 来源与验证依据

- 代码事实：
  - `UPMPackages/com.solotopia.nova.framework.sdk.iap.mobile/Nova/Scripts/Runtime/Services/Init/MobileProductFetchCoordinator.cs`
  - `UPMPackages/com.solotopia.nova.framework.sdk.iap.mobile/Nova/Scripts/Runtime/Services/MobileServiceHub.cs`
  - `UPMPackages/com.solotopia.nova.framework.sdk.iap.mobile/Nova/Scripts/Runtime/Services/Restore/MobileRestoreService.cs`
- 文档事实：
  - `UPMPackages/com.solotopia.nova.framework.sdk.iap.mobile/Nova/DOCS/MobileIAP-Architecture.md`
  - `UPMPackages/com.solotopia.nova.framework.sdk.iap.mobile/Nova/DOCS/MobileStore.md`
  - `UPMPackages/com.solotopia.nova.framework.sdk.iap.mobile/Nova/DOCS/MobileInitService.md`
  - `UPMPackages/com.solotopia.nova.framework.sdk.iap/Nova/Doc/IAPPlugin.md`
- 回归依据：
  - `Assets/Tests/Editor/IAP/MobileProductFetchCoordinatorTests.cs`
  - `Assets/Tests/Editor/IAP/MobileProductFetchRetryConfigTests.cs`
  - `Assets/Tests/Editor/IAP/MobileBackgroundTaskLifecycleTests.cs`
- 相关异步术语：[[GLO-12-unitask-async-await|GLO-12]]。
