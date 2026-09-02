# TGAPluginConfig

## 1. 简介

`TGAPluginConfig` 是 TGA 插件的运行时配置，实现 `ISDKPluginConfig`，由 `SDKManager` 自动注入到 `TGAPlugin.OnInitializeAsync(...)`。

## 2. 配置字段

| 字段 | 说明 |
|---|---|
| `AppID` | ThinkingAnalytics 后台分配的应用 ID |
| `Mode` | TGA SDK 上报模式，类型为包内 `TGAReportMode`，默认 `TGAReportMode.Normal` |
| `TimeZone` | TGA SDK 时区，类型为包内 `TGATimeZone`，默认 `TGATimeZone.Local` |
| `LogEnable` | 是否输出调试日志 |
| `ServerCmdName` | 解析埋点上报 URL 的 NetCmd 名称 |
| `ReportCmdName` | 登录后向业务服务器上报 TGA 标识时使用的 NetCmd 名称 |
| `IsTestUser` | 是否按测试用户打点 |
| `AssignDeviceIdToDistinctId` | 是否在 TGA 初始化后将 DeviceId 设置为 DistinctId，默认关闭 |
| `DisplayName` | ConfigWindow 显示名，固定为 `TGA 数据分析` |

## 3. 枚举配置

`Mode` 使用不依赖厂商程序集的 `TGAReportMode`：

- `TGAReportMode.Normal`：正式上报模式，默认值。
- `TGAReportMode.Debug`：调试模式。
- `TGAReportMode.DebugOnly`：仅调试模式。

`TimeZone` 使用不依赖厂商程序集的 `TGATimeZone`：

- 默认值为 `TGATimeZone.Local`。
- 其他可选值为 `UTC`、`Asia_Shanghai`、`Asia_Tokyo`、`America_Los_Angeles`、`America_New_York` 和 `Other`。
- 两组包内枚举的整数值与当前 ThinkingAnalytics SDK 枚举保持一致，原有序列化整数无需迁移；仅在原生插件初始化时转换为厂商枚举。

## 4. 初始化影响

- `AppID` 为空：跳过 TGA 初始化
- `ServerCmdName` 为空或无法解析 URL：跳过 TGA 初始化
- `Mode` / `TimeZone` 会在原生插件初始化时转换后写入 `TDConfig.mode` / `TDConfig.timeZone`
- `ReportCmdName` 用于登录后标识上报，不影响本地 SDK 初始化
- `AssignDeviceIdToDistinctId` 开启后，会在 `TDAnalytics.Init(...)` 后、发布 `TGADistinctId` 前调用 `TDAnalytics.SetDistinctId(TDAnalytics.GetDeviceId())`

## 5. 关联

- 插件本体：[TGAPlugin.md](./TGAPlugin.md)
