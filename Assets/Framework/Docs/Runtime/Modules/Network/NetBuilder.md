# NetBuilder

## 1. 简介

`NetBuilder` 是网络请求构建静态工具类，承接全部「构建 / 加密」职责：Header 构建（从 `Nova.Config.Common` / `Nova.SDK` 自动读取字段，渠道由 `InferChannel()` 自动填充）、Proto Body 序列化、AES-128-CBC 加密、HTTP Header JSON 拼装。

**所在文件：** `Assets/Framework/Scripts/Runtime/Modules/Network/Kit/NetBuilder.cs`
**命名空间：** `NovaFramework.Runtime`

> 整类标注 `[EditorBrowsable(EditorBrowsableState.Never)]`，**业务侧 IDE 补全中不可见**。仅供 `NetService` 与 Sibling Kit 包（如 `Login`）内部调用。

---

## 2. 公开 API

> 所有方法均为 `public static`，但整类带 `[EditorBrowsable(Never)]`——业务侧勿直接调用。

| 签名 | 说明 |
|---|---|
| `public static PbNetReqHeader BuildHeader()` | 构建完整请求 Header；包含 Channel（取 `Nova.Config.Channel` 映射的 `PbNetChannel`）；字段来源见下方「Header 字段来源表」 |
| `public static byte[] SerializeBody<T>(T body) where T : IMessage<T>` | 将 Proto 消息序列化为字节数组（`body.ToByteArray()`） |
| `public static byte[] Encrypt(byte[] plainBytes, string key, string iv)` | AES-128-CBC + PKCS7 加密；委托 `Util.Encrypt.AES.EncryptBytes` |
| `public static string BuildHeaderInfos(int appId, string aesIV)` | 构建正式环境 HTTP Header JSON：`{"app_id":N,"Encoding-Aes":"Base64(iv)"}` |
| `public static string BuildDebugHeaderInfos(int appId)` | 构建调试环境 HTTP Header JSON：`{"app_id":N,"X-Debug-Plain":"true"}` |

### Header 字段来源表

| 字段 | 来源 | 失败处理 |
|---|---|---|
| `AppId` | `Nova.Config.Common.AppID`（int.TryParse） | 解析失败 Log.Warning + 回退 0 |
| `Version` | `Application.version` | — |
| `Language` | `LanguageMetadata.GetFlag(Nova.Localization.Language)` | — |
| `DeviceId` | `Nova.SDK.TryGet<IDeviceIdProvider>().GetDeviceID()` | 未注册时回退空串 |
| `Platform` | `Application.platform`（switch 推断 `PbNetPlatform`） | 未匹配平台返回 `Unspecified` |
| `Channel` | `Nova.Config.Channel`（`InferChannel()` 私有方法映射为 `PbNetChannel`） | 无匹配渠道返回 `Unspecified` |
| `Uid` | `NetService.Uid` | 登录前为空串 |

---

## 3. 使用示例

> 以下示例演示 `Login.Async` 内部如何使用 `NetBuilder`：

```csharp
// Login.cs (kit.network.login) 内部调用模式
var body = new PbNetLoginReq
{
    Head = NetBuilder.BuildHeader(),   // 自动填充所有公共 Header 字段，含渠道（Channel）
    OpenId = openId,
    ForceNewAccount = forceNewAccount
};
byte[] protoBytes = NetBuilder.SerializeBody(body);
byte[] encryptedBytes = NetBuilder.Encrypt(protoBytes, aesKey, aesIv);
string headerJson = NetBuilder.BuildHeaderInfos(appId, aesIv);
```

---

## 4. 内部约束

- **整类 `[EditorBrowsable(Never)]`**：类级别标注，业务侧 Visual Studio / Rider 补全中不显示任何成员。
- **`Encrypt` 委托框架层**：加密逻辑委托 `Util.Encrypt.AES.EncryptBytes`，`NetBuilder` 只做职责归属封装，不实现加密算法。
- **`Platform` 映射范围**：仅 iOS / Android / WebGL 有明确映射，其余平台（含 Editor / Standalone）返回 `PbNetPlatform.Unspecified`；这是有意设计，非遗漏。
- **`Channel` 由 `BuildHeader` 自动填充**：渠道在 `BuildHeader()` 内通过私有 `InferChannel()` 从 `Nova.Config.Channel` 自动映射为 `PbNetChannel`，业务 Service 不要在 body 里手动赋值 `Channel`（违反 PAT-107）。`ChannelType.Google → PbNetChannel.Google`、`ChannelType.Apple → PbNetChannel.Apple`、`ChannelType.WeChat → PbNetChannel.Wechat`，其余渠道返回 `PbNetChannel.Unspecified`。
- **无状态**：所有方法均无副作用，线程安全。

---

## 5. 关联

- 同包：[NetService.md](./NetService.md) — 调用 `NetBuilder` 的编排器
- 同包：[NetErrorCode.md](./NetErrorCode.md) — 加密失败时使用的错误码
- 同包：[NetworkComponent.md](./NetworkComponent.md)
- 主框架：[../../Utils/Util.Encrypt.md](../../Utils/Util.Encrypt.md) — AES 加解密底层实现
- 主框架：[../SDK/Plugins/Device/IDeviceIdProvider.md](../SDK/Plugins/Device/IDeviceIdProvider.md) — DeviceId 来源接口
- ADR-020（程序集依赖方向单向）
