# Nova Framework - SDK - TGA

> 包名：`com.solotopia.nova.framework.sdk.tga`
> 当前版本：`0.1.7`

TGA 数据埋点插件，提供事件追踪服务

## 功能概览

- 封装 ThinkingAnalytics Unity SDK `3.4.6`。
- 提供 `ITrackPlugin` 事件上报能力。
- 提供 `IDeviceIdProvider` 设备标识能力。
- 支持通过 `TGAPluginConfig` 配置 `TDMode`、`TDTimeZone`、日志开关、上报指令名、测试用户标识。
- 支持在初始化后将 TGA `DeviceId` 同步为 `DistinctId`，用于让访客 ID 与设备 ID 保持一致。

## 安装

通过 Nova 私域 UPM 注册表以 UPM 依赖形式接入（注册表地址向 Nova Framework 内部开发人员索取）：

```json
"dependencies": {
  "com.solotopia.nova.framework.sdk.tga": "0.1.7"
}
```

## 配置

TGA 运行时配置由 `TGAPluginConfig` 承载，并由 `SDKManager` 注入到 `TGAPlugin.OnInitializeAsync(...)`。

| 字段 | 默认值 | 说明 |
|---|---|---|
| `AppID` | 空 | ThinkingAnalytics 后台分配的应用 ID。为空时跳过 TGA 初始化。 |
| `Mode` | `TDMode.Normal` | TGA SDK 上报模式，直接写入 `TDConfig.mode`。 |
| `TimeZone` | `TDTimeZone.Local` | TGA SDK 时区，直接写入 `TDConfig.timeZone`。 |
| `LogEnable` | `false` | 是否开启 TGA SDK 调试日志。 |
| `ServerCmdName` | 空 | 解析埋点上报 URL 的 `NetworkCmds` 指令名。为空或无法解析时跳过 TGA 初始化。 |
| `ReportCmdName` | 空 | 登录后向业务服务器上报 TGA 标识时使用的 NetCmd 名称。 |
| `IsTestUser` | `true` | 是否按测试用户写入 `nova_test`。 |
| `AssignDeviceIdToDistinctId` | `false` | 开启后在 `TDAnalytics.Init(...)` 后、发布 `TGADistinctId` 前调用 `TDAnalytics.SetDistinctId(TDAnalytics.GetDeviceId())`。 |

更多运行时 API 与初始化细节见 [Nova/Doc/INDEX.md](./Nova/Doc/INDEX.md)。

## 维护

变更记录见 [CHANGELOG.md](./CHANGELOG.md)。每次发版必须在 CHANGELOG 中追加对应版本节，否则发布脚本会中断。

## 当前开源状态

- 当前结论：包根第三方声明已补齐，可按“保留 ThinkingData 上游许可证 + 包根说明文件”的方式进入公开仓。

## 许可与第三方声明

- 包根许可边界说明见 [LICENSE.md](./LICENSE.md)。
- 上游来源、第三方声明与当前再分发边界见 [THIRD_PARTY_NOTICES.md](./THIRD_PARTY_NOTICES.md)。
- `Core/` 内随包分发的 `LICENSE`、`NOTICE`、`README` 等文件，应与对应内容一起保留。
