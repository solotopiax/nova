# Changelog

## [0.0.5] - 2026-06-18

### Changed

- 例行版本升级。

本文件记录该 UPM 包各版本的变更内容，遵循 [Keep a Changelog](https://keepachangelog.com/) 格式。

---

## [0.0.4] - 2026-06-15

### Changed
- 补发上次 release 后的包内变更，并对齐本轮 UPM 发布版本。

## [0.0.3] - 2026-06-10

### Changed
- 批量执行签名 UPM 重新发布，刷新包版本并对齐内网仓库分发批次。

---

## [0.0.2] - 2026-06-09

### Changed
- 优化 `LoginKitConfig` 的 Inspector 提示文案，并同步刷新包内文档说明，明确登录 / 删号协议名的填写语义。

## [0.0.1] - 2026-06-04

### Changed
- 由 com.solotopia.nova.framework.kit.network.login 改名而来，命名空间 .Login.* → .GameLogin.*，Demo LoginDemo → GameLoginDemo。

---

## [0.0.17] - 2026-06-01

### Breaking Changes

- `LoginKitConfig.CmdName` 重命名为 `LoginCmdName`，原引用此属性的业务代码需同步改名；存量 .asset 中旧字段值（`m_CmdName`）通过 `[FormerlySerializedAs]` 自动迁移，无需重填登录指令名。
- `Login.Logout()` 重命名为 `Login.Clear()`，语义不变（清空本实例 UID 与 NetService.Uid，后续请求不再携带 Uid）。

### Added

- 新增账号删除独立指令名配置 `LoginKitConfig.DeleteCmdName`，`Login.DeleteAsync()` 改用此指令名而非复用登录指令名；该字段为新增项，需在 ConfigWindow「Kit 配置」补填后重导出。

---

## [0.0.16] - 2026-06-01

### Breaking Changes

- 删除 `Login.Async` 的 `cmdName` 与 `cmdRow` 两套重载，登录入口简化为只传 `openId`，指令名与渠道由 ConfigWindow「Kit 配置」统一管理，业务侧不再感知底层协议参数。
- `Login.Async` 签名新增首位 `uid` 参数（`Async(string uid, string openId, bool forceNewAccount = false)`），原调用方需在 `openId` 前补传 `string.Empty` 以沿用登录态 uid。

### Added

- 新增 `LoginKitConfig`（实现 `IKitConfig`），在 ConfigWindow「Kit 配置」面板填写 Cmd Name 后，调用 `Login.Async(openId)` 即可完成登录，无需额外参数。
- 登录接口支持显式传入 uid：`Async` 首位参数 `uid` 非空时优先填入请求 Header，为空则沿用 `NetService.Uid`（登录态自动写回值）；`forceNewAccount` 语义不变，始终最终清空 uid。
- 新增账号删除接口 `DeleteAsync()`：删除当前登录账号，身份靠请求 Header.Uid 识别，无需传参；删除成功后自动清空本地登录态（等同登出）；cmdName 临时复用 `LoginKitConfig.CmdName`，待 `LoginKitConfig` 补充 `DeleteCmdName` 字段后需替换。

---

## [0.0.15] - 2026-05-29

### Fixed
- 跟随主包 `0.5.15`：新增包级 `.npmignore` 排除 `nova-samples.json` 及其 .meta，避免外部工程 Console 持续刷 `nova-samples.json has no meta file, but it's in an immutable folder` 警告并重复触发 `SamplePathRewriter.RunRewrite`。

---

## [0.0.14] - 2026-05-29

### Fixed
- 修复外部工程 import GameLoginDemo 后 `SamplePathRewriter` 静默不重写 sample 内场景与 SO 路径的 bug：源 `nova-samples.json` 的 `devPathPrefix` 误带尾斜杠 `Assets/Samples/GameLoginDemo/`，导致 C# 端 `Path.GetFileName(devRoot)` 返回空串、`LocateSampleRoot` 直接退出。现修正源数据为无尾斜杠形式，并由发版脚本统一兜底归一。

### Changed
- 跟随主包 `0.5.14` 发版流水线统一：本子包 sample 与主包 `MainDemo` 走完全对称的 Stage 1 / Stage 3，发版脚本不再针对主包/子包走两条不同分支，sample import 后 Inspector 字段、Docs 路径、Manifest 路径行为与主包一致。

---

## [0.0.13] - 2026-05-29

### Fixed
- 修复外部工程 import GameLoginDemo 后 ConfigWindow 显示 MainDemo 数据 / Procedure 面板路径丢失 / YooAsset Collector 看到 MainDemo 的多项串味问题：发版脚本现在与主包 MainDemo 对称地为子包 sample 复制 `Docs/Excels` + `Docs/Protos` 副本，并把 `Nova.prefab` 中所有 `*SourceDirPath` 字段以 `PrefabInstance override` 形式注入到 sample scene，落盘路径统一指向 `Assets/Samples/GameLoginDemo/Docs/...`。
- 修正 `nova-samples.json` 中 `sampleManifestRelative` 路径从 `Configs/SamplePathManifest.asset` 改为实际位置 `Editor/SamplePathManifest.asset`，避免发版脚本静默跳过 manifest 填充导致 `SamplePathRewriter` 启动时找不到重写目标。
- 消除外部工程 Console 中持续刷的 `Asset Packages/.../package.json.publish.bak has no meta file, but it's in an immutable folder` 报错：`package.json` 备份从包目录移到系统临时目录，避免备份文件随 `file:` 依赖暴露给 Unity 资产数据库。

---

## [0.0.12] - 2026-05-29

### Fixed
- `nova-samples.json` 的 `displayName` 从 `Login Demos` 修正为 `GameLoginDemo`，与 `sampleName` 保持一致。Unity Package Manager 用 `displayName` 作为 import 后 `Assets/Samples/<pkg>/<version>/<sampleDisplayName>/` 的最末层文件夹名，与开发态 `sourceDir` 末段一致，`WorkspaceActive` 的"向上递归找 ConfigMaster"才能命中。

---

## [0.0.11] - 2026-05-29

### Fixed
- 补发版本：`Samples~/GameLoginDemo` 实际已随 0.0.10 进入 tarball，但 `package.json` 缺 `samples` 字段导致 UPM Package Manager 无法识别 sample 入口；本版由发版脚本依据 `nova-samples.json` 自动注入 `samples`，外部工程可直接在 Package Manager 面板按需导入。

---

## [0.0.10] - 2026-05-29

### Added
- 新增 `nova-samples.json` 元数据，声明 `GameLoginDemo` 示例工程；后续可由 `/nova-create-sample` 与发版流水自动化处理 Sample 骨架与路径重写。

---

## [0.0.9] - 2026-05-27

### Added
- 登录入口新增「按命令名调用」重载，业务侧可直接传 NetCmd 名称发起登录，无需再先查表取 row。

### Fixed
- 登录请求对 `openId` 自动归一为空字符串，避免外部传入 `null` 时底层抛异常。

---

## [0.0.8] - 2026-05-26

### Breaking Changes
- 依赖 `com.solotopia.nova.framework.kit.network` 升至 `0.0.9`（kit.network 撤销 `NetRequest` 容器类型）。

### Renamed
- `LoginService` → `Login`：类名简化，与 `Nova.Network.Kit<Login>()` 调用形态对齐。
- 登录入口方法统一重命名为 `Async()`（原 `LoginAsync()`），提供 `ChannelType` 业务入口重载与完整 Proto 入口重载。

### Added
- 新增 `LoginErrorCode` 静态类，占位登录业务错误码段位（7000~7999），与 `NetErrorCode` 客户端段/服务端通用段错开。
- 新增 `SetDebugMode(bool)` 实例方法，支持单 Service 级别的调试模式覆盖。
- 新增 `Logout()` 方法，同步清空本实例 `UID` 与 `NetService.Uid` 静态字段。

### Changed
- 重写 `LoginService`：去除 `NetServiceBase<T>` 继承，改为独立 `sealed class`，自持 `UID` 属性与 `IsLoggedIn` 布尔属性。
- `Async(cmdRow, channel, openId, forceNewAccount)` 业务入口重载：内部改用 `NetBuilder.BuildHeader()` 填充 Head 后直接传裸 Body 给 `NetService.SendAsync`，不再经 `NetBuilder.BuildRequest()` 装箱为容器。
- `Login.Async` 合并为单一重载（去除接受 `NetRequest<PbNetLoginReq>` 的版本，因 kit.network 0.0.9 已撤销 NetRequest 容器类型）。
- `ToProtoChannel` 方法入参改为 `ChannelType`（原为 `PbNetChannel`），在 Service 内部完成映射；`TikTok` / `Official` / `Alipay` 无对应 PbNetChannel 值，统一走 `Unspecified`。

---

## [0.0.7] - 2026-05-21

### Changed
- 包内结构调整与冗余资源优化。

---

## [0.0.6] - 2026-05-21

### Added
- 补齐 `CHANGELOG.md` / `LICENSE.md` / `README.md` 三件套，纳入发版强制校验。

### Changed
- 跟随主框架 0.5.0 升版。
