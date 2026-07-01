# LoginErrorCode

## 1. 简介

`LoginErrorCode` 是登录业务错误码常量类，收录登录 / 绑定业务段服务端返回码（10000~10499）。
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
| `10400~10499` | 登录绑定业务错误（顶号 / 三方占用 / 绑定冲突 / 三方鉴权 / 账号未注册） |
| `7000~7999` | 客户端段（预留，与 `NetErrorCode` 客户端段负数 / 服务端通用段 1000/5000/6000/6001 错开，当前无定义） |

> 服务端通用协议级错误（PARAM_ERROR/SERVER_ERROR/AES_ERROR 等）统一用 `NetErrorCode`，不在本类扩展。

---

## 3. 已定义常量

| 常量 | 码值 | 含义 |
|---|---|---|
| `OK` | 0 | 成功 |
| `ErrUserNotFound` | 10000 | 用户不存在 |
| `ErrInvalidUID` | 10003 | UID 无效 |
| `ErrDeviceIdRequired` | 10006 | device_id 不能为空 |
| `ErrAccountLocked` | 10007 | 账号已锁定 |
| `ErrAccountBanned` | 10008 | 账号已封禁 |
| `ErrAccountDeleted` | 10011 | 账号已删除 |
| `ErrKicked` | 10400 | device_id 非最新，被顶号 |
| `ErrOpenidAlreadyBound` | 10401 | 该三方号已被他人占用（换绑场景） |
| `ErrBindConflict` | 10402 | 绑定冲突，需二选一（登录路径触发，响应带 guest_summary/existing_summary） |
| `ErrThirdPartyAuthFailed` | 10403 | open_id 缺失或格式非法（三方鉴权失败） |
| `ErrAccountNotFound` | 10404 | 三方号未注册（账号不存在） |

---

## 4. 命中链路

服务端错误码经如下链路原样透传到业务侧：

```
PbNetBaseResponse.Code  →  NetParser.ParseResponse  →  NetResult.Code
  →  NetService.SendAsync（Code != SUCCESS 时返回 Fail）  →  NetResponse.ErrorCode
```

- `NetService` 不做码值偏移，`resp.ErrorCode` 即服务端原始码。
- `Login.SendAsync` / `Login.SendBindResolveAsync` 失败分支内调 `LogLoginError`，按本类常量归类码值打可读日志（不改变返回值）。

---

## 5. 使用示例

```csharp
var login = Nova.Network.Kit<Login>();
var resp = await login.Async(string.Empty, openId);

if (!resp.IsSuccess)
{
    // 绑定冲突：读取摘要走二选一
    if (resp.ErrorCode == LoginErrorCode.ErrBindConflict)
    {
        // resp.Data.GuestSummary / resp.Data.ExistingSummary
        await login.BindResolveAsync(provider, openId, "guest");
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
- **与 `NetErrorCode` 段位不重叠**：`NetErrorCode` 用负数（客户端）+ 1000/5000/6000/6001（服务端通用），本类从 10000 起，避免混用。
- **不收录通用协议级错误**：PARAM_ERROR/SERVER_ERROR/AES_ERROR/APPID_MISSING 等统一在 `NetErrorCode` 维护。

---

## 7. 关联

- 同包：[Login.md](./Login.md) — 登录 Service（失败分支调 `LogLoginError` 归类本类码值）
- 同包：[LoginBind.md](./LoginBind.md) — 绑定二选一协议（`ErrBindConflict` 触发）
- 跨包：`NovaFramework.Runtime.NetErrorCode` — 网络层通用错误码
