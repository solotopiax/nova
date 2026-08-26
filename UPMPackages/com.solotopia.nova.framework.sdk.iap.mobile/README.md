# Nova Framework - SDK - IAP - Mobile

> 包名：`com.solotopia.nova.framework.sdk.iap.mobile`
> 当前版本：`0.1.10`

Google Play + iOS App Store 官方内购 Store，实现 Unity IAP 5.x 初始化、购买、Restore、服务端验单、补单、订阅到期与非消耗品持有状态。

## 安装

通过 Nova 私域 UPM 注册表以 UPM 依赖形式接入：

```json
"dependencies": {
  "com.solotopia.nova.framework.sdk.iap.mobile": "0.1.10"
}
```

## 依赖

- `com.solotopia.nova.framework`: `0.6.10`
- `com.solotopia.nova.framework.sdk.iap`: `0.1.5`
- `com.solotopia.unitask`: `10.0.6`
- `com.unity.purchasing`: `5.3.1`

## 当前公开入口

- `MobileStore`：通过父包 `IAPPlugin` 反射发现，不直接手动 new。
- `IAPMobileRequest`：Mobile 渠道支付请求；`ReceiptParam` 必须为 1-16 位十六进制且不能以 `0` 开头，空值表示不透传，并随平台票据往返。
- `MobileStoreConfig`：Google / Apple 查单与验单 NetCmd 名配置。
- `IIAPMobileQueryCapable`：平台商品信息查询能力。
- `IIAPMobileSubscriptionCapable`：订阅到期与非消耗品持有查询能力。
- `MobileIapNetService`：移动内购查单、批量验单协议封装。

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
