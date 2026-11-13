---
name: nova-project-router
description: Use when 处理 Unity 项目的开发、界面、资源、配置、存档、构建或排障任务，需要先识别是否使用 Nova；用户未提 Nova 也应检查。已接入或明确要求 Nova 时先选择 Nova 能力，目标明确可直接进入对应 Operation。
---

# Nova 项目任务路由

触发后先读取当前 Framework 的 [Docs/START_HERE.md](../../../Docs/START_HERE.md)，作为所有 `nova-project-*` Skill 的共同底线。

再读取 [references/contract.json](references/contract.json)，然后只读当前消费项目和 Framework 包内 [Docs/INDEX.md](../../../Docs/INDEX.md)。目标是产出可执行的边界与下一步，而不是直接修改项目。

## 渐进式披露

- L0：只用 frontmatter、Catalog 与本页共同底线判断是否命中。
- L1：命中后只读取本 Skill 与 [references/contract.json](references/contract.json)，冻结任务范围和副作用。
- L2：仅在候选路径确定后，读取该路径所列的模块 Docs 或目标项目事实；不要先展开所有 Skill。
- 查询“有什么功能 / 参数是什么意思 / 对应工具在哪里”时，先复用随包 [Agents/Tools/nova_capabilities.py](../../Tools/nova_capabilities.py) 的只读 `inventory --detail summary` / `describe --detail summary`，锁定精确任务或函数后才用 `describe <id> --detail full` 展开必要契约与正文；不一次加载全部任务契约，不把参数查询扩大成配置修改。
- L3：只把已确认的输入交给被选中的 Action Adapter；Router 自身不写入项目。
- L4：只交付本次路由所需的定位证据，不把路由结论冒充业务功能验证。

## 路由步骤

日常原则已由首段共同入口统一读取，不需要先选研发 Skill。仅在需要比较玩法方案、梳理复杂需求变化或阶段依赖时，转入[复杂需求梳理](../nova-project-development/SKILL.md)；目标明确后直接进入具体任务，避免两个 Router 相互调用。明确的单点操作继续直接路由对应 Operation，不展开方法全文。

仅项目自有普通业务源码的文案、常量等局部字面量，且不触及框架调用、配置/表源、序列化资产或生成链时，定位后结束本 Router 的只读阶段，再按项目源码编辑规则完成已授权修改和局部检查。不为这类小改强套页面 Operation 或研发 Skill；配置、资产和正式导出仍走原专用合同。

0. 先按 [NA-002：先核对当前能力，再选择接口或提出补充](../../../Docs/Onboarding/DEVELOPMENT_CONSTRAINTS.md#na-002先核对当前能力再选择接口或提出补充) 核对工程的有效依赖绑定或实际 `PackageInfo`、`com.solotopia.nova.framework` 的身份、版本与包根。用户只说 Unity 或一般功能名也执行此步；已核实且未变的同工程上下文可复用。包待解析不等于未接入；明确要求 Nova 的新项目进入准备链。确认未接入且无 Nova 要求时返回 `not_applicable`，再交通用 Unity 指导。不得为识别全扫工程或加载全部契约。
1. 区分项目组任务与框架组任务。新增 Runtime 模块、Inspector、SDK Plugin、Framework API 或修改 Framework 包源码都标为 `not_applicable`，说明应转交框架维护入口；这不意味着可以绕过 Nova 改走通用 Unity 实现。
2. 冻结目标、消费项目路径、目标平台/渠道/模式、目标场景或产物，以及允许的副作用。缺失且会改变方案时，提出一个最小澄清问题。
   新建游戏或整套交付按 [新项目开工准备](../../../Docs/Onboarding/PROJECT_STRUCTURE.md#新项目开工准备) 把目标拆为有依赖的既有 Operation，为各阶段列出匹配 Skill/contract、真实 Adapter、输入来源、缺项与最低证据；只展开即将执行阶段的文档，不预先加载全部 Skill。配置创建、首次填值和正式导出均交对应 Skill，不把未路由部分交给通用 Setup 脚本；Router 自身保持只读。
3. 消除常见歧义：

| 用户词 | 必须确认 |
|---|---|
| 新建 Nova 项目 / 开始做游戏 / 开工准备 | 按 PROJECT_STRUCTURE 的[三工具基线](../../../Docs/Onboarding/PROJECT_STRUCTURE.md#configpipifyplugpals-基线) 配置并实际使用 Config（ConfigMaster）、Pipify、PlugPals；就绪检查发现缺项后转配置/流水线/包管理既有入口，不能把工具标可选。按标准产品预加载基线完整落实 MainDemo 八项动作，先准备环境、固定 Resources、配置/导出，再验证并开展依赖业务；不以体量小、单机或当前只用部分模块裁减。已有项目局改按影响面核对，不自动迁移。HybridCLR 包、安装、启用、产物和运行分别判断 |
| PSD / PSB / PSD2UGUI / AI 转界面 | 查询、设计源同步、树编辑、生成或导图、AI识别/应用分别定位；先读取包与配置来源、persistentId和输出记录，普通生成不默认运行AI；业务UIView接入继续既有页面Skill |
| 新游戏 / 页面 / 界面 | 只建 View，还是还需 Prefab、UI 注册、入口与运行验证；布局实施前按 UIComponent 文档发现并冻结屏幕适配方案，不能直接套用 Demo |
| Unity 无响应 / 工具超时 / 启动弹窗 | 先按共同入口检查真实窗口、编译/导入进度和进程归属；只读路由仅报告，已授权开发由后续执行步骤及时处理窗口，不以反复重启替代诊断 |
| 功能清单 / 参数解释 / Inspector 或 Window 选项 | 查询源码声明与默认值，还是当前项目有效值与配置修改；查询结果不证明已运行或已自动化 |
| 表 / 配置 | Luban、UI、Localization、Network、Sound、Vibrate、ConfigMaster，还是项目自有SO/Prefab字段；表源修改不等于生成物修改 |
| 日常流程 / 批量自动化 / Pipify | 维护已保存Batch配置，还是实际运行；目标Settings、Step顺序、字段与运行副作用分别冻结 |
| 按目录生成图集 / 构建前更新图集 | 是否主动选择项目图集生产；配置、来源/排除、输出归属、预演或生成/恢复分别冻结，旧 Batch 不自动插入步骤 |
| 批量绑定控件 / 自动声明 UI 字段 | 已有普通 Prefab 的真实 UIView/对象引用，还是 PSD 布局生成；字段绑定与可选 partial 生成分开，生成后编译再新计划绑定 |
| 打包 / 发布 | Bundle、Player、CDN、商店、UPM；外部写入或 Git 不因名称自动获授权 |
| 上传 Google Play / AAB / supply 报错 | 配置、真实上传（含仅验证）还是只读排障；AAB/包名、轨道、用途、凭据账号及完整 Batch 范围是否已确认 |
| 资源更新 / 热更 | 构建 Bundle、部署 CDN、运行时下载或缓存清理 |
| HybridCLR / 热更 DLL | 业务 DLL 本地刷新、full AOT、Bundle、Player、CDN 或运行时诊断；不得把这些目标合并成一次操作 |
| 装包 / 升级包 / 卸载包 | 最新 registry 版本的单包安装/升级、direct dependency 卸载，还是指定版本、降级、来源切换、批量升级或 Framework 发布 |
| 接 SDK / Kit | 是否为已发布且包内文档可发现的单一 Nova SDK/Kit；包名、安装/升级意图、三维坐标、平台前置和最小本地探针是否已确认 |
| 事件 / 消息通知 | Event 事件还是网络消息；载荷、发布者、订阅拥有者、Fire/FireNow、线程与注销生命周期是否已确认 |
| 存档 / 本地保存 | PlayerPrefs、FileFragment 或 SQLite；classify/item、保存时机、清理范围、平台和加密前置是否已确认 |
| 金币 / 体力防改、关键整数内存防护 | 现有数值 owner、可信初值、int/long 类型及失败后的结算/保存边界；仅内存接入不要求存储选型、classify/item 或 AES 密钥 |
| 加载场景 / 切换关卡 | 入口 Scene、Build Settings、Nova 资源化 Content 场景，还是普通资源/Prefab；Package、location、mode、Handle owner 与卸载时机是否已确认 |
| Android 依赖 / EDM4U / GeneratedLocalRepo | UPM Resolve、EDM4U Force Resolve、Gradle 构建诊断还是清理缓存；当前 Android Target 与依赖图是否已确认 |
| 登录 / 领奖 / 业务 API / HTTP取消 | 先区分新协议或路由/认证接入，与既有单URL请求/页面Owner取消维护；前者冻结真实HostKey/NetCmd、认证与账号，后者复用原请求参数、`Nova.Network` 当前可用的取消重载、Owner/token与受控探针，不无条件要求表导出或账号 |
| BGM / 背景音乐 / 点击音效 / 声音 | 真实 AudioClip、Collector、地址、Sound 表、声音组、加载入口、业务触发、停止生命周期和实际播放成功探针是否都已确认 |
| 振动 / 震动 | Emphasis、Custom 或预设；数据源、Unit、Collector、业务触发、停止责任、目标真机和体感探针是否都已确认 |
| 流程 / Procedure | 业务 Procedure 还是框架启动链；业务程序集、进入/离开、异步取消、下一状态和 Play 探针是否都已确认 |
| 开始游戏 / 结算 / 重开 / 回大厅 | 当前业务 Service、入口与 Procedure/FSM 的起点/终点、局身份、结果/奖励、重开保留范围和退出清理是否已确认 |
| 暂停 / 恢复 / 倍速 | 框架全局速度与暂停缓存，还是单局/页面业务状态；输入、音频、计时、资源与网络是否跟随，由谁持有和恢复 |
| 启动不了 | Editor 编译、Play 门禁，还是 WebGL 浏览器页面/loader/Wasm/data、Unity/Nova 加载链、网络、真机进程或服务端结果 |
| 能不能打包 / 打包前检查 | 只读构建前置检查、真正构建 Bundle/Player，还是分析一次既有失败 |
| 真机闪退 / SDK 无回调 | Android/iOS 设备、Bundle ID、时间窗、日志源和脱敏规则是否已冻结；诊断不等于安装、启动或修复 |

4. 选择最窄的路径：
   - 新项目、完整打包或 HybridCLR 基线修正：先按[三平台构建与 HybridCLR 基线](../../../Docs/Onboarding/PROJECT_STRUCTURE.md#三平台构建与-hybridclr-基线)路由配置、正式设置入口、生成、DLL 部署与流水线；HybridCLR 是所有 Nova 项目必备项，不按小游戏/单机/离线自行省略。只读路由不执行安装或构建，已有项目局改不自动扩成整项目迁移。
   - 只查功能、参数声明、源码/文档及技能执行入口：直接使用下面的共享查询 CLI；Router 交付查询和路由结果，不强制进入写入 Operation。
   - 范围或项目事实不清：`nova-project-check-readiness`。
   - 编译失败、Play 门禁、黑屏或启动链异常：`nova-project-diagnose-startup`；已有 WebGL 页面启动失败先按其浏览器参考只读 loader → Wasm/data → Unity/Nova，不自动启动服务、刷新页或清缓存。
   - 入口场景、Build Settings、Nova 根或 Content 职责：`nova-project-setup-entry-scene`。
   - 一个已确认三维坐标的 ConfigMaster / ConfigRuntimeSO，或其已有面板字段/明确维度轴：`nova-project-configure-runtime`；非规范Master复用其中的独立 `normalize-master` 路线，明确合并保留锚点和整Master差异，整理后重新查阅及计划字段修改。
   - 项目自有 SO、Scene、Prefab 的 Inspector 字段、内置组件、变体/嵌套或已核实 UnityEvent：`nova-project-edit-settings`；普通资产优先受控 Action，场景及复杂 Prefab 按共享 Unity MCP 参考执行。ConfigMaster/ConfigRuntime/PipifySettings 仍走各自专用入口。
   - 创建、克隆、改名、排序或配置已保存Pipify Batch：`nova-project-configure-pipeline`；默认只保存，实际运行另冻结完整Batch副作用。
   - 项目明确选择按目录生成图集或恢复其未完成事务：`nova-project-configure-pipeline` 的 [图集参考](../nova-project-configure-pipeline/references/sprite-atlas.md)，复用 `spriteatlas.generate` / `spriteatlas.recover`；默认预演，不接管已有图集/Batch。
   - 一个已配置 registry 包的最新版本安装、同源最新版本升级或 direct dependency 安全卸载：`nova-project-manage-upm-package`；任务所需新包可按 [NA-014：授权范围、实时合同和恢复回执约束实际执行](../../../Docs/Onboarding/DEVELOPMENT_CONSTRAINTS.md#na-014授权范围实时合同和恢复回执约束实际执行) 的长期安装授权主动选定并执行，不逐包重复询问。指定版本、降级、来源切换、批量升级、Framework 自身或发布不进入该 Operation。
   - 当前消费项目的 direct Nova Framework 需要从同一 registry 升级到最新或明确更高版本：`nova-project-upgrade-framework`；必须由跨 reload 独立的包外 UPM 宿主执行，Framework 内部 Action 不得自升级。
   - 已发布 Nova SDK/Kit 的安装、配置、登录、支付、广告、云存档、推送、统计或平台业务接入：`nova-project-onboard-sdk-kit`；按目标包和任务只读相应 reference，复用已有公开 API 与项目自有业务源。已有 Excel 支付商品先只读转换，再复用 Config 面板保存及正式导出；厂商后台、凭据和设备结果分别验收。
   - 只需导出当前TableSettings、修改已有非敏感xlsx/csv标量，或给已有自描述xlsx追加普通字段后正式导出：`nova-project-export-tables`；源修改与普通字段追加复用同一 `nova_table_source` CLI 的明确分支。字段类型、行键与默认值从真实表确认，复杂Schema不扩入该分支。
   - 已确认 Luban 表源、导出和运行时读取链，含已有自描述xlsx普通字段追加并在游戏读取：`nova-project-integrate-table`；复用源CLI和正式导出，不手改生成代码，不把Sample表当未知业务目标。
   - 已有文本/字体修改、明确追加 Key/语言或语言切换：`nova-project-update-localization`；源结构追加复用其共享 CLI 严格分支，删除/改名及复杂字体不扩入该分支。给明确页面集合补齐 `TextLocalizing` 与引用则复用 `nova-project-update-ui-view` 的本地化绑定参考，逐对象保存并覆盖本次请求语言集合验证。
   - PSD/PSB 转 uGUI、PSD2UGUI 配置/来源查询、树同步与节点调整、图片导出、AI识别及应用：`nova-project-psd2ugui`；先由 inspect 发现实际目标，写入按 run 的固定 Schema。它独立管理设计源到生成物的转换，需要 Nova 业务页面时再接已有 create/update-view；缺商业授权或AI CLI只阻断相应分支。
   - 新建或注册业务 `UIView`：`nova-project-ui-create-view`。
   - 已注册 View、Prefab 或 UI 注册内容的定向调整：`nova-project-update-ui-view`。
   - 既有普通 Prefab 批量控件引用或明确选择机械字段生成：`nova-project-update-ui-view` 的 [控件绑定参考](../nova-project-update-ui-view/references/control-bindings.md)，复用精确 Inspect/configure Action；不支持的变体/嵌套继续原编辑路径。按次清理或可等待打开同属既有页面 Skill 的可选分支，旧页面无需迁移。
   - 已有数据表驱动业务页面并从现有入口打开：`nova-project-data-driven-ui`。
   - 已确认业务资源的 Collector、地址、加载和释放链，对象池接入、缓存维护、加密或密钥轮换：`nova-project-integrate-resource`；按需读取生命周期或加密参考，配置、生成、构建和运行分别验证。
   - 图片异步替换/图集子图绑定，或从指定内容包加载：`nova-project-integrate-resource` 的 [生命周期参考](../nova-project-integrate-resource/references/resource-lifecycle.md)，分别选 SpriteBinding 或 `Load*FromPackage*` 门面；图片助手当前只用默认包，旧加载/释放无需迁移。
   - 业务存储、关键整数内存防护、存档诊断、值 AES、SQLite Cipher 或 Privacy 默认 AES 轮换：`nova-project-integrate-persistence`；金币/体力防改按其内存防护参考复用 Core 公开 API 与业务 owner。密码与 Privacy 分支仍复用同三 Action，限定当前macOS Editor与精确数据/配置范围，设备与外部消费者独立验收；资源密钥、App AES与业务schema分别路由。
   - 已配置 HybridCLR，且只需在当前 activeBuildTarget、DevelopmentBuild 与激活 ConfigMaster 当前坐标下完成业务 DLL 本地 compile -> copy 整批刷新：`nova-project-refresh-hotfix-dlls`；full AOT、Bundle、Player 及 CDN/运行时诊断分别路由，不扩入该 Operation。
   - 已配置 HybridCLR，且需要按当前 Target、DevelopmentBuild 与激活 ConfigMaster 当前坐标执行 `Generate All` 与 link.xml 验证：`nova-project-generate-hybridclr-artifacts`；它不 Copy DLL、不构建最终 Player，也不替代最终 Player 后的 `CopyAotDlls`。
   - HTTP API、WebSocket 或已有普通请求的Owner取消/响应回收维护：`nova-project-integrate-network-api`。新协议或路由/认证变化才冻结相关HostKey/NetCmd、导出、账号与重放语义；既有单URL维护复用原参数、`Nova.Network` 门面与生命周期，不强制导表、Network加载或账号。WebSocket按连接、认证、心跳、重连与响应生命周期参考执行；可读项目事实先核，缺真实业务选择再澄清。
   - 加入声音表、真实 AudioClip、Collector、地址、声音组与播放/停止行为：`nova-project-integrate-sound`；从项目发现已有加载 Owner，缺现成触发代码时在授权业务源范围补齐，缺真实资源则保留依赖缺项，不以 serialID 代替实际播放与停止。
   - 已确认 Emphasis / Custom 振动数据、Unit、地址、Collector、业务触发、唯一停止责任、目标真机和体感探针：`nova-project-integrate-vibration`；没有真机反馈时最高为 `partial`。
   - 已确认业务程序集、业务 Procedure、进入/离开职责、异步取消、下一状态和 Play 探针：`nova-project-integrate-procedure`；复用 `ProcedureLoadDll` 延迟注册，不建立第二注册器。
   - 开始游戏、结算、重开或回大厅：`nova-project-integrate-procedure` 的 [references/business-flow.md](../nova-project-integrate-procedure/references/business-flow.md)；从实际入口复用项目现有业务 Service 与 Procedure/FSM。不存在 `Nova.Game` 门面；缺业务 Service 且创建正式项目实现属于现有授权时，补齐最小业务胶水，缺玩法决定或外部协议则报告对应缺项。
   - 暂停、恢复或倍速先确定拥有者：框架全局 `Nova.Self` 速度/暂停缓存走 `nova-project-edit-settings` 的 [references/app-and-debug-maintenance.md](../nova-project-edit-settings/references/app-and-debug-maintenance.md)；单局或页面的业务状态、输入/计时/资源等走 `nova-project-integrate-procedure` 的业务流程参考。`Time.timeScale=0` 不证明所有调度、网络或异步操作停止，不把直接设速度与缓存暂停混为同一操作。
   - 已确认事件类型、载荷、发布者、订阅拥有者、分发模型、线程和注销生命周期：`nova-project-integrate-event`；池化事件实例不得跨 handler 或异步链持有。
   - 已确认存储实现、classify/item、Config 后 Persist 加载顺序、保存/清理语义和加密前置：`nova-project-integrate-persistence`。
   - 已有存档查询、迁移或清理：`nova-project-integrate-persistence` 的维护参考；备份、转换和恢复以实际实现为准，字段开关保存不代表旧数据迁移成功。
   - App 大版本更新、RuntimeDebugger、编译日志级别或相关宏：`nova-project-edit-settings` 的 App 与调试维护参考；参数、宏、业务分支和平台结果分别验证。
   - 现有埋点说明与源 Tracks 文字维护：`nova-project-onboard-sdk-kit` 的 Tracks 参考；复用表源 CLI，不手改汇总物，不把叶子编辑称为事件新增或后台验收。
   - 已纳入当前 Nova 资源配置的 Content 场景，且 Package/default-Package、location、LoadSceneMode、唯一 ISceneHandle owner 与卸载时机明确：`nova-project-integrate-content-scene`；入口场景仍走 `nova-project-setup-entry-scene`。
   - 实际构建前只读检查 Target、场景、Config、YooAsset Package 与 HybridCLR 前置：`nova-project-preflight-build`；它不执行构建或修复。
   - 已有 Player、Gradle、Xcode 或 WebGL 构建失败证据，需要定位最早失败阶段：`nova-project-diagnose-build`；不得借诊断重跑构建、Resolve 或清理。
   - Android SDK/Kit 变更后需要 EDM4U Force Resolve：`nova-project-resolve-android-dependencies`；该 Action 含 Destructive，必须绑定精确计划确认，不能退化为任意 C# 或删目录。
   - 已冻结 Android/iOS 设备、Bundle ID、时间窗、日志源与脱敏规则的真机日志诊断：`nova-project-diagnose-device-runtime`；不安装、不清数据、不启动应用、不修复工程。
   - 本地 YooAsset Package 产物：`nova-project-build-bundles`；WebGL、Android、iOS 的本地安装包/平台工程及已核验产物的本机预览或设备安装：`nova-project-build-player`，分别按构建、CLI 与平台验证参考冻结范围。
   - 配置 Google Play AAB 上传、执行已授权的上传 Batch，或排查 fastlane supply 权限/版本号/提交失败：`nova-project-upload-google-play`；诊断不重试，仅验证仍有远端上传副作用，不能当作本地构建或 UPM 发布。
   - 单一确定的 Operation 不强制经过 Workflow；多个写入操作仅在读写集无冲突时并行。
5. 读取所选 Skill 的 [references/contract.json](references/contract.json) 后，按真实 Adapter 状态标记执行方式：引用 `nova_project_action` 的分支只通过 live `describe(action_id)` 判断为“当前 MCP 可执行”；Tool 缺失或 `describe` 未返回目标 ID 时标记“传输未就绪 / 当前未开放”，不得根据静态文档猜测可用，也不得退化为任意 C#。不依赖 Project Action 的分支标记其实际 Adapter（只读检查、受控 Unity 编辑、CLI 或人工判断）。通用 Unity Skill 只补充所选路径的引擎操作，不改写 Nova 的业务职责、配置、生命周期与导出链；没有可用 Nova 入口时报告具体缺口。Router 只报告可用性，不调用 `plan` 或制造写入。
6. 明确不可做的事：不扫描主仓私有规则或其他工程补资料；需要设计依据时按[共同知识入口](../../../Docs/START_HERE.md#设计依据与历史知识)只读当前包的 Minds 索引和命中条目，不展开全库、不将历史命令当作当前合同。不把 Sample 整体当产品模板；Demo 临时物料按 [NA-014：授权范围、实时合同和恢复回执约束实际执行](../../../Docs/Onboarding/DEVELOPMENT_CONSTRAINTS.md#na-014授权范围实时合同和恢复回执约束实际执行) 核对配套后复用。不猜 UIGroup / 表字段 / 资源地址，不手写 Prefab 或 Scene YAML，不把 Pipify 顺序 Batch 当作可并行 DAG。

## 共享查询入口

新建或恢复 Excel 配表样式先按 [资源工作流](../../../Docs/Onboarding/RESOURCE_WORKFLOW.md) 选择项目既有同类表或当前包的官方模板。UI 与 Sound 的完整行填入路由既有 Skill 的 `source-from-template` 分支；Table、本地化、网络、振动的官方模板原样复制路由各自 Skill 的 `source-template-copy` 分支（同一 CLI 的 `mode=copy`），保留多 Sheet、合并和各模块主键，不套 UI/Sound 行模型。本 Router 只选择入口，不写表。Tracks、SDK/Kit 专用表及项目自定义表继续各自已确认的来源和编辑入口；所有表型都受 [NA-002：先核对当前能力，再选择接口或提出补充](../../../Docs/Onboarding/DEVELOPMENT_CONSTRAINTS.md#na-002先核对当前能力再选择接口或提出补充) 约束，没有保真能力时报告具体缺口，不能退回空白建表或统一美化所有模块。

先按 [框架位置与文档入口](../../../Docs/START_HERE.md#先确定框架位置与文档入口) 定位当前 Framework，把真实物理根赋给当前终端变量 `FRAMEWORK_ROOT`；macOS/Linux 使用：

```text
python3 -B "$FRAMEWORK_ROOT/Agents/Tools/nova_capabilities.py" --root <projectRoot> inventory --kind tasks --query <goal> --detail summary --limit 5 --json
python3 -B "$FRAMEWORK_ROOT/Agents/Tools/nova_capabilities.py" --root <projectRoot> inventory --kind features --detail summary --json
python3 -B "$FRAMEWORK_ROOT/Agents/Tools/nova_capabilities.py" --root <projectRoot> describe <task-or-function-id> --detail summary --json
python3 -B "$FRAMEWORK_ROOT/Agents/Tools/nova_capabilities.py" --root <projectRoot> describe <task-or-function-id> --detail full --json
```

Windows 将 `python3` 换成 `py -3`；必要时显式传 `--agents-root "$FRAMEWORK_ROOT/Agents"`。`--detail summary` 保留准确身份、导航与状态，省略重复的全文契约/正文；过滤和分页仍根据完整记录，不把摘要当作执行许可。省略 `--detail` 仍为 `full` 以兼容旧调用，日常路由显式选摘要。若当前已安装版本尚无该参数，先按其实际帮助分页定位，再只展开目标，不声明工具已升级。这些查询复用 [coverage.json](../../coverage.json) 的功能族、源码、Docs、既有 Skill/Adapter 与限制；源码参数声明和默认值不等于项目当前有效值，后者还需读取已确认的配置资产和运行状态。查询不会安装未使用 SDK，也不读取主仓 `.nova/` / `Minds/`。工具缺失、条目未覆盖或资料不足时明确说明缺口，不据此创建新顶层 Skill。`check` 是框架维护完整性检查，不作为消费项目每次查询的前置。

## 输出

输出 `success`（仅指路由完成）、`blocked` 或 `not_applicable`，并包含：冻结的输入、推荐 Skill、原因、真实 Adapter 可用性、计划写入集、确认门、最低验证证据和未验证项。路由 Skill 本身不应声称业务功能已完成。
