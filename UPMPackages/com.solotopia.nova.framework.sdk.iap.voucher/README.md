# Nova Framework - SDK - IAP - Voucher

> 包名：`com.solotopia.nova.framework.sdk.iap.voucher`
> 当前版本：`0.0.1`

为 Nova IAP 提供服务端钱包、不可变报价和可恢复幂等扣减能力。客户端只提交 capability 生成的 Ready 报价，不直接接触券唯一码、protobuf 或交易 journal。

测试环境可通过独立 `IIAPVoucherTestCapable` 调用 `ThirdGiftVoucherTestGrant` 发放礼券和赠币；成功响应会更新当前账号钱包。该接口不自动重试，结果未知时应先刷新钱包对账。

## 安装

```json
"dependencies": {
  "com.solotopia.nova.framework.sdk.iap.voucher": "0.0.1"
}
```

## 核心保证

- 客户端在发送前生成并持久化唯一 `game_order_id` 和完整扣减 payload。
- 网络失败、取消或结果未知时保留原订单；所有恢复发送复用相同订单号与 payload。
- 服务端按 `app_id + user_id + game_order_id` 幂等，并以服务端钱包余额为权威。
- 本地终态事件可能因崩溃恢复重复派发，消费方必须按 `IAPResult.OrderId` 去重。
- Voucher 资产必须精确覆盖商品价格；余额不足时不做现金补差。

使用方式和配置见 [Nova/Doc/INDEX.md](./Nova/Doc/INDEX.md)，变更记录见 [CHANGELOG.md](./CHANGELOG.md)。

## 包边界

- `Nova/Scripts/Runtime/Data` 保存公开只读模型和内部持久化数据。
- `Nova/Scripts/Runtime/Services` 按网络、报价、交易和钱包职责拆分实现。
- `Nova/Scripts/Runtime/Protos` 只保存 protoc 生成代码，不手工修改。
- Runtime 程序集仅依赖 Nova Runtime、IAP Runtime 和 UniTask，不向 Editor/Test 开放友元程序集。
- Editor 测试只验证 public API、只读模型和程序集导出边界，不直接访问内部交易状态机。
