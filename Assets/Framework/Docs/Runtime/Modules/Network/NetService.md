# NetService

## 1. 简介

`NetService` 是网络请求静态编排器，封装 Protobuf + AES-128-CBC 请求全流程（URL 解析 → 序列化 → 加密 → HTTP POST → 解密 → BaseResponse 解析 → 业务 Proto 解析），并持有全局 Uid 和调试开关。

**所在文件：** `Assets/Framework/Scripts/Runtime/Modules/Network/Kit/NetService.cs`
**命名空间：** `NovaFramework.Runtime`

> 此类整体面向 Sibling Kit 包（如 `kit.network.login`）使用，**不面向业务侧**。业务侧 IDE 补全中，`SendAsync` 和 `SetUid` 均因 `[EditorBrowsable(Never)]` 而被隐藏。业务侧请通过 `Nova.Network.Kit<Login>()` 接入 `Login` 等业务 Service。

---

## 2. 公开 API

### 业务侧可读属性

| 签名 | 说明 |
|---|---|
| `public static string Uid { get; private set; }` | 当前登录用户 Uid；登录成功后由 `Login` 通过 `SetUid` 写回；进程重启归空（不持久化） |
| `public static bool IsDebugMode { get; private set; }` | 全局调试开关；调试模式下跳过 AES 加解密，发送 `X-Debug-Plain` 头；默认 `false` |

### 业务侧可用方法

| 签名 | 说明 |
|---|---|
| `public static void SetDebugMode(bool debugMode)` | 设置全局调试模式开关；通常在初始化阶段或通过 `NetworkComponentKitExtensions.SetDebugMode` 调用 |

### Sibling Kit 包内部 API（`[EditorBrowsable(Never)]`，业务侧勿直接调用）

| 签名 | 说明 |
|---|---|
| `[EditorBrowsable(Never)] public static void SetUid(string uid)` | 写回当前登录 Uid；`null` 时写空串；仅供 `Login` 在登录成功后调用 |
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
if (resp.IsSuccess && resp.Data != null)
{
    NetService.SetUid(resp.Data.Uid);
}
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

- **无需初始化**：`Uid` 和 `IsDebugMode` 有默认值，配置在运行时从 `Nova.Config.Common.AppAesKey / AppAesIV` 自动读取，不需要业务侧手动注入。
- **AES Key/IV 校验**：非调试模式下若 Key 或 IV 为空，`SendAsync` 立即返回 `NetErrorCode.AES_ENCRYPT_FAILED` 而不发出 HTTP 请求。
- **`AppID` 解析**：`Nova.Config.Common.AppID` 必须可解析为 `int32`，解析失败时 `Log.Warning` + 回退 0。
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
