# FirebasePluginConfig

## 1. 简介

`FirebasePluginConfig` 是 Firebase 插件的运行时配置，实现 `ISDKPluginConfig`。

和 TGA / AppsFlyer 不同，Firebase SDK 自身的大部分初始化信息并不从 Nova 配置系统注入；当前配置对象只负责承载业务服务器的标识上报协议名。

## 2. 配置字段

| 字段 | 说明 |
|---|---|
| `ReportCmdName` | 登录后向业务服务器上报 Firebase Push Token / Analytics Instance ID 时使用的 NetCmd 名称 |
| `DisplayName` | ConfigWindow 中的显示名称，固定为 `Firebase` |

## 3. 使用位置

- `SDKManager` 按 `ConfigType` 自动把本配置注入 `FirebasePlugin.OnInitializeAsync(...)`
- `FirebasePlugin` 会缓存该配置，并在用户登录后调用 `FirebaseReportNetService.Async(...)`

## 4. 关联

- 插件本体：[FirebasePlugin.md](./FirebasePlugin.md)
