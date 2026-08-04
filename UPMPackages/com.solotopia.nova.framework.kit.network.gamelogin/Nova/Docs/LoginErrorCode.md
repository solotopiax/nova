# LoginErrorCode

## 1. 简介

`LoginErrorCode` 收录客户端登录策略错误码（7000~7999）和服务端登录业务段（10000~10499）。
这些码由服务端 `PbNetBaseResponse.Code` 原样返回，经 `NetService` 透传到 `NetResponse.ErrorCode`，业务侧用本类常量与 `resp.ErrorCode` 比对。

**所在文件：** `Nova/Scripts/Runtime/LoginErrorCode.cs`
**命名空间：** `NovaFramework.Kit.Network.GameLogin.Runtime`
**类签名：** `public static class LoginErrorCode`

---

## 2. 段位规划

| 段位 | 说明 |
|---|---|
| `0` | 成功（OK） |
| `10000~10099` | 登录流程通用错误（账号状态 / UID / device_id 等） |
| `10012` | 请求体 UID 与请求头 UID 不一致 |
| `10400` | 顶号（device_id 非最新，登录 / 删除路径共用） |
| `10404` | 三方号未绑定任何账号 |
| `10407` | 请求头 OpenID 与 UID 归属不一致 |
| `7000~7999` | 客户端登录策略段；不经过服务端 |

> 服务端通用协议级错误（PARAM_ERROR/SERVER_ERROR/AES_ERROR 等）统一用 `NetErrorCode`，不在本类扩展。
> 账号绑定业务错误码（10401 三方占用 / 10402 绑定冲突 / 10403 三方鉴权）由 `GameBind` 模块的 `BindErrorCode` 维护。

---

## 3. 已定义常量

| 常量 | 码值 | 含义 |
|---|---|---|
| `OK` | 0 | 成功 |
| `ErrIdentityRequired` | 7000 | 当前无已确认 UID，客户端拒绝删除账号 |
| `ErrInvalidLoginResponse` | 7001 | 成功响应缺少 Data/UID，或账号状态非 Normal；旧身份不变 |
| `ErrUserNotFound` | 10000 | 用户不存在 |
| `ErrInvalidUID` | 10003 | UID 无效 |
| `ErrDeviceIdRequired` | 10006 | device_id 不能为空 |
| `ErrAccountLocked` | 10007 | 账号已锁定 |
| `ErrAccountBanned` | 10008 | 账号已封禁 |
| `ErrAccountDeleted` | 10011 | 账号已删除 |
| `ErrUIDMismatch` | 10012 | 请求体 UID 与请求头 UID 不一致 |
| `ErrKicked` | 10400 | device_id 非最新，被顶号 |
| `ErrAccountNotFound` | 10404 | 三方号未绑定任何账号（open_id 登录时未绑，由客户端决定注册新号或走绑定流程） |
| `ErrOpenidUIDMismatch` | 10407 | 三方账号与当前账号不匹配（open_id 已绑定其他 uid，与当前请求 uid 不一致） |

---

## 4. 命中链路

服务端错误码经如下链路原样透传到业务侧：

```
PbNetBaseResponse.Code  →  NetParser.ParseResponse  →  NetResult.Code
  →  NetService.SendAsync（Code != SUCCESS 时返回 Fail）  →  NetResponse.ErrorCode
```

- `NetService` 不做码值偏移，`resp.ErrorCode` 即服务端原始码。
- `Login.SendAsync` 失败分支内调 `LogLoginError`，按本类常量归类码值打可读日志（不改变返回值）。

---

## 5. 使用示例

```csharp
var login = Nova.Network.Kit<Login>();
var resp = await login.Async(string.Empty, openid);

if (!resp.IsSuccess)
{
    if (resp.ErrorCode == LoginErrorCode.ErrAccountNotFound)
    {
        // 三方号未绑定：客户端决定注册新号（forceNewAccount=true）或走绑定流程（GameBind 模块）
    }
    else if (resp.ErrorCode == LoginErrorCode.ErrAccountBanned)
    {
        // 提示账号已封禁
    }
    // ... 其他码分支 ...
}
```

---

## 6. 内部约束

- **不偏移码值**：本类常量值与服务端返回码一一对应，不做 7000 段偏移，确保 `resp.ErrorCode == LoginErrorCode.XXX` 直接命中。
- **与 `NetErrorCode` 段位不重叠**：`NetErrorCode` 使用客户端负数段与服务端通用段，本类从 10000 起，避免混用。
- **不收录通用协议级错误**：PARAM_ERROR/SERVER_ERROR/AES_ERROR/APPID_MISSING 等统一在 `NetErrorCode` 维护。
- **不收录绑定业务错误**：绑定相关码（10401/10402/10403）由 `GameBind` 模块的 `BindErrorCode` 维护。

---

## 7. 关联

- 同包：[Login.md](./Login.md) — 登录 Service（失败分支调 `LogLoginError` 归类本类码值）
- 跨包：`NovaFramework.Kit.Network.GameBind.Runtime.BindErrorCode` — 账号绑定业务错误码
- 跨包：`NovaFramework.Runtime.NetErrorCode` — 网络层通用错误码
