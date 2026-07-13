# Changelog

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
