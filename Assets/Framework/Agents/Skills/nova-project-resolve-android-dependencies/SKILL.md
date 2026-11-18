---
name: nova-project-resolve-android-dependencies
description: Use when Nova 项目组在接入或升级 Android SDK/Kit 后，需要冻结 EDM4U 依赖图并受控重建、核验 Android 原生依赖输出时使用。
---

# Nova 解析 Android 原生依赖

触发后先读取当前 Framework 的 [Docs/START_HERE.md](../../../Docs/START_HERE.md)，作为所有 `nova-project-*` Skill 的共同底线。

## 渐进式披露

先读取 [references/contract.json](references/contract.json)。仅在确认 EDM4U 行为时读取 [Docs/Editor/EditorUtil/EditorUtil.AndroidResolver/EditorUtil.AndroidResolver.md](../../../Docs/Editor/EditorUtil/EditorUtil.AndroidResolver/EditorUtil.AndroidResolver.md)；仅在本任务来自既有 Pipify 流程时读取 [Docs/Editor/EditorUtil/EditorUtil.Pipify/PipifySteps.md](../../../Docs/Editor/EditorUtil/EditorUtil.Pipify/PipifySteps.md)；仅在需要理解 Action 的确认与恢复语义时读取 [Docs/Editor/EditorUtil/EditorUtil.AgentActions/EditorUtil.AgentActions.md](../../../Docs/Editor/EditorUtil/EditorUtil.AgentActions/EditorUtil.AgentActions.md)。不要展开所有 SDK、Gradle、HybridCLR 或构建文档。

## 范围与冻结输入

本 Operation 只处理消费项目当前完整 EDM4U Android 依赖图的 Force Resolve。它不安装或升级 UPM 包、不修改 registry、不切换平台、不构建 Player，也不证明 Gradle、SDK 初始化或真机运行成功。

先冻结唯一项目根、`activeBuildTarget=Android`、EDM4U 已加载状态、非空完整依赖图及摘要、受管写入边界和执行入口。仓库摘要数组保留原始优先级顺序，调换顺序也属于依赖图漂移。依赖图为空、EDM4U 不可用、当前平台不是 Android 或 Resolve 目标不明确时返回 `not_applicable` 或 `blocked`，不得以清目录来“重新试一次”。

项目内产物包括 EDM4U 状态文件、[Assets/GeneratedLocalRepo/**（定位）](../../../Docs/Editor/EditorUtil/EditorUtil.AndroidResolver/EditorUtil.AndroidResolver.md)、[Assets/Plugins/Android/**（定位）](../../../Docs/Editor/EditorUtil/EditorUtil.AndroidResolver/EditorUtil.AndroidResolver.md) 中 EDM4U 管理的依赖文件与 Gradle/Manifest 模板。正规 Force Resolve 还可能运行 Java/Gradle、下载 Maven 依赖、更新主机缓存、安装 Android SDK 包及显示设置选择窗口；因此 Action 声明 `ExternalRead`、`ExternalWrite`，契约主版本为 `2`。不能把固定项目输出目录当成完整副作用边界。

确认前必须绑定当前解析设置、模板、Java/Gradle/Android SDK 的实际路径与身份、可删除的完整 `gpsr` 标签文件集合、外部下载与缓存/安装目标。当前 Plan 未完整冻结这些输入时返回 `blocked`，不得把用户对功能开发的同意当成对不明外部目标的执行许可。仓库 URL 只进入 SHA-256 摘要，不能把私有 Maven 地址、userinfo、query Token 或凭据写入 Plan、Receipt、日志或报告。

## 执行入口

首次准备或只读检查发现“模板开关已开启但文件缺失”时，先报告准确缺失路径及模板准备前置缺口。新项目由 NovaSpark 在首次包解析前准备；已有工程的维护代码可显式调用底层 `EditorUtil.AndroidResolver.PrepareTemplates()`，但它不是独立开放的 Project Action，没有菜单入口，不以任意 C# 或第三方私有方法替代消费端受控工具。该方法核对七项目标文件及 meta/`.DISABLED`、七项发布布尔原值与当前 Unity Android module 官方源，保留已有内容/GUID及 Custom Base Gradle 选择；非法内容、双份停用副本或官方源缺失时写前阻断，失败恢复本轮变更。不调用 Force Resolve，不安装包或改变 JDK 配置。完成准备后核对七文件、XML/非空规则、GUID与开关，再新建正式解析 Plan。详细来源及回滚规则见所属 AndroidResolver Docs；准备中的正常导入仍会触发项目既有回调，不能把准备完成称为 Maven 下载成功。

正常刷新可能重建 UI Toolkit 的四类引擎派生资源，含动态 Atlas 页直接持有的复制材质。新 Plan 只在精确模块、既存面板及字段引用证明归属后声明该效果；写前保持完整角色状态，写后仅接纳已证角色重建，原 dirty、输入纹理、项目资产与其它对象仍保护，全部原对象行保留。未知归属继续阻断；旧回执缺少前后 owner-map 时沿用原摘要与严格比较，不追溯升级 partial。维护方细节按需查看 AndroidResolver 文档，不按名称或 hideFlags 泛化放行。

当前 Action 支持已审官方 UPM EDM4U 1.2.188/1.2.190；按真实方法派发、源码/DLL 指纹、自然调度和非目标保护判断是否可执行。当前仅接纳已有模板与本地 SRCAAR/POM 支路；Java 只读版本检查，下载、Gradle 执行与 SDK 安装尚不属于该可执行支路。未知类型、状态、消费者、队列或容量具体 `blocked`，不得关闭设置、物化 getter、预先导入或调用私有回调来消除阻断。历史 Receipt 缺完整刷新证据、域重载或写后验证不完整时最高为 `partial`。

源码编译包按维护方随包审核的版本、包依赖、asmdef和完整编译源集合核对当前真实来源，不依赖某个消费项目的 DLL/MVID 常量。当前 DLL/MVID、编译条件、引用、响应与分析器身份仍进入本次 Plan，漂移拒绝；消费端不自动更新 reference 或接纳未知回调。官方预编译 EDM 的版本/路径/DLL/MVID采用独立精确发行合同，未知版本清楚报告尚未适配。完整路径域继续排除无点结尾的 shadergraph/shadersubgraph、meta与父目录。

当前正常 URP 17.4.0 管线绑定采用独立自然支路合同，冻结 Graphics/Quality、管线资产与 Global Settings；要求正规材质升级检查已完成且无待保存/升级工作。本次模板域不命中材质或 GraphicsSettings 时才可继续，不通过清空管线配置、禁用 AI、取消异步任务或调用重建来通过门。AI 回调尚未支持的异步写支路须精确报告，来源匹配不等于执行成功。

五类 AI 生成 Store 使用独立冷态/后态合同：无 Store 时只接纳缺失或精确已保存默认设置，不调用创建型getter；已初始化 Store 的设置文件按实际内容冻结，不强制默认文本或冷态文件大小限制。两态均保护原文件字节、长度和mtime。仅本次真实非空 imported 数组允许建立默认 Store、准确两类对象及原厂监听/退出订阅。原对象行和dirty仍完整保护，Receipt保存实际后态，Verify严格比较，不能沿用冷许可再次替换实例。冷态非默认设置、已有生成结果或未知订阅继续具体blocked。

已加载默认持久对象/设置而Store尚未创建的部分态也须逐字段冻结：保留原实例、dirty、持久映射、Session字段图、原退出订阅和文件；比较副本只处理真正新增角色。准确ApiState字段只可持有BCL Task.CompletedTask同实例的缺省哨兵，不概括接纳任意已完成任务。

无文件变化的正常域重载会跳过四参回调，但仍有五参派发和CodeEditor尾部；独立五参证据只覆盖该尾部，不为其它插件授予初始化许可。其余消费者按各自真实状态与合同处理；仍需四参证明时报告准确未闭合点，不主动制造导入、读取初始化getter或关闭插件来得到ready。

UVCS2.12.4已有真实四参正常初始化证明时复用原禁用状态检查，不给已有日志配置追加冷态要求。缺少该证明时才采用独立有限日志合同：已存在唯一默认repo、准确selector/factory/级别表和订阅全部满足后，新Plan才可读取实际disabled/无workspace/无processor状态，并仅有限物化PlasticApp与UVCSAssetPostprocessor两项日志缓存及准确只读订阅。保存完整before/after及只读自动添加偏好，禁止仓库创建、自定义配置/工厂/创建事件和文件/网络/偏好写。后续按原计划选择同一路径，冷态合同只捕获已有after并严格匹配，不重复授予物化；包缺席不强制安装，旧null字段继续旧四参及摘要规则。

已安装的旧配置迁移回调仅在完整冻结域无 `.asset` 时走专属检查：实际模块、源码和编译归属一致，正常 update 见证成立且无初始化/迁移重入、既有队列为空。原 Plan State/执行观察器只继承同域且回调/调度摘要一致的原见证，Verify 的反序列化回执须另核当前真实 update 栈。排队时等待自然结束，不调用、清除或推进迁移；历史迁移结果不替代或阻断本次 Android 证据，配置仍属于非目标保护。

[references/android-refresh-compiled-identity.json](references/android-refresh-compiled-identity.json) 现在保存已审源码包输入合同，包含包/版本、asmdef、规范化依赖、完整源集合摘要和数量；文件不是消费端 DLL 的自签名单。新回调与自然异步写支路仍须单独审核，来源匹配不等于所有状态均可执行。MCP宿主当前编译身份也冻结到刷新快照，不因跨项目支持取消写前/写后检查。

完整材料的容量通过此 Action 内部无损回执封装解决，不删对象行或改变 dirty/原摘要，不提高通用 Store 的 256KiB 或 MCP 的 2MiB 上限。原文、gzip、封装、完整 Operation 与公开 wire 数据分别有界；失败必须给出实际字节、上限与层级。公开 data 在容量内保持原字段/数组结构；超限明确 blocked 或 partial，并准确说明已附回执是否经 Core 持久化及当前展示缺失，不静默改成 opaque 对象。写后编码超限不得二次抛出：只能沿用此前已持久化的完整写前 Recovery，明确完整写后回执未持久化，不重放。旧普通回执可读不等于新成功，历史 blocked/partial 不因容量修复升级。

Framework 已注册并开放 `nova.project.android.resolve-dependencies`，类型为 `Generate`。它会通过 EDM4U Force Resolve 删除或替换既有受管输出，因此声明 `Destructive`、外部副作用与精确确认门；不得退化为 `execute_code`、反射、菜单模拟、临时 C# 或直接删除 `GeneratedLocalRepo`。

正式流程必须保持：

1. `describe` 当前 Schema，按冻结的 Android Target 建立只读 Plan；Plan 冻结依赖图、完整刷新合同与精确写入集，只有 `ready` 才进入确认。
2. 用户确认绑定该 Plan 后只执行一次 Force Resolve。依赖图漂移、返回失败、异常或 domain reload 都不得自动重放。
3. 正常 Execute 只执行一次正规 Resolve，并观察真实四数组派发、自然 Editor update、刷新逻辑后置条件；再由 Receipt 进入 Verify，核对当前依赖图、解析状态、完整受管文件、非目标保护和 Artifact SHA-256。
4. domain reload 后只有缺少成功标记的 Recovery Receipt 时最高为 `partial`；不能凭目录存在推断成功。

自然刷新等待复用同域轻量调度探针，保持十秒窗口并让出真实 Editor update；不在每轮等待中重读全部资产。队列和 Search 就绪后仍执行完整稳定输入及非目标检查，复用实际版本快照形成回执；超时后的诊断不能追溯升级完成标记。该优化不改变确认、写集、Verify 或历史 `partial` 的边界。

外围稳定状态变化时，新回执用分项摘要提示最多四个变化参与项，不输出配置原值。先依据具体参与项定位原因，不按“缓存变化”泛化放行；旧回执没有分项记录时不能反推，也不为补诊断自动重放已成功的 Resolve。文件摘要仅在同一次同步核验中复用，不跨写入、异步等待或阶段；这不代表整个 Plan 有十秒时限或已有运行中取消能力。

模板支路的 POM 必须核对完整预期字节摘要，不能只验 `packaging=aar`。目标缺失或本次明确删除时从源 POM 重建；未删除的既有 POM 以其原内容为基准，仅按正规解析修改已有 packaging 节点。JDK 路径、二进制与 release 摘要、最低版本及额外 Java 启动参数复用同一无弹窗工具链检查，不通过安装额外 JDK 或改 External Tools、PATH、用户/系统环境来消除阻断。正规同步 Resolve 仅在已冻结作用域内把 Unity 当前 JDK 传给当前进程 JAVA_HOME，finally/域重载/退出恢复原值及空/未设状态；该临时值可能被同时启动的其它 Unity 子进程继承，计划中须明示。它不接管厂商异步 Resolve 或独立自动预下载；当前 Action 仍不开放 Gradle/下载分支。正规 UseJetifier=true 支路还冻结 Gvh 同字节持久化与系统 `GooglePlayServices.UseJetifier=True` 原值/预期；不得将其他设置漂移归为正常物化。

底层已审同步下载另有JDK17/Gradle8.13适配：仅真实下载支路临时配置官方分发及固定SHA-256，保护并恢复五项项目Temp生成文件；不修改原厂包或下载脚本，不将版本检查伪装为另一Gradle版本。该实现不扩大当前Action的下载/缓存/SDK安装许可。“同步完成”不等于下载完成；下载验收必须另从真实Nova入口覆盖下载支路，核对空缓存、实际JVM、Gradle结果、完整依赖及AAR/JAR内容，并恢复测试设置与环境。模板注入或wrapper夹具不能代替。

正常刷新写集还包含对应导入产物和明确的引擎派生资源：Search 两个空属性缓存、字符串表、固定 GUID 临时文件及真实四数组对应的事务追加；SpeedTree 无相关方法/输入/非目标 artifact 依赖时的指定依赖前缀维护；已审内存/UI 缓存、完整 pending 队列中固定九个已有颜色键的同值物化，以及当前无文件写入需求的 Cursor 尾部。Search 验证逻辑物化，允许该派生缓存后续正常幂等维护，不声称被丢弃的后台 Task 已退出。Plan 必须冻结当前完整队列，不能沿用历史数量；非目标文件、原有对象脏状态、场景和选择仍须保持。

正常 Unity 导入 usage 属于现有 `ExternalWrite`，按当前开关明示 `assetPostProcessorsUsage` v2 的 importActionId、方法名、持续时间和调用数，不宣称远端送达；不擅自禁用统计。未知 CodeEditor 或会产生未冻结同步输出的路径仍具体阻断。

当前已审 EDM4U 1.2.188/1.2.190 的 `Google.EditorMeasurement.Report` 实际没有 HTTP 发送或持久上报队列，但未请求过同意时仍可显示窗口并在选择后写偏好与 Cookie。模板支路仅接纳当前自然全局关闭或已经请求同意、且没有进行中窗口的状态；冻结实际对象身份、启用值、系统同意、完整偏好及持久配置摘要，前后必须保持一致。不得关闭设置、伪造同意或调用 Report 来探测。未知版本、未初始化或域重载无法继承对象身份时保留阻断/部分证据。

构建场景回调实际调用 `ResolveSync(false, true)`，与此 Action 的 `ResolveSync(true)` 不同。已有成功解析可以作为后续构建的输入证据；回调无需重新解析的支路还须核对当前包集合、完整 `gpsr` 标签路径集合和完整解析设置与状态文件一致，并另验全部产物 SHA-256。依赖图、模板、设置或文件漂移时阻断构建并重新制定解析计划；不得关闭自动解析、修改私有场景计数或直接调用私有回调来放行。

只有依赖图、解析状态、完整受管文件集合和 Artifact 摘要全部一致，且 Action Verify 返回完整证据时才报告 `success`。Tool 缺失、Action 未注册或输入不完整时返回 `blocked`；人工或 CI 已经执行但只有部分可信证据时为 `partial`。需要安装 SDK/Kit 时先走 `nova-project-onboard-sdk-kit`；已经失败的 Player/Gradle 构建诊断走 `nova-project-diagnose-build`。

不默认修改 UPM、Config、业务源码、Gradle 业务自定义、Manifest 业务节点、HybridCLR、Bundle/Player、设备、厂商后台、凭据或 Git；外部依赖下载、主机缓存和 SDK 安装仅限精确计划与确认中的正规解析目标。
