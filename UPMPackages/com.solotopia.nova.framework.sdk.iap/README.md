# Nova Framework - SDK - IAP

> 包名：`com.solotopia.nova.framework.sdk.iap`
> 当前版本：`0.2.7`

Nova IAP 核心包，提供多渠道内购调度、Store 抽象、商品表运行期查询、SKU Excel 导入/模板导出、支付事件桥接、Loading 防重入与通用持久化模板。具体渠道能力由子包提供，例如 `com.solotopia.nova.framework.sdk.iap.mobile`。

## 安装

已接入 Nova Framework 的项目可打开 `Nova → Open PlugPals → 外网仓库`，搜索并安装 `com.solotopia.nova.framework.sdk.iap`。手动配置 UPM 时使用 `https://upm.solotopiax.com`，scope 为 `com.solotopia`：

```json
"dependencies": {
  "com.solotopia.nova.framework.sdk.iap": "0.2.7"
}
```

本包支持 Unity `6000.4.x`，最低 `6000.4.2f1`；清单依赖为 `com.solotopia.nova.framework@0.7.9`、`com.solotopia.unitask@10.0.6` 和 `com.solotopia.nova.framework.kit.network.gamelogin@0.1.18`，由 UPM 解析。随包示例使用新版启动配置等待能力，因此安装本包时也需要 Framework `0.7.9`。核心包本身不提供平台收银能力，还需按目标支付渠道安装对应 Store 子包。

## 当前公开入口

- `IAPPlugin`：SDK 插件入口，通过 `SDKComponent.TryGet<IAPPlugin>(out var iap)` 获取；统一接收 SDK Pause/Focus 生命周期并转发给有需要的 Store。通过 `ISDKLoginReceiver` 接收框架交付的账号 UID；可选等待已启用的 `ITrackPlugin`，不以 `Priority` 保证初始化顺序。
- `IAPPluginConfig`：Inspector 序列化配置，只保存配置数据和内联商品表。
- `IAP Products Excel`：Inspector 支持导出模板并导入工作簿；校验通过后全量覆盖 `IAPPluginConfig.Products`，不接入 Luban。
- `IAPProductCatalogProtocol` / `IAPProductCatalogUploader`：Editor-only 商品目录快照与共享上传入口；请求使用明文 Head 和 App AES 加密 Body，按 Debug/Release 选择固定测试/正式上传地址，商品列表手动按钮和 Pipify 的 `iap.product_catalog.upload` Step 共用同一上传实现。
- `IIAPProductTable`：运行期商品表查询接口，由 `IAPProductTableService` 基于配置数据构建。
- `IAPStoreBase`：各渠道 Store 的公共抽象基类。
- `IIAPStorePauseListener` / `IIAPStoreFocusListener`：Store 可选生命周期接口，不要求所有 Store 实现。
- `IAPLog` / `IAPLogOwner`：统一日志网关与 Store/Service 日志基类；`IAPStoreBase` 子类通过 protected `LogTag` 指定日志标签。
- `IAPRequest` / `IAPResult`：统一支付请求与结果；`ReceiptParam` 可随平台票据在支付、补单与恢复流程中往返。
- `IAPInitResult`：统一初始化结果；错误码均为 `int`，核心层和各 Store 分别定义自己的错误码枚举。

`IAPPluginConfig.EnableIAPLog` 默认开启；插件初始化时会把该值应用到统一日志网关，同时控制核心与各渠道 Store 的 Debug、Warning、Error 输出。

配置 Store 和商品后，须从 ConfigMaster 正式导出当前坐标的 `ConfigRuntimeSO`；Excel 导入只更新配置源，不自动导出。后端登录成功时调用 `Nova.SDK.Login(uid, userProperties)`，插件尚未就绪时由框架在就绪后补交最新 UID；业务仍需在适当时机调用 `CheckLocalOrdersAsync`。

## 支付结果与补单

`IsSuccess` 表示本次调用或订单校验成功，发货还须检查 `CanDeliver`，并由业务按真实订单号幂等处理。返回值与 `PaySuccess` 事件可能对应同一笔订单，事件也可能重放，不应分别直接发奖。

Android 第三方支付默认在首次建单后的三十分钟内，重复点击时先查询成功未验订单；命中后只补单，查询失败停止新购。各笔可发货历史订单通过原成功事件反馈，`PayAsync` 的批次完成结果不再授予发货资格。开关、窗口和升级行为见 [ThirdPay 购买前补单规则](README.md#跨包文档定位)（`com.solotopia.nova.framework.sdk.iap.thirdpay` 包内 `Nova/Doc/ThirdPayStore.md#android-重复点击补单查询`）。

## 文档

- [Nova/Doc/INDEX.md](./Nova/Doc/INDEX.md)
- [Nova/Doc/IAPPlugin.md](./Nova/Doc/IAPPlugin.md)
- [Nova/Doc/IAP-Architecture.md](./Nova/Doc/IAP-Architecture.md)
- [Nova/Doc/IAPProductCatalogProtocol.md](./Nova/Doc/IAPProductCatalogProtocol.md)

## Editor 商品目录上传

IAP Config 商品列表在“导入 SKU Excel”下方提供“上传支付表到服务器”按钮；Pipify 提供无参数的 `iap.product_catalog.upload` Step。两个入口读取当前激活且已导出的 `ConfigRuntimeSO`，发送明文 Head 与 App AES 加密 Body，并按 Debug/Release 自动选择测试或正式地址。

上传前必须保证当前导出平台与 Unity Active BuildTarget 一致，`AppID`、版本、渠道和商品表有效，并且 `AppAesKey`、`AppAesIV` 按 UTF-8 编码分别为 16 字节。完整 JSON、Hash、加密和失败语义见 [IAP 商品目录协议](./Nova/Doc/IAPProductCatalogProtocol.md)。

## 维护

变更记录见 [CHANGELOG.md](./CHANGELOG.md)。每次发版必须在 CHANGELOG 中追加对应版本节，否则发布脚本会中断。

## 跨包文档定位

本页链接相对于当前文件；独立包之间不假定相邻安装。先用 `UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()` 按下表精确包名查找，以该包的 `resolvedPath` 为根读取文档入口，再按包内相对链接进入目标。无 Editor 通道时使用当前有效 manifest 或解析工具给出的实际包位置。

| 包名 | 包内文档入口 | 本文引用的包内位置 |
|---|---|---|
| `com.solotopia.nova.framework.sdk.iap.mobile` | `Nova/DOCS/INDEX.md` | `Nova/DOCS/INDEX.md` |
| `com.solotopia.nova.framework.sdk.iap.thirdpay` | `Nova/Doc/INDEX.md` | `Nova/Doc/INDEX.md`<br>`Nova/Doc/ThirdPayStore.md#android-重复点击补单查询` |
| `com.solotopia.nova.framework.sdk.iap.voucher` | `Nova/Doc/INDEX.md` | `Nova/Doc/INDEX.md` |
