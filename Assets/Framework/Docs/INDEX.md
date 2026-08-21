# Framework 文档索引

> 首次接入 Nova、接手陌生项目或开始场景/资源/构建任务，先读 [Nova Agent 快速入口](START_HERE.md)。
>
> 对于当前 Skills 已覆盖的 Nova 日常项目任务，直接用自然语言说明目标、范围和约束。安装或升级 Nova 后首次打开 Unity，包内全部 Nova Project Skills 会自动投影到项目 `.agents/skills/` 供 Agent 发现，不需要执行 `sync` 或手工复制；当前 30 项能力、渐进式披露与 Action Adapter 边界见 [Nova Project Skills](../Agents/INDEX.md)，消费者 Git 边界见 [START_HERE.md](START_HERE.md)。
>
> 架构总览、设计规范、陷阱说明见 [ARCHITECTURE.md](ARCHITECTURE.md)

---

## 按任务类型快速定位

> 找到你的任务，直接跳转必读文档，不必逐行阅读索引。

| 任务 | 必读文档（按顺序） |
|------|-------------------|
| **当前 Skills 覆盖的日常 Nova 项目任务** | 用自然语言描述任务 → [Nova Project Skills](../Agents/INDEX.md)（确认匹配 Skill、影响范围与证据门）→ 该 Skill 引用的模块 Docs；全量可发现不代表全量顺序执行。 |
| **首次接入 / 接手陌生 Nova 项目** | [START_HERE.md · Agent 快速入口](START_HERE.md) → [项目与场景结构](Onboarding/PROJECT_STRUCTURE.md) → [资源工作流](Onboarding/RESOURCE_WORKFLOW.md) → [验证与构建](Onboarding/VALIDATION.md) |
| **检查 Nova 场景 / Resources 边界** | [EditorUtil.ProjectGuard.md · 集中规则与 Profiles](Editor/EditorUtil/EditorUtil.ProjectGuard.md) → [ProjectGuardWindow.md · 只读窗口](Editor/Windows/ProjectGuardWindow.md) |
| 理解框架整体架构与设计模式 | [ARCHITECTURE.md](ARCHITECTURE.md) |
| **新增业务 UI 界面**（UIView 子类） | [UIComponent.md · 泛型 Open API + 注册表](Runtime/Modules/UI/UIComponent.md) → [UIView.md · UIView 继承模板](Runtime/Modules/UI/Definitions/UIView.md) → [UIManager.md · OpenUIView 流程](Runtime/Modules/UI/UIManager/UIManager.md) |
| **新增全局事件** | [EventManager.md · EventData模板 + 订阅/发布](Runtime/Modules/Event/EventManager.md) |
| **新增 Runtime 模块**（Component + Manager） | [Runtime.md · 9 步骤](Runtime/Runtime.md) → [FrameworkComponent.md](Runtime/Modules/FrameworkComponent.md) → [FrameworkManager.md · 三层继承规范](Runtime/Modules/FrameworkManager.md) |
| **查询 / 请求系统通知权限或应用内评价**（Native） | [NativeComponent.md · 显式请求、状态与平台边界](Runtime/Modules/Native/NativeComponent.md) → [NativeManager.md · Android / iOS 分发与并发语义](Runtime/Modules/Native/Managers/Implements/NativeManager.md) |
| **新增 Inspector 面板** | [Editor.md · 开发规范](Editor/Editor.md) → [BaseComponentInspector.md · 子类模板](Editor/Inspectors/BaseComponentInspector.md) |
| **Inspector 运行时数据面板** | [IEditorRuntimeDrawer.md](Editor/Definitions/IEditorRuntimeDrawer.md) → [EditorUtil.Serializer.md · 读取私有字段](Editor/EditorUtil/EditorUtil.Serializer/EditorUtil.Serializer.md) |
| **复用纯 C# 对象（零 GC）** | [ReferencePool.md · IReference + Get/Put](Runtime/Core/Reference/ReferencePool.md) |
| **复用 GameObject / 资源对象** | [ObjectPoolManager.md · ObjectBase + CreatePool](Runtime/Modules/ObjectPool/ObjectPoolManager.md) |
| **加载 AB 包 / Prefab 实例化** | [AssetComponent.md](Runtime/Modules/Asset/AssetComponent.md)（所有 LoadXxx 返回 Handle，调用方负责 Release）|
| **实例化 Prefab / 销毁 Prefab 实例** | [PrefabComponent.md](Runtime/Modules/Prefab/PrefabComponent.md) → [IPrefabManager.md · Instantiate/Destroy API](Runtime/Modules/Prefab/PrefabManager/IPrefabManager.md) |
| **大版本检查 / APP 强更** | [AppComponent.md · CheckAsync+DownloadAsync+OpenStoreAsync](Runtime/Modules/App/AppComponent.md) → [AppManagerConfig.md · 超时+下载路由+规则](Runtime/Modules/App/Definitions/AppManagerConfig.md) |
| **加载运行时配置（AB 加载 ConfigRuntimeSO）** | [ConfigComponent.md](Runtime/Modules/Config/ConfigComponent.md) → [ConfigManager.md · AB加载+解析+PluginConfig索引](Runtime/Modules/Config/ConfigManager.md) |
| **加载 Excel/CSV 表格数据（Luban Project）** | [TableManager.md · 多 Binding 加载](Runtime/Modules/Table/TableManager.md) → [TableComponentInspector.md · 多 Project 与导出描述](Editor/Inspectors/TableComponentInspector/TableComponentInspector.md) → [EditorUtil.Table.Exporter.md · 多导出描述透传](Editor/EditorUtil/EditorUtil.Table/EditorUtil.Table.Exporter.md) |
| **编辑 Config SO / 导出 ConfigRuntime**（ConfigWindow 流程） | [ConfigWindow.md · 三段式布局+三维导出](Editor/Windows/ConfigWindow.md) → [ConfigMasterSO.md · Editor 设计态数据](Editor/Config/ConfigMasterSO.md) → [SchemaMigration.md · 旧资产迁移](Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.SchemaMigration.md) → [ConfigRuntimeSO.md · Runtime 快照](Runtime/Modules/Config/ConfigRuntimeSO.md) → [EditorUtil.Config.Exporter.md](Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.Exporter.md) → [EditorUtil.Config.WorkspaceActive.md · 激活 Master 锚点](Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.WorkspaceActive.md) → [EditorUtil.Config.YooAssetInjector.md · YooAsset 注入](Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.YooAssetInjector.md) |
| **Config 面板按平台/渠道/模式分别配置**（per-panel 可勾选维度） | [PanelDimensionMask.md · Editor 掩码三轴+IsGlobal](Editor/Config/Definitions/PanelDimensionMask.md) → [EditorUtil.Config.DimensionProjector.md · 三操作+双路径](Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.DimensionProjector.md) → [EditorUtil.Config.DimensionalResolver.md · 只读取数+回落逻辑](Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.DimensionalResolver.md) |
| **新增 SDK PluginConfig**（ISDKPluginConfig + 自动注入） | [PluginBase.md · PluginBase<TConfig>泛型基类+自动注入](Runtime/Modules/SDK/Definitions/PluginBase.md) → [ISDKPluginConfig.md · 接口契约](Runtime/Modules/SDK/Definitions/ISDKPluginConfig.md) → [EditorUtil.Config.SDKPluginScanner.md · 扫描工具](Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.SDKPluginScanner.md) → [PlatformChannelEntry.md · Editor 矩阵行结构](Editor/Config/Definitions/PlatformChannelEntry.md) |
| **新增 Kit 配置（IKitConfig + ConfigWindow 配置）** | [IKitConfig.md · marker 接口](Runtime/Modules/Config/Definitions/IKitConfig.md) → [KitConfigMissingException.md · 缺失异常](Runtime/Modules/Config/Definitions/KitConfigMissingException.md) → [EditorUtil.Config.KitConfigScanner.md · 扫描工具](Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.KitConfigScanner.md) → [ConfigWindow.md · Kit 配置一级组](Editor/Windows/ConfigWindow.md) |
| **构建 AssetBundle / RawFile (CI/编辑器菜单)** | [EditorUtil.BundleBuilder.md · YooAsset Scriptable/RawFile 构建封装](Editor/EditorUtil/EditorUtil.BundleBuilder/EditorUtil.BundleBuilder.md) → [PipifySteps.md · `bundlebuilder.build` / `bundlebuilder.build_raw_file` Step](Editor/EditorUtil/EditorUtil.Pipify/PipifySteps.md) |
| **管理私有 UPM 包（安装/升级/卸载/搜索/UPM 联动）** | [PlugPalsWindow.md · Verdaccio 包管理窗口](Editor/Windows/PlugPalsWindow.md) → [EditorUtil.PlugPals.md · 工具层能力](Editor/EditorUtil/EditorUtil.PlugPals/EditorUtil.PlugPals.md) |
| **检查 UPM 包是否有新版本（启动弹窗 / 手动打开）** | [EditorUtil.CheckUpdate.md · 版本检查工具](Editor/EditorUtil/EditorUtil.CheckUpdate/EditorUtil.CheckUpdate.md) → [CheckUpdateWindow.md · 更新提示窗口](Editor/Windows/CheckUpdateWindow.md) |
| **Inspector GUI 绘制工具** | [EditorUtil.Draw.md · 全方法签名](Editor/EditorUtil/EditorUtil.Draw/EditorUtil.Draw.md) |
| **持久化存储（读写数据）** | [PersistComponent.md · 直接访问属性](Runtime/Modules/Persist/PersistComponent.md) → [PlayerPrefsManager.md](Runtime/Modules/Persist/PlayerPrefsManager.md) / [FileFragmentManager.md](Runtime/Modules/Persist/FileFragmentManager.md) / [SQLiteManager.md](Runtime/Modules/Persist/SQLiteManager.md) |
| 理解 UIGroup 深度 / 遮挡排序 | [UIGroupHelper.md](Runtime/Modules/UI/UIGroupHelper/UIGroupHelper.md) → [UIManager.md · UIGroup.Refresh 算法](Runtime/Modules/UI/UIManager/UIManager.md) |
| **新增游戏流程**（Procedure） | [ProcedureBase.md · 继承模板+GetNextProcedureType](Runtime/Modules/Procedure/ProcedureBase.md) → [ProcedureComponent.md · 自动发现+初始化时序](Runtime/Modules/Procedure/ProcedureComponent.md) → [ProcedureManager.md · FSM 驱动](Runtime/Modules/Procedure/ProcedureManager.md)（具体 Procedure 实现由 Game 层提供，Bootstrap 分发） |
| **HybridCLR 业务 DLL 加载**（DLL 加载流程） | [ProcedureLoadDll.md · AOT metadata + DLL 加载 + 延迟注册](Runtime/Modules/Procedure/Procedures/ProcedureLoadDll.md) → [HybridConfigs.md · Runtime 加载配置](Runtime/Modules/Config/Definitions/HybridConfigs.md) → [DllAssetEntry.md · Runtime 单字段条目](Runtime/Modules/Config/Definitions/DllAssetEntry.md) → [HybridEditorConfigs.md · Editor 构建配置](Editor/Config/Definitions/HybridEditorConfigs.md) → [DllMasterAssetEntry.md · Editor 三字段条目](Editor/Config/Definitions/DllMasterAssetEntry.md) → [Util.HybridCLR.md · LoadAotMetadataAsync/LoadGameAssemblyAsync](Runtime/Utils/Util.HybridCLR.md) |
| **刷新 HybridCLR 业务热更 DLL**（消费态本地 Operation） | [nova-project-refresh-hotfix-dlls](../Agents/Skills/nova-project-refresh-hotfix-dlls/SKILL.md) → [EditorUtil.HybridCLR.md · compile/copy/import 事实](Editor/EditorUtil/EditorUtil.HybridCLR/EditorUtil.HybridCLR.md) → [HybridEditorConfigs.md](Editor/Config/Definitions/HybridEditorConfigs.md) → [DllMasterAssetEntry.md](Editor/Config/Definitions/DllMasterAssetEntry.md)；只覆盖当前 Target、DevelopmentBuild 与激活 ConfigMaster 当前坐标，不代表 AOT、Bundle、Player、CDN 或运行时成功。 |
| **HybridCLR 编辑期原子操作**（由 Pipify 编排流水线） | [EditorUtil.HybridCLR.md · link.xml/Generate/DLL 拷贝 API](Editor/EditorUtil/EditorUtil.HybridCLR/EditorUtil.HybridCLR.md) |
| **一键流水线 Step / 批处理配置**（Pipify 自动化） | [EditorUtil.Pipify.md · Registry+Runner+Reporters](Editor/EditorUtil/EditorUtil.Pipify/EditorUtil.Pipify.md) → [PipifySteps.md · 全 Step 清单](Editor/EditorUtil/EditorUtil.Pipify/PipifySteps.md) → [PipifySteps.Export.Helpers.md · 定位辅助](Editor/EditorUtil/EditorUtil.Pipify/PipifySteps.Export.Helpers.md) |
| **Pipify 导出新模块 Step（Table/UI/Localization/Network/Sound/Vibrate）** | [PipifySteps.md · 导出分组表](Editor/EditorUtil/EditorUtil.Pipify/PipifySteps.md) → 对应 EditorUtil.\*.Exporter.md |
| **Nova Project Skill 的确定性 C# 单元操作** | [当前架构、实施路线图与原候选决策](Editor/EditorUtil/EditorUtil.AgentActions/NovaProjectActions-Overall-Design.md) → [EditorUtil.AgentActions.md · 当前 19 项实现事实](Editor/EditorUtil/EditorUtil.AgentActions/EditorUtil.AgentActions.md) |
| **新平台打包 / Build 封装** | [EditorUtil.Build.md · BuildPlayer 薄封装](Editor/EditorUtil/EditorUtil.Build/EditorUtil.Build.md) |
| **Pipify 流水线配置窗口**（UI 入口） | [PipifyWindow.md · Batch 管理+参数编辑+运行](Editor/Windows/PipifyWindow.md) |
| **Jenkins 自动化 / CLI 批处理** | [EditorUtil.Pipify.md · RunBatchForCliAsync+参数覆盖](Editor/EditorUtil/EditorUtil.Pipify/EditorUtil.Pipify.md) |
| **热更新业务脚本挂载**（HybridCLR 原生 MB + Prefab 直挂） | 业务 UIView / 业务逻辑类直接继承 `MonoBehaviour`，Prefab 预挂后由 HybridCLR dll 加载时 Unity 反序列化自动恢复。旧 NovaBehaviour/IBaseLife 桥接已废止（2026-05-21）。 |
| **多语言本地化**（显示文本/切换语言/字体适配） | [LocalizationComponent.md · 初始化时序+GetText](Runtime/Modules/Localization/LocalizationComponent.md) → [LocalizationManager.md · ResolveLanguage+状态机](Runtime/Modules/Localization/LocalizationManager.md) → [LocalizationSettings.md · 文本Map+字体List双组设置](Runtime/Modules/Localization/LocalizationSettings.md) → [TextLocalizing.md · TMP专用+字体刷新链](Runtime/Modules/Localization/TextLocalizing.md) |
| **理解 FSM 工具** | [FsmState.md · 状态基类](Runtime/Core/Fsm/FsmState.md) → [Fsm.md · FSM 实现+接口](Runtime/Core/Fsm/Fsm.md) |
| **HTTP 请求（AES 加密 / UniTask 异步）** | [NetworkComponent.md · GetAsync/PostAsync](Runtime/Modules/Network/NetworkComponent.md) → [HttpManager.md · Transport SPI + 业务主备候选链](Runtime/Modules/Network/HttpManager/HttpManager.md) → [IDownloadService.md · 下载接口](Runtime/Modules/Network/HttpManager/IDownloadService.md) → [HttpResponse.md · 响应与进度数据](Runtime/Modules/Network/HttpManager/Definitions/HttpResponse.md) |
| **Alibaba Cloud OSS Runtime 上传/下载** | [Alibaba Cloud OSS C# SDK v2 包文档](../../../UPMPackages/com.solotopia.alibabacloud.oss/Nova/Docs/INDEX.md) |
| **编辑器 CDN 部署与缓存清理** | [ConfigWindow.md · CDN 内容分发网络部署](Editor/Windows/ConfigWindow.md) → [EditorUtil.CDN.md · OSS 上传+Cloudflare 清理传输引擎](Editor/EditorUtil/EditorUtil.CDN/EditorUtil.CDN.md) → [ConfigMasterSO.md · Editor-only 配置](Editor/Config/ConfigMasterSO.md) → [CDNEditorConfigs.md · 12 字段定义](Editor/Config/Definitions/CDNEditorConfigs.md) → [Alibaba Cloud OSS C# SDK v2 包文档](../../../UPMPackages/com.solotopia.alibabacloud.oss/Nova/Docs/INDEX.md) |
| **WebSocket 长连接（认证/心跳/重连）** | [NetworkComponent.md · ConnectServer/SendMessage](Runtime/Modules/Network/NetworkComponent.md) → [WebSocketManager.md · 状态机+协程链](Runtime/Modules/Network/WebSocketManager/WebSocketManager.md) |
| **NetCmd URL 路由配置（Host+Path）** | [NetworkManager.md · Luban 加载+URL 路由算法](Runtime/Modules/Network/NetworkManager/NetworkManager.md) → [NetworkSettings.md · HostKeySettings/NetCmdSettings](Runtime/Modules/Network/Definitions/NetworkSettings.md) |
| **DNS-over-HTTPS IP 收集** | [DoHManager.md · CollectAllIPAddresses 算法](Runtime/Modules/Network/DoHManager/DoHManager.md) |
| **新增 SDK 插件**（UPM 拔插式） | [SDK/INDEX.md · 模块入口与目录导航](Runtime/Modules/SDK/INDEX.md) → [ARCHITECTURE.md · 当前结构与分层](Runtime/Modules/SDK/ARCHITECTURE.md) → [Definitions/SDKPluginBase.md · 纯C#抽象基类+模板方法](Runtime/Modules/SDK/Definitions/SDKPluginBase.md) → [SDKComponent.md · InitializeAsync+使用示例](Runtime/Modules/SDK/SDKComponent.md) → [Definitions/ISDKPlugin.md · 接口契约](Runtime/Modules/SDK/Definitions/ISDKPlugin.md) |
| **触觉振动反馈**（NiceVibrations） | [VibrateComponent.md · Play/StopAll/LoadAsync](Runtime/Modules/Vibrate/VibrateComponent.md) → [VibrateManager.md · 链式播放+CTS](Runtime/Modules/Vibrate/VibrateManager.md) |
| **播放声音 / 管理声音组**（Sound） | [SoundComponent.md · PlaySound+LoadAsync](Runtime/Modules/Sound/SoundComponent.md) → [SoundManager.md · 优先级抢占+按name查表算法](Runtime/Modules/Sound/SoundManager.md) → [PlaySoundParams.md · 参数池化](Runtime/Modules/Sound/PlaySoundParams.md) |
| 理解框架启动顺序 / Manager 优先级 | [ARCHITECTURE.md · 启动与销毁顺序](ARCHITECTURE.md) → [Nova.md](Runtime/Modules/Nova/Nova.md) |

---

## Runtime — 核心层 (Core)

| 文档 | 说明 |
|------|------|
| [Core/Core.md](Runtime/Core/Core.md) | 核心层概览 |
| [FrameworkComponent.md](Runtime/Modules/FrameworkComponent.md) | 所有 Component 基类 |
| [FrameworkManager.md](Runtime/Modules/FrameworkManager.md) | 所有 Manager 基类 |
| [Definitions.md](Runtime/Core/Definitions/Definitions.md) | 框架级枚举（渠道/平台/模式/语言类型） |
| [ChannelType.md](Runtime/Core/Definitions/ChannelType.md) | 游戏运营渠道类型枚举（None/Official/Google/Apple/WeChat/TikTok/Alipay） |
| [DevelopMode.md](Runtime/Core/Definitions/DevelopMode.md) | 开发/发布模式枚举（Debug / Publish），Config 第三维度 |
| [PlatformType.md](Runtime/Core/Definitions/PlatformType.md) | 运行平台类型枚举（None/Android/iOS/PC/WebGL/Mini） |
| [Language.md](Runtime/Core/Definitions/Language.md) | 游戏语言枚举与 LanguageMetadata（GetDesc/GetFlag 字典查询） |
| [LanguageSelectionWay.md](Runtime/Core/Definitions/LanguageSelectionWay.md) | 已移除，保留历史兼容说明页 |
| [Extensions.md](Runtime/Core/Extensions/Extensions.md) | C# 和 Unity 扩展方法 |
| [Interfaces.md](Runtime/Core/Interfaces/Interfaces.md) | 基础层公共接口 |
| [ICoroutineRunner.md](Runtime/Core/Interfaces/ICoroutineRunner.md) | 协程运行器接口 |
| [IReadOnlyOrderedDictionary.md](Runtime/Core/Interfaces/IReadOnlyOrderedDictionary.md) | 只读有序字典接口 |
| [Structures.md](Runtime/Core/Collections/Structures.md) | 自定义数据结构总览（链表/有序字典等） |
| [NovaLinkedList.md](Runtime/Core/Collections/NovaLinkedList.md) | 泛型链表（ICollection 实现） |
| [NovaLinkedListRange.md](Runtime/Core/Collections/NovaLinkedListRange.md) | 链表范围迭代器 |
| [NovaLinkedSet.md](Runtime/Core/Collections/NovaLinkedSet.md) | 有序去重链表集合 |
| [NovaMultiDictionary.md](Runtime/Core/Collections/NovaMultiDictionary.md) | 多值字典（Key→LinkedListRange） |
| [NovaOrderedDictionary.md](Runtime/Core/Collections/NovaOrderedDictionary.md) | 有序字典 |
| [TypeNamePair.md](Runtime/Core/Collections/TypeNamePair.md) | 类型+名称配对结构体 |
| [DataReceiver.md](Runtime/Core/Table/DataReceiver.md) | 异步 AB 数据加载基类 |
| [IDataReceiver.md](Runtime/Core/Table/IDataReceiver.md) | 数据接收器接口 |
| [LubanDataReceiver.md](Runtime/Core/Table/LubanDataReceiver.md) | Luban JSON 数据接收器（DataReceiver 子类） |
| [DataTableMode.md](Runtime/Core/Table/DataTableMode.md) | 数据表模式枚举（List/Map/One） |
| [IDataTableSettings.md](Runtime/Core/Table/IDataTableSettings.md) | 数据表设置统一接口 |
| [IDataTableUnitSetting.md](Runtime/Core/Table/IDataTableUnitSetting.md) | 数据表单元设置统一接口 |
| [DataTableUnitSettingBase.md](Runtime/Core/Table/DataTableUnitSettingBase.md) | 数据表单元设置抽象基类（提取各模块公共序列化字段与接口实现） |
| [ITable.md](Runtime/Core/Table/ITable.md) | 表格容器接口（Luban TbXxx 实现） |
| [ILubanTables.md](Runtime/Core/Table/ILubanTables.md) | Luban *Tables 容器契约接口（GetAllTables） |
| [ILubanTableBinding.md](Runtime/Core/Table/ILubanTableBinding.md) | Luban 生成表清单、Codec 解码与 Nova 原始字节加载适配接口 |
| [LubanTablesLoader.md](Runtime/Core/Table/LubanTablesLoader.md) | Luban Tables 反射加载器（Table/Config 共用） |
| [Log.md](Runtime/Core/Log/Log.md) | 静态日志门面 |
| [LogTag.md](Runtime/Core/Log/LogTag.md) | 日志标签静态常量 |
| [LogLevel.md](Runtime/Core/Log/LogLevel.md) | 日志级别枚举 |
| [ILogHelper.md](Runtime/Core/Log/Interfaces/ILogHelper.md) | 日志辅助器接口 |
| [LogHelper.md](Runtime/Core/Log/Implements/LogHelper.md) | 日志辅助器内部实现 |
| [ReferencePool.md](Runtime/Core/Reference/ReferencePool.md) | 引用池（零 GC 对象复用） |
| [IReference.md](Runtime/Core/Reference/IReference.md) | 引用池对象契约接口 |
| [ReferencePoolInfo.md](Runtime/Core/Reference/ReferencePoolInfo.md) | 引用池统计信息结构体 |
| [ReferenceStrictCheckType.md](Runtime/Core/Reference/ReferenceStrictCheckType.md) | 引用池严格检查类型枚举 |
| [IReferenceHelper.md](Runtime/Core/Reference/Interfaces/IReferenceHelper.md) | 引用池辅助器接口 |
| [ReferenceHelper.md](Runtime/Core/Reference/Implements/ReferenceHelper.md) | 引用池辅助器内部实现 |
| [Txt.md](Runtime/Core/Txt/Txt.md) | 文本格式化工具 |
| [ITxtHelper.md](Runtime/Core/Txt/Interfaces/ITxtHelper.md) | 文本辅助器接口 |
| [TxtHelper.md](Runtime/Core/Txt/Implements/TxtHelper.md) | 文本辅助器内部实现 |
| [FsmState.md](Runtime/Core/Fsm/FsmState.md) | 有限状态机状态基类 |
| [Fsm.md](Runtime/Core/Fsm/Fsm.md) | 有限状态机实现 + IFsm 接口（含 AddStates 动态追加） |
| [IFsm.md](Runtime/Core/Fsm/IFsm.md) | 有限状态机泛型接口 |
| [Path.md](Runtime/Core/Path/Path.md) | 框架级路径常量工具（Streaming/Persistent/Cache/Hotfix/Persist 分区） |
| [Path.Streaming.md](Runtime/Core/Path/Path.Streaming.md) | StreamingAssets 只读路径（多平台分支） |
| [Path.Persistent.md](Runtime/Core/Path/Path.Persistent.md) | persistentDataPath 可写路径 |
| [Path.Cache.md](Runtime/Core/Path/Path.Cache.md) | Unity Caching 缓存路径 |

| [Path.Hotfix.md](Runtime/Core/Path/Path.Hotfix.md) | 热更新路径常量 |
| [Path.Persist.md](Runtime/Core/Path/Path.Persist.md) | 持久化路径常量 |

## Runtime — 工具集 (Core/Util)

| 文档 | 说明 |
|------|------|
| [Utils.md](Runtime/Utils/Utils.md) | 工具集概览 |
| [Util.TypeCreator.md](Runtime/Utils/Util.TypeCreator.md) | **DI 核心**：按类型名反射创建实例 |
| [Util.Assembly.md](Runtime/Utils/Util.Assembly.md) | 纯反射工具：跨程序集查找 Type / GetAssembly（按名查单个程序集）/ GetAssemblies（全量）/ 子类名称收集；无 IO 操作 |
| [Util.Json.md](Runtime/Utils/Util.Json.md) | JSON 序列化 |
| [Util.Placeholder.md](Runtime/Utils/Util.Placeholder.md) | Editor/Runtime/导出链共用的显式上下文占位符解析器 |
| [Util.Convert.md](Runtime/Utils/Util.Convert.md) | 基础类型转换 |
| [Util.Encrypt.md](Runtime/Utils/Util.Encrypt.md) | AES / XOR 加解密 |
| [Util.SysIO.md](Runtime/Utils/Util.SysIO.md) | 文件/路径操作 |
| [Util.MD5.md](Runtime/Utils/Util.MD5.md) | MD5 哈希（字节数组/文件） |
| [Util.HybridCLR.md](Runtime/Utils/Util.HybridCLR.md) | HybridCLR 生态唯一 Facade：LoadAotMetadataAsync / LoadGameAssemblyAsync，底层走 AssetComponent 加载 TextAsset，双 HashSet 幂等守卫 |

## Runtime — 业务模块层 (Modules)

| 文档 | 说明 |
|------|------|
| [Modules.md](Runtime/Modules/Modules.md) | 业务模块层索引概览 |
| [Nova.md](Runtime/Modules/Nova/Nova.md) | 框架全局入口 |

### Asset（资源加载）

| 文档 | 说明 |
|------|------|
| [AssetComponent.md](Runtime/Modules/Asset/AssetComponent.md) | 资源加载 Component（全 Load/Preload/Release API 薄代理入口） |
| [AssetCallbacks.md](Runtime/Modules/Asset/AssetCallbacks.md) | 说明旧回调式资源契约已被当前 Handle 模式取代 |
| [IAssetManager.md](Runtime/Modules/Asset/AssetManager/Interfaces/IAssetManager.md) | Asset Manager 接口（全量契约，所有 LoadXxx 返回 Handle） |
| [IAssetHandle.md](Runtime/Modules/Asset/AssetManager/Interfaces/IAssetHandle.md) | 主资源句柄接口（非泛型基接口 + 泛型 IAssetHandle&lt;T&gt;） |
| [ISubAssetsHandle.md](Runtime/Modules/Asset/AssetManager/Interfaces/ISubAssetsHandle.md) | 子资源批量句柄接口（ISubAssetsHandle + ISubAssetsHandle&lt;T&gt;，整批同生共死） |
| [IAllAssetsHandle.md](Runtime/Modules/Asset/AssetManager/Interfaces/IAllAssetsHandle.md) | 全资源批量句柄接口（IAllAssetsHandle + IAllAssetsHandle&lt;T&gt;，整批同生共死） |
| [IRawFileHandle.md](Runtime/Modules/Asset/AssetManager/Interfaces/IRawFileHandle.md) | 原始文件句柄接口（FilePath / GetBytes / Release） |
| [ISceneHandle.md](Runtime/Modules/Asset/AssetManager/Interfaces/ISceneHandle.md) | 场景句柄接口（IsValid / IsDone / UnloadAsync） |
| [IAssetDownloader.md](Runtime/Modules/Asset/AssetManager/Interfaces/IAssetDownloader.md) | 资源下载器接口（TotalCount/Progress/RunAsync/Cancel） |
| [AssetManager.md](Runtime/Modules/Asset/AssetManager/Implements/AssetManager.md) | Asset Manager 三层实现链（AssetManagerBase + AssetManager，12 个 partial 文件） |
| [AssetManagerConfig.md](Runtime/Modules/Asset/AssetManager/Definitions/AssetManagerConfig.md) | Asset Manager 配置类（运行模式、热更总开关、启动白名单、主备 URL、超时与并发） |
| [AssetDownloader.md](Runtime/Modules/Asset/AssetManager/Definitions/AssetDownloader.md) | IAssetDownloader 实现（YooAsset ResourceDownloaderOperation 包装） |
| [AssetDownloadUrlPolicy.md](Runtime/Modules/Asset/AssetManager/Definitions/AssetDownloadUrlPolicy.md) | YooAsset 候选 URL 轮换策略（传输失败与内容校验失败统一推进且去重） |
| [AssetRemoteService.md](Runtime/Modules/Asset/AssetManager/Definitions/AssetRemoteService.md) | YooAsset 远端寻址服务（常规主备 + 白名单 metadata-only 路由 + 占位符替换） |
| [YooAssetHandleAdapter.md](Runtime/Modules/Asset/AssetManager/Definitions/YooAssetHandleAdapter.md) | IAssetHandle 到 YooAsset.AssetHandle 的 ReferencePool 适配器 |
| [YooAssetSubAssetsHandleAdapter.md](Runtime/Modules/Asset/AssetManager/Definitions/YooAssetSubAssetsHandleAdapter.md) | ISubAssetsHandle 到 YooAsset.SubAssetsHandle 的 ReferencePool 适配器 |
| [YooAssetAllAssetsHandleAdapter.md](Runtime/Modules/Asset/AssetManager/Definitions/YooAssetAllAssetsHandleAdapter.md) | IAllAssetsHandle 到 YooAsset.AllAssetsHandle 的 ReferencePool 适配器 |
| [YooAssetRawFileHandleAdapter.md](Runtime/Modules/Asset/AssetManager/Definitions/YooAssetRawFileHandleAdapter.md) | IRawFileHandle 到 YooAsset AssetHandle + RawFileObject 的 ReferencePool 适配器（FilePath 为尽力提供的 bundle 路径） |
| [YooAssetSceneHandleAdapter.md](Runtime/Modules/Asset/AssetManager/Definitions/YooAssetSceneHandleAdapter.md) | ISceneHandle 到 YooAsset.SceneHandle 的 ReferencePool 适配器 |
| [AssetPlayMode.md](Runtime/Modules/Asset/Definitions/AssetPlayMode.md) | 资源运行模式枚举（EditorSimulate/Offline/Host/Web） |
| [AssetDecryptorType.md](Runtime/Modules/Asset/Definitions/AssetDecryptorType.md) | AB 解密器类型枚举 |
| [OffsetBundleDecryptor.md](Runtime/Modules/Asset/Definitions/Decryptors/OffsetBundleDecryptor.md) | 偏移解密器骨架实现（Wave 5 待补） |

### App（大版本检查）

| 文档 | 说明 |
|------|------|
| [App.md](Runtime/Modules/App/App.md) | App 模块概览（L1 导航） |
| [AppComponent.md](Runtime/Modules/App/AppComponent.md) | App Component（CheckAsync/DownloadAsync/OpenStoreAsync + 规则状态读取） |
| [IAppManager.md](Runtime/Modules/App/AppManager/IAppManager.md) | App Manager 接口 |
| [AppManagerBase.md](Runtime/Modules/App/AppManager/AppManagerBase.md) | App Manager 抽象基类（Priority=11） |
| [AppManager.md](Runtime/Modules/App/AppManager/AppManager.md) | App Manager 实现（HTTP 版本检查 + 规则匹配 + 下载/商店跳转，4 个 partial 文件） |
| [AppManagerConfig.md](Runtime/Modules/App/Definitions/AppManagerConfig.md) | App Manager 配置类（超时/路由/规则） |
| [AppVersionResult.md](Runtime/Modules/App/Definitions/AppVersionResult.md) | 版本检查结果枚举（NoDownload/RecommendedDownload/ForcedDownload） |
| [AppVersionResponse.md](Runtime/Modules/App/Definitions/AppVersionResponse.md) | 服务端响应 DTO（internal，RecommendedDownloadVersion/ForcedDownloadVersion） |
| [AppDownloadRoute.md](Runtime/Modules/App/Definitions/AppDownloadRoute.md) | APP 下载路由枚举（Store/Apk） |
| [AppDownloadRule.md](Runtime/Modules/App/Definitions/AppDownloadRule.md) | APP 下载弹窗规则枚举（None/Recommended/Forced） |

### Prefab（Prefab 实例化）

| 文档 | 说明 |
|------|------|
| [PrefabComponent.md](Runtime/Modules/Prefab/PrefabComponent.md) | Prefab 实例化 Component（InstantiateSync/Async + Destroy） |
| [IPrefabManager.md](Runtime/Modules/Prefab/PrefabManager/IPrefabManager.md) | Prefab Manager 接口 |
| [PrefabManagerBase.md](Runtime/Modules/Prefab/PrefabManager/PrefabManagerBase.md) | Prefab Manager 抽象基类（Priority=10） |
| [PrefabManager.md](Runtime/Modules/Prefab/PrefabManager/PrefabManager.md) | Prefab Manager 实现（IAssetHandle 持有 + PrefabInstanceTag 单路释放） |
| [PrefabManagerConfig.md](Runtime/Modules/Prefab/PrefabManager/PrefabManagerConfig.md) | Prefab Manager 配置类（当前为扩展占位体） |
| [PrefabInstanceTag.md](Runtime/Modules/Prefab/Definitions/PrefabInstanceTag.md) | Prefab 实例钩子组件（OnDestroy 单路释放 IAssetHandle） |
| [PrefabRecordedInstance.md](Runtime/Modules/Prefab/Definitions/PrefabRecordedInstance.md) | Prefab 实例诊断记录结构体（Instance + Location，供 Inspector 只读展示） |

### Config（配置）

| 文档 | 说明 |
|------|------|
| [ConfigComponent.md](Runtime/Modules/Config/ConfigComponent.md) | Runtime 配置门面（本地/缓存/远端应用配置字符串与基础类型读取、手动刷新） |
| [ConfigManager.md](Runtime/Modules/Config/ConfigManager.md) | 加载 ConfigRuntimeSO 并直接转发 Runtime 分组配置 |
| [ConfigManagerConfig.md](Runtime/Modules/Config/Definitions/ConfigManagerConfig.md) | 配置 Manager 初始化入参（AssetLocation） |
| [ConfigManagerBase.md](Runtime/Modules/Config/Implements/ConfigManagerBase.md) | 配置 Manager 抽象基类（Priority=10） |
| [IConfigManager.md](Runtime/Modules/Config/Interfaces/IConfigManager.md) | Runtime 分组配置接口；应用配置刷新能力由 Nova.Config 门面与内部 IAppConfigManager 承接 |
| [ConfigMasterSO.md](Editor/Config/ConfigMasterSO.md) | Editor 设计态主配置；持有三维矩阵、Editor 工具配置与导出目标 |
| [CDNEditorConfigs.md](Editor/Config/Definitions/CDNEditorConfigs.md) | CDN 部署与清缓存 Editor 配置，不导出 Runtime |
| [HybridEditorConfigs.md](Editor/Config/Definitions/HybridEditorConfigs.md) | HybridCLR 构建、link.xml 与 DLL 路径 Editor 配置 |
| [YooAssetEditorConfigs.md](Editor/Config/Definitions/YooAssetEditorConfigs.md) | YooAsset 工程资产路径 Editor 配置 |
| [PanelDimensionMask.md](Editor/Config/Definitions/PanelDimensionMask.md) | Editor 配置面板维度掩码 |
| [TypedDimensionMask.md](Editor/Config/Definitions/TypedDimensionMask.md) | Editor SDK / Kit 类型维度掩码 |
| [NamespaceOverride.md](Editor/Config/Definitions/NamespaceOverride.md) | Namespace Editor 维度 Override |
| [HybridEditorConfigsOverride.md](Editor/Config/Definitions/HybridEditorConfigsOverride.md) | HybridCLR Editor 维度 Override |
| [YooAssetEditorConfigsOverride.md](Editor/Config/Definitions/YooAssetEditorConfigsOverride.md) | YooAsset Editor 维度 Override |
| [ConfigRuntimeSO.md](Runtime/Modules/Config/ConfigRuntimeSO.md) | Runtime 快照：Platform / Channel / DevelopMode / AppConfigs / PrivacyConfigs / Namespace / HybridConfigs / Custom |
| [Definitions/IKitConfig.md](Runtime/Modules/Config/Definitions/IKitConfig.md) | Kit 固有配置 marker 接口（DisplayName）；实例按 Platform×Channel×DevelopMode 存于 PlatformChannelEntry，EnabledKits 为白名单，导出为当前单格 ConfigRuntimeSO |
| [Definitions/KitConfigMissingException.md](Runtime/Modules/Config/Definitions/KitConfigMissingException.md) | Kit 配置缺失异常；fail-fast 暴露配置漏填 |
| [AppConfigs.md](Runtime/Modules/Config/AppConfigs.md) | Runtime 应用配置（应用标识、AES、启动拉取 NetCmd 与配置项名称） |
| [PrivacyConfigs.md](Runtime/Modules/Config/PrivacyConfigs.md) | Runtime 隐私配置（Util.Encrypt.AES 默认 Key/IV，与 AppConfigs 独立） |
| [HybridConfigs.md](Runtime/Modules/Config/Definitions/HybridConfigs.md) | Runtime HybridCLR 配置（入口名与 DLL Asset 地址） |
| [CustomConfigs.md](Runtime/Modules/Config/Definitions/CustomConfigs.md) | Custom 本地 JSONPath 默认值与云端完整 JSON 查询入口 |
| [PlatformChannelEntry.md](Editor/Config/Definitions/PlatformChannelEntry.md) | Editor 三维配置矩阵行 |
| [Definitions/DllAssetEntry.md](Runtime/Modules/Config/Definitions/DllAssetEntry.md) | DLL 运行期寻址条目（AssetLocation 单字段），供 ConfigRuntimeSO.HybridConfigs.AotMetadataDlls / StartupGameDlls 持有 |
| [DllMasterAssetEntry.md](Editor/Config/Definitions/DllMasterAssetEntry.md) | Editor DLL 构建三字段条目 |

### Table（表格数据）

| 文档 | 说明 |
|------|------|
| [TableComponent.md](Runtime/Modules/Table/TableComponent.md) | 表格 Component（GetTable/HasTable 统一查询入口） |
| [TableManager.md](Runtime/Modules/Table/TableManager.md) | 表格 Manager（多 Binding → 原始单表加载 → 生成 Tables → ResolveRef） |
| [TableManagerConfig.md](Runtime/Modules/Table/Definitions/TableManagerConfig.md) | 表格 Manager 配置类 |
| [TableSettings.md](Runtime/Modules/Table/Definitions/TableSettings.md) | 多个 Luban Project、导出描述与运行时加载描述 |
| [TableManagerBase.md](Runtime/Modules/Table/Implements/TableManagerBase.md) | 表格 Manager 抽象基类（Priority=14） |
| [ITableManager.md](Runtime/Modules/Table/Interfaces/ITableManager.md) | 表格 Manager 接口（GetTable / HasTable / LoadSync / LoadAsync） |

### Event（事件）

| 文档 | 说明 |
|------|------|
| [EventComponent.md](Runtime/Modules/Event/EventComponent.md) | 事件 Component |
| [EventManager.md](Runtime/Modules/Event/EventManager.md) | 事件 Manager（含 EventPool） |
| [EventData.md](Runtime/Modules/Event/Definitions/EventData.md) | 事件数据抽象基类（EventArgs + IReference） |
| [EventTypeID.md](Runtime/Modules/Event/Definitions/EventTypeID.md) | 事件类型 ID 静态注册表（自增 ID，替代 Type.GetHashCode） |
| [EventManagerConfig.md](Runtime/Modules/Event/Definitions/EventManagerConfig.md) | 事件 Manager 配置类 |
| [EventManagerBase.md](Runtime/Modules/Event/Implements/EventManagerBase.md) | 事件 Manager 抽象基类 |
| [EventPool.md](Runtime/Modules/Event/Implements/EventPools/EventPool.md) | 事件池泛型实现 |
| [EventPoolMode.md](Runtime/Modules/Event/Implements/EventPools/EventPoolMode.md) | 事件池模式标志枚举 |
| [IEventManager.md](Runtime/Modules/Event/Interfaces/IEventManager.md) | 事件 Manager 接口 |

### UI（UI 系统）

| 文档 | 说明 |
|------|------|
| [UIComponent.md](Runtime/Modules/UI/UIComponent.md) | UI Component |
| [UIManager.md](Runtime/Modules/UI/UIManager/UIManager.md) | UI 主管理器 |
| [UIManagerBase.md](Runtime/Modules/UI/UIManager/Implements/UIManagerBase.md) | UI Manager 抽象基类 |
| [IUIManager.md](Runtime/Modules/UI/UIManager/Interfaces/IUIManager.md) | UI Manager 接口 |
| [UIManagerConfig.md](Runtime/Modules/UI/UIManager/Definitions/UIManagerConfig.md) | UI Manager 配置类 |
| [UISettings.md](Runtime/Modules/UI/UIManager/Definitions/UISettings.md) | UI 序列化设置 |
| [IUIViewRow.md](Runtime/Modules/UI/UIManager/Definitions/IUIViewRow.md) | UI 视图数据行接口（替代已删除的 UIViewEntry） |
| [IUIView.md](Runtime/Modules/UI/Definitions/IUIView.md) | UIView 接口 |
| [UIView.md](Runtime/Modules/UI/Definitions/UIView.md) | UIView 抽象基类（MonoBehaviour） |
| [UIGroupHelper.md](Runtime/Modules/UI/UIGroupHelper/UIGroupHelper.md) | Canvas 分层/深度排序 |
| [UIGroupHelperBase.md](Runtime/Modules/UI/UIGroupHelper/Implements/UIGroupHelperBase.md) | UIGroup 辅助器抽象基类 |
| [IUIGroupHelper.md](Runtime/Modules/UI/UIGroupHelper/Interfaces/IUIGroupHelper.md) | UIGroup 辅助器接口 |
| [IUIGroup.md](Runtime/Modules/UI/UIGroupHelper/Definitions/IUIGroup.md) | UIGroup 接口 |

### ObjectPool（对象池）

| 文档 | 说明 |
|------|------|
| [ObjectPoolComponent.md](Runtime/Modules/ObjectPool/ObjectPoolComponent.md) | 对象池 Component |
| [ObjectPoolManager.md](Runtime/Modules/ObjectPool/ObjectPoolManager.md) | 对象池 Manager |
| [ObjectPoolManagerBase.md](Runtime/Modules/ObjectPool/ObjectPoolManagerBase.md) | 对象池 Manager 抽象基类 |
| [IObjectPoolManager.md](Runtime/Modules/ObjectPool/IObjectPoolManager.md) | 对象池 Manager 接口 |
| [ObjectPoolManagerConfig.md](Runtime/Modules/ObjectPool/ObjectPoolManagerConfig.md) | 对象池 Manager 配置类 |
| [ObjectPoolConfig.md](Runtime/Modules/ObjectPool/ObjectPoolConfig.md) | 对象池创建配置类（传给 Create 方法） |
| [ObjectPool.md](Runtime/Modules/ObjectPool/ObjectPool.md) | 泛型对象池实现 |
| [ObjectPoolBase.md](Runtime/Modules/ObjectPool/ObjectPoolBase.md) | 对象池抽象基类 |
| [IObjectPool.md](Runtime/Modules/ObjectPool/IObjectPool.md) | 泛型对象池接口 |
| [Object.md](Runtime/Modules/ObjectPool/Object.md) | 池内对象泛型封装 |
| [ObjectBase.md](Runtime/Modules/ObjectPool/ObjectBase.md) | 池内对象抽象基类 |
| [ObjectInfo.md](Runtime/Modules/ObjectPool/ObjectInfo.md) | 对象池信息结构体 |
| [ReleaseObjectsFilter.md](Runtime/Modules/ObjectPool/ReleaseObjectsFilter.md) | 释放对象筛选委托 |


### Network（网络）

> Kit 包文档（`NetService` / `NetBuilder` / `Login` 等业务层封装）不在主框架 Docs 内，见下方 Kit 包链接。

| 文档 | 说明 |
|------|------|
| [NetworkComponent.md](Runtime/Modules/Network/NetworkComponent.md) | 网络 Component（DoH/Http/Network/WebSocket 四管理器入口） |
| [NetworkSettings.md](Runtime/Modules/Network/Definitions/NetworkSettings.md) | 网络设置：HostKeySettings（HostKeyUnits 单套列表）/ NetCmdSettings（NetCmdUnits 单套列表），实现 IDataTableSettings |
| [ProtoSettings.md](Runtime/Modules/Network/Definitions/ProtoSettings.md) | Protobuf 编辑器设置：ProtoSourceDirPath + ProtoUnits 列表（SourcePath / CSharpExportPath），仅 Editor 工具链使用 |
| [DoHSettings.md](Runtime/Modules/Network/Definitions/DoHSettings.md) | DoH 管理器配置 |
| [HttpSettings.md](Runtime/Modules/Network/Definitions/HttpSettings.md) | HTTP 管理器配置 |
| [BestHTTP 网络埋点文档](../../../UPMPackages/com.solotopia.nova.framework.besthttp/Nova/Docs/INDEX.md) | BestHTTP 三类网络事件、字段、叶子错误码、Nova 自动注册与 Network 面板开关 |
| [WebSocketSettings.md](Runtime/Modules/Network/Definitions/WebSocketSettings.md) | WebSocket 管理器配置（7 项参数） |
| [NetworkManager.md](Runtime/Modules/Network/NetworkManager/NetworkManager.md) | NetCmd URL 路由（两阶段 Luban 加载：HostKey + NetCmd）/ 网络状态检测 / 服务器时间 Manager |
| [NetworkManagerBase.md](Runtime/Modules/Network/NetworkManager/NetworkManagerBase.md) | Network Manager 抽象基类（Priority = 10） |
| [INetworkManager.md](Runtime/Modules/Network/NetworkManager/INetworkManager.md) | Network Manager 接口（GetTable<T> 新增） |
| [NetworkManagerConfig.md](Runtime/Modules/Network/NetworkManager/Definitions/NetworkManagerConfig.md) | Network Manager 配置类（HostKeyUnitSettings / NetCmdUnitSettings） |
| [INetworkHostKeyRow.md](Runtime/Modules/Network/NetworkManager/Definitions/INetworkHostKeyRow.md) | 域名数据行接口（Luban bean 实现契约） |
| [INetworkCmdRow.md](Runtime/Modules/Network/NetworkManager/Definitions/INetworkCmdRow.md) | 网络指令数据行接口（Luban bean 实现契约） |
| [HttpManager.md](Runtime/Modules/Network/HttpManager/HttpManager.md) | HTTP 短连接 / Transport SPI / DoH 候选链 / 文件上传 / 二进制下载 Manager |
| [HttpManagerBase.md](Runtime/Modules/Network/HttpManager/HttpManagerBase.md) | HTTP Manager 抽象基类（8 个 abstract 声明，Priority=10） |
| [IHttpManager.md](Runtime/Modules/Network/HttpManager/IHttpManager.md) | HTTP Manager 接口（继承 IDownloadService） |
| [IDownloadService.md](Runtime/Modules/Network/HttpManager/IDownloadService.md) | 下载服务接口（DownloadBinaryAsync / DownloadTextAsync） |
| [HttpManagerConfig.md](Runtime/Modules/Network/HttpManager/Definitions/HttpManagerConfig.md) | HTTP Manager 配置类 |
| [HttpResponse.md](Runtime/Modules/Network/HttpManager/Definitions/HttpResponse.md) | HTTP 响应数据（IReference 池化，StatusCode / Body / RawData / Headers / Error / IsSuccess / DownloadedBytes / TotalBytes / DownloadProgress） |
| [DoHManager.md](Runtime/Modules/Network/DoHManager/DoHManager.md) | DNS-over-HTTPS 查询 / 域名 IP 收集 Manager |
| [DoHManagerBase.md](Runtime/Modules/Network/DoHManager/DoHManagerBase.md) | DoH Manager 抽象基类 |
| [IDoHManager.md](Runtime/Modules/Network/DoHManager/IDoHManager.md) | DoH Manager 接口 |
| [DoHManagerConfig.md](Runtime/Modules/Network/DoHManager/Definitions/DoHManagerConfig.md) | DoH Manager 配置类 |
| [DNSAddress.md](Runtime/Modules/Network/DoHManager/DoH/DNSAddress.md) | DNS 地址静态常量 |
| [DNSAnswer.md](Runtime/Modules/Network/DoHManager/DoH/DNSAnswer.md) | DNS 应答数据 |
| [DNSCacheEntry.md](Runtime/Modules/Network/DoHManager/DoH/DNSCacheEntry.md) | DNS 缓存条目 |
| [DoHClient.md](Runtime/Modules/Network/DoHManager/DoH/DoHClient.md) | DoH 客户端（IDisposable） |
| [DoHData.md](Runtime/Modules/Network/DoHManager/DoH/DoHData.md) | 已移除，保留历史兼容说明页 |
| [ResourceRecordType.md](Runtime/Modules/Network/DoHManager/DoH/ResourceRecordType.md) | DNS 资源记录类型枚举 |
| [WebSocketManager.md](Runtime/Modules/Network/WebSocketManager/WebSocketManager.md) | WebSocket 长连接 / 认证心跳重连 / 跨线程消息分发 Manager |
| [WebSocketManagerBase.md](Runtime/Modules/Network/WebSocketManager/WebSocketManagerBase.md) | WebSocket Manager 抽象基类 |
| [IWebSocketManager.md](Runtime/Modules/Network/WebSocketManager/IWebSocketManager.md) | WebSocket Manager 接口 |
| [WebSocketManagerConfig.md](Runtime/Modules/Network/WebSocketManager/Definitions/WebSocketManagerConfig.md) | WebSocket Manager 配置类 |
| [WebSocketScope.md](Runtime/Modules/Network/WebSocketManager/WebSocket/WebSocketScope.md) | WebSocket 作用域容器类 |
| [WebSocketState.md](Runtime/Modules/Network/WebSocketManager/WebSocket/WebSocketState.md) | WebSocket 连接状态枚举 |
| [WebGL.md](Runtime/Modules/Network/WebSocketManager/WebSocket/WebGL.md) | WebGL 平台 WebSocket 适配 |

### Network — Kit 编排层（已下沉至框架主程序集，位于 `Modules/Network/Kit/`）

| 文档 | 说明 |
|------|------|
| [NetService.md](Runtime/Modules/Network/NetService.md) | 网络请求静态编排器（Protobuf → 使用 `AppConfigs.AppAesKey/AppAesIV` 的 AES-128-CBC → HTTP → AES 解密；`SendAsync` 带 `[EditorBrowsable(Never)]`，仅供业务 Service 调用） |
| [NetBuilder.md](Runtime/Modules/Network/NetBuilder.md) | 请求构建静态工具（Header 构建、Proto 序列化、AES 加密、Header JSON；整类 `[EditorBrowsable(Never)]`） |
| [NetResponse.md](Runtime/Modules/Network/NetResponse.md) | 业务层网络响应泛型包装（`IsSuccess` / `ErrorCode` / `Data`；静态工厂 `Success` / `Fail`） |
| [NetErrorCode.md](Runtime/Modules/Network/NetErrorCode.md) | 网络层错误码常量（客户端段负数 + 服务端通用段正数） |

### Network — Kit 公共层

> 本索引只覆盖主框架内的 Network 公共层。登录、云存档等业务 Kit 可以单向依赖这里的公共能力，但各自文档仍由对应 UPM 包独立维护，不再在主框架索引中挂接。
| [NetChannelBase.md](Runtime/Modules/Network/WebSocketManager/WebSocket/Channels/NetChannelBase.md) | 网络通道抽象基类 |
| [NetChannelType.md](Runtime/Modules/Network/WebSocketManager/WebSocket/Channels/NetChannelType.md) | 网络通道类型标志枚举 |
| [TcpChannel.md](Runtime/Modules/Network/WebSocketManager/WebSocket/Channels/TcpChannel.md) | TCP 通道实现 |
| [TcpPbChannel.md](Runtime/Modules/Network/WebSocketManager/WebSocket/Channels/TcpPbChannel.md) | TCP+Protobuf 通道实现 |
| [MessageType.md](Runtime/Modules/Network/WebSocketManager/WebSocket/Messages/MessageType.md) | 消息类型标志枚举 |
| [NetMessageBase.md](Runtime/Modules/Network/WebSocketManager/WebSocket/Messages/NetMessageBase.md) | 网络消息抽象基类 |
| [NetMessageTcpBase.md](Runtime/Modules/Network/WebSocketManager/WebSocket/Messages/NetMessageTcpBase.md) | TCP 消息基类 |
| [TcpMessage.md](Runtime/Modules/Network/WebSocketManager/WebSocket/Messages/TcpMessage.md) | TCP 消息实现 |
| [TcpPbMessage.md](Runtime/Modules/Network/WebSocketManager/WebSocket/Messages/TcpPbMessage.md) | TCP+Protobuf 消息实现 |

### Procedure（流程管理）

| 文档 | 说明 |
|------|------|
| [ProcedureComponent.md](Runtime/Modules/Procedure/ProcedureComponent.md) | 流程管理 Component（自动发现所有非抽象 ProcedureBase 子类，Bootstrap 分发入口） |
| [ProcedureManager.md](Runtime/Modules/Procedure/ProcedureManager.md) | 流程管理 Manager（FSM 驱动） |
| [ProcedureManagerBase.md](Runtime/Modules/Procedure/ProcedureManagerBase.md) | 流程 Manager 抽象基类 |
| [IProcedureManager.md](Runtime/Modules/Procedure/IProcedureManager.md) | 流程 Manager 接口 |
| [ProcedureManagerConfig.md](Runtime/Modules/Procedure/ProcedureManagerConfig.md) | 流程 Manager 配置类 |
| [ProcedureBase.md](Runtime/Modules/Procedure/ProcedureBase.md) | 流程基类（FsmState 特化，新增 GetNextProcedureType/ChangeToNext 辅助方法） |
| [ProcedureDataKeys.md](Runtime/Modules/Procedure/ProcedureDataKeys.md) | 流程间数据传递键常量（public） |
| [LauncherUI.md](Runtime/Modules/Procedure/LauncherUI.md) | 启动阶段 UI 概览（含本地化机制说明） |
| [LauncherUIController.md](Runtime/Modules/Procedure/LauncherUIController.md) | 启动 UI 控制器（public static，面板生命周期统一入口） |
| [LauncherSettings.md](Runtime/Modules/Procedure/LauncherSettings.md) | 启动阶段序列化设置（含 LocalizationJsonPathTemplate） |
| [LauncherLocalization.md](Runtime/Modules/Procedure/LauncherLocalization.md) | 启动期本地化解析器（Resources 通道，与 LocalizationManager 解耦） |
| [LauncherLocalizedText.md](Runtime/Modules/Procedure/LauncherLocalizedText.md) | 普通文本本地化绑定条目（TMP_Text + Key） |
| [LauncherDialogLocalizedText.md](Runtime/Modules/Procedure/LauncherDialogLocalizedText.md) | 弹窗文本本地化绑定条目（TMP_Text + Key + LauncherDialogType） |
| [LauncherStage.md](Runtime/Modules/Procedure/LauncherStage.md) | 启动阶段枚举 |
| [LauncherDialogPanel.md](Runtime/Modules/Procedure/LauncherDialogPanel.md) | 启动通用弹窗面板（多语言文本数组驱动） |
| [LauncherDialogType.md](Runtime/Modules/Procedure/LauncherDialogType.md) | 启动对话框类型枚举 |
| [LauncherProgressPanel.md](Runtime/Modules/Procedure/LauncherProgressPanel.md) | 启动进度面板（整数百分比 + 多语言文本数组） |
| [LauncherSplashPanel.md](Runtime/Modules/Procedure/LauncherSplashPanel.md) | 启动闪屏面板 |
| [Procedures/ProcedureLoadDll.md](Runtime/Modules/Procedure/Procedures/ProcedureLoadDll.md) | HybridCLR 业务 DLL 加载流程（BootstrapAsync → ConfigRuntimeSO → AOT metadata → DLL → 扫描注册业务 Procedure → 跳转业务入口；不主动回收 Launcher UI） |
| [Procedures/ProcedureAppDownload.md](Runtime/Modules/Procedure/Procedures/ProcedureAppDownload.md) | 大版本下载提示流程（Forced/RecommendedDownload 弹窗 → 跳商店/下载 APK → 循环等待用户操作） |
| [ProcedureSplash.md](Runtime/Modules/Procedure/ProcedureSplash.md) | 启动链入口流程（合并原 ProcedureLaunch：初始化 LauncherUIController + 最短保底时长 + Splash 跨流程存活） |
| [ProcedureRunInfo.md](Runtime/Modules/Procedure/ProcedureRunInfo.md) | Procedure 执行记录数据容器（TypeFullName / EnterRealtime / LeaveRealtime / Finished / Elapsed；由 ProcedureComponent.Update 采集；**仅 `#if UNITY_EDITOR` 编译，发布构建不存在**） |

### Debug（调试）

| 文档 | 说明 |
|------|------|
| [DebugComponent.md](Runtime/Modules/Debug/DebugComponent.md) | Debug Component |
| [DebugManager.md](Runtime/Modules/Debug/DebugManager.md) | Debug Manager（当前负责磁盘检测循环与事件发布） |
| [DebugManagerBase.md](Runtime/Modules/Debug/Managers/DebugManagerBase.md) | Debug Manager 抽象基类（Priority=0，声明 Initialize / Update / Shutdown） |
| [IDebugManager.md](Runtime/Modules/Debug/Managers/IDebugManager.md) | Debug Manager 接口（当前只暴露 Initialize / Shutdown） |
| [DebugManagerConfig.md](Runtime/Modules/Debug/Managers/DebugManagerConfig.md) | Debug Manager 配置类（当前持有 DiskCheckingConfigs） |
| [DiskCheckEventData.md](Runtime/Modules/Debug/DiskCheckEventData.md) | 磁盘检测事件数据 |
| [DiskCheckingConfig.md](Runtime/Modules/Debug/Windows/DiskCheckingConfig.md) | 磁盘检测配置（嵌套于 DebugComponent） |
| [DebuggerActiveType.md](Runtime/Modules/Debug/Definitions/DebuggerActiveType.md) | 调试器启用策略枚举（AlwaysEnable / Development / Editor / Disable） |
| [RuntimeDebugger.md](Runtime/Modules/Debug/Debugger/RuntimeDebugger.md) | Debug 模块内置调试器门面（含 Console rich text 预览规则） |
| [DebugOptions.md](Runtime/Modules/Debug/Debugger/DebugOptions.md) | 运行时调试选项容器 |
| [DebuggerAssets.md](Runtime/Modules/Debug/Debugger/DebuggerAssets.md) | 调试器资源目录与资源迁移同步规则 |

### Persist（持久化）

| 文档 | 说明 |
|------|------|
| [PersistComponent.md](Runtime/Modules/Persist/PersistComponent.md) | 持久化 Component（三独立 Manager，`Awake` 只创建，`LoadAsync()` 显式并行初始化） |
| [PersistManagerBase.md](Runtime/Modules/Persist/PersistManagerBase.md) | 持久化 Manager 泛型基类（Priority=0，InitializeBase，TickAutoSave，Validate*，virtual 扩展类型） |
| [PersistManagerConfigBase.md](Runtime/Modules/Persist/PersistManagerConfigBase.md) | 持久化 Manager 配置公共基类（UseAESEncrypt + AutoSaveInterval） |
| [PlayerPrefsManager.md](Runtime/Modules/Persist/PlayerPrefsManager.md) | PlayerPrefs Manager（全平台，async Initialize，脏标记延迟落盘，ValidateClassifyName） |
| [IPlayerPrefsManager.md](Runtime/Modules/Persist/IPlayerPrefsManager.md) | PlayerPrefs Manager 独立接口（28 个方法） |
| [PlayerPrefsManagerConfig.md](Runtime/Modules/Persist/PlayerPrefsManagerConfig.md) | PlayerPrefs Manager 配置类（继承 PersistManagerConfigBase） |
| [FileFragmentManager.md](Runtime/Modules/Persist/FileFragmentManager.md) | 文件片段 Manager（全平台，Binary 格式，async Load 重入保护，懒加载 + 脏追踪） |
| [IFileFragmentManager.md](Runtime/Modules/Persist/IFileFragmentManager.md) | 文件片段 Manager 独立接口（28 个方法） |
| [FileFragmentManagerConfig.md](Runtime/Modules/Persist/FileFragmentManagerConfig.md) | 文件片段 Manager 配置类（继承 PersistManagerConfigBase） |
| [FileFragmentItemGroup.md](Runtime/Modules/Persist/FileFragmentItemGroup.md) | 文件片段数据容器（AES 解密 null 检查 + count<0 + try-catch 防御） |
| [SQLiteManager.md](Runtime/Modules/Persist/SQLiteManager.md) | SQLite Manager（写缓冲 + 事务批量 + WAL，ValidateSQLiteClassify；WebGL 下静默禁用） |
| [ISQLiteManager.md](Runtime/Modules/Persist/ISQLiteManager.md) | SQLite Manager 独立接口（28 个方法 + GetAllClassifyNames） |
| [SQLiteManagerConfig.md](Runtime/Modules/Persist/SQLiteManagerConfig.md) | SQLite Manager 配置类（继承 PersistManagerConfigBase，额外含 CipherPassword） |
| [SQLiteManager.Table.md](Runtime/Modules/Persist/SQLiteManager.Table.md) | SQLite 表操作嵌套类 |

### Localization（本地化）

| 文档 | 说明 |
|------|------|
| [LocalizationComponent.md](Runtime/Modules/Localization/LocalizationComponent.md) | 本地化 Component（持有 Manager + 暴露全部 API） |
| [LocalizationManager.md](Runtime/Modules/Localization/LocalizationManager.md) | 本地化 Manager（语言切换状态机 / ResolveLanguage 回退算法） |
| [LocalizationManagerConfig.md](Runtime/Modules/Localization/LocalizationManagerConfig.md) | 本地化 Manager 配置类 |
| [LocalizationSettings.md](Runtime/Modules/Localization/LocalizationSettings.md) | 本地化设置（文本 Map + 字体 List 双组 UnitSettings，IDataTableUnitSetting 实现） |
| [LocalizationRefreshEventData.md](Runtime/Modules/Localization/LocalizationRefreshEventData.md) | 语言切换刷新事件数据 |
| [LocalizationFontData.md](Runtime/Modules/Localization/LocalizationFontData.md) | 单条字体配置数据 |
| [ILocalizationManager.md](Runtime/Modules/Localization/ILocalizationManager.md) | 本地化 Manager 接口（语言查询/切换/文本/字体数据全部契约） |
| [ILocalizationTextRow.md](Runtime/Modules/Localization/ILocalizationTextRow.md) | 本地化文本数据行接口（原 ILocalizationRow 重命名） |
| [ILocalizationFontRow.md](Runtime/Modules/Localization/ILocalizationFontRow.md) | 本地化字体数据行接口（Luban bean 实现契约） |
| [TextLocalizing.md](Runtime/Modules/Localization/TextLocalizing.md) | UI 文本自动本地化组件（TextMeshProUGUI 专用，事件驱动刷新） |

### SDK（SDK 插件）

> 完整模块文档树见 [Runtime/Modules/SDK/INDEX.md](Runtime/Modules/SDK/INDEX.md)
> 本索引只覆盖主框架内的 SDK 公共层。各 SDK / Kit 子包可以单向依赖这些公共契约，但文档仍由各自 UPM 包独立维护，不在主框架索引中逐包建立反向链接。
>
> Agent 处理具体 SDK / Kit 任务时，应从任务关键字识别当前工程中的候选包，再查找包内 `Nova/Doc/INDEX.md`。开发仓优先搜索 `UPMPackages/<package>/Nova/{Doc,Docs,DOCS}/INDEX.md`；消费工程依次搜索 `Packages/<package>/...` 与 `Library/PackageCache/<package>@<version>/...`。查阅已安装包文档是允许进入 `Library/PackageCache` 的定向场景。先读包内 INDEX，再按 INDEX 进入具体文档；不要因主框架索引未列出包名就判定“没有文档”。

| 文档 | 说明 |
|------|------|
| [Runtime/Modules/SDK/ARCHITECTURE.md](Runtime/Modules/SDK/ARCHITECTURE.md) | SDK 模块架构总览 / ADR / 目录骨架 |
| [SDKComponent.md](Runtime/Modules/SDK/SDKComponent.md) | SDK Component（GetComponentsInChildren 收集插件 + 异步初始化入口） |
| [Definitions/ISDKPlugin.md](Runtime/Modules/SDK/Definitions/ISDKPlugin.md) | SDK 插件基接口（Name / Priority / IsAvailable / InitializeAsync / DisposeAsync） |
| [Definitions/SDKPluginBase.md](Runtime/Modules/SDK/Definitions/SDKPluginBase.md) | SDK 插件通用抽象基类（纯 C#，非 MonoBehaviour，模板方法 + IsAvailable 管理） |
| [Definitions/PluginBase.md](Runtime/Modules/SDK/Definitions/PluginBase.md) | SDK 插件泛型基类（IPluginBaseMarker + PluginBase<TConfig>，自动 config 注入分支） |
| [Managers/Interfaces/ISDKManager.md](Runtime/Modules/SDK/Managers/Interfaces/ISDKManager.md) | SDK Manager 接口（Initialize / Get / TryGet / GetAll / Broadcast*） |
| [Managers/Implements/SDKManager.md](Runtime/Modules/SDK/Managers/Implements/SDKManager.md) | SDK Manager 唯一实现（Priority 分桶初始化、失败隔离、初始化后缓存稳定 DeviceID） |
| [Managers/Implements/SDKManagerBase.md](Runtime/Modules/SDK/Managers/Implements/SDKManagerBase.md) | SDK Manager 抽象基类（Priority=16） |
| [Managers/Definitions/SDKManagerConfig.md](Runtime/Modules/SDK/Managers/Definitions/SDKManagerConfig.md) | SDK Manager 配置类（承载 PluginEntries 数组） |
| [Plugins/Device/IDeviceIdProvider.md](Runtime/Modules/SDK/Plugins/Device/IDeviceIdProvider.md) | 设备唯一标识提供者接口（GetDeviceID）；Kit-Network NetBuilder.BuildHeader 自动读取 |

### Vibrate（振动反馈）

| 文档 | 说明 |
|------|------|
| [VibrateComponent.md](Runtime/Modules/Vibrate/VibrateComponent.md) | 振动 Component |
| [IVibrateManager.md](Runtime/Modules/Vibrate/IVibrateManager.md) | 振动 Manager 接口 |
| [VibrateManagerBase.md](Runtime/Modules/Vibrate/VibrateManagerBase.md) | 振动 Manager 抽象基类 |
| [VibrateManager.md](Runtime/Modules/Vibrate/VibrateManager.md) | 振动 Manager（三阶段 Luban 加载 + 全局单 CTS 链式播放） |
| [IVibrateRow.md](Runtime/Modules/Vibrate/IVibrateRow.md) | 振动数据行接口族（IVibrateRow / IVibrateEmphasisRow / IVibrateCustomRow） |
| [VibrateManagerConfig.md](Runtime/Modules/Vibrate/VibrateManagerConfig.md) | 振动 Manager 配置类 |
| [VibrateSettings.md](Runtime/Modules/Vibrate/VibrateSettings.md) | 振动序列化设置（EmphasisUnitSetting + CustomUnitSetting，实现 IDataTableSettings） |
| [VibrateUnitSetting.md](Runtime/Modules/Vibrate/VibrateUnitSetting.md) | 振动单元设置（Editor 导出路径 + Runtime AssetLocation/模式） |
| [VibrateType.md](Runtime/Modules/Vibrate/VibrateType.md) | 振动类型枚举 |

### Sound（声音）

| 文档 | 说明 |
|------|------|
| [SoundComponent.md](Runtime/Modules/Sound/SoundComponent.md) | 声音 Component（对外入口，Priority=19） |
| [ISoundManager.md](Runtime/Modules/Sound/ISoundManager.md) | 声音 Manager 接口 |
| [ISoundRow.md](Runtime/Modules/Sound/ISoundRow.md) | 声音数据行接口（Luban bean 实现契约） |
| [SoundManagerBase.md](Runtime/Modules/Sound/SoundManagerBase.md) | 声音 Manager 抽象基类（Priority=19） |
| [SoundManager.md](Runtime/Modules/Sound/SoundManager.md) | 声音 Manager（含嵌套类：SoundGroup、SoundAgent、SoundGroupHelper、SoundAgentHelper、PlaySoundInfo、PlaySoundErrorCode） |
| [SoundManagerConfig.md](Runtime/Modules/Sound/SoundManagerConfig.md) | 声音 Manager 配置类 |
| [SoundSettings.md](Runtime/Modules/Sound/SoundSettings.md) | 声音序列化设置（实现 IDataTableSettings，持有 SoundUnitsSettings 列表） |
| [SoundUnitSetting.md](Runtime/Modules/Sound/SoundUnitSetting.md) | 单个声音数据源的设置（Editor 导出路径 + Runtime AssetLocation/模式） |
| [SoundGroupShell.md](Runtime/Modules/Sound/SoundGroupShell.md) | 声音组外壳（Inspector 序列化配置） |
| [PlaySoundParams.md](Runtime/Modules/Sound/PlaySoundParams.md) | 播放声音参数（ReferencePool 池化） |
| [PlaySoundInfo.md](Runtime/Modules/Sound/PlaySoundInfo.md) | 播放声音信息（SoundManager 嵌套私有类，ReferencePool 池化） |
| [PlaySoundErrorCode.md](Runtime/Modules/Sound/PlaySoundErrorCode.md) | 播放声音错误码枚举（SoundManager 嵌套私有枚举） |
| [SoundConstant.md](Runtime/Modules/Sound/SoundConstant.md) | 声音常量定义（内部静态类） |

## Runtime — 临时 (Tmp)

| 文档 | 说明 |
|------|------|

## Editor — EditorUtil（工具集）

| 文档 | 说明 |
|------|------|
| [EditorUtil.md](Editor/EditorUtil/EditorUtil.md) | EditorUtil 静态工具类概览（partial class 入口） |
| [EditorUtil.Draw.md](Editor/EditorUtil/EditorUtil.Draw/EditorUtil.Draw.md) | Inspector GUI 绘制工具集（全方法签名） |
| [EditorUtil.FileSystem.md](Editor/EditorUtil/EditorUtil.FileSystem/EditorUtil.FileSystem.md) | 文件系统封装（路径转换、目录操作、AssetDatabase 刷新、DeletePath） |
| [EditorUtil.ProcessRunner.md](Editor/EditorUtil/EditorUtil.ProcessRunner/EditorUtil.ProcessRunner.md) | 统一外部进程调用器（RunSync 阻塞等待 / RunAsync 非阻塞流式） |
| [EditorUtil.FileWatcher.md](Editor/EditorUtil/EditorUtil.FileWatcher/EditorUtil.FileWatcher.md) | 文件变动监控器（FileSystemWatcher 全局单例，主线程回调） |
| [EditorUtil.Reflect.Tooltip.md](Editor/EditorUtil/EditorUtil.Reflect/EditorUtil.Reflect.Tooltip.md) | 字段 TooltipAttribute 反射读取工具（2 个 GetFieldTooltip 重载 + 缓存） |
| [EditorUtil.Serializer.md](Editor/EditorUtil/EditorUtil.Serializer/EditorUtil.Serializer.md) | 反射读取私有字段（RuntimeDrawer 用） |
| [EditorUtil.ScriptingDefineSymbols.md](Editor/EditorUtil/EditorUtil.ScriptingDefineSymbols/EditorUtil.ScriptingDefineSymbols.md) | 脚本宏定义增删查工具 |
| [EditorUtil.TrackRegistry.md](Editor/EditorUtil/EditorUtil.TrackRegistry/EditorUtil.TrackRegistry.md) | 追踪注册表 Xlsx 导出（唯一公开方法 Generate(projectRoot)） |
| [EditorUtil.TypeCache.md](Editor/EditorUtil/EditorUtil.TypeCache/EditorUtil.TypeCache.md) | 编辑器类型缓存（反射收集实现类名称） |
| [EditorUtil.CsvExporter.md](Editor/EditorUtil/EditorUtil.CsvExporter/EditorUtil.CsvExporter.md) | CSV 导出工具 |
| [EditorUtil.Asmdef.md](Editor/EditorUtil/EditorUtil.Asmdef/EditorUtil.Asmdef.md) | Assembly Definition 命名空间解析（向上查找 .asmdef） |
| [EditorUtil.Excel.md](Editor/EditorUtil/EditorUtil.Excel/EditorUtil.Excel.md) | Excel 读写工具（EPPlus 写入 + ExcelDataReader 读取，Config 预过滤使用） |
| [EditorUtil.Environment.md](Editor/EditorUtil/EditorUtil.Environment/EditorUtil.Environment.md) | 编辑器运行时环境数据工具（EnvironmentData / ColumnIndices / GetEnvironmentData，Channel 来自 RuntimeProvider） |
| [EditorUtil.Environment.Python3.md](Editor/EditorUtil/EditorUtil.Environment/EditorUtil.Environment.Python3.md) | Python3 运行环境多路径探测检查器（5 策略：ExplicitPath/PATH/PyLauncher/Where/PythonFallback；SessionState 缓存；33 候选路径） |
| [EditorUtil.Environment.LubanChecker.md](Editor/EditorUtil/EditorUtil.Environment/EditorUtil.Environment.LubanChecker.md) | Luban 运行环境检查器（dotnet-sdk 路径/版本≥8.0/Luban.dll 检测，SessionState 缓存；由 Pipeline 和 ConfigWindow 调用） |
| [EditorUtil.Luban.Pipeline.md](Editor/EditorUtil/EditorUtil.Luban/EditorUtil.Luban.Pipeline.md) | 非 Table 专用模块的 Unit/SchemaManifest Luban 导出流水线 |
| [EditorUtil.Luban.CliRunner.md](Editor/EditorUtil/EditorUtil.Luban/EditorUtil.Luban.CliRunner.md) | Luban CLI 外部进程调用器（代码生成/数据导出/protobuf3 schema 生成） |
| [EditorUtil.Proto.CliRunner.md](Editor/EditorUtil/EditorUtil.Proto/EditorUtil.Proto.CliRunner.md) | protoc CLI 外部进程调用器（Mac + Win 跨平台，Luban→protoc 闭环管线） |
| [EditorUtil.Luban.ConfigSyncer.md](Editor/EditorUtil/EditorUtil.Luban/EditorUtil.Luban.ConfigSyncer.md) | Luban `_configs/` 同步器（manifest + luban.conf + __tables__.xml） |
| [EditorUtil.Luban.SchemaManifest.md](Editor/EditorUtil/EditorUtil.Luban/EditorUtil.Luban.SchemaManifest.md) | 导出前扫描 Excel 生成的 Editor-only、可重建结构快照 |
| [EditorUtil.Luban.GeneratedOutput.md](Editor/EditorUtil/EditorUtil.Luban/EditorUtil.Luban.GeneratedOutput.md) | 非 Table Luban C# 第一行所有权标记、正文 Hash 校验与安全过期删除 |
| [EditorUtil.Luban.JsonMerger.md](Editor/EditorUtil/EditorUtil.Luban/EditorUtil.Luban.JsonMerger.md) | Luban per-table JSON → per-Excel Nova 格式合并器 |
| [EditorUtil.Luban.MapPropGen.md](Editor/EditorUtil/EditorUtil.Luban/EditorUtil.Luban.MapPropGen.md) | Map 模式属性生成器（TbXxx partial class 追加） |
| [EditorUtil.Luban.ExportHelper.md](Editor/EditorUtil/EditorUtil.Luban/EditorUtil.Luban.ExportHelper.md) | Luban 导出辅助工具：构建导出上下文、生成关联文件名、查找单元设置 |
| [EditorUtil.Luban.DataTypeNameHelper.md](Editor/EditorUtil/EditorUtil.Luban/EditorUtil.Luban.DataTypeNameHelper.md) | internal Excel Sheet 纯扫描器（不访问 SerializedProperty） |
| [EditorUtil.Draw.SourceFileTree.md](Editor/EditorUtil/EditorUtil.Draw/EditorUtil.Draw.SourceFileTree.md) | 数据源文件树绘制与命名空间列表编辑静态工具集 |
| [EditorUtil.CheckUpdate.md](Editor/EditorUtil/EditorUtil.CheckUpdate/EditorUtil.CheckUpdate.md) | UPM 包版本检查工具（启动自动检查 + `MarkSkip`/`ClearSkip` 持久化，复用 PlugPals 网络层） |
| [EditorUtil.HybridCLR.md](Editor/EditorUtil/EditorUtil.HybridCLR/EditorUtil.HybridCLR.md) | HybridCLR 原子操作合集（link.xml 校验/补全、Generate 系列封装、AOT/业务 DLL 拷贝；由 Pipify Steps 编排流水线） |
| [EditorUtil.AndroidResolver.md](Editor/EditorUtil/EditorUtil.AndroidResolver/EditorUtil.AndroidResolver.md) | Android 依赖解析工具（反射调用 EDM4U PlayServicesResolver.ResolveSync，强制重建 Assets/GeneratedLocalRepo/**；配合 HybridCLR Generate All 前置使用） |
| [EditorUtil.Pipify.md](Editor/EditorUtil/EditorUtil.Pipify/EditorUtil.Pipify.md) | [PipifyStep] 反射注册 Step + Batch 可视化配置 + UI/CLI 双入口的自动化流水线 |
| [EditorUtil.AgentActions.md](Editor/EditorUtil/EditorUtil.AgentActions/EditorUtil.AgentActions.md) | Nova Project Skills 的受控 C# Action Registry、Plan/Execute/Verify/Recovery 调度层；当前注册 19 项，MCP 安全开放 13 项 |
| [EditorUtil.Build.md](Editor/EditorUtil/EditorUtil.Build/EditorUtil.Build.md) | BuildPipeline.BuildPlayer 薄封装，统一异常与日志 |
| [EditorUtil.CDN.md](Editor/EditorUtil/EditorUtil.CDN/EditorUtil.CDN.md) | CDN 内容部署与缓存清理工具（阿里云 OSS 批量上传 + Cloudflare purge 分批清理；编排/传输适配器分层；无 public API，仅 internal，程序集外经 ConfigWindow「CDN 内容分发网络部署」面板触发） |
| [EditorUtil.Asset.Operator.md](Editor/EditorUtil/EditorUtil.Asset/EditorUtil.Asset.Operator.md) | 通用 ScriptableObject 资产查找/创建/按路径加载（泛型 Find&lt;T&gt; / CreateAt&lt;T&gt; / LoadAt&lt;T&gt;） |
| [EditorUtil.Asset.Cache.md](Editor/EditorUtil/EditorUtil.Asset/EditorUtil.Asset.Cache.md) | 动态解析并清理 YooAsset Editor 沙盒与框架 version 记录 |
| [EditorUtil.FileSystem.OutputApplier.md](Editor/EditorUtil/EditorUtil.FileSystem/EditorUtil.FileSystem.OutputApplier.md) | Editor 内部批量文件替换、删除、备份与失败回滚基础设施 |
| [EditorUtil.Config.StructureGuard.md](Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.StructureGuard.md) | Platform×Channel 枚举网格补齐与缺失引用清理 |
| [EditorUtil.Config.SDKPluginScanner.md](Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.SDKPluginScanner.md) | 全程序集扫描 ISDKPluginConfig 实现类型 + 实例补全/移除（EnsureInstance/RemoveInstance 按 DevelopMode 分组） |
| [EditorUtil.Config.Validator.md](Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.Validator.md) | AppConfigs/PluginConfig 必填字段校验（Severity 枚举 + ValidationIssue 结构体；支持三维 Platform×Channel×DevelopMode） |
| [EditorUtil.Config.Exporter.md](Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.Exporter.md) | 将 ConfigMasterSO 三维组合导出为 ConfigRuntimeSO.asset |
| [EditorUtil.Config.SchemaMigration.md](Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.SchemaMigration.md) | 版本化迁移旧 ConfigMasterSO，并重导出关联 Runtime 快照 |
| [EditorUtil.Config.RuntimeProvider.md](Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.RuntimeProvider.md) | 从 AssetDatabase 按三维（Platform×Channel×DevelopMode）读取 ConfigRuntimeSO（不缓存，替代已删除的 ConfigLookup）；GetChannel() 新增 |
| [EditorUtil.Config.WorkspaceActive.md](Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.WorkspaceActive.md) | 工程级激活 ConfigMaster 锚点；通过 Globals.json 持久化 GUID，四段回退策略，根除多 Sample 共存命中歧义 |
| [EditorUtil.Config.YooAssetInjector.md](Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.YooAssetInjector.md) | Asset 模块编辑期注入层；按 ConfigMaster 路径字段注入 YooAssetSettings / 加载 BundleCollectorSetting，替代全工程扫描 |
| [EditorUtil.Config.DimensionProjector.md](Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.DimensionProjector.md) | 维度投影器；按 PanelDimensionMask 在 WorkingCopy 上执行加维分裂/减维合并/广播，支持矩阵三类和顶层三类面板 |
| [EditorUtil.Config.DimensionalResolver.md](Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.DimensionalResolver.md) | 顶层类维度取数器（只读）；按坐标 + 掩码从 Override 列表解析 Namespace / HybridCLR / YooAsset 最终值 |
| [EditorUtil.Table.Exporter.md](Editor/EditorUtil/EditorUtil.Table/EditorUtil.Table.Exporter.md) | Table 正式 Luban Project 导出（五种格式、原始单表、Catalog、事务发布） |
| [EditorUtil.UI.Exporter.md](Editor/EditorUtil/EditorUtil.UI/EditorUtil.UI.Exporter.md) | UI 模块 Luban 导出入口（ExportAll/ExportCode/ExportData + 单文件 ExportCodeForFile/ExportDataForFile） |
| [EditorUtil.Localization.TextExporter.md](Editor/EditorUtil/EditorUtil.Localization/EditorUtil.Localization.TextExporter.md) | 本地化文本导出工具（ExportTextAll/ExportTextCode/ExportTextData/ExportSupportedLanguages，三阶段 PreFilter + Pipeline） |
| [EditorUtil.Localization.FontExporter.md](Editor/EditorUtil/EditorUtil.Localization/EditorUtil.Localization.FontExporter.md) | 本地化字体导出工具（ExportFontAll/ExportFontCode/ExportFontData，标准 Luban Pipeline） |
| [EditorUtil.Network.HostKeyExporter.md](Editor/EditorUtil/EditorUtil.Network/EditorUtil.Network.HostKeyExporter.md) | HostKeys 公共导出门面（ConfigRuntime DevelopMode、配对 Sheet 与暂存发布） |
| [EditorUtil.Network.NetCmdExporter.md](Editor/EditorUtil/EditorUtil.Network/EditorUtil.Network.NetCmdExporter.md) | NetCmds 公共导出门面（保持表结构并暂存发布） |
| [EditorUtil.Network.ProtoExporter.md](Editor/EditorUtil/EditorUtil.Network/EditorUtil.Network.ProtoExporter.md) | Proto 协议批量编译（ExportAllProtos：按每个 Unit.CSharpExportPath 分别调 Proto.CliRunner.CompileSingle） |
| [EditorUtil.Sound.Exporter.md](Editor/EditorUtil/EditorUtil.Sound/EditorUtil.Sound.Exporter.md) | Sound 专用导出编排（API 不变，Luban 暂存、验证并通过 OutputApplier 发布） |
| [EditorUtil.Vibrate.Exporter.md](Editor/EditorUtil/EditorUtil.Vibrate/EditorUtil.Vibrate.Exporter.md) | Vibrate 双轨导出编排（Emphasis/Custom 独立暂存、验证与事务发布） |

## Editor — Definitions（公共类型定义）

| 文档 | 说明 |
|------|------|
| [IEditorRuntimeDrawer.md](Editor/Definitions/IEditorRuntimeDrawer.md) | RuntimeDrawer 接口 |
| [FileFolderTree.md](Editor/Definitions/FileFolderTree.md) | 目录树结构 |

## Editor — Windows（编辑器窗口）

| 文档 | 说明 |
|------|------|
| [ConfigWindow.md](Editor/Windows/ConfigWindow.md) | Nova 全局配置窗口（三段式布局：顶栏 SO 选择+Platform/Channel/DevelopMode+导出 ObjectField、左树 LubanEnv/Python3Env/AppConfig/NamespaceConfig/HybridCLRConfig/SDK、右面板详情；支持三维导出 ConfigRuntimeSO；HybridCLR DLL 列表通过 HybridCLRConfig 面板编辑；导出目标 SO 通过 EditorPrefs GUID 持久化；partial 拆 10 文件） |
| [CheckUpdateWindow.md](Editor/Windows/CheckUpdateWindow.md) | Nova 包版本更新提示窗口（表格展示 Package/Current/Latest，支持"跳过当前版本"持久化） |
| [PipifyWindow.md](Editor/Windows/PipifyWindow.md) | Pipify 流水线配置与执行窗口（Nova/Open Pipify） |
| [EnvironmentWindow.md](Editor/Windows/EnvironmentWindow.md) | 空壳说明页：`EnvironmentWindow` 类不存在、`Windows/EnvironmentWindow/` 目录为空；「环境检测」UI 实际内嵌在 ConfigWindow 左树「环境检测」组（Luban / Python3 / HybridCLR 三条目），检测引擎在 `EditorUtil.Environment` |

## Editor — Tools（工具 + AB 处理器）

| 文档 | 说明 |
|------|------|
| [EditorUtil.BundleBuilder.md](Editor/EditorUtil/EditorUtil.BundleBuilder/EditorUtil.BundleBuilder.md) | YooAsset 标准 AssetBundle 与可选 RawFile 构建封装 |
| [PlugPalsWindow.md](Editor/Windows/PlugPalsWindow.md) | 私有 Verdaccio 仓库 UPM 包管理窗口（安装/升级/卸载/搜索/UPM 联动） |

## Editor — Inspector

| 文档 | 说明 |
|------|------|
| [Inspectors.md](Editor/Inspectors/Inspectors.md) | Inspector 模块索引概览 |
| [BaseComponentInspector.md](Editor/Inspectors/BaseComponentInspector.md) | 所有 Inspector 抽象基类 |
| [AssetComponentInspector.md](Editor/Inspectors/AssetComponentInspector/AssetComponentInspector.md) | 资源加载 Inspector |
| [ConfigComponentInspector.md](Editor/Inspectors/ConfigComponentInspector/ConfigComponentInspector.md) | 配置 Inspector（Luban 驱动导出 UI：目录树 + per-unit 导出按钮 + 运行时配置展示） |
| [TableComponentInspector.md](Editor/Inspectors/TableComponentInspector/TableComponentInspector.md) | 表格 Inspector |
| [EventComponentInspector.md](Editor/Inspectors/EventComponentInspector/EventComponentInspector.md) | 事件 Inspector |
| [NovaComponentInspector.md](Editor/Inspectors/NovaComponentInspector/NovaComponentInspector.md) | Nova 全局参数 Inspector |
| [ObjectPoolComponentInspector.md](Editor/Inspectors/ObjectPoolComponentInspector/ObjectPoolComponentInspector.md) | 对象池 Inspector |
| [UIComponentInspector.md](Editor/Inspectors/UIComponentInspector/UIComponentInspector.md) | UI Inspector |
| [NetworkComponentInspector.md](Editor/Inspectors/NetworkComponentInspector/NetworkComponentInspector.md) | 网络 Inspector |
| [ProcedureComponentInspector.md](Editor/Inspectors/ProcedureComponentInspector/ProcedureComponentInspector.md) | 流程管理 Inspector |
| [DebugComponentInspector.md](Editor/Inspectors/DebugComponentInspector/DebugComponentInspector.md) | Debug Inspector |
| [PersistComponentInspector.md](Editor/Inspectors/PersistComponentInspector/PersistComponentInspector.md) | 持久化 Inspector |
| [SDKComponentInspector.md](Editor/Inspectors/SDKComponentInspector/SDKComponentInspector.md) | SDK Inspector（Manager 选择器 + Plugin 条目分组列表） |
| [PluginEntriesDrawer.md](Editor/Inspectors/SDKComponentInspector/PluginEntriesDrawer.md) | Plugin 条目绘制器（反射扫描 + 分组渲染 + Missing 清理） |
| [LocalizationComponentInspector.md](Editor/Inspectors/LocalizationComponentInspector/LocalizationComponentInspector.md) | 本地化 Inspector（文本多语言投影 + 字体暂存事务导出） |
| [VibrateComponentInspector.md](Editor/Inspectors/VibrateComponentInspector/VibrateComponentInspector.md) | 振动 Inspector |
| [SoundComponentInspector.md](Editor/Inspectors/SoundComponentInspector/SoundComponentInspector.md) | 声音 Inspector |
| [AppComponentInspector.md](Editor/Inspectors/AppComponentInspector/AppComponentInspector.md) | App Inspector（Manager 选择器 + 3 组 Foldout 配置） |
| [PrefabComponentInspector.md](Editor/Inspectors/PrefabComponentInspector/PrefabComponentInspector.md) | Prefab 实例化 Inspector（Manager 选择器 + 单路径回收说明 + 运行时实例列表） |

### CustomInspectors（非 FrameworkComponent 的自定义 Inspector 与编辑器工具）

| 文档 | 说明 |
|------|------|
| [TextLocalizingInspector.md](Editor/Inspectors/CustomInspectors/TextLocalizingInspector.md) | TextLocalizing 组件 Inspector（键名+字体标记 Popup+译文预览） |
| [TextLocalizingAutoMount.md](Editor/Inspectors/CustomInspectors/TextLocalizingAutoMount.md) | TMP 添加时自动挂载 TextLocalizing 的编辑器钩子 |
| [TextLocalizingValidator.md](Editor/Inspectors/CustomInspectors/TextLocalizingValidator.md) | 全工程预制体扫描并补挂缺失 TextLocalizing 的 Inspector 按钮工具 |

## Editor — DataPipeline（模块专用预处理与导出编排）

| 文档 | 说明 |
|------|------|
| [DataPipeline.md](Editor/DataPipeline/DataPipeline.md) | 非 Table 模块专用导出总览（Localization、UI、Network、Sound、Vibrate） |
| [LocalizationExcelPreFilter.md](Editor/DataPipeline/Implements/Localizations/LocalizationExcelPreFilter.md) | Localization 模块：Excel 预过滤器（多语言列拆分为按语言 Name+Value 临时 Excel） |
| [LocalizationTextExporter.md](Editor/DataPipeline/Implements/Localizations/LocalizationTextExporter.md) | Localization 模块：数据、代码、Map 与语言列表的内部导出编排 |
| [UIExporter.md](Editor/DataPipeline/Implements/UIs/UIExporter.md) | UI 模块：Excel 契约校验、Luban 暂存导出与发布编排 |
| [NetworkExcelPreFilter.md](Editor/DataPipeline/Implements/Networks/NetworkExcelPreFilter.md) | Network Excel 输入投影（HostKeys Debug/Release 配对与选择，NetCmds 原样） |
| [NetworkExporter.md](Editor/DataPipeline/Implements/Networks/NetworkExporter.md) | Network 模块：预处理、Luban 暂存生成、验证与事务发布编排 |

## Editor — Menus / Tools

| 文档 | 说明 |
|------|------|
| [Menus.md](Editor/Menus/Menus.md) | Nova 顶级菜单项 |
| [FolderMenuItems.md](Editor/Menus/FolderMenuItems.md) | Open IDE Project / Open Folder |
| [AssetCacheMenuItems.md](Editor/Menus/AssetCacheMenuItems.md) | 清空本地热更资源缓存菜单 |
| [EnableLogsMenuItems.md](Editor/Menus/EnableLogsMenuItems.md) | Enable Logs 菜单 |
