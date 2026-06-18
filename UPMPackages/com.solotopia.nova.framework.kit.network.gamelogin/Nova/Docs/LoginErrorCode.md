# LoginErrorCode

## 1. 简介

`LoginErrorCode` 是登录业务错误码占位类，约定段位 7000~7999，与 `NetErrorCode` 客户端段（负数）和服务端通用段（1000 / 5000 / 6000 / 6001）互不冲突。当前为空类，实际错误码常量随业务推进补充。

**所在文件：** `Nova/Scripts/Runtime/LoginErrorCode.cs`
**命名空间：** `NovaFramework.Kit.Network.GameLogin.Runtime`
**类签名：** `public static class LoginErrorCode`

---

## 2. 公开 API

> **当前版本无已定义常量。** 以下为约定的段位规划，待后续补充。

| 段位 | 说明 |
|---|---|
| `7000~7099` | 登录流程通用错误（待填充） |
| `7100~7999` | 业务扩展段（待填充） |

---

## 3. 使用示例

```csharp
// 当前仅用于 switch/case 分支预留，实际常量待后续版本填充
var login = Nova.Network.Kit<Login>();
var resp = await login.Async(string.Empty, openId);
if (!resp.IsSuccess)
{
    // 7000~7999 为登录业务专属段（当前无已定义常量）
    if (resp.ErrorCode >= 7000 && resp.ErrorCode < 8000)
    {
        // 登录业务错误
    }
}
```

---

## 4. 内部约束

- **与 `NetErrorCode` 段位不重叠**：`NetErrorCode` 使用负数（客户端）+ 1000/5000/6000/6001（服务端通用），登录业务段从 7000 起，避免混用。
- **不强制在此类扩展服务端通用错误**：服务端通用协议级错误（如 PARAM_ERROR/SERVER_ERROR）统一用 `NetErrorCode`；本类仅收录登录语义相关的业务错误码。

---

## 5. 关联

- 同包：[Login.md](./Login.md) — 返回 `NetResponse<PbNetLoginResp>` 的 Service
