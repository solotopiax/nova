# NetService

## 1. 简介

`NetService` 是网络请求静态编排器，封装固定的 Protobuf + AES-128-CBC 请求全流程（URL 解析 → 序列化 → 使用 `Nova.Config.AppConfigs.AppAesKey/AppAesIV` 加密 → HTTP POST → 使用同一配置解密 → BaseResponse 解析 → 业务 Proto 解析），并持有全局 UID、OpenID。

**所在文件：** `Assets/Framework/Scripts/Runtime/Modules/Network/Kit/NetService.cs`
**命名空间：** `NovaFramework.Runtime`

> 此类整体面向 Sibling Kit 包使用，**不面向业务侧**。身份写入、清理与互斥 API 均带 `[EditorBrowsable(Never)]`。

---

## 2. 公开 API

### 业务侧可读属性

| 签名 | 说明 |
|---|---|
| `public static string UID { get; }` | 当前 UID；与 OpenID 共用同一身份锁，进程重启归空 |
| `public static string OpenID { get; }` | 当前 OpenID；与 UID 共用同一身份锁，进程重启归空 |

### Sibling Kit 包内部 API（`[EditorBrowsable(Never)]`，业务侧勿直接调用）

| 签名 | 说明 |
|---|---|
| `[EditorBrowsable(Never)] public static void SetUID(string uid)` | 写回当前 UID；`null` 时写空串；Kit 处理权威业务结果或清理登录态时使用 |
| `[EditorBrowsable(Never)] public static void SetOpenID(string openid)` | 写回当前 OpenID；`null` 时写空串；Kit 处理权威业务结果或清理登录态时使用 |
| `[EditorBrowsable(Never)] public static void SetIdentity(string uid, string openid)` | 原子写入 UID/OpenID 身份对；Nova 内部调用方统一使用此方法 |
| `[EditorBrowsable(Never)] public static void GetIdentity(out string uid, out string openid)` | 原子读取同一份 UID/OpenID 身份快照 |
| `[EditorBrowsable(Never)] public static void ClearIdentity()` | 原子清空 UID/OpenID |
| `[EditorBrowsable(Never)] public static IDisposable TryBeginIdentityOperation()` | 非排队获取全局身份操作租约；已有 Login/Delete/Bind/Resolve 时返回 `null` |
| `[EditorBrowsable(Never)] public static async UniTask<NetResponse<TResp>> SendAsync<TReq, TResp>(INetworkCmdRow cmdRow, TReq request, MessageParser<TResp> parser)` | 完整请求链路；由各业务 Service（如 `Login`）调用；`TReq : IMessage<TReq>`，`TResp : IMessage<TResp>` |

---

## 3. 使用示例

> 以下示例演示 Sibling Kit 包（如 `Login`）如何调用 `SendAsync`；业务侧不应直接使用此 API。

```csharp
// Login.cs (kit.network.login)
var resp = await NetService.SendAsync(
    cmdRow,
    body,              // 已填充 Head 的 PbNetLoginReq
    PbNetLoginResp.Parser
);
// SendAsync 只解析并返回响应；Login/Bind 再按业务结果同步 UID/OpenID。
```

---

## 4. 内部约束

- **统一诊断日志**：Editor 与 Development Build 中，每次请求在实际 HTTP 发送前通过 `Log.Debug` 输出请求 Proto JSON；BaseResponse 与业务 Proto 解析后通过 `Log.Debug` 输出响应 JSON。日志均为单行 JSON，便于 Console 检索和外部工具解析：

  ```json
  {"source":"Nova.NetService","stage":"request","name":"Login","url":"https://example.com/login","sent":true,"data":{}}
  {"source":"Nova.NetService","stage":"response","name":"Login","httpStatusCode":200,"code":0,"msg":"","data":{},"rawDataLength":64}
  ```

  只有已进入实际 HTTP 发送阶段的请求才使用 `Log.Debug`。URL 缺失、AES 配置缺失或加密失败等未发送请求使用 `Log.Warning`，并输出 `"sent":false` 与 `reason`；不会产生误导性的 Debug 请求日志。HTTP 失败、解密失败、BaseResponse 解析失败、业务 Proto 解析失败或传输异常也会输出一次响应终态；尚未获得的 `httpStatusCode` / `code` 为 `null`，并附 `failureStage`、`error` 与 `rawDataLength`。正式非 Development Build 会移除这些日志调用，避免输出 UID、OpenID、验证码和存档等敏感内容，也避免 JSON 序列化开销。
- **固定加解密链路**：每个业务请求均从 `Nova.Config.AppConfigs.AppAesKey/AppAesIV` 取 AES Key/IV，发送前加密、收到响应后解密。
- **无需手动注入**：`UID`、`OpenID` 有默认值；AES Key/IV 由运行时的 `Nova.Config.AppConfigs.AppAesKey/AppAesIV` 自动读取。
- **仅进程内身份**：`UID` 与 `OpenID` 都不写 PlayerPrefs、FileFragment 或 SQLite；进程重启后均为空，必须重新登录恢复。
- **身份对不可撕裂**：UID/OpenID 使用同一把锁成对读写；旧 `SetUID/SetOpenID` 仅保留兼容入口，Nova 内部不得继续分步更新。
- **身份变更互斥**：Login、Delete、Bind、Resolve 共用一个非排队租约；竞争调用立即返回 `IDENTITY_OPERATION_IN_PROGRESS(-6)`，QueryConflict 不修改身份，因此不占租约。
- **业务结果为身份真相源**：响应 Header 仅作请求身份回显，`NetService.SendAsync` 不用它覆盖缓存；Login 使用登录响应 UID，Bind/Resolve 使用各自成功结果同步身份。
- **AES Key/IV 校验**：`SendAsync` 先确认 `Nova.Config.LoadAsync()` 已完成，并校验 `AppAesKey / AppAesIV` 均为非空的 UTF-8 16 字节字符串；任一条件不满足都会记录配置入口、返回 `NetErrorCode.AES_ENCRYPT_FAILED` 且不发出 HTTP 请求。配置路径为 `Nova/Open Config → 通用配置 → 应用配置`，按当前 `Platform × Channel × DevelopMode` 配置后重新导出 `ConfigRuntimeSO`。
- **配置分域**：Network 只使用 `AppConfigs.AppAesKey / AppAesIV` 作为应用协议凭据，绝不回退到隐私配置的默认 AES Key/IV。
- **`AppID` 解析**：`Nova.Config.AppConfigs.AppID` 必须可解析为 `int32`，解析失败时 `Log.Warning` + 回退 0。
- **`HttpResponse` 池化**：`SendAsync` 内部使用 `ReferencePool.Put(httpResponse)` 在 `finally` 块归还，调用方无需手动释放。

---

## 5. 关联

- 同包：[NetBuilder.md](./NetBuilder.md) — 序列化、加密、Header JSON 构建
- 同包：[NetResponse.md](./NetResponse.md) — 返回值结构
- 同包：[NetErrorCode.md](./NetErrorCode.md) — 错误码定义
- 同包：[NetworkComponent.md](./NetworkComponent.md) — `Kit<T>()` 入口
