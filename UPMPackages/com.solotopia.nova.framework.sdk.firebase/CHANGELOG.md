# Changelog

## [Unreleased]

## [0.1.8] - 2026-08-27

### Fixed

- 修复 FirebaseDemo 覆盖导入或包升级后路径重写遗漏的问题。

## [0.1.7] - 2026-08-26

### Changed

- 默认 Topic 和显式 Topic 订阅在调用 Firebase 前等待 FCM Token 就绪，避免 iOS 首次安装时 APNs / FCM Token 尚未准备好就发起 Topic 操作。
- `AutoRequestNotificationPermission` 继续保持默认开启，并补充 Native 模块自身不自动弹窗与 Firebase 插件配置自动请求之间的文档边界。
- README 版本号对齐 `package.json` 的 `0.1.7`，并补充 FirebaseDemo 的 GameLogin 示例依赖说明。

### Fixed

- 修复 `FirebaseEditorDestroyFix` 未在域重载后自动注册，导致退出 Play Mode 清理钩子可能不生效的问题。
- 修正 Firebase 推送点击埋点测试仍断言旧字段名的问题。

## [0.1.6] - 2026-08-20

### Changed

- FirebaseDemo 移除重复场景监听，改由 Framework `0.6.16` 的统一 `SceneRoute` 接管；最低 Framework 依赖同步提升至 `0.6.16`。

## [0.1.5] - 2026-08-18

### Added

- 新增 `IFirebasePushTaskPlugin.QueuePushTaskAsync(...)` 业务 push task 接口；任务会先写入 `IFileFragmentManager` 本地缓存，按 `PushFlushIntervalSeconds`（默认 100 秒）或 `PushFlushBatchSize`（默认 5 条）批量发送，发送成功后按 `task_key + int CacheVersion` 精确删除，避免发送过程中同 key 覆盖的新任务被误删。
- `FirebasePlugin` 在 Firebase 真实初始化完成后自动同步 `top_all`、语言、平台、时区和国家默认 FCM topic；国家码通过 AD 模块统一获取，同步状态会持久化，并在变化时先退订旧 topic 再订阅新 topic。

### Changed

- 将随包分发的 Firebase Unity SDK 官方载荷从 `12.10.1` 升级到 `13.14.0`，同步更新 Analytics、App、Crashlytics、Messaging 与 Remote Config 的 Editor 依赖描述、桌面库、iOS/tvOS 原生库和 Android m2repository 产物。
- Firebase 登录后上报 `PbNetReportFirebaseReq` 时同步携带国家码与 `+08:00` 这类服务端可读时区偏移；默认时区 topic 仍保持 `utc_plus_08` 等 Firebase 安全格式。
- Firebase 默认国家 topic 和登录上报改为通过 AD 模块获取国家码；Firebase 不再配置国家码等待超时、不再直接等待广告数据槽位，也不再使用系统区域作为国家码兜底。
- Firebase push task 在应用从后台恢复前台时会主动请求发送当前本地缓存；实际发送仍受 Firebase 初始化完成和 `SetUserId` 就绪门槛保护。
- Firebase push task 取消任务时协议层只发送 `task_key` 与 `cancel`，不再携带 `trigger_time` 或 `template_id`。
- 收口 FCM topic 订阅公开入口，移除 `SubscribeAsync` / `UnsubscribeAsync` 包装方法，统一通过 `SetTopicSubscribed(topic, subscribed)` 订阅或退订。
- 默认语言 topic 等待 `LocalizationRefreshEventData` 提供真实当前语言后再同步；`Nova.Localization.Language` 仍为 `Unspecified` 时仅同步全量、平台和时区 topic，并保留旧语言 topic 直到新语言就绪后再按差异退订/订阅。
- 将 Framework 与 GameLogin 最低依赖同步至 `0.6.15`、`0.1.8`，并将新增 Firebase push 默认值投影到 Sample ConfigMaster。

## [0.1.4] - 2026-08-13

### Changed

- 将随包分发的 Firebase Unity SDK 官方载荷从 `12.10.1` 升级到 `13.14.0`，同步更新 Analytics、App、Crashlytics、Messaging 与 Remote Config 的各平台产物。
- 将 Framework 与 GameLogin 最低依赖同步至本轮发布版本。


## [0.1.3] - 2026-08-12

### Added

- 新增 Firebase Console / Readme 官方快捷入口。


## [0.1.2] - 2026-08-03

### Changed

- FirebaseDemo 同步 Localization 支持语言表与 JSON / Binary 数据格式能力，并将 Framework 最低依赖提升至 `0.6.3`。

## [0.1.1] - 2026-07-31

### Changed

- 将 `FirebasePlugin` 初始化优先级调整为 `30`。

## [0.1.0] - 2026-07-29

### Changed

- 将 Framework 与 GameLogin 最低依赖分别提升至 `0.6.0` 与 `0.1.0`。
- FirebaseDemo 同步启动应用配置网络命令、运行时配置与场景覆盖。

## [0.0.24] - 2026-07-16

### Fixed

- 将 Firebase Android Library 的 Manifest package 与 Gradle namespace 改为 Nova 独立命名空间，避免 AGP 8 下与原厂 Firebase 资源库发生命名冲突。

## [0.0.23] - 2026-07-13

### Changed

- 提升 Framework 与 GameLogin 的依赖下界，保证独立安装链最终解析到 `unitask@10.0.6`。

## [0.0.22] - 2026-07-13

### Changed

- 将 Nova Framework 的最低依赖版本提升至 `0.5.37`，避免安装时解析到仍使用旧契约的框架版本。

## [0.0.21] - 2026-07-08

### Fixed

- 清理 sample asmdef 克隆残留引用：删除代码未使用的 gamesave / tga / appsflyer / ad 跨包程序集引用（从 MainDemo 模板克隆时带入，FirebaseDemo 实际未调用），避免消费工程未安装这些无关包时编译报 CS0234。
- 补全 sample 依赖声明：package.json dependencies 增加 com.solotopia.nova.framework.kit.network.gamelogin（FirebaseDemo 的登录/绑定/存档演示实际使用），修复消费工程 import sample 后缺对应程序集的编译失败。
- 移除断链程序集引用（GUID 09050f5d… 在项目中已无归属，Unity 报 Missing Reference）。

## [0.0.20] - 2026-07-08

### Changed

- 例行发布；物料配置对齐（日常主仓保留真值，发版时自动脱敏为占位后进 npm/github 公开侧）。

## [0.0.19] - 2026-06-29

### Changed

- Firebase 桌面核心原生库（`Firebase/Plugins/x86_64/FirebaseCppApp-*.bundle/.so/.dll`）改由 **Git LFS** 承载、随开源仓分发：正常 `git clone` 自动 smudge 还原，clone 即用，不再需要手动从官方 SDK 补齐。
- `FirebaseDesktopLibraryGuard` 由「缺库即引导下载」改为「LFS 兜底」：仅在用户未安装 Git LFS 客户端（拿到指针文件）或手动删除该库时提示，引导首选 `git lfs pull`，次选官方 SDK 补齐。
- 同步刷新 README 桌面库章节（中英双语）与本包 `.gitignore`/`.gitattributes` 注释，移除「未随开源仓分发 / 超 GitHub 100MB 限制」等已不成立的措辞。

## [0.0.18] - 2026-06-18
- 依赖对齐：`com.solotopia.nova.framework`→`0.5.32`，修复公网 registry 仅有 0.5.32 而旧声明 0.5.31 缺失导致安装 404 的问题。

### Changed

- Firebase 桌面调试原生库（`Firebase/Plugins/x86_64/FirebaseCppApp-*.bundle/.so/.dll`，单文件超 100MB、仅 Editor 桌面播放态使用）不再随**开源仓（git/GitHub）**分发，以符合公开仓库单文件体积限制；**UPM 包（npm tarball）仍正常包含桌面库**，真机 Android/iOS 构建不依赖该库。
- 依赖对齐：`com.solotopia.nova.framework` 依赖下界提升至 `0.5.31`。

### Added

- 新增 `FirebaseDesktopLibraryGuard` 编辑器检查：当从 git 拉取源码、当前平台缺失对应桌面库时，在 Console 与弹窗中引导从 Firebase 官方页面（https://firebase.google.com/download/unity）下载补全；真机构建不依赖该桌面库。

## [0.0.17] - 2026-06-18

### Changed

- 例行版本升级。

本文件记录该 UPM 包各版本的变更内容，遵循 [Keep a Changelog](https://keepachangelog.com/) 格式。

---

## [0.0.16] - 2026-06-15

### Changed
- 补发上次 release 后的 Firebase SDK 包内变更，并对齐本轮 UPM 发布版本。

## [0.0.15] - 2026-06-10

### Changed
- 批量执行签名 UPM 重新发布，刷新包版本并对齐内网仓库分发批次。

---

## [0.0.14] - 2026-06-09

### Changed
- 优化 `FirebasePluginConfig` 的 Inspector 提示文案，并同步刷新包内文档说明，明确上报协议名的配置语义。

## [0.0.13] - 2026-06-04

### Fixed
- 修复发布产物中 SamplePathManifest 未填充重写目标的问题：发布描述符 `nova-samples.json` 的 `sampleManifestRelative` 误指向 `Configs/`（实际在 `Editor/`），导致外部工程 import 后场景 / Prefab 内资产路径仍为开发工程目录 `Assets/Samples/<Demo>/...` 而未替换为真实 import 路径。

---

## [0.0.12] - 2026-06-04

### Added
- 新增 Firebase 第三方数据上报 cmd 与登录、注册接口对接。
- 新增 Firebase 示例工程。

### Changed
- 将原分散的 analytics / crashlytics / messaging / remote-config 等子模块统一归并至 sdk.firebase 主包。

### Fixed
- 修复 SDK 配置面板若干 bug。

---

## [0.0.11] - 2026-05-22

### Changed
- `FirebasePlugin` / `FirebasePlugin.Methods` 接口与内部实现同步刷新。

---

## [0.0.10] - 2026-05-21

### Changed
- 包内结构调整与冗余资源优化。

---

## [0.0.9] - 2026-05-21

### Added
- 补齐 `CHANGELOG.md` / `LICENSE.md` / `README.md` 三件套，纳入发版强制校验。

### Changed
- 跟随主框架 0.5.0 升版，对齐 Firebase 子包依赖。
