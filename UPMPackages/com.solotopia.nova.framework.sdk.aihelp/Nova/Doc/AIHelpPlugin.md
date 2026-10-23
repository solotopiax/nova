# AIHelpPlugin

`AIHelpPlugin` 是 Nova 对 AIHelp 智能客服 / 帮助中心 SDK 的对接层入口，继承框架 `SDKPluginBase`，由 `SDKManager` 统一编排初始化与生命周期。业务侧统一通过 `Nova.SDK.TryGet(out AIHelpPlugin plugin)`（或 `SDKManager.Get<AIHelpPlugin>()`）获取实例并调用，**不需要也没有公开的 `Initialize` 方法**——初始化由 `SDKManager` 在启动流程中读取配置后自动完成。

**命名空间**：`NovaFramework.SDK.AIHelp.Runtime`。插件实现 `ISDKLoginReceiver`，`Name` 返回 `"AIHelp"`；配置类型由 `ConfigType => typeof(AIHelpPluginConfig)` 声明。

## 接入步骤

1. 安装本包（AIHelp Unity SDK 6.0 已随包 bundle，无需额外安装原厂包）。
2. 在 ConfigMaster 中启用 `AIHelpPluginConfig`，填入 netcmd 指令名 `ServerCmdName`（如 `AIHelpServerUrl`，运行时由框架 `INetworkManager` 按该指令名从 netcmd 表解析出完整 URL 并取域名传给 AIHelp）与 AIHelp 后台分配的 `AppId`，以及可选的 `InitialLanguage` / `EnableLogging`。
3. `SDKManager` 按插件声明的配置类型自动拉取并注入。`ServerCmdName` / `AppId` 缺失、指令未找到或域名解析失败时，插件记录 Warning 并抛出 `InvalidOperationException`；Manager 隔离该插件失败，状态为 `Failed`，`IsAvailable` 不会置为 true。成功调用原厂初始化并注册监听后，才完成本地初始化。
4. 在已有启动链中等待 `Nova.SDK.WaitForPluginAsync<AIHelpPlugin>(ct)` 并确认 `SDKPluginInitializationState.Ready`，再通过 `TryGet` 获取实例；`GetSDKVersion()` 非空可作为本地初始化探针。客服后台配置与页面真正可用仍需以已授权的 `Show(entranceId)` 返回值及平台回调验证。
5. 游戏登录成功后通过 `Nova.SDK.Login(uid, userProperties)` 交付账号；Manager 在插件就绪后调用 `ISDKLoginReceiver.OnSDKLogin`，AIHelp 自动同步 UID。已有 GameLogin 路径会发出通知，不重复调用；需要 `name` / `serverId` / 标签 / 自定义数据时，业务再按需补充富信息。

## 后台配置（`AIHelpPluginConfig`）

| 字段 | 说明 |
| --- | --- |
| `ServerCmdName` | NetCmd 指令名（如 `AIHelpServerUrl`）。由 `INetworkManager` 解析完整 URL，移除 `http://` / `https://` 并截去路径后传入原厂初始化。未设置默认指令名；缺失或域名解析失败导致初始化失败。 |
| `AppId` | AIHelp 后台为当前应用分配的 App ID，传入原厂初始化。未设置默认值；缺失导致初始化失败。 |
| `InitialLanguage` | 初始语言码（如 `en`、`zh-CN`）。留空则用 AIHelp 默认；运行时可经 `SetLanguage` 切换。 |
| `EnableLogging` | 是否开启原厂 SDK 日志，默认 false；开发期按需开启。 |

`ServerCmdName` 对应的 netcmd 指令需提前在 netcmd 表中配置好指向 AIHelp 域名的 Host；`AppId` 由 AIHelp 后台控制台为对应应用创建后分配，不是本包自定义的值；`entranceId`（见下）同样在 AIHelp 后台创建入口后获得，用于区分不同场景（如登录页客服入口、设置页客服入口等）。

## 公开方法

| 方法 | 说明 |
| --- | --- |
| `bool Show(string entranceId, string welcomeMessage = null)` | 拉起在线客服 / 帮助中心页面；`welcomeMessage` 仅在线客服页面有效。返回是否成功拉起。 |
| `void Login(string uid, string name = null, string serverId = null, List<string> userTags = null, string customDataJsonString = null)` | 同步用户信息给 AIHelp。Manager 通过 `OnSDKLogin` 自动同步 UID；需要富信息时业务按需调用。`userTags` 需提前在 AIHelp 后台配置；`customDataJsonString` 格式为 `{"key":"value"}`。 |
| `void OnSDKLogin(string userId, long sessionId)` | Manager 的账号交付入口；非空 UID 调用 `Login(userId)`。业务使用统一 `Nova.SDK.Login`，不直接调用该生命周期入口。 |
| `void UpdateUserInfo(string name = null, string serverId = null, List<string> userTags = null, string customDataJsonString = null)` | 更新用户信息，不改变登录态。 |
| `void ResetUserInfo()` | 重置用户信息；用户退出登录时调用，避免残留上一账号信息。 |
| `void SetLanguage(string languageCode)` | 切换 AIHelp 展示语言，如 `en`、`zh-CN`；为空则忽略。 |
| `void ShowSingleFAQ(string faqId, AIHelp.ConversationMoment moment)` | 展示单条 FAQ；`moment` 控制是否/何时展示进入会话入口。 |
| `void ShowUrl(string url)` | 以 AIHelp 内置浏览器展示指定 URL。 |
| `void FetchUnreadMessageCount()` | 主动查询未读消息数；结果经 `OnMessageArrived` 事件异步回显，vendor 内部有频率限制。 |
| `void FetchUnreadTaskCount()` | 主动查询未读工单数；结果经 `OnUnreadTaskCountChanged` 事件异步回显。 |
| `void SetPushToken(string pushToken, AIHelp.PushPlatform platform)` | 设置推送 token 与平台。插件不依赖 Firebase，`pushToken` 由业务自行获取后传入。 |
| `void SetUploadLogPath(string logPath)` | 设置上传日志文件路径（Persistent 下绝对路径），目前仅支持 `.log` / `.bytes` / `.txt` / `.zip`。 |
| `bool IsShowing()` | AIHelp 页面是否正在展示中；未初始化返回 `false`。 |
| `string GetSDKVersion()` | 获取 AIHelp SDK 版本号；未初始化返回空串。 |
| `void Close()` | 关闭当前 AIHelp 页面。 |

业务方法在原厂初始化未完成、内部 `m_InitOver=false` 时早退；`Show` / `IsShowing` 返回 false，`GetSDKVersion` 返回空串。空入口 ID、FAQ ID、URL、语言码、Token 或日志路径也会由各入口忽略。Config 错误会使 Manager 标记 Failed，不能把整体 SDK 初始化结束当作 AIHelp 成功。Ready 和版本只能证明项目内初始化，不能证明后台入口、推送或客服通信可用。

## 事件

> 两个事件均为 `event Action<string>`，参数是原厂原样回传的 JSON 字符串，插件不做二次解析。原厂回调通过 `UniTask.Post` 切到 Unity 主线程再交付；订阅方按生命周期对称退订，不把原始用户内容写入日志。不存在按 `int` 传数量的 `OnUnreadMessageCountChanged` 事件。

| 事件 | 触发时机 |
| --- | --- |
| `event Action<string> OnMessageArrived` | vendor `EventType.MessageArrival` 异步回调触发，通常在收到新消息、或调用 `FetchUnreadMessageCount()` 后收到查询结果时触发。 |
| `event Action<string> OnUnreadTaskCountChanged` | vendor `EventType.UnreadTaskCount` 异步回调触发，通常在未读工单数变化、或调用 `FetchUnreadTaskCount()` 后收到查询结果时触发。 |

订阅示例：

```csharp
if (Nova.SDK.TryGet(out AIHelpPlugin plugin))
{
    // 页面启用时订阅命名处理器；页面停用时用 -= 对称退订。
    plugin.OnMessageArrived += HandleMessageArrived;
    plugin.OnUnreadTaskCountChanged += HandleUnreadTaskCountChanged;
}
```

## 自动登录

`AIHelpPlugin` 实现 `ISDKLoginReceiver`，由 Manager 在统一登录通知时交付 UID；初始化晚于登录时会补交当前账号，不自行订阅 `SDKEventData.UserLogin`。业务无需再手动 `plugin.Login(uid)`；富信息按需用 `Login` 或 `UpdateUserInfo` 补充。插件当前不实现 `ISDKLoginSessionEndReceiver`，因此项目登出除统一 `Nova.SDK.EndLoginSession()` 外，仍需在已就绪 AIHelp 实例上调用 `ResetUserInfo()` 清理原厂用户状态。

## 释放与已排队回调

释放会注销两类原厂监听并关闭客服页，但不会把 `m_InitOver` 清为 false，也不会清空业务事件委托或取消已提交的 `UniTask.Post`。因此已持有的旧插件引用不能靠普通方法的初始化判断确认仍可用；业务随 SDKManager 生命周期重新获取实例，并在离开页面时主动退订事件。已排队回调可能在原厂监听注销后才进入主线程，事件订阅者异常也没有逐个隔离。

原厂监听按 EventType 注销，而不是按本插件的准确委托注销，可能影响同一原厂事件的其他注册；注销或关闭异常也没有整体 finally 保证剩余步骤完成。初始化在注册监听前已经设置本地完成标志，注册失败仍可能留下该标志，查询方法不能替代 Manager 的 Ready 检查。

## 输入与平台构建边界

业务入口不自动切换主线程。可选用户字段只在非 null 时设置；标签直接用逗号拼接，不过滤或转义，附加 JSON 不在本层解析。语言码、FAQ、日志路径和 URL 只做各自的非空门禁，不验证后台存在、文件存在或 URI 合法性。初始化的域名提取仅剥离 HTTP(S) 前缀并截去路径，不是通用 URI 解析器；参数应由项目预先核对。

Android Gradle 导出回调不以 Runtime Config 的启用状态作为门禁：它更新自己标记的依赖块，再为父级 `gradle.properties` 补缺失的 AndroidX/Jetifier 键；既有 false 不覆盖。缺文件或找不到精确依赖块可以 Warning 后返回，IO 异常继续传播，不证明 Gradle 解析成功。iOS 后处理为存在的两个目标追加 `-ObjC`，最终 Xcode 编译和原厂页面可用性仍需平台验收。

## 推送 Token

本插件不集成 Firebase 或任何推送 SDK；`SetPushToken(pushToken, platform)` 的 `pushToken` 完全由业务侧通过自己接入的推送方案（如 Firebase Cloud Messaging、APNs）获取后传入，插件只负责把它转交给 vendor `AIHelpSupport.SetPushTokenAndPlatform`。

## 与官方 SDK 的关系

本插件基于随包 bundle 的 AIHelp Unity SDK 6.0（`Core/AIHelp/`）封装，调用 vendor API 时统一使用 `global::AIHelp.*` 全限定名以避免命名空间遮蔽。vendor 层的初始化参数、`EventType` 枚举等底层概念见 [官方SDK技术文档.md](./官方SDK技术文档.md)。
