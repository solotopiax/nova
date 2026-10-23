# 微信小游戏平台能力

精确包为 `com.solotopia.nova.framework.sdk.wechat.minigame`。先读实际解析包 `Nova/Docs/INDEX.md`；日常任务只查对应 `RUNTIME_API.md` 段落和 `CAPABILITY_MATRIX.md`。所有业务从 `WeChatMiniGamePlugin` 与返回的 Nova DTO/包装对象调用，不引用 `WeChatWASM` 或引力原厂类型。

## 配置与平台资格

顶层 `WeChatMiniGamePluginConfig` 走公共 Config Operation，`EnabledSDKs` 只保存其 FullName。先读取 `AppId/InitializationTimeoutSeconds/ApiTimeoutSeconds/EnableMemoryWarning` 当前值及目标环境，再按任务配置，不把 Sample 物料复制到正式项目。

微信包可以留在 Android、iOS 和普通 WebGL 项目依赖中，但真正平台资格是 `WeChatMiniGameEnvironment.IsMiniGame`。Editor 跳过原厂初始化，普通浏览器 WebGL 也不代表微信容器；宏只决定编译资格，不能绕过环境/Ready 门禁。没有微信开发者工具或真机时保留配置/编译/本地产物证据，最高 `partial`。

## 任务到既有封装

| 用户目标 | 复用入口与必须读取的文档 |
|---|---|
| 登录微信并接入游戏账号 | `LoginAndVerifyAsync`，INDEX 的登录说明和 `RUNTIME_API.md` 登录段；业务协议查 `SERVER_CONTRACT.md`，游戏 UID 后续按[身份与存档](identity-and-save.md)处理 |
| 隐私、权限、分享、剪贴板、订阅通知 | `GetPrivacyStatusAsync/RequirePrivacyAuthorizationAsync`、公开分享/剪贴板入口、`RequestSubscribeAndCreateNoticeAsync`；读 Runtime 用户手势/通知段 |
| 广告位、奖励视频、Banner、插屏 | Config `AdUnits`；`CreateConfiguredRewardedVideoAd/CreateConfiguredBottomCenteredBannerAd/CreateConfiguredInterstitialAd` 与返回对象；读 Runtime Config 广告位、对象生命周期段 |
| 买游戏币/道具、补单、发货 | Config `Purchases/MidasOfferId/ZoneId/CurrencyType/GameCurrencyCoinsPerYuan`；`CreateConfiguredPaymentIntent/PurchaseAsync/RegisterPaymentFulfillmentHandler/VerifyPaymentOrderAsync/VerifyAllPaymentOrdersAsync`；读 Runtime 支付段、`SERVER_CONTRACT.md/PAYMENT_PLATFORM_REVIEW.md` |
| 请求域名、签名、验单、通知、文本安全 | 六条文档化 CmdName，经既有 `WeChatMiniGameBackend` + Nova Network/Proto；读 `SERVER_CONTRACT.md`，复用 Network Operation |
| 触摸/网络/生命周期/安全区、键盘/系统/更新 | 当前 Plugin 的 `Subscribe*`、系统入口与 Nova 包装对象；读 Runtime 相应段，订阅和有状态对象由调用方对称释放 |
| 开放数据域、游戏圈、用户信息按钮 | `PostOpenDataMessage/CreateGameClubButton/CreateUserInfoButton` 等文档化入口；开放数据域单向消息不自动生成排行榜业务或可信数据 |
| 微信专属 Storage/文件/缓存 | `Set/GetStorage*`、`CreateFileSystem` 返回的文件系统；描述符 Open/Close 配对，清缓存/删除必须冻结具体目标 |
| 引力统计与腾讯 DataNexus | 读取 Runtime 正式统计与 DataNexus 段，再配置对应开关和客户端物料；不为每个事件新增 Skill |
| 本地转换产物 | 已保存 Pipify 的 `wechat.minigame.export`，通过 Build Player 的受控 Batch 入口；先读 INDEX 的 Pipify 条件 |

这些都是已有 Runtime/API 路由，不等于新增 Action。业务 source/UI/流程写入复用对应 Operation；保持一个 Plugin 门面和共享对象生命周期，不为每个平台 API 复制另一个系统。

## 手势与异步生命周期

要求用户手势的调用在真实用户操作链立即 `CaptureUserGesture()` 并执行，不先 await、延迟帧或缓存令牌复用。订阅消息必须从 `SubscribeTouchEnd` 原生同步回调中做 UI 命中判断，再立即申请；普通 Unity Button 的同帧回调不能替代。Agent 自动点击只能在实际宿主能保留该原生上下文时作为证据，不能用伪令牌通过门禁。

广告对象显式 Load/Show/Dispose，Banner 等待首次加载；激励基础资格按关闭完成结果，价值奖励仍需项目防重/服务端裁决。持有方释放 `Subscribe*` 返回的 IDisposable、广告、用户信息/游戏圈按钮、Chat 和更新管理器。进入后台或切换页面后不会默认继续复用失效对象。

## 支付、表源与服务端

六条配置字段为 `VerifyLoginCmdName/GeneratePaymentSignatureCmdName/VerifyPaymentOrdersCmdName/QueryPaymentOrdersCmdName/CreateNoticeCmdName/CheckTextContentCmdName`。Host/Path 使用项目 HostKey/NetCmd 表，复用 Network Operation 维护源、正式导出、类型、Config 与运行时消费；默认 Backend 已存在时不另写重复适配器。

购买表通过既有 ConfigWindow Excel 导入和包内 `Nova/Templates/WeChatPurchasesTemplate.xlsx` 维护；导入覆盖前检查差异。字段 `TableId/Kind/ProductId/Quantity/PriceFen` 按当前模板校验；游戏币必须满足 `PriceFen × GameCurrencyCoinsPerYuan = Quantity × 100`，`0` 汇率表示未配置，道具金额为每件单价分。保存并正式导出目标坐标，不用 Luban 通用表替换此内联表，不把客户端报价当可信实付。

已有项目 `.xlsx` 批量导入复用[内联商品转换](inline-products.md)与 Config `patch-panels`；显式 Sheet、源 SHA 和目标汇率校验后生成完整列表值。它不改 Workbook 行，不创建订单，不把 source 校验成功当 Master/Runtime 已保存。

AppSecret、session_key、微信 access_token、Midas 私钥留在服务端。登录校验只返回微信身份，游戏服登录另行确认；签名、验单、通知、文本安全按 `SERVER_CONTRACT.md` 核对。项目发货器主动支付和补单复用同一个幂等入口；不因已收到平台支付回调就发奖。Android/iOS 两类支付和 iOS 的 `allow_pay/env=0` 条件以当前平台审查页为准，商户资格与真机仍单独验收。

## 构建与统计

`wechat.minigame.export` 是终端 Player 转换 Step，不与普通 `build.package` 串成二次 WebGL 构建。它读取当前微信 Config 的 AppID、官方 `MiniGameConfig.asset` 的转换选项；Unity 6 要 WebGL2，首版 YooAsset `BundledCopyOption=None`。通过既有 Build Player/Pipify Operation 审查 Batch 写集、异步转换完成、`minigame/` / `webgl/` 和关键文件，临时模板和设置恢复由现有转换事务处理。上传、后台白名单、隐私、包体规则、开发者工具与设备运行不由本地导出成功代替。

启动主资源预加载完成后现有流程单次 `ReportGameStart()`，不在 SDK Ready 时提前上报。引力在服务端确认微信 OpenID 后启动，`IsGravityInitialized`/归因回调与真实后台接收分开；DataNexus `EnableDataNexus/DataNexusUserActionSetId/DataNexusSdkKey` 默认关闭/未配置，只有明确启用且客户端物料有效才由导出注入。引力 Access Token、DataNexus SDK Key 是客户端 SDK 物料，不能替换为服务端秘密；日志和报告仍脱敏。

## 复用与证据

普通 HTTP/下载继续 Nova Network，本地业务进度继续 Persist，资源继续 Asset，UI/声音/流程继续既有模块。仅微信特有功能调用本包公开封装。包内 `CAPABILITY_MATRIX.md` 用来决定真实归属，不能把“已有函数”当成“当前项目已绑定”。

本地探针只能证明当前层：坐标导出、编译、转换产物或真实容器回调。登录、支付、发货、订阅、广告、统计分别记录测试输入、成功/失败信号与外部条件；默认不创建订单、触发生产请求、删除缓存、上传或安装。未完成的必要层保留 `partial`，清理本轮临时对象、订阅、文件与测试态配置后交付。
