# Nova Framework - SDK - IAP

> 包名：`com.solotopia.nova.framework.sdk.iap`
> 当前版本：`0.0.9`

Nova IAP 核心包，提供多渠道内购调度、Store 抽象、商品表运行期查询、支付事件桥接、Loading 防重入与通用持久化模板。具体渠道能力由子包提供，例如 `com.solotopia.nova.framework.sdk.iap.mobile`。

## 安装

通过 Nova 私域 UPM 注册表以 UPM 依赖形式接入：

```json
"dependencies": {
  "com.solotopia.nova.framework.sdk.iap": "0.0.9"
}
```

## 当前公开入口

- `IAPPlugin`：SDK 插件入口，通过 `SDKComponent.TryGet<IAPPlugin>(out var iap)` 获取。
- `IAPPluginConfig`：Inspector 序列化配置，只保存配置数据和内联商品表。
- `IIAPProductTable`：运行期商品表查询接口，由 `IAPProductTableService` 基于配置数据构建。
- `IAPStoreBase`：各渠道 Store 的公共抽象基类。
- `IAPResult` / `IAPInitResult`：统一支付结果与初始化结果；错误码均为 `int`，核心层和各 Store 分别定义自己的错误码枚举。

## 文档

- [Nova/Doc/INDEX.md](./Nova/Doc/INDEX.md)
- [Nova/Doc/IAPPlugin.md](./Nova/Doc/IAPPlugin.md)
- [Nova/Doc/IAP-Architecture.md](./Nova/Doc/IAP-Architecture.md)

## 维护

变更记录见 [CHANGELOG.md](./CHANGELOG.md)。每次发版必须在 CHANGELOG 中追加对应版本节，否则发布脚本会中断。
