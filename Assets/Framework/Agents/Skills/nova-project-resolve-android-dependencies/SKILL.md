---
name: nova-project-resolve-android-dependencies
description: Use when Nova 项目组在接入或升级 Android SDK/Kit 后，需要冻结 EDM4U 依赖图并受控重建、核验 Android 原生依赖输出时使用。
---

# Nova 解析 Android 原生依赖

触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。

## 渐进式披露

先读取 `references/contract.json`。仅在确认 EDM4U 行为时读取 `Docs/Editor/EditorUtil/EditorUtil.AndroidResolver/EditorUtil.AndroidResolver.md`；仅在本任务来自既有 Pipify 流程时读取 `Docs/Editor/EditorUtil/EditorUtil.Pipify/PipifySteps.md`；仅在需要理解 Action 的确认与恢复语义时读取 `Docs/Editor/EditorUtil/EditorUtil.AgentActions/EditorUtil.AgentActions.md`。不要展开所有 SDK、Gradle、HybridCLR 或构建文档。

## 范围与冻结输入

本 Operation 只处理消费项目当前完整 EDM4U Android 依赖图的 Force Resolve。它不安装或升级 UPM 包、不修改 registry、不切换平台、不构建 Player，也不证明 Gradle、SDK 初始化或真机运行成功。

先冻结唯一项目根、`activeBuildTarget=Android`、EDM4U 已加载状态、非空完整依赖图及摘要、受管写入边界和执行入口。仓库摘要数组保留原始优先级顺序，调换顺序也属于依赖图漂移。依赖图为空、EDM4U 不可用、当前平台不是 Android 或 Resolve 目标不明确时返回 `not_applicable` 或 `blocked`，不得以清目录来“重新试一次”。

项目内产物包括 EDM4U 状态文件、`Assets/GeneratedLocalRepo/**`、`Assets/Plugins/Android/**` 中 EDM4U 管理的依赖文件与 Gradle/Manifest 模板。正规 Force Resolve 还可能运行 Java/Gradle、下载 Maven 依赖、更新主机缓存、安装 Android SDK 包及显示设置选择窗口；因此 Action 声明 `ExternalRead`、`ExternalWrite`，契约主版本为 `2`。不能把固定项目输出目录当成完整副作用边界。

确认前必须绑定当前解析设置、模板、Java/Gradle/Android SDK 的实际路径与身份、可删除的完整 `gpsr` 标签文件集合、外部下载与缓存/安装目标。当前 Plan 未完整冻结这些输入时返回 `blocked`，不得把用户对功能开发的同意当成对不明外部目标的执行许可。仓库 URL 只进入 SHA-256 摘要，不能把私有 Maven 地址、userinfo、query Token 或凭据写入 Plan、Receipt、日志或报告。

## 执行入口

正常刷新可能重建 UI Toolkit 的四类引擎派生资源，含动态 Atlas 页直接持有的复制材质。新 Plan 只在精确模块、既存面板及字段引用证明归属后声明该效果；写前保持完整角色状态，写后仅接纳已证角色重建，原 dirty、输入纹理、项目资产与其它对象仍保护，全部原对象行保留。未知归属继续阻断；旧回执缺少前后 owner-map 时沿用原摘要与严格比较，不追溯升级 partial。维护方细节按需查看 AndroidResolver 文档，不按名称或 hideFlags 泛化放行。

当前 Action 按真实方法派发、源码/DLL 指纹、自然调度和非目标保护判断是否可执行。当前仅接纳已有模板与本地 SRCAAR/POM 支路；Java 只读版本检查，下载、Gradle 执行与 SDK 安装尚不属于该可执行支路。未知类型、状态、消费者、队列或容量具体 `blocked`，不得关闭设置、物化 getter、预先导入或调用私有回调来消除阻断。历史 Receipt 缺完整刷新证据、域重载或写后验证不完整时最高为 `partial`。

包重编译引起身份变化时，由 Framework 维护方核对实际加载、磁盘、编译映射与当前回调分支后维护精确身份，消费端不自行替换摘要。完整路径域同时排除无点结尾的 `shadergraph` / `shadersubgraph`、相应 `.meta` 和受影响父目录，避免误入着色器导入或子图重载。

已安装的旧配置迁移回调仅在完整冻结域无 `.asset` 时走专属检查：实际模块、源码和编译归属一致，正常 update 见证成立且无初始化/迁移重入、既有队列为空。原 Plan State/执行观察器只继承同域且回调/调度摘要一致的原见证，Verify 的反序列化回执须另核当前真实 update 栈。排队时等待自然结束，不调用、清除或推进迁移；历史迁移结果不替代或阻断本次 Android 证据，配置仍属于非目标保护。

仅该依赖 Framework Editor 的模块使用 `references/android-refresh-compiled-identity.json` 保存已审完整 DLL/MVID，避免指纹维护本身再次触发重编译。消费端只读；维护方核实实际加载、源码及编译映射后更新。缺失、非法或与实际身份不符时阻断，不自动信任新 DLL。

完整材料的容量通过此 Action 内部无损回执封装解决，不删对象行或改变 dirty/原摘要，不提高通用 Store 的 256KiB 或 MCP 的 2MiB 上限。原文、gzip、封装、完整 Operation 与公开 wire 数据分别有界；失败必须给出实际字节、上限与层级。公开 data 在容量内保持原字段/数组结构；超限明确 blocked 或 partial，并准确说明已附回执是否经 Core 持久化及当前展示缺失，不静默改成 opaque 对象。写后编码超限不得二次抛出：只能沿用此前已持久化的完整写前 Recovery，明确完整写后回执未持久化，不重放。旧普通回执可读不等于新成功，历史 blocked/partial 不因容量修复升级。

Framework 已注册并开放 `nova.project.android.resolve-dependencies`，类型为 `Generate`。它会通过 EDM4U Force Resolve 删除或替换既有受管输出，因此声明 `Destructive`、外部副作用与精确确认门；不得退化为 `execute_code`、反射、菜单模拟、临时 C# 或直接删除 `GeneratedLocalRepo`。

正式流程必须保持：

1. `describe` 当前 Schema，按冻结的 Android Target 建立只读 Plan；Plan 冻结依赖图、完整刷新合同与精确写入集，只有 `ready` 才进入确认。
2. 用户确认绑定该 Plan 后只执行一次 Force Resolve。依赖图漂移、返回失败、异常或 domain reload 都不得自动重放。
3. 正常 Execute 只执行一次正规 Resolve，并观察真实四数组派发、自然 Editor update、刷新逻辑后置条件；再由 Receipt 进入 Verify，核对当前依赖图、解析状态、完整受管文件、非目标保护和 Artifact SHA-256。
4. domain reload 后只有缺少成功标记的 Recovery Receipt 时最高为 `partial`；不能凭目录存在推断成功。

自然刷新等待复用同域轻量调度探针，保持十秒窗口并让出真实 Editor update；不在每轮等待中重读全部资产。队列和 Search 就绪后仍执行完整稳定输入及非目标检查，复用实际版本快照形成回执；超时后的诊断不能追溯升级完成标记。该优化不改变确认、写集、Verify 或历史 `partial` 的边界。

模板支路的 POM 必须核对完整预期字节摘要，不能只验 `packaging=aar`。目标缺失或本次明确删除时从源 POM 重建；未删除的既有 POM 以其原内容为基准，仅按正规解析修改已有 packaging 节点。JDK 路径、二进制与 release 摘要、最低版本及额外 Java 启动参数复用同一无弹窗工具链检查，不通过改环境或配置来消除阻断。正规 UseJetifier=true 支路还冻结 Gvh 同字节持久化与系统 `GooglePlayServices.UseJetifier=True` 原值/预期；不得将其他设置漂移归为正常物化。

正常刷新写集还包含对应导入产物和明确的引擎派生资源：Search 两个空属性缓存、字符串表、固定 GUID 临时文件及真实四数组对应的事务追加；SpeedTree 无相关方法/输入/非目标 artifact 依赖时的指定依赖前缀维护；已审内存/UI 缓存、完整 pending 队列中固定九个已有颜色键的同值物化，以及当前无文件写入需求的 Cursor 尾部。Search 验证逻辑物化，允许该派生缓存后续正常幂等维护，不声称被丢弃的后台 Task 已退出。Plan 必须冻结当前完整队列，不能沿用历史数量；非目标文件、原有对象脏状态、场景和选择仍须保持。

正常 Unity 导入 usage 属于现有 `ExternalWrite`，按当前开关明示 `assetPostProcessorsUsage` v2 的 importActionId、方法名、持续时间和调用数，不宣称远端送达；不擅自禁用统计。未知 CodeEditor 或会产生未冻结同步输出的路径仍具体阻断。

当前已审 EDM4U 1.2.188 的 `Google.EditorMeasurement.Report` 实际没有 HTTP 发送或持久上报队列，但未请求过同意时仍可显示窗口并在选择后写偏好与 Cookie。模板支路仅接纳当前自然全局关闭或已经请求同意、且没有进行中窗口的状态；冻结实际对象身份、启用值、系统同意、完整偏好及持久配置摘要，前后必须保持一致。不得关闭设置、伪造同意或调用 Report 来探测。未知版本、未初始化或域重载无法继承对象身份时保留阻断/部分证据。

构建场景回调实际调用 `ResolveSync(false, true)`，与此 Action 的 `ResolveSync(true)` 不同。已有成功解析可以作为后续构建的输入证据；回调无需重新解析的支路还须核对当前包集合、完整 `gpsr` 标签路径集合和完整解析设置与状态文件一致，并另验全部产物 SHA-256。依赖图、模板、设置或文件漂移时阻断构建并重新制定解析计划；不得关闭自动解析、修改私有场景计数或直接调用私有回调来放行。

只有依赖图、解析状态、完整受管文件集合和 Artifact 摘要全部一致，且 Action Verify 返回完整证据时才报告 `success`。Tool 缺失、Action 未注册或输入不完整时返回 `blocked`；人工或 CI 已经执行但只有部分可信证据时为 `partial`。需要安装 SDK/Kit 时先走 `nova-project-onboard-sdk-kit`；已经失败的 Player/Gradle 构建诊断走 `nova-project-diagnose-build`。

不默认修改 UPM、Config、业务源码、Gradle 业务自定义、Manifest 业务节点、HybridCLR、Bundle/Player、设备、厂商后台、凭据或 Git；外部依赖下载、主机缓存和 SDK 安装仅限精确计划与确认中的正规解析目标。
