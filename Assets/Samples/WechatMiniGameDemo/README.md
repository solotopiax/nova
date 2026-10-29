# WechatMiniGameDemo

该示例的“微信登录校验”按钮会连续执行 `wx.login → 微信身份校验 → GameLogin`；未绑定时继续使用 TGA DeviceID 登录游客账号并调用 GameBind。页面同时展示运行环境、支付、全部订单及状态、补单、订阅消息、文本安全、隐私、剪贴板、振动、分享和微信生命周期事件。

## 运行

1. 先安装 `com.solotopia.nova.framework.kit.network.gamelogin`、`com.solotopia.nova.framework.kit.network.gamebind` 和 `com.solotopia.nova.framework.sdk.tga`；它们是本 Sample 的演示依赖，不是微信 SDK Runtime 的强制依赖。
2. 在 Unity 中切换到 WebGL。
3. 打开实际导入目录中的 `WechatMiniGameDemo.unity`。
4. 在 Editor 中可以预览页面；微信 SDK 的平台初始化会跳过，平台操作按具体 API 提示不支持或不可用。仍须核对其它已启用插件、配置、资源与启动状态，不能保证任意工程均无初始化错误。
5. 使用微信小游戏转换工具导出，并在微信开发者工具或真机中体验真实功能。

导入的配置与页面直接使用随包资产，开发仓的自动生成工具不会在消费工程中重建或覆盖它们。公开配置中的密钥和厂商物料保留脱敏占位值；首次运行前，在 ConfigWindow 的当前坐标填写自己的隐私 AES-Key / AES-IV（UTF-8 各 16 字节），保存并导出运行时配置。Editor 页面预览不代表微信登录、支付或后台已可用。

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
   -> 其他失败：按错误原因处理，不自动注册新账号
```

示例的 `ProcedurePreload` 会等待 `Nova.SDK.InitializeTask` 完成全部已启用插件的初始化；TGA 就绪后，网络登录请求可由 `NetBuilder` 从其 `IDeviceIdProvider` 自动写入 DeviceID。`Priority` 只控制稳定遍历和释放顺序，不保证初始化先后。两次 GameLogin 都保持 `forceNewAccount=false`：第一次用 OpenID 查询绑定账号；只有明确返回 `10404` 时，第二次才按 DeviceID 优先登录已有游客，不存在才创建。其他失败不会自动注册新账号。GameLogin 不产生绑定副作用，GameBind 只建立或裁决绑定关系。本轮按固定微信校验 → 游戏服务器登录/绑定流程演示，不新增账号选择或冲突解决页面；服务端错误按既有反馈处理。

`GameAccountLogin`、`GameAccountBind`、`GameAccountBindConflict`、`GameAccountBindResolve` 和 `TGAReport` 都同时维护在 Sample 的 `NetworkCmds.xlsx` 表源、导出的 `NetworkCmds.json` 和生成类型中。

## 连接游戏业务服务器

登录校验、支付验单、消息订阅和文本内容安全涉及账号、资金或平台凭据，必须明确客户端与服务端边界。接入时：

1. 游戏服务器按包内 `Nova/Protos/pb_net_wechat_minigame.proto` 实现微信登录校验、道具签名、最多 20 笔批量验单、当前用户全部订单查询、个人通知任务创建和文本内容安全检查。
2. 在项目的 HostKey/NetCmd 表中将六个固定协议名指向对应服务端接口，并正式导出表数据。
3. 在 Config 的微信小游戏配置填写真实 `MidasOfferId`，并通过微信配置页的购买表配置商品（`TableId`、名称、类型、微信商品 ID、金额分、数量）。Demo View Inspector 的 Demo Materials 选择游戏币和道具对应购买表 ID。订阅通知按钮上方可填写模板 ID、服务端通知配置 ID、触发 Unix 秒（`0` 表示立即）及稳定任务幂等键；失败重试时应复用同一键。文本安全按钮上方可填写待检查文本。

客户端无需实现或注入 `IWeChatMiniGameBackend`。Nova 会随 Plugin 自动创建默认 Backend；服务端或路由未完成时请求会返回明确错误，不会伪造登录或支付成功。

文本安全使用 `WeChatMiniGamePlugin.CheckTextContentAsync`，客户端只上传待检查内容与业务场景。微信 `access_token` 由服务端持有并调用微信安全接口，禁止下发到客户端。

业务服务端必须完成 `code2Session`，并向客户端只返回 `OpenID / UnionID`，不在这一步返回游戏 UID。支付订单号、订单快照、状态迁移和补单由 Nova 客户端统一管理；每批最多提交 20 笔服务端验单，得到 `CanDeliver` 后逐笔幂等发货。服务端不创建或保存客户端订单，也不执行发货。客户端仍不得保存 AppSecret、session_key、access token 或 Midas 签名密钥。

支付按钮复用同一业务入口：Android/iOS 共用游戏币 game 与道具 goods 路线，iOS 仍需真实商户和设备验收。iOS 不包含客服会话充值；仅在 `wx.checkIsSupportMidasPayment` 返回允许且 `env=0` 时拉起支付。游戏币还必须在 ConfigWindow 配置与微信平台、服务端一致的“每元游戏币数量”；本 Sample 的 WebGL/WeChat 开发与正式配置已按 Solar 表填写为 10，正式项目仍须核对自身商户汇率。订单会在调用微信前按游戏 UID 写入 Nova Persist；无论微信前端回调成功、失败或超时，最终发货资格均以服务端验单为准。

Android/iOS 游戏币共用 `wx.requestMidasPayment`，固定 `mode=game`；道具共用 `wx.requestMidasPaymentGameItem`，签名原文固定 `mode=goods`。iOS 两类支付均要求 `allow_pay` 允许且 `env=0`，不向微信传入未公开的 `platform=ios`；Android 保留 `platform=android`。这是客户端实现口径，iOS 游戏币的商户和真机可用性尚未验收，详见实际安装微信包内 `Nova/Docs/PAYMENT_PLATFORM_REVIEW.md`。

## 购买、补单和模拟资产

Demo 用 `CreateConfiguredPaymentIntent(tableId, payload)` 读取微信配置中的购买表，非敏感透传保留 `source=nova-demo` 和按钮位置。它不读取 Solar 的业务商品表，也不写死商品价格。游戏币的金额为该档位总价，道具金额为单价；真实项目应通过服务端商品配置验证客户端输入。

`DemoWechatFulfillmentService` 在 SDK 初始化完成后注册。页面只订阅反馈，关闭页面会解除反馈、微信触摸和生命周期事件并释放广告；发货器不持有 View，仍可处理后台补单，最终随 Plugin 注册句柄释放。该服务把订单去重记录、模拟商品余额和引力上报标记保存到同一份 Nova Persist JSON 存档；保存失败或调用已取消时不确认发货，同一订单再次收到不会重复增加余额。存储分类 `nova_demo_wechat_fulfillment_v1` 仅用于 Demo，永远不代表真实资产；正式项目必须维护自己的业务存档和退款审计。

查询订单后，列表合并当前 UID 的本地未完成订单与服务端只读订单。点击“订单类型”轮换全部、游戏币、道具；点击任意订单行再点“验证选中订单”。本地补单只能针对当前 UID 已有本地快照；服务端已完成订单可查看，不能伪造为本地订单重新发货。全部补单复用相同幂等发货器。

发货后的引力业务事件 `nova_demo_wx_fulfilled` 包含订单号、商品、类型、购买来源、发货阶段及验单次数；常规支付、手动补单、后台补单共享这个接入点。SDK 尚未初始化或统计失败时保留待上报记录，后续发货或游戏账号登录后重试。Demo事件保持演示用途；正式微信插件已独立自动接入TGA阶段，并在Paid后按Solar报价口径报告引力/腾讯，Demo不再手动调用收入入口。报价不是可信实付。引力 SDK 只有本地提交、没有服务器幂等确认，因此进程在提交与标记保存之间退出仍可能重复；业务收入统计最终应以服务端支付流水对账。

## 广告与演示物料

三类广告指微信 SDK 自带的激励视频（RewardedVideo）、插屏（Interstitial）和横幅（Banner）。在 DemoWechatMiniGameView Prefab 的 Inspector 中填写 Demo Materials 三类广告位 ID；未填时可回退正式位置键 `demo_rewarded`、`demo_interstitial`、`demo_banner`。Demo 空物料不会写入正式广告位列表。广告 ID、订阅模板和通知配置为空时页面给出明确提示；示例不会伪造可用物料。

激励视频和插屏先点击加载，再点击显示，避免等待加载后丢失用户手势。页面区分激励完整播放、提前关闭和错误，插屏展示后等待关闭回调；不会直接给真实奖励。Banner 与 Solar 一致使用窗口全宽、底部居中布局，尺寸变化时重新贴齐，提供显示/隐藏演示。引力广告展示只在微信显示成功回调后上报，Banner 在 Show 成功后上报；另有 `nova_demo_wx_ad_request` 演示入口操作。微信当前广告对象没有素材点击回调，页面不把打开按钮或关闭当作素材点击。

通知模板、服务端通知配置和稳定任务幂等键在 DemoWechatMiniGameView Prefab Inspector 的 Demo Materials 中维护，运行时填入输入框，仍可手动修改。此组仅用于 Sample，不影响 SDK 正式业务。微信订阅从原生 `SubscribeTouchEnd` 回调内命中按钮并立即拉起授权；普通 Unity `Button.onClick` 会抑制重复请求。`WeChatMiniGameTmpInputBridge` 接管小游戏软键盘，普通 Editor 和浏览器保留默认 TMP 输入。微信触点坐标、复杂 Canvas 遮挡和实际通知送达仍需开发者工具及真机验收。

本 Sample 保留来自历史 Solar 演示的配置：引力沉默唤起周期为 7 天，开发/正式模式分别配置，现有 `ConfigRuntime.asset` 为开发模式示例。公开副本中的物料可能已脱敏，保存值和旧导出物均不代表当前工程有效凭据；正式项目必须填写自己的应用物料并按目标坐标保存、导出。引力物料在微信配置字段之后展示。SDK 只在微信 OpenID 经业务服务端校验后启动，并仅在原厂 Initialize 成功回调后视为可用。客户端集成版本见实际安装微信包根目录的 `LOCAL_VERSION.md`。项目还须将 `https://backend.gravity-engine.com` 和 `https://api.gravity-engine.com` 加入微信小游戏合法域名，并按业务隐私策略决定何时启用埋点。

微信配置中的 DataNexus 数据源 ID 默认为 `0`、SDK Key 默认为空，开关默认关闭；这两项表示未配置。项目取得正式 DataNexus 物料后，在开发、正式坐标分别填写并启用，再导出微信小游戏。开关关闭或物料无效时导出会跳过注入；有效时 game.js 会携带该客户端 SDK Key，切勿误填服务端密钥，并须配置 https://api.datanexus.qq.com 为合法请求域名。独立微信热力引擎 WXSEAppKey 不属于此 DataNexus 接线。

普通浏览器和 Unity Editor 中会跳过微信 SDK 初始化并打印一条警告，这是平台隔离的预期行为。分享、隐私授权和写剪贴板等能力必须由按钮点击直接触发，以满足微信的用户手势要求。

## 支付阶段与补单统计

Demo 购买入口设置独立来源 `demo.currency_button` / `demo.item_button`，不改变原始支付 Payload。长期 `DemoWechatFulfillmentService` 订阅 `PaymentProgress`，持久保存阶段事件；正常购买、登录/前台恢复、发货器就绪及手动补单均携带尝试标识和恢复原因。引力异步初始化完成、登录、手动补单和后续阶段触发统计重试，页面关闭不停止服务。阶段事件复用 event_id，SDK 没有后台接收确认，不能承诺网络端恰好一次；实际收入仍需服务端提供可信资金结果。

事件登记表为微信包 `Nova/Tracks/Tracks.xlsx`。完整需求交接与待确认方案见实际安装微信包内 `Nova/Docs/REVIEW_HANDOFF.md`；历史评审文档不能替代当前平台和业务验收。

## 通过 Codex 使用与复用

示例编辑器配置只面向本 Sample，会遍历 Master 中保存的启用 SDK/Kit 及开发、正式条目，并非只修改当前活动坐标；首次商品表导入只在相应配置为空时补默认项，空 AOT 列表也不会自动形成完整 AOT 准备。重建页面、配置和流水线不构成事务，部分步骤失败后需检查已保存的变化。读取或学习这些工具不等于执行它们。

通知表单会 Trim 模板、配置和任务键；触发时间使用 Unix 秒，`0` 表示立即，任务键仍应由业务稳定保存。软键盘桥按当前 TMP 文本与行类型传初值，多行和数字模式来自控件声明；关闭键盘不代表提交业务表单。购买使用正式购买表 ID，修改历史静态 ProductId 字段不会替代正式表配置。模拟资产的 schema/订单检查与保存失败后恢复内存，都不能证明存储已物理回滚或服务端资产可信；真实业务须独立验证持久化与签名。

可以直接描述“配置微信登录”“批量维护购买表”“检查补单为什么没有发货”“把演示界面接入自己的项目”。Codex 从实际安装 Framework 的 `Docs/START_HERE.md`、`Agents/INDEX.md` 进入现有 SDK/Kit、配置、网络、UI、持久化和平台流水线能力，并按需读取微信 reference 和实际包内 Docs；不依赖主仓的 `UPMPackages` 路径或 Minds，也不按平台、按钮、商品分别新增 Skill。

读取实际平台、配置、表源、场景和可用工具后，用既有确定性入口执行固定步骤。服务端协议、支付资格、有效商户、用户手势与真机条件无法由本地配置代替；查阅示例不自动授权登录绑定、拉起支付、请求权限、修改存档、上报或发送通知。配置保存、正式导出、初始化回调、服务端响应、发货持久化与平台实际效果逐层验收。

导入路径重写、旧版本目录清理、Sample 基类与启动流程的共同边界，见已安装 Framework 的 `Docs/Onboarding/PROJECT_STRUCTURE.md` 中“Sample 的导入、辅助工具与页面边界”；本页只补充当前示例的差异。
