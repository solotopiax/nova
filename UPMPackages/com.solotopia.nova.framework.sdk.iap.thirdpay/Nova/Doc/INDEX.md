# ThirdPay 文档索引

本包提供应用内第三方支付 Store，仅支持 InAppAuto。

| 文档 | 内容 |
|---|---|
| [ThirdPayStore.md](./ThirdPayStore.md) | 接入与验收顺序、国家码锁定时序、统一配置、订单状态和平台支付行为 |
| [ThirdPayStoreConfig.md](./ThirdPayStoreConfig.md) | Store 配置字段 |

主要公开类型：

- `IIAPThirdPayConfigCapable`：国家码覆盖、当前国家码、支付配置可用状态及服务端关闭原因码。
- `IIAPThirdPayProductCapable`：第三方商品快照查询。
- `IIAPThirdPayCheckoutCapable`：Google 信息页策略与 WebView 文案控制。
- `IAPThirdPayRequest`：支付请求；可选的 `FirstRechargeReward` 与 `ExtraBonusReward` 仅随当前加密支付 URL 传给网页，WebView 展示由 ThirdPay 内部全屏处理。
- `IAPThirdPayRewardItem`：网页奖励物品；`ItemId` 与 `Quantity` 均要求为正数。
- `ThirdIapNetService`：与 Mobile IAP 同层的商品列表、渠道参数、待补发订单和批量验单协议封装。

## 支付请求参数

| 字段 | 用途 | 是否进入订单或验单 |
|---|---|---|
| `CustomData` | 客户端业务透传，随 `IAPResult.CustomData` 返回 | 不进入网页奖励参数 |
| `ReceiptParam` | 第三方订单与验单往返透传，写入加密 JSON 顶层 `receipt_param` | 参与订单键与验单 |
| `FirstRechargeReward` | 首充网页展示奖励，写入 `first_recharge_reward` | 仅当前支付页使用 |
| `ExtraBonusReward` | 额外赠送网页展示奖励，写入 `extra_bonus_reward` | 仅当前支付页使用 |

`IAPThirdPayRequest.CustomParam` 已移除。网页奖励统一使用 `IAPThirdPayRewardItem`，构造时要求 `itemId > 0` 且 `quantity > 0`；没有对应奖励时保持 `null`，加密 JSON 会省略该节点。

支付 URL 构造、UniWebView 5.11.1 生命周期、Android Auth Tab / Custom Tabs 外部支付页、支付回调和全屏支付页均由包内实现，业务无需注入支付页打开器。`ThirdPayStore` 仅持有 `ThirdPayServiceHub`，由 Hub 统一装配服务并维护国家码、商品快照、当前账号存档、外部浏览器 session 与后台任务生命周期。

统一配置中的 `client_config` 当前只转换为包内 `internal ThirdPayClientConfig` 并输出受 IAP 日志开关控制的 Debug 日志；上述公开能力接口没有读取它的成员，业务代码不能把 `ClientConfig.Test` 或 `TryGetValue` 当作公开 API。
