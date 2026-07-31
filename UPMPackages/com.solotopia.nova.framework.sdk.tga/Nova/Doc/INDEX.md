# Nova Framework - SDK - TGA 文档索引

> 本包封装 ThinkingAnalytics（TGA）接入，是 Nova 默认的数据埋点插件之一。
> 当前运行时公开面主要由 `TGAPlugin` 与 `TGAPluginConfig` 组成。

## 业务侧公开 API

| 类型 | 说明 | 文档 |
|---|---|---|
| `TGAPlugin` | TGA 插件，实现 `ITrackPlugin` 与 `IDeviceIdProvider` | [TGAPlugin.md](./TGAPlugin.md) |
| `TGAPluginConfig` | TGA 插件配置，承载 AppID / TDMode / TDTimeZone / 日志开关 / 上报指令名 / DeviceId 同步 DistinctId 开关 | [TGAPluginConfig.md](./TGAPluginConfig.md) |

## 当前能力

- 常规埋点：`TrackEvent(...)`
- 高级事件：`TrackFirst(...)`、`TrackUpdatable(...)`、`TrackOverwritable(...)`
- 用户属性：`UserSet(...)`、`UserSetOnce(...)`、`UserAdd(...)`、`UserAppend(...)`
- 公共属性：静态属性、动态属性、框架级属性四套链路
- 设备标识：`GetDeviceId()` / `IDeviceIdProvider.GetDeviceID()`，可配置初始化后将 `DeviceId` 同步为 `DistinctId`

## 配置摘要

- `Mode` 使用 `TDMode`，默认 `TDMode.Normal`，初始化时写入 `TDConfig.mode`。
- `TimeZone` 使用 `TDTimeZone`，默认 `TDTimeZone.Local`，初始化时写入 `TDConfig.timeZone`。
- `ServerCmdName` 通过 Nova Network 模块解析真实上报 URL；为空或无法解析时跳过 TGA 初始化。
- `AssignDeviceIdToDistinctId` 默认关闭；开启后会在发布 `TGADistinctId` 数据槽位前同步 `DeviceId` 到 `DistinctId`。

## 平台边界

- 整体受 `#if !UNITY_WEBGL` 保护，WebGL 不编译本包
- 依赖 ThinkingAnalytics Unity SDK `3.4.6`

## 相关

- [TGAPlugin.md](./TGAPlugin.md) — TGA 埋点插件
- [TGAPluginConfig.md](./TGAPluginConfig.md) — TGA 插件配置
