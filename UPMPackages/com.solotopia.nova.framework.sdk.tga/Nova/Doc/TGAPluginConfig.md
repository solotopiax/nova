# TGAPluginConfig

## 1. 简介

`TGAPluginConfig` 是 TGA 插件的运行时配置，实现 `ISDKPluginConfig`，由 `SDKManager` 自动注入到 `TGAPlugin.OnInitializeAsync(...)`。

## 2. 配置字段

| 字段 | 说明 |
|---|---|
| `AppID` | ThinkingAnalytics 后台分配的应用 ID |
| `Mode` | 上报模式整型值，运行时转成 `TDMode` |
| `LogEnable` | 是否输出调试日志 |
| `ServerCmdName` | 解析埋点上报 URL 的 NetCmd 名称 |
| `ReportCmdName` | 登录后向业务服务器上报 TGA 标识时使用的 NetCmd 名称 |
| `IsTestUser` | 是否按测试用户打点 |
| `DisplayName` | ConfigWindow 显示名，固定为 `TGA 数据分析` |

## 3. 初始化影响

- `AppID` 为空：跳过 TGA 初始化
- `ServerCmdName` 为空或无法解析 URL：跳过 TGA 初始化
- `ReportCmdName` 用于登录后标识上报，不影响本地 SDK 初始化

## 4. 关联

- 插件本体：[TGAPlugin.md](./TGAPlugin.md)
