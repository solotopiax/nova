# 身份、绑定与云存档

用于游客账号、第三方身份、账号绑定及云进度任务。路径都相对于实际已解析包，不依赖 Nova 主仓；先读各包 INDEX，以下名称只作定向导航，实际版本的声明与文档冲突时记录漂移再核对，不调用不存在的 API。

## 路由与实际配置

精确 package id 前缀为 `com.solotopia.nova.framework.`。

| 包后缀 / 用户目标 | 顶层 Config 与真实字段 | INDEX 后按需页面 |
|---|---|---|
| `kit.network.gamelogin` / 游戏账号登录 | `LoginKitConfig`：`LoginCmdName/DeleteCmdName` | `Nova/Docs/Login.md`、`LoginKitConfig.md` |
| `kit.network.gamebind` / 绑定、查询和冲突裁决 | `BindKitConfig`：`BindingQueryCmdName/BindCmdName/BindConflictCmdName/BindResolveCmdName` | `Nova/Docs/Bind.md`、`BindKitConfig.md`、`AccountLoginAndThirdPartyBindFlow.md` |
| `kit.network.gamesave` / 云进度读取与上传 | `SaveKitConfig`：`GetCmdName/SetCmdName` | `Nova/Docs/Save.md`、`SaveKitConfig.md` |
| `sdk.googlesignin` / Google 身份 | `GoogleSignInPluginConfig`：`ClientId/RequestEmail/FilterByAuthorizedAccounts/AutoSelectEnabled/AutoRestoreOnInitialize` | `Nova/Docs/GoogleSignInPluginConfig.md`、`GoogleSignInPlugin.md` |
| `sdk.applesignin` / Apple 身份 | `AppleSignInPluginConfig.RequestFullName` | `Nova/Docs/AppleSignInPluginConfig.md`、`AppleSignInPlugin.md` |
| `sdk.facebook` / Facebook 身份及资料 | `FacebookPluginConfig`：`FacebookAppId/FacebookClientToken/AutoDownloadAvatarOnLogin/AvatarSize` | `Nova/Doc/FacebookPlugin.md` |
| `sdk.wechat.minigame` / 微信身份 | `WeChatMiniGamePluginConfig.VerifyLoginCmdName` 与 `AppId` | [微信平台](wechat-platform.md)，目标包 `RUNTIME_API.md/SERVER_CONTRACT.md` |

Kit 的 CmdName 是指令名，Host/Path 从消费项目正式 HostKey/NetCmd 表解析，不能把示例指令名、域名或默认空值当成项目已配置。先复用 Network Operation 核对权威表源、正式数据/访问类型、Config 引用与运行时加载。绑定四条指令属于 BindKitConfig，不向 LoginKitConfig 添加历史字段。

Google `ClientId` 是网页客户端编号而非客户端秘密；初始化默认 `RequestEmail/FilterByAuthorizedAccounts/AutoSelectEnabled=true`、`AutoRestoreOnInitialize=false`。Apple `RequestFullName=true`，姓名可能仅首次授权返回。Facebook 头像自动下载默认开启、尺寸默认 100；实际已有值保持不变。平台服务标识、安全凭据与商店物料分别管理，不把证书、私钥或服务端密钥写进配置或测试脚本。

## 接入与已有业务复用

1. 按共用路径安装已确认包，扫描精确 Config FullName，再经 Config Operation 启用、编辑并导出。Google/Apple/Facebook 是 SDK；Login/Bind/Save 是 Network Kit。不因为安装登录 SDK 就重新实现整个账号系统。
2. 从匹配版本的 Nova `Scripts/Runtime` 平台服务和构建文档确认目标支持。Google Android 使用 Credential Manager；Apple 原生登录由 iOS 路径执行。非目标平台或 Editor 的可编译/可初始化不能证明授权弹窗成功。Android Manifest/ProGuard、iOS capability 等复用包文档化构建入口，不手写原生文件。
3. 已有账号入口优先复用。三方 `IAuthPlugin.LoginAsync(provider, ct)` 返回 `AuthResult`，目标平台身份还需游戏服按当前协议确认；UID 与 OpenID 分别处理。微信 `LoginAndVerifyAsync` 校验微信身份但不直接建立游戏 UID。
4. Login/Bind/Save 的公开 Service 分别由 `Nova.Network.Kit<Login/Bind/Save>()` 获取；按文档对应的 `Async`、`QueryBindingAsync/BindAsync/QueryConflictAsync/ResolveAsync`、`GetAsync/GetFullAsync/SetAsync/SetFullAsync` 使用。Header 只保留已确认的会话 UID/OpenID，候选登录或绑定目标只放业务 Body，不直接写内部 `NetService`。

本 Workflow 保留现有业务调用；增加按钮、回调、流程或存档绑定时路由到 UI、Procedure、Event、Persistence 或 Network Operation，明确业务 source 写入范围后继续执行。

## 登录、冲突与存档语义

三方未绑定错误 `10404` 才可按文档走 `Login.Async(string.Empty, string.Empty, false)`，以 DeviceID 优先登录已有游客、不存在才创建，再进行 Bind。其他网络/服务端失败不得自动注册；`forceNewAccount=true` 的强制新账号行为不用于这条回退。

绑定冲突 `10402` 后查询双方摘要，由玩家选择保留当前或已有进度；时间更晚不构成自动覆盖依据。Resolve 负责账号归属，Save 负责云传输，业务决定本地/云覆盖并使用 Persist 管理本地进度。沿 GameBind 的完整流程文档验收，不能只做一次 Bind 调用就声称云进度闭环。

GameLogin 当前成功路径会通知 SDK 账号；核对已有路径后避免再调一次 `Nova.SDK.Login` 或重复注册。仅在项目自有登录流程需要时复用统一账号 API，注册判断以游戏服真实响应为依据。

## 最小探针与外部条件

- 本地接入：Config/Runtime 坐标、CmdName 解析、编译与现有启动入口中的目标 SDK 状态/Kit 配置读取；不会发业务请求的只读观察优先。
- 真实身份：已授权测试账号与平台设备上的授权回调、游戏服响应和最终 UID；隐藏 code、身份令牌、OpenID/UID 原值。
- 云存档：测试 UID 下已确认可重放请求，读/写与回读一致，并核对本地持久化与账号切换隔离。写云数据、删账号、冲突裁决和覆盖本地进度不用于默认接入探针。

未获测试账号、设备、后台协议或权限时保留本地完成证据并返回 `partial`；未定义成功探针或协议而尚未执行时 `blocked`。平台授权成功不证明绑定、发货、云进度或其他 SDK 已收到游戏账号。
