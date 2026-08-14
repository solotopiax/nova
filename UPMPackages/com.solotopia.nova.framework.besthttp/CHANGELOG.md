# Changelog

## [Unreleased]

## [0.1.6] - 2026-08-14

### Added

- 接入 Framework `0.6.12` 的业务指定连接 IP 能力，保留原始域名 Host/SNI，并在 BestHTTP 不具备 `SetIPAddress` 能力时自动回退系统 DNS。
- HTTP 响应补充服务器到达状态，遥测注册改为使用稳定事件名和属性字典，降低对 BestHTTP 专属遥测类型的耦合。

## [0.1.5] - 2026-08-13

### Changed

- Best HTTP 最低依赖与 `coreVersion` 升级至 `3.0.19`；Best TLS Security 保持 `3.0.5`。
- Framework 最低依赖提升至 `0.6.11`，对齐 AES 默认凭据与 Persist 加载顺序契约。

## [0.1.4] - 2026-08-13

### Changed

- 将 Framework 最低依赖提升至 `0.6.10`，保证网络后端与本轮加密契约一致。

## [0.1.3] - 2026-08-06

### Fixed

- 将遥测 sink 注册与 UniTask 就绪监听拆分到 `AfterAssembliesLoaded` / `BeforeSceneLoad` 两阶段，修复 iOS IL2CPP 启动时 PlayerLoop 尚未注入导致的空引用异常。
- 下载请求改为在 BestHTTP 完成回调中复制响应内容，避免响应释放后读取到空数据。

### Changed

- `coreVersion` 同步为 Best HTTP `3.0.18` 与 Best TLS Security `3.0.5`。

## [0.1.2] - 2026-08-04

### Changed

- 扩充包内 BestHTTP 网络遥测文档，补充请求阶段、逻辑请求终态、叶子错误码及 TLS/OCSP 诊断口径。

## [0.1.1] - 2026-08-03

### Added

- 自动注册 BestHTTP 结构化遥测接收器，将请求尝试、失败尝试与逻辑请求终态扇出到所有可用的 Nova 通用埋点插件。
- 新增 Network Inspector 上报开关、启动期有界缓存，以及包内 `Nova/Docs` 事件字段与叶子错误码文档。

### Changed

- Nova Framework 最低依赖提升至 `0.6.4`，Best HTTP / Best TLS Security 最低依赖提升至 `3.0.18` / `3.0.5`。

## [0.1.0] - 2026-07-29

### Changed

- 将 Nova Framework 最低依赖版本提升至 `0.6.0`。

## [0.0.13] - 2026-07-21

### Changed

- BestHTTP 传输层接入 Framework `0.5.42` 的 DoH URL 候选规划与 IP 可用性判定契约。
- 将 Nova Framework 最低依赖版本提升至 `0.5.42`。

## [0.0.12] - 2026-07-16

### Fixed

- 初始化 Best TLS Security 时使用 3.0.4 实际公开的 `SecurityOptions` API，关闭 OCSP 在线查询并执行 TLS 数据库初始化，修复错误类型名导致的 `CS0234`。

## [0.0.11] - 2026-07-15

### Changed

- 清理 `BestHttpTransport` 构造函数内已废弃的 TLS/OCSP 配置示例代码，改为保留说明性注释。

## [0.0.10] - 2026-07-13

### Changed

- 将 Nova Framework 与 UniTask 的最低依赖版本分别提升至 `0.5.38`、`10.0.6`，确保 Unity 6000.5 新安装链解析到兼容 Tracker API 的 UniTask 版本。

## [0.0.9] - 2026-07-09

### Added

- 新增可选依赖屏蔽宏 `NOVA_BEST_HTTP`（Runtime asmdef `versionDefines`：`com.tivadar.best.http` 存在即定义，遵循 ADR-064「宏交 asmdef」）。`BestHttpTransport` / `BestHttpTransport.Methods` 中所有对付费外部包 Best HTTP（`Best.HTTP` 命名空间：`HTTPRequest` / `HTTPResponse` / `AsyncHTTPException` / `MultipartFormDataStream` 等）的引用均以 `#if NOVA_BEST_HTTP` 包裹：未安装 Best HTTP 时 `BestHttpTransport` 仍实现 `IHttpTransport` 并正常注册，但各请求方法返回「传输不可用」的降级 `HttpResponse`（不再因缺库编译报 CS0246/CS0234）。`BestHttpTransportRegistration` 无第三方类型引用，保持无条件注册。

### Changed

- 将 Nova Framework 的最低依赖版本提升至 `0.5.37`，避免安装时解析到仍使用旧契约的框架版本。

## [0.0.8] - 2026-06-30

### Changed

- 依赖对齐：`com.solotopia.nova.framework`→`0.5.32`，修复公网 registry 仅有 0.5.32 而旧声明 0.5.31 缺失导致安装 404 的问题。

## [0.0.7] - 2026-06-19

### Changed

- 依赖对齐：`com.solotopia.nova.framework`→`0.5.31`、`com.solotopia.unitask`→`10.0.5`。

## [0.0.6] - 2026-06-18

### Changed

- 例行版本升级。

## [0.0.5] - 2026-06-16

### Changed
- 升级内部依赖 `com.tivadar.best.http` 3.0.8 → 3.0.17、`com.tivadar.best.tlssecurity` 3.0.1 → 3.0.4。

## [0.0.4] - 2026-06-16

### Changed
- 将 `com.tivadar.best.http` / `com.tivadar.best.tlssecurity` 移入 `dependencies`（依赖权威源），`nova.requiredLibraries` 仅保留展示元数据并补全 `purchaseUrl`。
- Runtime asmdef 移除 `versionDefines`、`defineConstraints` 置空（不再依赖 `NOVA_BEST_HTTP` 宏，改由 `dependencies` 保证 BestHTTP 程序集存在）。

## [0.0.3] - 2026-06-15

### Changed
- 刷新 BestHTTP Runtime asmdef 配置，随本轮包内变更发布新版本。

## [0.0.2] - 2026-06-15

### Changed

- 更新包级授权文件内容。

## [0.0.1] - 2026-06-15

### Added

- 新增 Nova Framework 的 BestHTTP 可选 HTTP 后端适配包。
