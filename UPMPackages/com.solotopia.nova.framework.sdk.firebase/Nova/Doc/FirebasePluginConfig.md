# FirebasePluginConfig

## 1. 简介

`FirebasePluginConfig` 是 Firebase 插件的运行时配置，实现 `ISDKPluginConfig`。

和 TGA / AppsFlyer 不同，Firebase SDK 自身的大部分初始化信息并不从 Nova 配置系统注入；当前配置对象只承载框架侧需要的业务服务器协议名和 push task 批量发送策略。默认国家 topic 的国家码等待、超时和缓存兜底已经收口到 AD 模块的 `IAdPlugin.GetCountryCodeAsync(...)`。

## 2. 配置字段

| 字段 | 说明 |
|---|---|
| `ReportCmdName` | 登录后向业务服务器上报 Firebase Push Token / Analytics Instance ID 时使用的 NetCmd 名称 |
| `PushCmdName` | 批量创建或取消服务端 push task 时使用的 NetCmd 名称；为空时不会发送协议，本地缓存保留等待下次触发 |
| `PushFlushIntervalSeconds` | push task 本地缓存后的批量发送间隔，默认 `100` 秒；小于等于 `0` 时写入后立即尝试发送 |
| `PushFlushBatchSize` | push task 本地缓存达到该数量时立即发送，默认 `5` 条；小于 `1` 时运行时按 `1` 处理 |
| `DisplayName` | ConfigWindow 中的显示名称，固定为 `Firebase` |

## 3. 使用位置

- `SDKManager` 按 `ConfigType` 自动把本配置注入 `FirebasePlugin.OnInitializeAsync(...)`。
- `FirebasePlugin` 会缓存该配置，并在用户登录后调用 `FirebaseReportNetService.Async(...)`。
- `FirebasePlugin` 会读取 `PushCmdName`、`PushFlushIntervalSeconds` 和 `PushFlushBatchSize` 控制 push task 缓存后的批量发送协议、时间阈值与数量阈值；应用从后台恢复前台也会主动请求发送缓存，但仍复用 Firebase 初始化和用户身份就绪门槛。
- 默认国家 topic 和登录上报的国家码通过 `IAdPlugin.GetCountryCodeAsync(...)` 获取；最终国家码为空或为 `IV` 时不会订阅国家 topic，也不会覆盖旧国家 topic 存档。

## 4. Push Task 协议注意事项

- 发送协议前必须满足 Firebase 初始化完成和 `SetUserId(...)` 已成功同步用户身份。
- `PushCmdName` 为空、协议失败或响应不成功时，本地缓存不会删除，会等待后续时间阈值、数量阈值、恢复前台或重新登录后的触发。
- `FirebasePushTask.Cancel == true` 时，协议层只发送 `task_key` 和 `cancel`，不会携带 `trigger_time` 或 `template_id`。

## 5. 关联

- 插件本体：[FirebasePlugin.md](./FirebasePlugin.md)
