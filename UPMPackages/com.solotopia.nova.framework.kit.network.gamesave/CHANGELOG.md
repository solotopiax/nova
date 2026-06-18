# Changelog

## [0.0.12] - 2026-06-18

### Changed

- 例行版本升级。

本文件记录该 UPM 包各版本的变更内容，遵循 [Keep a Changelog](https://keepachangelog.com/) 格式。

---

## [0.0.11] - 2026-06-15

### Changed
- 补发上次 release 后的包内变更，并对齐本轮 UPM 发布版本。

## [0.0.10] - 2026-06-10

### Changed
- 批量执行签名 UPM 重新发布，刷新包版本并对齐内网仓库分发批次。

---

## [0.0.9] - 2026-06-09

### Changed
- 优化 `SaveKitConfig` 的 Inspector 提示文案，并同步刷新包内文档说明，明确存档读取 / 写入协议名的填写语义。

## [0.0.8] - 2026-06-04

### Fixed
- 修复发布产物中 SamplePathManifest 未填充重写目标的问题：发布描述符 `nova-samples.json` 的 `sampleManifestRelative` 误指向 `Configs/`（实际在 `Editor/`），导致外部工程 import 后场景 / Prefab 内资产路径仍为开发工程目录 `Assets/Samples/<Demo>/...` 而未替换为真实 import 路径。

---

## [0.0.7] - 2026-06-04

### Changed
- 同步存档协议（PbNetGamesave）与错误码文档；清理 asmdef 与 GameSave 冗余引用。

---

## [0.0.6] - 2026-06-01

### Changed
- 更新 GameSaveDemo 演示示例：刷新登录指令名配置（适配 Login Kit 拆分的 LoginCmdName/DeleteCmdName）与网络指令表数据，演示工程开箱即用。

---

## [0.0.5] - 2026-06-01

### Breaking Changes

- 删除 `Save` 全部 12 个 `cmdName`/`cmdRow` 重载，存取接口收口为 6 个零 cmd 参数极简入口（`GetAsync(key)` / `GetAsync(keys)` / `GetFullAsync()` / `SetAsync(key, value)` / `SetAsync(keys, values)` / `SetFullAsync(value)`），指令名由 ConfigWindow「Kit 配置」统一管理，业务侧不再感知底层协议参数。

### Added

- 新增 `SaveKitConfig`（实现 `IKitConfig`），在 ConfigWindow「Kit 配置」面板填写 Get Cmd Name 与 Set Cmd Name 后，6 个极简入口直接可用，Get 系共用 `GetCmdName`，Set 系共用 `SetCmdName`。

---

## [0.0.4] - 2026-05-28

### Added
- 上传存档请求新增游戏存档版本号、应用版本号、最近设备 ID 三项元数据，应用版本号与设备 ID 由框架自动取值；游戏存档版本号通过新增的 `SetGameVersion` 实例方法注入，登录初始化阶段调用一次后续请求自动携带。
- 拉取存档响应新增最近一次存档的版本号、应用版本号、设备 ID、时间戳元数据，业务侧可用于校验存档来源是否一致。

---

## [0.0.3] - 2026-05-27

### Breaking Changes
- 全量与非全量存档接口拆分为独立入口，全量改走 `GetFullAsync` / `SetFullAsync`，旧版"keys 为空即全量"的隐式回退不再支持。
- 非全量存取强校验入参：key 不可空、value 不可为 null、批量长度必须一致，校验失败直接返回参数错误，不再静默剔除。

### Added
- 存取接口新增「按命令名调用」重载，业务侧可直接传 NetCmd 名称发起请求，无需先查表取 row。
- 新增全量拉取与全量上传接口，整包载荷直接传入即可，无需自拼 keys/values。

---

## [0.0.2] - 2026-05-27

### Added
- 新增 GameSaveDemo 演示 Sample，覆盖 Get 全量 / Get 单 key / Get 批量 keys / Set 单条 / Set 批量五个 Save 业务 Service 调用，打开 GameSaveDemo.unity 直接 Play 即可运行。

---

## [0.0.1] - 2026-05-27

### Added
- 新建 `com.solotopia.nova.framework.kit.network.gamesave` 包，沿用 `kit.network.login` 包结构，定位为云存档业务 Kit，与本机 `PersistManager` 持久化体系不重叠。
- 新增 `Save` 业务 Service（`sealed class`），通过 `Nova.Network.Kit<Save>()` 获取实例；提供 `SetDebugMode(bool)` 实例方法及完整业务接口。
- 新增 `SaveErrorCode` 静态类，占位游戏存档业务错误码段位（8000~8999），与 `NetErrorCode` 通用段、`LoginErrorCode`（7000~7999）错开。
- 新增 `pb_net_save.proto` 协议文件，定义 `PbNetGameDataNode` 通用键值节点，以及 `PbNetGetGameDataReq/Resp` 与 `PbNetSetGameDataReq/Resp` 消息。

### API
- `GetAsync(cmdRow)`：全量拉取存档（不传 keys 即全量）。
- `GetAsync(cmdRow, string key)`：单 key 拉取。
- `GetAsync(cmdRow, string[] keys)`：批量拉取；keys 为 null 或空时退化为全量。
- `SetAsync(cmdRow, string key, string value)`：单条上传。
- `SetAsync(cmdRow, string[] keys, string[] values)`：批量上传；keys/values 必须同时非 null 且长度一致，否则直接返回 `NetErrorCode.PARAM_ERROR`。
- 所有重载统一返回 `NetResponse<PbNetGetGameDataResp>` / `NetResponse<PbNetSetGameDataResp>`，业务侧自行从 `Datas` 取值并 `Util.Json.Parse` 解析 `Value`。
