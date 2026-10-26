# Changelog — com.solotopia.nova.framework

## [Unreleased]

## [0.7.4] - 2026-10-26

### Added

- UI 普通 OpenUIViewSync/Async 字符串入口按表内 Name 打开；OpenUIViewByAssetSync/Async 按唯一 AssetLocation 打开。首参数含义由方法名明确区分，第二参数 userData 原样传递，泛型、名称与地址打开均使用已加载表格中的分组、暂停与池化配置。

- Sound 新增 PlaySoundByAsset 按资源地址唯一查表播放；本地化新增 GetFontData 按当前或指定语言与 Mark 查询字体配置；Network 新增 ResolveHostKeyRow 按 HostKey 表内 Name 读取原始配置。

### Breaking

- 移除旧版非泛型“资源地址 + 分组”打开重载，地址调用改用 OpenUIViewByAssetSync/Async；共享地址改用 Name 选择配置。普通字符串入口的首参现在是 Name，第二个字符串是业务数据，旧调用可能仍能编译但含义已变化，必须检查迁移。旧泛型 bool 重载保留并标记弃用，开关不再覆盖表格；缺配置或地址有歧义返回 -1，不补默认值。自定义 IUIManager 需实现两个 Name 字符串重载及两个 ByAsset 方法。

- Sound 移除 PlaySound(groupName, assetLocation, params) 及 PlaySoundParams.Loop / Priority / VolumeInSoundGroup，迁移到表内配置和 PlaySound(name[, params]) 或 PlaySoundByAsset。Vibrate 数值入口改名为 PlayCustomByParams / PlayEmphasisByParams，后者删除原来不生效的 interval，保留名称入口。自定义 ISoundManager / IVibrateManager / ILocalizationManager / INetworkManager 实现需按新接口补齐成员；MainDemo 已迁移。

### Important

本次统一 UI、声音和振动的表格入口，包含不兼容调整，升级前请迁移旧调用。

(1) **UI 按名称或地址打开。** `OpenUIViewSync/Async` 的字符串首参改为表内 `Name`；资源地址改用 `OpenUIViewByAssetSync/Async`。第二参只传业务数据，分组、暂停下层、对象池由表决定，旧开关不再生效。旧“地址＋分组”调用可能仍能编译，必须逐处检查。

(2) **声音配置交给表格。** 使用 `PlaySound(name)` 或 `PlaySoundByAsset(address)`，移除旧“组＋地址”调用。循环、优先级、音量改在表中配置，不再通过 `PlaySoundParams` 覆盖；空间混合度现在实际生效。修改源表后请正式导出。

(3) **区分表格振动与手工参数。** `PlayCustom/PlayEmphasis(name)` 继续按名称播放；数值调用改用 `PlayCustomByParams/PlayEmphasisByParams`，后者删除原来无效的 `interval`。现在会合并全部表的同名步骤，同顺序保留来源顺序，请检查此前未生效的后续配置。

(4) **检查缺配置和重载失败。** UI/声音缺配置或地址不唯一时返回 `-1`。声音表的空行、空名称或重名会拒绝整轮索引，重新加载不再沿用旧行或部分表。振动加载失败保留旧完整配置，空配置加载成功会清空旧组。

(5) **补齐自定义管理器。** 自行实现 UI、声音、振动、本地化或网络管理器的项目，需同步新接口。字体新增 `GetFontData` 按语言与 `Mark` 查询，域名配置新增 `ResolveHostKeyRow(name)`。

### Fixed

- Sound 播放完整使用声音表的组、资源、循环、优先级、音量与 SpatialBlend，修复空间混合度未写入 AudioSource；拒绝无配置或歧义地址，重名或空名称拒绝整轮索引。Vibrate 汇总每个区域全部 Unit 的表行，同 Order 保持源顺序，成功后同时替换两路分组，修复只读首表、重复加载残留与构组半更新。

### Changed

- 重要升级窗口中每个版本的具体说明统一显示红色，包名、版本号及其他文字保持原有颜色，正文仍可选择复制。

## [0.7.3] - 2026-10-23

### Changed

- 对齐 HybridCLR 8.15.0 的编辑器命名空间及本地输出配置；运行时程序集名和框架公开入口保持不变。

- Unity 支持范围统一为 6000.4.x，最低 6000.4.2f1；同步安装入口与包级兼容说明。

- 对齐依赖版本：com.unity.inputsystem 1.19.0 → 1.20.1、com.unity.scriptablebuildpipeline 3.0.3 → 4.0.1、com.solotopia.yooasset 1.2.0 → 1.2.1、com.solotopia.nova.framework.mcp 0.1.5 → 0.1.6。

- 移除项目 Skill 契约中的 Unity 版本限制，由包级兼容说明和 NovaSpark 安装入口统一说明支持条件。

### Fixed

- 兼容 Unity 6000.4 的布局接口调用，保持 FlowLayoutGroup 与 VirtualVerticalLayoutGroup 在该版本下正常编译。

### Important

本版本仅支持 **Unity 6000.4.x**，最低版本为 **6000.4.2f1**。更高的 Unity 版本存在 HybridCLR 兼容问题，请使用符合上述条件的 Unity 编辑器；暂勿使用 Unity 6000.5 及更高版本。

## [0.7.2] - 2026-10-23

### Changed

- 补充第三方支付的平台接入检查指引，以及 MAX Android 构建修复的能力导航。

## [0.7.1] - 2026-10-23

### Changed

- 补充 Agent 能力所需的 Nova Framework - MCP 安装说明。

### Important

Nova 新增 Agent 支持。项目组可以通过自然语言查阅框架用法，并让 Agent 协助调整配置、导出数据、构建游戏和排查问题，减少查阅文档和重复操作。

使用这些能力需要安装 **Nova Framework - MCP**（`com.solotopia.nova.framework.mcp`）。它会随 Framework 的依赖自动安装，NovaSpark 也会将它列为默认安装项；已有项目缺少该包时，可打开 **Nova → Open PlugPals**，找到该包并安装。

## [0.7.0] - 2026-10-23

### Added

- 新增函数、重载、访问器、参数和字段的只读能力查询，按平台条件与声明变体定位源码、XML、Docs 和执行契约，并核对 Excel、配置及依赖输入。

- 新增 Config 面板维护与 nova.project.config.patch-coordinate：支持 Namespace、AppConfigs、维度编辑、Master 规范化及 IAP Store 引用补齐；渠道与开发模式使用互斥分支，仅保存选择字段。保护草稿和精确写集，兼容旧请求与回执，Runtime 仍单独导出编译。

- 新增可复用 UI 导出 Action，冻结表源、配置和写集，按本次编译及脚本重载验证，现有 UI Skill 共用同一入口。

- 新增 Pipify 配置检查及 Batch 创建、克隆、重命名、步骤排序和参数编辑；只保存配置，运行复用原入口。

- 新增项目自有普通 SO、Prefab 组件字段检查与修改，冻结真实字段路径和写集，禁止旁路修改专用配置或存档加密字段。

- 新增指定 NamedBuildTarget 完整脚本宏集合修改，支持空集合、无变化不写及编译重载后验证。

- 新增 Editor 值 AES 检查、迁移与恢复：FileFragment 共享全集、PlayerPrefs 已索引字符串及显式全局 Save、SQLite 标准值同库事务分别受控并保留备份。persistence.inspect-data 默认仅查索引与固定文件元数据，不读玩家值或凭据、不保存建连；值迁移使用独立 scope 和计划，Cipher、Privacy 密钥轮换独立处理。

- 新增已保存私有 Master 的资源密钥源码与 linker 生成 Action，预演精确文件变化并在编译后核验，不切换 Master 或平台。

- 新增 Tracks 事件、属性、页签、文字、目标和统一样式维护 CLI，提供计划、验证与恢复；正式汇总 Action 复用 TrackRegistry，冻结登记来源及输出。

- 新增已有 IAP、微信商品工作簿的只读转换 CLI，保存复用 Config 面板 Action。

- 新增 xlsx/csv 非敏感标量表源编辑 CLI，按行键、字段和原值冻结修改；本地化专用分支可新增 Key 与已定义语言列，保留原内容、样式和合并。正式导出与运行验收复用原能力。

- 新增已冻结 WebGL 导出的本机运行 CLI，提供 Wasm、Brotli/Gzip 响应头，拒绝目录越界和产物漂移。

- AppManager 新增 Android APK HTTPS 主备下载、受限归档检查及同目录原子提交；失败或取消清理临时文件并保留旧产物，下载路径与安装结果分开返回。

- 新增默认关闭的 Android 游戏内 APK 安装能力，串联受控下载、原生验签和系统安装确认，同步启动弹窗及中英文案。

- 新增 NetworkComponent.SendProtoAsync 公开门面，业务 Service 复用现有 Header、AES、主备、重试、解析与取消链；不开放隐藏身份写入。

- 现有存档 Action 新增 macOS Editor SQLite 非空 Cipher 同库轮换与 Privacy 默认 AES 三存储协调分支，支持自动生成或已有项目 opaque 凭据、封存备份及原 Operation 恢复；Privacy 经 Master 保存和 Runtime 正式导出闭环，未完成时阻断 Editor 启动与构建，设备及外部消费者另行验收。

- MainDemo 新增全局暂停、恢复、速度、退出和显式存档保存入口，支持 Content 场景独立加载卸载，异步结果随页面清场。

- MainDemo 新增 Table + UI 物品详情示例，正式注册导出 UIView，按编号读取已加载表，支持重复点击回焦、错误清空和池化重开刷新。

- MainDemo 新增 FileFragment 业务进度迁移示例，保留旧档并同条目保存来源、版本及新版进度，支持重开读回、幂等更新和冲突拒写；提供隔离 Play 入口，真实玩家数据、其他存储及设备耐久性另验。

- 已有 AAB 设备安装复用 Player Skill，分别核验 APK 集构建、签名、设备读回及清场；普通 Player 仍要求 Unity BuildReport。

- Firebase Demo 新增消息分组输入及订阅、退订按钮，按页面生命周期接线，区分派发提示与 SDK 实际回执。

- 新增升级后重要内容强制提示，汇总各包 CHANGELOG Important 的跨版本声明，区分单包和批量升级，按本地用户保存人工确认并在重载后补查；发版预检展示全文并询问修改或补充。

- 新增可选 ProtectedInt/ProtectedLong 内存防护、ACTk 适配、异常隔离、分帧换钥及脱敏埋点；隐私配置贯通三维导出，未安装 ACTk 保持普通数值及存档兼容，并提供官方购买引导。能力随独立 MemoryProtection 包提供。

- 新增 EditorUtil.Network.Http 默认 Head 与单次 JSON HTTP 门面，统一 app_id、platform、channel、develop_mode、version 及超时、取消响应语义。

- 新增 nova.project.iap.upload-product-catalog，冻结已保存 Runtime、完整目录、版本和固定目标，通过可选包桥至多提交一次；发送前记录脱敏意图，只读核验原回执，缺后台实际应用证据时保持 partial。

### Changed

- ConfigWindow 保存下沉至共享设计态服务，保持保存范围和迁移收尾语义，保护窗口草稿，落盘后刷新失败仅报警告。

- Agent 查询统一导航源码/XML、模块 Docs 与执行契约，涵盖 Sample、Skill 脚本和指定原生入口，按实际安装包及唯一 Sample 判断适用性；Library 仅存可重建缓存，脱敏副本生成便携索引。移除人工 Knowledge、历史验收档案及全库审查账本，对齐 Docs、Minds/OBS 和协作规则的过期入口；参数与生命周期说明按需读取，实际任务重新验收并清场。

- 配置坐标检查补齐已有坐标、掩码轴、Namespace 和 App 字段发现；密钥与 IV 仅报告配置状态，来源、生效阶段和旧功能缺口明确保留。

- PipifyWindow 与自动化共用参数和目标保存服务，保护窗口及延迟编辑草稿，仅刷新干净窗口。

- 自然语言 inventory/describe 新增 summary 摘要模式，默认 full 保持兼容；Router 分页定位后再展开精确契约。游戏控制复用 Procedure Service 与 Nova.Self，UI 本地化按完整语言集合验收；SDK 指引明确实参、事件、生命周期及平台边界，区分接入、诊断和维护证据，维护任务不额外要求 Play。

- 普通 HTTP GET、字符串 POST、原始数据和文件上传通过可选 ICancellableHttpManager 提供令牌末位取消重载，兼容旧调用及自定义管理器；MainDemo 页面关闭时取消请求、等待终态并唯一归还响应。

- 现有 Skill 支持同契约任务分支及 Workflow Operation 子集，复用声音、振动、网络纯导出和 Tracks 维护入口；冻结父契约，继承写集、锁、幂等、确认和验收要求，Python 与 Unity 同步拒绝越权，完整接入和外部收件仍独立验收。

- 受控 Player 构建声明原回调外部统计，冻结活动与请求目标的 SRP、Core/Probe 空资产及缓存；WebGL 接纳全 null 管线与空 VolumeProfile，Quality 只恢复原引用的官方完成态，对应 Analytics 回调仅接纳已关闭设置。保留 Adaptive Performance 空 provider、EDM 警告和 IAP 缺配置分支，未知状态阻断，归属不明残留保持 partial，不关闭设置或跳过厂商回调。

- Player 构建支持慢计划登记、断线取回与终态清场，保持原四阶段及平台验收要求。

- MainDemo WebSocket 演示新增独占连接、二进制回显确认、显式重连及关闭退订，不接管其他业务通道。

- 持久化 Skill 增加隔离 schema 迁移与冲突、故障恢复示例，仅操作独立临时文件，不授权真实存档维护。

- Android 构建检查复用 Manifest、ProGuard、AppsFlyer 和 Firebase 规则，冻结配置来源、处理器及上下文，按实际工程采集构造和前后处理阶段；缺失阶段、跨工程预测或观察异常不能算通过，未完成的原生接线及写入仍明确保留。

- 调整 Config 隐私页分区和横线，将插件重新检查及状态置于说明上方，绿色表示已检测到、黄色表示未检测到。

- 重要升级说明按包、版本及正文分层缩进，合并包名称与包名，版本按完整 SemVer 倒序；包间粗线、版本间细线、功能区横线及深色正文便于阅读，勾选和确认固定于底部独立区域。

- 内存防护迁入可选 com.solotopia.nova.framework.memory.protection，主包仅保留通用隐私扩展数据与读取接口，移除防护实现、ACTk 适配及 MainDemo 1.10，改用独立 MemoryProtectionDemo。

- 独立包可声明 requiredFrameworkApiVersion，发布器在打包前核对正式依赖下界及同批或目标 registry 的可用性，阻断新 API 与旧主框架组合。

- EditorUtil.Network.Http 默认 Head 的 develop_mode 由 DevelopMode 枚举生成 Debug/Release 字符串，避免任意协议文本。

- Framework Editor 新增 com.unity.scriptablebuildpipeline 3.0.3 直接依赖，显式配对 Unity.ScriptableBuildPipeline.Editor 的构建路径与回调接口；消费工程升级时需解析该构建依赖。

- 主框架依赖配对 MCP 0.1.5 与 NiceVibrations 10.0.6，确保安装入口解析到本轮配套版本。

### Fixed

- 校正 Skill/Docs 中的 Action、导出位置、配置来源和生命周期说明，包括 Excel Write 为 CSV、Table 注册失败、LanguageInfo 只读、ObjectPool 释放、Proto 导出跳过以及事件队列和两级引用池回收边界；区分已有事务恢复与真源缺失，修正导航而不放宽执行契约。

- ConfigWindow 保存失败提示按真实写入阶段区分，写入开始后要求核对配置资产，避免误称未发生保存。

- ConfigRuntime 导出直接拒绝 Channel=None；Collector 分开报告顶层配置与当前坐标的有效 Override 路径，保留其他 Generate Action 的坐标解析语义。

- Android Manifest 模板仅复制 XML 正文，输出资产保持独立 GUID，避免复制 .meta 产生冲突。

- 能力导航纳入微信引力平台边界与 MAX 自有 Pods 脚本，仅枚举真实 UPM 目录并拒绝符号链接越界；按变更影响核对当前来源和辅助输入，避免普通文件中断遍历或路径调整引发无关模块失效。

- 数据导出严格解析组件 JSON，规范化 Table code/data 与 Network Proto/data 范围，冻结 Namespace、设置、表源、模板、工具和完整输出，拒绝漂移、损坏回执、缺项或路径类型替换；Table 保持自身 Luban 配置来源，其他代码结果绑定本次编译及所属类型，区分更新、无需更新和空编译，同令牌至多补一次清缓存编译。租约覆盖清场刷新，获取失败不删他人产物，写后失败保留 partial 且不重放；旧收据保留原证据边界。

- 修复 WebSocket 初连、重连的认证、心跳和状态隔离，WebGL 按连接实例分派；握手支持取消和超时，关闭等待收发退出并清消息、回收池对象，Tcp/TcpPb 不再解包 Close 控制帧。

- 修复 Luban union-key 模板重复生成 global::global::，恢复合法 Dictionary 类型。

- 修复 MainDemo 资源、Prefab、HTTP、声音、广告及 App/Config/Native 页面的异步所有权，关闭重开后丢弃旧反馈，取消后迟到实例统一回收，响应和句柄唯一释放；UI 子页按所有权取消，保留关闭失败编号供重试，本地化补齐持久 Key 绑定。

- 修复 MainDemo 对象池空 Target、名称不一致和重复取用覆盖引用；预加载退出、重入和重试各自持有异步任务，新增重新预加载入口，主流程仅回收自身导航并列出已注册业务流程。

- 修正 MainDemo linker 的 GameLogin 程序集名称，已保存 Pipify 组合先导出配置及网络代码/数据，再验证、生成和编译 DLL，保留既有参数及其他批次。

- TextLocalizing 在 Start 完成后成对订阅、刷新和注销；配置加载合并并发句柄请求，拒绝关闭后迟到回写及取消重入，失败完成全部等待者并可重试。本地化字体和语言成功后替换快照，失败保留原数据，避免重入累加。

- 表源 XML 精确识别自闭合空白格，避免吞入相邻单元格并丢失新增行样式，保持标量和 Tracks 编辑行为。

- 普通 config.patch-panels 保留各物理坐标已有 Privacy AES Key/IV，拒绝广播、合并、更换或清空覆盖，初配和同值仍可用；Config 导出仅在 YooAsset 解析字段变化或资产原已脏时保存，避免密钥轮换扩大写集。

- 完善 FileFragment、值 AES、Cipher 与 Privacy 中断恢复，核对原 token、完整备份、受保护 Prefab 及 Master/Runtime 原或新状态和磁盘摘要；支持本操作保存失败的完整脏态、半写清理和 FileFragment 仅清 stage 重试。只清自建半写文件，跨崩溃 .restore 认证后隔离保留；旧日志缺保护材料时仅原计划全文仍匹配才可恢复，未知或额外修改继续阻断。

- SQLite 维护以独占 CreateNew 建立自有备份，源库仍只打开既有文件；Privacy 恢复保留 ISO 时间原文，避免自动日期转换破坏来源认证，凭据封存和启动 marker 失败仍受保护。

- ClearUnusedCacheAsync 在令牌已取消或 Manifest 未就绪时不启动，底层 Failed 抛出包名、状态和错误；开始后取消仅停止等待，不中断或回滚磁盘清理。

- 脚本宏通过 Unity 原生单对象序列化保存 PlayerSettings，独立重读文件并在重载后核验完整状态与脏标志，拒绝未保存草稿及活动 Build Profile，避免只改内存或误写其他 Profile。

- Persist.LoadAsync 共享初始化支持未完成时并发等待及完成后复用结果，修复原始 UniTask 重复等待，保留隐私维护门禁和原始失败。

- Bundle 构建冻结已保存场景、Collector 依赖、OutputCache、SBP 输出/缓存及回调，阻断脏资产、混合 RawFile 和未知 SDK/Shader/ProBuilder 副作用；完整校验报告、辅助产物及恢复回执，兼容旧明文。Android 场景分支观察自然 Version Handler 收尾，初次快照后计十秒等待，以轻量探针让队列推进，超时或取消保持 partial，不推进或重放回调。

- HybridCLR Generate 冻结平台、IL2CPP、Player 回调、PlayerSettings、StripAOT、工具链和临时选项，分别核验生成 link.xml 与 Master AOT 表并去重；拒绝未知副作用和旧 staging。Android 观察 SDK 阶段及 Gradle 输出，WebGL 用独占重载租约先保存结果再放行重载；慢计划可登记取回，失败或断线不重放，编译重载证据不足保持 partial。

- 修复 HybridCLR Generate 场景对象重建、首次正式重载及 Unity DTO 延迟加载的误判，按保存场景完整身份双射及同一 SceneView 相机 HDR 恢复核验；字体零状态使用同次序列化结果，兼容 JsonUtility 的 null/空串往返，修复压缩快照重复封装。完整前后态无损保存且受大小限制，旧记录、后续重载或未知变化仍拒绝。

- HybridCLR Generate 冻结输入或回调不匹配时，返回同次已有数据的有界脱敏差异，涵盖身份、顺序、重复及字体保护，并保留完整后态保存失败原因；缺失、歧义和超限明确报告，不放宽比较与准入。

- Pipify Generate All 成功后补全现有 link.xml，避免覆盖配置要求的 AOT 保留条目，生成内部裁剪与最终 Player AOT 拷贝仍分别验收。

- 签名包与公开同步从实际脱敏源码生成白名单便携导航，仅保存身份、位置、指纹及 Docs/契约引用，支持合法多级输入和产物路径；不分发 Knowledge、旧大索引、源码片段、原始配置或执行材料，核对来源摘要与函数区间，消费查询无需 Dotnet，生成成功不代表语义已审或运行通过。

- Player 构建期 YooAssetSettings 用 FileUtil 仅复制正文并导入独立 GUID，避免 SaveAssets/CopyAsset 保存其他脏资产；清理失败或正文漂移时保留副本和所有权 marker 并报错。

- Android Force Resolve 冻结目录、工具、模板、POM、EDM4U analytics/同意状态及完整刷新合同，仅执行一次正规解析；接纳已有模板和已安装旧配置迁移的自然无弹窗分支，拒绝首次初始化、未知参与者及 shadergraph/shadersubgraph 越界导入。按完整 POM 和产物验收，保护非目标状态，写后失败及缺新合同的旧回执保持 partial。

- Android 依赖刷新以轻量探针异步等待 Editor update，保留十秒等待和完整前后验收；快照及回执使用有界无损表示并兼容旧数据。精确核对对象、场景、选择及文件，区分允许的 UI Toolkit/Atlas 派生资源，SpeedTree 使用全项目累计有界扫描；诊断给出有界真实路径及身份差异，不放宽用户资源保护。

- Android Resolve 按实际加载模块、源码归属、SHA-256 和 MVID 核对刷新与 MCP Plan/Verify 调用链；编译身份移入既有 Skill reference，避免内嵌下游指纹产生重编译失效循环，未知编译结果仍拒绝，历史 partial 不升级。

- Player HybridCLR AOT 重建拒绝输入、Bee 源及平台产物重叠，核验完整平铺 DLL 集和目录摘要，Windows 32/64 使用实际 Bee 路径；等待 ProjectAuditor 官方延迟报告完成并绑定本次 BuildReport，失败仅在归属明确且无待执行复制时恢复 LastClean，超时保留恢复材料。

- 修复 Skill 同步与随包校验对 Workflow 子集、AAB 安装画像及消费工程 MCP 来源的识别，按本工程 lock 拒绝歧义；保留父子风险门、普通 Player 最低证据及依赖限制，补齐存档保存前置和 UPM 已提交操作的受控恢复说明。

- 修复 Player WebGL 对 Assets/Framework 回调路径、FontAsset 类型转发、Facebook/Firebase iOS 空分支及源字体零缓存的误判；核对精确编译身份、SRP 和日志下游，记录官方 reset、TMP 清理及 Facebook Resources 自然绑定。观测不重新加载未加载字体，真实脏资产、未知同名类型或副作用仍阻断，不主动改写厂商计数。

- HostKey 旧表缺少备用域名列时仍校验主域名，缺少 Value 列拒绝导出，模板补齐默认空的 FallbackValue。

- MainDemo Asset 页支持指定 Package 缓存清理及取消等待，按 Owner/Package 防重复，终态不明不重放，区分操作返回与磁盘验收。

- 正式 Verify 支持有界异步收件，核验仅执行一次，断线后可读完整脱敏结果；同步入口、partial 及 Receipt/Operation 语义保持。

- 本地 Bundle、Player 和 HybridCLR 区分正式交付与附带构建统计证据，保留外发、凭据、确认及回调门；显式外部验收、上传等独立节点仍为必需。

- AgentActions 文件摘要使用 64 KiB 临时缓冲降低批量分配，保持 SHA-256、只读共享及取消检查。

- MCP 新 API 发布配对按 Framework → MCP 方向核对同批主框架基线和精确桥包版本，不再误要求反向依赖，保留普通 SDK/Kit 的正式依赖门。

- 存档维护备份、日志和门禁迁至 Library/Nova/AgentActions/PersistenceMaintenance，表源计划、回执和锁迁至同级 TableSource，避免过程材料进入版本管理目录。

- 适配 Unity 6000.6.7f1 编译：Debug 自定义布局使用五参数尺寸接口，IAPDemo 使用 TMP 文本换行模式。

- 修复 DataReceiver 异步加载或释放失败后并发等待无法结束的问题：向本轮全部等待者传播失败并清除共享任务，允许后续重试；收到非 TextAsset 资源时返回失败并归还资源，避免通用数据读取链持续等待或遗留资源。

### Breaking

- 使用 ProtectedInt、ProtectedLong、MemoryProtection 的项目须安装 Nova Framework - MemoryProtection，并在业务 asmdef 引用 NovaFramework.MemoryProtection.Runtime；公开命名空间仍为 NovaFramework.Runtime。隐私防护开关由独立包持有，旧值自动迁移，主框架不强制依赖防护包或 ACTk。

- 能力查询改为声明当次验收要求，不再读取或输出历史验收记录、通过率与旧目标；依赖旧统计字段的脚本需适配。

- 最低支持 Unity 6000.6.7f1；对象身份采用完整 EntityId，Debug 布局补齐 uGUI 2.6 的最大尺寸接口，消费 Skill 同步版本前置。

- 移除 EditorUtil.ProjectGuard 全部公开 API/DTO、Project Guard 窗口菜单、自动 Play 检查及 Quick/Play/Build/Release profiles，同时移除 NOVA-SCENE/NOVA-RES 配置门策略；独立 NOVA-BUILD001～012 预检迁入 EditorUtil.Build，nova.project.build.inspect-readiness 与原 Skill 入口保留。依赖旧 API、DTO 或 profile 的调用方须迁移。

### Important

Nova 新增 Agent 支持。项目组可以通过自然语言查阅框架用法，并让 Agent 协助调整配置、导出数据、构建游戏和排查问题，减少查阅文档和重复操作。

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

### Validation

- 资源加解密五种 Bundle × 三种清单共 15 组 Android 目标构建及 Editor OfflinePlayMode 验证通过，覆盖五类资源加载、句柄释放和 Bundle 卸载；临时密钥与测试产物已恢复。

## [0.6.37] - 2026-10-08

### Added

- 新增可选 SDK 注册交付及一次性用户属性能力，注册包含同次登录并支持初始化晚到，供微信引力与 TGA 自动接线。

### Changed

- 本轮新增注册并登录通知入口命名为 RegisterAndLogin，明确包含注册及一次登录，既有 Login 入口保持不变。

### Fixed

- SDK Register 每次有效调用均交付注册和登录，不在接口内部按 UID 去重；注册调用时机由业务负责。

## [0.6.36] - 2026-09-29

### Added

- 隐私配置新增仅 iOS 有效的 Info.plist UsageDescription JSON，随 Config 导出并在 iOS 构建时注入应用级用途说明；ConfigWindow 导出前与构建时校验无效键和空文案，ProjectGuard 检查 iOS 隐私配置与导出快照的漂移。

### Fixed

- 修复 iOS 隐私清单构建后处理中的 Path 类型歧义，避免 iOS 条件编译报 CS0104。

## [0.6.35] - 2026-09-28

### Added

- 通用隐私配置新增仅 iOS 可编辑的 PrivacyInfoConfig，导出时校验 Required Reason API JSON，并在 iOS 构建后处理合并应用级 PrivacyInfo.xcprivacy。

### Changed

- SDK 插件改为按显式能力依赖并发初始化，新增插件级状态等待和最新账号 UID、用户属性补交；全局 InitializeTask 仍等待全部插件的最终状态。

- ConfigWindow 将微信小游戏配置中的引力引擎字段统一展示在微信字段之后，不改变保存与导出语义。

- 隐私配置 PrivacyInfoConfig 默认填入 Solar iOS Required Reason API 参考值；旧 ConfigMaster 首次打开时补齐空值并在保存后保留可清空和自定义语义。

### Fixed

- 系统语言自动选择补充 UI Culture 标签识别，覆盖 Unity SystemLanguage 无独立枚举值的 12 种语言，并区分巴西葡语及塞尔维亚语文字变体；保留已保存偏好、支持语言与原有回退规则。

- iOS 导出时按 PlayerSettings 的最低系统版本提升较低或缺失的 Pods 部署版本设置，保留已达到更高版本的 Pods 配置。

### Breaking

- ISDKManager 新增插件状态查询、定向等待和登录会话方法；自定义 ISDKManager 实现必须补齐成员。ISDKPlugin.Priority 不再控制初始化先后，插件间顺序约束须改用 ISDKInitializationDependencies 显式声明。

## [0.6.34] - 2026-09-23

### Fixed

- WebGL Host 的首包布局判断改为读取 Player 构建后处理生成的 `StreamingAssets/nova-webgl-layout.txt`；纯 CDN 不再请求不存在的 `BuiltinCatalog.bytes`，避免微信小游戏开发者工具输出预期 404，旧 Player 仍保留兼容探测。
- SDK 插件发现仅在 Editor Play 检查测试程序集引用，Player 不再因 HybridCLR 程序集解析 `netstandard` 失败而中断 SDK 初始化，也无需为此向 `link.xml` 添加 `netstandard`。

## [0.6.33] - 2026-09-23

### Breaking

- 删除已完成使命的 BestHTTP 自动卸载迁移器、旧 adapter 编译桥与条件公开兼容 API；框架内部 HTTP 传输契约恢复为 `IHttpTransport`，仍固定使用 UnityWebRequest。仍装有旧 BestHTTP/TLS 包的消费项目需先自行移除这些依赖再升级。
- 删除 Config schema 0→1、Globals v1→v2、旧序列化字段别名与 Cloudflare `PurgeURL` 兼容逻辑；现存 Sample 配置已统一保存为当前结构，旧项目必须先使用历史 Framework 完成迁移或重新生成配置。

### Added

- ConfigWindow 支持由 SDK Editor 程序集声明配置面板快捷动作，在不建立 Framework 到具体 SDK 依赖的前提下，为 SDK 专属配置窗口提供字段上方入口。
- Pipify 每个 Step 新增内存复制入口，“+”菜单顶部可将完整 Step 配置粘贴到任意 Batch，并支持重复粘贴且不与来源共享编辑状态。

### Changed

- Pipify Runner 在 CLI 覆盖和 Active BuildTarget 同步后，统一展开所有 Step 参数快照中的 `{Platform}`、`{Channel}`、`{Package}`、`{Version}` 与 `{Time}`，不再要求各 Step 重复实现。

### Fixed

- 更正 0.6.31 / 0.6.32 历史发布记录：FileFragment 的版本化完整性封套与崩溃恢复实际随 0.6.32 发布。
- UPM Sample 发布链路不再复制含本机绝对路径的可重建 `nova-export-manifest.json`，并在临时副本中脱敏 DataMaster 密钥、Android keystore 路径及 Office 作者/时间元数据；signed tarball 增加开发期描述符残留门禁。
- RuntimeDebugger 支持由 `DebugComponent` 注入 UGUI 字体；WebGL / 微信小游戏可绑定实际包含中文字形的字体，避免 Editor 正常而 Player 中文缺字。
- WebGL Host 的 `BuiltinCatalog.bytes` 布局探测在纯 CDN 模式未命中时改由 Nova 输出明确的 Warning 并继续使用纯 `WebNetwork`；开发者工具 Network 面板仍保留真实 HTTP 404。
- `nova-create-sample` 现会为新 Sample Scene 自动生成与 Runtime asmdef 同命名空间的 `TableTablesBinding` override，并同步重命名可选的 Running 程序集及 DLL 产物；MainDemo Binding 模板缺失时立即中止，避免生成带错误类型或重复程序集名的 Sample。
- Pipify Step 行内的参数配置、复制和删除按钮改为垂直居中，修复控件整体轻微上偏。
- HybridCLR 在 WebGL 生成 AOT 裁剪 DLL 时，若 Player Settings 指向不存在的 `PROJECT:` 模板，会仅在临时 script-only Player 构建期间改用 Unity 默认模板并在结束后恢复，避免无关的网页模板校验阻断 `GenerateAll`。
- WebGL Bundle/RawFile 使用 `BundledCopyOption=None` 时改为按 Unity 资产语义删除当前 Package 的旧 `StreamingAssets/yoo/<PackageName>` 及 `.meta`，避免空目录元数据残留导致纯 CDN 导出误判；不会清空整个 `StreamingAssets`。
- Pipify 在执行前统一拦截包含未注册 Step 的 Batch，避免前序步骤已执行后才失败；窗口会以红色标记 Missing Step、禁用运行按钮，并在按钮下方提示安装并确保对应 Package 编译成功或删除缺失步骤。
- SDK 运行时插件发现排除 NUnit 与 Unity Test Runner 测试程序集，避免 Editor Play 把测试夹具识别为真实插件并输出误导性配置声明警告。
- HybridCLR Sample 自动同步缺少当前平台源产物时，日志改为“HybridCLR 热更新 DLL 同步”，并明确 Config 中的 DLL 列表只是复制与加载映射，不代表 DLL 已生成。
- SDK 插件在 Unity Editor 中明确报告平台不支持时改为警告并跳过，保持插件不可用且继续初始化其他插件，不再记录误导性的初始化错误。
- ProjectGuard 新增业务程序集目标平台检查；Demo 的 Runtime asmdef 明确排除当前平台时，会在进入 Play 前说明平台不受支持，`ProcedureLoadDll` 也会在已配置程序集未进入运行环境时给出平台与 asmdef 排查方向，不再误报为仅缺少 `StartupGameDlls` 配置；同时为依赖移动广告链的 AdDemo 补齐 WebGL 排除约束。
- WebGL 文件系统同步改为串行合并重叠的 `FS.syncfs` 请求，并将 IndexedDB 同步失败写入明确日志；FileFragment 只在正式分片完成替换后提交同步，避免一次保存重复发起异步落盘。
- WebGL 文件系统同步在微信小游戏等无 IndexedDB 的宿主中改为会话内受控降级，不再调用宿主不兼容的 `FS.syncfs` 错误路径并终止 Unity 主循环。

## [0.6.32] - 2026-09-20

### Changed

- `nova.project.player.build` 在既有 Active BuildTarget、场景、ProjectSettings、构建模式与精确输出路径冻结契约下开放 WebGL Player 目录构建；Android 与 iOS 仍保持关闭。

### Fixed

- Generate Actions 接受合法的 `ChannelType.None` 坐标，WebGL 的 HybridCLR 预构建不再被错误拦截。

## [0.6.31] - 2026-09-18

### Added

- WebGL 新增 `TagsOnLaunch`（默认）、`OnDemand` 与 `AllOnLaunch` 三种资源运行策略，以及有明确 Bundle Handle 所有权的 `IAssetWarmupGroup` 和 `CreateWarmupAll` / `CreateWarmupByTags` / `CreateWarmupByLocations` API。

### Changed

- 网络协议不再输出请求或响应的通用 JSON 包装日志；应用配置成功后仅输出一条包含响应 Proto JSON 的结果日志，失败继续使用明确的 Warning/Error 表达。
- CDN 资源部署与白名单部署移除“上传前清理”选项，统一直接上传并覆盖同名 OSS 对象，避免删除与重传之间出现短暂资源缺失。
- WebGL Bundle 与 RawFile 构建重新开放 `None`、`ClearAndCopyByTags`、`ClearAndCopyAll` 三种首包布局；运行时以 YooAsset 官方 `BuiltinCatalog.bytes` 为依据，让 Host 自动选择纯 CDN 或 StreamingAssets + CDN，Offline 固定使用纯 WebServer，纯 CDN 不再进入首包 Manifest 回退。
- 启动路由把常规资源补丁与 WebGL 启动 Warmup 分开：`OnDemand` 跳过 `ProcedureHotfix`，`TagsOnLaunch` 预热后释放并在启动 DLL 消费完成后清理未使用内存，`AllOnLaunch` 持有到 AssetManager 关闭；两种 Warmup 不受 `EnableHotfix` 关闭常规热更的影响。
- WebGL Bundle 请求超时同时覆盖 StreamingAssets 与 CDN 来源；`.version`、`.hash/.bytes` 继续使用各自独立超时，并复用现有主备、重试、最近成功域名和 UWR 埋点配置。
- Asset Inspector 将终端加载模式说明合并为单个、随 Active BuildTarget 切换的 HelpBox；Pipify 的 WebGL 首包提示同样随平台和当前拷贝选项动态显示，只保留使用者需要的选择要求、资源准备和选错影响。

### Fixed

- FileFragment 改用临时文件校验后替换并保留上一版 `.bak`，新增版本化完整性校验、旧格式兼容和坏档隔离恢复；AES padding 或单分片损坏不再阻断整个 Persist 启动链。
- Pipify 的 `export.config.Channel` 下拉不再显示 `None`；旧 Batch 中遗留的 `None` 会在打开参数区时归一为 `Official` 并提示保存。
- SDK 配置 DTO 所在 Runtime 程序集在 WebGL 下保持可解析，避免保留但未启用的 Google Sign-In、Apple Sign-In、MAX、AIHelp 与 IAP 配置在加载 `ConfigMasterSO` / `ConfigRuntimeSO` 时产生 Missing types 警告；原生能力是否运行仍由当前平台配置决定。
- FileFragment 在 WebGL 下恢复已有 `.dat` 分片时改为主线程逐文件读取并分帧执行，避免首次落盘后刷新页面因线程池不可用而永久停在预加载 0%；Android、iOS 与 Editor 继续使用线程池并行读取。
- WebGL 文件删除失败时不再等待 finalizer 或休眠重试，避免异常分支阻塞浏览器主线程；其他平台保留原有句柄回收与重试行为。
- WebGL 下 App 更新开关固定关闭并跳过大版本检查，避免进入当前仅面向 Android/iOS 的商店跳转或安装包下载流程；Asset 资源流程保持独立。
- WebGL 下“失败/取消时强制退出”固定关闭，Android/iOS 保持原配置；App 与 Asset 面板的配置项禁用时，对应 HelpBox 现在会同步灰显。

### Breaking

- Config 的 Platform 坐标禁止 `None`；Channel 的 `None` 保持为合法的无渠道坐标。ConfigWindow、导出、校验、构建预检、Agent Action 与 ConfigRuntime 加载均应遵循该边界。
- 删除未投入使用的 `PreloadAsync`，不保留兼容转发。调用方需迁移到 `IAssetWarmupGroup` 并明确释放；仓外自定义 `IAssetManager` / `AssetManagerBase` 实现需补齐三项 `CreateWarmup*` 成员。
- 删除从未被启动流程读取的 `AssetManagerConfig.AutoHotfix` 与 `AssetComponent.m_AutoHotfix`；Android、iOS 和 WebGL 启动资源准备仍在进入 `ProcedureHotfix` 后立即开始。

## [0.6.30] - 2026-09-15

### Changed

- HostKey + NetCmd 业务请求候选链耗尽后的“通信失败 / 结果未确认”终态日志降为 Warning，避免普通网络失败被 TGA `ta_app_crash` 误记为应用崩溃；网络诊断继续使用 `uwr_request_start/error/end`。

### Fixed

- ConfigWindow 保存复制或旧版本遗留的维度矩阵时，可按当前编辑坐标一键归一实际冲突组并重新校验后保存；用户弹窗只说明原因、影响与操作建议，字段路径和逻辑键仅写入 Console / Editor.log；已勾选维度继续保留独立分支，SDK / Kit 托管配置保持逐格深拷贝，避免用户陷入无法保存且无有效恢复入口的死循环；插件实例 Ensure Action 增加写盘前预演，拒绝生成新的维度不一致。

## [0.6.29] - 2026-09-14

### Added

- `Nova.Asset` 新增同步 `TryIsDeviceInStartupWhitelist`，允许业务查询本次启动已拉取的默认包白名单，并区分数据不可用与明确未命中。

### Fixed

- Asset 下载策略为同一文件的重叠请求分配独立执行状态，避免新请求在旧请求回调交接窗口命中 `CandidateInFlight` 并报错。

## [0.6.28] - 2026-09-14

### Fixed

- `SDKComponent.InitializeTask` 改用多等待者共享完成源，修复初始化尚未结束时多个调用方并发等待会抛出 `Already continuation registered` 的问题，同时保持初始化只执行一次及完成结果可重复等待。
- ConfigWindow 的未勾选维度会在普通字段编辑时立即广播；勾选或取消维度会原子投影整个矩阵的全部逻辑组，新增枚举行按各面板 Mask 继承既有逻辑组，保存仅校验并完整落盘 WorkingCopy，Exporter 同步阻断同组不一致，避免 Android 与 iOS 等不同物理格混入新旧配置；保存门禁弹窗延迟到 GUI 绘制结束后展示。

## [0.6.27] - 2026-09-04

### Changed

- ProjectGuard Play 阻断提示按应用、隐私、SDK、Kit 等用户可见类别合并，并根据占位值、格式错误或导出差异说明真实原因，不再重复列举同类问题或推断用户主动修改过配置。
- SDK Inspector 生成的打点汇总表区分 Framework 说明页与事件页；事件表头恢复为 11pt 居中样式，数据行不再误用二级表头的加粗字体。
- Network Inspector 将默认 HTTP 超时字段改称“业务请求超时时间（秒）”，并同步调整帮助说明；运行时超时语义不变。

## [0.6.26] - 2026-09-04

### Breaking

- 删除未投入使用的 AssetBundle 解密占位契约：`AssetDecryptorType`、`OffsetBundleDecryptor`、`AssetManagerConfig.DecryptorType` 与 `AssetComponent.m_DecryptorType`。
- 删除 `AssetPlayMode.WebPlayMode`；WebGL 继续使用 `HostPlayMode`，底层文件系统由框架按平台自动选择。
- `IUIManager` 新增 `OnOpenUIViewFail` 事件；仓外自定义实现需要补齐该成员。

### Added

- WebGL HostPlayMode 增加远端 Bundle 单次物理请求超时，并在远端 Manifest 候选耗尽后回退随 Player 发布的首包元数据；Bundle 仍保持常规远端按需加载。
- BuildReadiness 增加场景渠道快照校验；Asset/App 渠道与已导出的 ConfigRuntime 不一致时阻断 Nova Player 构建流程，并提示重新导出和保存场景。

### Changed

- Asset Inspector 按平台互斥编辑 WebGL Bundle 请求超时与非 WebGL 单文件字节流入超时，并补充启动必须资源与首包 Tag 一致性的提示。

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
