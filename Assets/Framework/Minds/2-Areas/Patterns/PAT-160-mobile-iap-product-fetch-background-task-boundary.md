---
id: PAT-160
title: Mobile IAP 商店连接、商品拉取与后台任务边界
summary: 商店连接不阻塞主 Loading，商品拉取成功态单向收敛，后台任务只做取消与异常收口
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
  - 商店连接
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

# PAT-160：Mobile IAP 商店连接、商品拉取与后台任务边界

## 适用场景

- Mobile IAP 对接 Unity IAP 的商店连接、商品拉取、Restore、已有购买拉取和权益刷新链路。
- 网络抖动、Google Play / App Store 连接慢、商品拉取部分成功或迟到失败回调并存的场景。
- Store 内部需要把补单扫描、权益刷新、验单队列或支付验单桥接挂到后台生命周期入口。
- 拆分 Mobile 内部服务时，需要判断某个对象是 service 还是某个 service 内部协调器。

## 核心做法

1. 游戏主初始化不等待商店连接和商品信息拉取；`MobileStore` 通过 `MobileServiceHub.RunBackgroundTask` 启动连接，连接成功前由 `IsStoreReady` 拦截支付。
2. 商品拉取状态由 `MobileInitService` 内部协调器收口；`MobileProductFetchCoordinator` 放在 `Services/Init`，不是独立 service。
3. 商品整体失败才进入自动重试；重试延迟由 `MobileStoreConfig.ProductFetchRetryDelaysMs` 配置，默认 2s / 5s / 10s。
4. 任一轮收到成功商品，或失败数量小于本轮请求数量时，立即进入成功态并停止后续重试。
5. 成功态是单向收敛态：迟到失败回调不能把状态回退为失败，不能重复触发商品成功后置流程，也不能重新污染已成功商品。
6. 不可用 SKU 集合只表达“当前 `StoreController` 仍查不到”的商品，不保存 Unity IAP 原始失败列表。成功回调和失败回调都必须按 Controller 当前事实校正。
7. 商品首次进入成功态后，只自动触发平台 `FetchPurchases` 与延迟权益刷新；订阅倒计时到期也只触发 `FetchPurchases` 与权益刷新；`RestoreTransactions` 只保留给用户主动恢复购买入口，避免 iOS 启动期或后台刷新弹出 Apple ID 验证框。
8. `FetchPurchases` 与 `CheckEntitlement` 可能并发返回；已有购买拉取期间只登记权益刷新请求，成功回调必须先缓存 `ConfirmedOrder` receipt、处理 `PendingOrder`，再汇总权益。权益回调还应从 `Entitlement.Order.Info.Receipt` 就地补充票据，避免 `FullyEntitled` 生成缺少 Google token 或 Apple order id 的恢复记录。
9. Store 内部后台动作统一经 `MobileServiceHub.RunBackgroundTask`，接入运行期取消令牌和异常日志收口。
10. 后台入口委托固定为 `Func<CancellationToken, UniTask>`；返回 `UniTask<T>` 的方法必须通过 lambda 或无返回包装方法显式 `await` 并丢弃结果，不能直接作为方法组传入。
11. `StoreController.Connect()` 的等待必须通过 `AttachExternalCancellation` 接入 Store 运行期取消令牌，避免 Dispose 后连接等待继续悬挂。
12. 登录补单可以先执行不依赖平台连接的服务端查单与本地验单；商店尚未连接或商品尚未就绪时，平台权益刷新必须登记延后请求，并在商品成功态后补跑，不能把提前返回当作完成。
13. `FetchPurchases` 成功后先合并平台票据再触发统一补单；失败时优先消费已登记的权益刷新，否则已登录账号通过统一入口执行服务端、本地和权益兜底。缺少 `PendingOrder` 引用的订单继续等待后续平台拉取。

## 为什么这样做

Unity IAP 的连接成功、商品拉取成功、商品拉取失败和已有购买回调并不一定按业务期望的严格顺序到达。网络抖动时可能出现“先失败、再成功、再迟到失败”的组合；如果失败回调直接回退初始化结果或无条件写入不可用 SKU，会造成已成功商品被拦截、补单重复触发、权益刷新结果和支付状态交错。

`StoreController.Connect()` 依赖平台和网络回调，且自身没有业务超时；把它留在游戏主初始化 await 链会导致回调长期不返回时 Loading 无法结束。将连接移入 Store 运行期后台任务后，游戏可以继续启动，支付仍由 `IsStoreReady` 保证只能在连接成功后发起，Dispose 也能通过运行期取消令牌退出连接等待。

把商品拉取做成“成功态单向收敛 + 失败态有限重试 + 不可用 SKU 按 Controller 事实校正”，可以同时覆盖全失败、部分成功和迟到失败三类场景。启动期和订阅倒计时只做无系统登录弹框的 `FetchPurchases` 和权益刷新，用户主动恢复购买才调用 `RestoreTransactions`，既避免 iOS 无交互弹框，也保留订阅和非消耗品恢复链路。后台任务入口只承担取消和异常收口，可以避免连接、补单、Restore 和权益刷新各自裸 `Forget()`，也避免有返回值方法被当成无返回后台任务直接传入导致编译或语义错误。

## 反模式

- 把 `OnProductsFetchFailed` 当作初始化失败，导致商店已连接但商品慢返回时整体 Store 不可用。
- 把 Unity IAP 失败回调原始 SKU 列表直接写入不可用集合，不检查 `StoreController` 当前是否已有商品。
- 成功后迟到失败仍调度重试或重复触发商品成功后置流程。
- 启动期自动调用 `RestoreTransactions`，导致 iOS 在玩家进游戏时弹出 Apple ID 验证框。
- 每次商品回调都直接启动补单扫描，绕过统一补单入口的串行保护。
- 在 `FetchPurchases`、权益刷新或验单桥接里直接裸 `Forget()`，不接入 Store 运行期取消令牌。
- 在 `MobileStore.InitializeAsync` 中直接等待 `StoreController.Connect()`，让平台连接占用游戏主 Loading。
- 后台等待 `StoreController.Connect()` 时不接入 Store 运行期取消令牌，导致 Dispose 后任务仍悬挂。
- 商店尚未连接时直接丢弃登录补单末尾的权益刷新请求，导致后台连接完成后没有确定的补跑信号。
- `FetchPurchases` 失败回调只打印日志，既不消费已登记权益刷新，也不执行已具备条件的完整补单兜底。
- `FetchPurchases` 尚未回调时先汇总 `CheckEntitlement`，把 `FullyEntitled` 固化成缺少平台验单凭据的本地恢复记录。
- 只依赖 `FetchPurchases` 缓存票据，不读取 `Entitlement.Order.Info.Receipt` 作为权益回调的就地兜底。
- 将返回 `UniTask<IReadOnlyList<IAPResult>>` 的权益刷新方法直接方法组传入 `RunBackgroundTask`。
- 为商品拉取协调器单独建立 `Services/ProductFetch` 目录，破坏 Mobile 当前“Services 目录只放 service”的目录语义。

## 目录和职责边界

`MobileProductFetchCoordinator` 是 Init 内部状态机协作对象，不是 service。它负责商品拉取状态、重试、部分成功判定、迟到失败短路和不可用 SKU 校正；对外生命周期仍由 `MobileInitService` 和 `MobileServiceHub` 管理。

`MobileServiceHub.RunBackgroundTask` 是 Store 内部后台任务统一入口，不是业务调度器。它不决定补单次数、不吞掉业务返回、不派发结果，只负责运行期取消令牌、取消日志和异常日志收口。

## 防复发检查

- 检查商品拉取成功态后是否还存在把状态回退为失败的路径。
- 检查写入不可用 SKU 前是否查询 `StoreController` 当前事实。
- 检查启动期是否只自动触发 `FetchPurchases` 和延迟权益刷新，不调用 `RestoreTransactions`。
- 检查订阅倒计时是否只触发 `FetchPurchases` 和权益刷新，不调用 `RestoreAsync` / `RestoreTransactions`。
- 检查用户主动恢复购买入口是否仍正常调用 `RestoreTransactions`。
- 检查补单扫描是否仍由统一补单入口串行执行，扫描中重复触发只标记下一轮补跑。
- 检查 `RunBackgroundTask` 调用点是否都传入 `Func<CancellationToken, UniTask>`，返回 `UniTask<T>` 的方法是否显式包装。
- 检查商店连接是否由 `RunBackgroundTask` 启动，连接等待是否通过 `AttachExternalCancellation` 响应 Store Dispose。
- 检查登录补单早于商店连接时是否登记延后权益刷新，并在商品成功后仅消费一次。
- 检查 `FetchPurchases` 失败回调是否优先消费已登记权益刷新，并在没有延后请求时通过统一补单入口执行兜底。
- 检查 `FetchPurchases` 回调是否先缓存票据再消费延后权益刷新，并确认 `Entitlement.Order.Info.Receipt` 会在权益汇总前写入票据缓存。
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
