# 项目与场景结构

本页帮助 Agent 识别 Nova 的最小结构。已有项目的结论来自当前工程；任何新游戏都默认落实下述 MainDemo 标准产品预加载基线。产品玩法可以独立设计，标准基础能力不随游戏体量缩减；Demo 导航、示例账号和演示业务不属于产品基线。

## 新项目开工准备

新建项目先准备基础环境、固定启动资源和配置，跑通最小启动闭环，再开展依赖这些前置的业务制作。准备工作本身可以创建必要配置、预加载代码和占位资源；尚未开发的业务内容先明确来源与接入阶段，在开始消费它们前补齐，不能伪称已存在。已有工程仅核对本次相关项，不自动迁移启动架构。

### Config、Pipify、PlugPals 基线

所有 Nova 项目按 [NA-002：先核对当前能力，再选择接口或提出补充](DEVELOPMENT_CONSTRAINTS.md#na-002先核对当前能力再选择接口或提出补充) 配置并实际使用这三个工具：

| 工具 | 必须配置与使用的内容 | 最低证据与既有入口 |
| --- | --- | --- |
| Config（ConfigMaster） | 项目自有 `ConfigMasterSO`、实际 Platform × Channel × DevelopMode、正式导出目标和运行时加载绑定；与本项目 `PipifySettingsSO` 配成同一持久化业务工作区 | 保存读回、正式导出的 `ConfigRuntimeSO` 与实际消费配置一致；`nova-project-configure-runtime`，工作区规则见 [WorkspaceActive](../Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.WorkspaceActive.md) |
| Pipify | 项目自有 `PipifySettingsSO` 和适合当前任务的具名 Batch；按 MainDemo 参考调整步骤、顺序、参数与前置，让可重复的导出、构建等流程沿现有流水线执行 | 首次缺资产或绑定时用 [Pipify 窗口](../Editor/Windows/PipifyWindow.md) 的创建/选择入口完成，现有配置 Action 不创建 Settings 或切工作区；再由 `nova-project-configure-pipeline` 保存 Batch 并 Verify，按既有 `nova.project.pipify.run-batch` 执行已授权的适用流程，核对 Job 终态与真实产物；只有配置保存不能称已实际使用 |
| PlugPals | 核对并配置项目实际需要的云仓库来源及对应 UPM registry/scope，通过 PlugPals 查询可用包、核对已安装状态和任务依赖，并按需安装 | 仓库地址沿 [PlugPals 窗口](../Editor/Windows/PlugPalsWindow.md) 工具栏配置并保存；默认地址显示或配置文件存在都不能代替实际查询成功。核对有效解析，所需新包沿 `nova-project-manage-upm-package` 安装并 Resolve/Verify；安装 Action 不代改 PlugPals 仓库设置。已安装包足够时无需为证明使用而额外装包 |

新建项目或评估完整项目基线时，逐项记录 `已就绪 / 待准备 / 阻塞`，未取得实际使用证据的项列为待验证；窗口打开过、资产存在或单次直接 Build 成功均不能代替。已有工程可复用仍有效的配置和使用证据；局部任务发现缺项时报告并交给相应准备步骤，不能标成“可选/不适用”或擅自扩大修改范围。保存 Batch 的任务仍只负责保存，不能据此运行；完整开工与交付须另完成授权范围内的实际使用。上传、发布、通知、删除等步骤不因三工具必用获得额外授权。

### 三平台构建与 HybridCLR 基线

所有 Nova 项目执行 [NA-004：业务默认进入热更范围，AOT 例外须证明必要性](DEVELOPMENT_CONSTRAINTS.md#na-004业务默认进入热更范围aot-例外须证明必要性)。HybridCLR 必须实际安装、启用并接入业务程序集；Config 配置入口 Procedure、AOT 元数据与启动/按需业务 DLL 的源位置、目标位置、运行期地址，以及 link.xml 路径。来源读取当前 HybridCLR 设置、实际程序集与依赖，参考 MainDemo 再按项目调整；按需业务列表无实际用途时可以为空，不能把启动业务 DLL 或必需 AOT 映射整体留空。拷贝目标必须进入有效 Collector，正式导出后沿 `ProcedureLoadDll` 加载。

Android、iOS、WebGL 的完整本地打包 Batch 以当前 MainDemo 的全量流程为基础，按下列职责和依赖顺序配置。项目数据依赖所需的其它表/代码导出在首次 DLL 编译前完成；已经核实的前置可复用，不能因有简短 APK 示例便省略整条基础链。

| 顺序 | 必需职责与当前 Step ID |
| --- | --- |
| Android 前置 | `edm4u.android_resolve`；iOS/WebGL 不加入该 Android 步骤 |
| 1 | Config：`export.config` |
| 2 | Network HostKey 类型与数据：`export.network.hostkey.code` → `export.network.hostkey.data` |
| 3 | Network NetCmd 类型与数据：`export.network.netcmd.code` → `export.network.netcmd.data`；协议变化时另补正式 Proto 导出 |
| 4 | AOT 保留：`hybridclr.validate_linkxml` |
| 5 | HybridCLR 生成：`hybridclr.generate_all` |
| 6 | 当前目标业务 DLL：`hybridclr.compile_dll_active_build_target` |
| 7 | DLL 部署：`hybridclr.copy_aot_dll` → `hybridclr.copy_game_dll` |
| 8 | 正式资源：`bundlebuilder.build`（RawFile 等特殊内容按当前资源方案另配） |
| 9 | Player / 平台工程：`build.package` |

`shell.open_folder` 可作为最后的产物定位步骤。完整流程通常为 Android 14 步、iOS/WebGL 13 步（含打开目录）；验收看实际职责、前置与产物，不按固定步数机械凑齐，也不照搬上传、通知或 CDN 等范围外动作。创建和维护 Batch 继续使用 `nova-project-configure-pipeline`，注册 Step 和字段以当前 live Schema 为准；执行前显式切到对应 Unity Active BuildTarget，核对 Channel/DevelopMode、DevelopmentBuild、SDK/签名/输出和资源策略，Batch 名称或保存的只读 Target 不会自动切平台。

`GenerateAll` 内部先通过临时 script-only `BuildPipeline.BuildPlayer` 产出当前目标裁剪 AOT DLL，再生成桥接和泛型引用，因此上述 AOT 拷贝位于本次裁剪成功之后。不能拷贝旧目标或旧版本文件；最终 Player 后若裁剪结果变化，按最终结果补齐部署及资源一致性。生成、业务 DLL 刷新、DLL 拷贝、Bundle、最终 Player 和实际加载分别验收，详见 [HybridCLR 安装与验收](../Editor/EditorUtil/EditorUtil.Environment/EditorUtil.Environment.md#hybridclr-安装与验收) 与 [GenerateAll 的临时裁剪构建](../Editor/EditorUtil/EditorUtil.HybridCLR/EditorUtil.HybridCLR.md#generateall-的临时-script-only-player)。仅勾选启用、包存在、空配置、Editor 直接运行源码或普通 IL2CPP 包成功均不能证明基线完成；单项配置任务不因此自动运行整套三平台构建。

### 默认启动基线

固定启动资源也须按产品实际横屏或竖屏定位完成布局，不能原样沿用 Demo 的方向参数。开工准备中的适配清单从闪屏、Loading 和启动弹窗延伸到玩法、暂停及结算，按 [逐页适配与内容避让](../Runtime/Modules/UI/UIComponent.md#从启动到玩法的逐页适配)处理；不默认同时适配两种方向，也不以玩法首屏或整页安全区缩进代替完整验收。

- 使用完整 canonical [Nova.prefab](../../Prefabs/Nova.prefab)，保留框架启动入口。默认框架链为 `ProcedureSplash → ProcedureCheckVersion → 按配置选择更新流程 → ProcedureLoadDll → 项目业务入口`；不能把业务预加载类型直接填入框架入口字段来跳过启动准备。
- 默认引入 MainDemo 的 Resources 固定启动资源及真实依赖，配置 `ProcedureComponent.LauncherSettings`；实际来源、复制边界、默认键与验证见 [固定启动资源](RESOURCE_WORKFLOW.md#新项目默认固定启动资源)。路径有值不代表资源已存在。
- 业务预加载必须以当前 MainDemo 的 `ProcedurePreload` 为默认参考，按下节完整落实八项标准动作，再进入产品首个业务流程。开发态来源是 `Assets/Samples/MainDemo/Scripts/Runtime/Procedures/ProcedurePreload.cs`；消费端使用[当前已安装版本声明的正式 MainDemo Sample](../../package.json)，不依赖开发机绝对路径或修改包缓存。
- 复用流程取消、进入与尝试代次保护、逐项结果检查、失败处理和启动面板收尾，建立项目自己的命名空间、程序集、入口与正式数据。流程保持模块级编排，具体文本键、表类型和字体标记由配置及专项验收负责，不硬编码进通用预加载；具体门禁见下节。

### 标准产品预加载基线

以下八项是公司游戏产品的默认基础能力，小游戏、单机或离线产品同样保留。Agent 开工时主动读取当前 MainDemo 与对应 Component 契约，准备产品自己的配置、表、字体、界面、音频和其他真实依赖；不能等用户逐项提醒，也不能以“当前玩法尚未调用”自行删除节点。

仓内 MainDemo 与其他 Demo 的网络步骤统一先检查网络表加载结果，再等待本次启动的 GM 应用配置拉取；等待成功、失败或未配置都继续后续步骤。该等待属于第 3 步，使用当前流程的取消令牌，不增加步骤或另发刷新请求；各 Demo 专用的 SDK 就绪判断仍独立保留。

| 顺序 | 标准动作 | 产品接线与最低判断 |
| --- | --- | --- |
| 1 | `Nova.Persist.LoadAsync()` | 按目标平台准备受支持的持久化后端；任务完成不等于所有后端可读写，不把不支持的后端警告当成功。后续存取继续遵守 Component 消费边界，缺门面时准确报告缺口。 |
| 2 | `Nova.Localization.LoadAsync()`，随后 `InitCurrentLanguageAsync()` | 准备产品文本、支持语言与字体，核对实际生效语言和必需文本/字体；启动面板自用的语言 JSON 不替代业务本地化。 |
| 3 | `Nova.Network.LoadAsync()`，成功后等待 `Nova.Config.WaitForStartupAppConfigAsync(CancellationToken)` | 准备产品实际所需的 HostKey/NetCmd 并检查加载结果，再等待本次启动的唯一一轮 GM 拉取。GM 未配置、失败或超时沿用缓存/本地默认值继续；重复等待不重拉。网络表加载成功不证明服务器可用；离线玩法不能据此删除节点。启动拉取的超时、共享结果及取消边界见 [ConfigComponent](../Runtime/Modules/Config/ConfigComponent.md)。 |
| 4 | 等待 `Nova.SDK.InitializeTask` | 按公司产品及目标渠道要求确认启用清单和配置；全局任务完成、`IsInitialized` 均不代表所有插件成功。必需插件通过现有状态查询/等待接口核实为 `Ready`，服务及厂商行为另验。 |
| 5 | `Nova.Table.LoadAsync()` | 准备产品权威表、正式数据/代码和加载映射；检查结果及必需表类型可查询，不能只看计数或流程完成。 |
| 6 | `Nova.UI.LoadAsync()` | 准备注册表、UIGroup、Prefab、字体和绑定；核目标入口已注册且实际可打开，空注册表加载成功不能代替。 |
| 7 | `Nova.Sound.LoadAsync()` | 准备声音表、音频、声音组和地址；核实际播放、停止与释放，不以空配置或请求编号当播放成功。 |
| 8 | `Nova.Vibrate.LoadAsync()` | 保留振动数据初始化与平台能力判断。设备不支持时按 `IsSupported` 等真实能力分支处理并说明边界；不能删除整个节点或声称已有真实体感。 |

标准动作、基础初始化成功、具体业务可用和上架/厂商验收分别判断。SDK 按已确认的公司产品与渠道清单接入，不照搬 MainDemo 全部演示插件；物料暂缺时按 [NA-014：授权范围、实时合同和恢复回执约束实际执行](DEVELOPMENT_CONSTRAINTS.md#na-014授权范围实时合同和恢复回执约束实际执行) 从各 Demo 复用所需临时物料，不将示例账号/密钥称为产品专有物料或正式上架通过。缺少产品清单、可用的专有/临时凭据或隐私前置时列为待准备，不擅自填空清单、关开关或假报完成。平台确无具体能力时，保留节点及其支持判断；只有用户明确改变产品范围才调整标准动作并记录影响，Agent 不自行以“单机/小游戏”裁减。

业务预加载必须包含以下执行保护：

1. **逐项检查结果**：检查实际版本所有 `UniTask<bool>` 返回值，`false` 进入失败分支；对无 bool 的任务、语言选择及 SDK 初始化，按该模块真实契约核结果。MainDemo 和其余 Demo 均检查六项 bool 与本地化实际生效语言；合法空配置或插件调度完成仍不代表产品实际所需内容及厂商服务可用。
2. **真实输入与最小可用验证**：冻结八节点的配置/物料来源、公开调用和成功条件。具体内容探针放在对应模块或专项验收，不为通过检查把示例文本键、表名和字体标记变成通用启动门禁。按每个模块具体需求验证可用性；没有公开就绪属性时不编造，有依赖入口缺口时不通过 Manager、反射或假进度绕过。空配置合法与否按领域契约判断，合法空结果也不能证明产品所需能力已接入。
3. **异步与失败收尾**：绑定本次进入的取消令牌和进入/尝试代次，关键等待后、失败回调、重试与 finally 均检查身份；禁止重复并发初始化和旧任务写回。进度只累计实际通过的步骤，全部通过才转入业务；失败、取消不得设全部完成。
4. **按实际语义重试**：提供失败提示、可执行的恢复/重试和退出处理，收尾启动 UI。Persist、SDK 等共享任务可能缓存首次失败，再 await 不代表重新初始化；不存在安全重试入口时保留阻塞项，不发明 Reset API、死循环重试或跳过该项。

该清单维护在本节，入口与 Skill 只引用并冻结本次执行事实，不另建按项目长期保存的验收账本。已有项目的普通页面/数值局改不自动触发整套迁移；在新建游戏、改造预加载或评估产品基线时，缺项必须明确报告并由相应 Operation 补齐。基础流程可按阶段实施，但八项未完成不能称为“标准产品预加载基线已就绪”。

### 按实际依赖逐项准备

从零到一按“识别工程与工具 → 只读盘点 → 选定各阶段 Skill/contract、参数来源及依赖 → 执行既有准备 Operation → 最小启动验收 → 依赖业务 → 资源/Player 及目标运行验收”推进。按实际依赖串行衔接，不把全部 Skill 固定串成新流水线；独立工作可以继续。每阶段首次写入前按 [NA-002：先核对当前能力，再选择接口或提出补充](DEVELOPMENT_CONSTRAINTS.md#na-002先核对当前能力再选择接口或提出补充) 核对实际入口，按 [NA-014：授权范围、实时合同和恢复回执约束实际执行](DEVELOPMENT_CONSTRAINTS.md#na-014授权范围实时合同和恢复回执约束实际执行) 核对参数，创建 Master、首次填表和初始化脚本均不豁免。

| 层级 | 进入依赖它的业务阶段前必须具备的事实 | 复用入口 |
| --- | --- | --- |
| 工程与工具 | 精确工程路径、Unity/Framework 解析版本、目标平台 Build Support；Editor 无阻塞窗口、无阻断编译错误；所需 MCP/CLI、Python、Luban/dotnet 实际可用 | `nova-project-check-readiness`；[环境检查边界](../Editor/EditorUtil/EditorUtil.Environment/EditorUtil.Environment.md) |
| 平台构建环境 | Android 的 SDK/NDK/JDK、IL2CPP 与 ABI，或本次目标的对应工具；实际使用的原生依赖解析和必要签名配置，不用另一平台的成功代替 | `nova-project-preflight-build`、`nova-project-resolve-android-dependencies`、构建 Skill |
| 场景与显示 | canonical connected instance、唯一运行拓扑、启用场景和 Content 所有者；方向、设计分辨率、支持比例和安全区方案已确定 | `nova-project-setup-entry-scene`；[屏幕适配](../Runtime/Modules/UI/UIComponent.md#屏幕适配与安全区) |
| 固定启动资源 | 三面板、字体/材质、闪屏贴图、所需启动语言 JSON 已导入并绑定；Resources 键唯一，启动前可读，无业务 DLL/Bundle 反依赖 | 启动场景 Skill 的固定资源分支；[资源工作流](RESOURCE_WORKFLOW.md) |
| 权威配置 | 项目自有 ConfigMaster、正确 Platform × Channel × DevelopMode、正式导出的 ConfigRuntime、激活绑定和场景快照一致；App/Privacy/SDK 等参数逐项核对业务来源、格式及共享范围，不能把包名、游戏名或随机值当后台物料 | `nova-project-configure-runtime`、所选 SDK/Kit Skill；[App 参数来源](../Runtime/Modules/Config/AppConfigs.md#取值来源与格式) |
| 表与业务物料 | 本阶段所需的权威表源、正式数据/代码导出、加载映射、UIGroup、Prefab、字体/图片/声音等已准备并绑定；配表沿用[官方或项目既有样式](RESOURCE_WORKFLOW.md#配表模板与样式)，占位物料也须实际可加载 | 对应表/UI/本地化/声音/振动 Operation，不手改生成物 |
| 资源加载 | 实际 YooAsset Settings、Collector、Package、地址与所选运行模式相符；本地包/首包或远端内容满足该模式，不把 Editor 模拟当设备资源可用 | `nova-project-integrate-resource`、`nova-project-build-bundles` |
| 日常流水线（Pipify，必需） | 按[三工具基线](#configpipifyplugpals-基线) 完成工作区配对、项目 Batch 保存与授权范围内的实际运行；创建前只读参考当前 MainDemo 的相关 Batch，按目标适配步骤、顺序、参数和前置，不能把示例名称或顺序视为完整交付证明 | `nova-project-configure-pipeline`；[新项目的 Batch 参考](../Editor/EditorUtil/EditorUtil.Pipify/EditorUtil.Pipify.ConfigurationEditing.md#新项目的-batch-参考) |
| 包管理（PlugPals，必需） | 按[三工具基线](#configpipifyplugpals-基线) 核实仓库配置、实际包查询与依赖解析；按任务需要自主选包，不照搬 Demo 全部依赖 | `nova-project-manage-upm-package`；安装授权按 [NA-014：授权范围、实时合同和恢复回执约束实际执行](DEVELOPMENT_CONSTRAINTS.md#na-014授权范围实时合同和恢复回执约束实际执行) |
| 业务入口与程序集 | asmdef、命名空间、生成 DLL、入口类型、Config 与加载地址分别匹配当前 `ProcedureLoadDll`；预加载有明确顺序、结果判定、取消及失败路径 | `nova-project-integrate-procedure`；配置/导出物由相应 Operation 承担 |
| HybridCLR（必需） | 按[三平台构建与 HybridCLR 基线](#三平台构建与-hybridclr-基线)核对包、本地安装/版本、启用、业务程序集、完整 DLL/link 配置、资源映射及当前目标正式生成；不能以非热更方案或普通 APK 成功代替 | [HybridCLR 安装与验收](../Editor/EditorUtil/EditorUtil.Environment/EditorUtil.Environment.md#hybridclr-安装与验收)；配置、Generate、DLL 刷新、Bundle/Player 各自 Operation |

Agent 从工程读取可确定的路径、字段和值，在本轮说明中列出每项的 `已就绪 / 待准备 / 阻塞`、依据、负责 Skill/真实 Adapter、参数来源及依赖关系；具体平台能力限制单独记录，不把标准预加载模块整体标成“不适用”。项目专有物料暂缺时，按 [NA-014：授权范围、实时合同和恢复回执约束实际执行](DEVELOPMENT_CONSTRAINTS.md#na-014授权范围实时合同和恢复回执约束实际执行) 从 Nova 各 Demo 查找可用的临时配套物料，记录临时来源和后续替换位置，不因尚无专有物料直接停工。其他确实与目标无关的工程层级可以说明不适用。不要求用户逐字段填写，也不强制新增长期清单文件；产品及渠道清单之外的演示 SDK 不自动安装。临时来源也不能满足必需物料，或缺权限/安全执行通道时，停住依赖该项的阶段，继续独立准备工作，不能先绕过再补。

### 开工放行与验收

经 Unity 保存并读回配置、资源引用及正式导出结果，沿真实入口确认固定启动 UI 可加载/显示，按标准产品预加载基线逐项取得加载结果及必要可用性证据，再开展依赖业务。测试预加载时可以使用项目自有最小落点；它不代替后来正式业务入口和完整内容验收。只验证 UI/Sound、初始化请求已返回、安装按钮已点、进度条完成、目录存在或单独 APK 构建成功，都不能作为整套前置就绪的证明。

Editor 启动、目标 Player、真机、热更与外部服务分层报告；只有任务所需层级完整时才能称对应范围成功。必要预置内容仍缺失时，不得报告“环境全部准备完成”。新项目基础准备采用上述既有 Operation 组合，不另造一套注册、配置或资源管理器。

## 先建立事实清单

按项目检查以下内容：

1. 用户从哪个场景进入游戏，Build Settings 中有哪些启用场景。
2. 哪个场景或启动流程承载 Nova。
3. 是否存在运行时 Additive 加载的业务场景，以及谁负责加载和卸载。
4. 是否存在测试、插件 Demo、工具或美术预览场景；它们不应自动被视为产品场景。
5. 项目是否声明了 Nova 托管范围；未声明内容不做合法性推断。

## Nova 根节点

只要场景选择承载 Nova，就使用完整的 canonical [Nova.prefab](../../Prefabs/Nova.prefab)。不要把 `Nova` 与若干 `FrameworkComponent` 手工挂到普通 GameObject 上，因为这样很容易遗漏组件、引用和初始化关系。

同一时刻共同运行的场景拓扑只应有一个有效 Nova。单独编辑的测试场景或不会共同加载的替代入口，不应仅因仓库中还存在另一个 Nova 场景就被判为冲突。

## 单场景与多场景拓扑按项目选择

Nova 不要求统一的场景数量或命名。以下都是可行结构：

- 单场景：Nova 与业务内容位于同一入口场景。
- 入口加 Content：入口承载 Nova，业务场景按项目流程 Additive 加载。
- 自定义启动链：项目自己的 Bootstrap 决定何时进入承载 Nova 的场景。
- 多个替代入口：开发、测试或不同产品入口分别存在，但不会在同一运行拓扑同时激活。

不要为了满足文档而拆场景。先确认项目的生命周期、加载成本和协作方式，再选择结构。

## Content 的边界

Content 是项目可选的场景角色，不是所有项目必须存在的结构。项目一旦明确把某场景声明为 Content：

- 场景不再承载 Nova 或其它 Nova 根副本。
- 场景不会自动加载；Nova 不会自动加载 Content。
- 加载时机、Additive 参数、失败处理和卸载责任由项目代码明确实现。
- 使用 YooAsset 场景句柄时，生命周期结束应调用 `UnloadAsync()`。

资源化场景可从 [AssetComponent](../Runtime/Modules/Asset/AssetComponent.md) 与 [ISceneHandle](../Runtime/Modules/Asset/AssetManager/Interfaces/ISceneHandle.md) 继续阅读。项目若选择 Unity 原生场景流程，也应明确持有者和卸载边界。

## 不应被普遍强制的结构

- 固定的 Starter 目录、namespace、场景名或场景数量。
- 每个 Scene 都必须登记角色。
- Contract、representative asset 或分目录 `AGENTS.md` 必须存在。
- Content 自动补载或自动写入 Build Settings。
- 插件 Demo、测试场景和未参与产品闭包的 Sample 必须满足产品场景规则。

这些能力可由具体项目显式采用，但不能用来判断所有 Nova 项目是否合法。

## Sample 的导入、辅助工具与页面边界

先读取 [当前 Framework 的 Sample 分发声明](../../package.json) 中的 `samples`，再通过 Unity Package Manager 的实际导入结果及该 Sample 的 `SamplePathManifest` 确定本工程根目录。开发仓 `Assets/Samples/<Sample>`、包内 `Samples~/<Sample>` 和消费工程导入目录是不同位置；下面的源码、表与资源路径均须在对应的实际 Sample 根下定位，不能从 Framework 文档跨目录拼接。

Sample 的源码命名空间、asmdef 名、生成 DLL、ConfigRuntime 中的入口/Binding、资源地址和实际导入目录各有独立来源。典型数据链是 Excel/schema 与生成配置 → 正式 JSON/访问代码 → 场景或 Runtime Config 引用 → 实际加载；复制出来的源码和清单只是骨架，不能由文件名推断已导出、已编译或已经加载。YooAsset 地址由有效 Collector 规则生成，字体表只提供地址/材质/比例，不创建真实字体；link.xml 的 preserve 声明也不证明实际裁剪和 AOT 已通过。

MainDemo 实际导入根下的 `PSD2UGUI/` 是随公开 MainDemo 提供的可选制作示例（Nova 开发仓来源为 `Assets/Samples/MainDemo/PSD2UGUI`；消费态根目录按本节的分发声明与当前导入清单定位），插件源码和 DLL 仍仅由内部包提供，不增加 MainDemo Runtime 的程序集或包依赖。其 `Psd2UguiSampleAvailability` 仅在 Editor 中检查插件类型：缺少插件时，将本目录内引用插件脚本的配置、编辑树、来源文档和复用索引连同 `.meta` 收到 `Authoring~`；插件可用时恢复原位置。普通 PSD、图片和生成的 uGUI 不移动。两处同名文件或未保存编辑会停止迁移，不覆盖数据；卸载前须先保存并关闭编辑树，这个通知回调不能取消 Unity 包卸载。首次检查在脚本重载后执行，完成前不要打开插件编辑资产；项目其它目录的插件资源不在此保护范围。

`SamplePathManifest` 的字段 getter 只是读取配置，`RewriteTargets` 返回底层列表的只读接口而非独立快照。`SamplePathRewriter` 在 Editor 域加载后延迟检查并按清单改写导入路径，匹配完整目录前缀，兼容 Windows 分隔符和 JSON 转义；已经指向目标或名称相似的其它目录保持不变。重写目标限定在当前 Sample 内，并能找到暂存到 `Authoring~` 或已恢复原位的文件，避免缺插件时漏掉配置、编辑树和 `.ui.asset`。两处同时存在会拒绝猜测；外部自定义路径不匹配开发前缀，保持原值。它不是跨文件事务，也不提供通用符号链接安全保证。文件未齐时有界重试，不等于持续监听所有导入；标记只用于内部重写状态，不证明资产绑定正确。已有自动工具的资产文本写入不能成为 Agent 手工改写 YAML 的先例，实际资产维护仍经 Editor/MCP。

导入准备还会先检查清单的 `GeneratedDirectories`，即使已有重写完成标记也会恢复缺失的生成目录。开发态 [nova-samples.json](../../nova-samples.json) 的 `generatedDirectories` 是发布声明，发布器将它写入暂存 Sample 的 `m_GeneratedDirectories`；旧清单默认空列表，不猜测或创建普通资源目录。当前各 Sample 声明 `Dlls/AOTMetas` 和 `Dlls/GameDlls`。发布仅保留这些目录的骨架、文件夹 `.meta` 与隐藏占位 `.nova-keep`，排除声明范围内的本机 DLL 输出；占位不作为 Unity 资产或 YooAsset 资源收集。其他位置的合法程序集、数据和插件保持原内容。

目录准备仅限当前清单所属的 `Assets/Samples/` 根下，拒绝越界、符号链接、非法相对路径及同名文件，通过 `AssetDatabase` 注册并核验目录，不覆盖已有文件，不修改工作区绑定。该限制只约束新目录准备，不扩展为旧字符串重写器的通用安全保证。单个失效清单只报告自身问题，不阻断其他有效 Sample。目录存在仅证明 Editor 模拟采集可访问该路径；缺少普通源资产仍是错误，必需 DLL 的生成、复制、正式资源构建与 Player/设备验收仍须独立完成。

公开 Sample 的隐私密钥和厂商物料为脱敏占位值。开始 Play 前，需在当前 Platform × Channel × DevelopMode 的隐私配置填写自己的 AES-Key / AES-IV（UTF-8 各 16 字节），保存并导出运行时配置；目录恢复不豁免该初始化前提。隔离验收可以使用只属于临时工程的测试配置，但不能把测试密钥写回开发真源或随包发布，也不能把厂商占位值视为真实服务可用的证据。

`SampleVersionGuard` 的版本比较使用前三段数字，不是完整 SemVer 或 UPM lock 判断。某些实现按整个旧版本目录调用 `DeleteAsset`，其中可能包含同版其他 Sample 及用户改动，范围大于弹窗中提到的单个 Demo；确认前核对完整目标，失败不提供事务恢复。旧版删除询问与启动场景询问是两个独立动作，拒绝删除不表示后续不会再询问场景；场景文件存在也不证明其可运行。专用 PrefabBuilder、配置补齐或诊断桥同样只服务自己的固定 Sample，不是可泛化的任意反射/任意资产写入口。

微信示例的 `DemoWechatMiniGameSetup` 先通过当前类型对应的 MonoScript 定位所属脚本，只允许 canonical 开发真源进入自动配置与显式重建。导入副本在读取配置或 Prefab 前跳过自动处理，显式 `Build` 拒绝跨根执行，避免把开发仓固定路径当作消费工程路径，或覆盖项目组已导入的配置与页面。源资产缺失仍由原开发步骤报错；消费工程通过 ConfigWindow、Pipify 与 Prefab Inspector 维护自身资产。

Sample 的 `BaseDemoView` 在首次 `OnInit` 接线，池化重开不等于重新初始化。关闭默认只清本页登记的反馈行并进入 UIView 关闭链，清反馈不结束账号、取消网络或关闭厂商页面；派生类自行管理业务等待、事件、纹理和句柄。反馈最多保留 200 行并在帧末销毁旧对象，`Success` 只是显示级别，不证明下层任务成功。模板或字段缺失的静默跳过也不证明 Prefab 完整绑定。

启动 `ProcedureLogin` 的一秒等待属于演示流程，不进行游戏服认证；具体页面按钮才可能请求真实 GameLogin。各 Sample 均执行上述八步，只有检查通过才累计进度并进入后续流程，重试从零开始；SDK 专属示例还按自身实际平台检查必需插件，不能把不支持平台的演示分支视作厂商初始化成功。固定令牌、进入与尝试代次用于隔离旧任务、旧弹窗与 finally，但不承诺底层全局加载立即停止或重新初始化。Persist、SDK 的共享任务失败后需要按其真实恢复能力处理，重新等待不等于重新初始化；各 Demo 的独立取消和清理说明以其 README 及模块 Docs 为准。
