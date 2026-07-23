# Alibaba Cloud OSS C# SDK v2 for Unity

`com.solotopia.alibabacloud.oss` 将阿里云 OSS C# SDK v2 封装为 Nova 可依赖的 Unity UPM Runtime 包。

## 上游版本

- 仓库：<https://github.com/aliyun/alibabacloud-oss-csharp-sdk-v2>
- 提交：`892c0209b9808b352f9e0814e7da32c49496ea16`
- 上游协议：Apache-2.0

GitHub 提交是本包的版本真相源。完整、未修改的 tracked 源码位于 `Core/alibabacloud-oss-csharp-sdk-v2~`，Unity 使用的 DLL 由该快照构建。`Core/alibabacloud-oss-csharp-sdk-v2-892c0209-source.tar.gz` 是同一提交的可校验 Git archive，确保会过滤 `.gitignore` 的打包器仍能携带完整快照。

## 运行环境

支持 Nova 基线 Unity 6000.4 / .NET Standard 2.1 的 Editor 和 Standalone Runtime。Android/iOS 在 API 与程序集层兼容，但本次环境未安装对应 Unity Player Support，仍需在目标工程执行 IL2CPP 构建和真机网络验证。当前不承诺 WebGL：上游默认传输层使用 `System.Net.Http`，没有 UnityWebRequest/WebGL 专用实现。

## 安全要求

禁止在客户端包、配置或代码中保存长期 AccessKey。移动端和桌面客户端应使用服务端签发的 STS 临时凭据，或使用服务端生成的预签名 URL。

接入示例与限制见 [Nova/Docs/INDEX.md](Nova/Docs/INDEX.md)。上游原始说明见 [Core/alibabacloud-oss-csharp-sdk-v2~/README-CN.md](Core/alibabacloud-oss-csharp-sdk-v2~/README-CN.md)。
