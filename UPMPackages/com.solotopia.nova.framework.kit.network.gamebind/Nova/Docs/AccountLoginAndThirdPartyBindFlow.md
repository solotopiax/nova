# 账号登录与三方绑定业务流程

本文是 GameLogin、GameBind 与 GameSave 的业务编排手册，面向接入方和 AI。字段级结构仍以各 package 的 Proto 与 API 文档为准。

## 1. 账号状态

| 状态 | 含义 | 客户端关键数据 |
|---|---|---|
| 游客账号 | 只有游戏 UID，没有三方绑定 | UID、`device_id` |
| 已绑定账号 | UID 已绑定一个三方账号 | UID、`device_id`、渠道、OpenID |
| 冲突待选择 | 当前账号要绑定的 OpenID 已归属其他 UID | guest UID、目标 OpenID、existing UID、双方进度摘要 |

UID 是游戏账号，OpenID 是第三方平台用户标识。两个 UID 的游戏数据不会由服务端自动合并；同一 OpenID 同一时刻只归属一个 UID。

## 2. 最重要的 Header 规则

请求 Header 和业务 Body 中的 OpenID 语义不同：

| 位置 | 语义 | 客户端来源 |
|---|---|---|
| `PbNetReqHeader.openid` | 当前 UID 已拥有的身份声明 | `NetService.OpenID` |
| Login Body `open_id` | 本次登录使用的第三方凭据 | `Login.Async(..., openid, ...)` 参数 |
| Bind Body `open_id` | 本次要绑定、查询或裁决的目标 | Bind 方法的 `openid` 参数 |

当 Header 同时携带渠道与 OpenID 时，服务端会验证该 OpenID 当前绑定的 UID 是否等于 `head.uid`，不一致直接返回 `10407`，不会继续执行登录、绑定或裁决。

因此：

- Header OpenID 只能声明当前身份，不能放“准备绑定的目标 OpenID”。
- `BindAsync`、`QueryConflictAsync`、`ResolveAsync` 的目标 OpenID 只进入业务 Body。
- 游客账号尚未绑定第三方账号时，`NetService.OpenID` 应为空，Bind 请求 Header 也不会携带 OpenID。
- 响应 Header 的 OpenID 是请求身份回显，不是绑定或裁决后的权威归属结果。

## 3. 登录与顶号

### 3.1 登录入口

- 没有有效 UID：按设备登录或注册游客账号。
- 已知 UID：携带 UID 和当前 `device_id` 登录。
- 已绑定第三方账号：客户端必须同时恢复 UID，并以 UID + OpenID 登录。

当前服务端不支持“只有已绑定 OpenID、UID 为空”直接找回账号；该请求会因身份归属不一致返回 `10407`。产品如需跨设备第三方账号恢复，必须先提供 UID 恢复机制。

三方登录只读取既有绑定关系，不产生绑定副作用；未绑定返回 `10404`，业务应先进入游客账号，再走 GameBind。

### 3.2 顶号发生在哪里

每次成功登录都会把 UID 的最新 `device_id` 更新为当前设备。登录接口是“成为最新设备”的入口，不会因为换设备而拒绝本次登录。

```text
A 以 UID=100 登录 -> 服务端最新设备=A
B 以 UID=100 登录 -> 服务端最新设备=B
A 不重新登录，访问受保护接口 -> 10400 ErrKicked
B 访问受保护接口 -> 正常
```

所以测试顶号必须让旧设备在另一设备登录后直接访问受保护接口；旧设备若再次登录，会重新成为最新设备，无法证明之前没有顶号。

裁决成功后，服务端也会把当前设备设置为 `final_uid` 的最新设备，避免刚切换账号就立即被顶号。

## 4. 正常绑定与冲突

### 4.1 正常绑定

1. 当前 UID 已登录。
2. 第三方 SDK 完成授权，业务取得目标 OpenID。
3. 调用 `BindAsync(provider, openid)`。
4. 目标从未绑定时，服务端将其绑定到当前 UID。
5. 目标已绑定当前 UID 时，按幂等成功处理。

Bind 成功后，客户端把目标 OpenID 写入 `NetService.OpenID`。网络中断导致结果不确定时可以安全重试。

### 4.2 绑定冲突

目标 OpenID 已属于其他 UID 时，Bind 返回 `10402` 和 `existing_uid`，此时不修改任何账号数据：

```text
BindAsync(target OpenID)
  -> 10402 + existing_uid
QueryConflictAsync(target OpenID)
  -> existing_summary
玩家比较当前本地进度与 existing 摘要
ResolveAsync(target OpenID, guest/existing)
  -> final_uid + abandoned_uid
```

- guest 侧进度由客户端本地读取。
- existing 侧摘要由冲突查询返回，目前包含 UID 与最近存档时间。
- 存档时间只用于展示，不能自动决定保留哪一边。
- 必须由玩家明确选择，不自动合并或覆盖。

## 5. 裁决与存档编排

| 选择 | OpenID 最终归属 | `final_uid` | `abandoned_uid` |
|---|---|---|---|
| `guest` | 当前游客 UID | guest UID | existing UID |
| `existing` | 原已绑定 UID | existing UID | guest UID |

Resolve 成功后，GameBind 使用业务响应的 `final_uid` 更新 `NetService.UID`，并将目标 OpenID 写入 `NetService.OpenID`。响应 Header 不负责这次身份切换。

服务端只裁决账号归属，不合并、不迁移、不删除两边游戏数据。业务层随后处理存档：

```csharp
var resolve = await bind.ResolveAsync(openid, choice);
if (!resolve.IsSuccess)
{
    return;
}

if (choice == "guest")
{
    // 保留本地进度：上传当前本地整包。
    await Nova.Network.Kit<Save>().SetFullAsync(localPayload);
}
else
{
    // Resolve 已把进程内身份切到 final_uid：拉取云端整包覆盖本地。
    var cloud = await Nova.Network.Kit<Save>().GetFullAsync();
    // 由业务层写入本地持久化。
}
```

业务层还应隔离或清理 `abandoned_uid` 的本地会话与未上传临时数据，避免继续用错误 UID 写入。

## 6. 错误码与业务动作

### 登录

| 错误码 | 含义 | 业务动作 |
|---:|---|---|
| `10006` | 缺少设备 ID | 修正设备标识后重试 |
| `10000` | UID 不存在 | 清理失效 UID，进入游客登录或账号恢复 |
| `10007` | 账号锁定 | 停止登录，进入解锁或客服流程 |
| `10008` | 账号封禁 | 停止登录并提示封禁状态 |
| `10011` | 账号已删除 | 清理当前会话 |
| `10404` | OpenID 未绑定 | 先登录游客账号，再走绑定 |
| `10407` | Header OpenID 与 UID 不匹配 | 刷新身份；目标 OpenID 不得放 Header |

### 绑定与裁决

| 错误码 | 含义 | 业务动作 |
|---:|---|---|
| `10401` | OpenID 被占用或当前 UID 已绑定其他 OpenID | 不覆盖绑定，按产品策略引导 |
| `10402` | 目标 OpenID 已绑定其他 UID | 查询冲突摘要并展示二选一 |
| `10403` | OpenID、渠道或选择参数无效 | 修正授权结果或参数，不自动重试 |
| `10404` | 查询或裁决时绑定已不存在 | 刷新绑定状态并重走流程 |
| `10406` | 事务忙、锁竞争或超时 | 短暂退避，重新查询冲突后重试 |
| `10407` | Header 当前身份声明错误 | 修正或清空当前身份 OpenID |

### 通用

| 错误码 | 含义 | 业务动作 |
|---:|---|---|
| `10400` | 当前设备不是 UID 最新设备 | 停止会话，回到登录流程 |
| `1000` / `1001` | 请求无法解析或缺少参数 | 修复客户端参数 |
| `10003` | 非登录接口 UID 非法 | 清理 UID 后重新登录 |
| `5000` / `5001` | 服务或数据库错误 | 通用退避重试，不自行切换归属 |
| `5011` | App ID 未配置 | 核对包体、`app_id` 和服务端配置 |

顶号和 OpenID 归属校验可能以 HTTP 200 返回，客户端必须读取响应业务 `code`，不能把 HTTP 200 当作业务成功。

## 7. 推荐状态机

```text
未登录
  -> 设备登录 / 已知 UID 登录 -> 当前账号

当前账号
  -> 正常游戏
  -> Bind 成功 -> 已绑定
  -> Bind 10402 -> 冲突待选择
  -> 受保护接口 10400 -> 未登录

冲突待选择
  -> 选择 guest -> final_uid=guest -> 上传本地存档
  -> 选择 existing -> final_uid=existing -> 拉取云端存档
  -> 10401/10402/10404 -> 刷新冲突状态
  -> 10406 -> 退避后重新查询
```

## 8. 能力边界

- 第三方 token 真实性由上层第三方授权流程保证，GameBind 接收已取得的 OpenID。
- GameLogin 负责登录鉴权；GameBind 负责绑定归属；GameSave 和业务层负责数据覆盖。
- UID/OpenID 只缓存在 `NetService` 进程内，框架不持久化；进程重启后必须由登录流程恢复。
- `final_uid` 是裁决后继续游戏的唯一当前 UID；`abandoned_uid` 数据保留，但不再持有目标 OpenID。

## 9. 相关文档

- [Bind.md](./Bind.md)
- [BindErrorCode.md](./BindErrorCode.md)
- [BindKitConfig.md](./BindKitConfig.md)
- GameLogin package：`Nova/Docs/Login.md`
