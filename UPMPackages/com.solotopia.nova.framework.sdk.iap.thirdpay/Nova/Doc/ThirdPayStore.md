# ThirdPayStore

## 1. 简介

`ThirdPayStore` 是第三方支付渠道实现，继承 `IAPStoreBase`，用于处理非官方商店支付场景。

当前实现支持两类拉起模式：

- `Browser`
- `InAppAuto`

## 2. 公开 API

| 成员 | 说明 |
|---|---|
| `InitializeAsync(...)` | 初始化 store、缓存配置与运行时上下文 |
| `CanHandle(IAPRequest)` | 仅处理 `IAPThirdPayRequest` |
| `PayAsync(IAPRequest, CancellationToken)` | 发起支付 |
| `SetAccountIDAsync(string, CancellationToken)` | 切换账号并触发补单 / 渠道参数拉取 |
| `FetchProductListAsync(CancellationToken)` | 拉取商品与支付渠道列表 |
| `GetProductInfo(long)` | 读取指定商品信息 |
| `GetPayTypeList()` | 读取支付渠道列表，上次成功渠道排前 |
| `SetCountryCode(string)` | 更新当前国家码 |
| `OnBrowserDeepLinkResolved(string)` | Browser 模式 DeepLink 回流入口 |
| `DisposeAsync(CancellationToken)` | 释放运行时状态 |

## 3. 当前实现要点

- 订单 ID 改为客户端本地生成 `clientOrderId`
- 支付 URL 由客户端本地拼接 AES query
- 先落盘再打开支付页，避免崩溃丢单
- Browser 模式依赖 DeepLink 命中本地订单后返回 `Success`

## 4. 使用示例

```csharp
await thirdPayStore.SetAccountIDAsync(userId, ct);
await thirdPayStore.FetchProductListAsync(ct);

var request = new IAPThirdPayRequest
{
    TableId = 1001,
    PayTypeId = 2,
    PayMethod = "alipay",
};

IAPResult result = await thirdPayStore.PayAsync(request, ct);
```

## 5. 关联

- 配置类型：[ThirdPayStoreConfig.md](./ThirdPayStoreConfig.md)
- 当前实现设计：[ThirdPayStore-Design.md](./ThirdPayStore-Design.md)
