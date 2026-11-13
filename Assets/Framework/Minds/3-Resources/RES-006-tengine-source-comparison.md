---
id: RES-006
title: TEngine 源码对比与 Nova 借鉴评估
summary: TEngine 借鉴取舍、可选落地与原生能力纠偏
category: external
status: active
date: 2026-11-12
aliases:
  - TEngine 源码调研
  - RES-006-tengine-source-comparison
keywords: [TEngine, Nova, UI, SpriteAtlas, 生命周期, 代码生成, 行为测试, 可选增强, 原生能力]
tags: [resource, nova, tengine, architecture]
related:
  - "[[ADR-001-component-manager-three-layer]]"
  - "[[ADR-042-assetmanager-load-api-all-return-handle]]"
  - "[[ADR-032-drop-novabehaviour-bridge]]"
---

# RES-006 TEngine 源码对比与 Nova 借鉴评估

TEngine 最值得 Nova 吸收的是**把高频、容易出错的业务接线封装成便利能力**：控件绑定、子控件归属、页面订阅收尾、图片异步替换。Nova 在本轮核对的启动失败处理、资源句柄、配置导出和包管理链路上已有更细的保护，应在这些基础上补便利层。

本条目一至六节保留固定源码基线的外部研究与采用建议；研究阶段的“当前”指下表基线，不代表后续工作树。后续实现与复盘单列在文末，具体用法回到所属 Docs，不将建议升级为架构决策或全平台验收结论。

## 来源与适用范围

| 项目 | 核对基线 |
|---|---|
| TEngine | [main 提交 a83415119d9129ac995a58ad48e37a4ef153419d](https://github.com/Alex-Rachel/TEngine/tree/a83415119d9129ac995a58ad48e37a4ef153419d)，提交日期 2026-11-10；包 `com.alex.tengine` 6.0.0 |
| Nova | HEAD `30d8189df5c19f17b92c75f933f704acfcbf393e`；[Framework package.json](../../package.json) 为 `com.solotopia.nova.framework` 0.7.8 |
| 工程 Unity 版本 | TEngine 的 [ProjectVersion.txt](https://github.com/Alex-Rachel/TEngine/blob/a83415119d9129ac995a58ad48e37a4ef153419d/UnityProject/ProjectSettings/ProjectVersion.txt#L1-L2) 为 2022.3.62f3；Nova 的 [ProjectVersion.txt](../../../../ProjectSettings/ProjectVersion.txt) 为 6000.4.2f1。工程版本、包声明的最低版本和实际兼容验证须分开 |

范围包括核心模块、UI、事件、计时、资源、启动、热更、图集生产、表导出、构建、分发和相关测试/自动化；未把 Fantasy 分支或外部服务端能力算进当前主线。两者都使用 YooAsset、HybridCLR、UniTask、Luban，不能把共有依赖的能力算作 TEngine 独创。

可信度：类型、调用链和条件分支来自当前源码；易用性收益是工程判断。未进行 Unity 运行、性能对跑、真实 Bundle/CDN 或设备验证，因此不据此宣称谁更快、内存更低或已达到生产可靠性。

## 一 主要优劣对比

| 方面 | TEngine 当前特点 | Nova 当前特点 | 判断 |
|---|---|---|---|
| 核心架构 | Module 接口、按命名创建实现、按需注册更新 | Component 接 Unity，Manager 承担实现，接口隔离；结构与配置入口明确 | TEngine 样板较少；Nova 文件和接线更多，但契约边界更清楚。无需替换 Nova 架构 |
| 页面内部开发 | 前缀扫描、typed 绑定生成、Window/Widget 组合、小列表辅助 | UIView、序列化引用、表注册、分组和实例池；页面内部仍需较多手工接线 | TEngine 有可直接借鉴的便利机制 |
| 页面异步体验 | 提供可 await 的显示入口，但完成/失败判断不严 | 打开异步执行，公开入口返回 serialID；已有加载中关闭处理 | Nova 可补可靠的可等待结果，而非照搬轮询等待 |
| 图片与图集 | 目标级请求替换、图集 Handle 共享、GetSprite 克隆回收 | 通用资源/子资源句柄；没有同等 Image/SpriteRenderer 绑定层 | 是本轮最明确的业务便利性增量之一 |
| 资源所有权 | 裸资源加内部对象池计数；实例挂引用组件回收 | 中性 Handle 显式拥有；Prefab 实例销毁归还句柄 | 两者都有实例回收；Nova 应保留显式 Handle |
| 多内容包加载 | 主要加载入口可传 packageName | 元数据/下载能选包，主要 Load 入口固定默认包 | Nova 存在具体接线缺口；有多内容包需求再补 |
| 启动与热更 | 主线流程可读，但部分失败、离开和恢复路径不完整 | AOT→业务 DLL 屏障、流程取消、可启动版本检查及清单回退 | 保留 Nova 现有防护，不搬 TEngine loader |
| 缓存释放 | 实际为过期、优先级、LastUseTime 规则 | 已有同类规则 | LRU 不是独有优势；未找到支撑 ARC 宣传的实现 |
| 图集生产 | dirty 集合合并变更，已有图集原地修改 | 现有 Bundle/Pipify 主链未提供同等第一方图集生产器；Unity 已有原生文件夹图集 | 仅有批量规则等明确需求再选便利步骤；原判断的价值边界见[图集取舍的纠正](#图集取舍的纠正)，不照搬自动删源图 |
| 表、配置与构建 | 样板项目预置路径短；旧脚本存在参数和状态问题 | 多配置维度、隔离导出与发布、Pipify、PlugPals | 借短路径体验，不重建第二套配置/构建系统 |
| 验证方式 | 有针对图片竞态的真实组件测试和独立盲任务评估设计 | 已有多领域测试；部分关键路径仍是源码字符串断言 | 借可控时序与真实消费者方法，不能按测试数量排名 |

## 二 值得优先借鉴的机制

### 1 页面关闭时统一结束本次打开产生的工作

TEngine 的 `AddUIEvent` 将订阅集中登记，销毁时统一退订；父窗口也负责销毁子 Widget。它让开发者少写一组注销代码。见 [UIBase](https://github.com/Alex-Rachel/TEngine/blob/a83415119d9129ac995a58ad48e37a4ef153419d/UnityProject/Assets/GameScripts/HotFix/GameLogic/Module/UIModule/UIBase.cs#L284-L330)、[GameEventMgr](https://github.com/Alex-Rachel/TEngine/blob/a83415119d9129ac995a58ad48e37a4ef153419d/UnityProject/Assets/TEngine/Runtime/Core/GameEvent/GameEventMgr.cs#L33-L95) 和 [UIWidget](https://github.com/Alex-Rachel/TEngine/blob/a83415119d9129ac995a58ad48e37a4ef153419d/UnityProject/Assets/GameScripts/HotFix/GameLogic/Module/UIModule/UIWidget.cs#L201-L232)。

Nova 已有正确的页面取消和代次模式：[DemoAppView](../../../Samples/MainDemo/Scripts/Runtime/UIs/DemoAppView/DemoAppView.cs) 第 95–154 行会在打开时新建 CTS，在关闭/销毁时取消，并阻止旧请求回写；但这仍由具体页面维护。[UIView](../../Scripts/Runtime/Modules/UI/Definitions/UIView.cs) 的关闭、回收和最终销毁是不同阶段，不能用 GameObject 销毁 token 覆盖全部情况。

建议把 Nova 自有模式抽成**可选的每次打开作用域**：统一收集退订、取消令牌和动态子部件；关闭即结束，重开重新建立。实例长期引用另行管理。既有 Nova.Event/Nova.Prefab 继续承担分发和资源工作，框架受控流程保障释放幂等，不依赖每个子类都记得调用基类收尾。

验收重点：打开→关闭→归池→重开，旧响应晚到，重复关闭，监听异常，Unity Stop。收益看业务页面减少了多少重复清理代码，以及是否消除了重复订阅和旧结果回写。

### 2 给 Image 换图时自动处理过期结果和资源归属

典型问题：背包格子先请求图标 A，随后被复用显示 B，A 晚到却覆盖 B。另一个问题是多个目标共享图集，其中一个取消时不能释放其他目标仍需要的资源。

TEngine 的 [ResourceExtComponent.Resource](https://github.com/Alex-Rachel/TEngine/blob/a83415119d9129ac995a58ad48e37a4ef153419d/UnityProject/Assets/TEngine/Runtime/Module/ResourceModule/Extension/ResourceExtComponent.Resource.cs#L157-L222) 记录目标当前请求，替换时取消旧请求，回写前再检查请求身份和目标存活。[SubSprite 实现](https://github.com/Alex-Rachel/TEngine/blob/a83415119d9129ac995a58ad48e37a4ef153419d/UnityProject/Assets/TEngine/Runtime/Module/ResourceModule/Extension/ResourceExtComponent.SubSprite.cs#L43-L138) 区分真正 SpriteAtlas 与普通切图，共享加载等待，并回收 `GetSprite` 产生的克隆。

Nova 的 [LoadAsync](../../Scripts/Runtime/Modules/Asset/Managers/AssetManager/Implements/AssetManager.Load.cs) 第 52–69 行、[LoadSubsAsync](../../Scripts/Runtime/Modules/Asset/Managers/AssetManager/Implements/AssetManager.LoadSubs.cs) 第 54–71 行已有句柄和调用方取消；差距在目标绑定层，不在底层能否加载 SpriteAtlas。

建议在现有 Handle 之上补图片绑定：每目标保留当前绑定，新请求成功后替换并释放旧所有权；取消、失败、清空、关闭与最终销毁分别定义。真正图集与普通切图分别处理。不要照搬 TEngine 的周期扫描及延迟回收作为唯一机制，也不要直接把 YooAsset 类型暴露给业务。

验收重点：A/B 完成顺序反转，共享图集单方取消，缺图保留旧图，跨普通图/子图 API 替换，目标销毁，克隆计数归零。TEngine 已有对应测试设计，见本条目第五节。

### 3 让业务能够等待页面真正打开并拿到明确结果

TEngine 提供 `ShowUIAsyncAwait`，调用形式便于继续编排；但 [ShowUIAwaitImp](https://github.com/Alex-Rachel/TEngine/blob/a83415119d9129ac995a58ad48e37a4ef153419d/UnityProject/Assets/GameScripts/HotFix/GameLogic/Module/UIModule/UIModule.cs#L338-L363) 对已有窗口直接返回，新窗口超时也返回对象，不能把返回等同准备成功。

Nova 的 [OpenUIViewAsync](../../Scripts/Runtime/Modules/UI/Managers/UIManager/Implements/UIManager.cs) 第 258–305 行返回 serialID，加载中关闭已有处理；实际异步由内部执行，失败走通知。建议保留兼容入口，增加关联本次 serialID 的可等待结果：完成绑定和 OnOpen 才成功，失败、取消、被关闭有明确终态。复用同一 UI 执行链，不新建管理器，不用逐帧查询加超时冒充完成协议。

验收重点：已打开、同页仍加载、打开后立刻关闭、加载失败、OnOpen 抛错、重复等待及归池重开。此项涉及公共接口，应与绑定工具分开评审。

### 4 自动生成机械绑定，手写业务保持独立

TEngine 真实提供节点前缀扫描、typed 字段、事件样板和子控件边界；当前 [ScriptGeneratorSetting](https://github.com/Alex-Rachel/TEngine/blob/a83415119d9129ac995a58ad48e37a4ef153419d/UnityProject/Assets/Editor/UIScriptGenerator/ScriptGeneratorSetting.asset#L15-L26) 启用引用表绑定。[ScriptAutoGenerator](https://github.com/Alex-Rachel/TEngine/blob/a83415119d9129ac995a58ad48e37a4ef153419d/UnityProject/Assets/Editor/UIScriptGenerator/ScriptAutoGenerator.cs#L184-L246) 分开生成文件与人工实现文件；第 425–435 行对已有人工实现跳过生成。这一职责划分值得借鉴。

Nova 的 [UIExporter](../../Scripts/Editor/DataPipeline/Implements/UIs/UIExporter.cs) 负责 UI 注册表、Luban 代码和 Map，职责不同；现有 [创建页面能力](../../Agents/Skills/nova-project-ui-create-view/SKILL.md) 已覆盖脚本、Prefab、引用与注册。不能把“缺批量绑定助手”扩大成“Nova 没有 UI 生成”。

采用前先核对当前 PSD2UGUI 能否覆盖同一任务；需要补充时，以既有 UIView/序列化字段为目标，提供扫描结果预览、一次完整校验和稳定重复生成。保留业务代码、已有监听、嵌套 Prefab 和变体。不要引入整数索引绑定协议、无条件 RemoveAllListeners 或先删旧代码再写新代码。

验收重点：20 个以上真实控件的页面，重名、缺组件、改名、删除及变体均不静默错绑；重跑不覆盖业务，不触发无关改动；用实际操作数和耗时判断是否值得保留。

### 5 图集只更新受影响部分，并保留已有资产身份

TEngine 的 [EditorSpriteSaveInfo](https://github.com/Alex-Rachel/TEngine/blob/a83415119d9129ac995a58ad48e37a4ef153419d/UnityProject/Assets/TEngine/Editor/AtlasMakerEditor/EditorSpriteSaveInfo.cs#L132-L168) 用 dirty 集合合并变更；第 327–395 行原地修改已有图集，第 727–778 行比较成员集合与源文件/配置修改时间。它不是完整依赖内容 Hash，但比每次全量重建更贴合日常编辑。

Nova 的 [BundleBuilder](../../Scripts/Editor/EditorUtil/EditorUtil.BundleBuilder/EditorUtil.BundleBuilder.cs) 和 [Pipify Bundle Step](../../Scripts/Editor/EditorUtil/EditorUtil.Pipify/Steps/PipifySteps.BundleBuilder.cs) 已有资源构建链，没有同等第一方的目录到图集生产步骤。需要时可在既有 Pipify/Action 中增加可选步骤，限定项目声明的来源和输出；不要求所有项目启用。

必须补齐的接线：TEngine 第 91–94 行虽有 `RefreshAllForBuild`，本轮核对的构建入口未找到调用，不能称作构建前保障。Nova 若采用，必须实际串在 Bundle 之前。先用差异预览再写入；只清有明确归属的输出，不照搬自动删除重名或带空格源图片的逻辑。

验收重点：导入、移动、删除只更新相关图集；重复生成 GUID 和引用稳定；规则变化与中断可恢复；最终 Bundle 和一份消费工程能加载实际子图。优化耗时须实测。

### 6 把能确定的错误提前到编译和生成阶段

TEngine 的 [EventInterfaceGenerator](https://github.com/Alex-Rachel/TEngine/blob/a83415119d9129ac995a58ad48e37a4ef153419d/Tools/GameEventSourceGenerator/SourceGenerator/Generator/EventInterfaceGenerator.cs#L30-L72) 从接口生成事件常量和 typed 调用/订阅助手；[GameEventAnalyzer](https://github.com/Alex-Rachel/TEngine/blob/a83415119d9129ac995a58ad48e37a4ef153419d/Tools/GameEventSourceGenerator/GameEventAnalyzer/Analyzer/GameEventAnalyzer.cs#L128-L164) 检查可识别事件的参数数量和类型。价值是把错误前移，不是再造一条事件总线。

Nova 已有 [EventTypeID](../../Scripts/Runtime/Modules/Event/Managers/Definitions/EventTypeID.cs) 的类型 ID 和 [EventPool](../../Scripts/Runtime/Modules/Event/Managers/Implements/EventPools/EventPool.cs) 的即时/排队分发、限流及异常处理。可先选择 2–3 个高频误用做诊断，或生成仍调用原事件链的适配层，保持分发时机、回池和订阅归属。

不直接移植 TEngine 生成器：[Definition](https://github.com/Alex-Rachel/TEngine/blob/a83415119d9129ac995a58ad48e37a4ef153419d/Tools/GameEventSourceGenerator/SourceGenerator/Definition.cs) 固定输出 GameLogic；生成过程按接口短名组织文件，对跨命名空间同名接口、复杂签名等仍需重新设计。Nova 采用时必须覆盖跨 asmdef、裁剪、HybridCLR 和 IDE/Unity 编译器差异。

## 三 按实际需求再补的能力

| 候选 | 当前真实差距与采用边界 |
|---|---|
| 显式非默认包加载 | TEngine [ResourceModule](https://github.com/Alex-Rachel/TEngine/blob/a83415119d9129ac995a58ad48e37a4ef153419d/UnityProject/Assets/TEngine/Runtime/Module/ResourceModule/ResourceModule.cs#L828-L854) 传递 packageName；Nova [Load 实现](../../Scripts/Runtime/Modules/Asset/Managers/AssetManager/Implements/AssetManager.Load.cs) 第 27、54 行固定默认包。有 DLC/多内容包需求时增加兼容入口，覆盖同地址不同包、UI/场景和取消；这与 UPM 模块分发是两件事 |
| 资源失败诊断 | Nova [IAssetHandle](../../Scripts/Runtime/Modules/Asset/Managers/AssetManager/Interfaces/IAssetHandle.cs) 只有有效、完成、资源与释放；完成不等于成功。图片绑定需要区分缺图、损坏、取消时，可补中性结果/错误详情，先审查全部消费者，不外泄 YooAsset 类型 |
| 子控件与小列表 | TEngine [UIBase](https://github.com/Alex-Rachel/TEngine/blob/a83415119d9129ac995a58ad48e37a4ef153419d/UnityProject/Assets/GameScripts/HotFix/GameLogic/Module/UIModule/UIBase.cs#L477-L578) 封装数量同步和分帧创建。可做可选 Presenter，复用 Nova Prefab/池与页面作用域；分帧创建全部项目不是虚拟列表，不引入第二套 Window/Widget 基类体系 |
| 局部安全区适配 | Nova 已有根缩放与物理像素安全区查询，见 [UIComponent 文档](../../Docs/Runtime/Modules/UI/UIComponent.md)。可补按真实父 RectTransform 坐标转换的局部组件；TEngine 与 Nova 存量导航示例均有直接混用像素/画布单位的问题，不能复制硬编码边距 |
| 只更新发生变化的生成文件 | Nova [Table Exporter](../../Scripts/Editor/EditorUtil/EditorUtil.Table/EditorUtil.Table.Exporter.cs) 发布目录时仍登记全部替换。可在原事务发布器内考虑字节相同跳过，验证收据、回滚和删除语义；TEngine UI 生成同样会删旧重写，不是全链增量生成范本 |
| 只轮询需要 Update 的模块 | TEngine [ModuleSystem](https://github.com/Alex-Rachel/TEngine/blob/a83415119d9129ac995a58ad48e37a4ef153419d/UnityProject/Assets/TEngine/Runtime/Core/ModuleSystem.cs#L165-L205) 单独登记 IUpdateModule；Nova [FrameworkManagersGroup](../../Scripts/Runtime/Core/FrameworkManagersGroup.cs) 第 46–51 行轮询全部 Manager。仅在 Profiler 证明值得优化时采用，不能凭少几次空调用推导帧率提升 |

HTML 原型到 uGUI 可以作为探索方向，但本轮只核实 [HtmlToUGUIBaker](https://github.com/Alex-Rachel/TEngine/blob/a83415119d9129ac995a58ad48e37a4ef153419d/UnityProject/Assets/TEngine/Extension/HtmlToUGUI/Editor/HtmlToUGUIBaker.cs) 的 JSON 到控件树窗口实现，未证明文档所述全部自动化服务已接通。Nova 已有 PSD2UGUI 路线，新增输入链需先证明能减少实际工作。

## 四 应保留 Nova 的部分与不宜照搬的实现

### 保留现有资源与启动边界

1. **显式 Handle 与实例归属。** Nova [PrefabManager.Load](../../Scripts/Runtime/Modules/Prefab/Managers/PrefabManager/Implements/PrefabManager.Load.cs)、[PrefabInstanceTag](../../Scripts/Runtime/Modules/Prefab/Definitions/PrefabInstanceTag.cs) 已把实例销毁收敛到句柄释放；自动回收不是缺项。遵循 [[ADR-042-assetmanager-load-api-all-return-handle]]，不要退回裸对象加隐式池计数。
2. **检查可启动后才提交版本。** [AssetManager](../../Scripts/Runtime/Modules/Asset/Managers/AssetManager/Implements/AssetManager.cs) 第 306–330 行检查清单和启动范围，第 507–527、605–651 行实际恢复当前/历史/内置清单；TEngine 的字符串回填不能替代这条链。
3. **流程取消和 AOT 屏障。** [ProcedureBase](../../Scripts/Runtime/Modules/Procedure/Definitions/ProcedureBase.cs) 持有流程 CTS；[ProcedureLoadDll](../../Scripts/Runtime/Modules/Procedure/Procedures/ProcedureLoadDll.cs) 第 135–178 行先完成 AOT 再加载业务 DLL；[Util.HybridCLR](../../Scripts/Runtime/Core/Util/Util.HybridCLR/Util.HybridCLR.cs) 检查错误码、合并同身份并发、允许失败后重试。这些应继续沿原实现增强。
4. **配置、导出、构建和分发主链。** [Table Exporter](../../Scripts/Editor/EditorUtil/EditorUtil.Table/EditorUtil.Table.Exporter.cs) 已隔离生成，[OutputApplier](../../Scripts/Editor/EditorUtil/EditorUtil.FileSystem/EditorUtil.FileSystem.OutputApplier.cs) 有发布和回滚；[Pipify Build Step](../../Scripts/Editor/EditorUtil/EditorUtil.Pipify/Steps/PipifySteps.Build.cs) 检查 BuildReport；[PlugPals](../../Docs/Editor/Windows/PlugPalsWindow.md) 管理实际包状态。不重建平行入口。

这些是已核对链路上的比较，不意味着 Nova 所有异常分支都已完善：例如部分通用 Load 仅受调用方 token 控制，不能称 Shutdown 自动取消全部加载；部分关键验收仍需行为测试和目标平台补证。

### TEngine 源码中需要避开的具体问题

下列是条件分支推演，没有声称本轮在 Unity 中复现。

| 问题 | 条件与影响 | 源码 |
|---|---|---|
| 控件绑定可能同类型错绑 | 生成索引遇命名节点就递增，收集引用遇缺组件却跳过。前面的 Button 缺失，可令后面的 Button 错占前一个字段索引 | [ScriptAutoGenerator](https://github.com/Alex-Rachel/TEngine/blob/a83415119d9129ac995a58ad48e37a4ef153419d/UnityProject/Assets/Editor/UIScriptGenerator/ScriptAutoGenerator.cs#L302-L316) 与同文件第 626–632 行 |
| await 返回不保证准备完成 | 已有但仍加载的窗口直接返回；等待超时仍返回对象。IsLoadDone 也早于全部准备逻辑 | [UIModule](https://github.com/Alex-Rachel/TEngine/blob/a83415119d9129ac995a58ad48e37a4ef153419d/UnityProject/Assets/GameScripts/HotFix/GameLogic/Module/UIModule/UIModule.cs#L338-L363) 与 [UIWindow](https://github.com/Alex-Rachel/TEngine/blob/a83415119d9129ac995a58ad48e37a4ef153419d/UnityProject/Assets/GameScripts/HotFix/GameLogic/Module/UIModule/UIWindow.cs#L464-L501) |
| AOT 失败可能一直等待 | 最后/唯一 metadata 回调为 null 时，减计数后直接返回，未更新完成标记；补元数据错误码仅记录而不阻断 | [ProcedureLoadAssembly](https://github.com/Alex-Rachel/TEngine/blob/a83415119d9129ac995a58ad48e37a4ef153419d/UnityProject/Assets/GameScripts/Procedure/ProcedureLoadAssembly.cs#L260-L290)，等待位置第 113–119 行 |
| 可选更新恢复边界不足 | 已有 GAME_VERSION 时先写远端版本，清单/下载尚未成功；Optional 失败分支只回填版本字符串后继续，未实际恢复对应清单/核对资源。当前示例配置为 Force，不能称 Demo 必然触发 | [ProcedureInitResources](https://github.com/Alex-Rachel/TEngine/blob/a83415119d9129ac995a58ad48e37a4ef153419d/UnityProject/Assets/GameScripts/Procedure/ProcedureInitResources.cs#L76-L104) 与同文件第 136–164 行 |
| 事件回调异常会绕过状态恢复 | Callback 先置执行标记，正常结束才 CheckModify；中途抛错会中断后续 handler，并让待增删状态留到后续成功分发。单布尔标记也不表示递归分发深度 | [EventDelegateData](https://github.com/Alex-Rachel/TEngine/blob/a83415119d9129ac995a58ad48e37a4ef153419d/UnityProject/Assets/TEngine/Runtime/Core/GameEvent/EventDelegateData.cs#L96-L154) |
| 计时器补帧无界递归 | AddTimer 不拒绝 0/负循环间隔；补帧递归反复增加该间隔，可能永不收敛；正间隔大积压也没有每帧预算 | [TimerModule](https://github.com/Alex-Rachel/TEngine/blob/a83415119d9129ac995a58ad48e37a4ef153419d/UnityProject/Assets/TEngine/Runtime/Module/TimerModule/TimerModule.cs#L287-L338)，入口第 40–56 行 |
| 自动图集处理会删除源资源 | 导入处理含空格/同名图片时会执行删除；输出清理也不能只凭目录和期望名单决定归属 | [SpritePostprocessor](https://github.com/Alex-Rachel/TEngine/blob/a83415119d9129ac995a58ad48e37a4ef153419d/UnityProject/Assets/TEngine/Editor/AtlasMakerEditor/SpritePostprocessor.cs#L244-L283) |
| 旧构建入口参数和完成状态不可靠 | CLI 读出 packageVersion 却没有传给 BuildInternal；部分失败只日志返回，窗口仍显示构建完成。不能只看“完成”文案 | [ReleaseTools](https://github.com/Alex-Rachel/TEngine/blob/a83415119d9129ac995a58ad48e37a4ef153419d/UnityProject/Assets/TEngine/Editor/ReleaseTools/ReleaseTools.cs#L36-L60) 第 119–159、219–250 行及 [BuildPipelineWindow](https://github.com/Alex-Rachel/TEngine/blob/a83415119d9129ac995a58ad48e37a4ef153419d/UnityProject/Assets/TEngine/Editor/ReleaseTools/BuildPipelineWindow.cs#L485-L497) |

缓存宣传也要单独澄清：[README](https://github.com/Alex-Rachel/TEngine/blob/a83415119d9129ac995a58ad48e37a4ef153419d/README.md#L27-L31) 声称 LRU/ARC，但实际 [池创建](https://github.com/Alex-Rachel/TEngine/blob/a83415119d9129ac995a58ad48e37a4ef153419d/UnityProject/Assets/TEngine/Runtime/Module/ResourceModule/ResourceModule.Pool.cs#L59-L65) 接普通对象池，[默认释放器](https://github.com/Alex-Rachel/TEngine/blob/a83415119d9129ac995a58ad48e37a4ef153419d/UnityProject/Assets/TEngine/Runtime/Module/ObjectPoolModule/ObjectPoolModule.ObjectPool.cs#L561-L598) 按过期、优先级和 LastUseTime 筛选。未找到 ARC 实现/策略切换。Nova [ObjectPool.Methods](../../Scripts/Runtime/Modules/ObjectPool/Managers/Implements/ObjectPools/ObjectPool.Methods.cs) 第 97–154 行已有同类规则；不应因此引入所谓“更先进缓存架构”。

接入成本也不能只看 README：工程版本、说明文档和旧 CLI 的 Unity 版本有漂移；[update-tengine.bat](https://github.com/Alex-Rachel/TEngine/blob/a83415119d9129ac995a58ad48e37a4ef153419d/update-tengine.bat) 用镜像覆盖更新部分目录，并非可追踪的完整包升级。当前主线 [网络文档](https://github.com/Alex-Rachel/TEngine/blob/a83415119d9129ac995a58ad48e37a4ef153419d/Books/3-8-%E7%BD%91%E7%BB%9C%E6%A8%A1%E5%9D%97.md) 只有待补充，Fantasy 集成属于另一个分支；Nova 已有 [HTTP/Proto/WebSocket 入口](../../Docs/Runtime/Modules/Network/NetworkComponent.md)，不能在主线比较中反过来判 Nova 缺网络能力。

## 五 更值得吸收的验证方法

**可控完成顺序，检查实际状态与引用。** TEngine [SpriteResourceLifecycleTests](https://github.com/Alex-Rachel/TEngine/blob/a83415119d9129ac995a58ad48e37a4ef153419d/UnityProject/Assets/TEngine/Tests/Editor/SpriteResourceLifecycleTests.cs) 有三个 UnityTest 场景及参数检查，使用真实组件和可控内存 Provider 验证共享、取消、旧请求、切 API、销毁。Atlas 克隆部分由测试注入，并非实际图集打包验收。

Nova 的 [ManifestFallbackRegressionTests](../../../Tests/Editor/AssetManagerManifestFallbackRegressionTests.cs) 部分用源码字符串断言保接线，不能证明坏网下实际恢复。也不能说 Nova 都是静态测试：[ConfigManagerLoadTests](../../../Tests/Editor/ConfigManagerLoadTests.cs) 已验证 Shutdown 后晚到句柄释放。应复用现有测试设施，把新增便利层和关键弱证据路径补成可控时序测试。

**从真实工具生成到真实消费者读取。** TEngine [Luban 隔离执行](https://github.com/Alex-Rachel/TEngine/blob/a83415119d9129ac995a58ad48e37a4ef153419d/UnityProject/.codex/scripts/workflow_lib/domains.py#L67-L136) 复制当前工具/模板到空输出环境，对比全部产物清单与 Hash；[回归场景](https://github.com/Alex-Rachel/TEngine/blob/a83415119d9129ac995a58ad48e37a4ef153419d/UnityProject/.codex/scripts/workflow_lib/regression.py#L65-L103) 覆盖 standard/lazyload。但这尚不等于生成代码编译和 bytes 实际读取。Nova 已有隔离导出与收据，可补“实际安装包路径→正式示例表→导出→编译→按真实主键读取”的消费者回归，避免另一套导出器。

**用独立盲任务评价 Nova 协作体系。** TEngine 的 [scenarios](https://github.com/Alex-Rachel/TEngine/blob/a83415119d9129ac995a58ad48e37a4ef153419d/UnityProject/.codex/evals/scenarios.json) 与 [evaluation](https://github.com/Alex-Rachel/TEngine/blob/a83415119d9129ac995a58ad48e37a4ef153419d/UnityProject/.codex/evals/evaluation.md) 区分结构检查和模型真实行为，含不该触发的近似任务及失败停止点。Nova [test_nova_skills](../../Agents/Tools/tests/test_nova_skills.py) 已测 Catalog/投影/合同，但本轮范围未发现同类独立任务集。可首选 8–12 个典型任务，观察是否选对能力、止于授权和报告真实证据；不增加日常任务全量强制门禁。

## 六 采用顺序与投入判断

| 顺序 | 建议 | 相对成本与完成标准 |
|---|---|---|
| 第一批 | 页面本次打开作用域；图片绑定及其竞态测试 | 中等。直接从一个真实页面与可复用图标格验证，既有架构内减少重复代码和错误 |
| 第一批可独立评审 | 可等待的页面打开结果 | 中等。兼容 serialID 入口，所有成功/失败/关闭/取消路径只结算一次，状态与实际 OnOpen 一致 |
| 有痛点就试点 | 批量绑定助手、可选图集生产 | 中等。先核对 PSD2UGUI/既有工具，确认减少操作；生成物稳定且不破坏已有资源/代码 |
| 第二批 | 子控件/小列表、编译期诊断、多内容包加载、资源错误诊断 | 依使用场景决定。每项独立冻结契约和兼容面，不捆绑成大改造 |
| 持续补强 | 真实消费者回归和独立盲任务评估 | 从少量高价值场景开始，结果记录到实际执行，不用检查脚本存在代替通过 |
| 暂不投入 | 更换事件总线、重写资源池、重做启动链、复制一键打包系统、未经测量的 Update 微优化 | 本轮没有足以抵消迁移和维护成本的收益证据 |

成本是相对工程判断，未承诺工期。若复制具体 TEngine 代码，应保留其 [MIT 许可](https://github.com/Alex-Rachel/TEngine/blob/a83415119d9129ac995a58ad48e37a4ef153419d/LICENSE) 要求的声明；共有第三方部分仍按各自来源识别，不把整个工程都当成 TEngine 自写代码。

相关长期边界：[[ADR-001-component-manager-three-layer]]、[[ADR-042-assetmanager-load-api-all-return-handle]]、[[ADR-032-drop-novabehaviour-bridge]]。本条目不取代这些决策；后续实施先复核实际源码版本和已安装工具，建议不能自动授权架构迁移。

## 后续实现与兼容边界

上文保留固定基线的研究判断与采用建议。后续 Nova 在既有链路增加了下列**主动选择才启用**的入口；当前用法和限制由所属 Docs/源码维护，本表只作研究到实现的导航，不把研究建议或测试代码存在改写成已完成 Unity、消费工程、Bundle、Player、设备或性能验收。

| 研究方向 | 主动启用方式 | 后续定位与保留边界 |
| --- | --- | --- |
| 本次打开的工作归属 | 取得 `OpenScope` 并明确登记工作 | [UIViewScope](../../Docs/Runtime/Modules/UI/Definitions/UIViewScope.md)；不访问不创建，旧页手工生命周期不接管 |
| 可靠的打开完成结果 | 选择新的 `OpenUIViewAwaitAsync` 等门面 | [IUIOpenAwaiter](../../Docs/Runtime/Modules/UI/UIManager/Interfaces/IUIOpenAwaiter.md)；独立可选能力，旧 serialID 入口保留 |
| 图片替换和图集克隆归属 | 为目标创建 `SpriteBinding` 并调用换图 | [SpriteBinding](../../Docs/Runtime/Modules/Asset/SpriteBinding.md)；复用原 Handle，不创建第二套共享缓存或引用计数，原外部 Handle 不自动接管 |
| 指定内容包加载 | 调用 `Load*FromPackage*` 门面 | [IAssetPackageLoader](../../Docs/Runtime/Modules/Asset/AssetManager/Interfaces/IAssetPackageLoader.md)；旧接口不增加强制成员，默认包调用保留，上层未接入参数不自动获得选包 |
| 机械字段与精确控件绑定 | 明确的 AI/MCP 请求，选择生成或绑定 | [UIBindings](../../Docs/Editor/EditorUtil/EditorUtil.UIBindings.md)；普通 Prefab，生成/编译/重新绑定分阶段，业务代码与监听不覆盖；当前没有专用 Unity 生成按钮 |
| 批量规则生产主图集 | 配置 `Enabled=true`，明确选择生成 Step | [SpriteAtlasGeneration](../../Docs/Editor/EditorUtil/EditorUtil.SpriteAtlasGeneration.md)；新配置关闭、规则为空、Step 默认预演，旧 Batch 不自动插入，源图不删改 |

这些入口保留 Nova 的资源所有权与原配置/构建主链。“旧项目无需修改现有代码、配置和流程”针对继续旧写法；主动采用时，仍可能局部替换图片加载与收尾、登记工作、改用新调用，或为字段生成补 `partial`。可选不等于每项都有布尔开关，也不等于新旧拥有者可以同时释放同一资源。

实现触及既有核心链路：UI 的打开、关闭、回收、Shutdown、初始化异常记录及晚到清理，Asset 的五类加载通道共用核心，以及相应基类/Component 门面；并非只添加独立助手文件。旧 `IUIManager` / `IAssetManager` 未添加强制成员，新能力使用独立可选接口。启用边界与旧项目兼容必须沿这些真实调用点核对，不能仅凭默认关闭就推导零风险。

## 图集取舍的纠正

前期把“目录到图集”视为 Nova 工具缺口，沟通中又将维护单张图片的成本泛化到了原生文件夹图集，夸大了普遍收益。Unity 的 `Objects for Packing` 已支持文件夹关联，`Include in Build` 已支持随构建包含及运行时加载，见 [Unity 文件夹说明](https://docs.unity.cn/Manual/sprite/atlas/workflow/select-items-objects-packing-list.html)、[构建包含说明](https://docs.unity.cn/6000.0/Documentation/Manual/sprite/atlas/workflow/include-build.html)。没有同名 Nova 生成器，不等于缺少图集能力。

后续工具生成的仍是原生 SpriteAtlas；增量在多图集规则、排除、预演、流水线和输出恢复保护，不是新的打包或合批机制。当前把目录展开为具体 Sprite 列表，成员变化须重跑，比原生文件夹引用多一层维护。普通文件夹图集优先原生；明确需要批量规则时再选择增强。没有实测的效率或性能收益保持未知，不推荐旧项目普遍迁移；本次复盘只修正价值判断，不自动删除现有工具。

## 可复用的实施教训

- **先说明怎样用，再解释内部机制。** 字段生成请求需要落到具体页面、控件和字段；图片绑定需要展示打开、刷新、关闭的完整归属。真实入口、默认影响及局部接入改动由当前 [UIBindings](../../Docs/Editor/EditorUtil/EditorUtil.UIBindings.md#怎样要求生成字段)、[SpriteBinding](../../Docs/Runtime/Modules/Asset/SpriteBinding.md#页面与池化接入) 维护；共用表达与取舍规则归 [开发约束](../../Docs/Onboarding/DEVELOPMENT_CONSTRAINTS.md)，不在各入口复制长篇说明。
- **借便利层，不增加第二个所有者。** 页面作用域只结束登记工作，绑定助手只归还自己取得的句柄，已受理业务可由独立拥有者继续；页面隐藏、打开结果返回和底层资源清理是不同阶段。设计理由仍服从 [[ADR-042-assetmanager-load-api-all-return-handle]]，不以统一收尾改写旧项目的资源计数。
- **真实时序和真实消费者决定证据强度。** 从未 Awake 对象的原生销毁令牌问题，需要真实 Play 才能暴露；字段源码生成后还须真实编译、重读字段再绑定，图集生产后还须按目标验证打包与加载。运行前确认当前程序集、反例先证成立、准备失败也清场及退出后再查副作用，统一落在 [验证规范](../../Docs/Onboarding/VALIDATION.md#unity-测试前置与证据)。
- **恢复不能只恢复磁盘外观。** 新建 Unity 资产回滚还须注销 AssetDatabase 登记并验证缓存；未知回调改动不能收编为工具可覆盖内容，恢复状态未结束不能清走。领域保护继续由 [图集恢复实现](../../Scripts/Editor/EditorUtil/EditorUtil.SpriteAtlas/EditorUtil.SpriteAtlasGeneration.Transaction.cs) 与所属 Docs/Skill 维护。

影响面是框架维护者的选型、可选能力接入及验收方法。行为测试定位见 [UI 生命周期](../../../Tests/Editor/UIOptionalEnhancementTests.cs)、[图片绑定](../../../Tests/Editor/SpriteBindingTests.cs)、[图集恢复](../../../Tests/Editor/SpriteAtlasGenerationTests.cs) 和 [绑定 Action](../../../Tests/Editor/AgentActions/UIBindingsActionTests.cs)；测试代码存在不等于已执行通过。兼容设计、源码检查、Editor/真实 Play、外部消费项目、Bundle/Player/设备及性能各自报告，不在本条目保存历史通过计数或把局部证据升级为全链验收。
