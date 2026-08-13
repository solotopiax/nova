# Alibaba Cloud OSS Editor Tool 文档索引

## 定位

本包是供 Nova Editor CDN 内容部署使用的阿里云 OSS C# SDK v2 工具包，不属于 Nova `SDKManager` 插件，也不是 Runtime SDK。SDK DLL 与桥接程序集位于 `Nova/Editor/`，不会参与 Player 构建。

## 使用与降级

- Nova Framework 不在 `package.json` 中强制依赖本包；只有需要“资源部署”或“白名单部署”时才安装。
- `NovaFramework.Editor` 通过 `versionDefines` 在包存在时编译 OSS 强类型调用；包缺失时 Framework 和 ConfigWindow 仍可正常编译、打开。
- ConfigWindow 会显示 Warning HelpBox 和“打开 UPM 安装 OSS”按钮，并仅禁用两项 OSS 部署操作；Cloudflare 缓存清理保持可用。
- Pipify 的 `cdn.deploy` 与 `cdn.whitelist.deploy` 在缺包时抛出明确的安装错误，避免无声跳过部署。

## 凭据

不要把长期 AccessKey ID / Secret 写入会随 Player 发布的客户端包、配置或代码。CDN 编辑器配置仅应使用权限受限的部署凭据，并遵循项目的凭据保管规范。

## 上游资料

- [中文 README](../../Core/alibabacloud-oss-csharp-sdk-v2~/README-CN.md)
- [英文 README](../../Core/alibabacloud-oss-csharp-sdk-v2~/README.md)
- [上游 CHANGELOG](../../Core/alibabacloud-oss-csharp-sdk-v2~/CHANGELOG.md)
- [上游 LICENSE](../../Core/alibabacloud-oss-csharp-sdk-v2~/LICENSE)
