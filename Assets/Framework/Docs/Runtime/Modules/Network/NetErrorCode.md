# NetErrorCode

## 1. 简介

`NetErrorCode` 是网络层错误码常量集合，覆盖客户端本地错误（负数段）与服务端通用协议错误（正数段）。业务专属错误码（如登录 7000~7999）由各业务子包独立定义，不在本类扩展。

**所在文件：** `Assets/Framework/Scripts/Runtime/Modules/Network/Kit/NetErrorCode.cs`
**命名空间：** `NovaFramework.Runtime`
**类签名：** `public static class NetErrorCode`

---

## 2. 公开 API

### 成功码

| 常量 | 值 | 说明 |
|---|---|---|
| `SUCCESS` | `0` | 请求成功 |

### 服务端通用段（正数，服务端返回）

| 常量 | 值 | 说明 |
|---|---|---|
| `PARAM_ERROR` | `1000` | 参数错误 |
| `SERVER_ERROR` | `5000` | 服务器内部错误 |
| `AES_ERROR` | `6000` | 服务端 AES 加解密错误 |
| `APPID_MISSING` | `6001` | 服务端未收到 `app_id` |

### 客户端段（负数，本地流程错误，不经过服务端）

| 常量 | 值 | 说明 |
|---|---|---|
| `NETWORK_ERROR` | `-1` | 网络不可用或 HTTP 请求失败 |
| `AES_DECRYPT_FAILED` | `-2` | 客户端 AES 解密失败 |
| `PROTO_PARSE_FAILED` | `-3` | Proto 反序列化失败 |
| `URL_NOT_FOUND` | `-4` | NetCmd 未找到对应 URL |
| `AES_ENCRYPT_FAILED` | `-5` | 客户端 AES 加密失败（通常是 Key/IV 未配置） |

---

## 3. 使用示例

```csharp
var resp = await Nova.Network.Kit<Login>().Async(cmdRow, ChannelType.Google, openId);
if (!resp.IsSuccess)
{
    switch (resp.ErrorCode)
    {
        case NetErrorCode.NETWORK_ERROR:
            // 提示网络不可用
            break;
        case NetErrorCode.AES_ENCRYPT_FAILED:
            // 配置未就绪，通常是 AES Key/IV 缺失
            break;
        case NetErrorCode.SERVER_ERROR:
            // 服务端 5000 通用内部错误
            break;
        default:
            // 业务专属错误码（如 LoginErrorCode 段 7000~7999）
            break;
    }
}
```

---

## 4. 内部约束

- **业务专属错误码不在本类**：登录、支付等业务段错误码由各业务子包（如 `LoginErrorCode`）独立定义，约定使用 7000~7999 等区段，避免与本类冲突。
- **负数 = 客户端本地错误**：负数段可作为"服务端从未参与"的快速判断依据。

---

## 5. 关联

- 同包：[NetResponse.md](./NetResponse.md) — `ErrorCode` 字段的值域
- 同包：[NetService.md](./NetService.md) — 产生各错误码的具体分支
- 同包：[NetworkComponent.md](./NetworkComponent.md)
