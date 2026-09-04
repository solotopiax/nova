# Changelog — com.solotopia.nova.framework

## [Unreleased]

## [0.6.26] - 2026-09-04

### Breaking

- 删除未投入使用的 AssetBundle 解密占位契约：`AssetDecryptorType`、`OffsetBundleDecryptor`、`AssetManagerConfig.DecryptorType` 与 `AssetComponent.m_DecryptorType`。
- 删除 `AssetPlayMode.WebPlayMode`；WebGL 继续使用 `HostPlayMode`，底层文件系统由框架按平台自动选择。
- `IUIManager` 新增 `OnOpenUIViewFail` 事件；仓外自定义实现需要补齐该成员。

### Added

- WebGL HostPlayMode 增加远端 Bundle 单次物理请求总超时，并在远端 Manifest 候选耗尽后回退随 Player 发布的首包元数据；Bundle 仍保持常规远端按需加载。
- BuildReadiness 增加场景渠道快照校验；Asset/App 渠道与已导出的 ConfigRuntime 不一致时阻断 Nova Player 构建流程，并提示重新导出和保存场景。

### Changed

- Asset Inspector 按平台互斥编辑 WebGL Bundle 总超时与非 WebGL 单文件字节流入超时，并补充启动必须资源与首包 Tag 一致性的提示。

## [0.6.25] - 2026-09-02

### Added

- 新增框架级 `Nova.InstallTimeMs`，在首次启动时持久化 13 位 UTC Unix 毫秒时间戳。
- 新增最小化 Editor 升级迁移器：消费项目升级 Framework 后自动从 manifest 移除当前及历史 BestHTTP/TLS 包与 testables，并触发 UPM Resolve；TGA 上游 DoH 不受影响。

### Breaking

- 删除 DoH Manager、配置、Inspector 与公共接口；Framework HTTP 固定使用 UnityWebRequest 和系统 DNS。
- 删除可替换 HTTP transport SPI、独立连接超时参数及相关网络遥测扩展点；仓外自定义实现需要迁移到现行 HTTP 契约。

### Changed

- App 版本检查、Asset 热更新与 `HostKey + NetCmd` 业务协议分别使用各自的 UnityWebRequest 主备地址链；业务链仅在未取得正式 HTTP 响应时切换备用域名。
- 三条链统一复用主备候选规划、轮次/重试坐标与最近成功域名偏好；最大物理发送数为 `C × R × (K + 1)`，每个物理请求使用完整的模块超时，并统一上报 schema 1 的 `uwr_request_start/error/end`。

### Removed

- 删除 Nova HTTP 适配子包及其第三方 HTTP/TLS 依赖、Samples AOT/link 配置、Player define 和安装入口注入逻辑。

## [0.6.24] - 2026-08-31

### Fixed

- 修复跨 App 版本覆盖安装后本地可启动 Manifest 因 `PackageFilePrefix` 变化无法命中，并兼容迁移旧版纯版本号记录。
- 修复 `PackageFilePrefix` 非空时启动白名单元数据重试次数计算错误、可能漏掉常规 CDN 候选的问题。
- 修复多个 prefix 分支共用远端目录时，部署前清理会误删其他 App 分支的问题；清理范围收紧为本次上传计划的精确对象。
- 修复 `PackageFilePrefix` 使用 `{Time}` 时，CDN 部署按新时间重算前缀而找不到构建产物的问题。
- 修复远端失败后仅凭 Manifest 有效便复用当前版本、未复核启动资源范围完整的问题。
- 修复内置 Manifest 回退最终停留在 `OfflinePlayMode`、导致 Sandbox 资源失去归属的问题；回退完成后恢复 `HostPlayMode`。

## [0.6.23] - 2026-08-29

### Fixed

- 启动白名单元数据路由按 Player 有效 `YooAssetSettings.PackageFilePrefix` 识别 `.version/.hash/.bytes`。

## [0.6.22] - 2026-08-28

### Breaking

- `IAssetManager` / `AssetManagerBase` 新增 `HasPatchByTagsAsync`；仓外自定义资源 Manager 实现需要补齐该成员。

### Fixed

- 启动期补丁检查与 `LaunchHotfixTags` 实际下载范围保持一致；空下载器不再显示 0% 热更进度后跳入 `ProcedureLoadDll`。

## [0.6.21] - 2026-08-28

### Changed

- PlugPals 改为以 Unity 实际注册包及 `resolvedPath/package.json` 判断安装状态，并为声明后等待解析与解析失败提供独立状态和操作门禁。
- ConfigWindow 支持独立切换编辑平台；平台与 Unity Active BuildTarget 不一致时仍可编辑和保存，但禁止 ConfigRuntime 导出及 YooAsset 配置生效，CDN 操作明确使用当前编辑平台。
- README 补充 Unity 版本、渲染管线、脚本后端与核心依赖等运行环境说明。

## [0.6.20] - 2026-08-27

### Breaking

- `ConfigMasterSO.CurrentPlatform` 从可写字段调整为只读属性，始终实时映射 Unity Active BuildTarget；旧资产通过 `FormerlySerializedAs` 兼容，但外部直接赋值代码需要改用 BuildTarget 切换流程。

### Added

- 本地化文本增加 Arabic、Persian、Hebrew RTL 方向识别，并通过 TMP `ITextPreprocessor` 接入 MIT 许可的 Arabic shaping 实现。
- App 推荐更新增加用户放弃时间记录与远端提示间隔控制。
- 新增统一的 Editor Active Platform 解析、校验与切换工具。

### Changed

- 启动期与运行时统一使用同一套语言解析策略，并修复异步切换语言及字体加载的竞态。
- 所有随包发布的 Demo 将 Localization 初始化前移到 Network 之前。

### Fixed

- Table 的内置 Luban 导出描述与 canonical `Nova.prefab` 改用 Framework UPM 逻辑模板路径，Sample 导入消费工程后 Inspector 不再显示开发仓的 `Assets/Framework/Templates/Luban`；旧配置仍可正常导出。

## [0.6.19] - 2026-08-27

### Fixed

- Sample 覆盖导入会重新校验全部路径目标，等待尚未落盘的资产后再写完成标记；发版阶段拒绝生成空重写清单，避免升级后继续保留旧版本路径。
- Sample 场景会把配对的 ConfigMaster 与 PipifySettings 原子写入 `Globals.json`，同时保存进入前的业务绑定；切回业务场景时成对恢复，Pipify Window/CLI 与构建只读入口不再按首个资产猜测。
- ConfigWindow 在未绑定引导页选择或新建 ConfigMaster 后正确重建 WorkingCopy，后续编辑与保存不再失效。

## [0.6.18] - 2026-08-26

### Added

- 新增 Agent Capabilities 只读总览，聚合 Nova Skills、Project Action Registry 与 MCP 暴露状态。
- SDK 构建处理器新增 Android、iOS 与 WebGL 的 Nova 前后置完成钩子。

### Changed

- Nova 菜单中的组合词统一使用空格分隔，并将 Project Guard 窗口切换为统一 IMGUI 绘制。
- Framework 最低 MCP 依赖提升至 `0.1.2`，确保能力总览可读取 MCP Action 暴露快照。

## [0.6.17] - 2026-08-21

### Breaking

- `IAdPlugin` 新增 `IsUserConsentSet()`、`HasUserConsent()` 与 `WaitForPrivacyFlowAsync()`；仓外自定义实现需要补齐成员。

### Added

- 新增表、网络、声音、振动与本地化五类受控导出 Action，并补齐对应 Nova Project Skills、契约与 MCP 暴露。

### Changed

- Framework 最低 MCP 依赖提升至 `0.1.1`，确保新增导出 Action 可由默认 Provider 调用。
- CheckUpdateWindow 的升级版本提示色调循环速度与 PlugPals 保持一致。

## [0.6.16] - 2026-08-20

### Breaking

- HybridCLR 配置与生成结果中的 `GameDlls` 字段更名为 `StartupGameDlls`；序列化资产通过 `FormerlySerializedAs` 兼容，直接访问旧字段的外部 C# 代码需同步改名。

### Added

- 新增并完善 29 个 `nova-project-*` 消费端 Skills，以及可审计的 Nova Project Action 执行层。
- Framework 显式依赖 `com.solotopia.nova.framework.mcp@0.1.0`，为项目组提供默认 MCP Action 桥接。
- 新增统一 Sample `SceneRoute`、构建前检查、Android 依赖解析与 HybridCLR/Bundle/Player 构建 Action。

### Changed

- HybridCLR 区分启动 DLL 与运行时按需 DLL，Config 三维配置、Kit 导出和 MainDemo 同步适配。
- Sample 场景配置注入改由 Framework 统一编排，移除各 Sample 重复的 `SampleSceneAutoFix`。

## [0.6.15] - 2026-08-18

### Changed

- 收录本轮已合入的 YooAsset RawFile 构建、资源版本检查与配置投影更新；MainDemo 的本地版本检查文件改为 `ProjectSettings/Nova/AppDownloadRules.json`。
- Framework 最低配套包统一解析至 HybridCLR `10.1.0`、Luban `10.1.0` 与 YooAsset `1.1.0`，Sample ConfigMaster 同步补齐新增配置字段的序列化默认值。

## [0.6.14] - 2026-08-17

### Breaking

- `INativeManager` / `NativeManagerBase` 新增 `RequestInAppReviewAsync(CancellationToken)`；自定义 Native Manager 实现需补齐该成员。

### Added

- `Nova.Native.RequestInAppReviewAsync()` 提供 Android / iOS 的轻量系统应用内评价请求桥接。`RequestDispatched` 只表示请求已交给系统，不表示系统提示展示、用户评价或提交完成。

## [0.6.13] - 2026-08-14

### Added

- `HostKey + NetCmd` 业务主备/IP 轮换接入可选整链遥测扩展点，允许 BestHTTP 适配包用同一个 `best_http_chain_id` 收口一次 attempt、零到多次 error 和一次最终 end。

### Changed

- App 版本 JSON 在主地址请求失败、内容为空或规则无效时继续尝试备用地址；YooAsset 版本与 Manifest 元数据在常规启动路径同样按原有 URL 策略轮换主备地址。

### Fixed

- DoH HTTPS 请求恢复系统证书校验，不再无条件接受服务端证书。

## [0.6.12] - 2026-08-14

### Breaking

- 网络公共 `INetworkManager` / `IDoHManager` 契约新增主备 URL 解析与运行时 DoH 开关方法；自定义实现需重新编译并补齐实现。

### Added

- 业务网络支持 HostKey 主备域名、DoH IPv4 候选与保留原域名 Host/SNI 的指定连接 IP 路由，并补充 HTTP 到达状态诊断。
- Nova 安装或升级后自动将包内项目组 Skill 投影到项目根 `.agents/skills/`，对用户文件和受管文件冲突采取保守保留策略。

### Changed

- 网络请求、DoH、资源启动链、配置窗口与 ProjectGuard 文档和实现同步本轮契约；DoH 不具备安全 IP 注入能力时运行期自动回退系统 DNS。

## [0.6.11] - 2026-08-13

### Changed

- AES 默认凭据缺失、无效或显式 Key/IV 未成对传入时，统一输出隐私配置路径与当前坐标的配置指引。
- 启用 AES 的 Persist 存储实现在 `Nova.Persist.LoadAsync()` 初始化前确认默认凭据已就绪；标准启动顺序为先 `await Nova.Config.LoadAsync()`，再加载 Persist。
- Persist 代码、Inspector 与文档统一使用“存储实现”术语；Framework 不再强制依赖 Alibaba Cloud OSS Runtime 包，CDN 面板在缺少可选 OSS Editor 工具包时提供安装引导。

## [0.6.10] - 2026-08-13

### Breaking

- 网络请求不再提供明文调试旁路；移除 `NetService`、`NetworkComponent` 的调试开关和四参发送入口，调用方需重新编译并统一使用标准加密链路。

### Added

- 隐私配置新增 AES 默认 Key/IV，供框架加解密初始化与 Persist 文件分片加密使用。

### Changed

- Persist Inspector 按当前 ConfigMaster 坐标读取 AES 配置；MainDemo AES 示例改为使用已初始化的默认密钥。

## [0.6.9] - 2026-08-12

### Breaking

- `IIAPResult` 新增 `ErrorSource`、`ErrorDesc` 与 `OrderId`；自定义支付结果实现需同步补齐这些契约字段。

### Added

- 新增 `Nova.Native.OpenNotificationSettingsAsync()`：Android API 26+ 与 iOS 15.4+ 可精准打开当前应用的系统通知设置；不支持或无法精准跳转时返回 `false`，不降级到应用设置页。
- 已安装 SDK 可在 `Nova/Open SDK URL` 下直接打开官方 Console 与 Readme。

### Fixed

- Pipify 与构建链会临时对齐并恢复 Development Build 状态，并校验 HybridCLR `MethodBridge.cpp` 的 `DEVELOPMENT` 标记，避免实际 Player ABI 与构建参数不一致。

## [0.6.8] - 2026-08-07

### Added

- 新增 Native 通知权限模块，提供 Android / iOS 权限状态查询、显式请求、系统设置跳转、平台回调并发治理与对应 Inspector、构建处理器及 MainDemo 页面。
- CDN 部署新增可选远端清理、最新 YooAsset 完整版本自动关联与白名单版本文件解析能力。
- Player Build 期间按当前 `ConfigMasterSO` 临时生成唯一 `Resources/YooAssetSettings.asset`，构建后对称清理，避免多 Sample 永久副本冲突。

### Changed

- YooAsset 版本请求改用 `CheckTimeout` 总超时，文件下载通过 `IdleTimeout` 配置字节流入 watchdog。
- YooAsset 编辑配置新增输出目录名与包文件前缀，ConfigWindow、Pipify 和导出链同步支持。

## [0.6.7] - 2026-08-06

### Breaking

- `IAssetManager` / `AssetManagerBase` 新增 `SaveAssetCheckDeviceId` 与 `CommitBootableVersion` 契约；自定义 Asset Manager 实现需同步补齐两个方法。

### Added

- 资源启动链新增设备白名单、独立元数据根地址、可启动版本记录与请求候选 URL 轮换策略。
- Asset Inspector 与 Unity 菜单新增本地热更资源缓存清理入口。
- CDN 工具链新增启动白名单文件生成、独立上传与多维配置支持。

### Changed

- PlugPals registry 加载改为并发请求与渐进展示，并保留显式空 URL 的配置语义。
- 默认 `Nova.prefab` 关闭热更与启动白名单，Editor / Runtime 资源模式分别设为 EditorSimulate / Offline。

## [0.6.6] - 2026-08-05

### Fixed

- Config 导出校验可从 Unity 缺失托管引用元数据中恢复无歧义的 SDK / Kit 原类型；已启用但缺少有效配置时阻断导出，未启用的失效残留仅警告，无法确认启用状态时保守阻断。
- ConfigWindow 清理失效 SDK / Kit 引用后立即保存真实配置并重建 WorkingCopy，避免旧副本在后续保存时把空槽位重新写回。

### Changed

- 仅有 Warning 的导出校验结果改为显式展示，由用户选择继续导出或取消，不再静默忽略。

## [0.6.5] - 2026-08-04

### Breaking

- Network Header Proto 字段 `app_id/device_id` 改为 `appid/devid`，生成属性改为 `Appid/Devid`；字段号与 wire 类型不变，不保留旧属性别名。

### Added

- `NetService` 新增 UID/OpenID 原子读写与清理 API，以及 Login/Delete/Bind/Resolve 共用的非排队身份操作租约。

## [0.6.4] - 2026-08-03

### Breaking

- 第三方登录提供方常量名与底层数据槽 key 统一为 `SDKDataKeys.ThirdLoginProvider` / `"ThirdLoginProvider"`；相关 SDK 发版时需级联依赖同一最新 Framework 版本，确保跨程序集常量一致。
- `PbNetChannel` 恢复为游戏运营渠道协议枚举，与 `ChannelType` 的 `Official / Google / Apple / WeChat / TikTok / Alipay` 同名同值；原错误定义的 Facebook 登录类型不再属于 Header 渠道。

### Added

- Network Inspector 新增 BestHTTP 网络埋点开关；仅在 BestHTTP 适配包安装时可用，其他情况下灰置。

### Changed

- Network 运行时暴露 BestHTTP 埋点开关，供适配包启动注册后实时读取。

## [0.6.3] - 2026-08-03

### Added

- Luban 专用数据导出链新增 JSON / Binary 格式选择，运行时可按统一配置加载对应数据。
- Localization 新增支持语言表导出与运行时行契约，补齐多语言数据闭环。

### Changed

- Network、Localization、Sound、UI 与 Vibrate 的 Inspector、Pipify、模板和运行时加载统一传递数据格式。
- 各模块切换格式时会登记并清理同名反格式产物，避免旧 JSON / Binary 文件混入构建。

## [0.6.2] - 2026-07-31

### Fixed

- ProjectGuard 启动配置校验改用 `WorkspaceActive.Get()` 锚定当前 `ConfigMasterSO`，并通过 `ExportTarget` 精确定位 `ConfigRuntimeSO`；配置资产可位于 Scene 目录之外。

## [0.6.1] - 2026-07-31

### Added

- 公共请求与响应 Header 新增 `openid` 字段；`NetBuilder.BuildHeader` 自动填充当前请求身份，UID/OpenID 由业务 Kit 按权威结果同步。

### Breaking

- `NetService.Uid` / `SetUid` 更名为 `NetService.UID` / `SetUID`。

## [0.6.0] - 2026-07-29

### Added

- 新增启动应用 Custom 配置：本地默认值与磁盘快照先完成启动，随后非阻塞等待 Network 路由就绪并单次拉取完整远端 JSON。
- 新增 `Nova.Config.Custom` 路径读取与 `RefreshAppConfigAsync()` 手动刷新入口，并加入 `AppCustomConfig` 网络协议和 Demo 配置。
- Table 编辑与运行链新增正式 Luban Project、导出描述、加载描述及数据文件到 YooAsset 地址的显式映射。

### Changed

- Table 从单 Project、Profile 与 Runtime Binding 模型迁移为多 Project、Export Description 与 Load Description 模型。
- Runtime 日志统一使用 Editor 条件编译隔离，Editor 绘制工具补齐折叠、布局、状态与属性行能力。
- 所有配套 Sample 同步应用配置网络命令、运行时配置和场景覆盖。

### Breaking

- `CustomConfigs` 契约替换为 `CustomConfigData` 与 `CustomConfig`，公开入口由 `CustomConfigs` 更名为 `Custom`。
- Table 的 `TableProjectSettings`、`TableExportProfileSetting`、`TableRuntimeBindingSetting` 及相关 Profile API 替换为新的 Project、Description 与 Asset Address 契约。

## [0.5.46] - 2026-07-24

### Fixed

- 修复 ConfigMaster 重构后 `CDNEditorConfigs` 与维度覆盖未被 npm 发布链路脱敏的问题；公开 Sample 中的 OSS、Cloudflare 和部署路径配置全部替换为字段专属占位符。
- signed tgz 在执行 `npm publish` 前新增公开默认配置复检，发现任何待脱敏字段时立即阻止上传。

## [0.5.45] - 2026-07-24

### Added

- 新增全局占位符解析能力，统一支持 `{Platform}`、`{Channel}`、`{Package}`、`{Version}` 与 24 小时制 `{Time}`（`yyyy-MM-dd-HH-mm-ss`），并分别从 Editor ConfigMaster 与 Runtime ConfigRuntime 构造上下文。
- App 更新检查与资源远端地址支持动态 URL 模板；App 更新新增独立总开关。
- CDN 流水线新增缓存清理参数与步骤，并补齐 Pipify 飞书通知、CDN 路径的占位符帮助说明。

### Changed

- Config 编辑态与运行态契约重构：`ConfigMasterSO` 收敛到 Editor，运行态按 `AppConfigs`、`HybridConfigs`、`CustomConfigs` 分层导出，并新增旧配置结构迁移。
- CDN、HybridCLR、YooAsset 编辑配置统一为顶层配置加维度覆盖结构，ConfigWindow、Inspector、导出与校验链路同步更新。

### Breaking

- `CommonConfig` / `IConfigManager.Common` 更名为 `AppConfigs`，HybridCLR 运行参数改由 `HybridConfigs` 提供；依赖旧配置接口的业务代码需要迁移。

## [0.5.44] - 2026-07-23

### Added

- Pipify 新增 CDN 部署、全量 Excel 导出与飞书通知步骤，并支持使用配置维度解析后的本地和远端路径。
- DoH 新增 HostKey 全量预热和 CNAME 解析诊断树，可在 Network Inspector 中查看每层解析来源、地址与失败原因。
- 资源远端地址模板新增 `{Channel}` 占位符，Config 导出时会把当前渠道同步为启动期快照。

### Changed

- CDN 配置改用 Cloudflare Zone ID 生成清理请求，并完善顶层配置与维度覆盖的投影和迁移行为。
- HybridCLR 与 YooAsset 的维度覆盖允许用空值明确覆盖顶层配置，不再把空值自动解释为回退。
- 公开发布副本会将 Pipify 飞书参数及 CDN 顶层、维度覆盖中的敏感字段统一替换为字段专属占位符。

## [0.5.43] - 2026-07-23

### Added

- 新增 CDN 内容部署面板，支持按配置维度将资源批量上传到阿里云 OSS，并分批触发 Cloudflare 缓存清理。
- 新增 `com.solotopia.alibabacloud.oss@0.0.1` 依赖及 Unity Runtime 封装，为 Editor 部署流程提供签名上传能力。
- 新增埋点工作簿聚合与定义文档生成能力，可从 Framework 和各 UPM 包统一收集 `Tracks.xlsx`。

### Changed

- ConfigWindow 接入 CDN 整套配置快照与维度投影，敏感字段使用密码输入框展示且不进入 Runtime 配置导出。
- 补齐环境检测、Inspector、EditorUtil 与菜单的当前实现文档，并清理失效入口。

## [0.5.42] - 2026-07-21

### Added

- 新增基于各 UPM 包 `Tracks.xlsx` 的埋点表聚合工具，支持在 Editor 中生成统一的全局埋点注册表。
- `SDKDataKeys` 新增 `OpenId` 与第三方登录提供方跨插件数据槽，供登录 SDK 与分析 SDK 解耦交换身份信息；该提供方数据槽现名为 `ThirdLoginProvider`。

### Changed

- 收敛 Excel/Luban 导出边界与输出交付流程，并统一数据表单元设置的路径、模式和索引契约。
- HTTP 传输接入 DoH URL 候选规划，为 BestHTTP 传输层提供统一的 IP 候选判定能力。

## [0.5.41] - 2026-07-16

### Added

- PlugPals“已安装”页新增横向“一键升级”按钮，可一次确认后批量升级当前列表中的全部可升级包；没有可升级项时按钮自动禁用。

### Changed

- PlugPals 窗口主标题更名为“PlugPals 云插件服务中心”，并让标题布局自适应窗口宽度，避免较长标题被裁切。

## [0.5.40] - 2026-07-16

### Added

- 新增 `EditorUtil.ProjectGuard` 与 `ProjectGuardWindow`，集中校验场景入口、资源目录和项目结构，并在进入 Play Mode 前阻断错误级诊断项。
- 新增 `START_HERE.md` 及项目结构、资源工作流、验证流程等渐进式接入文档。

### Fixed

- `Nova.OnDestroy` 仅允许当前活动实例执行 Manager 关闭与静态引用清理，避免旧实例销毁时误清理新实例状态。

## [0.5.39] - 2026-07-15

### Added

- `Samples~/MainDemo` 震动 Demo（`DemoVibrateView`）新增强度、尖锐度、预持续时间、持续时间的自定义输入框，并对输入参数做范围校验。

## [0.5.38] - 2026-07-13

### Fixed

- 将 `com.solotopia.unitask` 最低依赖版本提升至 `10.0.6`，确保 NovaSpark 在 Unity 6000.5 新工程中解析到 bundled UniTask core 2.5.11，避免 Tracker 使用废弃 `TreeViewItem` API 导致 `CS0619`。

## [0.5.37] - 2026-07-13

### Changed

- Pipify 的 Android 签名配置统一为 keystore path 与 key alias 命名，已有配置升级后不会自动迁移，需重新保存对应值，否则构建前校验会报配置缺失；Split Application Binary 也可脱离 App Bundle 独立生效。
- PlugPals 安装带 scoped registry 的包时，第三方依赖保持为 UPM 传递依赖，不再展开到项目顶层 manifest；卸载仍会清理旧版留下的同类条目。

### Removed

- 旧 Spine 槽位换肤与动画时长查询的四个公开扩展入口已删除且不会恢复；外部 Spine 使用方需在升级前迁移到自身适配层或 Spine 官方 API。

## [0.5.36] - 2026-07-08

### Fixed

- 清理 MainDemo 示例 asmdef 克隆残留引用：`NovaFramework.Samples.Runtime` 删除代码未使用的 gamelogin / gamesave / tga / appsflyer / ad 跨包程序集引用（MainDemo Runtime 实际仅依赖框架自身），避免消费工程仅安装框架、import MainDemo 后因缺这些子包程序集编译报 CS0234。此为 sample 模板根因清理——`nova-create-sample` 由 MainDemo 克隆子 sample 时不再继承这批残留引用。

## [0.5.35] - 2026-07-08

### Changed

- SDK 模块重构：`SDKManager` 运行时启用统一以 `ConfigMaster.EnabledSDKs` 为唯一源，排序改用 `ISDKPlugin.Priority`（接口属性）；`SDKComponentInspector` 订阅 `ConfigMaster` 保存事件，`ConfigWindow` 保存后即时刷新 SDK 面板。
- 明确 DataMaster 接口规范：`topicId` 传 `Params` 字典 key（`topic_name`），非 `experiment.topicId`；补必传分流用户属性 `app_version` / `install_time` 处理。

### Removed

- 删除 `SDKPluginEntry` 的 `[SerializeField] public int Priority` 字段（运行时排序已改用 `ISDKPlugin.Priority`，该序列化字段为冗余残留）。**注意**：旧 `ConfigMaster.asset` 中该字段的存值在反序列化时丢失，属序列化行为变更（不影响跨包编译，无消费方引用该字段）。

## [0.5.34] - 2026-07-03

### Changed

- 宏定义迁移至 asmdef versionDefines（ADR-064 后半步）：`NOVA_NICEVIBRATIONS`（`com.solotopia.nicevibrations` ≥ 10.0.5）、`NOVA_SIMPLEDISKUTILS`（`com.solotopia.simplediskutils` ≥ 1.0.7）改由 `NovaFramework.Runtime.asmdef` 的 `versionDefines` 声明，装包即自动跨平台定义；`package.json` 的 `nova.requiredLibraries` 移除对应 `defineSymbols` 展示字段（requiredLibraries 仅作 PlugPals 展示，宏定义权威改由 asmdef 承载）。

### Removed

- 删除 `NOVA_UNIWEBVIEW`、`NOVA_WEBGLSUPPORT` 死宏声明（孤儿宏根除）：`package.json` 的 `nova.requiredLibraries` 中 `com.onevcat.uniwebview`、`com.solotopia.webglsupport` 移除 `defineSymbols`，asmdef 不再重新定义（全仓无 `#if` 引用，确认为死宏）。

### Fixed

- Debug 模块 `Settings.asset` 默认 `_isEnabled` 置为 0（关闭），避免开发态 Debug 面板误启用。

## [0.5.33] - 2026-07-03

### Added

- `NetResponse<T>` 新增 `Fail(int errorCode, string errorMessage, T data)` 工厂重载，支持失败响应携带业务数据（ADR-068）：服务端在业务错误码下仍返回业务体（如绑定冲突返回 existing_uid）时，业务侧可在 `IsSuccess=false` 时读取 `Data` 获取附带信息。

### Changed

- `NetService.SendAsync` 业务错误码分支增强：服务端返回业务错误且携带业务体时，尝试解析并随失败响应带回；解析失败或无业务体则降级为不带 data 的失败响应，不影响错误码 / 描述透传。
- BuildProcessor 的 `UnityManifest.xml` 模板路径解析重构：新增 `NovaBuildShared.ResolveUnityManifestTemplatePath()`，开发态优先 `Assets/Framework` 路径，UPM 引用态回退包 `resolvedPath` / `assetPath` / `AssetDatabase.FindAssets` 三级定位，修复 UPM 引用态模板缺失导致 Android 构建漏复制 UnityManifest 的问题。
- `EditorUtil.Config.WorkspaceActive.GetActiveRuntime` 定位策略增强（ADR-047）：首选 `ConfigMasterSO.ExportTarget` 序列化引用（GUID 追踪，资产可置于任意位置），`ExportTarget` 为 null 时回退 ADR-033 布局约定兜底，覆盖未配 ExportTarget 的老工程与新 sample。
- 同步刷新 Minds / Docs / AGENTS.md 文档（ADR-067 登录 / 绑定 / 云存档三端分离、ADR-068 网络失败携带数据、ADR-069 云存档跨用户查询、PAT-140 UPM 包与 sample 依赖关系）。

## [0.5.32] - 2026-06-19

### Changed

- PlugPals 公共仓库默认地址恢复为公网 `https://upm.solotopiax.com`（`c_DefaultExternalUrl` 常量与 nova-publish 技能默认值同步切回公网；内网云 `4874` 不受影响）。
- 随上轮 Debug 面板、BuildProcessor、EditorUtil.Luban/Network 等已提交改动一并发版。

## [0.5.31] - 2026-06-18

### Changed

- PlugPals 公共仓库默认地址恢复为公网 `https://upm.solotopiax.com`。

## [0.5.30] - 2026-06-18

### Changed

- 例行版本升级。

本文件记录 Nova Framework 主包各版本的变更内容，遵循 [Keep a Changelog](https://keepachangelog.com/) 格式。

---

## [0.5.29] - 2026-06-16

### Added
- PlugPals 安装时把被声明 registry scope 覆盖的依赖显式写入项目 `manifest.dependencies` 顶层，确保 UPM 作为直接依赖解析安装（仅作主包传递依赖时 UPM 不保证拉取 scoped-registry 包）；卸载时一并移除。
- PlugPals 卸载按钮增加确认弹窗。

### Changed
- `EditorUtil.PlugPals.UninstallPackage` 增加 `registryUrlsNeededByOthers` 参数，移除私有仓库前对仍被其它已安装包共用的 url 做保留保护（public 签名变更，仅 PlugPals 窗口内部调用）。

## [0.5.28] - 2026-06-16

### Added
- PlugPals 支持包自带声明的作用域仓库（`package.json` 的 `nova.scopedRegistries`）：安装/升级时按 URL 注册或更新到项目 `manifest.json`，卸载时按 URL 移除；被声明 scope 前缀覆盖的依赖在缺库检测中放行，交由该私有仓库 + UPM 传递解析自动拉取（典型场景：MAX 子包依赖的 AppLovin 官方私有云仓库）。
- PlugPals 卸载时对仍被其它已安装包共用的私有仓库做保留保护，避免误删；安装/升级前增加确认弹窗。

### Changed
- SDK 广告接口统一加载结果模型：`AdLoadEvent` 重命名为 `AdLoadResult` 并合并失败载荷，`IAdPlugin.RequestAsync` 返回 `UniTask<AdLoadResult>`，以 `Success` 标识成功/失败并携带错误码与描述。
- CheckUpdate 编辑器工具随 PlugPals registry 配置调整同步更新。

### Removed
- 删除 `AdLoadFailEvent`（失败信息合并入 `AdLoadResult`）。

## [0.5.27] - 2026-06-16

### Changed
- PlugPals 依赖检测重构：遍历包 `dependencies`，以 PlugPalsWindow 内存中已拉取的外网/内部云 registry 包列表判命中；命中依赖按来源自动配 scope 随主包安装，未命中（本地无 + 非 `com.unity.`/`com.solotopia.` 前缀 + registry 无）弹购买/内部云引导并中止安装。
- AssetManager 远端清单不可达时改为三级离线回退编排：沿用当前已激活清单 → 本地缓存版本清单 → 内置首包清单。

### Removed
- 移除 PlugPals 运行时宏注入机制（`requiredLibraries.defineSymbols` 注入、`PlugPalsInjectedDefines.json` 账本、后台审计弹窗、会话级抑制、scope 已注册判据）；可选库的宏改由各 asmdef 的 `versionDefines` / `defineConstraints` 自行处理。

## [0.5.26] - 2026-06-15

### Changed
- Asset 远端清单不可达时回退使用当前已激活清单或内置清单，避免 DNS / 网络异常导致启动流程卡死。
- PlugPals 缺失依赖检测跳过 Unity 包与已注册 scope，并对重复缺失提示做签名去重。
- MainDemo 字体资源随本轮主框架 sample 发布同步刷新。

## [0.5.25] - 2026-06-15

### Fixed
- 修复 PlugPalsWindow 点击安装/升级时仍可能在同步路径上拉取远端 package 元数据导致编辑器卡顿的问题；缺失三方库预检现在只使用包列表阶段已经写入条目的 `dependencies` / `nova.requiredLibraries` 元数据。

## [0.5.24] - 2026-06-15

### Fixed
- 修复 PlugPalsWindow 点击安装/升级时在 IMGUI 按钮同步栈内直接触发 UPM Resolve，导致消费端 Unity 编辑器长时间无响应的问题；现在 manifest 写入后将 Package Manager Resolve 排队到下一帧并合并重复请求。
- 收敛 PlugPalsWindow 远程包请求的 CancellationTokenSource 释放路径，避免 domain reload 前后重复 Dispose 的日志噪声。

### Changed
- 同步刷新 PlugPals 与 EditorUtil.PlugPals 文档，明确安装/升级/卸载后的 UPM Resolve 延迟执行边界。

## [0.5.23] - 2026-06-15

### Changed
- 补发 `upm-release-2026.06.10-01` 后主框架累计变更，包含 BestHTTP 可选后端解耦、内部云仓库展示与缺失依赖提示链路、Util.Json 依赖迁移等内容。
- MainDemo 示例与框架文档随本轮主包发版同步刷新。

## [0.5.22] - 2026-06-10

### Changed
- 网络模块增强 DoH 预热与缓存直连链路：数据加载完成后可自动触发预热，HTTP 请求优先复用缓存 IP 直连以提升请求稳定性，并同步收敛相关文档说明。
- ConfigWindow 的 Luban 相关面板与 Network Inspector 提示文案同步刷新，降低配置入口的理解成本。
- MainDemo 示例场景随主包同步更新。

## [0.5.21] - 2026-06-09

### Changed
- Config 导出时会将当前选中的 `DevelopMode` 回写到激活场景中的全部 `FrameworkComponent`，并在 Inspector 顶部以只读彩色文案显示当前开发模式。
- App / Asset 面板拆分为 Debug / Release 的主备地址配置，启动阶段改为读取节点本地序列化的 `DevelopMode` 选择路由，解除对 Config 加载时序的依赖。
- App 版本检查新增主备地址回退语义：主地址失败、超时、返回空内容或地址为空时自动切备用，备用同样不可用时统一返回 `NoDownload`。

## [0.5.20] - 2026-06-05

### Changed
- 启动流程优化：Splash / 进度面板的销毁时机由启动流程内部（ProcedureLoadDll）移交业务入口统一回收，避免 LoadDll → 业务流程衔接时提前回收启动 UI 导致首屏闪帧。
- 同步刷新框架 L0/L1/L2 文档（Editor 工具、Config 维度体系、Asset 热更接口等）与代码注释。

---

## [0.5.19] - 2026-06-04

### Added
- Config 维度体系增强：支持按面板维度掩码（PanelDimensionMask / TypedDimensionMask）与 HybridCLR / Namespace / YooAsset 维度 Override，配置导出按维度投影取数。

### Changed
- 网络 Kit 层（NetService / NetBuilder / NetResponse / NetParser 等）配合登录、存档、支付 Kit 接入做配套增强。
- 优化 Pipify 流程，场景切换时自动绑定相关设置。

### Fixed
- 修正示例工程中的若干运行期报错与冗余资源。

---

## [0.5.18] - 2026-06-01

### Fixed
- 修复 iOS 打包时 Pods 目录路径拼接在部分环境下异常的问题，xcframework 自动 Embed 更稳定。

---

## [0.5.17] - 2026-06-01

### Added
- 新增 Kit 配置体系：Kit 包（如登录、存档）可通过实现 `IKitConfig` 声明自己所需的配置，框架在 ConfigWindow 新增「Kit 配置」面板自动扫描并列出所有 Kit 配置项，按平台与渠道分别填写后随框架配置一起导出；业务侧通过 `Nova.Config.GetKitConfig<T>()` 取用，无需各 Kit 自建配置入口。
- Asset 模块支持启动期按 tag 切片预热：可在 AssetComponent Inspector 配置启动热更的 tag 列表，仅下载本次启动真正需要的资源分片，缩短首次进入耗时。
- Asset 模块新增按 tag / 按地址创建下载器、运行期刷新资源清单、清理无用缓存等接口，支持并发数与重试次数配置，便于业务侧做分阶段下载与缓存治理。
- Hotfix 阶段新增热更提示弹窗，向玩家展示更新进度与下载体量。

### Changed
- 热更流程支持热更完成后自动清理无用缓存，避免旧版本资源长期占用磁盘（可在 Inspector 开关）。

---

## [0.5.16] - 2026-05-29

### Changed
- 主包对 `com.solotopia.yooasset` 的依赖从 `1.0.0` 提升到当前最新版 `1.0.3`，使框架默认随附最新的资源系统封装层。

---

## [0.5.15] - 2026-05-29

### Fixed
- 修复外部工程 import sample 后 Console 持续刷 `Asset Packages/<pkg>/nova-samples.json has no meta file, but it's in an immutable folder` 警告并触发 `SamplePathRewriter.RunRewrite` 重跑的问题：`nova-samples.json` 是发版描述符，仅开发期使用，不应进入 npm tarball；现通过 `.npmignore` 显式排除该文件及其 .meta，外部工程不再看到它落在只读 `Packages/` 区。

---

## [0.5.14] - 2026-05-29

### Changed
- 发版流水线统一：`publish_packages.py` 重构后主包 MainDemo 与所有子包 sample 走完全对称的 Stage 1 / Stage 3，主包不再走专属分支。所有 sample 等量复制 `Docs/Excels` + `Docs/Protos`、注入 `Nova.prefab` 全套 `*SourceDirPath` PrefabInstance override、写入 `SamplePathManifest`、以 `Samples~` 形式被 npm pack 收录；Stage 3 finally 反向还原 `Samples~ → Samples~.dev` 后整体清空，临时态零残留。
- 新增 `Assets/Framework/nova-samples.json` 作为主包 sample 描述符，与子包对齐；发版脚本从描述符读取 `sampleName / sourceDir / expectedNamespace / sampleManifestRelative / devPathPrefix`，主包子包不再分两套硬编码常量。

### Fixed
- 修复发版脚本对 `nova-samples.json` 中 `devPathPrefix` 末尾斜杠的脆弱性：现在统一在描述符加载阶段做 `rstrip("/")` 防御性归一，避免 C# `SamplePathRewriter.LocateSampleRoot` 因 `Path.GetFileName(devRoot)` 返回空字符串而静默放弃路径重写——之前 LoginDemo 因源 `nova-samples.json` 误带尾斜杠出现该 bug。

---

## [0.5.13] - 2026-05-29

### Fixed
- `EditorUtil.Config.WorkspaceActive` 增加多 sample 切换感知：当前活跃 scene 在 `Assets/Samples/<sampleRoot>/` 下且与 `Globals.json` 缓存的 ConfigMaster 不在同一 sample 根时，自动按 scene 重新推断 ConfigMaster 并覆盖 `Globals.json`，避免外部工程同时 import 多个 sample 时打开 LoginDemo 场景却仍读 MainDemo 的 ConfigMaster / YooAssetSettings / ConfigRuntime 资产。

---

## [0.5.12] - 2026-05-29

### Fixed
- `EditorUtil.Config.WorkspaceActive` 第③段路径推断升级为「从 scene 所在目录起逐级向上递归找 `Editor/ConfigMaster.asset`」：开发态扁平结构 `Assets/Samples/{Demo}/{Scene}.unity` 与 UPM 导入态嵌套结构 `Assets/Samples/{PackageDisplayName}/{Version}/{SampleDisplayName}/{Scene}.unity` 共用同一逻辑，外部工程 import sample 后打开 ConfigWindow 立即识别激活 ConfigMaster，不再提示「未检测到激活的 ConfigMaster」。

---

## [0.5.11] - 2026-05-29

### Added
- ConfigMaster 新增 `YooAssetSettingsPath` / `BundleCollectorSettingPath` 字段，YooAsset 全局设置与 Bundle 收集器配置改由项目根相对路径显式声明，替代 `AssetDatabase.FindAssets` 全工程扫描。
- ConfigWindow 新增「YooAsset」配置面板与「BindGuide」绑定指引面板，可视化维护两条路径与对应资产引用。
- 新增 `EditorUtil.Config.WorkspaceActive` / `EditorUtil.Config.YooAssetInjector`，将路径注入收口在 ConfigMaster，避开 Editor 启动期 Resources 多副本玄学。

### Changed
- `Nova.Visitors` 框架版本号同步升至 0.5.11。
- ConfigMasterSO 中仅供 Editor 期消费的字段（YooAsset/BundleCollector 路径、AOT/Game DLL 列表、`EditorEntries` 视图、`GetCommon` / `EditorAddEntry` / `EditorRemoveEntryAt`）补齐 `#if UNITY_EDITOR` 包围与注释，运行时表面收紧。

### Demo
- `Assets/Samples/MainDemo/` 配置资产、字体 SDF、各 Demo View prefab 同步刷新；新增 `MainDemo/Editor/YooAssetSettings.asset` 作为 sample 自带 YooAsset 全局配置。

### Obs Vault
- 沉淀 ADR-DRAFT「YooAsset 设置经 ConfigMaster 注入」「Editor active master anchor」「Nova.prefab 跟随 framework」与多条 PAT（atomic write json via rename / upm private fork / publish sample rewrite symmetric / create sample user decides dirname）。

---

## [0.5.10] - 2026-05-28

### Changed
- `Nova.Visitors` 框架版本号同步升至 0.5.10。
- `SDKComponentInspector` 插件条目绘制层调整，编辑器交互体验保持原有契约。

### Removed
- 清理 `Assets/Tests/` 下两份临时回归测试脚本（AssetHandle / KitNetwork 重构验证用例），相关回归已并入对应模块自带测试。

### Demo
- `Assets/Samples/MainDemo/` 配置资产、本地化文本、Launcher 弹窗 prefab、字体 SDF 等资源同步刷新，跟随主包 Samples~ 一起发布。

---

## [0.5.9] - 2026-05-27

### Added
- 声音模块新增「按名称播放」入口，业务侧只需传 `ISoundRow.Name` 主键即可触发播放，无需再手动取行。
- 振动模块新增「按名称播放」入口，自定义振动与强调振动均可按数据表 Name 字段直接播放。

### Editor
- 新增 PlugPals 私有 Verdaccio 仓库 UPM 包管理工具，支持远程包列表拉取、安装/卸载、按版本查看更新日志。

### Docs
- 声音 / 振动 / Procedure 等模块 L2 文档同步刷新对外接口现状。

### Obs Vault
- 沉淀本期声音/振动 API 设计、UPM 包文档强制三件套、CHANGELOG 行文规范、XML 注释禁 HTML 转义、cs↔doc 同步铁律等多条规范。

---

## [0.5.8] - 2026-05-27

### Breaking Changes
- UI 模块新增「对象池开关」能力，关闭后视图直接销毁、不再走池缓存；UIView 子类如有覆写 OnInit 需按新签名补回参数。
- UIView 默认不再带淡入淡出，旧版自动挂 CanvasGroup 与 4 个淡入淡出字段已移除；如需淡入淡出请由业务侧自行实现。

### Added
- UI 视图打开 API 新增「是否走对象池」入参，业务可按视图按需选择缓存或直接销毁。

### Changed
- 资源加载文档全面强化「LoadXxx 必须经 Handle 释放」铁律，避免引用计数泄漏。
- 演示 prefab 同步清理已废弃的淡入淡出残留字段。

### Docs
- UI 模块与 Asset 模块的 L2 文档按本版本接口同步刷新。

### Obs Vault
- 沉淀 UI 深度因子重平衡 / Inspector 下沉、Asset Load API 统一回 Handle 等本期决策；并补全 API 命名、ManagerConfig 透传、UPM 版本管理等通用模式。

---

## [0.5.7] - 2026-05-26

### Breaking Changes
- 启动期 UI（进度面板 / 弹窗）改为多语言驱动，旧的单语言标题 / 内容字段已移除，文本统一由面板内多语言数组按类型显示。
- 弹窗显示接口签名调整，业务侧只需传按钮回调，文案由面板自身按弹窗类型选取。
- 移除一个无引用的 UI 加载抽象基类。

### Added
- 启动期新增独立本地化能力，可在资源系统就绪前的全链路（启动闪屏 → 版本检查 → 热更新 → 应用下载 → DLL 加载）安全使用本地化文本。
- 启动期内置中英文文案与字体资源，本地化资源路径模板可通过 ProcedureComponent Inspector 配置。

### Docs
- 启动期 UI 与本地化相关 L2 文档同步刷新。

---

## [0.5.6] - 2026-05-22

### Changed
- 网络 / 声音模块对外接口与 DTO 调整，调用方需按新签名迁移。
- 框架核心若干内部细节打磨。
- MainDemo 演示工程切换为基于 Nova.UI 的树形导航 + TMP 文字渲染。

### Added
- Vault 沉淀本期演示拓扑、UI 命名、Demo 覆盖标准、prefab 制作、文本组件、池化辐射等多条规范。

## [0.5.5] - 2026-05-22

### Fixed
- 外部工程导入 sample 后 Inspector 业务字段为空与 SDK `[Missing]` 提示问题已解决。
- 多版本 sample 共存时新旧版本识别不准的问题已修复（按 semver 数值排序）。

### Changed
- 发版脚本统一支持 docs / 业务字段两类自动注入，新增字段后无需改脚本。

---

## [0.5.4] - 2026-05-22

### Removed
- 放弃此前的桥接式生命周期方案，回归 HybridCLR 原生 MonoBehaviour + Prefab 直挂；相关脚本与 Inspector 一并清理。

### Added
- 发版流程支持把项目根 Docs 资源（表格 / 协议）随 sample 一起打包，外部工程导入后 Inspector 路径自动对齐 sample 内副本，无需手动改路径。

### Changed
- HybridCLR 约束规则全面重写为原生方案口径，dll 加载唯一入口与版本一致性等约束保留。

---

## [0.5.3] - 2026-05-21

### Fixed
- 修复 0.5.2 演示工程改名后命名空间 / 配置残留导致的「业务程序集未加载」与「入口 Procedure 未找到」启动报错。

---

## [0.5.2] - 2026-05-21

### Changed
- 演示工程更名 Demo → MainDemo，目录 / asmdef / 命名空间 / 发布脚本与子框架 sample 脚手架模板同步对齐。
- qa 测试改为按需就近建测试脚本（命名带「关键词+YYYYMMDD」），不再依赖固定测试入口。

### Removed
- 演示工程废除固定 Test 入口目录。

---

## [0.5.1] - 2026-05-21

### Changed
- 包内结构调整与冗余资源优化。

---

## [0.5.0] - 2026-05-21

### Added
- 接入 UPM 标准 Samples 机制，演示工程改作 sample 分发；导入后自动检测旧版本残留并询问设置启动场景。
- 各 UPM 包补齐 CHANGELOG / LICENSE / README 三件套，发版脚本强制校验。
- 提供子框架 sample 脚手架模板。

### Changed
- 主框架版本 0.4.2 → 0.5.0。
- 演示工程目录从项目根迁入 sample 子树。
- 发布工具沉淀为 Claude Code skill，发版命令统一入口。

### Removed
- 废除 bootstrap.zip 机制，相关脚本与 Editor 工具一并移除。
