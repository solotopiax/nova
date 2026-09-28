# Nova Framework - SDK - TGA 文档索引

> 本包封装 ThinkingAnalytics（TGA）接入，是 Nova 默认的数据埋点插件之一。
> 当前运行时公开面主要由 `TGAPlugin` 与 `TGAPluginConfig` 组成。

## 业务侧公开 API

| 类型 | 说明 | 文档 |
|---|---|---|
| `TGAPlugin` | TGA 插件，实现 `ITrackPlugin` 与 `IDeviceIdProvider` | [TGAPlugin.md](./TGAPlugin.md) |
| `TGAPluginConfig` | TGA 插件配置，承载 AppID / TGAReportMode / TGATimeZone / 日志开关 / 上报指令名 / DeviceId 同步 DistinctId 开关 | [TGAPluginConfig.md](./TGAPluginConfig.md) |

## 当前能力

- 常规埋点：`TrackEvent(...)`
- 高级事件：`TrackFirst(...)`、`TrackUpdatable(...)`、`TrackOverwritable(...)`
- 用户属性：`UserSet(...)`、`UserSetOnce(...)`、`UserAdd(...)`、`UserAppend(...)`
- 公共属性：静态属性、动态属性、框架级属性四套链路
- 设备标识：`GetDeviceId()` / `IDeviceIdProvider.GetDeviceID()`，可配置初始化后将 `DeviceId` 同步为 `DistinctId`

## 配置摘要

- `Mode` 使用包内 `TGAReportMode`，默认 `TGAReportMode.Normal`，初始化时转换并写入 `TDConfig.mode`。
- `TimeZone` 使用包内 `TGATimeZone`，默认 `TGATimeZone.Local`，初始化时转换并写入 `TDConfig.timeZone`。
- `ServerCmdName` 通过 Nova Network 模块解析真实上报 URL；为空或无法解析时跳过 TGA 初始化。
- `AssignDeviceIdToDistinctId` 默认关闭；开启后会在发布 `TGADistinctId` 数据槽位前同步 `DeviceId` 到 `DistinctId`。

## 平台边界

- Android / iOS 继续使用 ThinkingAnalytics 对应的原生实现。
- WebGL 使用 ThinkingAnalytics PC/WebGL 实现；`DeviceId` 是通过 PlayerPrefs 持久化的随机 GUID，不是硬件标识。清除浏览器站点数据、使用无痕模式或更换站点来源后会重新生成。
- 依赖 ThinkingAnalytics Unity SDK `3.4.6`

## 相关

- [TGAPlugin.md](./TGAPlugin.md) — TGA 埋点插件
- [TGAPluginConfig.md](./TGAPluginConfig.md) — TGA 插件配置

## SDK 初始化与账号规则

本插件由 ConfigMaster 启用。初始化是否等待其他 SDK 由插件的 `ISDKInitializationDependencies` 能力声明决定，`Priority` 不控制启动先后；业务只需目标能力时使用 `Nova.SDK.WaitForPluginAsync<T>()` 并检查 `Ready`。游戏后端登录成功后调用 `Nova.SDK.Login(uid, userProperties)`，框架会在插件就绪后补交最新账号；切换账号再次调用，登出调用 `EndLoginSession()`。新接入 SDK 应按框架 SDK 插件契约 实现。
