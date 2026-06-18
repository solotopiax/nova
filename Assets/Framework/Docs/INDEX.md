# Framework 文档索引

> 架构总览、设计规范、陷阱说明见 [ARCHITECTURE.md](ARCHITECTURE.md)

---

## 按任务类型快速定位

> 找到你的任务，直接跳转必读文档，不必逐行阅读索引。

| 任务 | 必读文档（按顺序） |
|------|-------------------|
| 理解框架整体架构与设计模式 | [ARCHITECTURE.md](ARCHITECTURE.md) |
| **新增业务 UI 界面**（UIView 子类） | [UIComponent.md · 泛型 Open API + 注册表](Runtime/Modules/UI/UIComponent.md) → [UIView.md · UIView 继承模板](Runtime/Modules/UI/Definitions/UIView.md) → [UIManager.md · OpenUIView 流程](Runtime/Modules/UI/UIManager/UIManager.md) |
| **新增全局事件** | [EventManager.md · EventData模板 + 订阅/发布](Runtime/Modules/Event/EventManager.md) |
| **新增 Runtime 模块**（Component + Manager） | [Runtime.md · 9 步骤](Runtime/Runtime.md) → [FrameworkComponent.md](Runtime/Modules/FrameworkComponent.md) → [FrameworkManager.md · 三层继承规范](Runtime/Modules/FrameworkManager.md) |
| **新增 Inspector 面板** | [Editor.md · 开发规范](Editor/Editor.md) → [BaseComponentInspector.md · 子类模板](Editor/Inspectors/BaseComponentInspector.md) |
| **Inspector 运行时数据面板** | [IEditorRuntimeDrawer.md](Editor/Definitions/IEditorRuntimeDrawer.md) → [EditorUtil.Serializer.md · 读取私有字段](Editor/EditorUtil/EditorUtil.Serializer/EditorUtil.Serializer.md) |
| **复用纯 C# 对象（零 GC）** | [ReferencePool.md · IReference + Get/Put](Runtime/Core/Reference/ReferencePool.md) |
| **复用 GameObject / 资源对象** | [ObjectPoolManager.md · ObjectBase + CreatePool](Runtime/Modules/ObjectPool/ObjectPoolManager.md) |
| **加载 AB 包 / Prefab 实例化** | [AssetComponent.md](Runtime/Modules/Asset/AssetComponent.md)（所有 LoadXxx 返回 Handle，调用方负责 Release）|
| **实例化 Prefab / 销毁 Prefab 实例** | [PrefabComponent.md](Runtime/Modules/Prefab/PrefabComponent.md) → [IPrefabManager.md · Instantiate/Destroy API](Runtime/Modules/Prefab/PrefabManager/IPrefabManager.md) |
| **大版本检查 / APP 强更** | [AppComponent.md · CheckAsync+DownloadAsync+OpenStoreAsync](Runtime/Modules/App/AppComponent.md) → [AppManagerConfig.md · 超时+下载路由+规则](Runtime/Modules/App/Definitions/AppManagerConfig.md) |
| **加载运行时配置（AB 加载 ConfigRuntimeSO）** | [ConfigComponent.md](Runtime/Modules/Config/ConfigComponent.md) → [ConfigManager.md · AB加载+解析+PluginConfig索引](Runtime/Modules/Config/ConfigManager.md) |
| **加载 Excel/CSV 表格数据（Luban 方案）** | [TableManager.md · GetTable/HasTable 统一查询](Runtime/Modules/Table/TableManager.md) → [TableComponentInspector.md · Luban 导出流程](Editor/Inspectors/TableComponentInspector/TableComponentInspector.md) → [EditorUtil.Luban.Pipeline.md · 导出流水线](Editor/EditorUtil/EditorUtil.Luban/EditorUtil.Luban.Pipeline.md) |
| **编辑 Config SO / 导出 ConfigRuntime**（ConfigWindow 流程） | [ConfigWindow.md · 三段式布局+三维导出](Editor/Windows/ConfigWindow.md) → [ConfigMasterSO.md · 设计态数据+DevelopMode](Runtime/Modules/Config/ConfigMasterSO.md) → [ConfigRuntimeSO.md · 运行态导出物](Runtime/Modules/Config/ConfigRuntimeSO.md) → [EditorUtil.Config.Exporter.md](Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.Exporter.md) → [EditorUtil.Config.WorkspaceActive.md · 激活 Master 锚点](Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.WorkspaceActive.md) → [EditorUtil.Config.YooAssetInjector.md · YooAsset 注入](Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.YooAssetInjector.md) |
| **Config 面板按平台/渠道/模式分别配置**（per-panel 可勾选维度） | [PanelDimensionMask.md · 掩码三轴+IsGlobal](Runtime/Modules/Config/Definitions/PanelDimensionMask.md) → [EditorUtil.Config.DimensionProjector.md · 三操作+双路径](Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.DimensionProjector.md) → [EditorUtil.Config.DimensionalResolver.md · 只读取数+回落逻辑](Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.DimensionalResolver.md) |
| **新增 SDK PluginConfig**（ISDKPluginConfig + 自动注入） | [PluginBase.md · PluginBase<TConfig>泛型基类+自动注入](Runtime/Modules/SDK/Definitions/PluginBase.md) → [ISDKPluginConfig.md · 接口契约](Runtime/Modules/SDK/Definitions/ISDKPluginConfig.md) → [EditorUtil.Config.SDKPluginScanner.md · 扫描工具](Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.SDKPluginScanner.md) → [PlatformChannelEntry.md · 矩阵行结构（按DevelopMode分组）](Runtime/Modules/Config/PlatformChannelEntry.md) |
| **新增 Kit 配置（IKitConfig + ConfigWindow 配置）** | [IKitConfig.md · marker 接口](Runtime/Modules/Config/Definitions/IKitConfig.md) → [KitConfigMissingException.md · 缺失异常](Runtime/Modules/Config/Definitions/KitConfigMissingException.md) → [EditorUtil.Config.KitConfigScanner.md · 扫描工具](Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.KitConfigScanner.md) → [ConfigWindow.md · Kit 配置一级组](Editor/Windows/ConfigWindow.md) |
| **构建 AssetBundle (CI/编辑器菜单)** | [EditorUtil.BundleBuilder.md · YooAsset SBP 构建封装](Editor/EditorUtil/EditorUtil.BundleBuilder/EditorUtil.BundleBuilder.md) → [PipifySteps.md · `assetbundle.build` Step](Editor/EditorUtil/EditorUtil.Pipify/PipifySteps.md) |
| **管理私有 UPM 包（安装/升级/卸载/搜索/UPM 联动）** | [PlugPalsWindow.md · Verdaccio 包管理窗口](Editor/Windows/PlugPalsWindow.md) → [EditorUtil.PlugPals.md · 工具层能力](Editor/EditorUtil/EditorUtil.PlugPals/EditorUtil.PlugPals.md) |
| **检查 UPM 包是否有新版本（启动弹窗 / 手动打开）** | [EditorUtil.CheckUpdate.md · 版本检查工具](Editor/EditorUtil/EditorUtil.CheckUpdate/EditorUtil.CheckUpdate.md) → [CheckUpdateWindow.md · 更新提示窗口](Editor/Windows/CheckUpdateWindow.md) |
| **Inspector GUI 绘制工具** | [EditorUtil.Draw.md · 全方法签名](Editor/EditorUtil/EditorUtil.Draw/EditorUtil.Draw.md) |
| **持久化存储（读写数据）** | [PersistComponent.md · 直接访问属性](Runtime/Modules/Persist/PersistComponent.md) → [PlayerPrefsManager.md](Runtime/Modules/Persist/PlayerPrefsManager.md) / [FileFragmentManager.md](Runtime/Modules/Persist/FileFragmentManager.md) / [SQLiteManager.md](Runtime/Modules/Persist/SQLiteManager.md) |
| 理解 UIGroup 深度 / 遮挡排序 | [UIGroupHelper.md](Runtime/Modules/UI/UIGroupHelper/UIGroupHelper.md) → [UIManager.md · UIGroup.Refresh 算法](Runtime/Modules/UI/UIManager/UIManager.md) |
| **新增游戏流程**（Procedure） | [ProcedureBase.md · 继承模板+GetNextProcedureType](Runtime/Modules/Procedure/ProcedureBase.md) → [ProcedureComponent.md · 自动发现+初始化时序](Runtime/Modules/Procedure/ProcedureComponent.md) → [ProcedureManager.md · FSM 驱动](Runtime/Modules/Procedure/ProcedureManager.md)（具体 Procedure 实现由 Game 层提供，Bootstrap 分发） |
| **HybridCLR 业务 DLL 加载**（DLL 加载流程） | [ProcedureLoadDll.md · AOT metadata + DLL 加载 + 延迟注册](Runtime/Modules/Procedure/Procedures/ProcedureLoadDll.md) → [ConfigRuntimeSO.md · 运行期配置来源](Runtime/Modules/Config/ConfigRuntimeSO.md) → [DllAssetEntry.md · 运行期单字段条目](Runtime/Modules/Config/Definitions/DllAssetEntry.md) → [DllMasterAssetEntry.md · 编辑期三字段条目](Runtime/Modules/Config/Definitions/DllMasterAssetEntry.md) → [Util.Assembly.md · GetAssembly/RefreshAssemblies](Runtime/Utils/Util.Assembly.md) → [Util.HybridCLR.md · LoadAotMetadataAsync/LoadGameAssemblyAsync](Runtime/Utils/Util.HybridCLR.md) |
| **HybridCLR 编辑期原子操作**（由 Pipify 编排流水线） | [EditorUtil.HybridCLR.md · link.xml/Generate/DLL 拷贝 API](Editor/EditorUtil/EditorUtil.HybridCLR/EditorUtil.HybridCLR.md) |
| **一键流水线 Step / 批处理配置**（Pipify 自动化） | [EditorUtil.Pipify.md · Registry+Runner+Reporters](Editor/EditorUtil/EditorUtil.Pipify/EditorUtil.Pipify.md) → [PipifySteps.md · 全 Step 清单](Editor/EditorUtil/EditorUtil.Pipify/PipifySteps.md) → [PipifySteps.Export.Helpers.md · 定位辅助](Editor/EditorUtil/EditorUtil.Pipify/PipifySteps.Export.Helpers.md) |
| **Pipify 导出新模块 Step（Table/UI/Localization/Network/Sound/Vibrate）** | [PipifySteps.md · 导出分组表](Editor/EditorUtil/EditorUtil.Pipify/PipifySteps.md) → 对应 EditorUtil.\*.Exporter.md |
| **新平台打包 / Build 封装** | [EditorUtil.Build.md · BuildPlayer 薄封装](Editor/EditorUtil/EditorUtil.Build/EditorUtil.Build.md) |
| **Pipify 流水线配置窗口**（UI 入口） | [PipifyWindow.md · Batch 管理+参数编辑+运行](Editor/Windows/PipifyWindow.md) |
| **Jenkins 自动化 / CLI 批处理** | [EditorUtil.Pipify.md · RunBatchForCliAsync+参数覆盖](Editor/EditorUtil/EditorUtil.Pipify/EditorUtil.Pipify.md) |
| **热更新业务脚本挂载**（HybridCLR 原生 MB + Prefab 直挂） | 业务 UIView / 业务逻辑类直接继承 `MonoBehaviour`，Prefab 预挂后由 HybridCLR dll 加载时 Unity 反序列化自动恢复。旧 NovaBehaviour/IBaseLife 桥接已废止（2026-05-21）。 |
| **多语言本地化**（显示文本/切换语言/字体适配） | [LocalizationComponent.md · 初始化时序+GetText](Runtime/Modules/Localization/LocalizationComponent.md) → [LocalizationManager.md · ResolveLanguage+状态机](Runtime/Modules/Localization/LocalizationManager.md) → [LocalizationSettings.md · 文本Map+字体List双组设置](Runtime/Modules/Localization/LocalizationSettings.md) → [TextLocalizing.md · TMP专用+字体刷新链](Runtime/Modules/Localization/TextLocalizing.md) |
| **理解 FSM 工具** | [FsmState.md · 状态基类](Runtime/Core/Fsm/FsmState.md) → [Fsm.md · FSM 实现+接口](Runtime/Core/Fsm/Fsm.md) |
| **HTTP 请求（AES 加密 / UniTask 异步）** | [NetworkComponent.md · GetAsync/PostAsync](Runtime/Modules/Network/NetworkComponent.md) → [HttpManager.md · Transport SPI + DoH 候选链](Runtime/Modules/Network/HttpManager/HttpManager.md) → [IDownloadService.md · 下载接口](Runtime/Modules/Network/HttpManager/IDownloadService.md) → [HttpResponse.md · 响应与进度数据](Runtime/Modules/Network/HttpManager/Definitions/HttpResponse.md) |
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
| [ChannelType.md](Runtime/Core/Definitions/ChannelType.md) | 业务渠道类型枚举（None/Official/Google/Appstore/WX/DY/Alipay） |
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
| [AssetManagerConfig.md](Runtime/Modules/Asset/AssetManager/Definitions/AssetManagerConfig.md) | Asset Manager 配置类（包名/自动清理/EditorPlayMode/RuntimePlayMode/热更总开关 EnableHotfix/CheckTimeout/IdleTimeout/并发） |
| [AssetDownloader.md](Runtime/Modules/Asset/AssetManager/Definitions/AssetDownloader.md) | IAssetDownloader 实现（YooAsset ResourceDownloaderOperation 包装） |
| [AssetRemoteService.md](Runtime/Modules/Asset/AssetManager/Definitions/AssetRemoteService.md) | YooAsset 远端寻址服务（`cmdName -> URL` 桥接 + 占位符替换） |
| [YooAssetHandleAdapter.md](Runtime/Modules/Asset/AssetManager/Definitions/YooAssetHandleAdapter.md) | IAssetHandle 到 YooAsset.AssetHandle 的 ReferencePool 适配器 |
| [YooAssetSubAssetsHandleAdapter.md](Runtime/Modules/Asset/AssetManager/Definitions/YooAssetSubAssetsHandleAdapter.md) | ISubAssetsHandle 到 YooAsset.SubAssetsHandle 的 ReferencePool 适配器 |
| [YooAssetAllAssetsHandleAdapter.md](Runtime/Modules/Asset/AssetManager/Definitions/YooAssetAllAssetsHandleAdapter.md) | IAllAssetsHandle 到 YooAsset.AllAssetsHandle 的 ReferencePool 适配器 |
| [YooAssetRawFileHandleAdapter.md](Runtime/Modules/Asset/AssetManager/Definitions/YooAssetRawFileHandleAdapter.md) | IRawFileHandle 到 YooAsset.RawFileHandle 的 ReferencePool 适配器 |
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
| [ConfigComponent.md](Runtime/Modules/Config/ConfigComponent.md) | 配置 Component（AB 异步加载入口，暴露 IsLoadOver / Common / Namespace / PluginConfig 查询） |
| [ConfigManager.md](Runtime/Modules/Config/ConfigManager.md) | 配置 Manager（AB 加载 ConfigRuntimeSO + 解析单值 CommonConfig + PluginConfig 索引；Namespace 是全框架命名空间唯一权威） |
| [ConfigManagerConfig.md](Runtime/Modules/Config/Definitions/ConfigManagerConfig.md) | 配置 Manager 初始化入参（AssetLocation） |
| [ConfigManagerBase.md](Runtime/Modules/Config/Implements/ConfigManagerBase.md) | 配置 Manager 抽象基类（Priority=10） |
| [IConfigManager.md](Runtime/Modules/Config/Interfaces/IConfigManager.md) | 配置 Manager 接口（IsLoadOver / DevelopMode / Common / Namespace / Platform / Channel + LoadAsync + GetSDKPluginConfig×2 + GetKitConfig×2 + GetAllPluginConfigs） |
| [ConfigMasterSO.md](Runtime/Modules/Config/ConfigMasterSO.md) | Config 主 SO（设计态）：Platform×Channel 矩阵 + CommonByMode + EnabledSDKs + KitConfigsByMode（三维矩阵）+ EnabledKits；GetCommon(mode)；per-panel 维度掩码（CommonMask/SDKMasks/KitMasks/NamespaceMask/HybridCLRMask/YooAssetMask）+ 顶层 Override 旁路（NamespaceOverrides/HybridCLROverrides/YooAssetOverrides） |
| [Definitions/PanelDimensionMask.md](Runtime/Modules/Config/Definitions/PanelDimensionMask.md) | 配置面板维度掩码（ByPlatform/ByChannel/ByDevelopMode + IsGlobal 属性） |
| [Definitions/TypedDimensionMask.md](Runtime/Modules/Config/Definitions/TypedDimensionMask.md) | 带类型全名的维度掩码条目（TypeName + Mask）；供 SDKMasks / KitMasks 使用 |
| [Definitions/NamespaceOverride.md](Runtime/Modules/Config/Definitions/NamespaceOverride.md) | Namespace 字段维度 Override 单项（Platform/Channel/DevelopMode 坐标 + Value） |
| [Definitions/HybridCLROverride.md](Runtime/Modules/Config/Definitions/HybridCLROverride.md) | HybridCLR 面板四字段维度 Override 单项（Editor-only） |
| [Definitions/YooAssetOverride.md](Runtime/Modules/Config/Definitions/YooAssetOverride.md) | YooAsset 两路径维度 Override 单项（Editor-only） |
| [ConfigRuntimeSO.md](Runtime/Modules/Config/ConfigRuntimeSO.md) | Config 运行态导出物 SO：DevelopMode + Common（单值）+ Platform + Channel + EnabledSDKConfigs + EnabledKitConfigs；GetKitConfig<T>() |
| [Definitions/IKitConfig.md](Runtime/Modules/Config/Definitions/IKitConfig.md) | Kit 固有配置 marker 接口（DisplayName），全局单份，由 ConfigWindow「Kit 配置」管理 |
| [Definitions/KitConfigMissingException.md](Runtime/Modules/Config/Definitions/KitConfigMissingException.md) | Kit 配置缺失异常；fail-fast 暴露配置漏填 |
| [CommonConfig.md](Runtime/Modules/Config/CommonConfig.md) | 全局公共配置（AppID / AppAesKey / AppAesIV 三个单值字段；Namespace 已移至 ConfigMasterSO 顶层） |
| [PlatformChannelEntry.md](Runtime/Modules/Config/PlatformChannelEntry.md) | Platform×Channel 矩阵行（SDKConfigsByMode 按 DevelopMode 分组；GetSDKConfigs(mode)） |
| [Definitions/DllAssetEntry.md](Runtime/Modules/Config/Definitions/DllAssetEntry.md) | DLL 运行期寻址条目（AssetLocation 单字段），供 ConfigRuntimeSO.AotMetadataDlls / GameDlls 持有 |
| [Definitions/DllMasterAssetEntry.md](Runtime/Modules/Config/Definitions/DllMasterAssetEntry.md) | DLL 主配置条目（编辑期三字段：SourceLocation / TargetLocation / AssetLocation），供 ConfigMasterSO.AotMetadataDlls / GameDlls 持有 |

### Table（表格数据）

| 文档 | 说明 |
|------|------|
| [TableComponent.md](Runtime/Modules/Table/TableComponent.md) | 表格 Component（GetTable/HasTable 统一查询入口） |
| [TableManager.md](Runtime/Modules/Table/TableManager.md) | 表格 Manager（两阶段加载：AB JSON 并行 → 反射 TableTables） |
| [TableManagerConfig.md](Runtime/Modules/Table/Definitions/TableManagerConfig.md) | 表格 Manager 配置类 |
| [TableSettings.md](Runtime/Modules/Table/Definitions/TableSettings.md) | TableUnitSetting（IndexField、TableMode）+ IDataTableSettings/IDataTableUnitSetting 接口实现 |
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
| [NetService.md](Runtime/Modules/Network/NetService.md) | 网络请求静态编排器（Protobuf + AES-128-CBC 全流程；`SendAsync` 带 `[EditorBrowsable(Never)]`，仅供业务 Service 调用） |
| [NetBuilder.md](Runtime/Modules/Network/NetBuilder.md) | 请求构建静态工具（Header 构建、Proto 序列化、AES 加密、Header JSON；整类 `[EditorBrowsable(Never)]`） |
| [NetResponse.md](Runtime/Modules/Network/NetResponse.md) | 业务层网络响应泛型包装（`IsSuccess` / `ErrorCode` / `Data`；静态工厂 `Success` / `Fail`） |
| [NetErrorCode.md](Runtime/Modules/Network/NetErrorCode.md) | 网络层错误码常量（客户端段负数 + 服务端通用段正数） |
| [NetworkComponentKitExtensions.md](Runtime/Modules/Network/NetworkComponentKitExtensions.md) | `NetworkComponent` Kit 扩展方法（`SetDebugMode`，已下沉至 `NovaFramework.Runtime`） |

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
> 本索引只覆盖主框架内的 SDK 公共层。各 SDK 子包可以单向依赖这些公共契约，但文档由各自 UPM 包独立维护，不再在此建立导航耦合。

| 文档 | 说明 |
|------|------|
| [Runtime/Modules/SDK/ARCHITECTURE.md](Runtime/Modules/SDK/ARCHITECTURE.md) | SDK 模块架构总览 / ADR / 目录骨架 |
| [SDKComponent.md](Runtime/Modules/SDK/SDKComponent.md) | SDK Component（GetComponentsInChildren 收集插件 + 异步初始化入口） |
| [Definitions/ISDKPlugin.md](Runtime/Modules/SDK/Definitions/ISDKPlugin.md) | SDK 插件基接口（Name / Priority / IsAvailable / InitializeAsync / DisposeAsync） |
| [Definitions/SDKPluginBase.md](Runtime/Modules/SDK/Definitions/SDKPluginBase.md) | SDK 插件通用抽象基类（纯 C#，非 MonoBehaviour，模板方法 + IsAvailable 管理） |
| [Definitions/PluginBase.md](Runtime/Modules/SDK/Definitions/PluginBase.md) | SDK 插件泛型基类（IPluginBaseMarker + PluginBase<TConfig>，自动 config 注入分支） |
| [Managers/Interfaces/ISDKManager.md](Runtime/Modules/SDK/Managers/Interfaces/ISDKManager.md) | SDK Manager 接口（Initialize / Get / TryGet / GetAll / Broadcast*） |
| [Managers/Implements/SDKManager.md](Runtime/Modules/SDK/Managers/Implements/SDKManager.md) | SDK Manager 唯一实现（Priority=16，Priority 分桶并行初始化，失败隔离，自动注入 PluginBase 派生插件） |
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
| [VibrateUnitSetting.md](Runtime/Modules/Vibrate/VibrateUnitSetting.md) | 振动单元设置（SourcePath、导出路径、AssetLocation、DataTypeNames） |
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
| [SoundUnitSetting.md](Runtime/Modules/Sound/SoundUnitSetting.md) | 单个声音数据源的单元设置（SourcePath、导出路径、AssetLocation、DataTypeNames） |
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
| [EditorUtil.Serializer.md](Editor/EditorUtil/EditorUtil.Serializer/EditorUtil.Serializer.md) | 反射读取私有字段（RuntimeDrawer 用） |
| [EditorUtil.ScriptingDefineSymbols.md](Editor/EditorUtil/EditorUtil.ScriptingDefineSymbols/EditorUtil.ScriptingDefineSymbols.md) | 脚本宏定义增删查工具 |
| [EditorUtil.TypeCache.md](Editor/EditorUtil/EditorUtil.TypeCache/EditorUtil.TypeCache.md) | 编辑器类型缓存（反射收集实现类名称） |
| [EditorUtil.CsvExporter.md](Editor/EditorUtil/EditorUtil.CsvExporter/EditorUtil.CsvExporter.md) | CSV 导出工具 |
| [EditorUtil.Asmdef.md](Editor/EditorUtil/EditorUtil.Asmdef/EditorUtil.Asmdef.md) | Assembly Definition 命名空间解析（向上查找 .asmdef） |
| [EditorUtil.Excel.md](Editor/EditorUtil/EditorUtil.Excel/EditorUtil.Excel.md) | Excel 读写工具（EPPlus 写入 + ExcelDataReader 读取，Config 预过滤使用） |
| [EditorUtil.Environment.md](Editor/EditorUtil/EditorUtil.Environment/EditorUtil.Environment.md) | 编辑器运行时环境数据工具（EnvironmentData / ColumnIndices / GetEnvironmentData，Channel 来自 RuntimeProvider，Config 和 Network 预过滤器共用） |
| [EditorUtil.Environment.Python3.md](Editor/EditorUtil/EditorUtil.Environment/EditorUtil.Environment.Python3.md) | Python3 运行环境多路径探测检查器（5 策略：ExplicitPath/PATH/PyLauncher/Where/PythonFallback；SessionState 缓存；33 候选路径） |
| [EditorUtil.Environment.LubanChecker.md](Editor/EditorUtil/EditorUtil.Environment/EditorUtil.Environment.LubanChecker.md) | Luban 运行环境检查器（dotnet-sdk 路径/版本≥8.0/Luban.dll 检测，SessionState 缓存；由 Pipeline 和 ConfigWindow 调用） |
| [EditorUtil.Luban.Pipeline.md](Editor/EditorUtil/EditorUtil.Luban/EditorUtil.Luban.Pipeline.md) | Luban 导出流水线 + LubanExportContext（Table/Config 统一入口，含环境检查 guard） |
| [EditorUtil.Luban.CliRunner.md](Editor/EditorUtil/EditorUtil.Luban/EditorUtil.Luban.CliRunner.md) | Luban CLI 外部进程调用器（代码生成/数据导出/protobuf3 schema 生成） |
| [EditorUtil.Proto.CliRunner.md](Editor/EditorUtil/EditorUtil.Proto/EditorUtil.Proto.CliRunner.md) | protoc CLI 外部进程调用器（Mac + Win 跨平台，Luban→protoc 闭环管线） |
| [EditorUtil.Luban.ConfigSyncer.md](Editor/EditorUtil/EditorUtil.Luban/EditorUtil.Luban.ConfigSyncer.md) | Luban _configs/ 目录同步器（luban.conf + __tables__.xml） |
| [EditorUtil.Luban.JsonMerger.md](Editor/EditorUtil/EditorUtil.Luban/EditorUtil.Luban.JsonMerger.md) | Luban per-table JSON → per-Excel Nova 格式合并器 |
| [EditorUtil.Luban.MapPropGen.md](Editor/EditorUtil/EditorUtil.Luban/EditorUtil.Luban.MapPropGen.md) | Map 模式属性生成器（TbXxx partial class 追加） |
| [EditorUtil.Luban.LocalizationTextExporter.md](Editor/EditorUtil/EditorUtil.Luban/EditorUtil.Luban.LocalizationTextExporter.md) | 本地化文本导出器（PreFilter + Luban Pipeline 三阶段编排：C# 类型生成 / 按语言数据导出 / MapPropGen + 语言列表） |
| [EditorUtil.Luban.ExportHelper.md](Editor/EditorUtil/EditorUtil.Luban/EditorUtil.Luban.ExportHelper.md) | Luban 导出辅助工具：构建导出上下文、生成关联文件名、查找单元设置 |
| [EditorUtil.Luban.DataTypeNameHelper.md](Editor/EditorUtil/EditorUtil.Luban/EditorUtil.Luban.DataTypeNameHelper.md) | 数据类型名称刷新工具：读取数据源文件提取有效 Sheet 名称并填充 DataTypeNames |
| [EditorUtil.Draw.SourceFileTree.md](Editor/EditorUtil/EditorUtil.Draw/EditorUtil.Draw.SourceFileTree.md) | 数据源文件树绘制与命名空间列表编辑静态工具集 |
| [EditorUtil.CheckUpdate.md](Editor/EditorUtil/EditorUtil.CheckUpdate/EditorUtil.CheckUpdate.md) | UPM 包版本检查工具（启动自动检查 + `MarkSkip`/`ClearSkip` 持久化，复用 PlugPals 网络层） |
| [EditorUtil.HybridCLR.md](Editor/EditorUtil/EditorUtil.HybridCLR/EditorUtil.HybridCLR.md) | HybridCLR 原子操作合集（link.xml 校验/补全、Generate 系列封装、AOT/业务 DLL 拷贝；由 Pipify Steps 编排流水线） |
| [EditorUtil.AndroidResolver.md](Editor/EditorUtil/EditorUtil.AndroidResolver/EditorUtil.AndroidResolver.md) | Android 依赖解析工具（反射调用 EDM4U PlayServicesResolver.ResolveSync，强制重建 Assets/GeneratedLocalRepo/**；配合 HybridCLR Generate All 前置使用） |
| [EditorUtil.Pipify.md](Editor/EditorUtil/EditorUtil.Pipify/EditorUtil.Pipify.md) | [PipifyStep] 反射注册 Step + Batch 可视化配置 + UI/CLI 双入口的自动化流水线 |
| [EditorUtil.Build.md](Editor/EditorUtil/EditorUtil.Build/EditorUtil.Build.md) | BuildPipeline.BuildPlayer 薄封装，统一异常与日志 |
| [EditorUtil.Asset.Operator.md](Editor/EditorUtil/EditorUtil.Asset/EditorUtil.Asset.Operator.md) | 通用 ScriptableObject 资产查找/创建/按路径加载（泛型 Find&lt;T&gt; / CreateAt&lt;T&gt; / LoadAt&lt;T&gt;） |
| [EditorUtil.Config.StructureGuard.md](Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.StructureGuard.md) | Platform×Channel 枚举网格补齐与缺失引用清理 |
| [EditorUtil.Config.SDKPluginScanner.md](Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.SDKPluginScanner.md) | 全程序集扫描 ISDKPluginConfig 实现类型 + 实例补全/移除（EnsureInstance/RemoveInstance 按 DevelopMode 分组） |
| [EditorUtil.Config.Validator.md](Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.Validator.md) | CommonConfig/PluginConfig 必填字段校验（Severity 枚举 + ValidationIssue 结构体；支持三维 Platform×Channel×DevelopMode） |
| [EditorUtil.Config.Exporter.md](Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.Exporter.md) | 将 ConfigMasterSO 三维组合导出为 ConfigRuntimeSO.asset |
| [EditorUtil.Config.RuntimeProvider.md](Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.RuntimeProvider.md) | 从 AssetDatabase 按三维（Platform×Channel×DevelopMode）读取 ConfigRuntimeSO（不缓存，替代已删除的 ConfigLookup）；GetChannel() 新增 |
| [EditorUtil.Config.WorkspaceActive.md](Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.WorkspaceActive.md) | 工程级激活 ConfigMaster 锚点；通过 Globals.json 持久化 GUID，四段回退策略，根除多 Sample 共存命中歧义 |
| [EditorUtil.Config.YooAssetInjector.md](Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.YooAssetInjector.md) | Asset 模块编辑期注入层；按 ConfigMaster 路径字段注入 YooAssetSettings / 加载 BundleCollectorSetting，替代全工程扫描 |
| [EditorUtil.Config.DimensionProjector.md](Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.DimensionProjector.md) | 维度投影器；按 PanelDimensionMask 在 WorkingCopy 上执行加维分裂/减维合并/广播，支持矩阵三类和顶层三类面板 |
| [EditorUtil.Config.DimensionalResolver.md](Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.DimensionalResolver.md) | 顶层类维度取数器（只读）；按坐标 + 掩码从 Override 列表解析 Namespace / HybridCLR / YooAsset 最终值 |
| [EditorUtil.Table.Exporter.md](Editor/EditorUtil/EditorUtil.Table/EditorUtil.Table.Exporter.md) | Table 模块 Luban 导出入口（ExportAll/ExportCode/ExportData） |
| [EditorUtil.UI.Exporter.md](Editor/EditorUtil/EditorUtil.UI/EditorUtil.UI.Exporter.md) | UI 模块 Luban 导出入口（ExportAll/ExportCode/ExportData + 单文件 ExportCodeForFile/ExportDataForFile） |
| [EditorUtil.Localization.TextExporter.md](Editor/EditorUtil/EditorUtil.Localization/EditorUtil.Localization.TextExporter.md) | 本地化文本导出工具（ExportTextAll/ExportTextCode/ExportTextData/ExportSupportedLanguages，三阶段 PreFilter + Pipeline） |
| [EditorUtil.Localization.FontExporter.md](Editor/EditorUtil/EditorUtil.Localization/EditorUtil.Localization.FontExporter.md) | 本地化字体导出工具（ExportFontAll/ExportFontCode/ExportFontData，标准 Luban Pipeline） |
| [EditorUtil.Network.HostKeyExporter.md](Editor/EditorUtil/EditorUtil.Network/EditorUtil.Network.HostKeyExporter.md) | 域名表导出工具（ExportHostKeyAll/ExportHostKeyCode/ExportHostKeyData，含 NetworkExcelPreFilter 预过滤） |
| [EditorUtil.Network.NetCmdExporter.md](Editor/EditorUtil/EditorUtil.Network/EditorUtil.Network.NetCmdExporter.md) | 指令表导出工具（ExportNetCmdAll/ExportNetCmdCode/ExportNetCmdData，含 NetworkExcelPreFilter 预过滤） |
| [EditorUtil.Network.ProtoExporter.md](Editor/EditorUtil/EditorUtil.Network/EditorUtil.Network.ProtoExporter.md) | Proto 协议批量编译（ExportAllProtos：按每个 Unit.CSharpExportPath 分别调 Proto.CliRunner.CompileSingle） |
| [EditorUtil.Sound.Exporter.md](Editor/EditorUtil/EditorUtil.Sound/EditorUtil.Sound.Exporter.md) | Sound 模块 Luban 导出薄封装（ExportAll/ExportData/ExportCode，unitSetting 控制单文件还是全量） |
| [EditorUtil.Vibrate.Exporter.md](Editor/EditorUtil/EditorUtil.Vibrate/EditorUtil.Vibrate.Exporter.md) | Vibrate 双轨导出工具（Emphasis + Custom 各提供 Data/Code/All，方法直接挂在 EditorUtil.Vibrate partial class 上） |

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

## Editor — Tools（工具 + AB 处理器）

| 文档 | 说明 |
|------|------|
| [EditorUtil.BundleBuilder.md](Editor/EditorUtil/EditorUtil.BundleBuilder/EditorUtil.BundleBuilder.md) | YooAsset ScriptableBuildPipeline 资源包构建封装（11 项参数） |
| [EditorUtil.BundleBuilder.md](Editor/EditorUtil/EditorUtil.BundleBuilder/EditorUtil.BundleBuilder.md) | AB 名称整理与构建封装 |
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
| [LocalizationComponentInspector.md](Editor/Inspectors/LocalizationComponentInspector/LocalizationComponentInspector.md) | 本地化 Inspector（Luban 驱动：文本三阶段 Pipeline 导出 + 字体标准 Pipeline 导出） |
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

## Editor — DataPipeline（Excel 预过滤管线）

| 文档 | 说明 |
|------|------|
| [DataPipeline.md](Editor/DataPipeline/DataPipeline.md) | DataPipeline 目录概览（Config 预过滤器已移除；当前保留 Localization/Network 两个预过滤实现） |
| [LocalizationExcelPreFilter.md](Editor/DataPipeline/Implements/Localizations/LocalizationExcelPreFilter.md) | Localization 模块：Excel 预过滤器（多语言列拆分为按语言 Name+Value 临时 Excel） |
| [NetworkExcelPreFilter.md](Editor/DataPipeline/Implements/Networks/NetworkExcelPreFilter.md) | Network 模块 Excel 预过滤器（域名表 DevelopValue/PublishValue 合并 + 指令表 Platform/Channel 过滤） |

## Editor — Menus / Tools

| 文档 | 说明 |
|------|------|
| [Menus.md](Editor/Menus/Menus.md) | Nova 顶级菜单项 |
| [FolderMenuItems.md](Editor/Menus/FolderMenuItems.md) | Open IDE Project / Open Folder |
| [EnableLogsMenuItems.md](Editor/Menus/EnableLogsMenuItems.md) | Enable Logs 菜单 |
| [Tools.md](Editor/Tools/Tools.md) | 编辑器独立工具（Bootstrap、构建脚本、AB 处理器） |
