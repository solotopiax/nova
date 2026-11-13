# Nova Framework - SDK - IAP - Voucher

> 包名：`com.solotopia.nova.framework.sdk.iap.voucher`
> 当前版本：`0.0.1`

为 Nova IAP 提供服务端钱包、不可变报价和可恢复幂等扣减能力。客户端只提交 capability 生成的 Ready 报价，不直接接触券唯一码、protobuf 或交易 journal。

测试环境可通过独立 `IIAPVoucherTestCapable` 调用 `ThirdGiftVoucherTestGrant` 发放礼券和赠币；成功响应会更新当前账号钱包。该接口不自动重试，结果未知时应先刷新钱包对账。

## 安装

在 Unity 中打开 `Nova/Open PlugPals`，从外网仓库安装 `com.solotopia.nova.framework.sdk.iap.voucher`；默认外网入口为 [upm.solotopiax.com](https://upm.solotopiax.com)，实际以窗口中保存的地址为准。本包声明依赖 Nova Framework `0.7.3`、IAP `0.2.6` 和 `com.solotopia.unitask` `10.0.6`，不要求安装 Mobile、ThirdPay、UniWebView 或 Unity Purchasing。下方是已配置对应 registry 后的依赖片段。

```json
{
  "dependencies": {
    "com.solotopia.nova.framework.sdk.iap.voucher": "0.0.1"
  }
}
```

安装后还须登记并启用 `VoucherStoreConfig`、配置实际商品和钱包/扣减路由、保存 ConfigMaster 并正式导出当前平台 Runtime。登录后自动开始一次钱包刷新与本地订单恢复；业务在钱包就绪后报价、支付。完整准备及验收见 [VoucherStore.md](./Nova/Doc/VoucherStore.md#接入与验收顺序)。

## 交易与履约边界

- 客户端在发送前生成唯一 `game_order_id`，并先调用存档保存完整扣减 payload；跨进程恢复依赖当前账号的 PersistManager 确实可用并成功保存。
- 网络失败、取消或结果未知时保留原订单；所有恢复发送复用相同订单号与 payload。
- 服务端必须按 `app_id + user_id + game_order_id` 幂等，并以服务端钱包余额为权威；客户端实现不能单独证明后端已满足该契约。
- 本地终态事件可能因崩溃恢复重复派发，消费方必须按 `IAPResult.OrderId` 去重。
- Voucher 资产必须精确覆盖商品价格；余额不足时不做现金补差。

使用方式和配置见 [Nova/Doc/INDEX.md](./Nova/Doc/INDEX.md)，变更记录见 [CHANGELOG.md](./CHANGELOG.md)。

## 包边界

- [Nova/Scripts/Runtime/Data](Nova/Scripts/Runtime/Data/) 保存公开只读模型和内部持久化数据。
- [Nova/Scripts/Runtime/Services](Nova/Scripts/Runtime/Services/) 按网络、报价、交易和钱包职责拆分实现。
- [Nova/Scripts/Runtime/Protos](Nova/Scripts/Runtime/Protos/) 只保存 protoc 生成代码，不手工修改。
- Runtime 程序集仅依赖 Nova Runtime、IAP Runtime 和 UniTask，不向 Editor/Test 开放友元程序集。
- Editor 测试只验证 public API、只读模型和程序集导出边界，不直接访问内部交易状态机。

## 跨包文档定位

本页链接相对于当前文件；独立包之间不假定相邻安装。先用 `UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()` 按下表精确包名查找，以该包的 `resolvedPath` 为根读取文档入口，再按包内相对链接进入目标。无 Editor 通道时使用当前有效 manifest 或解析工具给出的实际包位置。

| 包名 | 包内文档入口 | 本文引用的包内位置 |
|---|---|---|
| `com.solotopia.nova.framework.sdk.iap` | `Nova/Doc/INDEX.md` | `Nova/Doc/IAP-Architecture.md#支付打点三维属性` |
