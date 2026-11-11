# Nova Framework - SDK - IAP - Mobile

> 包名：`com.solotopia.nova.framework.sdk.iap.mobile`
> 当前版本：`0.2.2`

Google Play + iOS App Store 官方内购 Store，实现 Unity IAP 5.x 初始化、购买、Restore、服务端验单、补单、订阅到期与非消耗品持有状态。

## 安装

已接入 Nova Framework 的项目可打开 `Nova → Open PlugPals → 外网仓库`，搜索并安装 `com.solotopia.nova.framework.sdk.iap.mobile`。手动配置 UPM 时使用 `https://upm.solotopiax.com`，scope 为 `com.solotopia`：

```json
"dependencies": {
  "com.solotopia.nova.framework.sdk.iap.mobile": "0.2.2"
}
```

## 依赖

本包支持 Unity `6000.4.x`，最低 `6000.4.2f1`。以下为当前 `package.json` 声明的依赖版本，由 UPM 解析，不代表项目当前已解析的版本：

- `com.solotopia.nova.framework`: `0.7.3`
- `com.solotopia.nova.framework.sdk.iap`: `0.2.2`
- `com.solotopia.unitask`: `10.0.6`
- `com.unity.purchasing`: `5.4.4`

## 当前公开入口

- `MobileStore`：通过父包 `IAPPlugin` 反射发现，不直接手动 new。
- `IAPMobileRequest`：Mobile 渠道支付请求；`ReceiptParam` 必须为 1-16 位十六进制且不能以 `0` 开头，空值表示不透传。编码保留 UID / `ReceiptParam` 原字母大小写；订单匹配仍把 `ReceiptParam` 转大写，iOS UUID 往返不保证保留原大小写。
- `MobileStoreConfig`：Google / Apple 查单与验单 NetCmd 名配置。
- `IIAPMobileQueryCapable`：平台商品信息查询能力。
- `IIAPMobileSubscriptionCapable`：订阅到期与非消耗品持有查询能力。
- `MobileIapNetService`：移动内购查单、批量验单协议封装。

## 接入顺序

在 ConfigMaster 启用 IAP、添加 `MobileStoreConfig` 并配置平台商品 `ProductID`；将所用平台的查单、普通验单及订阅验单命令配置到 Network 的 NetCmd/Host，随后正式导出当前坐标的 `ConfigRuntimeSO`。商店后台商品、测试账号、支付资格与服务端验单支持需由项目实际准备，包安装不会创建它们。

后端登录成功时调用 `Nova.SDK.Login(uid, userProperties)`，框架会在 IAP 插件就绪后补交 UID；再按业务时机调用统一补单入口。真实购买还要求非空 UID 为 1-8 位十六进制且不以 `0` 开头，`TableId` 不超过 `99,999,999`。完整配置、事件及恢复要求见 [MobileStore.md](./Nova/DOCS/MobileStore.md#接入与验收顺序)。

消耗品补单通过 `PaySuccess` 通知，订阅与非消耗品恢复分别通过 `SubscriptionRestored` / `NonConsumeRestored` 通知。三种事件、支付返回值与恢复返回列表共用业务幂等处理；发货需检查 `IsSuccess && CanDeliver`。Editor 模拟成功、商店连接成功或商品拉取成功均不代表真机购买、服务端验单及实际发货已通过。

## 商品拉取与重试

- Unity IAP 商店连接成功即认为 Mobile Store 初始化完成；商品信息在后台拉取，不阻塞初始化结果。
- 商品整体拉取失败后会按 `2s / 5s / 10s` 最多自动重试 3 次。
- 只要任一轮收到成功商品，或失败数量小于本轮请求数量，就认为商品信息链路已完成并停止重试。
- 成功回调会清理旧失败 SKU，并按 `StoreController` 当前状态恢复仍缺失的 pending SKU。
- 失败回调只把 `StoreController` 当前仍查不到的 SKU 标记为不可用；迟到失败不会污染已成功商品。
- 商品成功后只会自动触发启动期 `FetchPurchases` 和延迟权益刷新；订阅倒计时到期会再次 `FetchPurchases` 刷新平台已有购买与票据缓存，再执行权益刷新；`RestoreTransactions` 仅在用户主动恢复购买时调用，避免 iOS 进游戏或后台刷新时弹出 Apple ID 验证框。

## 文档

- [Nova/DOCS/INDEX.md](./Nova/DOCS/INDEX.md)
- [Nova/DOCS/MobileStore.md](./Nova/DOCS/MobileStore.md)
- [Nova/DOCS/MobileIAP-Architecture.md](./Nova/DOCS/MobileIAP-Architecture.md)
- [Nova/DOCS/MobileInitService.md](./Nova/DOCS/MobileInitService.md)
- [Nova/DOCS/MobileUtils.md](./Nova/DOCS/MobileUtils.md)

## 维护

变更记录见 [CHANGELOG.md](./CHANGELOG.md)。每次发版必须在 CHANGELOG 中追加对应版本节，否则发布脚本会中断。
