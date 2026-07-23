# Alibaba Cloud OSS C# SDK v2 文档索引

## 定位

本包是阿里云 OSS C# SDK v2 的纯 Runtime 库封装，不属于 Nova `SDKManager` 插件。安装后直接使用 `AlibabaCloud.OSS.V2` 命名空间。

## Runtime 支持

| 平台 | 状态 | 说明 |
|---|---|---|
| Unity Editor | 支持 | 用于开发和联调 |
| Standalone | 支持 | 使用上游 `HttpClient` 传输 |
| Android | API 兼容，待验证 | 本次环境未安装 Android Player Support；需补 IL2CPP 构建及真机网络、证书验证 |
| iOS | API 兼容，待验证 | 本次环境未安装 iOS Player Support；需补 IL2CPP 构建及真机网络、证书验证 |
| WebGL | 未承诺 | 上游没有 UnityWebRequest/WebGL 专用传输，受浏览器 CORS 和网络能力限制 |

该结论表示程序集满足 Unity Player Runtime 的 API 基线，不等同于已覆盖所有设备网络环境。IL2CPP 使用包内 `link.xml` 保留 OSS 程序集，避免 `XmlSerializer` 使用的模型被裁剪；目标项目发版前仍必须完成对应平台的 IL2CPP 构建与真机回归。

## 客户端凭据

客户端只使用 STS 临时凭据或预签名 URL。不要把长期 AccessKey ID/Secret 写入代码、配置表、Prefab、环境包或远端可下载配置。

```csharp
using OSS = AlibabaCloud.OSS.V2;

var config = OSS.Configuration.LoadDefault();
config.Region = "cn-hangzhou";
config.CredentialsProvider = new OSS.Credentials.StaticCredentialsProvider(
    stsAccessKeyId,
    stsAccessKeySecret,
    stsSecurityToken);

using var client = new OSS.Client(config);
var result = await client.PutObjectAsync(new OSS.Models.PutObjectRequest
{
    Bucket = bucketName,
    Key = objectKey,
    Body = contentStream
});
```

`stsAccessKeyId`、`stsAccessKeySecret` 和 `stsSecurityToken` 必须由可信服务端短期签发。生命周期较短、操作简单的上传下载优先使用预签名 URL，客户端不需要持有任何 OSS 凭据。

## 上游资料

- [中文 README](../../Core/alibabacloud-oss-csharp-sdk-v2~/README-CN.md)
- [英文 README](../../Core/alibabacloud-oss-csharp-sdk-v2~/README.md)
- [上游 CHANGELOG](../../Core/alibabacloud-oss-csharp-sdk-v2~/CHANGELOG.md)
- [上游 LICENSE](../../Core/alibabacloud-oss-csharp-sdk-v2~/LICENSE)
