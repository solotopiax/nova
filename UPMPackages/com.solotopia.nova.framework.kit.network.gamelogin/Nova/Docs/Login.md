# Login

## 1. 简介

`Login` 是登录业务网络 Service。Header 只声明此前已确认身份，本次候选 UID/OpenID 位于 Body；仅合法成功响应的外层 UID/OpenID 会原子替换 `NetService` 身份。

**所在文件：** `Nova/Scripts/Runtime/Login.cs`
**命名空间：** `NovaFramework.Kit.Network.GameLogin.Runtime`
**类签名：** `public sealed partial class Login`

> 通过 `Nova.Network.Kit<Login>()` 获取实例，不继承任何基类，无参构造即可使用。

---

## 2. 公开 API

### 属性

| 签名 | 说明 |
|---|---|
| `public string UID` | 当前已登录用户 UID；直接读取 `NetService.UID`，登出后清空 |
| `public bool IsLoggedIn => !string.IsNullOrEmpty(UID)` | 当前是否已登录（UID 非空）；只读派生属性 |

### 方法

| 签名 | 说明 |
|---|---|
| `public UniTask<NetResponse<PbNetLoginResp>> Async(string uid, string openid, bool forceNewAccount = false)` | Header 保留旧确认身份；候选 `uid/openid` 只进 Body；强制新账号时 Body 两者置空；仅成功、Data 非空、UID 非空且状态 Normal 时提交响应外层身份 |
| `public UniTask<NetResponse<PbNetDeleteResp>> DeleteAsync()` | 只删除当前确认 UID；Header UID 与 Body UID 必须一致；无 UID 返回 7000 且不发请求 |
| `public void Clear()` | 清空 NetService 的 UID、OpenID；后续请求 Header 不再携带身份字段 |

> 为当前账号绑定三方 OpenID 请使用 `GameBind` 模块的 `Nova.Network.Kit<Bind>()`，登录与绑定职责分离。
> 完整的顶号、Header 身份、绑定冲突与存档编排手册位于 GameBind package 的 `Nova/Docs/AccountLoginAndThirdPartyBindFlow.md`。

---

## 3. 使用示例

```csharp
// 前提：ConfigWindow 已配置 LoginKitConfig.CmdName（如 "GameLogin"）

// 获取 Login Service 实例
var login = Nova.Network.Kit<Login>();

// 发起登录，openid 由第三方 SDK 回调提供；uid 留空则沿用登录态 UID
var resp = await login.Async(string.Empty, openid);

if (resp.IsSuccess)
{
    string uid = login.UID;  // 登录成功后 UID 已自动写回
    // 继续业务逻辑
}
else
{
    int code = resp.ErrorCode;
    // 根据 NetErrorCode / LoginErrorCode 做分支处理
}

// 清空登录态
login.Clear();
// login.IsLoggedIn == false
```

---

## 4. 内部约束

- **身份写回**：只使用 `PbNetLoginResp.Uid/Openid` 原子更新；不回退请求 OpenID。失败或响应非法返回时保留旧缓存。
- **仅进程内缓存**：UID、OpenID 都不由 GameLogin 写入 PlayerPrefs、FileFragment 或 SQLite；进程重启后必须重新登录。
- **强制新账号**：`forceNewAccount=true` 时 Body UID/OpenID 都清空，但 Header 仍保留当前确认身份；响应通过相同合法性校验后提交服务端返回身份。
- **最新设备与顶号**：每次成功登录都把 UID 的最新 `device_id` 更新为当前设备；旧设备后续访问受保护接口收到 `10400`。
- **候选身份组合**：Body 支持 UID-only、OpenID-only、UID+OpenID 和两者皆空；服务端负责校验候选归属，Header 不参与表达本次候选身份。
- **删号清理条件**：响应 Data 明确为 Locked/Banned/Deleted，且请求目标 UID 仍等于当前 UID 时清空身份；Normal/Unspecified/null Data/目标已变化均不清理。即使业务失败，只要携带上述明确状态 Data 也执行清理。
- **并发语义**：Login/Delete 与 GameBind 的 Bind/Resolve 共用非排队身份租约；竞争调用返回 `-6`，不会发请求。
- **协议生成属性硬切换**：`OpenId` 已改为 `Openid`；字段号不变，不提供旧属性别名。
- **`DeleteAsync` cmdName 取自 `DeleteCmdName`**：`DeleteAsync` 内部取 `LoginKitConfig.DeleteCmdName` 解析为指令行；在 ConfigWindow 中为 `LoginKitConfig.DeleteCmdName` 填入对应 NetCmd 名称并重导出后方可正常调用。
- **`LoginKitConfig` 必须配置**：`Async` 内部通过 `Nova.Config.GetKitConfig<LoginKitConfig>()` 取配置，未在 ConfigWindow 配置 `LoginKitConfig` 时抛 `KitConfigMissingException`（开发期 fail-fast，暴露漏配）。
- **游戏运营渠道来源**：Header 的 `channel` 由 `NetBuilder.BuildHeader()` 从 `Nova.Config.Channel` 自动读取，业务侧无需传入；它只表示包体分发与运营来源，不表示第三方登录提供方。
- **固定传输加密**：所有登录业务请求均由 `NetService` 使用 `Nova.Config.AppConfigs.AppAesKey/AppAesIV` 进行 AES-128-CBC 加密后发送，并以同一配置解密响应。
- **`ChannelType` 映射范围**：`Official / Google / Apple / WeChat / TikTok / Alipay` 均同名映射到 `PbNetChannel`，只有 `ChannelType.None` 或未知值映射为 `PbNetChannel.Unspecified`。
- **`Head` 自动填充**：`NetBuilder.BuildHeader()` 在 `SendAsync` 内部调用，业务侧无需手动构建 Header。
- **依赖主框架公共网络编排层**：`NetService.SendAsync` / `NetBuilder.BuildHeader` / `NetResponse<T>` 均来自主框架包 `com.solotopia.nova.framework` 的 Network Kit 公共层。
- **失败分支码值归类**：`SendAsync` 失败时调 `LogLoginError`，按 `LoginErrorCode` 常量归类码值打可读日志；不改变返回值，业务侧仍按 `resp.ErrorCode` 自行分支。
- **登录不做绑定副作用**：`Async` 传入 `openid` 时服务端只“读”绑定关系（查 `open_id` 已绑的 UID 并登入），未绑返回 `ErrAccountNotFound`(10404)；为当前账号绑定三方号是 `GameBind` 模块的独立职责。

---

## 5. 关联

- 同包：[LoginErrorCode.md](./LoginErrorCode.md) — 登录业务段错误码
- 同包：[LoginKitConfig.md](./LoginKitConfig.md) — 登录 Kit 配置
- 跨包：`com.solotopia.nova.framework.kit.network.gamebind` — 账号绑定业务 Kit（`Bind`）
