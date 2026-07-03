# Nova Framework - Kit - Network - GameBind 文档索引

> 本包为 Nova 框架账号绑定业务 Kit，在主框架包 `com.solotopia.nova.framework` 的 Network Kit 公共编排层基础上，封装绑定、冲突查询、裁决三段协议。
> 业务侧通过 `Nova.Network.Kit<Bind>()` 获取实例，无需关心协议细节。
>
> **职责边界：** 本包只负责账号归属裁决（open_id 绑哪个 uid、冲突时谁为主）；存档数据覆盖（本地覆盖云端 / 云端覆盖本地）由业务层配合 `GameSave` 模块编排，登录态切换由 `GameLogin` 模块负责。三者职责正交。

---

## 业务侧公开 API

| 类型 | 说明 | 文档 |
|---|---|---|
| `Bind` | 账号绑定业务 Service（BindAsync / QueryConflictAsync / ResolveAsync / SetDebugMode） | [Bind.md](./Bind.md) |
| `BindKitConfig` | 账号绑定 Kit 固有配置（BindCmdName / BindConflictCmdName / BindResolveCmdName），在 ConfigWindow 一次配置后 Bind 内部自动取用 | [BindKitConfig.md](./BindKitConfig.md) |
| `BindErrorCode` | 账号绑定业务错误码（服务端绑定业务段 10400~10499 + 客户端段 7000~7999 预留） | [BindErrorCode.md](./BindErrorCode.md) |

## 协议

- `pb_net_bind.proto` 定义 `BindSummary` / `PbNetBindReq` / `PbNetBindResp` / `PbNetBindConflictReq` / `PbNetBindConflictResp` / `PbNetBindResolveReq` / `PbNetBindResolveResp`，字段说明见 [Bind.md](./Bind.md) 协议数据结构表。

## 错误码

- [BindErrorCode.md](./BindErrorCode.md) — 账号绑定段错误码（10400~10499）
- 通用网络错误码由底层公共网络层返回，本包只维护账号绑定业务段错误码。

## 相关

- [Bind.md](./Bind.md) — 账号绑定业务 Service
- [BindKitConfig.md](./BindKitConfig.md) — 账号绑定 Kit 配置
- [BindErrorCode.md](./BindErrorCode.md) — 账号绑定业务错误码
