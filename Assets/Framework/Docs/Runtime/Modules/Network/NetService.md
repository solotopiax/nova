# NetService

## 1. 简介

`NetService` 是网络请求静态编排器，封装 Protobuf + AES-128-CBC 请求全流程（URL 解析 → 序列化 → 加密 → HTTP POST → 解密 → BaseResponse 解析 → 业务 Proto 解析），并持有全局 UID、OpenID 和调试开关。

**所在文件：** `Assets/Framework/Scripts/Runtime/Modules/Network/Kit/NetService.cs`
**命名空间：** `NovaFramework.Runtime`

> 此类整体面向 Sibling Kit 包（如 `kit.network.login`）使用，**不面向业务侧**。业务侧 IDE 补全中，`SendAsync`、`SetUID` 和 `SetOpenID` 均因 `[EditorBrowsable(Never)]` 而被隐藏。业务侧请通过 `Nova.Network.Kit<Login>()` 接入 `Login` 等业务 Service。

---

## 2. 公开 API

### 业务侧可读属性

| 签名 | 说明 |
|---|---|
| `public static string UID { get; private set; }` | 当前 UID；由 Login/Bind 根据权威业务结果同步，进程重启归空 |
| `public static string OpenID { get; private set; }` | 当前 OpenID；由 Login/Bind 根据权威业务结果同步，进程重启归空 |
| `public static bool IsDebugMode { get; private set; }` | 全局调试开关；调试模式下跳过 AES 加解密，发送 `X-Debug-Plain` 头；默认 `false` |

### 业务侧可用方法

| 签名 | 说明 |
|---|---|
| `public static void SetDebugMode(bool debugMode)` | 设置全局调试模式开关；通常在初始化阶段或通过 `NetworkComponentKitExtensions.SetDebugMode` 调用 |

### Sibling Kit 包内部 API（`[EditorBrowsable(Never)]`，业务侧勿直接调用）

| 签名 | 说明 |
|---|---|
| `[EditorBrowsable(Never)] public static void SetUID(string uid)` | 写回当前 UID；`null` 时写空串；Kit 处理权威业务结果或清理登录态时使用 |
| `[EditorBrowsable(Never)] public static void SetOpenID(string openid)` | 写回当前 OpenID；`null` 时写空串；Kit 处理权威业务结果或清理登录态时使用 |
| `[EditorBrowsable(Never)] public static async UniTask<NetResponse<TResp>> SendAsync<TReq, TResp>(INetworkCmdRow cmdRow, TReq request, MessageParser<TResp> parser, bool? debugModeOverride = null)` | 完整请求链路；由各业务 Service（如 `Login`）调用；`TReq : IMessage<TReq>`，`TResp : IMessage<TResp>` |

---

## 3. 使用示例

> 以下示例演示 Sibling Kit 包（如 `Login`）如何调用 `SendAsync`；业务侧不应直接使用此 API。

```csharp
// Login.cs (kit.network.login)
var resp = await NetService.SendAsync(
    cmdRow,
    body,              // 已填充 Head 的 PbNetLoginReq
    PbNetLoginResp.Parser,
    m_DebugModeOverride
);
// SendAsync 只解析并返回响应；Login/Bind 再按业务结果同步 UID/OpenID。
```

> 业务侧调试开关设置（通过 NetworkComponentKitExtensions 或直接调用均可）：

```csharp
// 通过扩展方法（推荐，保持接口一致性）
Nova.Network.SetDebugMode(true);

// 或直接调用
NetService.SetDebugMode(true);
```

---

## 4. 内部约束

- **统一诊断日志**：Editor 与 Development Build 中，每次请求在实际 HTTP 发送前通过 `Log.Debug` 输出请求 Proto JSON；BaseResponse 与业务 Proto 解析后通过 `Log.Debug` 输出响应 JSON。日志均为单行 JSON，便于 Console 检索和外部工具解析：

  ```json
  {"source":"Nova.NetService","stage":"request","name":"Login","url":"https://example.com/login","sent":true,"data":{}}
  {"source":"Nova.NetService","stage":"response","name":"Login","httpStatusCode":200,"code":0,"msg":"","data":{}}
  ```

  只有已进入实际 HTTP 发送阶段的请求才使用 `Log.Debug`。URL 缺失、AES 配置缺失或加密失败等未发送请求使用 `Log.Warning`，并输出 `"sent":false` 与 `reason`；不会产生误导性的 Debug 请求日志。业务 Proto 无法解析时，响应日志的 `data` 为 `null`，并追加 `parseError`。正式非 Development Build 会移除这些日志调用，避免输出 UID、OpenID、验证码和存档等敏感内容，也避免 JSON 序列化开销。
- **日志与明文模式分离**：统一诊断日志不复用 `IsDebugMode`；`IsDebugMode` 仍只控制 AES 加解密和 `X-Debug-Plain`，不会决定日志是否输出。
- **无需初始化**：`UID`、`OpenID` 和 `IsDebugMode` 有默认值，配置在运行时从 `Nova.Config.AppConfigs.AppAesKey / AppAesIV` 自动读取，不需要业务侧手动注入。
- **仅进程内身份**：`UID` 与 `OpenID` 都不写 PlayerPrefs、FileFragment 或 SQLite；进程重启后均为空，必须重新登录恢复。
- **业务结果为身份真相源**：响应 Header 仅作请求身份回显，`NetService.SendAsync` 不用它覆盖缓存；Login 使用登录响应 UID，Bind/Resolve 使用各自成功结果同步身份。
- **AES Key/IV 校验**：非调试模式下若 Key 或 IV 为空，`SendAsync` 立即返回 `NetErrorCode.AES_ENCRYPT_FAILED` 而不发出 HTTP 请求。
- **`AppID` 解析**：`Nova.Config.AppConfigs.AppID` 必须可解析为 `int32`，解析失败时 `Log.Warning` + 回退 0。
- **`debugModeOverride`**：每次 `SendAsync` 可通过此参数覆盖全局 `IsDebugMode`，仅影响单次请求。
- **`HttpResponse` 池化**：`SendAsync` 内部使用 `ReferencePool.Put(httpResponse)` 在 `finally` 块归还，调用方无需手动释放。

---

## 5. 关联

- 同包：[NetBuilder.md](./NetBuilder.md) — 序列化、加密、Header JSON 构建
- 同包：[NetResponse.md](./NetResponse.md) — 返回值结构
- 同包：[NetErrorCode.md](./NetErrorCode.md) — 错误码定义
- 同包：[NetworkComponentKitExtensions.md](./NetworkComponentKitExtensions.md) — `SetDebugMode` 扩展方法入口
- 同包：[NetworkComponent.md](./NetworkComponent.md) — `Kit<T>()` 入口
- ADR-020（程序集依赖方向单向，`NetworkComponentKitExtensions` 设计动因）
