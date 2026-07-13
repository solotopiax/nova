# AIHelpPlugin

`AIHelpPlugin` 是 Nova 对 AIHelp 智能客服 / 帮助中心 SDK 的对接层入口，继承框架 `SDKPluginBase`，由 `SDKManager` 统一编排初始化与生命周期。业务侧统一通过 `Nova.SDK.TryGet(out AIHelpPlugin plugin)`（或 `SDKManager.Get<AIHelpPlugin>()`）获取实例并调用，**不需要也没有公开的 `Initialize` 方法**——初始化由 `SDKManager` 在启动流程中读取配置后自动完成。

## 接入步骤

1. 安装本包（AIHelp Unity SDK 6.0 已随包 bundle，无需额外安装原厂包）。
2. 在 ConfigMaster 中启用 `AIHelpPluginConfig`，填入 netcmd 指令名 `ServerCmdName`（如 `AIHelpServerUrl`，运行时由框架 `INetworkManager` 按该指令名从 netcmd 表解析出完整 URL 并取域名传给 AIHelp）与 AIHelp 后台分配的 `AppId`，以及可选的 `InitialLanguage` / `EnableLogging`。
3. `SDKManager` 在应用启动流程中按 `AIHelpPlugin.RequiredConfigType` 自动拉取该配置并调用初始化；`ServerCmdName` / `AppId` 缺失、或 `ServerCmdName` 对应的 netcmd 指令未找到 / 解析不出域名时，插件都会记 Warning 并早退跳过初始化，但不抛异常，因此框架契约下 `IsAvailable` 仍为 `true`（详见框架 `SDKPluginBase`：`OnInitializeAsync` 不抛异常即置 `IsAvailable=true`）——此时插件内部 `m_InitOver` 为 `false`，后续所有公开方法早退为空操作。业务应确保 `ServerCmdName` / `AppId` 配置就绪且对应 netcmd 指令可解析，不能仅凭 `IsAvailable` 判断 AIHelp 是否真正可用。
4. 业务侧通过 `Nova.SDK.TryGet(out AIHelpPlugin plugin)` 拿到实例后按需调用 `Show(entranceId)` 等方法。
5. 用户登录后无需手动调用 `Login`：插件已自动订阅框架 `SDKEventData.UserLogin`，登录时以 `uid` 自动同步给 AIHelp；需要携带 `name` / `serverId` / 标签 / 自定义数据时，业务再显式调用 `Login` 重载补充富信息。

## 后台配置（`AIHelpPluginConfig`）

| 字段 | 说明 |
| --- | --- |
| `ServerCmdName` | netcmd 指令名（如 `AIHelpServerUrl`）。运行时由框架 `INetworkManager` 按该指令名从 netcmd 表解析出完整 URL，去掉 `https://` 后取域名部分传入 vendor `Initialize`。必填，缺失或解析失败则跳过初始化。 |
| `AppId` | AIHelp 后台为当前应用分配的 App ID，传入 vendor `Initialize`。必填，缺失则跳过初始化。 |
| `InitialLanguage` | 初始语言码（如 `en`、`zh-CN`）。留空则用 AIHelp 默认；运行时可经 `SetLanguage` 切换。 |
| `EnableLogging` | 是否开启 vendor SDK 日志，开发期建议勾选。 |

`ServerCmdName` 对应的 netcmd 指令需提前在 netcmd 表中配置好指向 AIHelp 域名的 Host；`AppId` 由 AIHelp 后台控制台为对应应用创建后分配，不是本包自定义的值；`entranceId`（见下）同样在 AIHelp 后台创建入口后获得，用于区分不同场景（如登录页客服入口、设置页客服入口等）。

## 公开方法

| 方法 | 说明 |
| --- | --- |
| `bool Show(string entranceId, string welcomeMessage = null)` | 拉起在线客服 / 帮助中心页面；`welcomeMessage` 仅在线客服页面有效。返回是否成功拉起。 |
| `void Login(string uid, string name = null, string serverId = null, List<string> userTags = null, string customDataJsonString = null)` | 同步用户信息给 AIHelp。框架已在 `SDKEventData.UserLogin` 时自动以 `uid` 调用；需要携带富信息时业务显式调用本重载。`userTags` 需提前在 AIHelp 后台配置对应标签；`customDataJsonString` 格式为 `{"key":"value"}`。 |
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

以上方法在插件未成功初始化（`ServerCmdName` / `AppId` 缺失、netcmd 域名解析失败、或尚未完成 `OnInitializeAsync`，内部 `m_InitOver` 为 `false`）时全部早退为空操作，`Show` / `IsShowing` 返回 `false`，`GetSDKVersion` 返回空串。注意此时 `IsAvailable` 按框架契约仍为 `true`，不能作为“AIHelp 是否真正可用”的判断依据。

## 事件

> 两个事件均为 `event Action<string>`，参数是 **vendor 原样回传的 JSON 字符串**，插件不做二次解析，由业务自行按需解析。不存在按 `int` 传数量的 `OnUnreadMessageCountChanged` 事件。

| 事件 | 触发时机 |
| --- | --- |
| `event Action<string> OnMessageArrived` | vendor `EventType.MessageArrival` 异步回调触发，通常在收到新消息、或调用 `FetchUnreadMessageCount()` 后收到查询结果时触发。 |
| `event Action<string> OnUnreadTaskCountChanged` | vendor `EventType.UnreadTaskCount` 异步回调触发，通常在未读工单数变化、或调用 `FetchUnreadTaskCount()` 后收到查询结果时触发。 |

订阅示例：

```csharp
if (Nova.SDK.TryGet(out AIHelpPlugin plugin))
{
    plugin.OnMessageArrived += json => Debug.Log($"OnMessageArrived: {json}");
    plugin.OnUnreadTaskCountChanged += json => Debug.Log($"OnUnreadTaskCountChanged: {json}");
}
```

## 自动登录

`AIHelpPlugin` 在 `OnInitializeAsync` 中会自动订阅框架事件管理器的 `SDKEventData.UserLogin`：用户登录时插件取 `login.UserId` 并自动调用 `Login(uid)` 完成用户身份同步，业务无需在登录流程里手动调用一次 `Login`。若需要携带 `name` / `serverId` / `userTags` / `customDataJsonString` 等富信息，业务应在自动登录之后（或任意时机）显式调用带参数的 `Login` 重载补充，或改用 `UpdateUserInfo` 更新不改变登录态的信息。用户退出登录时，业务应调用 `ResetUserInfo()` 清除 AIHelp 侧的登录用户信息。

## 推送 Token

本插件不集成 Firebase 或任何推送 SDK；`SetPushToken(pushToken, platform)` 的 `pushToken` 完全由业务侧通过自己接入的推送方案（如 Firebase Cloud Messaging、APNs）获取后传入，插件只负责把它转交给 vendor `AIHelpSupport.SetPushTokenAndPlatform`。

## 与官方 SDK 的关系

本插件基于随包 bundle 的 AIHelp Unity SDK 6.0（`Core/AIHelp/`）封装，调用 vendor API 时统一使用 `global::AIHelp.*` 全限定名以避免命名空间遮蔽。vendor 层的初始化参数、`EventType` 枚举等底层概念见 [官方SDK技术文档.md](./官方SDK技术文档.md)。
