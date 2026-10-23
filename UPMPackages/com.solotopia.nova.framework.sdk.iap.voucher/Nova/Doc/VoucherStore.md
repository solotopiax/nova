# VoucherStore

## 1. 调用流程

Voucher 支付固定经过以下流程：

1. 登录事件通过 `IAPPlugin.SetUserId` 切换账号；公开钱包立即变为 NotReady。
2. `RefreshWalletAsync` 从服务端取得当前账号权威余额；同一账号 generation 内重叠调用共享同一次网络加载。
3. `Quote(tableId, priceMills)` 计算精确覆盖价格的不可变组合。
4. 业务层只在 `quote.Status == Ready` 时创建 `IAPVoucherRequest`。
5. `IAPPlugin.PayAsync` 生成唯一订单号，先写完整命令 journal，再发送扣减。

钱包刷新尚未完成时，同账号的多个调用者可以同时等待共享结果。一次刷新完成后，下一次 `RefreshWalletAsync` 会重新发起网络加载。调用方取消令牌只取消自己的等待，不取消其他调用者共享的账号刷新；切换账号或释放会话会取消旧账号作用域，旧账号迟到响应仍不能发布到当前钱包。

这里的 generation 保护只约束钱包发布：当前交易协调器在扣减成功后没有消费 `TryPublish` 的布尔结果，旧账号迟到成功仍可能继续保存并派发携带原订单的 `CanDeliver=true` 结果。业务发货须同时核对订单所属账号并按订单号幂等，不能把钱包没有更新当作业务结果不会到达。当前 `SetUserId` 对空 UID 直接返回，不调用 `SwitchAccount`；仅传空 UID 不会主动失效旧钱包作用域，退出账号仍需完整的 Store 生命周期清理。

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

价格单位是千分之一货币单位。面额以 InvariantCulture 的 Number 格式解析，必须为正且乘 1000 后为 Int64 范围内整数；解析失败返回 0，不能用四舍五入掩盖多出的精度。报价使用金额状态的动态规划，当前没有状态数、内存、取消或极端数量溢出的统一保护，输入规模须受真实业务商品和钱包约束。它不检查券过期、服务器实时余额或任意外部 DTO 的合法性；Ready 只表示本地快照下找到精确组合。

报价和钱包公开集合复制容器，内部资产元素仍可共享；`VoucherFrozenSpend` 本身不排序或清理非法元素，命令构造和真实报价链才负责具体冻结规则。`IAPVoucherRequest` 构造只要求非空 Ready 报价，实际支付还要核对商品、账号、generation 和钱包版本。每次发布钱包会使旧版本报价失效。

## 4. journal 与恢复

- journal 按 `game_order_id` 索引，保存账号、商品、精确券码、赠币用量、国家、自定义数据和创建时间。
- 网络错误、取消、超时或无法判断服务端是否执行时，记录进入 `PendingRecovery`，不会删除。
- 当前账号存在结果未知订单时，新 Voucher 支付先恢复原订单，不生成第二个订单号。
- 成功或明确拒绝先持久化为待派发终态，事件桥返回后才删除记录。
- 如果进程在事件窗口崩溃，重启后可能再次派发同一 `OrderId`；消费方必须幂等。
- 网关异常或取消可以转成待恢复结果，但 journal 保存与业务派发异常仍继续传播；“返回 Pending”不是所有存储或事件异常的统一兜底，也不表示服务端扣减已撤销。

journal 先修改内存再调用保存，保存异常没有回滚；首次添加失败后内存可能仍留 Prepared，后续恢复可能发送它。派发异常保留待派发记录，删除保存失败也可能在重启后重派。恢复逐单执行，某次保存/派发异常会停止剩余项目；Pending 结果本身不携带已冻结订单号，不能据此另造订单。命令比对包含资产顺序、创建时间、schema 与自定义数据，同订单重建时间或重新报价可能产生冲突。

钱包 scope 不等于网络鉴权：网关在发送时读取当前 Header，没有验证 Header UID 与冻结 command.AccountId 相同；换账号会创建新 Coordinator，旧 Coordinator 的保存回调仍借用 Store 当前上下文。真实项目须以服务端账号归属和稳定订单幂等收口，不能只依赖客户端钱包 generation。释放与换账号的取消也没有逐项 finally，取消回调异常可留下未完成的本地清理。

## 5. 服务端契约

客户端依赖服务端满足以下现有契约：

- 幂等键至少按 `app_id + user_id + game_order_id` 唯一。
- 订单记录和资产扣减在同一服务端事务内提交。
- 同订单、同 payload 重复请求返回原终态或明确“已处理”终态，不重复扣减。
- 同订单、不同 payload 返回幂等冲突，不执行新请求。
- 每次请求都重新校验券归属、状态和赠币余额，客户端钱包不作为权威。

客户端网络发送为 at-least-once；服务端资产效果由上述幂等约束实现 exactly-once effect。

网关只在 HTTP 前后检查取消，令牌没有传入实际网络请求。扣减响应的 Success/Shipped 都可映射为可发货成功；坏钱包映为未知，网络/服务器类错误可待恢复，其余明确拒绝按现有分类处理。测试发放优先解释业务响应体，客户端没有独立 DevelopMode 门禁；服务端测试环境限制仍是必需契约，未知结果先对账。

## 6. 协议生成约束

`PbNetGiftVoucher.cs` 必须由仓库 protoc 使用 `--csharp_opt=internal_access` 生成。生成类型保持程序集内部可见，业务侧只能通过正式或测试 capability 及其领域模型使用 Voucher 能力。
