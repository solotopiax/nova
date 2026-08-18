# Changelog

## [Unreleased]

## [0.1.7] - 2026-08-18

### Changed

- 将 Framework 与 GameLogin 最低依赖同步至 `0.6.15`、`0.1.8`；TGADemo 同步序列化新增配置字段的默认值。

## [0.1.6] - 2026-08-13

### Breaking

- 移除 TGA 上报请求的明文调试开关；埋点通信固定使用标准 Protobuf 与 AES 加密链路。

### Changed

- 将 Framework 与 GameLogin 最低依赖同步至本轮发布版本。

## [0.1.5] - 2026-08-12

### Added

- 通过包内 Editor assembly 新增 TGA Console / Readme 官方快捷入口。

## [0.1.4] - 2026-08-03

### Breaking

- 第三方登录提供方用户属性由 `nova_third_platform` 更名为 `nova_third_login_provider`，并与 Framework 的 `SDKDataKeys.ThirdLoginProvider` 数据槽语义对齐。

### Changed

- Framework 与 GameLogin 最低依赖分别提升至 `0.6.4` 与 `0.1.3`。

## [0.1.3] - 2026-08-03

### Changed

- TGADemo 同步 Localization 支持语言表与 JSON / Binary 数据格式能力，并将 Framework 最低依赖提升至 `0.6.3`。

## [0.1.2] - 2026-07-31

### Fixed

- AppsFlyer 与第三方登录数据桥接共享一次 SDK 初始化等待，避免并发等待同一初始化任务触发 `Already continuation registered`。

## [0.1.1] - 2026-07-31

### Changed

- 将 `TGAPlugin` 初始化优先级调整为 `10`，作为当前 SDK 依赖链的首个初始化分桶。
- `TGAPluginConfig.Mode` 与构造参数改用 `TDMode`，新增时区配置与可选的 DeviceId-to-DistinctId 初始化行为。

## [0.1.0] - 2026-07-29

### Changed

- 将 Framework 与 GameLogin 最低依赖分别提升至 `0.6.0` 与 `0.1.0`。
- TGADemo 同步启动应用配置网络命令、运行时配置与场景覆盖。

## [0.0.22] - 2026-07-21

### Added

- 自动消费账号插件发布的 `OpenId` / `ThirdLoginProvider` 数据，通过 TGA `UserSet` 更新 `nova_openid` 与第三方登录提供方属性。
- 自动同步 AppsFlyer ID 与 Facebook ID 到 TGA `UserSetOnce` 用户属性。

### Fixed

- `nova_first_version` 改为持久化的首次安装版本，避免升级后被当前版本覆盖。
- 移除冗余的 `nova_uid` 自定义用户属性，继续使用 TGA 预置账号 ID。

### Changed

- 将 Nova Framework 最低依赖版本提升至 `0.5.42`。

## [0.0.21] - 2026-07-13

### Changed

- 提升 Framework 与 GameLogin 的依赖下界，保证独立安装链最终解析到 `unitask@10.0.6`。

## [0.0.20] - 2026-07-13

### Changed

- 将 Nova Framework 的最低依赖版本提升至 `0.5.37`，避免安装时解析到仍使用旧契约的框架版本。

## [0.0.19] - 2026-07-08

### Fixed

- 清理 sample asmdef 克隆残留引用：删除代码未使用的 gamesave / appsflyer / ad 跨包程序集引用（从 MainDemo 模板克隆时带入，TGADemo 实际未调用），避免消费工程未安装这些无关包时编译报 CS0234。
- 补全 sample 依赖声明：package.json dependencies 增加 com.solotopia.nova.framework.kit.network.gamelogin（TGADemo 的登录/绑定/存档演示实际使用），修复消费工程 import sample 后缺对应程序集的编译失败。
- 移除断链程序集引用（GUID 09050f5d… 在项目中已无归属，Unity 报 Missing Reference）。
- 补全包级依赖：package.json 此前无 dependencies 字段，新增 com.solotopia.nova.framework（本包 Runtime 与 sample 均依赖框架），修复独立安装时缺框架程序集。

## [0.0.18] - 2026-07-08

### Changed

- TGA 协议 / FPS 相关调整；物料配置对齐（日常主仓保留真值，发版时自动脱敏）。

## [0.0.17] - 2026-06-18

### Changed

- 例行版本升级。

本文件记录该 UPM 包各版本的变更内容，遵循 [Keep a Changelog](https://keepachangelog.com/) 格式。

---

## [0.0.16] - 2026-06-15

### Changed
- 补发上次 release 后的包内变更，并对齐本轮 UPM 发布版本。

## [0.0.15] - 2026-06-10

### Changed
- 批量执行签名 UPM 重新发布，刷新包版本并对齐内网仓库分发批次。

---

## [0.0.14] - 2026-06-09

### Changed
- 优化 `TGAPluginConfig` 的 Inspector 提示文案，并同步刷新包内文档说明，明确关键协议与参数字段的用途。

## [0.0.13] - 2026-06-04

### Fixed
- 修复发布产物中 SamplePathManifest 未填充重写目标的问题：发布描述符 `nova-samples.json` 的 `sampleManifestRelative` 误指向 `Configs/`（实际在 `Editor/`），导致外部工程 import 后场景 / Prefab 内资产路径仍为开发工程目录 `Assets/Samples/<Demo>/...` 而未替换为真实 import 路径。

---

## [0.0.12] - 2026-06-03

### Changed
- TGA 上报地址改为填网络指令名（ServerCmdName），由 Network 模块在运行时解析出真实 URL；旧 ServerUrl 字段已移除，需在 Config 面板重填指令名并配套设置 NetworkCmds 表。

---

## [0.0.11] - 2026-05-26

### Added
- `TGAPlugin` 实现 `IDeviceIdProvider` 接口，新增显式接口方法 `string IDeviceIdProvider.GetDeviceID()`，复用 `TDAnalytics.GetDeviceId()`，返回值 null 安全兜底为空串。
- 保留原有 `public string GetDeviceId()`（驼峰小 d），业务侧既有调用方不受影响。

---

## [0.0.10] - 2026-05-22

### Changed
- `TGAPlugin` / `TGAPlugin.Methods` / `TGAPlugin.UserProperty` 接口与内部实现同步刷新。

---

## [0.0.9] - 2026-05-21

### Changed
- 包内结构调整与冗余资源优化。

---

## [0.0.8] - 2026-05-21

### Added
- 补齐 `CHANGELOG.md` / `LICENSE.md` / `README.md` 三件套，纳入发版强制校验。

### Changed
- 跟随主框架 0.5.0 升版。
