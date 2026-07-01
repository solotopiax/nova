# LoginBind（登录绑定二选一协议）

## 1. 简介

本文档描述登录触发绑定冲突时的二选一协议，对应服务端 `/v1/account/bind/resolve`。
登录（`/v1/user/loginV1`）返回 `ErrBindConflict(10402)` 且响应携带 `guest_summary` / `existing_summary` 时，客户端需调用 `Login.BindResolveAsync` 让玩家在 guest（当前账号）与 existing（云端已有账号）之间二选一。

> 协议对齐基线：`/v1/user/loginV1`（V1 形态，channel 已移入请求头 `head.channel`）。旧版 `/v1/user/login` 不再支持。

**所在文件：** `Nova/Protos/pb_net_login.proto`（`BindSummary` / `PbNetBindResolveReq` / `PbNetBindResolveResp` 均定义于本 proto）
**生成代码：** `Nova/Scripts/Runtime/Protos/PbNetLogin.cs`
**命名空间：** `NovaFramework.Kit.Network.GameLogin.Runtime`

---

## 2. 触发条件

登录请求携带 `channel != UNSPECIFIED && open_id != ""` 时，登录同时走三方绑定（account.DoBind）。当 `open_id` 已绑他人且双方账号都有「有意义进度」时，服务端返回 `ErrBindConflict(10402)`，并在响应里附带双方账号摘要。

**「有意义进度」判定**：`level / vip_level / gold / diamond / exp` 任一 > 0 即视为有真实进度。

---

## 3. BindSummary（二选一账号摘要）

被 `PbNetLoginResp.guest_summary` (field 8) 与 `PbNetLoginResp.existing_summary` (field 9) 引用。

| 字段 | 序号 | 类型 | 说明 |
|---|---|---|---|
| uid | 1 | int64 | 账号 ID |
| level | 2 | int32 | 等级 |
| vip_level | 3 | int32 | VIP 等级 |
| gold | 4 | int64 | 金币 |
| diamond | 5 | int64 | 钻石 |
| exp | 6 | int64 | 经验 |

---

## 4. PbNetLoginResp 新增字段（本轮）

| 字段 | 序号 | 类型 | 说明 |
|---|---|---|---|
| guest_summary | 8 | BindSummary | 仅 `ErrBindConflict(10402)` 时有值：当前账号（guest）摘要 |
| existing_summary | 9 | BindSummary | 仅 `ErrBindConflict(10402)` 时有值：已有账号（existing）摘要 |

> proto3 向前兼容：旧客户端忽略这两个字段，不影响原有登录响应解析。

---

## 5. PbNetBindResolveReq（二选一请求）

| 字段 | 序号 | 类型 | 必填 | 说明 |
|---|---|---|---|---|
| head | 1 | PbNetReqHeader | 是 | 请求公共头（含 channel；uid 即 guest_uid，经 device_id 顶号校验） |
| provider | 2 | int32 | 是 | 三方平台（与 PbNetChannel 枚举值对齐，直接透传） |
| open_id | 3 | string | 是 | 冲突的三方标识（服务端自查 existing_uid，不接受客户端传） |
| choice | 4 | string | 是 | `guest`=保留当前进度 / `existing`=保留云端进度 |
| verify_code | 5 | string | 否 | 二次验证（高危操作防盗号，按业务开启） |

---

## 6. PbNetBindResolveResp（二选一响应）

| 字段 | 序号 | 类型 | 说明 |
|---|---|---|---|
| head | 1 | PbNetRespHeader | 响应公共头 |
| uid | 2 | string | 最终选中的主账号 |
| abandoned_uid | 3 | string | 被放弃但保留的 uid |

> 任何一选都不合并数据。选 guest → open_id 改绑 guest_uid，existing 标 abandoned；选 existing → open_id 保持绑 existing，guest 标 abandoned。

---

## 7. 登录绑定行为矩阵

登录携带 `channel + open_id` 时的处理（走 account.DoBind）：

| 场景 | 返回码 | 响应字段 | 客户端动作 |
|---|---|---|---|
| open_id 未被绑 → 首次绑定 | OK(0) | uid=本账号 | 正常进入 |
| open_id 已绑本 uid → 幂等 | OK(0) | uid=本账号 | 正常进入 |
| open_id 绑别人，对方有进度且本号有进度 | ErrBindConflict(10402) | uid + guest_summary + existing_summary | 走 BindResolveAsync 二选一 |
| open_id 绑别人，对方有进度但本号无进度 | OK(0) | uid=对方账号（登入 existing） | 切到 existing 账号 |
| open_id 绑别人，对方无进度 | OK(0) | uid=本账号（迁移，open_id 改绑本号） | 正常进入 |
| open_id 被他人占用（换绑场景） | ErrOpenidAlreadyBound(10401) | — | 提示已占用 |
| open_id 缺失/格式非法 | ErrThirdPartyAuthFailed(10403) | — | 提示参数错误 |

---

## 8. 客户端衔接示例

```csharp
var login = Nova.Network.Kit<Login>();

// 1. 登录
var loginResp = await login.Async(string.Empty, openId);
if (loginResp.IsSuccess)
{
    // 正常进入
}
else if (loginResp.ErrorCode == LoginErrorCode.ErrBindConflict)
{
    // 2. 二选一：读取双方摘要展示给玩家
    BindSummary guest = loginResp.Data.GuestSummary;
    BindSummary existing = loginResp.Data.ExistingSummary;
    // ... UI 展示 guest.level / existing.level 等，让玩家选择 ...

    // 3. 玩家选择后发起二选一
    var resolveResp = await login.BindResolveAsync(
        provider: (int)PbNetChannel.Google,
        openId: openId,
        choice: "guest",   // 或 "existing"
        verifyCode: null);

    if (resolveResp.IsSuccess)
    {
        string finalUid = login.UID;  // 已自动写回最终选中的主账号
        // abandoned_uid = resolveResp.Data.AbandonedUid
    }
}
```

---

## 9. 错误码

错误码常量定义于 `LoginErrorCode`，详见 [LoginErrorCode.md](./LoginErrorCode.md)。
本轮新增触发的关键码：`ErrBindConflict = 10402`。

---

## 10. 关联

- 同包：[Login.md](./Login.md) — 登录 Service（`Async` / `BindResolveAsync` / `DeleteAsync`）
- 同包：[LoginErrorCode.md](./LoginErrorCode.md) — 登录/绑定业务错误码
- 同包：[LoginKitConfig.md](./LoginKitConfig.md) — `BindResolveCmdName` 配置
