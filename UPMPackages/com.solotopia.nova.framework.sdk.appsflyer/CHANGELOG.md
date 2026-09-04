# Changelog

## [Unreleased]

## [0.1.11] - 2026-09-04

### Changed

- AppsflyerDemo 适配 Framework `0.6.26` 的 `IUIManager.OnOpenUIViewFail` 事件；最低 Framework 依赖同步提升至 `0.6.26`。

## [0.1.10] - 2026-09-02

### Changed

- 为 AppsFlyer 插件补充静态配置类型元数据，并保持配置 DTO 可跨平台序列化。

## [0.1.9] - 2026-08-28

### Changed

- AppsflyerDemo 移除 Launcher Prefab 上重复的 `TextLocalizing` 组件，统一由 Framework Launcher UI 文本渲染链处理。

## [0.1.8] - 2026-08-27

### Changed

- AppsflyerDemo 在 Network 初始化前完成 Localization 加载与当前语言初始化。

## [0.1.7] - 2026-08-27

### Fixed

- 修复 AppsflyerDemo 覆盖导入或包升级后路径重写遗漏的问题。

## [0.1.6] - 2026-08-20

### Changed

- AppsflyerDemo 移除重复场景监听，改由 Framework `0.6.16` 的统一 `SceneRoute` 接管；最低 Framework 依赖同步提升至 `0.6.16`。

## [0.1.5] - 2026-08-18

### Changed

- 将 Framework 与 GameLogin 最低依赖同步至 `0.6.15`、`0.1.8`。

## [0.1.4] - 2026-08-13

### Changed

- 将内置 AppsFlyer Unity Plugin 升级至 `6.18.1`，并同步 Android / iOS 依赖声明。
- 将 Framework 与 GameLogin 最低依赖同步至本轮发布版本。


## [0.1.3] - 2026-08-12

### Added

- 新增 AppsFlyer Console / Readme 官方快捷入口。

### Fixed

- 配置校验按目标平台执行：Android 仅校验 DevKey，iOS 仅校验 AppId，避免无关字段导致初始化被跳过。


## [0.1.2] - 2026-08-03

### Changed

- AppsflyerDemo 同步 Localization 支持语言表与 JSON / Binary 数据格式能力，并将 Framework 最低依赖提升至 `0.6.3`。

## [0.1.1] - 2026-07-31

### Changed

- 将 `AppsFlyerPlugin` 初始化优先级调整为 `20`，确保在 TGA 标识数据就绪后初始化。

## [0.1.0] - 2026-07-29

### Changed

- 将 Framework 与 GameLogin 最低依赖分别提升至 `0.6.0` 与 `0.1.0`。
- AppsFlyerDemo 同步启动应用配置网络命令、运行时配置与场景覆盖。

## [0.0.27] - 2026-07-23

### Changed

- App ID 配置说明明确为 App Store Connect 中的 Apple ID，避免与其他平台应用标识混淆。

## [0.0.26] - 2026-07-16

### Fixed

- Android 构建前确保 `gradleTemplate.properties` 包含 `android.uniquePackageNames=false`，避免 AppsFlyer 依赖在 AGP 8 构建链中触发重复包名校验错误。

## [0.0.25] - 2026-07-13

### Changed

- 提升 Framework 与 GameLogin 的依赖下界，保证独立安装链最终解析到 `unitask@10.0.6`。

## [0.0.24] - 2026-07-13

### Changed

- 将 Nova Framework 的最低依赖版本提升至 `0.5.37`，避免安装时解析到仍使用旧契约的框架版本。

## [0.0.23] - 2026-07-08

### Fixed

- 清理 sample asmdef 克隆残留引用：删除代码未使用的 gamesave / tga / ad 跨包程序集引用（从 MainDemo 模板克隆时带入，AppsflyerDemo 实际未调用），避免消费工程未安装这些无关包时编译报 CS0234。
- 补全 sample 依赖声明：package.json dependencies 增加 com.solotopia.nova.framework.kit.network.gamelogin（AppsflyerDemo 的登录/绑定/存档演示实际使用），修复消费工程 import sample 后缺对应程序集的编译失败。
- 移除断链程序集引用（GUID 09050f5d… 在项目中已无归属，Unity 报 Missing Reference）。

## [0.0.22] - 2026-07-08

### Changed

- 例行发布；物料配置对齐（日常主仓保留真值，发版时自动脱敏为占位后进 npm/github 公开侧）。

## [0.0.21] - 2026-06-30

### Changed

- 依赖对齐：`com.solotopia.nova.framework`→`0.5.32`，修复公网 registry 仅有 0.5.32 而旧声明 0.5.31 缺失导致安装 404 的问题。

## [0.0.20] - 2026-06-19

### Changed

- 依赖对齐：`com.solotopia.nova.framework`→`0.5.31`。

## [0.0.19] - 2026-06-18

### Changed

- 例行版本升级。

本文件记录该 UPM 包各版本的变更内容，遵循 [Keep a Changelog](https://keepachangelog.com/) 格式。

---

## [0.0.18] - 2026-06-15

### Changed
- 补发上次 release 后的包内变更，并对齐本轮 UPM 发布版本。

## [0.0.17] - 2026-06-10

### Changed
- 批量执行签名 UPM 重新发布，刷新包版本并对齐内网仓库分发批次。

---

## [0.0.16] - 2026-06-09

### Changed
- 优化 `AppsFlyerPluginConfig` 的 Inspector 提示文案，并同步刷新包内文档说明，明确关键协议与参数字段的用途。

## [0.0.15] - 2026-06-04

### Fixed
- 修复发布产物中 SamplePathManifest 未填充重写目标的问题：发布描述符 `nova-samples.json` 的 `sampleManifestRelative` 误指向 `Configs/`（实际在 `Editor/`），导致外部工程 import 后场景 / Prefab 内资产路径仍为开发工程目录 `Assets/Samples/<Demo>/...` 而未替换为真实 import 路径。

---

## [0.0.14] - 2026-06-04

### Added
- 新增 AppsFlyer 第三方数据上报 cmd 与登录、注册接口对接。
- 新增 AppsFlyer 示例工程。

### Changed
- 登录成功后新增通知回调；更新 cmd 描述。

---

## [0.0.13] - 2026-05-22

### Changed
- `AppsFlyerPlugin` / `AppsFlyerPlugin.Methods` 接口与内部实现同步刷新。

---

## [0.0.12] - 2026-05-21

### Changed
- 包内结构调整与冗余资源优化。

---

## [0.0.11] - 2026-05-21

### Added
- 补齐 `CHANGELOG.md` / `LICENSE.md` / `README.md` 三件套，纳入发版强制校验。

### Changed
- 跟随主框架 0.5.0 升版，对齐 EDM 依赖版本。
