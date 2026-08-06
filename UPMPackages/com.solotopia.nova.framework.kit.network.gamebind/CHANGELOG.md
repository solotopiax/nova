# Changelog

## [Unreleased]

## [0.0.11] - 2026-08-06

### Added

- 新增 `QueryBindingAsync(string openid)` 只读查询接口、`BindingQueryCmdName` 配置和 `PbNetBindingQueryReq/Resp` 协议，用于查询指定 OpenID 是否已绑定及对应 UID。
- GameBindDemo 新增绑定状态查询按钮，继续复用现有 OpenID 输入框。
- 新增 `ErrUIDAlreadyBoundOtherOpenID`(10408)，用于区分“当前 UID 已绑定其他 OpenID”与“目标 OpenID 已被占用”。

### Fixed

- `PbNetBindingQueryResp.head` 修正为响应公共头 `PbNetRespHeader`。

### Changed

- `coreVersion` 同步为 `1.0.0`。

## [0.0.10] - 2026-08-04

### Breaking

- 三个 Proto `open_id` 字段统一改为 `openid`，生成属性改为 `Openid`；字段号不变。

### Changed

- Bind/Resolve 接入 Framework 全局身份操作租约，并改为原子提交 UID/OpenID 身份对；QueryConflict 保持只读。
- GameBindDemo 支持显式 UID 登录，并按新 Login 身份语义归一化 UID 与强制新账号参数。
- Framework 与 GameLogin 最低依赖分别提升至 `0.6.5` 与 `0.1.4`。

## [0.0.9] - 2026-08-03

### Breaking

- `BindAsync` 的 `provider` 参数由 `int` 改为 `ThirdLoginProvider`；协议字段仍为 `int32`，调用方改用 `ThirdLoginProvider.Facebook / Google / Apple / Wechat`。

### Fixed

- 移除 GameBind 对游戏运营渠道 `PbNetChannel` 的错误复用，明确 `ThirdLoginProvider` 与 `ChannelType` / `PbNetChannel` 是两套独立契约。

### Changed

- Framework、GameLogin 与 GameSave 最低依赖分别提升至 `0.6.4`、`0.1.3` 与 `0.1.1`。

## [0.0.8] - 2026-08-03

### Changed

- GameBindDemo 同步 Localization 支持语言表与 JSON / Binary 数据格式能力，并将 Framework 最低依赖提升至 `0.6.3`。

## [0.0.7] - 2026-07-31

### Added

- `Bind` 新增只读 `OpenID` 属性，直接反映 `NetService` 的进程内 OpenID 缓存。
- 补充 `ErrAccountNotFound`(10404) 与 `ErrOpenidUIDMismatch`(10407) 绑定错误码及可读日志。

### Changed

- 绑定、冲突查询和裁决的目标 `openid` 只写入业务 Body，请求 Header 仅声明当前身份；Bind/Resolve 成功后按业务结果同步 UID/OpenID。
- 新增账号登录、三方绑定、顶号与存档编排的完整业务流程手册。
- 三个公开方法的 OpenID 参数名统一为 `openid`。

## [0.0.6] - 2026-07-29

### Changed

- 将 Framework、GameLogin 与 GameSave 最低依赖分别提升至 `0.6.0`、`0.1.0` 与 `0.1.0`。
- GameBindDemo 同步启动应用配置网络命令、运行时配置与场景覆盖。

## [0.0.5] - 2026-07-21

### Added

- 新增直接绑定、冲突查询和冲突裁决三类关键行为埋点，覆盖成功、失败与异常结果。
- 绑定成功后更新 `nova_openid`；Resolve 成功后先切换埋点用户 UID，再更新 `nova_openid`。

## [0.0.4] - 2026-07-13

### Changed

- 提升 Framework、UniTask、GameLogin 与 GameSave 的依赖下界，保证独立安装时完整解析到本轮 Unity 6000.5 兼容版本。

## [0.0.3] - 2026-07-13

### Changed

- 将 Nova Framework 的最低依赖版本提升至 `0.5.37`，避免安装时解析到仍使用旧契约的框架版本。

## [0.0.2] - 2026-07-08

### Fixed

- 清理 sample asmdef 克隆残留引用：删除代码未使用的 tga / appsflyer / ad 跨包程序集引用（从 MainDemo 模板克隆时带入，GameBindDemo 实际未调用），避免消费工程未安装这些无关包时编译报 CS0234。
- 补全 sample 依赖声明：package.json dependencies 增加 com.solotopia.nova.framework.kit.network.gamelogin + com.solotopia.nova.framework.kit.network.gamesave（GameBindDemo 的登录/绑定/存档演示实际使用），修复消费工程 import sample 后缺对应程序集的编译失败。

## [0.0.1] - 2026-07-02

### Added

- 初始版本：账号绑定业务网络 Kit，封装绑定、冲突查询、裁决三段协议。
- `Bind` Service：`BindAsync(provider, openId)` 绑定、`QueryConflictAsync(openId)` 冲突查询、`ResolveAsync(openId, choice, verifyCode)` 裁决；`SetDebugMode` 调试覆盖。
- `BindKitConfig`：`BindCmdName` / `BindConflictCmdName` / `BindResolveCmdName` 三协议指令名配置。
- `BindErrorCode`：账号绑定业务错误码常量（ErrKicked=10400 / ErrOpenidAlreadyBound=10401 / ErrBindConflict=10402 / ErrThirdPartyAuthFailed=10403 / ErrBindBusy=10406）。
- proto：`pb_net_bind.proto` 定义 `BindSummary` / `PbNetBindReq` / `PbNetBindResp` / `PbNetBindConflictReq` / `PbNetBindConflictResp` / `PbNetBindResolveReq` / `PbNetBindResolveResp`。
- 文档：`Bind.md` / `BindErrorCode.md` / `BindKitConfig.md` / `INDEX.md`。
