# Changelog

本文件记录 `com.solotopia.nova.framework.sdk.aihelp` 的版本变更。
格式遵循 [Keep a Changelog](https://keepachangelog.com/)，版本号遵循语义化版本。

## [Unreleased]

## [0.0.11] - 2026-08-27

### Changed

- AIHelpDemo 在 Network 初始化前完成 Localization 加载与当前语言初始化。

## [0.0.10] - 2026-08-27

### Fixed

- 修复 AIHelpDemo 覆盖导入或包升级后路径重写遗漏的问题。

## [0.0.9] - 2026-08-20

### Changed

- AIHelpDemo 移除重复场景监听，改由 Framework `0.6.16` 的统一 `SceneRoute` 接管；最低 Framework 依赖同步提升至 `0.6.16`。

## [0.0.8] - 2026-08-18

### Changed

- 将 Framework 最低依赖提升至 `0.6.15`，使独立安装解析到本轮统一契约。

## [0.0.7] - 2026-08-13

### Changed

- 将 Framework 最低依赖提升至 `0.6.10`，使独立安装解析到本轮统一契约。

## [0.0.6] - 2026-08-12

### Added

- 新增 `Nova/Open SDK URL/AIHelp Console` 与 `AIHelp Readme`，可直接打开 AIHelp 官方后台和 Unity 接入文档。

## [0.0.5] - 2026-08-06

### Changed

- `coreVersion` 从 `1.0.0` 同步为 AIHelp Unity SDK `6.0.+`。

## [0.0.4] - 2026-08-03

### Changed

- AIHelpDemo 同步 Localization 支持语言表与 JSON / Binary 数据格式能力，并将 Framework 最低依赖提升至 `0.6.3`。

## [0.0.3] - 2026-07-29

### Changed

- 将 Nova Framework 最低依赖版本提升至 `0.6.0`。
- AIHelpDemo 同步启动应用配置网络命令、运行时配置与场景覆盖。

## [0.0.2] - 2026-07-13

### Changed

- 将 Nova Framework 的最低依赖版本提升至 `0.5.38`，使独立安装链通过 Framework 获取 `unitask@10.0.6`。

## [0.0.1] - 2026-07-09

### Added

- 首次封装 AIHelp Unity SDK 6.0 为 Nova SDK 插件包：`AIHelpPlugin` 继承 `SDKPluginBase`，由 `SDKManager` 统一编排初始化，提供智能客服会话、帮助中心 FAQ、用户信息同步、未读消息 / 工单数查询、语言切换、推送 token 设置等能力；登录事件自动同步用户身份。
- 随包 bundle AIHelp Unity SDK 6.0 官方原样代码：managed C#（`Core/AIHelp/`）与 iOS 原生库（`Core/Plugins/iOS/AIHelpSDK/`），业务无需额外安装原厂包。
- `AIHelpBuildProcessor`：Android 端构建期把 maven 依赖幂等注入导出的 `build.gradle`（含 androidx/jetifier 标志合并）；iOS 端构建期给 UnityFramework 与主 target 追加 `-ObjC` 链接标志，确保 AIHelp framework 的 Objective-C category 符号被正确链接。
- `AIHelpDemo` Sample：`DemoAIHelpView` 演示 View，覆盖 `Show` / `Login` / `FetchUnreadMessageCount` / `FetchUnreadTaskCount` / `Close` 等接口调用与事件回显。
- 落地包骨架：`package.json` / 文档三件套 / `THIRD_PARTY_NOTICES.md` / `nova-samples.json` / `Nova/Doc/` 三篇文档。

### Changed

- 将 Nova Framework 的最低依赖版本提升至 `0.5.37`，避免安装时解析到仍使用旧契约的框架版本。
