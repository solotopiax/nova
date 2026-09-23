# WechatMiniGameDemo

该示例先执行“微信登录校验”获取 `OpenID / UnionID`，再执行“登录游戏服务器”查询已有绑定；未绑定时使用 TGA DeviceID 登录游客账号并调用 GameBind。随后展示运行环境、支付、查单验单、订阅消息、隐私、剪贴板、振动、分享和微信生命周期事件。

## 运行

1. 先安装 `com.solotopia.nova.framework.kit.network.gamelogin`、`com.solotopia.nova.framework.kit.network.gamebind` 和 `com.solotopia.nova.framework.sdk.tga`；它们是本 Sample 的演示依赖，不是微信 SDK Runtime 的强制依赖。
2. 在 Unity 中切换到 WebGL。
3. 打开 `Assets/Samples/WechatMiniGameDemo/WechatMiniGameDemo.unity`。
4. 在 Editor 中可以预览页面；微信登录、支付等平台功能会跳过并显示提示，不会抛出初始化错误。
5. 使用微信小游戏转换工具导出，并在微信开发者工具或真机中体验真实功能。

Demo 在首次启动的 `ProcedurePreload.OnLeave`、主资源加载完成后，通过 `WeChatMiniGamePlugin.ReportGameStart()` 单次调用原厂 `WX.ReportGameStart()`。场景中的 `DebugComponent` 同时绑定了 Sample 自带的中文 Font，避免 RuntimeDebugger 在微信小游戏中因内置 Arial 缺少中文字形而只显示英文。

微信 AppID 在 ConfigWindow 的“微信小游戏”SDKConfig 中维护；Pipify 导出 Step 不提供参数区，输出目录、Development Build、CDN 及其他转换选项直接读取官方 `MiniGameConfig.asset`。

账号流程为：

```text
TGA 初始化（DeviceID） -> 微信 SDK 初始化
-> wx.login -> 业务服务端校验 code -> OpenID / UnionID
-> GameLogin(uid="", openid=OpenID, forceNewAccount=false) 请求游戏服注册/登录
   -> 成功：进入已绑定账号
   -> 10404 未绑定：GameLogin(uid="", openid="", forceNewAccount=false)
                    -> 按 DeviceID 登录/创建游客
                    -> GameBind(Wechat, OpenID)
                    -> 必要时处理绑定冲突
   -> 其他失败：按错误原因处理，不自动注册新账号
```

TGA `Priority=10`，微信 Plugin `Priority=20`，Nova SDKManager 按 Priority 分批初始化，因此网络登录前可由 `NetBuilder` 从 TGA 的 `IDeviceIdProvider` 自动写入 DeviceID。两次 GameLogin 都保持 `forceNewAccount=false`：第一次用 OpenID 查询绑定账号；只有明确返回 `10404` 时，第二次才按 DeviceID 优先登录已有游客，不存在才创建。其他失败不会自动注册新账号。GameLogin 不产生绑定副作用，GameBind 只建立或裁决绑定关系。绑定冲突时 Demo 只提示继续 `QueryConflictAsync + ResolveAsync`，不自动选择账号。

`GameAccountLogin`、`GameAccountBind`、`GameAccountBindConflict`、`GameAccountBindResolve` 和 `TGAReport` 都同时维护在 Sample 的 `NetworkCmds.xlsx` 表源、导出的 `NetworkCmds.json` 和生成类型中。

## 连接游戏业务服务器

登录校验、支付验单、消息订阅和文本内容安全涉及账号、资金或平台凭据，必须明确客户端与服务端边界。接入时：

1. 游戏服务器按包内 `Nova/Protos/pb_net_wechat_minigame.proto` 实现微信登录校验、道具签名、微信查单验证、当前用户全部订单查询、订阅结果登记和文本内容安全检查。
2. 在项目的 HostKey/NetCmd 表中将六个固定协议名指向对应服务端接口，并正式导出表数据。
3. 在 Config 的微信小游戏配置填写真实 `MidasOfferId`，并设置 Demo 的 `CurrencyProductId`、`ItemProductId`；需要消息订阅时再设置 `SubscriptionTemplateIds`。

客户端无需实现或注入 `IWeChatMiniGameBackend`。Nova 会随 Plugin 自动创建默认 Backend；服务端或路由未完成时请求会返回明确错误，不会伪造登录或支付成功。

文本安全使用 `WeChatMiniGamePlugin.CheckTextContentAsync`，客户端只上传待检查内容与业务场景。微信 `access_token` 由服务端持有并调用微信安全接口，禁止下发到客户端。

业务服务端必须完成 `code2Session`，并向客户端只返回 `OpenID / UnionID`，不在这一步返回游戏 UID。支付订单号、订单快照、状态迁移和逐笔补单由 Nova 客户端统一管理；服务端只校验道具签名参数、向微信验单并返回 `CanDeliver`，不创建或保存客户端订单，也不执行发货。客户端仍不得保存 AppSecret、session_key、access token 或 Midas 签名密钥。

支付按钮在 Android/iOS 复用同一条业务流程，并同时覆盖游戏币和道具直购。iOS 不包含客服会话充值；仅在 `wx.checkIsSupportMidasPayment` 返回允许且 `env=0` 时拉起支付。订单会在调用微信前按游戏 UID 写入 Nova Persist；无论微信前端回调成功、失败或超时，最终发货资格均以服务端验单为准。

Demo 实现了 `IWeChatMiniGameFulfillmentHandler` 以展示接入点，但不会真的增加游戏资产。正式项目必须把 `OrderId` 去重记录与资产变更一起写入业务存档；重复调用同一订单必须返回成功且不能重复发货。发货成功或订单明确关闭后，Nova 才删除本地未完成订单。清缓存、卸载和本地存档篡改无法由纯客户端方案完全恢复，服务端仍应保留支付审计、退款和对账能力。

普通浏览器和 Unity Editor 中会跳过微信 SDK 初始化并打印一条警告，这是平台隔离的预期行为。分享、隐私授权和写剪贴板等能力必须由按钮点击直接触发，以满足微信的用户手势要求。
