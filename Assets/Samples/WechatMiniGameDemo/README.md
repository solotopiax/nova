# WechatMiniGameDemo

该示例的“微信登录校验”按钮会连续执行 `wx.login → 微信身份校验 → GameLogin`；未绑定时继续使用 TGA DeviceID 登录游客账号并调用 GameBind。页面同时展示运行环境、支付、全部订单及状态、补单、订阅消息、文本安全、隐私、剪贴板、振动、分享和微信生命周期事件。

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

示例的 `ProcedurePreload` 会等待 `Nova.SDK.InitializeTask` 完成全部已启用插件的初始化；TGA 就绪后，网络登录请求可由 `NetBuilder` 从其 `IDeviceIdProvider` 自动写入 DeviceID。`Priority` 只控制稳定遍历和释放顺序，不保证初始化先后。两次 GameLogin 都保持 `forceNewAccount=false`：第一次用 OpenID 查询绑定账号；只有明确返回 `10404` 时，第二次才按 DeviceID 优先登录已有游客，不存在才创建。其他失败不会自动注册新账号。GameLogin 不产生绑定副作用，GameBind 只建立或裁决绑定关系。绑定冲突时 Demo 只提示继续 `QueryConflictAsync + ResolveAsync`，不自动选择账号。

`GameAccountLogin`、`GameAccountBind`、`GameAccountBindConflict`、`GameAccountBindResolve` 和 `TGAReport` 都同时维护在 Sample 的 `NetworkCmds.xlsx` 表源、导出的 `NetworkCmds.json` 和生成类型中。

## 连接游戏业务服务器

登录校验、支付验单、消息订阅和文本内容安全涉及账号、资金或平台凭据，必须明确客户端与服务端边界。接入时：

1. 游戏服务器按包内 `Nova/Protos/pb_net_wechat_minigame.proto` 实现微信登录校验、道具签名、最多 20 笔批量验单、当前用户全部订单查询、个人通知任务创建和文本内容安全检查。
2. 在项目的 HostKey/NetCmd 表中将六个固定协议名指向对应服务端接口，并正式导出表数据。
3. 在 Config 的微信小游戏配置填写真实 `MidasOfferId`，并设置 Demo 的 `CurrencyProductId`、`ItemProductId`。订阅通知按钮上方可填写模板 ID、服务端通知配置 ID、触发 Unix 秒（`0` 表示立即）及稳定任务幂等键；失败重试时应复用同一键。文本安全按钮上方可填写待检查文本。

客户端无需实现或注入 `IWeChatMiniGameBackend`。Nova 会随 Plugin 自动创建默认 Backend；服务端或路由未完成时请求会返回明确错误，不会伪造登录或支付成功。

文本安全使用 `WeChatMiniGamePlugin.CheckTextContentAsync`，客户端只上传待检查内容与业务场景。微信 `access_token` 由服务端持有并调用微信安全接口，禁止下发到客户端。

业务服务端必须完成 `code2Session`，并向客户端只返回 `OpenID / UnionID`，不在这一步返回游戏 UID。支付订单号、订单快照、状态迁移和补单由 Nova 客户端统一管理；每批最多提交 20 笔服务端验单，得到 `CanDeliver` 后逐笔幂等发货。服务端不创建或保存客户端订单，也不执行发货。客户端仍不得保存 AppSecret、session_key、access token 或 Midas 签名密钥。

支付按钮在 Android/iOS 复用同一条业务流程，并同时覆盖游戏币和道具直购。iOS 不包含客服会话充值；仅在 `wx.checkIsSupportMidasPayment` 返回允许且 `env=0` 时拉起支付。游戏币还必须在 ConfigWindow 配置与微信平台、服务端一致的“每元游戏币数量”；本 Sample 的 WebGL/WeChat 开发与正式配置已按 Solar 表填写为 10，正式项目仍须核对自身商户汇率。订单会在调用微信前按游戏 UID 写入 Nova Persist；无论微信前端回调成功、失败或超时，最终发货资格均以服务端验单为准。

iOS 游戏币在 Solar 旧实现中使用 `requestMidasPaymentGameItem` 的 `mode=coins`，当前 Nova 使用统一的 `requestMidasPayment` `mode=game`；二者的现网适用条件和签名协议尚未完成微信真机、实际商户验证，因此不能把“能力检测允许”当作“iOS 充值已验收”。

Demo 实现了 `IWeChatMiniGameFulfillmentHandler` 以展示接入点，但不会真的增加游戏资产。正式项目必须把 `OrderId` 去重记录与资产变更一起写入业务存档；重复调用同一订单必须返回成功且不能重复发货。发货成功或订单明确关闭后，Nova 才删除本地未完成订单。清缓存、卸载和本地存档篡改无法由纯客户端方案完全恢复，服务端仍应保留支付审计、退款和对账能力。

Demo 的发货回调会醒目打印“客户端已发货／模拟增加资产”并返回成功，不修改真实资产。单笔手动补单默认选当前账号最近创建的本地未完成订单，不依赖本次页面会话；查询当前用户订单则逐笔显示服务端返回的完整订单快照与状态。微信小游戏输入框使用 `WeChatMiniGameTmpInputBridge` 接管软键盘事件，普通 Editor 和浏览器 WebGL 仍使用默认 TMP 输入；微信开发者工具和真机的输入、触摸及实际通知送达仍需联调验收。

ConfigWindow 的 WebGL/WeChat 开发与正式配置已按 Solar 的 `Configs_China.xlsx` 填写引力 WebGL 项目 Access Token、沉默唤起周期 7 天，并分别使用调试/正常模式；引力物料在微信配置字段之后展示。当前导出的 `ConfigRuntime.asset` 对应开发模式。SDK 只在微信 OpenID 经业务服务端校验后启动，并仅在原厂 Initialize 成功回调后视为可用。客户端集成版本见包内 `LOCAL_VERSION.md`。项目还须将 `https://backend.gravity-engine.com` 和 `https://api.gravity-engine.com` 加入微信小游戏合法域名，并按业务隐私策略决定何时启用埋点。

微信配置中的 DataNexus 数据源 ID 默认为 `0`、SDK Key 默认为空，开关默认关闭；这两项表示未配置。项目取得正式 DataNexus 物料后，在开发、正式坐标分别填写并启用，再导出微信小游戏。开关关闭或物料无效时导出会跳过注入；有效时 game.js 会携带该客户端 SDK Key，切勿误填服务端密钥，并须配置 https://api.datanexus.qq.com 为合法请求域名。独立微信热力引擎 WXSEAppKey 不属于此 DataNexus 接线。

普通浏览器和 Unity Editor 中会跳过微信 SDK 初始化并打印一条警告，这是平台隔离的预期行为。分享、隐私授权和写剪贴板等能力必须由按钮点击直接触发，以满足微信的用户手势要求。
