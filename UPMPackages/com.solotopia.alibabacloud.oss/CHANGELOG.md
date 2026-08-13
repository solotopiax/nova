# Changelog

## [Unreleased]

## [0.0.2] - 2026-08-13

### Breaking

- 本包不再提供 Player Runtime 程序集或 DLL；原 `Solotopia.AlibabaCloud.OSS.Runtime` 不再可供运行时代码引用。

### Changed

- 将 OSS SDK 收拢为 Editor-only 工具，Player 构建不再包含其桥接程序集或 DLL。
- Nova CDN 面板在未安装本包时给出安装引导，仅禁用两项 OSS 部署操作。

## [0.0.1] - 2026-07-17

- 封装上游 GitHub 提交 `892c0209b9808b352f9e0814e7da32c49496ea16`。
- 附带完整 Git source archive 与 SHA-256 校验文件，规避打包器过滤 VCS 元数据。
- 提供 Unity 6000.4 / .NET Standard 2.1 Runtime 程序集。
- 增加 IL2CPP XML 序列化保留配置和可复现构建工程。
