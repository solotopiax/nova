# Changelog

## [0.0.1] - 2026-07-02

### Added

- 初始版本：账号绑定业务网络 Kit，封装绑定、冲突查询、裁决三段协议。
- `Bind` Service：`BindAsync(provider, openId)` 绑定、`QueryConflictAsync(openId)` 冲突查询、`ResolveAsync(openId, choice, verifyCode)` 裁决；`SetDebugMode` 调试覆盖。
- `BindKitConfig`：`BindCmdName` / `BindConflictCmdName` / `BindResolveCmdName` 三协议指令名配置。
- `BindErrorCode`：账号绑定业务错误码常量（ErrKicked=10400 / ErrOpenidAlreadyBound=10401 / ErrBindConflict=10402 / ErrThirdPartyAuthFailed=10403 / ErrBindBusy=10406）。
- proto：`pb_net_bind.proto` 定义 `BindSummary` / `PbNetBindReq` / `PbNetBindResp` / `PbNetBindConflictReq` / `PbNetBindConflictResp` / `PbNetBindResolveReq` / `PbNetBindResolveResp`。
- 文档：`Bind.md` / `BindErrorCode.md` / `BindKitConfig.md` / `INDEX.md`。
