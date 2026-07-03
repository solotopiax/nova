# Bind

## 1. 简介

`Bind` 是账号绑定业务网络 Service，封装绑定、冲突查询、裁决三段协议的发送逻辑全链路（Header 构建 → Proto 序列化 → AES 加密 → HTTP POST → 解析）。

**所在文件：** `Nova/Scripts/Runtime/Bind.cs`
**命名空间：** `NovaFramework.Kit.Network.GameBind.Runtime`
**类签名：** `public sealed partial class Bind`

> 通过 `Nova.Network.Kit<Bind>()` 获取实例，不继承任何基类，无参构造即可使用。
>
> **职责边界：** 本类只负责账号归属裁决（open_id 绑哪个 uid、冲突时谁为主），**不处理存档数据覆盖**（本地覆盖云端 / 云端覆盖本地），也**不改动本地登录态**。数据流向与登录态切换由业务层配合 `GameSave` / `GameLogin` 模块编排。

---

## 2. 公开 API

### 前置配置

`Bind` 所有接口的 `cmdName` 由 `BindKitConfig` 统一提供，需在 ConfigWindow「Kit 配置」中填写：

| 配置字段 | 说明 |
|---|---|
| `BindCmdName` | 绑定协议的 NetCmd 指令名（如 `GameAccountBind`），`BindAsync` 使用 |
| `BindConflictCmdName` | 冲突查询协议的 NetCmd 指令名（如 `GameAccountBindConflict`），`QueryConflictAsync` 使用 |
| `BindResolveCmdName` | 裁决协议的 NetCmd 指令名（如 `GameAccountBindResolve`），`ResolveAsync` 使用 |

> 若 `BindKitConfig` 未在 ConfigWindow 启用，调用任何入口将抛出 `KitConfigMissingException`（开发期 fail-fast）。

### 方法

| 签名 | 说明 |
|---|---|
| `public void SetDebugMode(bool debugMode)` | 设置本实例调试模式覆盖；仅影响本实例发出的请求；`false` 时不等于关闭全局，仅取消覆盖 |
| `public UniTask<NetResponse<PbNetBindResp>> BindAsync(int provider, string openId)` | 为当前账号绑定三方 openId；身份靠 `Header.Uid`（即 `NetService.Uid`，当前登录态）识别；命中 `ErrBindConflict`(10402) 时响应带 `ExistingUid`，需继续调 `QueryConflictAsync` + `ResolveAsync`；cmdName 取自 `BindKitConfig.BindCmdName` |
| `public UniTask<NetResponse<PbNetBindConflictResp>> QueryConflictAsync(string openId)` | 查询绑定冲突详情，拉取对方账号（existing）进度摘要供玩家二选一决策；服务端自查 existing_uid，guest 侧摘要客户端本地取；cmdName 取自 `BindKitConfig.BindConflictCmdName` |
| `public UniTask<NetResponse<PbNetBindResolveResp>> ResolveAsync(string openId, string choice, string verifyCode = null)` | 绑定冲突裁决；玩家二选一后调用，服务端做纯账号归属裁决，返回 `FinalUid` + `AbandonedUid`；不处理存档数据、不改动本地登录态；cmdName 取自 `BindKitConfig.BindResolveCmdName` |

### 参数选值

| 参数 | 选值 |
|---|---|
| `provider` | 与 `NovaFramework.Runtime.PbNetChannel` 枚举值对齐：Facebook=1 / Google=2 / Apple=3 / Wechat=4（0=Unspecified 禁用） |
| `choice` | `"guest"`=保留当前账号（当前登录态账号） / `"existing"`=保留对方账号（open_id 已绑的云端账号） |

### 协议数据结构

| 类型 | 说明 |
|---|---|
| `BindSummary { string Uid; long Timestamp }` | 账号摘要；`Uid` 为对方账号 ID，`Timestamp` 为最后一次上传服务器时间（秒级 Unix），辅助玩家二选一决策 |
| `PbNetBindReq { PbNetReqHeader Head; int Provider; string OpenId }` | 绑定请求；`Head.Uid` 为被绑定的当前账号 |
| `PbNetBindResp { PbNetRespHeader Head; string ExistingUid }` | 绑定响应；`ExistingUid` 仅 `ErrBindConflict`(10402) 时有值 |
| `PbNetBindConflictReq { PbNetReqHeader Head; string OpenId }` | 冲突查询请求 |
| `PbNetBindConflictResp { PbNetRespHeader Head; BindSummary ExistingSummary }` | 冲突查询响应；对方账号进度摘要 |
| `PbNetBindResolveReq { PbNetReqHeader Head; string OpenId; string Choice; string VerifyCode }` | 裁决请求 |
| `PbNetBindResolveResp { PbNetRespHeader Head; string FinalUid; string AbandonedUid }` | 裁决响应；`FinalUid` 为裁决后主账号，`AbandonedUid` 为被放弃但保留的账号 |

---

## 3. 使用示例

> **前提：** 在 ConfigWindow「Kit 配置」中启用 `BindKitConfig`，填写三个 CmdName；玩家已通过 `Nova.Network.Kit<Login>()` 登录（`Header.Uid` 为当前账号）。

```csharp
var bind = Nova.Network.Kit<Bind>();

// 1. 绑定
var bindResp = await bind.BindAsync((int)PbNetChannel.Google, openId);
if (bindResp.IsSuccess)
{
    // 绑定成功，继续游戏
}
else if (bindResp.ErrorCode == BindErrorCode.ErrBindConflict)
{
    // 2. 冲突：拉取对方账号进度摘要
    var conflict = await bind.QueryConflictAsync(openId);
    if (!conflict.IsSuccess) return;
    BindSummary existing = conflict.Data.ExistingSummary;

    // 3. UI 展示 guest（本地存档取）与 existing（服务端返回）进度，玩家二选一
    string choice = /* "guest" 或 "existing" */;

    // 4. 纯裁决
    var resolve = await bind.ResolveAsync(openId, choice);
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
| `"existing"` | 保留云端进度 → 云端覆盖本地 | 切登录态到 `finalUid`（`Login.Async(finalUid, "")`）→ `Save.GetFullAsync()` 拉云端 → 覆盖本地持久化 |

```csharp
if (choice == "guest")
{
    // 本地覆盖云端
    await Nova.Network.Kit<Save>().SetFullAsync(localPayload);
}
else // "existing"
{
    // 云端覆盖本地：先切登录态到 finalUid，再拉云端整包
    if (finalUid != Nova.Network.Kit<Login>().UID)
    {
        await Nova.Network.Kit<Login>().Async(finalUid, string.Empty);
    }
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

- **身份靠 `Header.Uid`**：三个接口的 `Header` 均由 `NetBuilder.BuildHeader()` 自动填充，`Header.Uid` 取自 `NetService.Uid`（当前登录态），业务侧无需传 uid；绑定前提是已登录。
- **`BindAsync` 冲突信号**：`ErrBindConflict`(10402) 响应仅带 `ExistingUid`（轻量），不带摘要；摘要需另调 `QueryConflictAsync` 获取。
- **`ResolveAsync` 不碰数据、不碰登录态**：裁决只返回账号归属（`FinalUid` / `AbandonedUid`）；数据覆盖与登录态切换由业务层显式编排，本类不做任何隐式写回。
- **不支持解绑/改绑**：系统不提供独立的解绑/改绑接口；open_id 已绑他人时仅"双方均有进度"触发 10402 二选一，其余情况返回 `ErrOpenidAlreadyBound`(10401)。
- **`Head` 自动填充**：所有入口内部调用 `NetBuilder.BuildHeader()`，业务侧无需手动构建 Header。
- **失败分支码值归类**：三个接口失败时按 `BindErrorCode` 归类码值打可读日志（`LogBindError`），不改变返回值，业务侧仍按 `resp.ErrorCode` 自行分支。
- **依赖主框架公共网络编排层**：`NetService.SendAsync` / `NetBuilder.BuildHeader` / `NetResponse<T>` / `INetworkCmdRow` 均来自主框架包 `com.solotopia.nova.framework` 的 Network Kit 公共层。

---

## 6. 关联

- 同包：[BindErrorCode.md](./BindErrorCode.md) — 账号绑定业务段错误码
- 同包：[BindKitConfig.md](./BindKitConfig.md) — 账号绑定 Kit 配置
