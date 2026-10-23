# Changelog

本文件记录 Nova Framework 各版本的团队聚合变更内容，遵循 [Keep a Changelog](https://keepachangelog.com/) 格式。

> 详细包级变更见 `Assets/Framework/CHANGELOG.md`，本文件仅作团队聚合视图，不进 npm tarball。

---

## [Unreleased]

## [0.7.1] - 2026-10-23

### Changed

- NovaSpark 默认显式安装 Framework、Nova Framework - MCP 与 Nova Framework - MemoryProtection，同步检测缺失及版本不一致；Anti-Cheat Toolkit 保持按需安装。

## [0.7.0] - 2026-10-23

### Changed

- 功能交付纳入 AGENTS、Docs、Minds、Agents 四向同步与逐函数能力暴露门；日常健康检查和主仓 UPM 预检共用覆盖校验，防止源码、文档、输入与执行契约漂移。

- nova-publish 支持 CHANGELOG Important 多行声明的开发维护、发版前全文核对与修改补充询问，并在签名包上传前校验声明归档和日志完整一致性。

- 调整 Agent 知识维护与发布门禁：说明归源码/XML 和既有 Docs，派生索引使用本地缓存及脱敏发布快照，路径变化只更新导航，不续签历史审查。

### Fixed

- 修正 NovaSpark 首次安装完成后打开 PlugPals 的菜单路径为 `Nova/Open PlugPals`，避免因多余空格报菜单不存在、反复重试并无法自动删除火种脚本。

### Breaking

- Nova 最低支持 Unity 6000.6.7f1，统一包元数据、Skill 契约和 NovaSpark 安装门；适配新版对象身份与布局接口。

## [0.6.39] - 2026-10-12

### Added

- Pipify 新增 Google Play AAB 上传，支持本批次成功产物或已有 AAB、内部测试草稿、指定轨道发布与更新说明，隔离项目配置和凭据环境；失败、取消、超时或上传过程中重载时停止并清理临时目录。新增 nova-project-upload-google-play Skill，复用 Pipify Action 分别执行已授权上传或只读权限、版本号排查，独立声明外部写入与凭据使用，消费端 Skill 共 31 项。

### Changed

- Google Play 轨道与更新说明语言提供中文预设及自定义输入，兼容已有字符串配置，取消编辑不置脏；界面明确草稿、开放下载、轨道用户范围和审核限制，提示连续编号。官方参数链接、字段补填提示与参数区留白统一布局和行高，点击链接不修改配置。

- Google Play 凭据只接受必填的 Unity 项目根相对 JSON 路径，执行前转为绝对路径；旧绝对路径或凭据环境变量配置须迁移。新建步骤使用已配置的凭据初值、内部测试草稿、英语（美国）、1800 秒超时，默认关闭仅验证和手动审核；既有参数不回写，公开副本脱敏配置及源码默认凭据路径。

- Google Play 仅验证仍会真实上传并校验轨道、发布状态及适用比例，最后不提交 edit、不保存轨道版本或开放下载，versionCode 可能被占用；手动审核选项灰显且不传入验证进程，退出后恢复原选择。草稿同样禁用手动审核并显示未勾选，UI/CLI 共用限制，切换开放下载时保留原选择。

- MainDemo 的资源构建打包批次加入 EDM4U Android 依赖解析，并新增资源构建、AAB 打包与 Google Play 上传组合批次及已有 AAB 单独上传批次，运行时快照按 Android/Google 坐标导出；上传前须配置本项目的产物、包名与凭据。

- MCP 配套依赖下界提升至 0.1.4，消费工程升级 Framework 时同步取得移除能力总览专用 API 的新版 MCP。

### Fixed

- Pipify 只记录明确成功的 Player 构建产物并在取消时中断；CLI 等待完整异步流水线结束后返回退出码，CI 命令须省略 -quit。窗口构建后在上传开始前完成程序集重载，校验配置与 AAB 身份后继续；重载中断清除残留进度并提示远端状态，已启动上传不自动重试。

- Google Play AAB 路径与应用包名必须成对填写或留空；缺项时在字段旁显示红框和补填提示、禁用运行，UI/CLI 执行前统一校验，避免重复提示。

- Google Play 未选择手动审核时允许 fastlane 按 Google 要求兼容同一次 edit 的提交参数，不重传 AAB；保留勾选时不擅自改为自动审核，参数被拒时给出具体操作提示。

- Pipify Step 复制成功通知固定使用绿色，避免沿用上次流水线失败通知的红色。

- 发布脚本 dry-run 只做检查与命令预演，不改写 Sample、Nova.prefab、包元数据或发布资料，不生成临时签名产物。

- UPM 与公开副仓脱敏覆盖微信小游戏引力 m_GravityAccessToken，非空值替换为字段专用占位符，strict 检查全部维度与公开副本残留；主仓开发配置不回写。

### Breaking

- 移除 Nova/Open Agent Capabilities View 菜单、AgentCapabilitiesWindow、EditorUtil.AgentCapabilities、视图专用刷新通知，以及 MCP 的 NovaProjectActionExposureSnapshot 与 NovaProjectActionGateway.GetExposureSnapshot；保留 Agent 模块、Skill 自动发现与投影、Project Action 和 MCP 执行链，外部快照调用方须改用 describe 协议。

## [0.6.38] - 2026-10-10

### Added

- 微信小游戏 TagsOnLaunch 接入 YooAsset 3.0.6 原生预下载，复用启动下载进度、主备重试和取消流程；OnDemand 与 AllOnLaunch 保持原语义。

- 新增资源加解密能力：Bundle 支持 None、Offset、XOR、AES 内存和分块认证流式方案，清单独立支持 None、AES、XOR，运行时自动匹配解密实现。

- ConfigMaster 按包名与 keyId 管理资源密钥及三维当前编号，纳入 WorkingCopy 和全局保存；默认全局共用并保留旧解密密钥，分维清单构建缺少明确平台时停止。客户端提供器按导出命名空间放入实际启动程序集，不生成固定 asmdef，并校验启动与裁剪边界。

- 资源清单构建与客户端复用 Runtime 算法；Editor 使用配对的 ResourceManifestAesDecryptor / ResourceManifestXorDecryptor，构建前校验方案与密钥一致性，Pipify 说明统一为配置入口和配套解密器，不提供未发布旧类名兼容入口。

- 受控 AssetBundle / RawFile Bundle Action 支持五种 Bundle 与三种独立清单逻辑方案，计划冻结正式类型及配对；省略清单参数的既有 None 请求继续有效。

### Changed

- 打点源表和汇总表统一 Network 模板的字体、字号、单色数据区、列宽及 A～C 事件分组合并，取消交替底色和 G～I 合并。

- YooAsset 依赖提升至私包 1.2.0（官方 Core 3.0.6），新增平台预下载适配注册入口；Web Player 不记录原生本地可启动版本。

- 资源密钥以已保存的 ConfigMaster 为唯一来源，旧独立 JSON 迁移后移除；源码、link.xml 与 meta 纳入项目私有仓库，不再默认忽略。保存只写 Master，顶部导出与 Pipify Config 导出才生成客户端源码；各 Demo 目录约定为 Scripts/Runtime/ResourceKeys。

- Config 保存按当前维度整理全部配置，清理无效坐标和旧 Override，保留独立维度及全部解密密钥；移除历史整理确认，导出仍只读取已保存数据。

- 移除仅用于 CHANGELOG 历史校验的 GitLab CI，保留本地提交、日常健康检查和发布前历史校验。

### Fixed

- 适配 YooAsset 3.0.6 BeginCollect 新增路径校验参数，Table 收集检查仍默认关闭路径校验。

- 原生下载继续在最终缓存内容校验后通知 URL 策略，保留内容校验失败后的主备重试。

- 资源密钥页按包选择、现有密钥、新增密钥和底部操作重排，修复标题裁切、卡片间距及字段错位，统一按钮高度与内容缩进；只从已有 YooAsset Package 创建记录，删除仅影响密钥，保留并提示 Collector 外旧记录。

- 修复资源密钥页在保存或 C# 编译后卡在读取状态；新增和显隐操作收至列表标题，未保存新增项以黄色标识，导入改为手动输入。

- Config 关闭时支持保存、不保存和取消；取消保留未保存草稿，保存失败不丢失编辑数据。

- Pipify 与 Bundle Builder 的加解密下拉共用候选和排序，排除抽象、不可实例化类型及客户端通用清单解密器。

- Demo 全局值按当前坐标归一，保留独立维度配置，移除无效 None 渠道行并重新导出运行时配置。

- 修正 Tracks 字段类型、归因属性交付时点、网络结果与错误码及资源阶段说明，明确支付汇总由服务端上报；运行时采集与目标保持不变。

- 默认 Sample 无资源密钥且不启用资源加密；清理微信 Demo 密钥记录、生成源码和裁剪文件，Bundle 与清单保持官方 None。

- UPM 和公开副仓统一清空资源密钥配置并移除工具生成文件；strict 检查全副本密钥残留及仍需私有密钥的构建选择，异常时停止写入，不回写主仓私有配置。

- 校正 Framework、GameLogin、IAP 与微信小游戏 Tracks 表的字段类型、触发时点、价格及支付阶段含义和客户端/服务端职责，运行时事件名称与上报目标保持不变。

- 修复发布工具因既有重复历史版本节无法收口新版本的问题，保留全部已发布正文和顺序，继续拒绝重复创建目标版本。

### Validation

- 资源加解密五种 Bundle × 三种清单共 15 组 Android 目标构建及 Editor OfflinePlayMode 验证通过，覆盖五类资源加载、句柄释放和 Bundle 卸载；临时密钥与测试产物已恢复。

## [0.6.37] - 2026-10-08

### Changed

- Framework 新增可选注册并登录及一次性用户属性能力；GameLogin 根据服务端新账号标记通知 SDK，TGA 暴露一次性用户属性，微信小游戏补齐购买表、支付阶段与统计恢复、广告及 Demo 接线。

### Breaking

- 微信小游戏调整 iOS 支付参数与门禁：道具固定现网并省略 platform，游戏币使用 requestMidasPayment / mode=game 并检查支付支持。

## [0.6.36] - 2026-09-29

### Changed

- Framework 补充 iOS Info.plist 用途说明配置并修复 iOS 构建编译错误；WeChat MiniGame SDK 同步 Demo 隐私配置快照。

## [0.6.35] - 2026-09-28

### Changed

- 发布 Framework `0.6.35`，同步发布 Ad `1.1.15`、AIHelp `0.0.16`、AppsFlyer `0.1.12`、DataMaster ABTest `0.1.1`、Facebook `0.1.14`、Firebase `0.1.17`、IAP `0.1.17`、TGA `0.1.17` 与 WeChat MiniGame SDK `0.0.3`；框架更新 SDK 初始化、iOS 隐私声明和本地化，微信包接入引力引擎、DataNexus 并更新服务端协议。

- CHANGELOG 维护新增结构化 add/release 工具，并由日常 health、Git 暂存区钩子、CI 与发布器共同阻断已发布版本节改写。

### Fixed

- Sample 依赖审计支持描述符显式声明演示专属包，避免将仅供 Demo 使用的 Kit/SDK 错误提升为包级强制依赖。

### Breaking

- 自定义 `ISDKManager` 实现需补齐新增方法；依赖 SDK 初始化先后顺序的插件需改用显式能力依赖。微信小游戏服务端需按新的 `PbNetWechat*` 协议和每批最多 20 笔验单接口升级。

## [0.6.34] - 2026-09-23

### Changed

- 发布 Framework `0.6.34` 与 WeChat MiniGame SDK `0.0.2`；Framework 改用构建期 WebGL 布局清单并修复 Player SDK 插件发现，微信异步 API 与持续监听回调补充分级脱敏日志。

## [0.6.33] - 2026-09-23

### Breaking

- Framework 删除旧 BestHTTP 兼容接口与 Config 历史迁移入口；仍依赖旧接口或旧 schema 的项目必须先迁移再升级。
- IAP Core 删除派生 Store 的旧 protected 打点接口；自定义 Store 必须改用新的通用属性与事件发送入口。
- 微信小游戏接入包 ID 从 `com.solotopia.nova.framework.wechat.minigame` 调整为 `com.solotopia.nova.framework.sdk.wechat.minigame`，使其在 PlugPals 中归入 SDK 分类；公开副仓不再包含该包。

### Changed

- Pipify 在调用每个 Step 前统一展开本次参数快照中的标准文本占位符，UI 与 CLI 使用相同语义且不改写存档。
- 发布 Framework `0.6.33`、Ad `1.1.14`、DataMaster ABTest `0.1.0`、IAP `0.1.16`、IAP Mobile `0.1.14`、MAX `0.1.12`、TGA `0.1.16` 与 WeChat MiniGame SDK `0.0.1`。

## [0.6.32] - 2026-09-20

### Added

- 新增 `com.solotopia.nova.framework.wechat.minigame` 接入包，内置微信官方 SDK `0.1.34`，并隔离普通浏览器 WebGL 与微信小游戏的原生库和启动逻辑。

### Changed

- `nova.project.player.build` 开放 WebGL Player 目录构建，沿用既有构建坐标与输出路径契约。

### Fixed

- FileFragment 增加崩溃安全写入、完整性校验、备份恢复和坏档隔离，单个损坏分片不再阻断 Persist 启动。
- Generate Actions 接受合法的 `ChannelType.None`，WebGL HybridCLR 预构建不再被错误拦截。

## [0.6.31] - 2026-09-18

### Breaking

- Config 的 Platform 与 Channel 坐标不再接受 `None`；旧 Channel None 数据按有效坐标迁移到 Official，旧 Platform None 数据会被清理，导出、构建和运行时均拒绝无效坐标。
- 资源预加载统一迁移到可显式释放的 Warmup Group；删除未投入使用的 `PreloadAsync` 与未生效的自动热更配置。

### Added

- WebGL 新增按启动 Tag、按需和启动全量三种资源策略，并支持按全部资源、Tag 或地址创建可控生命周期的 Warmup Group。

### Changed

- WebGL 启动资源准备、首包布局、超时与 CDN 回退链按 YooAsset 官方目录能力重新收口；CDN 部署改为直接覆盖上传，不再提供上传前清理。
- 应用配置网络日志仅在成功时输出完整响应，失败继续使用明确的 Warning 或 Error。
- 同步发布 SDK Ad `1.1.13`、AIHelp `0.0.15`、Apple Sign-In `0.0.20`、Firebase `0.1.16`、Google Sign-In `0.0.21`、IAP `0.1.15`、IAP Mobile `0.1.13`、MAX `0.1.11` 与 TGA `0.1.15`。

### Fixed

- 修复 WebGL SDK 配置类型缺失、FileFragment 恢复与删除阻塞、应用更新及失败退出误触发等问题，并让相关 Inspector 提示随配置可用状态正确显示。

## [0.6.30] - 2026-09-15

### Changed

- HostKey + NetCmd 业务请求候选链耗尽后的“通信失败 / 结果未确认”终态日志降为 Warning，避免普通网络失败被 TGA `ta_app_crash` 误记为应用崩溃；网络诊断继续使用 `uwr_request_start/error/end`。

### Fixed

- ConfigWindow 保存复制或旧版本遗留的维度矩阵时，可按当前编辑坐标一键归一实际冲突组并重新校验后保存；用户弹窗只说明原因、影响与操作建议，字段路径和逻辑键仅写入 Console / Editor.log；已勾选维度继续保留独立分支，SDK / Kit 托管配置保持逐格深拷贝，避免用户陷入无法保存且无有效恢复入口的死循环；插件实例 Ensure Action 增加写盘前预演，拒绝生成新的维度不一致。

## [0.6.29] - 2026-09-14

### Added

- `Nova.Asset` 新增同步启动白名单查询，可区分白名单数据不可用、设备命中与设备未命中。

### Fixed

- Asset 下载策略修复同一文件重叠请求共用候选状态导致的请求冲突。
- Firebase Topic 订阅与退订对注册服务瞬时错误执行有界重试和补偿同步。

### Changed

- 同步发布 Firebase `0.1.15`。

## [0.6.28] - 2026-09-14

### Fixed

- `SDKComponent.InitializeTask` 支持初始化中的并发等待与完成后的重复等待，并保持初始化只执行一次。
- ConfigWindow 修复未勾选维度广播、维度 Mask 原子投影、矩阵一致性校验及 WorkingCopy 保存/导出链问题。

### Changed

- 同步发布 SDK Ad `1.1.12`、DataMaster ABTest `0.0.20`、Firebase `0.1.14`、IAP `0.1.14` 与 MAX `0.1.10`。

## [0.6.27] - 2026-09-04

### Changed

- ProjectGuard 将 Play 阻断问题按应用、隐私、SDK、Kit 等类别合并，并展示占位值、格式错误或导出差异等真实原因。
- SDK Inspector 生成的打点汇总表恢复事件表头与数据行的正确字体样式。
- Network Inspector 将默认 HTTP 超时字段改称“业务请求超时时间（秒）”，运行时语义不变。
- 强化发布日志治理：重要代码调整在开发阶段先记录到 `[Unreleased]`，发版前逐项核对代码差异与日志覆盖关系，并通过自动审计阻止遗漏或未收口的条目进入发布。

## [0.6.26] - 2026-09-04

### Breaking

- 删除未投入使用的 AssetBundle 解密占位契约与 `AssetPlayMode.WebPlayMode`；WebGL 统一使用 `HostPlayMode`。
- `IUIManager` 新增 `OnOpenUIViewFail` 事件，仓外自定义实现需补齐该成员。

### Added

- WebGL HostPlayMode 增加 Bundle 单次物理请求总超时，并支持远端 Manifest 候选耗尽后回退首包元数据。
- BuildReadiness 增加 Asset/App 场景渠道与 ConfigRuntime 导出渠道一致性校验，不一致时阻断 Player 构建。

### Changed

- 默认 Unity MCP Provider 从 `10.1.2` 升级到 `10.2.0`。
- 所有已注册 Nova Project Action 默认进入 MCP 显式白名单，并新增可执行既有 Pipify Batch 的异步 Action；高风险操作仍保留计划、确认、锁与 Verify。
- 发布 Framework `0.6.26`、Nova MCP `0.1.3`，并同步发布 13 个适配 `IUIManager.OnOpenUIViewFail` 的 Network Kit / SDK Sample 包及包含 Android ProGuard 更新的 MAX `0.1.9`。

## [0.6.25] - 2026-09-02

### Breaking

- Framework 网络移除 DoH、IP 注入和可选 HTTP 后端，统一使用 UnityWebRequest 与系统 DNS。

### Removed

- 删除 Nova HTTP 适配子包及对应第三方 HTTP/TLS 包；App、Asset 与 HostKey + NetCmd 业务请求分别保留明确的主备域名策略。

### Changed

- App、Asset 与 `HostKey + NetCmd` 统一复用 UWR 主备规划器：全部候选走完算一轮，每次重试重新执行全部轮次，并支持最近成功域名优先与 `uwr_request_start/error/end` 链路埋点。

## [0.6.24] - 2026-08-31

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.6.24]` 节。

### Fixed

- 修复 `PackageFilePrefix` 变化后的覆盖安装缓存兼容、白名单元数据重试、CDN 多分支清理和动态时间前缀部署问题。
- 修复清单回退未复核启动资源完整性，以及内置回退后错误停留在 `OfflinePlayMode` 的问题。

---

## [0.6.23] - 2026-08-29

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.6.23]` 节。

### Fixed

- 修复启动白名单在 `PackageFilePrefix` 非空时未将 YooAsset `.version/.hash/.bytes` 请求路由到白名单元数据地址的问题。

---

## [0.6.22] - 2026-08-28

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.6.22]` 节。

### Breaking

- `IAssetManager` / `AssetManagerBase` 新增 `HasPatchByTagsAsync`；仓外自定义资源 Manager 实现需要补齐该成员。

### Fixed

- 启动期补丁检查与实际 Tag 下载范围保持一致，并统一 Launcher 文本渲染职责；同步发布 13 个带新版 Launcher Prefab 的 Network Kit 与 SDK Sample。

---

## [0.6.21] - 2026-08-28

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.6.21]` 节。

### Changed

- 发布 Framework `0.6.21`，完善 PlugPals 包解析状态识别，并允许 ConfigWindow 跨平台编辑和保存配置、仅在平台一致时导出。
- README 补充系统要求、核心依赖和框架能力说明。

---

## [0.6.20] - 2026-08-27

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.6.20]` 节。

### Breaking

- `ConfigMasterSO.CurrentPlatform` 改为只读并实时映射 Unity Active BuildTarget；外部直接赋值代码需要迁移。

### Added

- Framework 新增 Arabic RTL 文本整形、统一语言启动策略、推荐更新提示间隔和 Editor Active Platform 工具。

### Changed

- 发布 Framework `0.6.20`，并同步发布 13 个包含新版 Localization 预加载顺序的 Network Kit 与 SDK Sample。

---

## [0.6.19] - 2026-08-27

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.6.19]` 节。

### Fixed

- 修复 Sample 覆盖导入后的路径重写、ConfigMaster 与 PipifySettings 场景切换恢复，以及 ConfigWindow 首次绑定后的编辑保存流程。
- 发布 Framework `0.6.19`，并同步发布包含新版 Sample 路径重写器的 Network Kit 与 SDK 包。

---

## [0.6.18] - 2026-08-26

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.6.18]` 节。

### Added

- 发布 Agent Capabilities 只读总览、MCP Action 暴露快照与 SDK 构建完成钩子。

### Changed

- 发布 Framework `0.6.18`、Nova MCP `0.1.2`、Facebook `0.1.8`、Firebase `0.1.7`、IAP `0.1.7` 与 IAP Mobile `0.1.10`。
- Nova 菜单组合词统一使用空格分隔，并收录 Firebase、IAP 与移动内购稳定性修复。

---

## [0.6.17] - 2026-08-21

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.6.17]` 节。

### Breaking

- `IAdPlugin` 新增广告隐私授权状态查询与异步等待成员；仓外自定义实现需同步适配。

### Added

- 发布表、网络、声音、振动与本地化五类受控导出 Action，并通过 Nova MCP 向消费项目开放。

### Changed

- 发布 Framework `0.6.17`、Nova MCP `0.1.1`、SDK Ad `1.1.6` 与 SDK MAX `0.1.7`，依赖下界沿实际消费链同步提升。

---

## [0.6.16] - 2026-08-20

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.6.16]` 节。

### Breaking

- HybridCLR 的 `GameDlls` 源码字段更名为 `StartupGameDlls`；序列化资产兼容，直接访问旧字段的外部代码需同步改名。

### Added

- 发布 Nova 消费端 Skills、受控 Project Action 执行层与 `com.solotopia.nova.framework.mcp@0.1.0`。

### Changed

- 发布 Framework `0.6.16`，统一 Sample 场景配置路由，并更新 Config、HybridCLR、构建链与 MainDemo。
- 仅对本轮 Sample 实际变化的 Kit/SDK 包提升 Framework 最低依赖，不级联未变化包。

---

## [0.6.15] - 2026-08-18

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.6.15]` 节。

### Changed

- 发布 Framework `0.6.15`，收录本轮资源版本检查、YooAsset RawFile 构建和 Sample ConfigMaster 配置投影更新。
- Framework 依赖闭环同步至 Luban `10.1.0`、HybridCLR `10.1.0` 与 YooAsset `1.1.0`；下游 UPM 包同步提升最低依赖。

---

## [0.6.14] - 2026-08-17

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.6.14]` 节。

### Breaking

- `INativeManager` / `NativeManagerBase` 新增 `RequestInAppReviewAsync(CancellationToken)`；自定义 Native Manager 实现需补齐该成员。

### Added

- 发布 Framework `0.6.14`，新增 Android / iOS 应用内评价原生桥接与 MainDemo 显式演示入口。

### Changed

- Pipify 支持外部异步任务状态展示；Framework 关闭链在所有 Manager 完成关闭后清理默认 AES 凭据。

### Fixed

- App 更新默认关闭；资源下载 URL 策略、配置关闭路径与 Native 构建桥接同步收敛。

---

## [0.6.12] - 2026-08-14

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.6.12]` 节。

### Breaking

- 网络公共 `INetworkManager` / `IDoHManager` 契约新增主备 URL 解析与运行时 DoH 开关方法；自定义实现需重新编译并补齐实现。

### Added

- 发布 Framework `0.6.12`、BestHTTP 适配包 `0.1.6` 与 IAP Mobile `0.1.8`。
- 业务网络支持主备域名、DoH IPv4 候选和保留原域名 Host/SNI 的指定连接 IP 路由；Nova 安装或升级后自动投影项目组 Skill。

### Fixed

- IAP Mobile 启动和订阅到期刷新不再触发 iOS 平台恢复购买弹窗。

## [0.6.11] - 2026-08-13

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.6.11]` 节。

### Changed

- 发布 Framework `0.6.11`、Alibaba Cloud OSS Editor Tool `0.0.2` 与 BestHTTP 适配包 `0.1.5`；内部 Best HTTP 升级至 `3.0.19`，Best TLS Security 保持 `3.0.5`。
- Framework 的 AES 缺配诊断指向隐私配置的当前坐标；启用 AES 的 Persist 存储实现会在加载前校验默认凭据，标准顺序为先加载 Config 再加载 Persist。
- Alibaba Cloud OSS 改为可选 Editor 工具包，不再进入 Player；缺少时 CDN 面板仅禁用 OSS 部署并提供安装入口。
- `NovaSpark.cs` 固定 Framework `0.6.11` 与 BestHTTP `0.1.5`；EDM `1.2.188` 与 unity-mcp `v10.1.2` 保持当前版本。

## [0.6.9] - 2026-08-12

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.6.9]` 节。

### Breaking

- IAP 结果契约扩展 `IIAPResult` 字段，并将 `IAPResult.FailReason` 更名为 `ErrorDesc`；自定义结果实现和业务失败处理需按 `(ErrorSource, ErrorCode)` 适配。

### Added

- Framework 新增精准通知设置入口：Android API 26+、iOS 15.4+ 可直达当前应用的通知设置；不支持时明确返回 `false`，不回退到应用设置。
- AIHelp、AppsFlyer、Facebook、Firebase、MAX 与 TGA 新增官方 Console / Readme 菜单入口。

### Fixed

- IAP Mobile 修复未完成订单身份、商品拉取重试和迟到失败回调处理，避免共用 SKU 错认订单或网络抖动后商品永久不可用。
- Pipify / Build 与 HybridCLR Development ABI 校验对齐，避免构建参数和产物不一致。

### Changed

- 发布 Framework `0.6.9`、AIHelp `0.0.6`、AppsFlyer `0.1.3`、Facebook `0.1.4`、Firebase `0.1.3`、IAP `0.1.3`、IAP Mobile `0.1.5`、MAX `0.1.4` 与 TGA `0.1.5`。
- `NovaSpark.cs` 固定 Framework `0.6.9`；BestHTTP `0.1.3`、EDM `1.2.188` 与 unity-mcp `v10.1.2` 保持当前版本。

## [0.6.8] - 2026-08-07

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.6.8]` 节。

### Added

- Framework 新增 Native 通知权限模块、CDN 部署前远端清理、最新 YooAsset 版本自动关联和构建期运行时配置临时投影。
- MAX 新增 iOS 构建后 SKAdNetworkItems 离线补全，规避 AppLovin 在线列表超时导致的条目缺失。

### Changed

- IAP Mobile 扩充平台订单成功与失败诊断日志，补充交易 ID、账号透传字段、失败详情和 tableId 解析来源。
- 发布 Framework `0.6.8`、IAP Mobile `0.1.4` 与 MAX `0.1.3`。
- `NovaSpark.cs` 固定 Framework `0.6.8` 与 BestHTTP `0.1.3`；EDM `1.2.188` 与 unity-mcp `v10.1.2` 保持当前最新版本。

## [0.6.7] - 2026-08-06

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.6.7]` 节。

### Breaking

- Framework `IAssetManager` / `AssetManagerBase` 新增设备白名单与可启动版本提交方法，自定义 Asset Manager 实现需同步补齐契约。

### Added

- Framework 新增启动白名单、元数据根地址、URL 候选轮换、可启动版本记录、本地热更缓存清理与 CDN 白名单部署能力。
- GameBind 新增绑定状态查询协议/API 与 `ErrUIDAlreadyBoundOtherOpenID`(10408) 错误码。

### Fixed

- BestHTTP 修复 iOS IL2CPP 启动期 UniTask PlayerLoop 未就绪异常，并在下载回调释放前复制响应内容。

### Changed

- 发布 Framework `0.6.7`、BestHTTP `0.1.3`、GameBind `0.0.11`、GameLogin `0.1.5`、GameSave `0.1.3`、AIHelp `0.0.5`、DataMaster ABTest `0.0.11`、IAP Mobile `0.1.3` 与 MAX `0.1.2`。
- 更新各接入包 `coreVersion` / 原厂依赖元数据，MAX 同步 Moloco / Unity Ads adapter 版本并改为仅保存变更的 AppLovinSettings 资产。
- `NovaSpark.cs` 固定 Framework `0.6.7` 与 BestHTTP `0.1.3`；EDM `1.2.188` 与 unity-mcp `v10.1.2` 保持当前最新版本。

## [0.6.6] - 2026-08-05

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.6.6]` 节。

### Fixed

- Config 导出新增 SDK / Kit 启用清单与当前坐标有效配置的完整性校验；已启用配置缺失或原类型无法确认时阻断，未启用失效残留降级为 Warning。
- ConfigWindow 清理失效引用后立即保存并重建 WorkingCopy，避免旧副本回写复活已清理空槽位。

### Changed

- 发布 Framework `0.6.6`；纯 Warning 导出改为显式确认后继续。
- `NovaSpark.cs` 固定 Framework `0.6.6`；BestHTTP `0.1.2`、EDM `1.2.188` 与 unity-mcp `v10.1.2` 保持当前最新版本。

## [0.6.5] - 2026-08-04

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.6.5]` 节。

### Breaking

- Network Header Proto 字段统一为 `appid/devid`，Login、Bind 与 GameSave Proto 字段同步统一为 `openid/last_devid`；字段号与 wire 类型保持不变。
- `NetBuilder.BuildHeader` 不再接受候选 OpenID，Header 始终读取已确认身份快照。

### Added

- Framework 新增 UID/OpenID 原子身份读写、清理与非排队身份操作租约，Login、Delete、Bind、Resolve 共用同一并发边界。

### Changed

- 发布 Framework `0.6.5`、BestHTTP `0.1.2`、GameBind `0.0.10`、GameLogin `0.1.4` 与 GameSave `0.1.2`，按实际消费链提升最低依赖。
- `NovaSpark.cs` 固定 Framework `0.6.5` 与 BestHTTP 适配包 `0.1.2`；EDM `1.2.188` 与 unity-mcp `v10.1.2` 保持当前最新版本。

## [0.6.4] - 2026-08-03

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.6.4]` 节。

### Added

- Best HTTP `3.0.18` 与 Best TLS Security `3.0.5` 新增不依赖 Nova 的结构化网络遥测、协议叶子错误与 TLS/OCSP 诊断。
- BestHTTP 适配包 `0.1.1` 自动注册 Nova 遥测接收器，并在 Network Inspector 提供可用性感知的上报开关。

### Changed

- 发布 Framework `0.6.4`、GameLogin `0.1.3`、GameBind `0.0.9`、Apple Sign-In `0.0.11`、Facebook `0.1.3`、Google Sign-In `0.0.12` 与 TGA `0.1.4`，内部依赖下界对齐本轮最新版本。
- `NovaSpark.cs` 固定 Framework `0.6.4` 与 BestHTTP 适配包 `0.1.1`；EDM 与 unity-mcp 保持当前版本。

### Breaking

- 第三方登录提供方、游戏运营渠道与 TGA 用户属性名称统一到新契约。

## [0.6.3] - 2026-08-03

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.6.3]` 节。

### Added

- Framework 的 Localization、Network、Sound、UI 与 Vibrate 专用 Luban 链支持 JSON / Binary 格式选择及对应运行时加载。
- Localization 新增支持语言表导出与运行时行契约。

### Changed

- 发布 Framework `0.6.3` 与 13 个包含同步 Sample 的 Kit / SDK 包；对应包的 Framework 最低依赖统一提升至 `0.6.3`。
- `NovaSpark.cs` 固定 Framework `0.6.3`、unity-mcp `v10.1.2`；BestHTTP `0.1.0` 与 EDM `1.2.188` 保持当前最新版本。

## [0.6.2] - 2026-07-31

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.6.2]` 节。

### Fixed

- 发布 Framework `0.6.2`，修复 ProjectGuard 将 Scene 目录误作配置搜索范围、导致外部项目无法定位 `ConfigRuntimeSO` 的问题。
- 发布 Mobile IAP `0.1.2`，商品信息已成功拉取后不再因商店连接回调重复请求。
- 发布 TGA `0.1.2`，统一等待 SDK 初始化完成后启动数据桥接，避免并发等待同一初始化任务。

### Changed

- `NovaSpark.cs` 固定 Framework `0.6.2`；BestHTTP `0.1.0`、EDM `1.2.188` 与 unity-mcp `v10.1.0` 保持当前最新版本。

## [0.6.1] - 2026-07-31

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.6.1]` 节。

### Added

- 公共网络 Header 增加 OpenID，登录与绑定 Kit 按服务端权威结果统一同步 UID/OpenID。
- SDK MAX 增加 Banner 自动刷新间隔配置，默认 10 秒并限制为 5–120 秒。
- SDK TGA 增加时区与 DeviceId 作为 DistinctId 的可选配置。

### Changed

- 发布 Framework `0.6.1`、GameBind `0.0.7`、GameLogin `0.1.1`，以及 10 个 SDK 新版本。
- SDK 插件 Priority 调整为明确的初始化分桶；Ad 展示回调统一切回 Unity 主线程，收益回调保持即时分发。
- SDK MAX 对齐 AppLovin MAX `8.6.4` 与 adapter 版本矩阵，并依赖 SDK Ad `1.1.1`。
- 安装入口固定命名为 `NovaSpark.cs`，不再在文件名中追加版本号；Framework 固定为 `0.6.1`，BestHTTP 固定为 `0.1.0`。

### Breaking

- `NetService.Uid` / `SetUid` 更名为 `NetService.UID` / `SetUID`；`NetBuilder.BuildHeader` 增加可选 OpenID 参数。
- `TGAPluginConfig.Mode` 与构造参数由 `int` 改为 `TDMode`，并增加 `TDTimeZone`。

## [0.6.0] - 2026-07-29

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.6.0]` 节。

### Added

- 发布 Framework `0.6.0`，新增启动应用 Custom 配置、完整远端 JSON 快照刷新、路径读取 API 与 AppCustomConfig 网络协议。
- Table 工具链新增多 Luban Project、导出描述、加载描述与显式数据资产地址模型。

### Changed

- 发布 BestHTTP `0.1.0`、GameBind `0.0.6`、GameLogin/GameSave `0.1.0`，以及 11 个 SDK 新版本；所有仓内包依赖对齐到本轮最新版本。
- 发布 SDK MAX `0.1.0` 并将 SDK Ad 依赖提升至 `1.1.0`。
- NovaSpark 升级为 `NovaSpark2.13.cs`，固定 Framework `0.6.0`；BestHTTP、EDM 与 unity-mcp 版本保持当前最新值。

### Breaking

- Config 的旧 `CustomConfigs` 与 Table 的旧 Project/Profile/Binding 契约由新 Custom、Project、Export Description、Load Description 模型替换。

## [0.5.46] - 2026-07-24

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.46]` 节。

### Fixed

- 发布 `com.solotopia.nova.framework@0.5.46`，修复 ConfigMaster 新 CDN 配置结构在 npm Sample 中未完整脱敏的问题。
- 发布脚本新增 signed tgz 上传前复检，公开默认配置未全部替换为占位符时禁止 `npm publish`。
- NovaSpark 升级为 `NovaSpark2.12.cs`，固定 Framework `0.5.46`。

## [0.5.45] - 2026-07-24

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.45]` 节。

### Added

- 发布 `com.solotopia.nova.framework@0.5.45`，新增全局占位符、动态 URL 模板、App 更新总开关与 CDN 缓存清理能力。

### Changed

- Config 编辑态与运行态完成分层重构和旧结构迁移，统一 CDN、HybridCLR、YooAsset 的顶层配置与维度覆盖模型。
- NovaSpark 升级为 `NovaSpark2.11.cs`，固定 Framework `0.5.45`；BestHTTP、EDM 与 unity-mcp 版本保持当前最新值。

## [0.5.44] - 2026-07-23

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.44]` 节。

### Added

- 发布 `com.solotopia.nova.framework@0.5.44`，新增 Pipify CDN 部署、全量 Excel 导出、飞书通知、DoH 诊断树与 HostKey 预热能力。
- 发布 IAP `0.0.20`，新增商品工作簿导入、模板导出及编辑器校验能力。

### Changed

- 发布 SDK Ad `1.0.20`、Apple Sign-In `0.0.7`、AppsFlyer `0.0.27`、Google Sign-In `0.0.8` 与 IAP Mobile `0.0.14`，同步包内文档与配置展示说明。
- 公开发布副本对 Pipify 飞书参数及 CDN 顶层、维度覆盖配置执行字段专属占位符脱敏。
- NovaSpark 升级为 `NovaSpark2.10.cs`，固定 Framework `0.5.44`；BestHTTP、EDM 与 unity-mcp 版本保持当前最新值。

## [0.5.43] - 2026-07-23

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.43]` 节。

### Added

- 发布 `com.solotopia.nova.framework@0.5.43` 与首个 `com.solotopia.alibabacloud.oss@0.0.1`，新增阿里云 OSS 上传、Cloudflare 缓存清理及多维 CDN 配置面板。
- 发布 SDK Ad `1.0.19`，补齐聚合收益与单次曝光收益的埋点定义。

### Changed

- 发布 IAP `0.0.19` 与 IAP Mobile `0.0.13`，将模拟支付成功严格限定在 Editor 编译态。
- NovaSpark 升级为 `NovaSpark2.9.cs`，固定 Framework `0.5.43`；BestHTTP、EDM 与 unity-mcp 版本保持当前最新值。

## [0.5.42] - 2026-07-21

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.42]` 节。

### Added

- 发布 `com.solotopia.nova.framework@0.5.42`，新增埋点表聚合、跨插件 OpenID 数据槽与 DoH URL 候选规划。
- 发布 GameLogin `0.0.12` 与 GameBind `0.0.5`，补齐登录、删号、绑定和冲突裁决埋点。

### Changed

- 发布 Apple Sign-In `0.0.6`、Facebook `0.0.9`、Google Sign-In `0.0.7` 与 TGA `0.0.22`，打通 OpenID 用户属性自动同步链路。
- 发布 SDK Ad `1.0.18` 与 MAX `0.0.17`，收敛广告回调线程并新增 Banner ILRD 聚合上报。
- 发布 IAP `0.0.18` 与 IAP Mobile `0.0.12`，避让 registry 中未记录于 Git tag 的 `0.0.17` / `0.0.11`。
- 同步发布 BestHTTP `0.0.13` 与 DataMaster ABTest `0.0.8`。

## [0.5.41] - 2026-07-16

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.41]` 节。

### Added

- 发布 `com.solotopia.nova.framework@0.5.41`，为 PlugPals“已安装”页新增一键批量升级入口，并更新服务中心标题与自适应布局。

## [0.5.40] - 2026-07-16

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.40]` 节。

### Added

- 发布 `com.solotopia.nova.framework@0.5.40`，新增 ProjectGuard 项目结构校验、Play Mode 错误阻断与渐进式接入文档。

### Fixed

- 发布 `besthttp@0.0.12`、`sdk.appsflyer@0.0.26`、`sdk.firebase@0.0.24`，修复 TLS API、AGP 8 Gradle 属性与 Firebase Android namespace 兼容问题。
- 发布 `sdk.max@0.0.16`，更新 ByteDance Android/iOS mediation adapter 最低版本。

## [UPM 2026.07.14-01] - 2026-07-14

### Changed

- 发布 `com.solotopia.nova.framework.sdk.iap@0.0.16`，新增随平台票据往返的 `ReceiptParam`，并收敛补单扫描并发与登录前延迟触发。
- 发布 `com.solotopia.nova.framework.sdk.iap.mobile@0.0.10`，启用 `uid8 + tableId8 + receiptParam16` 透传布局，修复商品拉取、权益刷新、恢复与平台确认流程中的时序问题。
- NovaSpark 升级为 `NovaSpark2.4.cs`，固定 EDM `1.2.188` 与 unity-mcp `v10.1.0`。

## [0.5.38] - 2026-07-13

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.38]` 节。

### Fixed

- 发布 `com.solotopia.unitask@10.0.6`（bundled core 2.5.11），并沿 NovaSpark → Framework → Kit/SDK 的真实安装链提升依赖下界，修复 Unity 6000.5 新工程仍解析旧 Tracker API 而触发 `CS0619` 的问题。
- NovaSpark 升级为 `NovaSpark2.3.cs`，固定 Framework `0.5.38` 与 BestHTTP `0.0.10`，确保新安装入口实际获得兼容版本。

## [0.5.37] - 2026-07-13

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.37]` 节。

主要内容：
- 主包 framework 升级到 0.5.37：移除旧 Spine 槽位换肤与动画时长查询的四个公开扩展入口；Pipify 统一 Android keystore 与 key alias 命名，并让 Split Application Binary 可独立生效；PlugPals 不再把带 scoped registry 包的第三方传递依赖展开到项目顶层 manifest。
- 随 framework 依赖下界联动发布 Network Kit：`kit.network.gamebind@0.0.3`、`kit.network.gamelogin@0.0.10`、`kit.network.gamesave@0.0.17`。
- 随 framework 依赖下界联动发布 SDK：`sdk.ad@1.0.16`、`sdk.applesignin@0.0.4`、`sdk.appsflyer@0.0.24`、`sdk.facebook@0.0.7`、`sdk.firebase@0.0.22`、`sdk.googlesignin@0.0.5`、`sdk.iap@0.0.14`、`sdk.iap.mobile@0.0.8`、`sdk.tga@0.0.20`。
- 同轮发布 `besthttp@0.0.9`、`sdk.aihelp@0.0.1`、`sdk.datamaster.abtest@0.0.5`、`sdk.max@0.0.14`。

## [0.5.34] - 2026-07-03

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.34]` 节。

主要内容：
- 主包 framework 升级到 0.5.34：`NOVA_NICEVIBRATIONS` / `NOVA_SIMPLEDISKUTILS` 宏定义从 `package.json` 的 `nova.requiredLibraries.defineSymbols` 迁移至 `NovaFramework.Runtime.asmdef` 的 `versionDefines`（装包自动跨平台定义，ADR-064 后半步）；删除 `NOVA_UNIWEBVIEW` / `NOVA_WEBGLSUPPORT` 死宏声明（孤儿宏根除，全仓无 `#if` 引用）；Debug `Settings.asset` 默认 `_isEnabled` 置为 0。

## [0.5.33] - 2026-07-03

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.33]` 节。

主要内容：
- 主包 framework 升级到 0.5.33：`NetResponse<T>` 新增携带业务数据的失败工厂重载、`NetService` 业务错误体解析（ADR-068）；BuildProcessor `UnityManifest.xml` 模板路径三级定位重构；`WorkspaceActive.GetActiveRuntime` 增强 ExportTarget 优先定位（ADR-047）。
- 子包 `com.solotopia.nova.framework.kit.network.gamebind` 首次发布 0.0.1：账号绑定业务网络模块（ADR-067）。
- 子包 `com.solotopia.nova.framework.kit.network.gamelogin` 升级到 0.0.8。
- 子包 `com.solotopia.nova.framework.kit.network.gamesave` 升级到 0.0.15：`Save.GetFullAsync(targetUid)` 跨用户存档查询（ADR-069）。

## [0.5.26] - 2026-06-15

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.26]` 节。

主要内容：
- 主包 framework 升级到 0.5.26：Asset 远端清单不可达时回退使用当前已激活清单或内置清单，PlugPals 缺失依赖检测跳过 Unity 包与已注册 scope。
- 子包 `com.solotopia.nova.framework.besthttp` 升级到 0.0.3：刷新 BestHTTP Runtime asmdef 配置。
- 子包 `com.solotopia.nova.framework.sdk.ad` 升级到 1.0.9：AdDemo sample 字体资源随包发布同步刷新。
- 子包 `com.solotopia.nova.framework.sdk.iap` 升级到 0.0.9：IAPDemo sample 字体资源随包发布同步刷新。

## [0.5.25] - 2026-06-15

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.25]` 节。

主要内容：
- 主包 framework 升级到 0.5.25：进一步移除 PlugPals 安装按钮同步路径上的远端 package metadata 拉取，避免点击安装/升级时阻塞 Unity 主线程。

## [0.5.24] - 2026-06-15

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.24]` 节。

主要内容：
- 主包 framework 升级到 0.5.24：修复 PlugPalsWindow 点击安装/升级后同步触发 UPM Resolve 导致消费端 Unity 卡顿/无响应的问题，改为下一帧合并解析。
- 子包 `com.solotopia.nova.framework.besthttp` 升级到 0.0.2：同步更新包级授权文件。

## [0.5.23] - 2026-06-15

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.23]` 节。

主要内容：
- 主包 framework 升级到 0.5.23：补发 `upm-release-2026.06.10-01` 后累计变更，覆盖 BestHTTP 可选后端解耦、内部云仓库展示、缺失依赖提示与 Util.Json 依赖迁移等内容。
- 同步补发所有 eligible 且有变更的 UPM 子包；禁发名单内包仍保持不发布。

## [0.5.22] - 2026-06-10

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.22]` 节。

主要内容：
- 主包 framework 升级到 0.5.22：Network / DoH 链路增强，新增预热后自动缓存 IP 直连与更清晰的模块文档说明；ConfigWindow 的 Luban 相关提示文案同步收敛。
- MainDemo 示例场景随主包同步刷新。

## [0.5.21] - 2026-06-09

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.21]` 节。

主要内容：
- 主包 framework 升级到 0.5.21：新增场景级 `DevelopMode` 序列化回写与 Inspector 只读彩色展示；App / Asset 改为使用节点本地开发模式在 Debug / Release 主备地址间路由；版本检查增加主备地址失败回退并在双路都不可用时返回 `NoDownload`。
- 子包 `kit.network.gamelogin@0.0.2`、`kit.network.gamesave@0.0.9`、`sdk.appsflyer@0.0.16`、`sdk.firebase@0.0.14`、`sdk.tga@0.0.14`：同步收敛配置提示文案与包内文档说明。
- 子包 `sdk.ad@1.0.6`、`sdk.admob@0.0.3`、`sdk.iap@0.0.6`、`sdk.iap.mobile@0.0.2`、`sdk.max@0.0.6`：刷新包内文档索引与使用说明。

## [0.5.20] - 2026-06-05

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.20]` 节。

主要内容：
- 主包 framework 升级到 0.5.20：优化启动流程，Splash / 进度面板的销毁时机由启动流程内部移交业务入口统一回收，避免首屏衔接闪帧；同步刷新框架 L0/L1/L2 文档与代码注释。

---

## [0.5.17] - 2026-06-01

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.17]` 节。

主要内容：
- 主包 framework 升级到 0.5.17：新增 Kit 配置体系（`IKitConfig` + ConfigWindow「Kit 配置」面板 + `Nova.Config.GetKitConfig<T>()`），Asset 模块支持启动期按 tag 切片预热、运行期刷新清单与缓存治理接口，Hotfix 阶段新增热更提示弹窗与完成后自动清缓存开关。
- 子包 `kit.network.gamesave` 升级到 0.0.5：存取接口收口为 6 个零 cmd 参数极简入口，指令名由 ConfigWindow「Kit 配置」统一管理（新增 `GameSaveKitConfig`）。
- 子包 `kit.network.login` 升级到 0.0.16：登录入口简化为只传 `openId`，`Async` 签名新增首位 `uid` 参数，指令名与渠道由 ConfigWindow「Kit 配置」统一管理（新增 `LoginKitConfig`）。

---

## [0.5.16] - 2026-05-29

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.16]` 节。

主要内容：
- 主包 framework 升级到 0.5.16：把对 `com.solotopia.yooasset` 的依赖从 `1.0.0` 提升到当前 Verdaccio 最新版 `1.0.3`，框架默认随附最新资源系统封装层。

---

## [0.5.15] - 2026-05-29

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.15]` 节。

主要内容：
- 主包 framework 升级到 0.5.15、登录 Kit 升级到 0.0.15：两包均通过 `.npmignore` 排除 `nova-samples.json` 与 `.meta`，发版描述符仅留开发期源工程使用，不再随 npm tarball 落到外部工程的只读 `Packages/<pkg>/` 区，消除 `immutable folder` 警告与 `SamplePathRewriter` 的重复 RunRewrite。

---

## [0.5.14] - 2026-05-29

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.14]` 节。

主要内容：
- 主包 framework 升级到 0.5.14：发版流水线 `publish_packages.py` 重构为统一流水线——主包 `MainDemo` 与所有子包 sample 走完全对称的 Stage 1 / Stage 3，删除主包专属分支，新增 `Assets/Framework/nova-samples.json` 描述符，所有 sample 等量复制 `Docs/Excels` + `Docs/Protos`、注入 `Nova.prefab` 的 `*SourceDirPath` PrefabInstance override、写入 `SamplePathManifest`；脚本对 `devPathPrefix` 末尾斜杠做防御性 `rstrip("/")` 归一。
- 登录 Kit（kit.network.login@0.0.14）：修正源 `nova-samples.json` 的 `devPathPrefix` 末尾斜杠 bug，外部工程 import LoginDemo 后 `SamplePathRewriter` 不再静默放弃路径重写，sample scene 与 SO 路径正确指向 import 后真实根目录。

---

## [0.5.13] - 2026-05-29

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.13]` 节。

主要内容：
- 主包 framework 升级到 0.5.13：`WorkspaceActive` 增加多 sample 切换感知——当前活跃 scene 在 `Assets/Samples/<sampleRoot>/` 下且与 `Globals.json` 缓存的 ConfigMaster 所在 sample 根不一致时，自动按 scene 重新推断 ConfigMaster 并覆盖 `Globals.json`，根除外部工程同时 import 多个 sample 时打开次级 sample 却读到首个 sample 配置的玄学。
- 登录 Kit（kit.network.login@0.0.13）：发版脚本与主包对称——为子包 sample 复制 `Docs/Excels` + `Docs/Protos` 副本，注入 `Nova.prefab` 的 `*SourceDirPath` PrefabInstance override 到 sample scene；修正 `sampleManifestRelative` 路径错配；`package.json` 备份从包目录移到系统临时目录，消除外部工程 Console 持续刷的 `package.json.publish.bak` immutable folder 报错。

---

## [0.5.12] - 2026-05-29

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.12]` 节。

主要内容：
- 主包 framework 升级到 0.5.12：修复 `WorkspaceActive` 的 sample scene 路径推断，外部工程 import sample 后打开 ConfigWindow 不再提示"未检测到激活的 ConfigMaster"——从 scene 所在目录起逐级向上递归扫 `Editor/ConfigMaster.asset`，同一逻辑兼容开发态扁平结构与 UPM 导入态三段嵌套结构。
- 登录 Kit（kit.network.login@0.0.12）：修正 `nova-samples.json` 的 `displayName` 为 `LoginDemo`（原为带空格的 `Login Demos`），与 `sampleName` / `sourceDir` 末段对齐，确保 Unity Package Manager 落盘的最末层文件夹名与开发态一致。

---

## [0.5.11] - 2026-05-29

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.11]` 节。

主要内容：
- 主包 framework 升级到 0.5.11：ConfigMaster 新增 `YooAssetSettingsPath` / `BundleCollectorSettingPath` 显式路径字段，YooAsset 全局设置与 Bundle 收集器加载改由 ConfigWindow 配置驱动，避开 Editor 启动期 Resources 多副本与全工程 `AssetDatabase.FindAssets` 扫描；ConfigMasterSO 仅 Editor 期消费的字段补齐 `#if UNITY_EDITOR` 包围。
- yooasset 升级到 1.0.3：暴露 `YooAssetConfiguration.SetSettings`（internal）注入点与 `SettingLoader.LoadSettingDataAtPath<T>` 按路径加载重载，配合主包路径注入链路。
- 登录 Kit（kit.network.login@0.0.10）：新增 `nova-samples.json` 元数据，声明 `LoginDemo` 示例工程，预留 `/nova-create-sample` 与发版流水自动化能力。

---

## [0.5.10] - 2026-05-28

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.10]` 节。

主要内容：
- 主包 framework 升级到 0.5.10：清理 `Assets/Framework/Tests/` 临时回归脚本、SDKInspector 插件条目绘制层调整、`Nova.Visitors.Version` 同步、`Assets/Samples/MainDemo/` 资源刷新。
- gamesave Kit 升级到 0.0.4：上传请求新增 `GameVersion / AppVersion / LastDeviceId` 三项元数据自动填充，新增 `SetGameVersion` 实例方法供业务侧注入；拉取响应新增最近一次存档版本 / 设备 / 时间戳元数据。
- sdk.ad 升级到 1.0.3：调整 UPM displayName 为 "Nova Framework - SDK - AD"，与其它 SDK 子包命名风格统一。

---

## [0.5.9] - 2026-05-27

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.9]` 节。

主要内容：
- 声音 / 振动模块新增「按名称播放」入口，业务侧可直接传数据表 Name 字段触发播放。
- Editor 新增 PlugPals 私有 Verdaccio 仓库 UPM 包管理工具，支持远程包列表、安装/卸载与按版本查看更新日志。
- 网络 Kit（kit.network@0.0.10）：响应公共头新增 `app_id` 与 `uid` 字段，便于多产品/多账号场景识别。
- 登录 Kit（kit.network.login@0.0.9）：登录接口新增「按命令名调用」重载，并修复 `openId` 为 null 时底层异常。
- 存档 Kit（kit.network.gamesave@0.0.3）：拆分全量与非全量接口为独立入口，并新增「按命令名调用」重载（破坏性：旧版"keys 为空即全量"隐式回退取消）。

---

## [0.5.8] - 2026-05-27

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.8]` 节。

主要内容：
- UI 视图新增「对象池开关」，可按视图选择关闭后缓存还是直接销毁（破坏性：UIView 子类覆写 OnInit 需补回参数）。
- UIView 默认不再带淡入淡出，淡入淡出能力移交业务自行实现。
- Asset / UI 模块 L2 文档同步刷新，强化 LoadXxx 必须经 Handle 释放铁律。
- Vault 沉淀本期 UI 深度因子、Asset Load API 等决策与通用模式。

---

## [0.5.7] - 2026-05-26

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.7]` 节与各 UPM 子包 CHANGELOG。

主要内容：
- 启动期 UI 改为多语言驱动，弹窗显示接口签名简化（破坏性）。
- 启动期新增独立本地化能力，可在资源系统就绪前安全使用本地化文本。
- 网络 / 登录 / 数据上报三个 UPM 子包跟版升级。

---

## [0.5.6] - 2026-05-22

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.6]` 节与各 UPM 子包 CHANGELOG。

主要内容：
- 网络 / 声音模块对外接口与 DTO 调整。
- MainDemo 演示工程切换为基于 Nova.UI 的树形导航 + TMP 文字渲染。
- 7 个 UPM 子包跟版升级。
- Vault 沉淀本期演示拓扑、UI 命名、Demo 覆盖标准、prefab 制作等多条规范。

---

## [0.5.5] - 2026-05-22

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.5]` 节。

主要内容：
- 修复外部工程导入 sample 后 Inspector 业务字段为空与 SDK `[Missing]` 提示。
- 修复多版本 sample 共存时新旧版本识别不准的问题。

---

## [0.5.4] - 2026-05-22

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.4]` 节。

主要内容：
- 放弃此前的桥接式生命周期方案，回归 HybridCLR 原生 MonoBehaviour + Prefab 直挂。
- 发版流程支持把项目根 Docs 资源（表格 / 协议）随 sample 一起打包，外部工程导入后 Inspector 路径自动对齐。
- HybridCLR 约束规则全面重写为原生方案口径。

---

## [0.5.3] - 2026-05-21

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.3]` 节。

主要内容：
- 修复 0.5.2 演示工程改名后命名空间 / 配置残留导致的启动报错。

---

## [0.5.2] - 2026-05-21

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.2]` 节。

主要内容：
- 演示工程更名 Demo → MainDemo，目录 / asmdef / 命名空间 / 子框架脚手架同步对齐。
- qa 测试改为按需就近建测试脚本，不再依赖固定测试入口。

---

## [0.5.1] - 2026-05-21

主要内容：
- 包内结构调整与冗余资源优化。

---

## [0.5.0] - 2026-05-21

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.0]` 节。

主要内容：
- 接入 UPM 标准 Samples 机制，演示工程改作 sample 分发；导入后自动检测旧版本残留并询问设置启动场景。
- 各 UPM 包补齐 CHANGELOG / LICENSE / README 三件套，发版脚本强制校验。
- 主框架版本 0.4.2 → 0.5.0。
- 发布工具沉淀为 Claude Code skill。
- 废除 bootstrap.zip 机制。
