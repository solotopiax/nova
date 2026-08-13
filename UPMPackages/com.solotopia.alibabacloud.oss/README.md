# Alibaba Cloud OSS Editor Tool for Unity

`com.solotopia.alibabacloud.oss` 是 Nova CDN 内容部署使用的 Alibaba Cloud OSS C# SDK v2 Editor 工具包。

它不是 Nova Framework 的 Runtime 依赖，也不提供 Player 上传／下载能力。SDK DLL 与桥接程序集均位于 `Nova/Editor/`，不会进入 Player 构建。

## 使用方式

- 仅在需要阿里云 OSS 的资源部署或白名单部署时安装本包。
- 未安装时，ConfigWindow 的 CDN 面板会显示安装引导，并只禁用两项 OSS 部署操作；Cloudflare 缓存清理不受影响。
- Pipify 的 `cdn.deploy` 与 `cdn.whitelist.deploy` 在缺包时会返回明确的安装错误，不会静默跳过。
- Framework 不会自行写入消费者工程的 `manifest.json`；请通过项目已配置的 UPM 来源安装本包。

## 上游版本

- 仓库：<https://github.com/aliyun/alibabacloud-oss-csharp-sdk-v2>
- 提交：`892c0209b9808b352f9e0814e7da32c49496ea16`
- 上游协议：Apache-2.0

GitHub 提交是本包的版本真相源。完整、未修改的 tracked 源码位于 `Core/alibabacloud-oss-csharp-sdk-v2~`，Unity 使用的 DLL 由该快照构建。`Core/alibabacloud-oss-csharp-sdk-v2-892c0209-source.tar.gz` 是同一提交的可校验 Git archive，确保会过滤 `.gitignore` 的打包器仍能携带完整快照。

## 运行环境

支持 Nova 基线 Unity 6000.4 的 Editor。此包不支持、也不需要 Player / IL2CPP / WebGL 运行时接入。

## 安全要求

不要把长期 AccessKey 写入会随 Player 发布的客户端包、配置或代码。CDN 编辑器配置仅应使用受限权限的部署凭据，并按项目安全规范保管。

接入示例与限制见 [Nova/Docs/INDEX.md](Nova/Docs/INDEX.md)。上游原始说明见 [Core/alibabacloud-oss-csharp-sdk-v2~/README-CN.md](Core/alibabacloud-oss-csharp-sdk-v2~/README-CN.md)。
