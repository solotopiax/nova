# MobileStore 旧版设计归档

> 本文档保留文件入口用于兼容历史链接。
> 原内容基于 Unity IAP 4.x 和旧 MobileStore 设计，已不作为当前事实层维护。

当前实现已经迁移到 Unity IAP 5.x：

- `StoreController` 由 `MobileExtendedService` 唯一持有。
- Unity IAP 事件通过 `MobileStoreService` 路由。
- 初始化由 `MobileInitService` 驱动：`SetController → RegisterStoreCallbacks → Connect`。
- 商店连接成功即完成初始化，商品信息后台拉取。
- 补单、订阅、非消耗品持有状态统一存放在 `MobileStorePersistData`。
- 服务端验单按 Google / Apple、普通 / 订阅拆分为独立 Cmd。

请使用以下当前文档：

- [MobileIAP-Architecture.md](./MobileIAP-Architecture.md)
- [MobileStore.md](./MobileStore.md)
- [MobileInitService.md](./MobileInitService.md)
- [MobileUtils.md](./MobileUtils.md)
