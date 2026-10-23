# ThirdPayStoreConfig

| 字段 | 说明 |
|---|---|
| `Enabled` | 是否启用 ThirdPay Store |
| `SkipPaymentInformationScreen` | 是否默认跳过当前 Google 外部结算接口的信息页；该开关不代表允许跳过目标计划强制的选择、披露或家长控制步骤 |
| `ExternalBrowserReturnValidateDelaySeconds` | 外部浏览器未收到成功回调时，返回 App 后兜底验单前的等待秒数，非正配置回落为 `2.5` |
| `PaymentConfigCmdName` | 获取第三方支付配置信息的 NetCmd，默认 `ThirdGetPaymentConfig` |
| `QueryPendingOrderCmdName` | 查询支付成功但客户端尚未校验订单的 NetCmd |
| `VerifyIapCmdName` | 批量验证第三方订单的 NetCmd |
| `GoogleApiTimeoutSeconds` | Google 外链结算网络类操作的超时秒数，非正配置回落为 `15`；用户信息页不受此超时限制 |

第三方支付可用性、商品、`payment_customer_ids` 和 HTTPS 支付页地址统一由 `PaymentConfigCmdName` 对应协议返回。客户端不再分别配置商品、渠道参数和支付页 URL 的 NetCmd。

国家码不从 `ThirdPayStoreConfig` 读取。Store 初始化时并发查询 Billing、iOS Storefront 和 AD 国家码，但登录前不会锁定结果或发送用户态配置协议；登录后首次有效自动解析结果会写入 Lock，并在当前 Store 生命周期及重新登录后持续复用。运行时可通过 `SetDebugCountryCode` 显式覆盖当前读取结果，但不会重写既有 Lock。全部来源均无有效值时 `ThirdGetPaymentConfig` 不会发送，网络协议层也会返回 `PARAM_ERROR`。服务端负责白名单、黑名单和国家策略判断，客户端只消费 `enabled` 与整数 `disabled_reason`，不保存策略明细。

支付页打开方式由平台固定决定：Android 使用外部浏览器链，iOS 使用 UniWebView Safe Browsing，Editor 使用嵌入式 UniWebView。

`Enabled` 是 Store 配置开关；`IsPaymentConfigReady`、`IsPaymentAvailable` 和 `PaymentDisabledReason` 是运行时公开配置能力，分别表示统一配置是否就绪、服务端是否允许当前上下文支付及服务端关闭原因。读取原因码前先检查就绪状态；统一配置的 `client_config` 当前仅供包内快照使用，没有对应的公开配置能力。

## 国家状态与配置缓存边界

国家码归一化执行 `Trim().ToUpperInvariant()`，并拒绝空值、`IV` 和 `UNKNOWN`；通用国家状态容器不校验两位长度或完整 ISO 国家名单。iOS 桥另有三位码转换。`SetDebugCountryCode` 本身没有 `DevelopMode` 门禁，调用方负责限制调试入口；底层 `ThirdPayCountryState.SetLockCountryCode` 也允许替换值，生命周期内首次锁定由 Store/Resolver 的调用顺序维持，不是容器的不可变约束。

持久化只更新有效且非空的 Billing、iOS Storefront、AD 国家码；Debug、Lock 与 Storefront 标识不写入该存档。清空当前来源不能解释为删除已保存的回退值，保存回调失败也不回滚已经变更的内存。来源缓存、当前账号订单和当前配置上下文是不同状态。

`ThirdPayPaymentConfigService.EnsureAsync` 在同上下文快照命中时直接返回；同上下文在途请求共享完成源。调用方令牌只取消自己的等待，不取消底层请求。不同上下文通过版本与完成源身份阻止旧响应写回；直接调用 `EnsureAsync` 切换上下文不会立即清空旧 `Snapshot`，需要 Store 的失效流程或显式 `Invalidate`。因此裸读 `Snapshot`、`FindProduct` 不会自动检查当前 UID、国家、时效或重新联网。

配置最多连续请求三次，无重试间隔；只有网络返回值或配置结构被判无效才进入下一次。请求回调、JSON 转换等抛出的异常直接传给等待方，不保证重试三次，`LastFailureReason` 也不保证保存该异常文本。`EnsureAsync == true` 只代表取得结构可用的快照，服务端 `Enabled == false` 的响应仍可成功缓存；启用响应只要求商品配置节点存在及支付页是绝对 HTTPS URL，不校验可信域名单，也不证明每个商品的币种、价格或商户资格正确。

快照固定了本次请求选用的标量与商品列表容器，但不是深度不可变对象：`PbNetThirdProductInfo` 元素仍为原对象引用，重复 `ProductId` 的索引后项覆盖前项，比较使用 `Ordinal`；外部修改元素不会重建索引。`ClientConfig.Values` 只约束顶层字典，嵌套 `JObject` / `JArray` 仍可变。直接构造快照假设响应已经校验，不提供与服务层相同的空值和 URL 门禁。

两个秒数配置只以 `> 0` 选用，否则回落默认值；没有额外有限值校验。不要传入无穷值并假定下游 `TimeSpan.FromSeconds` 一定可用。Google 网络等待与页面返回等待的取消、缩放时间及原生调用边界见 [ThirdPayStore](ThirdPayStore.md#运行时失败与回调边界)。
