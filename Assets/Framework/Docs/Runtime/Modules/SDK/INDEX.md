# SDK 模块文档索引

> 架构总览见 [ARCHITECTURE.md](./ARCHITECTURE.md)

## 入口

| 文档 | 说明 |
|---|---|
| [ARCHITECTURE.md](./ARCHITECTURE.md) | 当前结构与职责边界 |
| [SDKComponent.md](./SDKComponent.md) | 场景入口，持有 `ISDKManager` 并代理 Unity 生命周期 |

## 边界

- 本索引只描述主框架 `SDK` 公共层。
- 各 SDK 子包允许单向依赖这些公共接口、基类与配置约定。
- 各 SDK 子包文档由对应 UPM 包独立维护，不在主框架文档树中逐包建立反向导航。

## SDK / Kit 包文档发现

Agent 遇到厂商名、能力名、包名或类型名等关键字时，按以下顺序查找当前工程的包文档：

1. 结合当前 manifest 依赖、Unity 已注册包和候选包 `package.json` 的 `name`、`displayName`、`keywords` 确定准确包名；不能仅凭开发仓文件夹名判断已安装。
2. 按准确包名读取当前 `PackageInfo.resolvedPath`；尚未注册但存在有效本地 `file:` manifest 绑定时，按 manifest 所在目录解析并核对目标包名。开发仓本地包可能在 `UPMPackages/`，消费包可能在 `Packages/` 或 Unity 缓存，但都以当前有效绑定为准，不猜 `Library/PackageCache` 哈希或选取缓存中的旧版本。
3. 在该实际包根下查找 `Nova/Doc/INDEX.md`，并兼容历史目录名 `Nova/Docs/INDEX.md`、`Nova/DOCS/INDEX.md`。框架文档给出的“包名＋包内路径”也从这个根定位，不从 Framework 文档跨目录拼接。
4. 先读包内 INDEX，再读取它链接的具体 API、配置、平台接入或排障文档；默认不展开所选独立包的厂商源码目录（例如包内 `Core/`，仍从本节确认的实际包根定位）。只定向读取已确认包根下的文档，不扩展扫描其他 Library 内容。
5. 当前工程未安装候选包、尚未解析出实际目录或不存在包内 INDEX 时，明确报告缺失的包名或文档入口，不用主框架公共层文档代替具体 SDK 文档。

框架自身的根目录和 Sample 导入位置同样按 [框架位置与文档入口](../../../START_HERE.md#先确定框架位置与文档入口) 核对。

## Definitions

| 文档 | 说明 |
|---|---|
| [Definitions/ISDKPlugin.md](./Definitions/ISDKPlugin.md) | Plugin 根接口 |
| [Definitions/Lifecycle.md](./Definitions/Lifecycle.md) | 可选生命周期广播接口 |
| [Definitions/ISDKPluginConfig.md](./Definitions/ISDKPluginConfig.md) | Plugin 配置接口 |
| [Definitions/SDKPluginBase.md](./Definitions/SDKPluginBase.md) | 通用抽象基类 |
| [Definitions/PluginBase.md](./Definitions/PluginBase.md) | 泛型配置插件基类 |
| [Definitions/SDKDataKeys.md](./Definitions/SDKDataKeys.md) | 共享数据槽位 key 常量 |
| [Definitions/Data.md](./Definitions/Data.md) | 当前公共数据类型总览 |
| [Definitions/Exceptions.md](./Definitions/Exceptions.md) | 当前异常类型总览 |

## Events

| 文档 | 说明 |
|---|---|
| [Events/ObservableEvent.md](./Events/ObservableEvent.md) | 事件容器抽象基类 |
| [Events/StickyEvent.md](./Events/StickyEvent.md) | 最新值回放事件 |
| [Events/ReplayEvent.md](./Events/ReplayEvent.md) | 固定容量历史回放事件 |

## Plugins

### Tracking

埋点登记、表格归属和样式统一遵循 [Tracks 维护规范](../../../TRACKS.md)。

| 文档 | 说明 |
|---|---|
| [Plugins/Tracking/IUserPropertyPlugin.md](./Plugins/Tracking/IUserPropertyPlugin.md) | 一次性用户属性可选能力 |
| [Plugins/Tracking/ITrackPlugin.md](./Plugins/Tracking/ITrackPlugin.md) | 通用埋点接口 |
| [Plugins/Tracking/IMonetizeTrackPlugin.md](./Plugins/Tracking/IMonetizeTrackPlugin.md) | 变现分析事件接口 |
| [Plugins/Tracking/IAcquisitionTrackPlugin.md](./Plugins/Tracking/IAcquisitionTrackPlugin.md) | 投放 / 买量转化事件接口 |
| [Plugins/Tracking/IAttributionPlugin.md](./Plugins/Tracking/IAttributionPlugin.md) | 归因接口 |

### Ad

| 文档 | 说明 |
|---|---|
| [Plugins/Ad/IAdPlugin.md](./Plugins/Ad/IAdPlugin.md) | 广告主接口 |
| [Plugins/Ad/IBannerControl.md](./Plugins/Ad/IBannerControl.md) | Banner 专属控制接口 |
| [Plugins/Ad/AdLoadEvent.md](./Plugins/Ad/AdLoadEvent.md) | 广告加载统一结果载荷 |

### IAP 公共层

支付公共层保留 `IIAPPlugin`、`IIAPRequest`、`IIAPResult` 与 `IAPErrorSource`，具体 Store、事件、配置和结果扩展由实际安装的支付包提供：

- `IIAPRequest` 只暴露商品表行 `TableId`；`IIAPPlugin` 提供 `SetUserId`、`PayAsync<T>` 和 `RestorePurchasesAsync<T>`。
- `IIAPResult` 只包含商品、成功状态、错误域/码/描述与订单号；`CanDeliver`、`IsRecoveredOrder`、`ReceiptParam` 和 `StoreType` 等由包内 `IAPResult` 承载，不能假定最小接口具备这些成员。
- Store 特有能力与补单扫描分别由包内 `IAPPlugin.TryGetCapability<T>`、`CheckLocalOrdersAsync` 提供。按上方包文档发现流程读取当前 IAP、Mobile、ThirdPay 或 Voucher 文档，不把通用接口说明当作具体渠道验收。

### Account / Cloud / Device

| 文档 | 说明 |
|---|---|
| [Plugins/Account/IAuthPlugin.md](./Plugins/Account/IAuthPlugin.md) | 登录接口 |
| [Plugins/Cloud/IPushPlugin.md](./Plugins/Cloud/IPushPlugin.md) | 推送接口 |
| [Plugins/Cloud/IRemoteConfigPlugin.md](./Plugins/Cloud/IRemoteConfigPlugin.md) | 远程配置接口 |
| [Plugins/Device/IDeviceIdProvider.md](./Plugins/Device/IDeviceIdProvider.md) | 设备唯一标识提供者 |

## Managers

| 文档 | 说明 |
|---|---|
| [Managers/Interfaces/ISDKRegistrationManager.md](./Managers/Interfaces/ISDKRegistrationManager.md) | 可选明确注册能力 |
| [Managers/Interfaces/ISDKManager.md](./Managers/Interfaces/ISDKManager.md) | Manager 对外契约 |
| [Managers/Implements/SDKManagerBase.md](./Managers/Implements/SDKManagerBase.md) | 抽象基类 |
| [Managers/Implements/SDKManager.md](./Managers/Implements/SDKManager.md) | 唯一实现 |
| [Managers/Definitions/SDKManagerConfig.md](./Managers/Definitions/SDKManagerConfig.md) | 初始化配置 |
| [Definitions/SDKPluginEntry.md](Definitions/SDKPluginEntry.md) | Inspector 序列化条目 |

## Editor 关联

| 文档 | 说明 |
|---|---|
| [../../../Editor/Inspectors/SDKComponentInspector/SDKComponentInspector.md](../../../Editor/Inspectors/SDKComponentInspector/SDKComponentInspector.md) | SDK Inspector |
| [../../../Editor/Inspectors/SDKComponentInspector/PluginEntriesDrawer.md](../../../Editor/Inspectors/SDKComponentInspector/PluginEntriesDrawer.md) | Plugin 条目绘制器 |
