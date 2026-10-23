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
| `public static string BuildHeaderInfos(int appId, string aesIV)` | 构建 HTTP Header JSON：`{"app_id":N,"Encoding-Aes":"Base64(iv)"}` |

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
byte[] encryptedBytes = NetBuilder.Encrypt(
    protoBytes,
    Nova.Config.AppConfigs.AppAesKey,
    Nova.Config.AppConfigs.AppAesIV
);
string headerJson = NetBuilder.BuildHeaderInfos(appId, Nova.Config.AppConfigs.AppAesIV);
```

---

## 4. 内部约束

- **整类 `[EditorBrowsable(Never)]`**：类级别标注，业务侧 Visual Studio / Rider 补全中不显示任何成员。
- **`Encrypt` 委托框架层**：加密逻辑委托 `Util.Encrypt.AES.EncryptBytes`，`NetBuilder` 只做职责归属封装，不实现加密算法。
- **固定 AES 配置来源**：`NetService` 固定将 `Nova.Config.AppConfigs.AppAesKey/AppAesIV` 传入 `Encrypt` 与 `BuildHeaderInfos`；业务请求一律构建 AES Header 并加密 Body。
- **`Platform` 映射范围**：仅 iOS / Android / WebGL 有明确映射；Editor 跟随当前 Build Target，不使用宿主运行平台，其余目标返回 `PbNetPlatform.Unspecified`。
- **`Channel` 只表示游戏运营渠道**：`BuildHeader()` 通过私有 `InferChannel()` 将 `Nova.Config.Channel` 的 `ChannelType` 同名映射为 `PbNetChannel`。`Official / Google / Apple / WeChat / TikTok / Alipay` 均有对应值，`None` 或未知值返回 `PbNetChannel.Unspecified`；该字段与第三方登录提供方无关。
- **当前身份与目标身份分离**：Header UID/OpenID 只能声明此前已由服务端确认的身份；Login/Bind 的候选或目标身份只放业务 Body。
- **字段硬改名**：Proto `app_id/device_id` 已改为 `appid/devid`，生成属性对应为 `Appid/Devid`；字段号和 wire 类型不变，但 JSON/descriptor 名称及 C# 属性名为破坏性变化。
- BuildHeader 不等待 Config/SDK/Localization 初始化；缺少入口或配置、设备 ID getter 异常仍可传播，不能把静态工具理解为任意线程安全。它每次构造新 Header，但不会自动写入任意业务 Body 的 Head 字段。
- SerializeBody、Encrypt 与 Header JSON 编码不在本层统一捕获异常；IV 的 Base64 是 UTF-8 字节编码，不含密钥，也不是凭据有效性验证。`EditorBrowsable(Never)` 只影响 IDE 补全，不是 C# 访问控制。

`pb_net_header.proto` 的请求字段号依次为 appid=1、uid=2、version=3、language=4、devid=5、platform=6、channel=7、openid=8；响应 timestamp=1 表达毫秒时间戳，响应身份不自动覆盖 NetService。`pb_net_base_response.proto` 的 code=1/message=2/data=3 由 NetParser 先解外壳，再交业务 parser。空解密字节返回协议失败，畸形 Proto 的 Parser 异常交调用层处理，合法外壳不证明服务端已部署同一协议。

---

## 5. 关联

- 同包：[NetService.md](./NetService.md) — 调用 `NetBuilder` 的编排器
- 同包：[NetErrorCode.md](./NetErrorCode.md) — 加密失败时使用的错误码
- 同包：[NetworkComponent.md](./NetworkComponent.md)
- 主框架：[../../Utils/Util.Encrypt.md](../../Utils/Util.Encrypt.md) — AES 加解密底层实现
- 主框架：[../SDK/Plugins/Device/IDeviceIdProvider.md](../SDK/Plugins/Device/IDeviceIdProvider.md) — DeviceId 来源接口
- ADR-020（程序集依赖方向单向）
