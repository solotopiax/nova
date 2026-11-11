# 项目与场景结构

本页帮助 Agent 识别 Nova 的最小结构。已有项目的结论来自当前工程；新建 Nova 项目默认采用下述 MainDemo 启动基线，按产品需要裁剪，不把整个 Demo 当成产品模板。

## 新项目开工准备

新建项目先准备基础环境、固定启动资源和配置，跑通最小启动闭环，再开展依赖这些前置的业务制作。准备工作本身可以创建必要配置、预加载代码和占位资源；尚未开发的业务内容先明确来源与接入阶段，在开始消费它们前补齐，不能伪称已存在。已有工程仅核对本次相关项，不自动迁移启动架构。

### 默认启动基线

- 使用完整 canonical `Nova.prefab`，保留框架启动入口。默认框架链为 `ProcedureSplash → ProcedureCheckVersion → 按配置选择更新流程 → ProcedureLoadDll → 项目业务入口`；不能把业务预加载类型直接填入框架入口字段来跳过启动准备。
- 默认引入 MainDemo 的 Resources 固定启动资源及真实依赖，配置 `ProcedureComponent.LauncherSettings`；实际来源、复制边界、默认键与验证见 [固定启动资源](RESOURCE_WORKFLOW.md#新项目默认固定启动资源)。路径有值不代表资源已存在。
- 业务预加载参考当前 MainDemo 的 `ProcedurePreload`：`Persist → Localization.LoadAsync / InitCurrentLanguageAsync → Network → 等待 SDK.InitializeTask → Table → UI → Sound → Vibrate`，再进入产品首个业务流程。按实际模块依赖保留、排序或明确跳过；纯单机也须确认存储、UI、声音等实际前置，不能仅因没有服务器就跳过全部初始化。
- 参考其流程取消、代际保护、失败重试和启动面板收尾，重新建立项目自己的命名空间、程序集、入口与数据。Demo 导航、示例表、账号、密钥和演示场景不是默认产品依赖。当前 Demo 有加载 bool 未检查的边界，生产接线须检查实际结果与模块可用性，不能以八步/100% 进度判成功。

### 按实际依赖逐项准备

| 层级 | 进入依赖它的业务阶段前必须具备的事实 | 复用入口 |
| --- | --- | --- |
| 工程与工具 | 精确工程路径、Unity/Framework 解析版本、目标平台 Build Support；Editor 无阻塞窗口、无阻断编译错误；所需 MCP/CLI、Python、Luban/dotnet 实际可用 | `nova-project-check-readiness`；[环境检查边界](../Editor/EditorUtil/EditorUtil.Environment/EditorUtil.Environment.md) |
| 平台构建环境 | Android 的 SDK/NDK/JDK、IL2CPP 与 ABI，或本次目标的对应工具；实际使用的原生依赖解析和必要签名配置，不用另一平台的成功代替 | `nova-project-preflight-build`、`nova-project-resolve-android-dependencies`、构建 Skill |
| 场景与显示 | canonical connected instance、唯一运行拓扑、启用场景和 Content 所有者；方向、设计分辨率、支持比例和安全区方案已确定 | `nova-project-setup-entry-scene`；[屏幕适配](../Runtime/Modules/UI/UIComponent.md#屏幕适配与安全区) |
| 固定启动资源 | 三面板、字体/材质、闪屏贴图、所需启动语言 JSON 已导入并绑定；Resources 键唯一，启动前可读，无业务 DLL/Bundle 反依赖 | 启动场景 Skill 的固定资源分支；[资源工作流](RESOURCE_WORKFLOW.md) |
| 权威配置 | 项目自有 ConfigMaster、正确 Platform × Channel × DevelopMode、正式导出的 ConfigRuntime、激活绑定和场景快照一致；所用 Privacy/SDK 配置具备真实来源 | `nova-project-configure-runtime`、所选 SDK/Kit Skill；不沿用 Sample 密钥/账号 |
| 表与业务物料 | 本阶段所需的权威表源、正式数据/代码导出、加载映射、UIGroup、Prefab、字体/图片/声音等已准备并绑定；占位物料也须实际可加载 | 对应表/UI/本地化/声音/振动 Operation，不手改生成物 |
| 资源加载 | 实际 YooAsset Settings、Collector、Package、地址与所选运行模式相符；本地包/首包或远端内容满足该模式，不把 Editor 模拟当设备资源可用 | `nova-project-integrate-resource`、`nova-project-build-bundles` |
| 业务入口与程序集 | asmdef、命名空间、生成 DLL、入口类型、Config 与加载地址分别匹配当前 `ProcedureLoadDll`；预加载有明确顺序、结果判定、取消及失败路径 | `nova-project-integrate-procedure`；配置/导出物由相应 Operation 承担 |
| HybridCLR | 包解析、本地安装及版本/Unity 匹配、启用策略分别核验；使用热更时补齐列表、DLL、link/AOT 和目标平台产物 | [HybridCLR 安装与验收](../Editor/EditorUtil/EditorUtil.Environment/EditorUtil.Environment.md#hybridclr-安装与验收)；刷新 DLL/Generate/Bundle/Player 各自 Operation |

Agent 从工程读取可确定的路径、字段和值，在本轮说明中列出每项的 `已就绪 / 待准备 / 不适用`、依据、负责 Operation 和依赖关系；不要求用户逐字段填写，也不强制新增长期清单文件。无关 SDK、服务器、商店或未使用模块不为了凑表而安装。缺少必需物料、权限或安全执行通道时，停住依赖该项的阶段，继续独立准备工作，不能先绕过再补。

### 开工放行与验收

经 Unity 保存并读回配置、资源引用及正式导出结果，沿真实入口确认固定启动 UI 可加载/显示、当前阶段所需预加载模块确实可用，再进入业务开发。测试预加载时可以使用项目自有最小落点；它不代替后来正式业务入口和完整内容验收。初始化请求已返回、安装按钮已点、进度条完成、目录存在或单独 APK 构建成功，都不能作为整套前置就绪的证明。

Editor 启动、目标 Player、真机、热更与外部服务分层报告；只有任务所需层级完整时才能称对应范围成功。必要预置内容仍缺失时，不得报告“环境全部准备完成”。新项目基础准备采用上述既有 Operation 组合，不另造一套注册、配置或资源管理器。

## 先建立事实清单

按项目检查以下内容：

1. 用户从哪个场景进入游戏，Build Settings 中有哪些启用场景。
2. 哪个场景或启动流程承载 Nova。
3. 是否存在运行时 Additive 加载的业务场景，以及谁负责加载和卸载。
4. 是否存在测试、插件 Demo、工具或美术预览场景；它们不应自动被视为产品场景。
5. 项目是否声明了 Nova 托管范围；未声明内容不做合法性推断。

## Nova 根节点

只要场景选择承载 Nova，就使用完整的 canonical `Assets/Framework/Prefabs/Nova.prefab`。不要把 `Nova` 与若干 `FrameworkComponent` 手工挂到普通 GameObject 上，因为这样很容易遗漏组件、引用和初始化关系。

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

Sample 的源码命名空间、asmdef 名、生成 DLL、ConfigRuntime 中的入口/Binding、资源地址和实际导入目录各有独立来源。典型数据链是 Excel/schema 与生成配置 → 正式 JSON/访问代码 → 场景或 Runtime Config 引用 → 实际加载；复制出来的源码和清单只是骨架，不能由文件名推断已导出、已编译或已经加载。YooAsset 地址由有效 Collector 规则生成，字体表只提供地址/材质/比例，不创建真实字体；link.xml 的 preserve 声明也不证明实际裁剪和 AOT 已通过。

`Assets/Samples/MainDemo/PSD2UGUI` 是随公开 MainDemo 提供的可选制作示例，插件源码和 DLL 仍仅由内部包提供，不增加 MainDemo Runtime 的程序集或包依赖。其 `Psd2UguiSampleAvailability` 仅在 Editor 中检查插件类型：缺少插件时，将本目录内引用插件脚本的配置、编辑树、来源文档和复用索引连同 `.meta` 收到 `Authoring~`；插件可用时恢复原位置。普通 PSD、图片和生成的 uGUI 不移动。两处同名文件或未保存编辑会停止迁移，不覆盖数据；卸载前须先保存并关闭编辑树，这个通知回调不能取消 Unity 包卸载。首次检查在脚本重载后执行，完成前不要打开插件编辑资产；项目其它目录的插件资源不在此保护范围。

`SamplePathManifest` 的字段 getter 只是读取配置，`RewriteTargets` 返回底层列表的只读接口而非独立快照。`SamplePathRewriter` 在 Editor 域加载后延迟检查并按清单改写导入路径，匹配完整目录前缀，兼容 Windows 分隔符和 JSON 转义；已经指向目标或名称相似的其它目录保持不变。重写目标限定在当前 Sample 内，并能找到暂存到 `Authoring~` 或已恢复原位的文件，避免缺插件时漏掉配置、编辑树和 `.ui.asset`。两处同时存在会拒绝猜测；外部自定义路径不匹配开发前缀，保持原值。它不是跨文件事务，也不提供通用符号链接安全保证。文件未齐时有界重试，不等于持续监听所有导入；标记只用于内部重写状态，不证明资产绑定正确。已有自动工具的资产文本写入不能成为 Agent 手工改写 YAML 的先例，实际资产维护仍经 Editor/MCP。

导入准备还会先检查清单的 `GeneratedDirectories`，即使已有重写完成标记也会恢复缺失的生成目录。开发态 `nova-samples.json` 的 `generatedDirectories` 是发布声明，发布器将它写入暂存 Sample 的 `m_GeneratedDirectories`；旧清单默认空列表，不猜测或创建普通资源目录。当前各 Sample 声明 `Dlls/AOTMetas` 和 `Dlls/GameDlls`。发布仅保留这些目录的骨架、文件夹 `.meta` 与隐藏占位 `.nova-keep`，排除声明范围内的本机 DLL 输出；占位不作为 Unity 资产或 YooAsset 资源收集。其他位置的合法程序集、数据和插件保持原内容。

目录准备仅限当前清单所属的 `Assets/Samples/` 根下，拒绝越界、符号链接、非法相对路径及同名文件，通过 `AssetDatabase` 注册并核验目录，不覆盖已有文件，不修改工作区绑定。该限制只约束新目录准备，不扩展为旧字符串重写器的通用安全保证。单个失效清单只报告自身问题，不阻断其他有效 Sample。目录存在仅证明 Editor 模拟采集可访问该路径；缺少普通源资产仍是错误，必需 DLL 的生成、复制、正式资源构建与 Player/设备验收仍须独立完成。

公开 Sample 的隐私密钥和厂商物料为脱敏占位值。开始 Play 前，需在当前 Platform × Channel × DevelopMode 的隐私配置填写自己的 AES-Key / AES-IV（UTF-8 各 16 字节），保存并导出运行时配置；目录恢复不豁免该初始化前提。隔离验收可以使用只属于临时工程的测试配置，但不能把测试密钥写回开发真源或随包发布，也不能把厂商占位值视为真实服务可用的证据。

`SampleVersionGuard` 的版本比较使用前三段数字，不是完整 SemVer 或 UPM lock 判断。某些实现按整个旧版本目录调用 `DeleteAsset`，其中可能包含同版其他 Sample 及用户改动，范围大于弹窗中提到的单个 Demo；确认前核对完整目标，失败不提供事务恢复。旧版删除询问与启动场景询问是两个独立动作，拒绝删除不表示后续不会再询问场景；场景文件存在也不证明其可运行。专用 PrefabBuilder、配置补齐或诊断桥同样只服务自己的固定 Sample，不是可泛化的任意反射/任意资产写入口。

微信示例的 `DemoWechatMiniGameSetup` 先通过当前类型对应的 MonoScript 定位所属脚本，只允许 canonical 开发真源进入自动配置与显式重建。导入副本在读取配置或 Prefab 前跳过自动处理，显式 `Build` 拒绝跨根执行，避免把开发仓固定路径当作消费工程路径，或覆盖项目组已导入的配置与页面。源资产缺失仍由原开发步骤报错；消费工程通过 ConfigWindow、Pipify 与 Prefab Inspector 维护自身资产。

Sample 的 `BaseDemoView` 在首次 `OnInit` 接线，池化重开不等于重新初始化。关闭默认只清本页登记的反馈行并进入 UIView 关闭链，清反馈不结束账号、取消网络或关闭厂商页面；派生类自行管理业务等待、事件、纹理和句柄。反馈最多保留 200 行并在帧末销毁旧对象，`Success` 只是显示级别，不证明下层任务成功。模板或字段缺失的静默跳过也不证明 Prefab 完整绑定。

启动 `ProcedureLogin` 的一秒等待属于演示流程，不进行游戏服认证；具体页面按钮才可能请求真实 GameLogin。各 Sample 的预加载模块不同，应读当前 `ProcedurePreload`；完成计步只证明相应 await 返回，不保证所有 bool 结果已检查。旧复制版还可能在异常重试时保留计数，不能以进度条完成代替模块可用性。关闭或离开流程对令牌/代际的保护也不承诺所有底层全局加载立即停止，各 Demo 的独立取消和清理说明以其 README 及模块 Docs 为准。
