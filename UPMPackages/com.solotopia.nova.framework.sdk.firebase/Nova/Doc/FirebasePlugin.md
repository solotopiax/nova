# FirebasePlugin

## 1. 简介

`FirebasePlugin` 是 Firebase 聚合插件，实现框架能力与 SDK 生命周期接口：

- `IMonetizeTrackPlugin`：事件埋点与用户属性
- `IPushPlugin`：FCM Token 获取、主题订阅、Token 刷新通知
- `ISDKPauseListener`：接收应用前后台切换，在恢复前台时请求发送本地 push task 缓存
- `ISDKLoginReceiver`：接收 `SDKManager` 交付的账号，设置 Firebase 用户 ID 并触发标识上报
- `ISDKLoginSessionEndReceiver`：更新账号会话边界，使旧会话的异步上报结果失效

`SDKManager` 在插件就绪后调用 `OnSDKLogin`，插件随后把 Firebase 标识异步上报到业务服务器。

## 2. 公开 API

| 成员 | 说明 |
|---|---|
| `TrackEvent(string, Dictionary<string, object>)` | 上报自定义埋点事件 |
| `TrackEvent(TrackEvent)` | 上报统一事件载荷 |
| `SetUserProperty(string, string)` | 设置用户属性 |
| `SetUserId(string)` | 同步 Analytics / Crashlytics 用户 ID |
| `OnTokenRefreshed` | FCM Token 刷新事件 |
| `GetTokenAsync(...)` | 等待并返回当前 FCM Token |
| `SetTopicSubscribed(string, bool)` | 订阅或退订主题 |
| `QueuePushTaskAsync(FirebasePushTask, ...)` | 缓存业务 push task，并按配置批量发送 |
| `SetAnalyticsEnabled(bool)` | 控制 Analytics 收集开关 |
| `GetToken()` | 同步读取已缓存的 FCM Token |
| `GetAnalyticsInstanceId()` | 同步读取已缓存的 Analytics Instance ID |
| `IsInitialized` | 初始化完成标志 |
| `IsNotificationLaunch` | 本次启动是否由推送点击触发 |

## 3. 初始化语义

- `Priority => 30` 只参与稳定通知和逆序释放排序；SDKManager 按能力依赖调度初始化，不按 Priority 分桶。

- `OnInitializeAsync(...)` 会等待 `FirebaseApp.CheckAndFixDependenciesAsync()` 完成；只有依赖检查通过并成功注册 `FirebaseMessaging` 回调后才返回。
- `SDKPluginBase` 会在 `OnInitializeAsync(...)` 正常返回后把 `IsAvailable` 置为 `true`，因此 Firebase 插件的 `IsAvailable == true` 表示 Firebase 依赖已可用且 Messaging 事件已完成注册。
- 若依赖检查失败，`OnInitializeAsync(...)` 会抛出异常，由 `SDKManager` 按单插件失败隔离语义记录并保持该插件不可用。
- 依赖检查通过后，若 `FirebasePluginConfig.AutoRequestNotificationPermission` 为 `true`（默认值），插件会先调度一次异步 Fire-and-Forget 请求；实际通知权限请求会等待 SDK Manager 对应的全 SDK 初始化完成，并在启动前景稳定后才请求 `Alert | Sound | Badge` 通知权限。该请求不阻塞 Firebase 初始化完成回调；若项目希望由业务自行选择交互时机，可在配置中关闭该开关。
- 大多数公开方法都会在 `m_InitOver == false` 时直接静默返回。
- 默认 Topic 和显式 Topic 订阅会等待 `TokenReceived` 提供有效 FCM Token 后再调用 Firebase；iOS 首次安装时如果 Firebase topic API 返回 APNs Token 尚未就绪，会对订阅或退订操作本身做有界延迟重试。
- `GetTokenAsync(...)` 不依赖 `m_InitOver` 直接返回，而是等待 `m_TokenReceived` 非空。

依赖检查与 Token 等待没有统一的内置截止时间，调用方需要限制等待时须传入自己的令牌。Topic 操作对可恢复错误按 2/5/10/20/30 秒间隔重试，包含首次最多六次；默认 Topic 同步耗尽后还可能安排后续补偿。单次等待、某个 Topic 调用成功与完整默认 Topic 差异同步完成应分别判断，不能从初始化 Ready 推导订阅或后台推送送达。

释放会取消内部等待并退订 Messaging，但清理没有逐项 finally，某次取消或退订抛异常可打断后续步骤。已 Post 到主线程的 Token/Message 回调没有释放态门禁，仍可能更新缓存、发布槽或通知业务；业务 Token 事件也没有逐监听者异常隔离。插件不清 Token、标识与消息去重缓存，业务订阅须自行配对移除。`IsNotificationLaunch` 在处理后续推送点击时也可变 true，不只表示冷启动来源。Token 和标识原文可能进入现有 Debug 日志，收集诊断时须脱敏。

结束业务会话只换 session 标记，不清原厂用户 ID 或取消标识等待；登录上报等齐 Token、Analytics ID、国家码后才核一次 session，HTTP 已发出便不能撤回。自动权限请求在检测到不在前台时会退出，不等待下一次前台补发。

## 4. 配置与上报

当前配置类型是 `FirebasePluginConfig`，包含以下框架侧字段：

- `ReportCmdName`：登录后向业务服务器上报 Firebase 标识时使用的 NetCmd 名称
- `PushCmdName`：批量创建或取消服务端 push task 时使用的 NetCmd 名称
- `PushFlushIntervalSeconds`：push task 本地缓存后的时间发送阈值，默认 `100` 秒；小于等于 `0` 时写入后立即尝试发送
- `PushFlushBatchSize`：push task 本地缓存达到数量阈值后立即发送，默认 `5` 条；小于 `1` 时运行时按 `1` 处理
- `AutoRequestNotificationPermission`：SDK 全部插件初始化完成且应用仍在前台后是否自动请求 `Alert | Sound | Badge` 通知权限，默认开启；关闭后不会自动调用 Native 通知权限请求

上报链路依赖 Firebase 自身发布的两个内部数据槽位：

- `SDKDataKeys.FirebasePushToken`
- `SDKDataKeys.FirebaseAnalyticsInstanceId`

发送 `PbNetReportFirebaseReq` 时还会附带：

- `country`：通过 `IAdPlugin.GetCountryCodeAsync(...)` 获取；广告模块只负责等待运行时国家码和超时控制，最终为空、超时或 `IV` 时按空字符串上报
- `timezone_offset`：当前设备 UTC 偏移，使用服务端可读格式，例如 `+08:00`、`+05:30`、`-03:30`

`timezone_offset` 的协议格式包含 `+` 和 `:`，不能直接用于 Firebase Topic。默认时区 topic 仍使用 Firebase 安全格式，例如 `top_debug_timezone_utc_plus_08` 或 `top_release_timezone_utc_plus_08`。

## 5. Push Task 缓存与发送

业务层通过 `IFirebasePushTaskPlugin.QueuePushTaskAsync(...)` 写入 push task。插件会先将任务缓存到 `IFileFragmentManager`，分类名为 `FirebasePushTasks`，并使用 `FirebasePushTask.TaskKey` 作为 item 主键；后续相同 `TaskKey` 会覆盖旧缓存。

`FirebasePushTask` 字段约束：

| 字段 | 说明 |
|---|---|
| `TaskKey` | 业务自定义唯一主键；不能为空；相同 key 会覆盖本地旧缓存 |
| `TriggerTime` | 服务端任务触发时间，Unix 秒；仅创建任务时发送 |
| `Cancel` | 为 `true` 时取消同 `TaskKey` 下未派发的服务端任务 |
| `TemplateId` | 服务端消息模板 ID；仅创建任务时发送 |

发送触发规则：

- Firebase 真实初始化完成且 `SetUserId(...)` 已成功同步用户身份后，如果本地已有缓存，会立即尝试发送一次。
- 写入后从首条缓存开始计时，达到 `PushFlushIntervalSeconds` 后发送；后续写入不会重置计时。
- 缓存数量达到 `PushFlushBatchSize` 时立即发送。
- 应用从后台恢复前台时，会主动请求发送当前所有本地缓存；实际发送仍受 Firebase 初始化和用户身份就绪门槛保护。
- `PushCmdName` 为空时不会发送协议，本地缓存保留，并记录日志等待下次触发。

发送成功后才会删除本地缓存。每次写入都会记录 `int CacheVersion`，发送快照成功后只删除当前缓存仍匹配同一 `TaskKey` 与同一 `CacheVersion` 的条目；如果协议发送过程中业务又写入了相同 `TaskKey`，新版本会保留并在下一轮 flush 中继续发送。

当前 `PbPushTaskResult` 不返回 `task_key`，因此客户端按 `NetResponse<PbNetCreatePushTasksResp>.IsSuccess == true` 视为整批发送成功，不做单条结果删除。

数量阈值只决定何时 flush，每次快照发送全部有效缓存，并不限制为五条。缓存分类没有 UID 隔离，UserReady 也是布尔值；HTTP 头在发送时读取当前身份，跨账号项目须在业务层约束任务归属。同 key 写失败可能已经改变 Persist 内存；发送后删除又会忽略 Save 的布尔结果，取消删除或重启均可能重发，不能承诺恰好一次。CacheVersion 是本进程计数，不恢复历史最大值，服务端仍需稳定 TaskKey 幂等。

Flush 间隔的 NaN/Infinity 回退 100 秒，非正数为立即尝试；失败路径也复用该间隔，没有独立网络退避。Dispose 不等待旧发送循环结束，旧计时器/循环标志也没有完整代际隔离；外部保留调度器后再次调用可能重建服务。`FirebasePushTaskNetService.SetDebugMode` 当前只存字段，Async 没有消费它，不应据此判断请求已切换环境。

`FirebasePushTask.Cancel == true` 表示取消同 `TaskKey` 下尚未派发的服务端任务。协议层会只发送 `task_key` 与 `cancel`，不会携带 `trigger_time` 或 `template_id`；创建任务时才发送触发时间和模板 ID。若业务对象里同时填了 `TriggerTime` 或 `TemplateId`，底层协议构造也会忽略它们。

## 6. 默认推送 Topic

Firebase 依赖检查通过后，`FirebasePlugin` 会启动默认 FCM topic 同步，但实际 Topic 订阅会等待 `TokenReceived` 提供有效 FCM Token。iOS 上 FCM Token 字符串可用不等于 APNs Token 已被 Firebase 原生层用于 topic 操作；如果 `SubscribeAsync(...)` 或 `UnsubscribeAsync(...)` 返回 APNs Token 尚未就绪，或 Firebase 注册服务返回 `INTERNAL_SERVER_ERROR`，插件会延迟后重试当前 topic 操作。同步只在 Android / iOS 编译平台执行，WebGL 不包含 Firebase Runtime。

默认 Topic 前缀来自 `IConfigManager.DevelopMode`：`Debug` 使用 `top_debug_`，`Release` 使用 `top_release_`。如果 Config Manager 不存在或尚未完成加载，则按 `Debug` 处理，避免误订阅正式分群。

基础 topic 在 Firebase 初始化完成后开始同步：

| 维度 | Topic 示例 | 来源 |
|---|---|---|
| 全量 | `top_debug_all` / `top_release_all` | 固定值 + `IConfigManager.DevelopMode` |
| 语言 | `top_debug_lang_en` / `top_release_lang_zh-CN` | `LocalizationRefreshEventData.NewLanguage` 或已初始化的 `ILocalizationManager.Language` |
| 平台 | `top_debug_platform_iOS` / `top_release_platform_Android` | 编译平台 |
| 时区 | `top_debug_timezone_utc_plus_08` / `top_release_timezone_utc_plus_05_30` | `TimeZoneInfo.Local.GetUtcOffset(DateTime.Now)` |

Localization Manager 的 `LoadAsync()` 只准备支持语言和字体数据，不代表当前语言已初始化。Firebase 因此不会在 `ILocalizationManager.Language == Language.Unspecified` 时生成新的语言 topic；全量、平台和时区 topic 仍会先同步。等 `LocalizationRefreshEventData` 发布真实 `NewLanguage` 后，Firebase 再同步 `top_debug_lang_*` 或 `top_release_lang_*`，并通过存档差异退订旧语言 topic、订阅新语言 topic。若语言未就绪但旧存档里已有有效语言 topic，本轮基础同步会暂时保留旧语言 topic，避免启动早期误退订。

国家 topic 独立同步：

- 数据来源：SDK Manager 中可用的 `IAdPlugin.GetCountryCodeAsync(ct)`
- 等待上限：`AdPluginConfig.CountryCodeWaitTimeoutSeconds`
- 兜底来源：无；广告模块不保存国家码缓存，超时或无有效运行时国家码时返回空字符串
- 有效值：非空且不等于 `IV`
- Topic 示例：`top_debug_country_US` / `top_release_country_US`

Android、`zh-CN`、UTC+08、国家码 `CN` 的 Debug 分群会同步 `top_debug_all`、`top_debug_lang_zh-CN`、`top_debug_platform_Android`、`top_debug_timezone_utc_plus_08`、`top_debug_country_CN`；Release 分群对应 `top_release_all`、`top_release_lang_zh-CN`、`top_release_platform_Android`、`top_release_timezone_utc_plus_08`、`top_release_country_CN`。

所有默认 topic 都带 `top_debug_` 或 `top_release_` 前缀。时区中的 `utc` 固定小写；非整点时区会保留分钟字段，例如 `utc_plus_05_30`。旧版本保存的 `top_*` Topic 会在下一次同步时通过差异机制先退订，再订阅当前 DevelopMode 对应的新 Topic。

## 7. 默认 Topic 存档

默认 topic 状态通过 `IFileFragmentManager` 持久化，分类名为 `FirebaseDefaultTopics`：

| Item | DTO | 说明 |
|---|---|---|
| `BaseState` | `FirebaseTopicSubscriptionState` | 记录语言、平台、时区和上次成功订阅的基础 topic 列表 |
| `CountryState` | `FirebaseCountryTopicSubscriptionState` | 记录国家码和上次成功订阅的国家 topic |

同步时会先读取旧状态并与当前状态计算差异：旧状态独有的 topic 先退订，新状态独有的 topic 再订阅。只有所有退订/订阅操作都成功后才覆盖保存新状态；若 iOS APNs Token 尚未就绪或 Firebase 注册服务瞬时失败导致本轮未完成，则保留旧存档并安排后续补偿同步，应用恢复前台时也会再次请求默认 topic 差异同步。若当前状态和存档一致，则不重复调用 Firebase 订阅接口。启动基础同步和 Localization 刷新触发的语言同步共用同一把内部锁，避免并发读写 `BaseState`。

国家码最终无效或为 `IV` 时，不会订阅国家 topic，也不会退订旧国家 topic 或覆盖旧国家存档。这样可以避免广告 SDK 临时返回 `IV` 或国家码暂不可用时误删上一次有效国家订阅。

Topic 差异逐项先退订再订阅，部分完成后失败不会回滚；状态保存忽略 Save 的布尔结果，日志不能证明落盘。基础同步有串行锁，国家同步没有同样的锁。国家码只做 Trim/大写/安全字符规范化并排除空值与 `IV`，没有严格验证 ISO 两字母；安全段替换可能碰撞，也没有长度校验。显式 Topic 接口不自动 Trim，非移动编译分支的空操作成功不作为平台订阅证据；原厂 Topic Task 等待本身也不保证随调用令牌立刻取消。

## 8. 使用示例

```csharp
FirebasePlugin firebase = /* 已从 SDKComponent / SDKManager 取得插件实例 */;

firebase.TrackEvent("level_start", new Dictionary<string, object>
{
    ["level_id"] = 3,
    ["source"] = "main_menu",
});

PushToken token = await firebase.GetTokenAsync();
firebase.SetTopicSubscribed("global_notice", true);

IFirebasePushTaskPlugin pushTasks = firebase;
await pushTasks.QueuePushTaskAsync(new FirebasePushTask
{
    TaskKey = "daily_reward",
    TriggerTime = DateTimeOffset.UtcNow.AddHours(8).ToUnixTimeSeconds(),
    TemplateId = 1001,
});

await pushTasks.QueuePushTaskAsync(new FirebasePushTask
{
    TaskKey = "daily_reward",
    Cancel = true,
});
```

## 9. 关联

Editor 桌面库 Guard 只检查固定版本文件是否存在，每个 Session 一次，不识别 LFS 指针或证明原生库可加载；退出 Play 的既有修复还会按 `FirebaseHandler` 对象名清理，并非精确实例认证。iOS 构建处理器注入的 `aps-environment=development` 与最终签名产物分开核验。`SetUserProperty` 当前直接透传 key/value，不实现 XML 所述的空参数过滤；业务应在调用边界校验。

- 配置类型：[FirebasePluginConfig.md](./FirebasePluginConfig.md)

Android构建规则由内部纯`CreateAndroidManifestRules()`集中构造，正常SDK回调与框架内部叶子预测共用原MessageForwardingService及default_notification_icon完整规则树，不复制转换算法。原回调仍在原位置注册`MessagingUnityPlayerActivity`并AddManifestRules，保持不读取运行时Firebase配置的原Android行为。该纯入口只产生领域数据，不初始化Firebase、不执行构建回调或写文件；叶子预测和离线编译不代表实际Android SDK/Player构建成功。
