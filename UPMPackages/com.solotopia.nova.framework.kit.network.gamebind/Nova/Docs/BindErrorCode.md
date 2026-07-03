# BindErrorCode

## 1. 简介

`BindErrorCode` 是账号绑定业务错误码常量类。服务端绑定业务段（10400~10499）由服务端原样返回，经 `NetService` 透传到 `NetResponse.ErrorCode`，业务侧用本类常量与 `ErrorCode` 比对。

**所在文件：** `Nova/Scripts/Runtime/BindErrorCode.cs`
**命名空间：** `NovaFramework.Kit.Network.GameBind.Runtime`
**类签名：** `public static class BindErrorCode`

---

## 2. 错误码常量

| 码 | 常量 | 含义 | 客户端动作 |
|---|---|---|---|
| 0 | `OK` | 成功 | 绑定成功，继续游戏 |
| 10400 | `ErrKicked` | device_id 非最新，被顶号 | 提示重新登录 |
| 10401 | `ErrOpenidAlreadyBound` | 该三方号已被他人占用（不支持改绑，无法迁到本账号） | 提示已被占用 |
| 10402 | `ErrBindConflict` | 绑定冲突，需二选一 | `BindAsync` 返回时调 `QueryConflictAsync` 拉详情 → 玩家二选一 → 调 `ResolveAsync` 裁决；`ResolveAsync` 返回时（并发复核到归属变化）提示重试 |
| 10403 | `ErrThirdPartyAuthFailed` | open_id 缺失或格式非法（三方鉴权失败） | 提示参数错误 |
| 10406 | `ErrBindBusy` | 操作繁忙，请稍后重试（`ResolveAsync` 行锁竞争 / 事务超时时返回） | 稍后原样重试 |

---

## 3. 使用示例

```csharp
var resp = await Nova.Network.Kit<Bind>().BindAsync((int)PbNetChannel.Google, openId);
if (!resp.IsSuccess)
{
    switch (resp.ErrorCode)
    {
        case BindErrorCode.ErrBindConflict:
            // 走冲突二选一流程
            break;
        case BindErrorCode.ErrOpenidAlreadyBound:
            // 提示该三方号已被占用
            break;
        case BindErrorCode.ErrThirdPartyAuthFailed:
            // 提示三方鉴权失败
            break;
    }
}
```

裁决重试分支：`ResolveAsync` 返回 `ErrBindBusy`(10406) 或 `ErrBindConflict`(10402，并发复核到归属变化) 时，客户端原样重试即可：

```csharp
var resolve = await Nova.Network.Kit<Bind>().ResolveAsync(openId, choice);
if (!resolve.IsSuccess &&
    (resolve.ErrorCode == BindErrorCode.ErrBindBusy || resolve.ErrorCode == BindErrorCode.ErrBindConflict))
{
    // 稍后原样重试同一次 ResolveAsync 调用
}
```

---

## 4. 内部约束

- **段位互斥**：`NetErrorCode` 使用负数（客户端）+ 1000/5000/6000/6001（服务端通用）；绑定业务段从 10400 起，与其他业务段错开。
- **不强制在此类扩展服务端通用错误**：服务端通用协议级错误（如 PARAM_ERROR/SERVER_ERROR）统一用 `NetErrorCode`；本类仅收录账号绑定语义相关的业务错误码。
- **`ErrKicked`(10400) 为设备维度通用码**：绑定各接口经 device_id 顶号校验时可能触发，与登录路径的顶号语义一致。
- **`ErrBindBusy`(10406) 可重试**：`ResolveAsync` 行锁竞争 / 事务超时时返回，语义为临时性繁忙，客户端原样重试即可，不改变入参。

---

## 5. 关联

- 同包：[Bind.md](./Bind.md) — 账号绑定业务 Service
