# Changelog

本文件记录 `com.solotopia.nova.framework.wechat.minigame` 的版本变更。

## [Unreleased]

## [0.0.1] - 2026-09-20

### Added

- 新增微信小游戏转换 SDK 的 Nova 接入包。

### Fixed

- 恢复 `Core/` 中官方 SDK 原始 `.meta` GUID，避免 `MiniGameConfig.asset` 等原厂序列化引用失效；包根模板镜像继续使用独立 GUID。
- 普通浏览器 WebGL 默认不再链接微信可选 Emscripten GLX 原生库；微信导出仍可按原配置临时启用，导出结束后自动恢复隔离。

### Changed

- 将微信官方 SDK `0.1.34` 完整内置到 `Core/`，不再要求消费工程安装外部 Git 依赖。
- 统一通过 Nova 包根路径读取模板和插件，适配本地副本目录。
- 在包根提供与 `Core/WebGLTemplates/` 一致的模板镜像，确保标准 Unity 能发现微信 WebGL 模板。
- 普通浏览器 WebGL 不再执行微信小游戏专用的启动页隐藏与键盘设置逻辑。
