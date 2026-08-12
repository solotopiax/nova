# VoucherStore

## 1. 调用流程

Voucher 支付固定经过以下流程：

1. 登录事件通过 `IAPPlugin.SetUserId` 切换账号；公开钱包立即变为 NotReady。
2. `RefreshWalletAsync` 从服务端取得当前账号权威余额；同一账号 generation 内重叠调用共享同一次网络加载。
3. `Quote(tableId, priceMills)` 计算精确覆盖价格的不可变组合。
4. 业务层只在 `quote.Status == Ready` 时创建 `IAPVoucherRequest`。
5. `IAPPlugin.PayAsync` 生成唯一订单号，先写完整命令 journal，再发送扣减。

```csharp
if (!sdk.TryGet<IAPPlugin>(out IAPPlugin iap)
    || !iap.TryGetCapability<IIAPVoucherCapable>(out IIAPVoucherCapable voucher))
{
    return;
}

VoucherRefreshResult refresh = await voucher.RefreshWalletAsync(ct);
if (!refresh.IsSuccess)
    return;

VoucherQuote quote = voucher.Quote(tableId, priceMills);
if (quote.Status != VoucherQuoteStatus.Ready)
    return;

var request = new IAPVoucherRequest(quote)
{
    CustomData = "shop_entry",
};
IAPResult result = await iap.PayAsync<IAPResult>(request, ct);
```

## 2. 测试发放

测试环境通过独立的 `IIAPVoucherTestCapable` 发放礼券和赠币，不会把测试入口混入正式 `IIAPVoucherCapable`：

```csharp
if (iap.TryGetCapability<IIAPVoucherTestCapable>(out IIAPVoucherTestCapable testVoucher))
{
    var grantRequest = new VoucherTestGrantRequest(
        new[] { new VoucherGrantLine(1001, 1) },
        new[] { new CoinGrantLine(2001, 10) });
    VoucherTestGrantResult grantResult = await testVoucher.TestGrantAsync(grantRequest, ct);
}
```

`TestGrantAsync` 成功后使用服务端响应原子更新当前账号钱包。该测试协议没有幂等订单号，因此客户端不会自动重试；超时、取消或网络结果未知时应先调用 `RefreshWalletAsync` 对账，再决定是否重新发放。服务端负责拒绝非测试环境请求。

## 3. 报价规则

报价只接受精确覆盖，不允许超付、金额容差或现金补差。存在多个精确解时依次比较：

1. 最大化礼券抵扣金额。
2. 最小化资产件数。
3. 优先使用高面值资产。
4. 同面值按资产 ID 和券码稳定排序。

例如价格为 8、钱包包含一张 6 元券和两枚 4 元赠币时，报价选择两枚 4 元赠币，不会因先取 6 元券而错误返回余额不足。

## 4. journal 与恢复

- journal 按 `game_order_id` 索引，保存账号、商品、精确券码、赠币用量、国家、自定义数据和创建时间。
- 网络错误、取消、超时或无法判断服务端是否执行时，记录进入 `PendingRecovery`，不会删除。
- 当前账号存在结果未知订单时，新 Voucher 支付先恢复原订单，不生成第二个订单号。
- 成功或明确拒绝先持久化为待派发终态，事件桥返回后才删除记录。
- 如果进程在事件窗口崩溃，重启后可能再次派发同一 `OrderId`；消费方必须幂等。

## 5. 服务端契约

客户端依赖服务端满足以下现有契约：

- 幂等键至少按 `app_id + user_id + game_order_id` 唯一。
- 订单记录和资产扣减在同一服务端事务内提交。
- 同订单、同 payload 重复请求返回原终态或明确“已处理”终态，不重复扣减。
- 同订单、不同 payload 返回幂等冲突，不执行新请求。
- 每次请求都重新校验券归属、状态和赠币余额，客户端钱包不作为权威。

客户端网络发送为 at-least-once；服务端资产效果由上述幂等约束实现 exactly-once effect。

## 6. 协议生成约束

`PbNetGiftVoucher.cs` 必须由仓库 protoc 使用 `--csharp_opt=internal_access` 生成。生成类型保持程序集内部可见，业务侧只能通过正式或测试 capability 及其领域模型使用 Voucher 能力。
