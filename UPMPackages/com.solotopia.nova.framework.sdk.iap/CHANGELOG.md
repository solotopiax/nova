# Changelog

## [Unreleased]

## [0.1.5] - 2026-08-18

### Fixed

- `PayAsync` 路由层和 `IAPStoreBase.PayGuardAsync` 公共 guard 失败现在也会补齐 `nova_iap_local_pay_fail`，未命中具体 Store 时通过 `nova_channel` 和 `nova_reason_detail` 保留路由失败来源，便于排查未找到支付渠道、Store 禁用、未初始化、重入和商品表缺失等早期失败。

### Changed

- 将 Framework 与 GameLogin 最低依赖同步至 `0.6.15`、`0.1.8`。

## [0.1.4] - 2026-08-13

### Changed

- 将 Framework 与 GameLogin 最低依赖同步至本轮发布版本。

## [0.1.3] - 2026-08-12

### Breaking

- `IAPResult.FailReason` 改为 `ErrorDesc`；失败构造路径要求提供 `IAPErrorSource`，业务应以 `(ErrorSource, ErrorCode)` 解码失败类型。

### Added

- 失败结果可保留已生成的 `OrderId` 与补单标记，便于服务端拒绝后的订单定位。

### Fixed

- IAPPlugin 发现已安装但未配置 StoreConfig 的可选 Store 时直接跳过初始化，避免 ThirdPay/Voucher 等可选渠道因 null 配置误初始化并报错。
- IAPDemo 的 HybridCLR AOT 元数据列表补充 ThirdPay 与 Voucher 可选支付包 DLL，避免安装对应包后缺少补充元数据配置。
- IAPDemo 的 ConfigRuntime 快照不再序列化 ThirdPay 可选包 Store 配置，避免未加载 `NovaFramework.SDK.IAP.ThirdPay.Runtime` 时 ConfigRuntimeSO 整体反序列化失败。
- 登录后自动补单后台任务会在插件释放时取消，避免继续访问已释放的 Store。

## [0.1.2] - 2026-08-03

### Changed

- IAPDemo 同步 Localization 支持语言表与 JSON / Binary 数据格式能力，并将 Framework 最低依赖提升至 `0.6.3`。

## [0.1.1] - 2026-07-31

### Changed

- 将 `IAPPlugin` 初始化优先级调整为 `70`，避免与其依赖的打点插件处于同一并行分桶。

## [0.1.0] - 2026-07-29

### Changed

- 将 Framework 与 GameLogin 最低依赖分别提升至 `0.6.0` 与 `0.1.0`。
- IAPDemo 同步启动应用配置网络命令、运行时配置与场景覆盖。

## [0.0.20] - 2026-07-23

### Added

- 商品列表编辑器新增 Excel 工作簿导入与模板导出，支持校验表头、商品字段、`TableId` 范围及重复项后一次性写入配置。

## [0.0.19] - 2026-07-23

### Changed

- `EnableAlwaysPaySucceed` 限定为 Editor 调试开关，非 Editor 编译态构造 Store 上下文时强制关闭。

## [0.0.18] - 2026-07-21

### Added

- 新增 IAP 埋点表，统一记录支付、补单、恢复与商品查询的事件契约。

### Changed

- 商品列表编辑器的重复高亮语义明确为 `TableId`，允许不同 `TableId` 复用同一 `ProductID`。
- Registry 中 `0.0.17` 已存在，本次发布直接升至 `0.0.18`。

## [0.0.16] - 2026-07-14

### Changed

- 为 `IAPRequest` / `IAPResult` 增加平台票据透传字段 `ReceiptParam`，支付、补单与恢复结果可统一回传该值。
- 收敛补单扫描并发与登录前延迟请求，当前扫描结束后可按需补跑一轮，避免重复触发或漏扫。

## [0.0.15] - 2026-07-13

### Changed

- 提升 Framework、UniTask 与 GameLogin 的依赖下界，保证独立安装时完整解析到本轮 Unity 6000.5 兼容版本。

## [0.0.14] - 2026-07-13

### Changed

- 将 Nova Framework 的最低依赖版本提升至 `0.5.37`，避免安装时解析到仍使用旧契约的框架版本。

## [0.0.13] - 2026-07-08

### Fixed

- 清理 sample asmdef 克隆残留引用：删除代码未使用的 gamesave / tga / appsflyer / ad 跨包程序集引用（从 MainDemo 模板克隆时带入，IAPDemo 实际未调用），避免消费工程未安装这些无关包时编译报 CS0234。
- 补全 sample 依赖声明：package.json dependencies 增加 com.solotopia.nova.framework.kit.network.gamelogin（IAPDemo 的登录演示实际使用），修复消费工程 import sample 后缺对应程序集的编译失败。
- 保留 iap.mobile 程序集引用：IAPDemo 的移动端订阅演示（`DemoIAPBridge.Mobile.cs`）使用 `NovaFramework.SDK.IAP.Runtime` 命名空间下、由 iap.mobile 程序集实现的 `IIAPMobileSubscriptionCapable` 等能力，故 asmdef 保留该引用（该演示需搭配安装 `com.solotopia.nova.framework.sdk.iap.mobile`；因 iap.mobile 反向依赖本包，无法在本包 dependencies 声明，属伴生包软要求）。

## [0.0.12] - 2026-06-30

### Changed

- 依赖对齐：`com.solotopia.nova.framework`→`0.5.32`，修复公网 registry 仅有 0.5.32 而旧声明 0.5.31 缺失导致安装 404 的问题。

### Removed

- `IIAPStoreContext` 移除 `IUIManager UIManager` 成员，`IAPStoreContext` 构造器同步移除 `IUIManager` 参数（破坏性变更）。全仓 grep 确认无消费方引用该成员，消费方 store 实现仅透传 context，编译不受影响；`iap.mobile` 对本包依赖下界已提升至 0.0.12。

## [0.0.11] - 2026-06-19

### Changed

- 依赖对齐：`com.solotopia.nova.framework`→`0.5.31`、`com.solotopia.unitask`→`10.0.5`。

## [0.0.10] - 2026-06-18

### Changed

- 例行版本升级。

本文件记录该 UPM 包各版本的变更内容，遵循 [Keep a Changelog](https://keepachangelog.com/) 格式。

---

## [0.0.9] - 2026-06-15

### Changed
- IAPDemo sample 字体资源随包发布同步刷新。

## [0.0.8] - 2026-06-15

### Changed
- Store 上下文新增当前 `DevelopMode`，支付打点 Debug 字段改为按 `DevelopMode.Debug` 判断，不再复用 `EnableAlwaysPaySucceed`。

---

## [0.0.7] - 2026-06-10

### Changed
- 批量执行签名 UPM 重新发布，刷新包版本并对齐内网仓库分发批次。

---

## [0.0.6] - 2026-06-09

### Changed
- 刷新 IAP 主包文档索引与接入说明，统一术语与使用描述，便于 store 侧能力检索。

## [0.0.5] - 2026-06-04

### Fixed
- 修复发布产物中 SamplePathManifest 未填充重写目标的问题：发布描述符 `nova-samples.json` 的 `sampleManifestRelative` 误指向 `Configs/`（实际在 `Editor/`），导致外部工程 import 后场景 / Prefab 内资产路径仍为开发工程目录 `Assets/Samples/<Demo>/...` 而未替换为真实 import 路径。

---

## [0.0.4] - 2026-06-04

### Changed
- 重构 IAP 多包结构：渠道专属 Request / Capability / DeductPlan 下沉至各子包，启用状态与懒初始化内聚至 IAPStoreBase。
- 所有商店存储统一为 baseStore；优化支付表配置页面卡顿。

### Removed
- 删除死代码 PendingOrderQueue 与 IAPPlugin 集中管控的 m_RuntimeDisabledStores。

### Fixed
- 修正若干支付逻辑与打点报错。

---

## [0.0.3] - 2026-05-21

### Changed
- 包内结构调整与冗余资源优化。

---

## [0.0.2] - 2026-05-21

### Added
- 补齐 `CHANGELOG.md` / `LICENSE.md` / `README.md` 三件套，纳入发版强制校验。

### Changed
- 跟随主框架 0.5.0 升版。
