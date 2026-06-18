# Nova Framework - SDK - TGA 文档索引

> 本包封装 ThinkingAnalytics（TGA）接入，是 Nova 默认的数据埋点插件之一。
> 当前运行时公开面主要由 `TGAPlugin` 与 `TGAPluginConfig` 组成。

## 业务侧公开 API

| 类型 | 说明 | 文档 |
|---|---|---|
| `TGAPlugin` | TGA 插件，实现 `ITrackPlugin` 与 `IDeviceIdProvider` | [TGAPlugin.md](./TGAPlugin.md) |
| `TGAPluginConfig` | TGA 插件配置，承载 AppID / 上报模式 / 日志开关 / 上报指令名 | [TGAPluginConfig.md](./TGAPluginConfig.md) |

## 当前能力

- 常规埋点：`TrackEvent(...)`
- 高级事件：`TrackFirst(...)`、`TrackUpdatable(...)`、`TrackOverwritable(...)`
- 用户属性：`UserSet(...)`、`UserSetOnce(...)`、`UserAdd(...)`、`UserAppend(...)`
- 公共属性：静态属性、动态属性、框架级属性四套链路
- 设备标识：`GetDeviceId()` / `IDeviceIdProvider.GetDeviceID()`

## 平台边界

- 整体受 `#if !UNITY_WEBGL` 保护，WebGL 不编译本包
- 依赖 ThinkingAnalytics Unity SDK `3.4.6`

## 相关

- [TGAPlugin.md](./TGAPlugin.md) — TGA 埋点插件
- [TGAPluginConfig.md](./TGAPluginConfig.md) — TGA 插件配置
