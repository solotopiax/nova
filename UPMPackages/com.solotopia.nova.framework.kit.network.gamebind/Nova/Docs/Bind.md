# Bind

## 1. 简介

`Bind` 是账号绑定业务网络 Service，封装绑定状态查询、绑定、冲突查询、裁决协议的发送逻辑全链路（Header 构建 → Proto 序列化 → AES 加密 → HTTP POST → 解析）。

**所在文件：** `Nova/Scripts/Runtime/Bind.cs`
**命名空间：** `NovaFramework.Kit.Network.GameBind.Runtime`
**类签名：** `public sealed partial class Bind`

> 通过 `Nova.Network.Kit<Bind>()` 获取实例，不继承任何基类，无参构造即可使用。
>
> **职责边界：** 本类只负责账号归属裁决（OpenID 绑定哪个 UID、冲突时谁为主），**不处理存档数据覆盖**。Bind/Resolve 成功后以权威业务结果同步 `NetService` 身份。
>
> 完整业务编排见 [账号登录与三方绑定业务流程](./AccountLoginAndThirdPartyBindFlow.md)。

---

## 2. 公开 API

### 前置配置

`Bind` 所有接口的 `cmdName` 由 `BindKitConfig` 统一提供，需在 ConfigWindow「Kit 配置」中填写：

| 配置字段 | 说明 |
|---|---|
| `BindCmdName` | 绑定协议的 NetCmd 指令名（如 `GameAccountBind`），`BindAsync` 使用 |
| `BindingQueryCmdName` | 绑定状态查询协议的 NetCmd 指令名（`GameAccountBindingQuery`），`QueryBindingAsync` 使用 |
| `BindConflictCmdName` | 冲突查询协议的 NetCmd 指令名（如 `GameAccountBindConflict`），`QueryConflictAsync` 使用 |
| `BindResolveCmdName` | 裁决协议的 NetCmd 指令名（如 `GameAccountBindResolve`），`ResolveAsync` 使用 |

> 若 `BindKitConfig` 未在 ConfigWindow 启用，调用任何入口将抛出 `KitConfigMissingException`（开发期 fail-fast）。

### 方法

| 签名 | 说明 |
|---|---|
| `public void SetDebugMode(bool debugMode)` | 设置本实例调试模式覆盖；仅影响本实例发出的请求；`false` 时不等于关闭全局，仅取消覆盖 |
| `public string OpenID` | 当前进程内已确认归属于当前 UID 的 OpenID；直接读取 `NetService.OpenID`，不持久化 |
| `public UniTask<NetResponse<PbNetBindResp>> BindAsync(ThirdLoginProvider provider, string openid)` | 为当前账号绑定目标 OpenID；Header 只携带当前身份，目标只进 Body；成功后更新 `NetService.OpenID`；命中 10402 时继续冲突流程 |
| `public UniTask<NetResponse<PbNetBindingQueryResp>> QueryBindingAsync(string openid)` | 查询指定 OpenID 是否已绑定及对应 UID；只读查询，不修改 `NetService` 身份；cmdName 取自 `BindKitConfig.BindingQueryCmdName` |
| `public UniTask<NetResponse<PbNetBindConflictResp>> QueryConflictAsync(string openid)` | 查询绑定冲突详情，拉取对方账号（existing）进度摘要供玩家二选一决策；服务端自查 existing_uid，guest 侧摘要客户端本地取；cmdName 取自 `BindKitConfig.BindConflictCmdName` |
| `public UniTask<NetResponse<PbNetBindResolveResp>> ResolveAsync(string openid, string choice, string verifyCode = null)` | 绑定冲突裁决；返回 `FinalUid` + `AbandonedUid`，不处理存档数据；成功后以 `FinalUid` 和目标 OpenID 同步身份 |

### 参数选值

| 参数 | 选值 |
|---|---|
| `provider` | `ThirdLoginProvider`：Facebook=1 / Google=2 / Apple=3 / Wechat=4（0=Unspecified 禁用）；与游戏运营渠道 `PbNetChannel` 无关 |
| `choice` | `"guest"`=保留当前账号（当前登录态账号） / `"existing"`=保留对方账号（open_id 已绑的云端账号） |

### 协议数据结构

| 类型 | 说明 |
|---|---|
| `BindSummary { string Uid; long Timestamp }` | 账号摘要；`Uid` 为对方账号 ID，`Timestamp` 为最后一次上传服务器时间（秒级 Unix），辅助玩家二选一决策 |
| `PbNetBindReq { PbNetReqHeader Head; int Provider; string OpenId }` | 绑定请求；`Head.Uid` 为被绑定的当前账号 |
| `PbNetBindResp { PbNetRespHeader Head; string ExistingUid }` | 绑定响应；`ExistingUid` 仅 `ErrBindConflict`(10402) 时有值 |
| `PbNetBindingQueryReq { PbNetReqHeader Head; string Openid }` | 绑定状态查询请求；目标 OpenID 位于 Body |
| `PbNetBindingQueryResp { PbNetRespHeader Head; bool IsBinded; string Uid }` | 绑定状态查询响应；未绑定时 `Uid` 为空 |
| `PbNetBindConflictReq { PbNetReqHeader Head; string OpenId }` | 冲突查询请求 |
| `PbNetBindConflictResp { PbNetRespHeader Head; BindSummary ExistingSummary }` | 冲突查询响应；对方账号进度摘要 |
| `PbNetBindResolveReq { PbNetReqHeader Head; string OpenId; string Choice; string VerifyCode }` | 裁决请求 |
| `PbNetBindResolveResp { PbNetRespHeader Head; string FinalUid; string AbandonedUid }` | 裁决响应；`FinalUid` 为裁决后主账号，`AbandonedUid` 为被放弃但保留的账号 |

---

## 3. 使用示例

> **前提：** 在 ConfigWindow「Kit 配置」中启用 `BindKitConfig`，填写三个 CmdName；玩家已通过 `Nova.Network.Kit<Login>()` 登录（`Header.Uid` 为当前账号）。

```csharp
var bind = Nova.Network.Kit<Bind>();

// 查询指定 OpenID 是否已经绑定；查询成功不会修改当前登录身份
var query = await bind.QueryBindingAsync(openid);
if (query.IsSuccess && query.Data.IsBinded)
{
    string boundUid = query.Data.Uid;
}

// 1. 绑定
var bindResp = await bind.BindAsync(ThirdLoginProvider.Google, openid);
if (bindResp.IsSuccess)
{
    // 绑定成功，继续游戏
}
else if (bindResp.ErrorCode == BindErrorCode.ErrBindConflict)
{
    // 2. 冲突：拉取对方账号进度摘要
    var conflict = await bind.QueryConflictAsync(openid);
    if (!conflict.IsSuccess) return;
    BindSummary existing = conflict.Data.ExistingSummary;

    // 3. UI 展示 guest（本地存档取）与 existing（服务端返回）进度，玩家二选一
    string choice = /* "guest" 或 "existing" */;

    // 4. 纯裁决
    var resolve = await bind.ResolveAsync(openid, choice);
    if (!resolve.IsSuccess) return;
    string finalUid = resolve.Data.FinalUid;

    // 5. 数据覆盖（业务层用 GameSave 编排，详见「4. 与云存档的衔接」）
    // 6. 用 finalUid 继续游戏
}
```

---

## 4. 与云存档的衔接（数据覆盖）

裁决只返回账号归属，**数据流向由业务层配合 `GameSave` 模块编排**。`choice` 决定覆盖方向：

| choice | 语义 | 业务层动作 |
|---|---|---|
| `"guest"` | 保留本地进度 → 本地覆盖云端 | `finalUid` 即当前账号；`Save.SetFullAsync(localPayload)` 上传本地整包到云端 |
| `"existing"` | 保留云端进度 → 云端覆盖本地 | Resolve 已按业务 `FinalUid` 同步 UID/OpenID → `Save.GetFullAsync()` 拉云端 → 覆盖本地持久化 |

```csharp
if (choice == "guest")
{
    // 本地覆盖云端
    await Nova.Network.Kit<Save>().SetFullAsync(localPayload);
}
else // "existing"
{
    // 云端覆盖本地：Resolve 已按业务 FinalUid 同步最终身份
    var cloud = await Nova.Network.Kit<Save>().GetFullAsync();
    if (cloud.IsSuccess)
    {
        // 业务侧用 cloud.Data.Datas 覆盖本地持久化
    }
}
```

> 由于数据覆盖交业务层编排，业务侧可自定义合并策略（如保留本地 Bag + 云端 Quest），不受协议约束。

---

## 5. 内部约束

- **Header 只声明当前身份**：四个接口均使用 `NetBuilder.BuildHeader()` 携带 `NetService.UID/OpenID`；方法参数 `openid` 是目标身份，只写入业务 Body。
- **身份操作互斥**：Bind/Resolve 与 Login/Delete 共用非排队租约，竞争时返回 `NetErrorCode.IDENTITY_OPERATION_IN_PROGRESS(-6)`；QueryConflict 只读，不占租约。
- **绑定状态查询只读**：`QueryBindingAsync` 只返回目标 OpenID 的绑定状态和 UID，不占身份操作租约，也不修改 `NetService.UID/OpenID`。
- **原子身份提交**：Bind 成功保留操作开始时的当前 UID 并与目标 OpenID 成对写入；Resolve 成功将 `FinalUid/OpenID` 成对写入。
- **生成属性硬切换**：三个 Proto Body 的 C# 属性由 `OpenId` 改为 `Openid`，字段号和 wire 类型不变。
- **裁决后的身份同步**：`ResolveAsync` 以业务响应 `FinalUid` 与目标 OpenID 覆盖进程内身份；响应 Header 只是请求身份回显。
- **仅进程内缓存**：Bind 不写 PlayerPrefs、FileFragment 或 SQLite；进程重启后 OpenID 为空。
- **`BindAsync` 冲突信号**：`ErrBindConflict`(10402) 响应仅带 `ExistingUid`（轻量），不带摘要；摘要需另调 `QueryConflictAsync` 获取。
- **`ResolveAsync` 不碰存档**：裁决返回账号归属（`FinalUid` / `AbandonedUid`）并更新进程内身份；存档覆盖仍由业务层显式编排。
- **不支持解绑/改绑**：系统不提供独立的解绑/改绑接口；目标 open_id 已绑他人时仅“双方均有进度”触发 10402 二选一，其余情况返回 `ErrOpenidAlreadyBound`(10401)；当前 UID 已绑定其他 OpenID 时返回 `ErrUIDAlreadyBoundOtherOpenID`(10408)。
- **`Head` 自动填充**：所有入口内部调用 `NetBuilder.BuildHeader()`，业务侧无需手动构建 Header。
- **失败分支码值归类**：四个接口失败时按 `BindErrorCode` 归类码值打可读日志（`LogBindError`），不改变返回值，业务侧仍按 `resp.ErrorCode` 自行分支。
- **依赖主框架公共网络编排层**：`NetService.SendAsync` / `NetBuilder.BuildHeader` / `NetResponse<T>` / `INetworkCmdRow` 均来自主框架包 `com.solotopia.nova.framework` 的 Network Kit 公共层。

---

## 6. 关联

- 同包：[BindErrorCode.md](./BindErrorCode.md) — 账号绑定业务段错误码
- 同包：[BindKitConfig.md](./BindKitConfig.md) — 账号绑定 Kit 配置
