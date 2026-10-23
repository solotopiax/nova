# NetService

## 1. 简介

`NetService` 是网络请求静态编排器，封装固定的 Protobuf + AES-128-CBC 请求全流程（URL 解析 → 序列化 → 使用 `Nova.Config.AppConfigs.AppAesKey/AppAesIV` 加密 → HTTP POST → 使用同一配置解密 → BaseResponse 解析 → 业务 Proto 解析），并持有全局 UID、OpenID。

发送时会一次性冻结请求字节与请求头，底层使用 UnityWebRequest 与系统 DNS 按主域名、备用域名顺序请求；业务 Service 不需要感知或决定切换策略。

**所在文件：** `Assets/Framework/Scripts/Runtime/Modules/Network/Kit/NetService.cs`
**命名空间：** `NovaFramework.Runtime`

> 此类的 C# 可见性是 public，但 SendAsync、身份写入、清理与互斥 API 面向 Sibling Kit 包并带 `[EditorBrowsable(Never)]`，业务不能直接绕过门面。项目新增标准 Proto 请求通过 [Nova.Network.SendProtoAsync](NetworkComponent.md#项目标准-proto-请求) 复用本链；登录、绑定和存档仍使用已有 Kit 专属入口。

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

- **结果日志边界**：`NetService` 不输出请求或响应的通用 JSON 包装；成功结果由具体业务层按业务语义输出。HTTP、解密、协议解析及服务端业务错误继续使用明确的 Warning/Error 表达，不再额外打印 `source/stage/name/httpStatusCode/rawDataLength` 等包装字段。
- **固定加解密链路**：每个业务请求均从 `Nova.Config.AppConfigs.AppAesKey/AppAesIV` 取 AES Key/IV，发送前加密、收到响应后解密。
- **无需手动注入**：`UID`、`OpenID` 有默认值；AES Key/IV 由运行时的 `Nova.Config.AppConfigs.AppAesKey/AppAesIV` 自动读取。
- **仅进程内身份**：`UID` 与 `OpenID` 都不写 PlayerPrefs、FileFragment 或 SQLite；进程重启后均为空，必须重新登录恢复。
- **身份对不可撕裂**：UID/OpenID 使用同一把锁成对读写；旧 `SetUID/SetOpenID` 仅保留兼容入口，Nova 内部不得继续分步更新。
- **身份变更互斥**：Login、Delete、Bind、Resolve 共用一个非排队租约；竞争调用立即返回 `IDENTITY_OPERATION_IN_PROGRESS(-6)`，QueryConflict 不修改身份，因此不占租约。
- **业务结果为身份真相源**：响应 Header 仅作请求身份回显，`NetService.SendAsync` 不用它覆盖缓存；Login 使用登录响应 UID，Bind/Resolve 使用各自成功结果同步身份。
- **AES Key/IV 校验**：`SendAsync` 先确认 `Nova.Config.LoadAsync()` 已完成，并校验 `AppAesKey / AppAesIV` 均为非空的 UTF-8 16 字节字符串；任一条件不满足都会记录配置入口、返回 `NetErrorCode.AES_ENCRYPT_FAILED` 且不发出 HTTP 请求。配置路径为 `Nova/Open Config → 通用配置 → 应用配置 → App Aes Key / App Aes IV`，按当前 `Platform × Channel × DevelopMode` 配置后重新导出 `ConfigRuntimeSO`。
- **配置分域**：Network 只使用 `AppConfigs.AppAesKey / AppAesIV` 作为应用协议凭据，绝不回退到隐私配置的默认 AES Key/IV。
- **`AppID` 解析**：`Nova.Config.AppConfigs.AppID` 必须可解析为 `int32`，解析失败时 `Log.Warning` + 回退 0。
- **`HttpResponse` 池化**：`SendAsync` 内部使用 `ReferencePool.Put(httpResponse)` 在 `finally` 块归还，调用方无需手动释放。
- **通信层终止条件**：服务器返回任意正式 HTTP 响应（包括 4xx/5xx）后立即结束候选链，业务成功和业务失败都不再触发主备切换；只有未获得正式 HTTP 响应的传输失败才继续尝试。
- **失败返回与异常的边界**：拿到响应但 `IsSuccess=false` 或正文为 `null` 时返回 `NETWORK_ERROR`；解密失败返回 `AES_DECRYPT_FAILED`；BaseResponse 或成功业务体解析失败返回 `PROTO_PARSE_FAILED`。服务端已有业务错误码时，附带业务体解析失败只丢弃 `data`，仍透传原错误码和描述。底层 POST 自身抛异常不在这里转换为上述错误码，而是经过 `finally` 后继续传播；调用尚未取得响应时没有可归还对象，已经取得的响应始终由此处唯一归池。协议解析失败也不会重放已结束的 HTTP 候选链。
- 三参数 SendAsync 转交 `CancellationToken.None`；四参数重载先检查预取消。它始终 POST，不根据 `INetworkCmdRow.Way` 分派，不自动给请求补 Head；Nova.Network 缺失、序列化或 POST 本身异常可直接传播，不能将所有失败等同返回 Fail。
- 身份锁只保证 SetIdentity/GetIdentity 的成对快照；分开读 UID 与 OpenID 可能跨越一次写入。SetUID/SetOpenID 仍是独立兼容写，未做服务端校验或持久化。ClearIdentity 不归还身份操作租约，也不取消请求；租约必须由取得者在 using/finally 中 Dispose，遗失会使后续竞争持续失败。
- UTF-8 16 字节不是 16 个字符，也不限定 ASCII；本地长度合法不能证明服务器使用相同凭据。

---

## 5. 关联

- 同包：[NetBuilder.md](./NetBuilder.md) — 序列化、加密、Header JSON 构建
- 同包：[NetResponse.md](./NetResponse.md) — 返回值结构
- 同包：[NetErrorCode.md](./NetErrorCode.md) — 错误码定义
- 同包：[NetworkComponent.md](./NetworkComponent.md) — `Kit<T>()` 入口
