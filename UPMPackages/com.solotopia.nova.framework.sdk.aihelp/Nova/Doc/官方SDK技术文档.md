# AIHelp 官方 SDK 技术要点（转述）

本文档转述 AIHelp Unity SDK 6.0 官方文档的技术要点，供理解底层行为与排查问题参考。Nova 对接层的日常业务接入请优先查阅 [AIHelpPlugin.md](./AIHelpPlugin.md)。

## 初始化

AIHelp SDK 使用前必须先初始化：

```csharp
AIHelpSupport.Initialize(domain, appId, language);
```

- `domain`：AIHelp 后台分配的域名标识。
- `appId`：AIHelp 后台分配的应用 ID。
- `language`：SDK 展示语言（可选，留空时跟随系统语言）。也存在不带 `language` 的重载。

## entranceId（入口 ID）

打开智能客服 / 帮助中心会话时需要指定一个「入口」：

```csharp
AIHelpSupport.Show(entranceId);
```

`entranceId` 在 AIHelp 后台创建入口时分配，用于区分不同接入场景（例如登录页客服入口、设置页客服入口），不同入口可在后台配置不同的知识库、欢迎语等展示内容。也支持通过 `ApiConfig`（Builder 模式）在打开时附带欢迎语等参数。

## 事件类型（EventType）

SDK 通过 `RegisterAsyncEventListener(EventType, listener)` 注册异步事件监听，事件类型（`AIHelp.EventType`）包括：

- `Initialization`：SDK 初始化完成
- `UserLogin`：用户登录
- `EnterpriseAuth`：企业认证
- `SessionOpen` / `SessionClose`：会话窗口打开 / 关闭
- `MessageArrival`：消息到达
- `LogUpload`：日志上传
- `UrlClick`：URL 点击
- `UnreadTaskCount`：未读工单数变化
- `ConversationStart`：会话开始（携带用户首条消息）

监听回调签名为 `void(string jsonEventData, Action<string> acknowledge)`，事件数据以 JSON 字符串传递，`acknowledge` 用于回执确认。

## 其他常用能力

- 用户登录 / 信息同步：`Login(userId)` / `Login(LoginConfig)`、`UpdateUserInfo(UserConfig)`、`ResetUserInfo()`。`UserConfig` 支持用户名、服务器 ID、用户标签、自定义数据（JSON 字符串）。
- 单条 FAQ 展示：`ShowSingleFAQ(faqId, ConversationMoment)`，`ConversationMoment` 控制何时引导用户转人工（从不 / 总是 / 仅在答案页 / 标记无帮助后）。
- 未读消息 / 工单数：`FetchUnreadMessageCount()` / `FetchUnreadTaskCount()`。
- 推送：`SetPushTokenAndPlatform(pushToken, PushPlatform)`，支持 APNS / Firebase / 极光 / 个推 / 华为 / OneSignal。
- 其它：`UpdateSDKLanguage`、`SetUploadLogPath`、`GetSDKVersion`、`IsAIHelpShowing`、`enableLogging`、`ShowUrl`、`AdditionalSupportFor(PublishCountryOrRegion)`（针对特定发行地区的附加支持）、`Close()`。

## 平台差异

- iOS：原生库随包分发在 `Core/Plugins/iOS/AIHelpSDK/`（`AIHelpSupportSDK.framework` + `AIHelpUnity.mm`/`.h` 桥接层）。
- Android：原生库不随包分发，构建期由本包 `AIHelpBuildProcessor`（`IPostGenerateGradleAndroidProject`）把 Maven 坐标 `net.aihelp:android-aihelp-aar:6.0.+` 注入导出后的 Android gradle 工程（`unityLibrary/build.gradle`）；不经 EDM4U，包内也无 Dependencies.xml。
- managed C# 封装层（`Core/AIHelp/`）按平台内部分发到 `Core/AIHelp/Core/iOS/` 与 `Core/AIHelp/Core/Android/`，由 `AIHelpCore` 统一路由。
