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
- 各 SDK 子包文档由对应 UPM 包独立维护，不在主框架文档树中建立反向导航。

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

| 文档 | 说明 |
|---|---|
| [Plugins/Tracking/ITrackPlugin.md](./Plugins/Tracking/ITrackPlugin.md) | 通用埋点接口 |
| [Plugins/Tracking/IMonetizeTrackPlugin.md](./Plugins/Tracking/IMonetizeTrackPlugin.md) | 变现分析事件接口 |
| [Plugins/Tracking/IAttributionPlugin.md](./Plugins/Tracking/IAttributionPlugin.md) | 归因接口 |

### Ad

| 文档 | 说明 |
|---|---|
| [Plugins/Ad/IAdPlugin.md](./Plugins/Ad/IAdPlugin.md) | 广告主接口 |
| [Plugins/Ad/IBannerControl.md](./Plugins/Ad/IBannerControl.md) | Banner 专属控制接口 |
| [Plugins/Ad/AdLoadEvent.md](./Plugins/Ad/AdLoadEvent.md) | 广告加载统一结果载荷 |

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
