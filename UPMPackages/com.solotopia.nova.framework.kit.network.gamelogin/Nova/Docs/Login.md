# Login

## 1. 简介

`Login` 是登录业务网络 Service，封装登录协议发送逻辑全链路（Header 构建 → Proto 序列化 → AES 加密 → HTTP POST → 解析 → UID 写回）。登录成功后自动将 UID 写回本实例属性与 `NetService` 静态字段，后续所有请求 Header 自动携带 `Uid`。

**所在文件：** `Nova/Scripts/Runtime/Login.cs`
**命名空间：** `NovaFramework.Kit.Network.GameLogin.Runtime`
**类签名：** `public sealed partial class Login`

> 通过 `Nova.Network.Kit<Login>()` 获取实例，不继承任何基类，无参构造即可使用。

---

## 2. 公开 API

### 属性

| 签名 | 说明 |
|---|---|
| `public string UID { get; private set; }` | 当前已登录用户 UID；登录成功后自动写回；登出后清空；初值为空字符串 |
| `public bool IsLoggedIn => !string.IsNullOrEmpty(UID)` | 当前是否已登录（UID 非空）；只读派生属性 |

### 方法

| 签名 | 说明 |
|---|---|
| `public void SetDebugMode(bool debugMode)` | 设置本实例调试模式覆盖；仅影响本实例发出的请求；`false` 时不等于关闭全局，仅取消覆盖 |
| `public UniTask<NetResponse<PbNetLoginResp>> Async(string uid, string openId, bool forceNewAccount = false)` | 发起登录请求（极简入口）；`uid` 非空时优先填入请求 Header，否则沿用 `NetService.Uid`（登录态自动写回值）；cmdName 从 `LoginKitConfig.LoginCmdName` 取，channel 从 `Nova.Config.Channel` 取；`LoginKitConfig` 未配置时抛 `KitConfigMissingException`；成功后自动写回 UID |
| `public UniTask<NetResponse<PbNetDeleteResp>> DeleteAsync()` | 删除当前登录账号（极简入口）；身份靠 `Header.Uid`（即 `NetService.Uid`，当前登录态）识别，业务侧无需传参；cmdName 取自 `LoginKitConfig.DeleteCmdName`；`LoginKitConfig` 未配置时抛 `KitConfigMissingException`；删除成功后自动清空本实例 `UID` 与 `NetService.Uid`（语义等同登出） |
| `public UniTask<NetResponse<PbNetBindResolveResp>> BindResolveAsync(int provider, string openId, string choice, string verifyCode = null)` | 绑定冲突二选一入口；登录返回 `LoginErrorCode.ErrBindConflict`(10402) 且响应带 `guest_summary`/`existing_summary` 时调用；身份靠 `Header.Uid`（即 guest_uid）识别，服务端自查 existing_uid；cmdName 取自 `LoginKitConfig.BindResolveCmdName`；`LoginKitConfig` 未配置时抛 `KitConfigMissingException`；成功后最终选中主账号 uid 自动写回 `UID` 与 `NetService.Uid` |
| `public void Clear()` | 清空本实例 `UID` 与 `NetService.Uid` 静态字段；后续请求 Header 不再携带 Uid |

---

## 3. 使用示例

```csharp
// 前提：ConfigWindow 已配置 LoginKitConfig.CmdName（如 "GameLogin"）

// 获取 Login Service 实例
var login = Nova.Network.Kit<Login>();

// 发起登录，openId 由第三方 SDK 回调提供；uid 留空则沿用登录态 uid
var resp = await login.Async(string.Empty, openId);

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

- **UID 双写**：登录成功后 `UID` 同时写入本实例属性和 `NetService.Uid` 静态字段，确保后续由其他 Service 发出的请求 Header 也携带 Uid。登出与删号成功时同样双清。
- **删号语义 = 账号不存在 = 强制登出**：`DeleteAsync` 成功后自动清空 `UID` 与 `NetService.Uid`，防止继续以失效 Uid 发后续请求。删号失败时登录态不变。
- **`DeleteAsync` cmdName 取自 `DeleteCmdName`**：`DeleteAsync` 内部取 `LoginKitConfig.DeleteCmdName` 解析为指令行；在 ConfigWindow 中为 `LoginKitConfig.DeleteCmdName` 填入对应 NetCmd 名称并重导出后方可正常调用。
- **`LoginKitConfig` 必须配置**：`Async` 内部通过 `Nova.Config.GetKitConfig<LoginKitConfig>()` 取配置，未在 ConfigWindow 配置 `LoginKitConfig` 时抛 `KitConfigMissingException`（开发期 fail-fast，暴露漏配）。
- **channel 来源**：`Async` 内部取 `Nova.Config.Channel`，业务侧无需传入；渠道在 ConfigWindow 全局配置一次即可。
- **`SetDebugMode` 覆盖语义**：`m_DebugModeOverride` 为 `bool?`，调用 `SetDebugMode(true/false)` 后会覆盖全局 `NetService.IsDebugMode`；若需恢复跟随全局，暂无公开 API，需重新 `Kit<Login>()` 获取新实例（全局 Kit 实例由 `NetworkComponent` 管理，视具体注册策略而定）。
- **`ChannelType` 映射范围**：`ChannelType.Google / Apple / WeChat` 有明确 Proto 映射；`TikTok / Official / Alipay` 及其他渠道统一映射为 `PbNetChannel.Unspecified`。
- **`Head` 自动填充**：`NetBuilder.BuildHeader()` 在 `SendAsync` 内部调用，业务侧无需手动构建 Header。
- **依赖主框架公共网络编排层**：`NetService.SendAsync` / `NetBuilder.BuildHeader` / `NetResponse<T>` 均来自主框架包 `com.solotopia.nova.framework` 的 Network Kit 公共层。
- **失败分支码值归类**：`SendAsync` / `SendBindResolveAsync` 失败时调 `LogLoginError`，按 `LoginErrorCode` 常量归类码值打可读日志；不改变返回值，业务侧仍按 `resp.ErrorCode` 自行分支。命中 `ErrBindConflict`(10402) 时日志提示走 `BindResolveAsync`。
- **绑定二选一流程**：登录返回 `ErrBindConflict`(10402) 且响应带 `guest_summary` / `existing_summary` 时，业务侧读取双方摘要展示给玩家，再调 `BindResolveAsync` 二选一；成功后最终选中主账号 uid 自动写回 `UID` 与 `NetService.Uid`。详见 [LoginBind.md](./LoginBind.md)。

---

## 5. 关联

- 同包：[LoginErrorCode.md](./LoginErrorCode.md) — 登录业务段错误码
- 同包：[LoginKitConfig.md](./LoginKitConfig.md) — 登录 Kit 配置
- 同包：[LoginBind.md](./LoginBind.md) — 绑定二选一协议
