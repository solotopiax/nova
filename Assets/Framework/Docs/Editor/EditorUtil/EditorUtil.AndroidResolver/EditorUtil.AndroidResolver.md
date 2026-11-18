# EditorUtil.AndroidResolver

## §1 文件头

| 项 | 值 |
|---|---|
| 类签名 | `public static partial class EditorUtil.AndroidResolver` |
| 命名空间 | `NovaFramework.Editor` |
| 全局访问 | `EditorUtil.AndroidResolver` |
| 功能描述 | 准备 Unity 官方 Android 模板，向同步 EDM4U 解析传递当前 JDK、适配下载用 Gradle，并核验受管输出 |

---

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| [EditorUtil.AndroidResolver.cs](../../../../Scripts/Editor/EditorUtil/EditorUtil.AndroidResolver/EditorUtil.AndroidResolver.cs) | `partial EditorUtil.AndroidResolver` | 公有接口：`Resolve()` |
| [EditorUtil.AndroidResolver.Methods.cs](../../../../Scripts/Editor/EditorUtil/EditorUtil.AndroidResolver/EditorUtil.AndroidResolver.Methods.cs) | `partial EditorUtil.AndroidResolver` | 已审同步解析、模板前置与 JDK 作用域 |
| [EditorUtil.AndroidResolver.Templates.cs](../../../../Scripts/Editor/EditorUtil/EditorUtil.AndroidResolver/EditorUtil.AndroidResolver.Templates.cs) | `partial EditorUtil.AndroidResolver` | 官方模板来源、七项文件与开关的原子准备及回滚 |
| [EditorUtil.AndroidResolver.JavaEnvironment.cs](../../../../Scripts/Editor/EditorUtil/EditorUtil.AndroidResolver/EditorUtil.AndroidResolver.JavaEnvironment.cs) | `AndroidResolverJavaEnvironment` | 同步解析期间的当前进程 JDK 交接与 finally/重载恢复 |
| [EditorUtil.AndroidResolver.GradleEnvironment.cs](../../../../Scripts/Editor/EditorUtil/EditorUtil.AndroidResolver/EditorUtil.AndroidResolver.GradleEnvironment.cs) | `AndroidResolverGradleEnvironment` | 仅下载支路的已审 Gradle 分发配置、校验与生成文件恢复 |
| [AndroidEdmCompatibilityContract.cs](../../../../Scripts/Editor/EditorUtil/EditorUtil.AgentActions/Handlers/Generate/AndroidEdmCompatibilityContract.cs) | `AndroidEdmCompatibilityContract` | 官方 EDM4U 1.2.188/1.2.190 的精确预编译发行身份 |

---

## §5 完整公开 API

```csharp
/// 触发一次同步 Android 依赖解析（等价于 EDM4U 菜单 Assets → External Dependency Manager → Android → Force Resolve）。
/// 内部调用 GooglePlayServices.PlayServicesResolver.ResolveSync(true)，等待本次强制解析结束。
/// 当前设置决定写入 Gradle 模板或直接下载依赖；返回成功不表示后续 Gradle 构建或实际下载已验收。
/// 找不到 EDM4U 类型/方法或反射调用抛异常时直接抛 InvalidOperationException，由调用方（Pipify Runner）
/// 捕获并标记 Step 失败；禁止静默跳过，避免下游 BuildPlayer 在 GeneratedLocalRepo 缺失时才崩。
public static void Resolve()

/// 显式准备七项 Unity 官方模板并启用对应发布开关；保留既有模板/GUID与 Custom Base Gradle 选择。
/// 不调用 Force Resolve；后续仍须新建正式解析 Plan。
public static void PrepareTemplates()
```

---

## §10 常见误区

| 误区 | 真相 |
|---|---|
| Nova 调用的 `ResolveSync` 与构建场景回调是同一重载 | Nova 明确匹配单参数 `ResolveSync(bool forceResolution)` 并传 `true`；EDM4U 另有双参数重载，构建场景回调使用 `ResolveSync(false, true)`，两者分别验收 |
| 同步解析返回成功就代表所有远端依赖已下载 | “同步”只表示等待当前解析分支结束。模板分支登记依赖，实际下载留到后续 Gradle；下载验收须确认真实下载分支、最终依赖及落盘 AAR/JAR，不能只看模板、目录或返回值 |
| 找不到 EDM4U 类型/方法时 Warning 跳过即可 | 跳过后 `Assets/GeneratedLocalRepo/**` 不会被重建，Step 3 BuildPlayer 阶段才被动触发 EDM4U，但目标目录缺失会报 `DirectoryNotFoundException` 并把 HybridCLR `GenerateStripedAOTDlls` 拖崩；当前实现一律抛异常，让 Pipify Runner 在 Step 1 就显式失败 |

---

## §11 使用示例

```csharp
// 在 HybridCLR 编译之前完成当前依赖解析；产物按实际分支另行核验
EditorUtil.AndroidResolver.Resolve();
EditorUtil.HybridCLR.CompileDllActiveBuildTarget();

// 或通过 Pipify Batch（推荐）：
// PipifyWindow 中将 Step "edm4u.android_resolve" 拖到 "hybridclr.generate_all" 之前
```

---

## §13 关联文档

- [EditorUtil.md](../EditorUtil.md) — EditorUtil 工具集概览
- [EditorUtil.HybridCLR.md](../EditorUtil.HybridCLR/EditorUtil.HybridCLR.md) — HybridCLR 原子操作合集
- [PipifySteps.md](../EditorUtil.Pipify/PipifySteps.md) — 全 Step 清单

## 模板准备与 JDK 交接

新项目由 NovaSpark 在首次包解析前准备 Android 模板。已有工程的维护代码可显式调用 `EditorUtil.AndroidResolver.PrepareTemplates()`；该底层 API 只做准备，不执行 Force Resolve，没有菜单入口，也不是独立开放的 Project Action。消费端受控工具遇到缺失模板时报告具体前置缺口，不以任意 C# 绕过合同。完成准备后重新执行解析 Skill 的 `describe → plan → execute → verify`，不能继承模板准备前的 Plan。Force Resolve 不隐式创建缺失模板，已启用但文件缺失时先报告准确路径，避免继续跌入 EDM 预下载支路。

七项文件的真实来源与 Unity 6000.4.2f1 Publishing Settings 一致：

| 项目文件（位于当前工程 Assets/Plugins/Android） | 当前 Android module 内的官方来源 |
| --- | --- |
| `AndroidManifest.xml` | `Apk/UnityManifest.xml` |
| `LauncherManifest.xml` | `Apk/LauncherManifest.xml` |
| `mainTemplate.gradle` | `Tools/GradleTemplates/mainTemplate.gradle` |
| `launcherTemplate.gradle` | `Tools/GradleTemplates/launcherTemplate.gradle` |
| `gradleTemplate.properties` | `Tools/GradleTemplates/gradleTemplate.properties` |
| `settingsTemplate.gradle` | `Tools/GradleTemplates/settingsTemplate.gradle` |
| `proguard-user.txt` | 官方行为为首次创建空文件 |

Android module 根从当前 Unity 的公开 `BuildPipeline.GetPlaybackEngineDirectory(Android, None, false)` 读取，不猜 Unity.app 内部路径。当前工作目录须匹配真实工程，且 PlayerSettings 不能存在原有未保存编辑；先取得与 Action 共用的编辑器、资产库、Android 模板/JDK 锁并验证全部既有内容及缺失项来源，再暂停自动刷新和资产导入，准备文件后一次启用对应七项布尔并定向保存 PlayerSettings。普通 Gradle/Properties 必须有正文；两个 Manifest 必须是合法、无外部实体的 manifest XML；Proguard 可为空。既有模板和 meta 不改写，停用副本通过 `AssetDatabase.MoveAsset` 恢复以保留 GUID；正本与 `.DISABLED` 同时存在时拒绝。失败恢复原开关、本轮创建与移动，未知并发变化或恢复失败保留现场并明确报错。重复准备没有文件或开关变化时不主动 Refresh。准备方法不打开辅助窗口；实际文件导入仍会触发项目原有回调，不代表 Maven 下载或构建成功。

EDM4U 1.2.188 与 1.2.190 的 Android 解析、模板、Java、设置、Version Handler 源码相同。[官方版本比较](https://github.com/googlesamples/unity-jar-resolver/compare/v1.2.188...v1.2.190)只有版本常量、导出器及 iOS Swift 包构建后支路发生变化；维护合同分别核验两个官方 UPM 版本的包来源、精确 DLL 位置、完整 SHA-256、MVID 与唯一加载程序集。新版本或被替换的发行文件仍具体阻断。官方预编译产物的身份可跨项目复用；它与 Unity 在消费项目重新编译的源码包身份不是同一规则。

EDM 的 Java 版本检查会显式传入 JDK 环境，但预下载 `GradleResolution → CommandLineDialog.RunAsync` 没有传 `envVars`；仅检查 External Tools 设置不能修复后者。Nova 的同步 `Resolve()` 使用 Unity 当前选择的 JDK，并仅在已审 `ResolveSync(bool)` 的整个同步生命周期内设置当前 Unity 进程 `JAVA_HOME`，使实际 Gradle wrapper 子进程继承该 JDK。官方 `CommandLine.Run` 等待子进程退出后才通知同步完成；不能把该作用域套给异步 `Resolve` 或未经审核的新版本。

作用域有独占防重入检查；保留未设、空字符串与原值的区别，不改变 PATH、其它变量、用户环境或系统环境，finally、域重载和退出均恢复，原值不进入日志/回执。期间其它新启动的 Unity 子进程也可能继承这个临时进程值，这是该上游无环境钩子的适配范围。Action 冻结原值摘要和是否定义，写前及写后重核；下载、真正 Gradle 执行与 SDK 安装仍不属于当前 Action 可执行支路。EDM 自己发起、未经 Nova 同步入口的自动预下载不受此作用域接管；应单独审核其实际触发条件。实际 Gradle Java 身份和 Maven 下载结果需要在已冻结且获授权的 Gradle 分支取得，不能用模板注入、Java 版本读取或 wrapper 夹具测试代替。

### 同步下载分支的 Gradle 适配与验收

已审 EDM4U 1.2.188/1.2.190 内嵌 wrapper 默认下载 Gradle 5.1.1，该版本不能在当前 Unity JDK 17 上正常启动。仅传递正确 JDK 还不够。Nova 在真正下载支路进入前核对上游 DLL、原始脚本与 wrapper 资源；当前适配核对实际选定的 JDK 17，再临时将项目 Temp 内生成的 wrapper 属性指向官方 Gradle 8.13 分发，并固定官方 SHA-256。本分支不执行 Unity 当前配置的 Gradle，因此不要求该目录为特定版本或 JAR 字节。保留原厂下载脚本与包内容，不修改版本探测结果，也不改 External Tools。

该作用域检查主线程、唯一工程、EDM 空闲状态和额外 Java/Gradle 参数；模板注入支路不创建此事务。五项生成文件的原始存在性、字节、mtime、属性与 POSIX 权限在正常返回、失败、重载及退出时恢复；Windows 不设置或比较 POSIX 执行位。未预料到的并发改动保留恢复材料并明确阻断，不覆盖未知内容或自动重放。Gradle/Maven 本身的正常缓存另属实际下载写入，不能将“wrapper 已恢复”解释为“主机没有任何写入”。

维护方的下载验收必须从正式 Nova 入口进入真实下载支路，使用独立空 Maven/Gradle 缓存，核对实际 JVM、Gradle 正常结束、最终坐标与间接依赖、AAR/JAR 完整性，以及作用域和原设置恢复。`GradleTemplateEnabled` 依实际模板文件及 EDM 状态判断，不能仅关闭 Unity 的模板布尔就声称已覆盖下载。修改解析设置选择测试支路仅限明确授权的验收副本；这不为消费端 Action 开放尚未冻结的下载、缓存或 SDK 安装目标。

## 消费端 Action 的执行边界

`nova.project.android.resolve-dependencies` 复用 `Resolve()`，按当前真实资源刷新参与者和自然状态建立可执行合同。Plan 先核稳定编辑态、完整方法派发规则、源码与 DLL/MVID、现有调度和无弹窗条件，再冻结模板支路、精确写集和非目标保护；未知实现、订阅、输入、容量或状态均以具体原因 `blocked`，不通过关闭设置或预先调用 getter 制造可执行状态。公开 `describe / plan / execute / verify` 与请求 Schema 保持不变。

文件摘要只在一次同步只读阶段内共享：Plan、Execute 写前检查、一次执行后稳定捕获、Verify 分别建立范围，嵌套核验及该阶段重复的回调身份预检复用当前范围；进入 Resolve 写入、异步等待或下一阶段前释放。缓存命中仍响应取消，来源准入、完整内容/MVID核验及来源 DLL 双读保持。该改动减少重复读取，不代表全部耗时已定位或长任务取消已经开放。

新的外围快照按实际参与回调保存编号和 SHA-256 分项摘要。完整稳定摘要或引用数量不一致时，错误最多列出四个变化参与项，不输出偏好或配置原值，也不据此放行变化。旧回执缺少分项记录时明确提示无法反推，仍执行原完整校验，不把既有 `partial` 追溯改为成功。

读取可能触发插件静态初始化的字段前，须有正常初始化证明或该类型独立、已审的有限读取合同。通用[初始化见证](../../../../Scripts/Editor/EditorUtil/EditorUtil.AgentActions/Handlers/Generate/AndroidRefreshInitializationWitness.cs) 核对真实引擎派发、完整方法集合及异常计数，并等到派发退出后的正常 update 才生效；编译、异常、程序集或方法集合改变时继续拒绝。没有独立合同的消费者仍可能需要本次进程的正常资源导入记录，不能以来源匹配替代。Action 不为建立记录主动制造导入、调用未审初始化器或更改插件启用状态。

已审 Core 在无文件变化的域重载派发中仍运行五参回调及 CodeEditor 尾部，却跳过四参回调。独立五参观察器因此只证明尾部已经自然完成，不能替代四参插件初始化记录。五类 AI Store 则由独立冷态合同判断，不再一律要求已创建 Store；其它尚需真实四参证明的消费者继续准确报告缺口。

源码编译包通过随包已审 [android-refresh-compiled-identity.json](../../../../Agents/Skills/nova-project-resolve-android-dependencies/references/android-refresh-compiled-identity.json) 核验实际包版本、规范化包依赖、asmdef 及完整编译源集合的相对路径与摘要。唯一 Unity 编译所有者、真实输出、每项源码与源文件集合均须吻合；新增/替换源码及未适配版本阻断。实际 DLL/MVID、当前编译条件、引用、响应与分析器身份进入当次冻结摘要，写前/写后变化仍阻断；不再把某一个消费工程的编译 DLL 当跨项目信任锚。reference 是维护方审核的源码输入合同，不由消费端自行更新。完整路径域继续排除无点结尾的 shadergraph/shadersubgraph、meta 与父目录。

URP 17.4.0 的既有正常管线绑定不再一律阻断；[AndroidResolveUrpRefreshContract](../../../../Scripts/Editor/EditorUtil/EditorUtil.AgentActions/Handlers/Generate/AndroidResolveUrpRefreshContract.cs) 限定已审 Runtime/Editor 来源、精确管线类型与真实资产身份，冻结 Graphics/Quality、管线资产、Global Settings 与 URP 项目设置。材质版本检查必须已由正规初始化完成、当前版本为 10，且无待保存、升级或重建工作；本次模板域不得命中材质或 GraphicsSettings。未知管线或待处理状态继续具体阻断，不改项目渲染配置、清队列或调用 Ensure 来通过解析。AI Assistant 的回调也按实际输入和订阅核对；未闭合的异步写支路不能只凭包来源已审核就放行。

材质检查还有独立的正常 update 初始化路径，不能把没有四参资源派发误报为该路径从未完成。读取 MaterialPostprocessor 前，核对已审 MaterialReimporter 首次标记的准确 true 初始化 IL、正常检查后的 false 值、已完成尾部及本域无已观察错误，并排除在该检查内部重入。它证明该正常检查已经访问升级表，不在 Plan 首次运行含 Shader.Find 的材质初始化器；材质版本和待保存状态仍继续核验。五参尾部记录单独不能证明材质或其它四参回调完成。

默认包的额外回调由[独立自然支路合同](../../../../Scripts/Editor/EditorUtil/EditorUtil.AgentActions/Handlers/Generate/AndroidResolveDefaultPackageCallbacks.cs)逐项限制：

| 参与者 | 当前可接纳的实际状态 |
| --- | --- |
| AI 动画、图片、网格、PBR、音频生成 | 已初始化的 Store 须为空且无派发/排队工作；尚未创建的 Store 仅在缺失或准确默认设置、无用户结果与预览时进入独立冷态合同 |
| AI Assistant 与 Skills | 无资产删除订阅；不命中真实 Skills 扫描目录，已有扫描状态满足只读条件 |
| AI Graph | `Library/AI.CoreGraph` 不存在；既有目录会触发异步图写入，当前继续具体阻断，保留数据及现场 |
| AI Search | 已存在的搜索配置关闭搜索；启用分支的异步知识队列尚未纳入模板写集 |
| 模型可视化 | 没有现存模型可视化窗口 |
| Unity Version Control | 实际禁用、无工作区和处理器；已有正常四参证明时复用原检查，缺少证明才使用冷态日志合同。仅关闭自动添加不足以覆盖移动、签出及全局保存队列的副作用 |

这些条件和相关对象引用进入 Plan 冻结与前后验收；未知来源、活动队列和状态变化仍拒绝，不清空结果、关闭窗口或停用版本控制来通过检查。

[五类 Store 合同](../../../../Scripts/Editor/EditorUtil/EditorUtil.AgentActions/Handlers/Generate/AndroidResolveAiStoreContract.cs) 独立冻结设置、模型、监听器、退出订阅及准确对象归属。首次创建 Store 时，已有设置文件才按单对象、精确脚本及字段默认文本核对；Store 已初始化时按实际文件计算摘要，保留用户设置，不附加默认文本及冷态 8192 字节限制。两态均保护 SHA-256、长度和 mtime，不通过加载 getter 建立状态。仅本次实际非空 imported 数组允许一次冷态到默认已初始化态转移，删除或移动不能冒充导入。原始对象行完整保留，比较副本只处理已证新增角色；其它对象、原 dirty 和原订阅保持。Receipt 绑定真实后态，Verify 严格比较当前后态，不能再次使用冷态许可替换已建立实例；旧空字段回执保留原摘要与严格比较。

正常域重载还可能只保留默认持久对象与设置而尚无 Store。合同逐字段核对其默认 AppData，并保留已有实例、原 dirty、Session 字段图、持久映射身份、退出订阅与磁盘文件；不能把已有角色算成本次新建而从比较中剔除。仅已审 ApiState 的准确字段可以持有与 BCL `Task.CompletedTask` 同实例的缺省哨兵，其它任务继续拒绝。

[UVCS 日志合同](../../../../Scripts/Editor/EditorUtil/EditorUtil.AgentActions/Handlers/Generate/AndroidResolveUvcsRefreshContract.cs) 仅覆盖已审 2.12.4。已有真实四参正常初始化证明时，直接复用原来的实际禁用、无工作区和处理器检查，保留既有日志配置。缺少证明时，才从纯 WrapperMap 根证明默认日志仓库已存在，核对准确 selector、factory、配置、级别表和订阅，再读取实际 UVCS 禁用状态。冷态 Plan 只可在此前提下有限物化两个固定名称的日志项及准确只读配置订阅，并保存完整前后材料；不创建仓库、不读取自定义日志配置、不调用未知工厂/创建事件，也不写文件、网络或偏好。自动添加偏好只读冻结键、存在性和有效值，不能用关闭自动添加代替禁用状态。缺少前置或实际仍启用时具体阻断。

UVCS 的首次物化许可只属于新 Plan。完整快照进入摘要及回执绑定，写前、写后和 Verify 均只捕获已有状态并严格匹配 Plan 的实际 after，不再授予物化权限；包缺席保持可选，不为此安装 UVCS。旧 `uvcs=null` 保持旧摘要和四参路径，不能补造历史许可。

旧配置迁移回调仍只接纳完整域无 `.asset`、真实正常 update 见证、无初始化/迁移重入且 queued=false 的自然分支；不调用或推进迁移。MCP Action update 闭包也须核精确源码、编译所有者、CompilerGenerated 和方法签名，其实际编译身份单独进入刷新 Snapshot。原 Plan State/观察器仅在同域且全部相关摘要相同后继承见证；反序列化 Verify 另验真实 update 栈。历史迁移结果不代替本次 Android 证据。

完整刷新快照与解析回执仅在此 Action 内采用有界无损表示：原文最多 8MiB，gzip 最多 128KiB，专属封装最多 192KiB；Plan 另按完整信封、写集与 null 字段核算 192KiB Operation 局部预算，通用 Store 仍为 256KiB。全部对象行、顺序、dirty 标志、原摘要及前后材料保持，不更改域与写前/写后校验。公开 data 保留原字段或数组形状，并以紧凑数据 1536KiB 为局部展示预算，为原 2MiB MCP 响应保留外层余量。Plan 超限具体 blocked；写后公开展示超限明确 partial/警告；完整编码回执只有成功附入结果后才交 Core 持久化，不能提前冒称已保存。若写后回执本身越过编码界，安全返回 partial 与实际容量原因，保留此前已持久化的完整写前 Recovery，并明确完整写后回执未持久化，不二次抛出或重放。容量失败说明实际字节、对应上限和层级。Verify 兼容旧普通回执；无损解码不代表解析或验收成功。

2026-10-19 的一次后来的只读复现含 17,337 条唯一对象行，快照 896,436B、完整计划回执 1,028,428B；完整回执无损封装后 101,744B，完整 Operation 模拟 116,113B，保留原 data 的 Gateway 预算模拟 902,239B。该样本用于容量回归，不是旧失败 Plan 的补造材料，也不证明 Resolve、Gradle 或 Player 已执行。永久测试仅保留经列语法审查的完整身份/类型/dirty、公开 Sample 场景与选择 ID 行，不分发完整私密回执。

当前可接纳范围是已有 Gradle 模板和本地 SRCAAR/POM。Plan 冻结完整有序 Maven 输入、原 POM 与既有目标、`gpsr` 删除集合、工具链、可能触及的父目录及普通无 AssetBundle 名的 importer；Java 只进行无弹窗版本读取。下载、Gradle 执行、SDK 安装以及不满足当前合同的其他自然支路仍阻断。POM 验收计算完整预期 SHA-256：目标缺失或被明确删除时采用源内容，否则采用未删除的既有目标；只修改已有 packaging 节点为 `aar`，无节点或已为 `aar` 时保留原字节，保留坐标、依赖及其余内容。预期值在内存中按已核 EDM4U 1.2.188/1.2.190 的 XML 格式计算，不写文件或调用 vendor 修补。

执行前重新冻结完整合同并比较；确认只绑定当前 Plan。Execute 只调用一次正规 `Resolve()`，自有观察器记录实际四数组派发，并等待正常 Editor update 推进后只读核验。它不调用私有 vendor 回调、不消费或清理队列、不主动 flush、不取消厂商工作。失败、超时、取消、域重载或写后证据缺失均保留 `partial` 回执，不重放；旧 Receipt 缺完整刷新合同或跨域身份时最高为 `partial`。只有完整正常解析、刷新及输出证据一致才可 Verify `success`。

刷新等待复用绑定当前域、真实类型与 DLL/MVID 的 Version Handler 轻量调度探针，十秒轮询窗口内不反复遍历全部资产、标签和设置。自有 update 信号异步续接到 Unity 上下文，先让同轮后续回调返回；队列自然完成且 Search 事务验收就绪后，再做一次完整稳定输入与非目标校验，并复用其中实际捕获的版本快照写入回执。完整采集仍受取消控制，不计为额外的自然等待预算；超时后的单次诊断捕获即使观察到队列后来完成，也不能将超时升级为成功。全部 Search、稳定输入、脏状态、事务及最终产物要求保持不变。

这里的十秒是实际 Editor update 返回后检查的协作预算。`NextEditorUpdate` 没有独立取消注册或超时定时器；Editor 不再发出 update 时，当前 await 不会仅因十秒或取消自行完成。`Dispose` 关闭观察窗，不主动推进官方队列，也不把晚到的刷新或历史 `completed=false` 补为成功。

非目标对象行以 Ordinal 集合线性比较，仍拒绝两侧重复、原行缺失、脏状态、场景或选择变化；已审 Grid 空单例新增边界保持。新计划另以有限 UI Toolkit 角色合同识别正常引擎资源重建，所有原始对象行仍完整保存，只有实际字段引用证明归属的行在比较副本中成对处理；项目资产、输入纹理与其它用户对象不授予变化。文件/目录摘要变化与对象行变化分别报告，对象诊断仅展示移除和新增各最多四条、每条最多 240 字符及省略号。

UI Toolkit 合同绑定当前已审模块的实际加载、磁盘 SHA/MVID和既存面板根，只读现有字段，不调用创建型 getter、Reset、Repaint、Render 或初始化入口。允许的四类角色为面板直接持有的默认材质、默认浮点 Shader 信息纹理、Shader 信息存储纹理和动态 Atlas 页（包含页的 `m_Blitter.m_BlitMaterial` 复制材质）；面板拓扑、输入资源、精确类型、非持久化身份与原 dirty 仍需符合冻结合同。写前完整角色状态不变，写后只允许已证角色正常重建，未知归属继续 blocked/partial。新 Snapshot 的 `uir` 与 Receipt 的 `uirAfter` 分别进入摘要；旧字段为空时保持旧摘要和旧严格对象规则，不能补造历史 owner-map 或追溯升级 partial。

写集除本轮精确产物外还明示正常引擎派生效果：对应资产的 Library 导入登记；Search 两个属性库及字符串表、GUID 临时 merge 文件和实际四数组对应的有序事务追加；SpeedTree 指定派生依赖前缀的有限保护；已审内存/UI 缓存和本轮实际已审颜色偏好键的同值物化。Search 仅接纳现有已初始化的空属性缓存，联合只读锁下验证逻辑物化、稳定文件和精确事务增量，允许该派生缓存后续正常幂等维护；不把逻辑完成等同于所有被丢弃的后台 Task 退出。

SpeedTree 仅接纳空属性方法集合，或来源已核的 URP 17.4.0 唯一版本 1 方法。当前固定 Core 仅在域重载时根据方法元数据登记 hash，同域模板刷新不调用该方法，也不登记或清理该前缀；仍要求没有 st9/spm 输入，全部现有非目标 artifact 依赖均不命中前缀，方法集合和来源进入前后重核。该实现没有原生登记值回读 API，不能把元数据或预期登记输入当作实际值已回读的证明。

SpeedTree 的历史 artifact 查询按全项目累计最多 131,072 份结果限界，不再因单资产历史版本超过 128 而拒绝；仍逐项核有效性、检查全部当前依赖，并释放全部查询包装。当前 artifact 数量及每份依赖数量的原边界保持，取消不跳过释放。

外围合同依据实际已加载实现和 backing 状态核对 Preset/Sprite、UPM/AssetEvents、Grid、Graph、颜色消费者及 CodeEditor 尾部。颜色完整 pending 队列在 Plan 时冻结，不绑定历史队列数量；三项 Clipboard 角色始终核验，其余七个已审颜色角色仅在当前缓存或 pending 中真实存在时纳入，包括 Tilemap 的 `Scene/Grid Component`。只接纳现有缓存或已有偏好正规格式往返同字节的物化；未知未加载键、缺失原值或未知实际消费者继续阻断。Lighting 已存在 UI 可按计划执行固定 shader/Handles 三色更新，其余稳定状态仍核验。

Preset 先通过公开 `Preset.GetAllDefaultTypes()` 核对全局默认类型集合；集合为空时没有任何默认 Preset 可应用，新项目不必先加载一个 PluginImporter 样本。集合非空时仍沿原路径使用现有原生 Importer 与公开 `PresetType(Object)` 核对，缺样本或有默认值继续具体阻断；不构造 Importer 或使用内部 Type 构造器。

CodeEditor 尾部的新计划要求独立五参派发及后续正常 update 完成记录，再读已有对象；旧回执继续原四参证明规则。Unity 原厂 `DefaultExternalCodeEditor` 仅在实际当前实例就是默认实例、且已有显式编辑器偏好时接纳：固定 Core 的同步方法为空实现，冻结注册表、偏好、安装字段和实例身份，不要求 Cursor 包、特定工程名或 `.vscode`。既有 Cursor 支路仍须满足原已审安装发现、配置文件无写固定点和真实路径排除条件；实际选中的 Rider、Visual Studio 或其它实现未自动扩审。未被该尾部调用的已注册 provider 不触发安装发现或生成器，也不通过改变 IDE 选择来通过 Plan。

当前 Version Handler 观察不覆盖 batchmode 或 `-executeMethod`，Search 与 UI Toolkit 也要求已有正常 GUI 初始化状态。消费端验收应在普通 Android Editor 稳定后通过正式网关读取 Plan；批处理编译通过不能替代该状态证明。

普通 Unity 导入 usage 沿用 Action 已声明的 `ExternalWrite`：已启用时可调用 `assetPostProcessorsUsage` v2，字段仅为 `importActionId`、方法名、持续时间与调用数；该调用不构成外部送达证明。当前严格指纹内的 Google `Report(string,string)` 完成回调只读 `Enabled` 后返回，没有 HTTP 或持久上报队列；仍要求系统同意已请求且无进行中窗口等自然条件，冻结对象、字段、偏好及配置摘要，不调用 Report/Prompt 探测、不改同意。正规 UseJetifier=true 支路的项目 Gvh 同字节设置持久化和系统 `GooglePlayServices.UseJetifier=True` 精确物化另在 Plan 中冻结及验收。

Force Resolve 和构建场景回调的无弹窗检查复用 `TryCaptureNoPromptToolchain`，冻结 JDK 二进制与 release 摘要、版本、明确工作目录，并拒绝额外 Java 参数。构建回调 `ResolveSync(false, true)` 的无解析场景合同与此 Force Resolve 合同分别验证。测试覆盖实际方法绑定、完整 POM 字节、持久设置、刷新回执、Search 事务和正常 Editor update 等；测试通过不等于本工程 Force Resolve、Gradle、Player 或真机验收通过，实际执行必须另有当前回执。

## 调用与失败边界

底层 Resolve 与受控 Action 的输出冻结是两层。`FindResolverType` 只查当前已加载程序集，不加载替代 DLL；命中后仍由官方 UPM 发行合同拒绝未知版本、错误路径、摘要/MVID变化及同名歧义。`ExecuteResolveSync` 只接受明确返回 bool 的 `ResolveSync(bool)`，先核模板就绪与 JDK，再在环境作用域内同步传 force=true；缺类型、缺方法、返回 false 或异常均显式失败，不静默跳过。反射目标异常保留 InnerException。底层函数没有 Action 的确认、完整写集和结果恢复合同；项目组通过已有 Skill 调度正式解析。
