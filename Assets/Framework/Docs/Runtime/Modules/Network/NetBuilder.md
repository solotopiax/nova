# NetBuilder

## 1. 简介

`NetBuilder` 是网络请求构建静态工具类，承接全部「构建 / 加密」职责：Header 构建（从 `Nova.Config.AppConfigs` / `Nova.SDK` 自动读取字段，渠道由 `InferChannel()` 自动填充）、Proto Body 序列化、AES-128-CBC 加密、HTTP Header JSON 拼装。

**所在文件：** `Assets/Framework/Scripts/Runtime/Modules/Network/Kit/NetBuilder.cs`
**命名空间：** `NovaFramework.Runtime`

> 整类标注 `[EditorBrowsable(EditorBrowsableState.Never)]`，**业务侧 IDE 补全中不可见**。仅供 `NetService` 与 Sibling Kit 包（如 `Login`）内部调用。

---

## 2. 公开 API

> 所有方法均为 `public static`，但整类带 `[EditorBrowsable(Never)]`——业务侧勿直接调用。

| 签名 | 说明 |
|---|---|
| `public static PbNetReqHeader BuildHeader()` | 构建完整请求 Header；UID/OpenID 始终来自 `NetService.GetIdentity` 的同一份已确认身份快照，不接受候选身份参数 |
| `public static byte[] SerializeBody<T>(T body) where T : IMessage<T>` | 将 Proto 消息序列化为字节数组（`body.ToByteArray()`） |
| `public static byte[] Encrypt(byte[] plainBytes, string key, string iv)` | AES-128-CBC + PKCS7 加密；委托 `Util.Encrypt.AES.EncryptBytes` |
| `public static string BuildHeaderInfos(int appId, string aesIV)` | 构建正式环境 HTTP Header JSON：`{"app_id":N,"Encoding-Aes":"Base64(iv)"}` |
| `public static string BuildDebugHeaderInfos(int appId)` | 构建调试环境 HTTP Header JSON：`{"app_id":N,"X-Debug-Plain":"true"}` |

### Header 字段来源表

| 字段 | 来源 | 失败处理 |
|---|---|---|
| `Appid` | `Nova.Config.AppConfigs.AppID`（int.TryParse） | 解析失败 Log.Warning + 回退 0 |
| `Version` | `Application.version` | — |
| `Language` | `LanguageMetadata.GetFlag(Nova.Localization.Language)` | — |
| `Devid` | `Nova.SDK.TryGet<IDeviceIdProvider>().GetDeviceID()` | 未注册时回退空串 |
| `Platform` | `Util.UrlTemplate.ResolveRuntimePlatform()`（按当前编译目标推断 `PbNetPlatform`） | 未匹配平台返回 `Unspecified` |
| `Channel` | `Nova.Config.Channel`（`ChannelType`，由 `InferChannel()` 映射为 `PbNetChannel`） | `ChannelType.None` 或未知值返回 `Unspecified` |
| `Uid` / `Openid` | `NetService.GetIdentity(out uid, out openid)` | 原子读取；未确认身份时均为空串，Proto3 不写入 wire |

---

## 3. 使用示例

> 以下示例演示 `Login.Async` 内部如何使用 `NetBuilder`：

```csharp
// Login.cs (kit.network.login) 内部调用模式
var body = new PbNetLoginReq
{
    Head = NetBuilder.BuildHeader(), // 只声明此前已由服务端确认的身份
    Openid = openid,                 // 本次登录候选身份只进入 Body
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
- **`Channel` 只表示游戏运营渠道**：`BuildHeader()` 通过私有 `InferChannel()` 将 `Nova.Config.Channel` 的 `ChannelType` 同名映射为 `PbNetChannel`。`Official / Google / Apple / WeChat / TikTok / Alipay` 均有对应值，`None` 或未知值返回 `PbNetChannel.Unspecified`；该字段与第三方登录提供方无关。
- **当前身份与目标身份分离**：Header UID/OpenID 只能声明此前已由服务端确认的身份；Login/Bind 的候选或目标身份只放业务 Body。
- **字段硬改名**：Proto `app_id/device_id` 已改为 `appid/devid`，生成属性对应为 `Appid/Devid`；字段号和 wire 类型不变，但 JSON/descriptor 名称及 C# 属性名为破坏性变化。
- **无状态**：所有方法均无副作用，线程安全；UID/OpenID 状态由 `NetService` 持有。

---

## 5. 关联

- 同包：[NetService.md](./NetService.md) — 调用 `NetBuilder` 的编排器
- 同包：[NetErrorCode.md](./NetErrorCode.md) — 加密失败时使用的错误码
- 同包：[NetworkComponent.md](./NetworkComponent.md)
- 主框架：[../../Utils/Util.Encrypt.md](../../Utils/Util.Encrypt.md) — AES 加解密底层实现
- 主框架：[../SDK/Plugins/Device/IDeviceIdProvider.md](../SDK/Plugins/Device/IDeviceIdProvider.md) — DeviceId 来源接口
- ADR-020（程序集依赖方向单向）
