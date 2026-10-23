# EditorUtil.Network.Http

`EditorUtil.Network.Http` 是 Editor 管理协议共用的 JSON HTTP 门面，只编译进 `NovaFramework.Editor`。

## 默认 Head

`CreateDefaultHeader()` 从当前导出的 `ConfigRuntimeSO` 与 `PlayerSettings` 生成：

| JSON 字段 | 类型 | 来源 |
|---|---|---|
| `app_id` | integer | `ConfigRuntimeSO.AppConfigs.AppID`，必须是正 int32 |
| `platform` | string | 导出平台：Android / iOS / WebGL |
| `channel` | string | 导出渠道：Official / Google / Apple / WeChat / TikTok / Alipay |
| `develop_mode` | string | `ConfigRuntimeSO.DevelopMode`，只输出 `Debug` 或 `Release` |
| `version` | string | `PlayerSettings.bundleVersion` |

序列化后的默认 Head 固定为以下结构：

```json
{
  "app_id": 1007,
  "platform": "Android",
  "channel": "Google",
  "develop_mode": "Debug",
  "version": "1.0.0"
}
```

默认 Head 只包含上述五个字段，不包含 `environment`、`package_id` 或运行期玩家身份字段。具体 Editor 业务协议需要额外字段时，应在自己的 Body DTO 中声明，不能改变公共 Head 的含义。

当前 Unity Active BuildTarget 与导出平台不一致，或任一必填值无效时，方法会抛出异常。已有 Runtime 快照的调用方应使用 `CreateDefaultHeader(runtime)`，让 Head 与业务数据来自同一导出物。

这些字段只参考 `PbNetReqHeader` 的应用、版本、平台和渠道语义，不序列化 Proto。玩家态的 `uid`、`language`、`devid`、`openid` 不进入 Editor Head。

## JSON 请求

`SendJsonAsync` 使用共享 `HttpClient` 发送单次 UTF-8 JSON 请求，`Content-Type` 固定为 `application/json; charset=utf-8`。地址必须是绝对 HTTP/HTTPS URI，调用方不能覆盖 `Content-Type`。

每次调用使用独立超时和取消令牌。调用方取消保留 `OperationCanceledException`，门面超时转换为 `TimeoutException`，网络异常保持原类型。非 2xx 响应不会被转换为异常，状态码、原因、正文及普通/内容响应头会复制到不可变 `Response` 中。

门面不解析 HostKey 或 NetCmd，不注入 Token，不重试，也不记录可能包含凭据的请求头。业务层负责地址、鉴权和响应语义。

## 传输与 DTO 边界

RequestHeader 验证正 int32 AppID、精确大小写平台/渠道和模式、非空版本，但不 Trim，构造只存内存。Response 的发送链先复制普通与内容头；直接调用构造器只是保存传入头集合，不自行深拷贝或重算状态码与成功标志。

请求 JSON 只校非 null，不解析语法，GET 等方法也可携带内容。额外头使用 TryAddWithoutValidation，保护 Content-Type，但不承诺完整校验每个值。链接令牌的非调用方取消统一转 TimeoutException，也可能来自发送器自行取消；注入发送器若返回未缓冲流，ReadAsStringAsync 未传令牌，超时不保证中断读正文。CTS 支持的最大超时仍可能在底层抛出。超时错误含完整 URI，调用者不要把凭据放入 URI。没有自动重试、Token 注入或 HostKey 路由解析。
