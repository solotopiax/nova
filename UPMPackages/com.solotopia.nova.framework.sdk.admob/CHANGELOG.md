# Changelog

本文件记录该 UPM 包各版本的变更内容，遵循 [Keep a Changelog](https://keepachangelog.com/) 格式。

---

## [Unreleased]

### Changed

- 全屏配置改为 PlacementGroups，兼容旧 Unity 字段和真实 ID 访问器，补齐广告基础包依赖；文档按现有初始化、四格式广告和构建处理器说明官方 SDK 条件、组拓扑与设备验收。

- 最低依赖对齐至Nova Framework 0.7.0、Ad 1.2.0，与本轮发布的框架及 SDK 接口配套。

- 对齐依赖版本：com.solotopia.nova.framework 0.7.0 → 0.7.3、com.solotopia.nova.framework.sdk.ad 1.2.0 → 1.2.1。

- 兼容声明调整为 Unity 6000.4.2f1，配合 Nova Framework 的 Unity 6000.4.x 支持范围。

- 同步 AdMob README、文档索引与插件说明，明确收益回调复用 AdChannelPluginBase 的 IAA 上报链路及 AdRevenueReport 配置。

- 对齐基础广告包 RevenueReport 命名、配置与收益上报诊断文档，服务端协议保持不变。

- 收益协议在 Paid 的显式收益入口发送；Banner 使用共享渠道累计入口及唯一存档（默认 5 次），非 Banner 按次上报，原始收益通知不重复发送协议。当前尚未接入独立分析 ad_ilrd。

### Fixed

- 移除已被实际实现取代的“仅骨架、无构建处理器”旧说明；明确 UMP 年龄配置、展开 ID 独立并发重试、统一组间隔和释放后不可复用，MAX 构建处理器不读取独立 AdMob 配置。

- Banner 就绪和收益查询统一使用实际控制的首个配置 ID；对象未创建、已销毁或显示调用异常时仅记录警告并反馈失败，避免虚假显示成功。

### Breaking

- 最低支持 Unity 6000.6.7f1，与 Nova Framework 基线一致。

## [0.0.5] - 2026-06-15

### Changed
- 补发上次 release 后的包内变更，并对齐本轮 UPM 发布版本。

## [0.0.4] - 2026-06-10

### Changed
- 批量执行签名 UPM 重新发布，刷新包版本并对齐内网仓库分发批次。

---

## [0.0.3] - 2026-06-09

### Changed
- 刷新 AdMob 子包文档索引与接入说明，统一配置字段描述与文档结构。

## [0.0.2] - 2026-05-21

### Changed
- 包内结构调整与冗余资源优化。

---

## [0.0.1] - 2026-05-21

### Added
- 接入 Nova Framework UPM 包结构，补齐 `CHANGELOG.md` / `LICENSE.md` / `README.md` 三件套，纳入发版强制校验。
