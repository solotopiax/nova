# Login

## 1. 简介

`Login` 是登录业务网络 Service，封装登录协议发送逻辑全链路（Header 构建 → Proto 序列化 → AES 加密 → HTTP POST → 解析）。登录成功后以业务响应 UID 和本次登录 OpenID 同步 `NetService`，后续请求自动携带当前身份。

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
| `public void SetDebugMode(bool debugMode)` | 设置本实例调试模式覆盖；仅影响本实例发出的请求；`false` 时不等于关闭全局，仅取消覆盖 |
| `public UniTask<NetResponse<PbNetLoginResp>> Async(string uid, string openid, bool forceNewAccount = false)` | 发起登录请求；`uid` 非空时优先填入请求 Header，否则沿用 `NetService.UID`；`openid` 同时进入业务 Body 与 Header 作为本次登录身份；成功后以业务响应 UID 同步身份；未绑返回 10404 |
| `public UniTask<NetResponse<PbNetDeleteResp>> DeleteAsync()` | 删除当前登录账号；身份靠 `Header.Uid`（即 `NetService.UID`）识别；删除成功后自动清空 UID、OpenID 与 NetService 进程内身份 |
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

- **身份写回**：登录成功后以 `PbNetLoginResp.Uid` 和本次登录 OpenID 更新 `NetService`；`Login.UID` 直接读取同一缓存。
- **仅进程内缓存**：UID、OpenID 都不由 GameLogin 写入 PlayerPrefs、FileFragment 或 SQLite；进程重启后必须重新登录。
- **强制新账号**：`forceNewAccount=true` 时以业务响应 UID 建立新会话，OpenID 保持为空，不产生绑定副作用。
- **最新设备与顶号**：每次成功登录都把 UID 的最新 `device_id` 更新为当前设备；旧设备后续访问受保护接口收到 `10400`。
- **跨设备三方登录**：当前服务端要求已绑定 OpenID 与 UID 一致；只有 OpenID、UID 为空会返回 `10407`，业务必须先恢复 UID。
- **删号语义 = 账号不存在 = 强制登出**：`DeleteAsync` 成功后自动清空 UID、OpenID，防止继续以失效身份发后续请求。删号失败时登录态不变。
- **`DeleteAsync` cmdName 取自 `DeleteCmdName`**：`DeleteAsync` 内部取 `LoginKitConfig.DeleteCmdName` 解析为指令行；在 ConfigWindow 中为 `LoginKitConfig.DeleteCmdName` 填入对应 NetCmd 名称并重导出后方可正常调用。
- **`LoginKitConfig` 必须配置**：`Async` 内部通过 `Nova.Config.GetKitConfig<LoginKitConfig>()` 取配置，未在 ConfigWindow 配置 `LoginKitConfig` 时抛 `KitConfigMissingException`（开发期 fail-fast，暴露漏配）。
- **channel 来源**：`Async` 内部取 `Nova.Config.Channel`，业务侧无需传入；渠道在 ConfigWindow 全局配置一次即可。
- **`SetDebugMode` 覆盖语义**：`m_DebugModeOverride` 为 `bool?`，调用 `SetDebugMode(true/false)` 后会覆盖全局 `NetService.IsDebugMode`；若需恢复跟随全局，暂无公开 API，需重新 `Kit<Login>()` 获取新实例（全局 Kit 实例由 `NetworkComponent` 管理，视具体注册策略而定）。
- **`ChannelType` 映射范围**：`ChannelType.Google / Apple / WeChat` 有明确 Proto 映射；`TikTok / Official / Alipay` 及其他渠道统一映射为 `PbNetChannel.Unspecified`。
- **`Head` 自动填充**：`NetBuilder.BuildHeader()` 在 `SendAsync` 内部调用，业务侧无需手动构建 Header。
- **依赖主框架公共网络编排层**：`NetService.SendAsync` / `NetBuilder.BuildHeader` / `NetResponse<T>` 均来自主框架包 `com.solotopia.nova.framework` 的 Network Kit 公共层。
- **失败分支码值归类**：`SendAsync` 失败时调 `LogLoginError`，按 `LoginErrorCode` 常量归类码值打可读日志；不改变返回值，业务侧仍按 `resp.ErrorCode` 自行分支。
- **登录不做绑定副作用**：`Async` 传入 `openid` 时服务端只“读”绑定关系（查 `open_id` 已绑的 UID 并登入），未绑返回 `ErrAccountNotFound`(10404)；为当前账号绑定三方号是 `GameBind` 模块的独立职责。

---

## 5. 关联

- 同包：[LoginErrorCode.md](./LoginErrorCode.md) — 登录业务段错误码
- 同包：[LoginKitConfig.md](./LoginKitConfig.md) — 登录 Kit 配置
- 跨包：`com.solotopia.nova.framework.kit.network.gamebind` — 账号绑定业务 Kit（`Bind`）
