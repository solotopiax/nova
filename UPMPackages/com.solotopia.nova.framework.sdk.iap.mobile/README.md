# Nova Framework - SDK - IAP - Mobile

> 包名：`com.solotopia.nova.framework.sdk.iap.mobile`
> 当前版本：`0.0.1`

Google Play + iOS App Store 官方内购 Store，实现 Unity IAP 5.x 初始化、购买、Restore、服务端验单、补单、订阅到期与非消耗品持有状态。

## 安装

通过 Nova 私域 UPM 注册表以 UPM 依赖形式接入：

```json
"dependencies": {
  "com.solotopia.nova.framework.sdk.iap.mobile": "0.0.1"
}
```

## 依赖

- `com.solotopia.nova.framework.sdk.iap`: `0.0.1`
- `com.unity.purchasing`: `5.3.1`

## 当前公开入口

- `MobileStore`：通过父包 `IAPPlugin` 反射发现，不直接手动 new。
- `IAPMobileRequest`：Mobile 渠道支付请求。
- `MobileStoreConfig`：Google / Apple 查单与验单 NetCmd 名配置。
- `IIAPMobileQueryCapable`：平台商品信息查询能力。
- `IIAPMobileSubscriptionCapable`：订阅到期与非消耗品持有查询能力。
- `MobileIapNetService`：移动内购查单、批量验单协议封装。

## 文档

- [Nova/DOCS/INDEX.md](./Nova/DOCS/INDEX.md)
- [Nova/DOCS/MobileStore.md](./Nova/DOCS/MobileStore.md)
- [Nova/DOCS/MobileIAP-Architecture.md](./Nova/DOCS/MobileIAP-Architecture.md)
- [Nova/DOCS/MobileInitService.md](./Nova/DOCS/MobileInitService.md)
- [Nova/DOCS/MobileUtils.md](./Nova/DOCS/MobileUtils.md)

## 维护

变更记录见 [CHANGELOG.md](./CHANGELOG.md)。每次发版必须在 CHANGELOG 中追加对应版本节，否则发布脚本会中断。
