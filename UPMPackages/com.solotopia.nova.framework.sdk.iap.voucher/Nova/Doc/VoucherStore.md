# VoucherStore

Voucher 使用服务端礼券和赠币钱包抵扣商品价格。它不拉起现金支付页，也不支持余额不足时自动切换其他 Store 或现金补差。


支付打点统一包含 `nova_platform`、`nova_channel`、`nova_store_type`：前两项来自商店初始化时的 `PlatformType` / `ChannelType` 枚举名称，商店类型固定为 `Voucher`；三项名称按实例缓存，发送时只复制缓存，重新初始化时刷新。不再把商店类型写入 `nova_channel`。大小写、缺失配置和路由失败边界见 [父包三维属性](../../../com.solotopia.nova.framework.sdk.iap/Nova/Doc/IAP-Architecture.md#支付打点三维属性)。
## 接入与验收顺序

1. 按 [包安装说明](../../README.md#安装) 安装 Voucher 及声明依赖，在 `IAPPluginConfig.StoreConfigs` 添加并启用 `VoucherStoreConfig`，在 `Products` 登记允许支付的 `TableId`。Voucher 请求不使用 `MobileProductID` 或 `ThirdProductID`；礼券档位、券码和赠币余额由服务端钱包提供。
2. 在 Network 配置中登记钱包、扣减及可选测试发放的实际 Host/NetCmd；将命令名写入 `VoucherStoreConfig`。三个名称默认均为空，安装包不会自动补路由或部署后端。
3. 保存 ConfigMaster，正式导出当前平台 Runtime 及修改过的 Network 配置/访问代码，并核对运行时引用。确认 IAP 上下文的 PersistManager 可读写当前账号存档；仅有内存 journal 不足以跨进程恢复。
4. 通过既有 SDK 登录链同步非空 UID。Store 自动发起本地恢复和钱包刷新，初始化完成不等于钱包已就绪；业务仍按刷新结果和 `Wallet.IsReady` 决定是否报价。接收全局结果时核对 `StoreType`，按稳定订单号幂等处理。
5. 在目标服务端验收钱包余额、精确报价、扣减成功/拒绝、网络结果未知后的原单重发、进程重启、账号切换与重复事件；同时验证后端原子扣减和业务发货幂等。Editor 模拟成功或本地静态检查不能证明后端已扣减、真实钱包或发货正常。`EnableAlwaysPaySucceed` 在 Editor 会绕过真实钱包、journal 和网络链，不作为这些验收证据。

## 1. 调用流程

Voucher 支付固定经过以下流程：

1. 登录事件通过 `IAPPlugin.SetUserId` 切换到不同的非空账号；公开钱包立即变为 NotReady，并自动启动本地恢复与钱包刷新。重复设置同一 UID 不重复触发这两项操作。
2. `RefreshWalletAsync` 从服务端取得当前账号权威余额；同一账号 generation 内重叠调用共享同一次网络加载。
3. `Quote(tableId, priceMills)` 计算精确覆盖价格的不可变组合。
4. 业务层只在 `quote.Status == Ready` 时创建 `IAPVoucherRequest`。
5. `IAPPlugin.PayAsync` 生成唯一订单号，先写完整命令 journal，再发送扣减。

钱包刷新尚未完成时，同账号的多个调用者可以同时等待共享结果。一次刷新完成后，下一次 `RefreshWalletAsync` 会重新发起网络加载。调用方取消令牌只取消自己的等待，不取消其他调用者共享的账号刷新；切换账号或释放会话会取消旧账号作用域，旧账号迟到响应仍不能发布到当前钱包。

`RefreshWalletAsync` 的取消通常转换为 `IsSuccess=false、ErrorCode=NetworkError` 返回；不要只捕获取消异常而忽略结果。刷新失败保留当前可见钱包，失败结果中的 `Wallet.IsReady` 仍可能为 `true`，不能据此声称本次联网刷新成功。

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

每条发放的档位/赠币 ID 与数量都要求大于零；两个集合至少有一个有效条目，集合中不能含 `null`。构造失败直接抛参数异常，不返回网络错误；调用 `TestGrantAsync(null)` 同样抛 `ArgumentNullException`。调用方取消返回 `NetworkError`，仅账号作用域取消返回 `StaleAccount`，均不证明服务端未发放。

## 3. 报价规则

报价只接受精确覆盖，不允许超付、金额容差或现金补差。存在多个精确解时依次比较：

1. 最大化礼券抵扣金额。
2. 最小化资产件数。
3. 优先使用高面值资产。
4. 同面值按资产 ID 和券码稳定排序。

例如价格为 8、钱包包含一张 6 元券和两枚 4 元赠币时，报价选择两枚 4 元赠币，不会因先取 6 元券而错误返回余额不足。

价格单位是千分之一货币单位。面额以 InvariantCulture 的 Number 格式解析，必须为正且乘 1000 后为 Int64 范围内整数；解析失败返回 0，不能用四舍五入掩盖多出的精度。报价使用金额状态的动态规划，当前没有状态数、内存、取消或极端数量溢出的统一保护，输入规模须受真实业务商品和钱包约束。它不检查券过期、服务器实时余额或任意外部 DTO 的合法性；Ready 只表示本地快照下找到精确组合。

`priceMills` 由调用方传入，报价引擎不从商品表读取价格，也不核对币种或换汇。调用方须用与钱包面值相同币种的正确商品金额报价；扣减协议不发送该价格，服务端须按 `table_id` 及实际资产重新校验商品应付金额。当前 Store 给交易命令的国家值固定为空，不能假定它自动继承 ThirdPay 国家码。`IAPVoucherRequest` 继承的 `ReceiptParam` 当前不进入 Voucher 命令、journal 或结果回填；业务恢复透传使用已保存的 `CustomData` 和稳定订单号。

报价和钱包公开集合复制容器，内部资产元素仍可共享；`VoucherFrozenSpend` 本身不排序或清理非法元素，命令构造和真实报价链才负责具体冻结规则。`IAPVoucherRequest` 构造只要求非空 Ready 报价，实际支付还要核对商品、账号、generation 和钱包版本。每次发布钱包会使旧版本报价失效。

## 4. journal 与恢复

- journal 按 `game_order_id` 索引，保存账号、商品、精确券码、赠币用量、国家、自定义数据和创建时间。
- 网络错误、取消、超时或无法判断服务端是否执行时，记录进入 `PendingRecovery`，不会删除。
- 当前账号存在结果未知订单时，新 Voucher 支付先恢复原订单，不生成第二个订单号。
- 成功或明确拒绝先持久化为待派发终态，事件桥返回后才删除记录。
- 如果进程在事件窗口崩溃，重启后可能再次派发同一 `OrderId`；消费方必须幂等。
- 网关异常或取消可以转成待恢复结果，但 journal 保存与业务派发异常仍继续传播；“返回 Pending”不是所有存储或事件异常的统一兜底，也不表示服务端扣减已撤销。

journal 先修改内存再调用保存，保存异常没有回滚；首次添加失败后内存可能仍留 Prepared，后续恢复可能发送它。派发异常保留待派发记录，删除保存失败也可能在重启后重派。恢复逐单执行，某次保存/派发异常会停止剩余项目；Pending 结果本身不携带已冻结订单号，不能据此另造订单。命令比对包含资产顺序、创建时间、schema 与自定义数据，同订单重建时间或重新报价可能产生冲突。

存档使用 `iap_voucher / data_{uid}`。基类保存回调在 UID 为空、PersistManager 缺失或数据为空时静默返回，因此 journal 调用保存正常返回不等于已落盘；应在实际运行环境验证重启可读回。当前报价失败或待恢复结果不能用来清理旧 journal，卸载、清缓存或换存档路径也不能当作撤销服务端扣减。

钱包 scope 不等于网络鉴权：网关在发送时读取当前 Header，没有验证 Header UID 与冻结 command.AccountId 相同；换账号会创建新 Coordinator，旧 Coordinator 的保存回调仍借用 Store 当前上下文。真实项目须以服务端账号归属和稳定订单幂等收口，不能只依赖客户端钱包 generation。释放与换账号的取消也没有逐项 finally，取消回调异常可留下未完成的本地清理。

## 5. 服务端契约

协议源为 `Nova/Protos/pb_net_gift_voucher.proto`，生成代码为 `Nova/Scripts/Runtime/Protos/PbNetGiftVoucher.cs`。下列路径是包内协议约定，实际发送地址仍由当前项目 NetCmd 的 Host/Path 决定。

| 配置入口 | 协议约定路径 | 请求/响应作用 |
|---|---|---|
| `GetVoucherListCmdName` | `/game_recharge/gift_voucher/list` | 公共 Header 鉴权，返回礼券分组及赠币余额 |
| `DeductVoucherCmdName` | `/game_recharge/gift_voucher/deduct` | 提交 Header、原 `game_order_id`、`table_id`、精确券码、赠币用量和国家，返回明确状态及扣减后钱包 |
| `TestGrantVoucherCmdName` | `/game_recharge/gift_voucher/test_grant` | 提交测试发放明细，返回执行结果及发放后钱包；无幂等订单号 |

客户端依赖服务端满足以下现有契约：

- 幂等键至少按 `app_id + user_id + game_order_id` 唯一。
- 订单记录和资产扣减在同一服务端事务内提交。
- 同订单、同 payload 重复请求返回原终态或明确“已处理”终态，不重复扣减。
- 同订单、不同 payload 返回幂等冲突，不执行新请求。
- 每次请求都重新校验券归属、状态和赠币余额，客户端钱包不作为权威。

客户端会在后续恢复时重新发送原命令，资产只扣一次依赖上述服务端幂等和事务契约；客户端静态实现不能证明已部署后端满足这些条件。

网关只在 HTTP 前后检查取消，令牌没有传入实际网络请求。扣减响应的 Success/Shipped 都可映射为可发货成功；坏钱包映为未知，网络/服务器类错误可待恢复，其余明确拒绝按现有分类处理。测试发放优先解释业务响应体，客户端没有独立 DevelopMode 门禁；服务端测试环境限制仍是必需契约，未知结果先对账。

## 返回、错误与发货

成功和明确拒绝均先调用保存终态，再发布事件；新购成功的同一结果还会从 `PayAsync` 返回。消费方须选择统一履约入口或对事件和返回共同去重，避免发两次奖励。成功 `OrderId` 为原 `game_order_id`，`CanDeliver=true`；恢复时另带 `IsRecoveredOrder=true`。服务端 `Shipped` 当前也返回 `CanDeliver=true`，不能把它解释为客户端自动禁止重复发货，仍须对稳定订单和后端实际履约状态去重。

报价的 `WalletNotReady`、`InvalidPrice`、`InsufficientBalance` 是 `VoucherQuoteStatus`，它们不是一次已经提交的扣减结果。支付失败先看 `IAPResult.ErrorSource`：初始化、禁用、重复支付和商品缺失属于 `PluginRouter`；`Voucher` 才按下表解释。

| Voucher 错误 | 当前处理边界 |
|---|---|
| `WalletNotReady (1)` | 钱包/账号尚未可用；先完成登录及刷新 |
| `InvalidPrice (2)`、`InsufficientBalance (3)` | 价格或精确组合不足；正常业务在 Quote 阶段先按状态处理，非 Ready 报价不能构造请求 |
| `StaleQuote (4)` | 请求商品、账号代际或钱包版本变化；刷新并重新报价后再由用户发起购买 |
| `TransactionPending (5)` | 原单结果未知且已保留，后续恢复重用原单；不代表扣减失败或已撤销，也不授予发货资格 |
| `NetworkError (6)` | 钱包刷新/测试发放网络失败或取消；已提交扣减的不确定结果通常由协调器统一返回 `TransactionPending` |
| `ServerRejected (7)` | 明确拒绝，失败事件与返回可能携带稳定订单号；不会作为未知订单继续自动恢复 |
| `JournalFailure (8)` | 首次写 journal 失败或同订单 payload 冲突；内存可能已留记录，先排查持久化，不能直接假定没有订单 |
| `ProtocolError (9)` | 钱包或测试响应不完整/资产面额无效；扣减响应无法安全分类时通常转为 `TransactionPending` |
| `StaleAccount (10)` | 测试发放响应属于旧账号，不发布钱包；普通钱包刷新同类迟到响应当前使用 `StaleQuote` |
| `TestGrantUnavailable (11)` | 测试发放路由未配置或不可解析；不代表客户端存在独立的正式环境权限门禁 |

预取消的支付入口或等待交易串行权时可抛 `OperationCanceledException`；已进入网关的取消通常保存为待恢复结果。存档和事件异常仍可直接抛出，不能假定每次支付都必定返回上述错误对象。`CheckLocalOrdersAsync` 是有限的本地 journal 扫描，不是商店 RestorePurchases，也不会从服务端找回客户端存档已丢失的任意旧订单。

## 6. 协议生成约束

`PbNetGiftVoucher.cs` 必须由仓库 protoc 使用 `--csharp_opt=internal_access` 生成。生成类型保持程序集内部可见，业务侧只能通过正式或测试 capability 及其领域模型使用 Voucher 能力。

该协议导入 `pb_net_header.proto`，重新生成须配置能找到框架权威公共 Header 的导入路径，并保持生成的 Header 类型引用一致；不要为编译方便在 Voucher 程序集中另生成一份同名公共 Header。仅修改当前生成 `.cs`、命令字符串或运行时 JSON，不能代替协议源、正式 Network 导出与后端配对。
