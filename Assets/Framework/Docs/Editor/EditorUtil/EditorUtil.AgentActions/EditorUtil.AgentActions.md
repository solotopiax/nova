# EditorUtil.AgentActions

`EditorUtil.AgentActions` 是 Nova Project Skills 的受控 C# 执行内核。它只注册输入可类型化、写入集可说明、成功可自动验证的稳定项目组操作，不接受任意 C# 类型名、方法名、反射参数或代码字符串。

当前架构、能力边界与路线图见 [NovaProjectActions-Overall-Design.md](NovaProjectActions-Overall-Design.md)。

## Unity 对象身份

当前 Editor 的运行对象身份使用 `Object.GetEntityId()`；进入自有快照、排序键、集合、弱见证键与反查比较时，通过静态 `EntityId.ToULong` 无损保存为 `ulong`。DTO 中原有 instanceId/nativeId 等字段名仍指本次运行身份，不代表旧 32 位 InstanceID。序列化对象引用读取 `SerializedProperty.objectReferenceEntityIdValue`，选择集读取 `Selection.entityIds`；不调用 getter 加载引用，也不以 GetHashCode 或截断整数替代身份。持久资产的 GUID/localId、GlobalObjectId 与运行 EntityId 继续独立核对。

TMP、Facebook、Generate SceneVisibility、输入设置、NativePreview 等相关后验沿用完整 64 位身份；字体/atlas 反查仍只枚举当前已加载对象，不通过 ID 加载或重建对象。SceneVisibility 自有 DTO 标量白名单仅新增无符号整数的准确范围校验，负数、溢出、未知字段及未知 DTO 类型继续拒绝。托管对象的既有引用见证与业务 hash 不转换成原生 EntityId。

此兼容调整不更换已审 Unity 工具链的精确版本、私有源码 SHA、DLL/MVID、字段形状或平台白名单。新 Editor 可以编译身份 API，不表示旧私有实现合同已在新版本验收；源或版本不匹配的构建/生成分支仍保持 blocked/partial，不以重写版本字符串放行。

## 当前能力

`table.export` 的 `scope` 在校验、选择、Plan 冻结与 Execute 共用去除首尾空白、忽略大小写的规范值；例如 `" code "` 只导出代码，不会落入全量导出分支。

Registry 当前注册 37 个 Action，全部进入 MCP 显式白名单：

| Action ID | Operation | RequiredEvidence | MCP |
|---|---|---|---|
| `nova.project.upm.manage-latest` | Package | PackageResolution | 开放 |
| `nova.project.upm.uninstall-direct` | Package | PackageResolution | 开放 |
| `nova.project.config.validate-coordinate` | Inspect | Static | 开放 |
| `nova.project.config.patch-coordinate` | Ensure | Static | 开放 |
| `nova.project.config.inspect-panel` | Inspect | Static | 开放 |
| `nova.project.config.patch-panels` | Ensure | Static | 开放 |
| `nova.project.config.ensure-panel-references` | Ensure | Static | 开放 |
| `nova.project.config.normalize-master` | Ensure | Static | 开放 |
| `nova.project.settings.inspect-target` | Inspect | Static | 开放 |
| `nova.project.settings.patch-target` | Ensure | Static + Artifact | 开放 |
| `nova.project.pipify.inspect-settings` | Inspect | Static | 开放 |
| `nova.project.pipify.configure-batch` | Ensure | Static | 开放 |
| `nova.project.config.inspect-plugin-types` | Inspect | Static | 开放 |
| `nova.project.config.ensure-plugin-instances` | Ensure | Static | 开放 |
| `nova.project.config.inspect-bundle-collector` | Inspect | Static | 开放 |
| `nova.project.config.export-runtime` | Generate | Static + Artifact | 开放 |
| `nova.project.hotfix.refresh-game-dlls` | Generate | Compile + Artifact | 开放 |
| `nova.project.build.inspect-readiness` | Inspect | Static | 开放 |
| `nova.project.table.export` | Generate | Static + Artifact | 开放 |
| `nova.project.ui.export` | Generate | Compile + Artifact | 开放 |
| `nova.project.network.export` | Generate | Static + Artifact | 开放 |
| `nova.project.sound.export` | Generate | Static + Artifact | 开放 |
| `nova.project.vibration.export` | Generate | Static + Artifact | 开放 |
| `nova.project.localization.export` | Generate | Static + Artifact | 开放 |
| `nova.project.android.resolve-dependencies` | Generate | PackageResolution + Artifact | 开放 |
| `nova.project.hotfix.generate-artifacts` | Generate | Compile + Artifact | 开放 |
| `nova.project.bundle.build-asset` | Build | Artifact | 开放 |
| `nova.project.bundle.build-raw-file` | Build | Artifact | 开放 |
| `nova.project.player.build` | Build | Artifact | 开放 |
| `nova.project.pipify.run-batch` | Build | Static（Runner 终态） | 开放 |
| `nova.project.config.generate-resource-keys` | Generate | Compile + Artifact | 开放 |
| `nova.project.settings.patch-build-defines` | Ensure | Static + Artifact（changed 分支还需 Compile） | 开放 |
| `nova.project.persistence.inspect-data` | Inspect | Static | 开放 |
| `nova.project.persistence.migrate-encryption` | Ensure | Static + Artifact | 开放 |
| `nova.project.persistence.recover-maintenance` | Ensure | Static + Artifact | 开放 |
| `nova.project.tracks.generate-registry` | Generate | Static + Artifact | 开放 |
| `nova.project.iap.upload-product-catalog` | Delivery | Static + Artifact + External | 开放；当前目录应用证据未闭合，结果最高 partial |

Action 的 Request Schema、effects、locks、contractMajor、idempotency 和 confirmation 以运行中的 Registry Descriptor 为准，不维护第二份静态 Action Catalog。

`nova.project.player.build` 当前支持活动目标为 Standalone 桌面平台或 WebGL。Plan 冻结当前 Active BuildTarget、Build Settings 启用场景、ProjectSettings、DevelopmentBuild、BuildMode 与精确输出路径；Action 不替调用方切换平台。WebGL 产物按目录验证，Android 与 iOS 在平台专属导出、签名选项进入冻结契约前继续 fail-closed。

Player 的 WebGL 属性回调按精确方法和当前程序集指纹核对：`Facebook.Unity.Editor.XCodePostProcess.PostProcessBuild_iOS` 与 `Firebase.Crashlytics.Editor.iOSPostBuild.OnPostprocessBuild` 按目标参数立即返回；两个 Android 场景方法另要求真实活动域已经是 WebGL。Firebase Xcode 跳过分支冻结准确 Logger、级别、目标、空文件名/外部委托和命令行摘要，声明可能产生的固定 Console/stdout 文本。Facebook 同类型的 `OnPostProcessBuild` 在平台判断前读取设置，按独立合同允许官方回调将空 singleton 自然绑定到唯一既有 Resources 配置；不预调用 getter、创建配置或执行 SDK 业务。未知回调、未审动态图集写入及 Android/iOS 分支继续阻断。捕获拟构建目标为 WebGL 的回调不能替代切换活动平台或实际 Player 构建验收。

三条官方构建后 reset 分别归零 Crashlytics `Generated`、EDM `HasWarned` 与 `scenesProcessed`。Plan 展示 typed 原值，Execute 与作用域写前逐项复核；合同 Hash 只规整这三个明示标量，保留全部字段、模块及其他输入。正常链返回后只读采集实际 after，schema 4 Receipt 与 Verify 要求同一字段身份及零值；失败保留实际材料，不主动设置字段或恢复旧计数。SRP 三个空分支只接受已审 Android/WebGL 成对编译身份，正式包源码与完整 roster/backing-state 门继续有效。

Player schema 4 另保存 TMP 与 Facebook 的真实 before/after/error。TMP 允许合法加载已有字体的已审生命周期和当前空表、单个可读 1×1 Alpha8 零 atlas 的官方清理，明确保留 FontEngine 和管理缓存的正常内存效果；字体/atlas 可以变 dirty，但不保存或清除原 dirty，文件、像素和外部事件仍受完整后验保护。Facebook 必须保持原配置、候选集、完整摘要及准确对象归属，缺失资源或出现其他绑定保持 partial。TMP 对明确 marker 且原 before 实际已加载的非清理字体，只允许源预测 hash 与精确 map/weak 单调登记；原 winner/live 槽和完整字体/源/atlas 保护不变，无逐行材料的旧回执不升级。细节仅在对应分支阅读 [TMP 合同](../../PlayerTmpFontBuildContract.md) 和 [Facebook 合同](../../PlayerFacebookBuildContract.md)；定向测试不代表实际 Player 已构建。

WebGL 的 `UnityEditor.Build.Rendering.EnsureSinglePipelineOnBuild` 仅在活动平台的完整管线资产列表全部为托管 null、Quality 两个原共享容器均有效且为空时准入。Plan 使用局部列表读取管线资产；官方回调会填充共享缓存，构建后仅将原引用上的精确全 null 完成态恢复为空。容器替换、数量不符或其他内容保留并返回 `partial`，不清理来源不明的缓存。`UnityEditor.Rendering.Analytics.VolumeProfileOverridesAnalytic` 仅在原厂完整过滤条件得到空 GUID 集时准入；两个统计回调保留原 Analytics 配置：disabled 沿原早退；enabled 时 SRP 仅接受当前/默认 pipeline 与实际 global settings 都为 null、准确私有空列表池可证明的正常借还，Adaptive 仅接受准确空 metadata 表且没有请求，不调用或清空 provider。已审 metadata 首次 backing 读取可以自然初始化空容器并注册一次既有 Play 状态订阅，计划明确声明此正常效果；已有表非空仍拒绝。回执只记录实际缓存/池/报告范围，不猜测已缓存场景重跑次数或 native 送达。见 [SRP / Adaptive 空分支合同](../../PlayerEmptyUsageBuildContract.md)。这些限定分支不解除其他构建阻塞，也不代表 Player 已构建成功。


CoreModule 的 AudioRandom、UI 与 Insights 三项 usage 分别冻结原 Analytics/音频/诊断配置和真实 BuildReport payload 范围；Insights 只允许官方 `-createproject` 布尔与准确纯 predicate 的正常缓存变化。`BuildEventsHandlerPostProcess` 只接纳当前自然注册的准确 WebGL 原模块与空派生表，其属性不实现权限接口而自然返回 null；原窗口名单/默认2D与完整真实报告的准确 role 包映射受保护，三个 SceneView 统计字段可以由官方正常更新。未知 analyticSent 订阅、provider、对象替换或输入漂移继续阻断/partial。正常开发构建中的既有 native usage 注册、限流/排队及诊断效果被明确声明，回执不宣称远端接收、所有队列结束或后台 Task 退出，也不重放发送。见 [三项 Core usage](../../PlayerBuiltinUsageBuildContract.md) 与 [WebGL BuildEvents](../../PlayerBuildEventsContract.md)。

ProjectAuditor 报告提供类型查找按实际 `Type` 去重 UnityEditor 的转发入口；同名但不同的实际类型仍拒绝。该查询只读已加载程序集元数据，不初始化类型或执行报告回调。

Android 合同中的反射助手也只在各自准入范围内成立：`AndroidVersionHandlerReadOnlyContract.FindLoadedType` 从当前已加载程序集查找，按实际 `Type` 去重；类型未找到或仍有多个不同实际类型时拒绝，各程序集 `GetType` 的异常直接传播。`NovaAndroidSdkLeafBuildContract.ResolveConfigurationType` 还要求名称属于固定配置角色名单，并要求自然已加载的唯一匹配；它不加载替代程序集、创建 SDK 实例或调用业务 getter。

`AndroidResolvePeripheralRefreshContracts.ReadInstance` 只读实际对象本层声明的非静态字段（`DeclaredOnly`），不沿继承链查找、不读取属性；null 对象、缺字段、静态字段或反射错误会传播，本助手没有自己的模块身份校验或吞错分支，调用方必须先验证类型。元数据查找不等于后续 `FieldInfo.GetValue` 不会触发已审静态初始化，更不等于 Android SDK、Manifest 或设备行为已经验收。

WebGL 的另十四项包回调由 `PlayerPackageCallbacksContract` 按准确 DLL/包源码与属性方法单独登记。EDM 只接非 Android 的自然 null resolver/零计数；ProBuilder 还要求已初始化 Settings、无保存事件、完整 Player 依赖及全局 loaded 对象均无 ProBuilder 内容；IAP 只允许既已满足目标的插件兼容性 setter 与正常配置缓存替换，原文件/meta 保持。Timeline 三阶段只接完整依赖与 loaded 对象均无 TimelineAsset、全套空统计哨兵一致的分支，官方 pre 可替换同结构统计，不发 Timeline payload。Shader 保留内部 stripper、事件和无 SRP 门，按当前 shadows 接纳官方 feature list 重建；global null/export false/reporter null 约束正规 Empty reporter 的创建与清除，不写两份 Shader JSON 或启用 profiler。ProjectAuditor 只接实际 Pref/EditorPrefs 关闭且规则包未安装、无旧延迟 Audit；其 Shader 回调可向准确字典的原 List 追加固定 WebGL DTO，旧 key/list 与完整顺序前缀不得变化。UGUI 使用正式 BuildReport GUID/type 与实际四计数缓存，允许既有配置的 native usage 尝试/诊断，不声称发送或远端送达。执行前完整值再核；回执另存同域 `packageCallbacksBefore/After/Report/Error`，缺失、漂移或后验不足保持 partial，不重放、不清缓存或补字段。详见现有 [BuildEvents](../../PlayerBuildEventsContract.md) 与 [空分支合同](../../PlayerEmptyUsageBuildContract.md)。

Player 输入捕获仅排除由准确 TrueType 导入器与已加载主 Font 的 `m_Texture` 直接引用、完整零缓存形状共同证明的非保存纹理缓存，不调用 Font/Material getter、不预加载或清 dirty；其余原生设置与所有未知 dirty 对象仍按完整正文冻结。规则与保护边界见 [Bundle 脏资产门](../EditorUtil.BundleBuilder/EditorUtil.BundleBuilder.md)。

Generate 的自有 `HybridClrStripGradleScope` 在独立 Player 中只接受准确当前源码与真实无活动租约；三个回调自然在读取设置/报告/路径前返回。Player 不调用 Begin、Dispose 或任何回调去制造 inactive；活动 Generate 租约不能被独立 Player 接管。

`nova.project.config.export-runtime` 的请求仍显式携带 `platform / channel / developMode`，但 `platform` 必须等于 Plan 时 Unity 当前 Active BuildTarget 映射的 Nova `PlatformType`。未映射为 Android / iOS / WebGL、请求不一致，或 Execute 前 Active BuildTarget 漂移，都会以 `blocked` 收口并要求先切换 BuildTarget 后重新 Plan；该限制不改变底层 `EditorUtil.Config.Exporter.Export` 的显式平台 API。

Config 的有效执行坐标要求平台和渠道为非 `None` 的精确枚举名称。共享 Generate 请求解析仍接受 `ChannelType.None`，`config.export-runtime` 已在自身请求边界拒绝该值；校验、插件补齐和构建预检须分别按其当前实现判断。

`nova.project.bundle.build-asset` 与 `nova.project.bundle.build-raw-file` 的 `encryptionPolicy` 必填，只接受 `none / offset / xor / aes-memory / aes-stream`；可选 `manifestEncryptionPolicy` 只接受 `none / aes / xor`，省略或 `null` 使用 `none`，因此既有明文请求继续有效。两组选择独立，固定映射见 [BundleBuilder 资源加密方案](../EditorUtil.BundleBuilder/EditorUtil.BundleBuilder.md#受控-bundle-action)。Plan 冻结并展示两项逻辑值和实际三个正式类型名，Execute 只使用冻结类型；请求不接受 C# 类型名、旧通用类名或自定义加解密器入口，AES/XOR 清单固定配对。

`nova.project.config.ensure-plugin-instances` 在写盘前先拒绝已有的维度矩阵不一致，并在完整内存副本上预演本次 Ensure；若补行、补实例或启用类型会制造新的未勾选维度冲突，或启用后仍有任一 Platform × Channel × DevelopMode 坐标缺少该类型实例，则以 `blocked` 收口且不修改资产。此时应改用 `matrix` 范围补齐，或先在 ConfigWindow 完成矩阵修复/维度调整，再重新 Plan。

`nova.project.config.patch-coordinate` 原请求对明确坐标的 Namespace 与 AppConfigs 五个固定字符串字段执行批量预演、同组广播和共享保存，设计态仍可编辑其他目标平台。同 Action 的互斥 `operation=coordinate-switch + coordinate` 只保存当前渠道/开发模式，Platform 必须匹配并冻结 Active BuildTarget，不隐式切 Unity 平台、不修改其他配置。两分支只落盘 ConfigMaster；Runtime 导出另行执行。旧请求/Receipt 兼容，精确请求、空值、写集、草稿、失败与只读验证见 [设计态共享保存](../EditorUtil.Config/EditorUtil.Config.DesignTimeEditing.md)。

Android 刷新合同另保存有限 UI Toolkit 角色的真实字段归属，识别既存面板持有的四类引擎渲染资源（含动态 Atlas 页直接持有的复制材质）正常重建；原始对象行完整保留，其余对象、输入纹理、资产、dirty、场景与选择仍严格保护。旧 Snapshot/Receipt 没有角色字段时保持原摘要和原严格比较，不事后升级旧 partial；具体维护边界见 [AndroidResolver](../EditorUtil.AndroidResolver/EditorUtil.AndroidResolver.md)。

## IAP 商品目录一次提交

`nova.project.iap.upload-product-catalog` 的请求仅有 `runtimeGuid`（现存 `ConfigRuntimeSO` 资产的 N 格式 GUID）与 `developMode`（精确 `Debug` / `Release`）。Action 只接受持久化激活 Master 的已保存显式 `ExportTarget`，拒绝 dirty、导出坐标/模式不一致及非规范资产。它不导出、不保存、不修复 `Globals.json` 的 pathHint，也不接受任意 URL、Header、商品数组、Key/IV、Body 或代码。缺可选 IAP 包为 `not_applicable`，前置或身份不满足为 `blocked`。

Plan 冻结 Master/Runtime 两对象内存摘要、Master/Runtime及各自meta、`ProjectSettings/Nova/Globals.json`、`ProjectSettings/ProjectSettings.asset` 六文件、活动目标、环境/应用身份/客户端版本、完整规范目录和最终请求，以及固定桥/代码身份。Framework 不强依赖 IAP 程序集；只通过已经核验的可选 Editor 桥 `IAPProductCatalogActionBridge.Prepare/Describe/SubmitAsync` 复用 IAP 的原投影、JCS、App AES 和 HTTP 实现。Body 与凭据仅在原 Prepared 内存中，公开 Plan/Receipt 只给有限元数据与摘要。

真实发送须已有针对目标、Debug/Release 环境、客户端版本、目录摘要和至多一次请求的精确授权，再绑定原一次性 Plan 确认。Execute 在全部输入重核后，于 `Library/Nova/AgentActions/IapCatalogUploads/<attemptId>.json` 独占写入并读回 `dispatching` 正式意图；保存失败不发送，发送后只以原意图转成 `completed` 结果。同 Prepared 任一提交尝试后不能再用；Inspector/Pipify/Action 共用忙碌门，不排队或重试。

2xx 的 `http-accepted` 只证明 HTTP 接受；当前协议没有权威后台应用响应或查询契约，Execute/Verify 最高 `partial`。精确 HTTP409 且 `code=CATALOG_VERSION_CONFLICT` 表示新目录未应用，不能自动改版本或重发。其他错误、取消或仅剩意图均保留实际未知边界；回执只含发送事实、状态码、响应长度/SHA、有限分类和时间，不含原响应、异常正文或请求Body。

Verify 使用原 Core recovery token，只读原 attempt 和指纹，不 Prepare、Submit 或查询后台；重载后原计划失效，不能恢复 Execute。原记录缺失/损坏、只有意图或结果未保存时保持 `partial`，不得推定远端没写入。正式意图/结果及 Operation 保留为证据。旧 Inspector 与 `iap.product_catalog.upload` Step 的精确409 Warning兼容不变，通用Batch未自动获得目录预冻结，Batch终态不证明目录应用。该分支不创建订单、不验单、不发货，也不改变原IAP业务任务的验收目标。参数、协议和调用边界见既有 `nova-project-onboard-sdk-kit` 的[按需目录引用](../../../../Agents/Skills/nova-project-onboard-sdk-kit/references/product-catalog-upload.md)及已解析 IAP 包 `Nova/Doc/IAPProductCatalogProtocol.md`。

## 代码组织

```text
EditorUtil.AgentActions/
├── Definitions/
│   ├── AgentActionContracts.cs
│   └── AgentActionModels.cs
├── Core/
│   ├── AgentActionHandler.cs
│   ├── AgentActionLockManager.cs
│   ├── AgentActionOperationStore.cs
│   ├── AgentActionPlanStore.cs
│   └── AgentActionRuntime.cs
├── Handlers/
│   ├── Build/
│   ├── Ensure/
│   ├── Generate/
│   ├── Inspect/
│   └── Package/
├── EditorUtil.AgentActions.Registry.cs
└── EditorUtil.AgentActions.Dispatcher.cs
```

Registry 只扫描 `NovaFramework.Editor` 程序集中的 `IAgentActionHandler`；消费项目程序集不能注册可执行 Action。每个 Handler 只负责 DTO、输入冻结、领域 API 调用、Receipt 和证据，不复制领域算法，也不依赖 EditorWindow。

## Descriptor 契约

每个 Handler 通过 `AgentActionAttribute` 声明：

- 稳定 ID 与 Domain；
- `Inspect / Ensure / Generate / Build / Package / RuntimeProbe / Delivery`；
- Workspace、Unity、External、BuildArtifact、Destructive、Credential 副作用；
- `ReadOnly / EnsureState / ReplaceGeneratedOutput / CreateIfAbsent / SubmitOnce`；
- Static、Compile、PackageResolution、Artifact、Play、Device、External 证据；
- confirmation、stable-editor、reload semantics、locks 与 contractMajor。

写入、Build 与 Package Action 还应显式声明 `RequiresEditMode`。Dispatcher 会在 Plan 入口、异步 Plan 保存前和 Execute 写入前分别检查 Play Mode 状态；该门与 `RequiresStableEditor` 独立，避免未来 `RuntimeProbe` 被错误禁止在 Play Mode 运行。

Registry 在快照中发布描述和 Handler；每次 Rebuild 增加 generation。重复 ID、非法 ID、无证据、写入却无锁/确认、ReadOnly 却声明写入、请求类型非法或 Schema 为空的 Handler 不进入可执行 Registry。Registry 这里只检查 Schema 非空，完整 Schema 合法性由 MCP Gateway 再核；不能把注册通过当作 Schema 已解析。快照中的 DTO、Handler 和成员引用也不是递归深拷贝。Registry 存在任何 issue 时 MCP 整体 fail-closed。

扫描和 Handler 构造只限当前 Framework Editor 程序集，但 `Activator.CreateInstance` 仍会运行构造函数；构造失败记 issue，后续属性 getter 或反射失败并非全部被此 catch 包住。静态运行时以初始化线程登记主线程，生产依赖 Unity 的 `InitializeOnLoad` 正常加载。`ConfigureForTests` 逐项替换服务，异常可能留下部分替换，也不释放旧 Store，不是生产重置入口。

## 请求 Schema

每个 Action 使用独立 `[Serializable]` DTO。Core 从 DTO 生成 `RequestSchemaJson`，并由同一 `AgentActionRequestContract<T>` 执行解析：

- 最大 64 KiB、最大深度 64；
- 根必须是 JSON object；
- 拒绝重复字段和未知字段；
- 拒绝缺失必填字段和 token 类型错误；
- 禁止 Newtonsoft `TypeNameHandling`；
- Handler 再执行领域语义校验。

MCP 只做 transport 上限与 Descriptor 顶层字段预检，不能复制一套 Action 专用 Schema。

空白请求先按 `{}` 解析，仍须满足实际 DTO 必填项；引用类型或 nullable 的显式 null 不等于领域允许为空。基础 token 检查将 enum 和 char 归为字符串，不替代枚举成员、字符长度或业务范围校验。`ToObject` 可以运行 DTO 构造与 setter；这里只把 `JsonException` 转成解析失败，其他异常仍可能传播。执行上下文只保存确认字符串和取消令牌，本身不验证人类授权、不检查取消。

## 公共入口

```csharp
AgentActionDescriptor Describe(string actionId)
IReadOnlyList<AgentActionDescriptor> GetAll()

Task<AgentActionPlan> PlanAsync(
    string actionId,
    string requestJson,
    CancellationToken cancellationToken)

Task<AgentActionResult> ExecuteAsync(
    string planId,
    string confirmationToken,
    CancellationToken cancellationToken)

Task<AgentActionResult> ExecuteAsync(
    string expectedActionId,
    string planId,
    string confirmationToken,
    CancellationToken cancellationToken)

Task<AgentActionResult> VerifyAsync(
    string actionId,
    string receiptOrRecoveryToken,
    CancellationToken cancellationToken)

Task<AgentActionResult> RunAsync(
    string actionId,
    string requestJson,
    CancellationToken cancellationToken)
```

MCP 必须使用含 `expectedActionId` 的 Execute 重载。Core 原子消费 Plan 后、任何领域写入前核对 Action ID，避免从其他入口产生的错误计划跨 Action 执行。三参数 Execute 保留给同程序集/受信直接调用；`RunAsync` 只允许无需确认的 Action，MCP 不暴露它。

## 慢计划的传输恢复

当前 Nova MCP 网关在同一个 `operation=plan` 内提供可选 `plan_mode=start/status/forget`；未提供时仍同步等待原 Plan。Agent 预先生成并保留 N 格式 GUID `request_id`，登记后下一次 Editor update 仍调用原 Provider/Core Plan，按同号查询完整脱敏响应。外层 `completed` 仅表示传输结果保存，须继续检查原响应是否 `ready`；`plan_candidate` 也只标示同域与期限候选，执行仍校验 Registry generation、输入与一次性消费。域重载、过期或请求冲突不会恢复或自动重跑。

收件箱仅保存 SessionState 中的请求摘要和脱敏响应；它不是 PlanStore、OperationStore 或另一套领域执行器。一次活动计划、16 条记录、一小时 TTL 与每响应 2 MiB 上限均明确拒绝超限；过期墓碑需显式忘记，忘记不删除领域状态。完整字段、故障及清场协议以已安装 `com.solotopia.nova.framework.mcp/Nova/Docs/INDEX.md` 为准。此能力解决慢 Plan 断线后取回，不解除未知回调、字体或 Android/iOS 构建门。

## PlanStore

- 只保存 ready 计划的可执行 HandlerState；
- 默认容量 256；
- 默认 TTL 30 分钟；
- 添加、过期清理、移除和 TryTake 在锁内完成；
- TryTake 原子单次消费；确认失败或执行失败也不能复用；
- 超限不驱逐仍有效计划；
- domain reload 后内存计划全部失效；
- 可释放 HandlerState 在过期、移除或执行结束时 Dispose。

## OperationStore 与恢复

ready Plan 会先在以下目录建立最小持久化 Operation：

```text
Library/Nova/AgentActions/Operations/
```

记录包含 operationId、Action/contract、Registry generation、请求 SHA-256、write set、状态与领域 Receipt；不保存原始请求。文件以同目录临时文件原子替换，单条最大 256 KiB，路径只接受 32 位十六进制 operationId。

Store 是过程记录，不是领域状态机、审批签名或永久权限：加载时绑定 Action/contract/Operation，未额外据 generation、TTL 或终态认证领域事实；内部状态更新也不要求严格前一阶段。公开 token 读取有容量与结构门，内部按 ID 读取采用另一条反序列化路径，不能把公开入口的全部检查推给内部助手。临时写入清理失败会被吞掉，可能留下本操作 `.tmp`；时钟、ID 及调用前字段更新的异常也不全在文件写入 catch 内。

`recovery_token` 只能加载当前 Action/contract 的 Receipt 并进入 Verify。它不能恢复 HandlerState、不能判断 Execute 应否继续、不能重放写操作。若 Execute 在 domain reload 前已提交但未回传，调用方也只能等待稳定后 Verify。

Plan 和 Verify 会写/更新这条 `Library` 基础设施记录。“只读”首先表示不执行所计划的领域提交，不保证物理零写：例如持久化维护可创建长期 idle 锁载体，SQLite 值/Privacy 检查会取得真实事务并可能改变 WAL/SHM；构建计划中的有限资源加载也有独立正常内存效果。具体范围由领域合同限定。

## 调度顺序

### Plan

1. 主线程与 stable-editor 门；
2. Registry lookup 与严格请求解析；
3. 取得 Descriptor locks；
4. Handler 只读冻结领域计划；
5. 核对 Registry generation；
6. 写入 PlanStore；
7. 建立 OperationStore；失败则移除可执行计划并 fail-closed。

Registry lookup、请求解析与取得租约发生在领域 `try` 之前，相关反射、枚举和服务异常可能直接传播。租约只在当前进程内按去空、trim、Ordinal 去重排序后的资源名排他，不是文件锁或跨 Editor 互斥。Plan 不是对所有依赖对象任意 getter、构造与惰性初始化都无副作用的保证。

### Execute

1. 原子 TryTake；
2. 核对 expectedActionId、Registry generation、confirmation、Editor 状态；
3. 取得 locks；
4. 持久化 `executing`；
5. 调用一次 Handler Execute；
6. 统一包裹 `actionId + contractMajor + payload` Receipt；
7. 按 RequiredEvidence 归一化状态并持久化结果；
8. 领域执行期间的取消或异常不重放；声明含 `WorkspaceWrite / UnityWrite / ExternalWrite / BuildArtifact / Destructive` 时返回 `partial`，否则返回 `blocked`。`TryTake` 在执行 `try` 外，时钟或过期状态清理异常可传播；最外层 `finally` 中的 HandlerState `Dispose` 异常也可传播，不能把这些分支保证为结果 DTO。

这里按描述符声明的副作用分类，`Credential` 单独不在 `CreateExecutionFailure` 的写入集合内，不能据 `blocked` 倒推从未读取凭据或没有外部效果。失败结果本身不回滚、不重试，也不恢复已消费的计划。PlanStore 的容量拒绝、移除和过期清理只对实现 `IDisposable` 的状态调用 `Dispose`，异常继续传播；不把任意状态对象当作 Unity 资产销毁。资源租约只释放自己的登记资源，不删除领域产物；同一个租约的并发释放不能从正常串行重复释放的幂等性推导。

### Verify

1. 接受 Receipt envelope 或 recovery token；
2. 核对 Action 与 contractMajor；
3. stable-editor 与 locks；
4. Handler 只读验证领域状态；
5. 证据不足时把 success 降为 partial；
6. 可更新 Operation 最近验证状态。

Verify 不重放 Execute、不恢复 Plan；领域 catch 将取消和异常转成 `partial`，入口 Registry/token/locks 与外层租约释放仍有可传播出口。`NormalizeResult` 只检查合法状态和声明证据是否足够，不重新证明领域事实；格式正确、摘要自洽或 `EvidenceKinds` 已填也不能代替实际后验。

## MCP 调用桥

Framework 的必需 Editor 配套包 `com.solotopia.nova.framework.mcp` 当前默认提供 Unity MCP 自定义 Tool：

```text
nova_project_action
```

调用：

1. `describe`：列出当前开放 Action 或返回一个 Descriptor/Request Schema；
2. `plan`：传 `action_id + request`；
3. `execute`：必须传 `action_id + plan_id`；确认型 Action 再传 `confirmation_token=plan_id`；
4. `verify`：参数名仍为 `receipt`，其值优先使用 Core 返回的 `recovery_token`；不要把内部领域 Receipt 当作新的执行授权。

当前显式 ExposurePolicy：

```text
nova.project.upm.manage-latest
nova.project.upm.uninstall-direct
nova.project.config.validate-coordinate
nova.project.config.patch-coordinate
nova.project.config.inspect-panel
nova.project.config.patch-panels
nova.project.config.ensure-panel-references
nova.project.config.normalize-master
nova.project.settings.inspect-target
nova.project.settings.patch-target
nova.project.pipify.inspect-settings
nova.project.pipify.configure-batch
nova.project.config.inspect-plugin-types
nova.project.config.ensure-plugin-instances
nova.project.config.inspect-bundle-collector
nova.project.config.export-runtime
nova.project.hotfix.refresh-game-dlls
nova.project.hotfix.generate-artifacts
nova.project.build.inspect-readiness
nova.project.bundle.build-asset
nova.project.bundle.build-raw-file
nova.project.player.build
nova.project.android.resolve-dependencies
nova.project.table.export
nova.project.ui.export
nova.project.network.export
nova.project.sound.export
nova.project.vibration.export
nova.project.localization.export
nova.project.pipify.run-batch
nova.project.config.generate-resource-keys
nova.project.settings.patch-build-defines
nova.project.persistence.inspect-data
nova.project.persistence.migrate-encryption
nova.project.persistence.recover-maintenance
nova.project.tracks.generate-registry
nova.project.iap.upload-product-catalog
```

`GenerateActionCommon.TryParseCoordinate` 当前接受 `ChannelType.None`；这只是共享请求解析行为。Config 导出、校验与插件补齐 Handler 各自在请求边界拒绝该哨兵，因此不能把共享解析通过当作配置坐标可执行。准确边界以具体 Handler 到领域 API 的完整链为准。

Gateway 要求 Registry 的全部 Action 与显式 `ExposurePolicy` 集合完全一致；任一遗漏、陈旧 ID、非法 Schema 或 Registry issue 都会使 Action 面整体 fail-closed。`Destructive / ExternalWrite / Credential / Delivery` 不再作为隐藏已注册 Action 的条件，但仍必须准确声明，并继续受一次性确认、资源锁、Receipt、脱敏和 Verify 约束。

`nova.project.pipify.run-batch` 只接受当前活动 `PipifySettings` 的 GUID 与精确 Batch 名称。Plan 冻结设置文件 SHA-256、活动 BuildTarget 和有序 Step/参数；Execute 登记异步任务并在真实运行期重新持有资源锁；Job 状态原子写入 `Library/Nova/Pipify/Jobs/`。domain reload 前未结束的 Job 只会变为 `Interrupted`，不会恢复或重放。其 `success` 仅证明 Runner 顺序完成且无异常，不替代具体 Player、Bundle、CDN、浏览器或设备证据。

这里公开 Step 快照按 `PipifyPassword` 标记脱敏，不能保证未标记的任意自定义参数无秘密；实际 Batch 仍是设置中的引用，不是整个工程、Master、场景及全部未保存内存的深冻结。Core 取消令牌用于登记前检查，不自动取消已登记的后台 Batch；Runner 的步骤、Persist/Dispose 次序和重载失败仍按 [Pipify](../EditorUtil.Pipify/EditorUtil.Pipify.md) 的实际链处理。

Tool 只返回安全 DTO；项目根路径替换为 `<project-root>`，敏感字段打码，URL 去除认证、query 和 fragment。包依赖为 `Framework -> Nova MCP -> Unity MCP`；程序集依赖保持单向：`NovaFramework.Mcp.Editor` 只定义中立 SPI 和 Gateway，独立 `NovaFramework.Mcp.UnityMcp.Editor` 适配默认 Provider，Framework 在 domain load 注册唯一 Action Provider。消费端 Skill 不会逐个成为 MCP Tool；默认 Adapter 只注册 `nova_project_action`，再由 live `describe` 返回当前开放 Action。

## 状态与证据

Bundle 两个 Handler 复用 `BuildActionCommon` 冻结实际管线输出与缓存/临时路径；AssetBundle 分支另冻结 SBP 回调、场景并检查持久化脏资产，RawFile 不继承这套 SBP/全局 dirty 门。未知 SBP 回调、已有 SBP 临时目录或不符合独立 RawFile Package 的采集配置分别在其适用分支写前阻断。Bundle Receipt 增加共享写集和执行后辅助产物；Verify 校验辅助精确集合、正式 YooAsset 报告身份和报告引用 Bundle。清全局缓存仅声明边界，不提供缓存完整性证据。旧 Bundle Receipt 不继承新增证据，Player 原 Receipt 不受该 Bundle 门约束。

持久化脏资产检查只排除不由普通保存写回的非原生 Shader/MonoScript，以及三个精确内建资源路径中带 DontSaveInEditor 的非原生对象。原生主/子资产始终受检查；未知非原生 Prefab 和组件仍阻断。不会按 Packages/Library 前缀或 DontSave 标志整体豁免，也不调用全局 SaveAssets。

AssetBundle 冻结已保存场景 setup、有效 Collector 根的递归依赖及实物/meta/关键 ProjectSettings，执行前后核对 GUID、依赖 Hash 与 SHA-256；失败 Receipt 也只读报告源资产和场景是否保持。已审引擎/包 SBP 回调采用精确实现与分支条件：Analytics 外发及 provider 下游已核、Shader 内部实现/事件已核、ProBuilder 无对象且无输入脚本依赖。未知内部 stripper、委托或 SDK 副作用仍逐项阻断，SBP 放行不适用于 Player。

Firebase SBP 合同复用同一 `BuildActionCommon` 的 typed 输入与一次执行作用域：只读 Plan 不触发 Measurement 初始化，精确冻结四资源和 metadata/manifest 行闭包，Execute 记录正常回调的日志、文件、标签与 `Generated` 前后状态。可写资源/meta 从“必须保持”的依赖 Hash 中分离，仍由专属语义与原字节保持规则验证；不会把允许变化当输入漂移或把原合法文件升级为本次成功。失败及取消也带恢复回执并解除日志订阅。IAP 原配置与 importer 已满足值分支沿同一回调快照复用；更宽的 Player 或 SDK 分支不继承此 SBP 契约。具体文件和门禁见 [Bundle Action](../EditorUtil.BundleBuilder/EditorUtil.BundleBuilder.md#受控-bundle-action)。

AssetBundle 的内部恢复 Payload 通过 `BuildArtifactReceiptCodec` 完整有界压缩，以固定版本、UTF-8 原文长度、SHA-256 和 gzip 校验保持证据；公共信封与 `AgentActionOperationStore` 的 256 KiB 上限不变。Verify 兼容旧明文回执，拒绝损坏、重复字段、超限或嵌套封装，不重放构建。对外 `DataJson` 仍保留完整可读领域对象。Firebase 另冻结 VersionHandler 日志的输出标志和级别，非空日志文件名或外部委托在写前阻断。

SBP 的 EDM 场景回调在非 Android 且 `gradleResolverInstance` 为空时沿已核自然早退分支。Android 另冻结非空完整依赖图、已满足的解析状态/产物、工具、调度、日志及设置：首次正常场景必须记录本次 `starting → checking → skipped → succeeded` 和计数增长，后续场景只接纳无解析事件的自然计数增长。固定 DLL 的公开 `Logger.LogMessage` 尾部事件在 Console 过滤后仍发出；作用域只临时订阅自身观察者，退订核对原空委托后才重采集，保持日志级别与 debug 开关，双通道不重复计数。正规 Jetifier setter 的 Gvh XML 同字节写回及单键 EditorPrefs True 物化明确列入计划，其余项目字典、偏好与 Firebase 保护保持。Verify 只读，不调用 ReadAll/Resolve 或补造日志；缓存命中可走独立证据分支：只有本次写出的精确报告和完整 SBP trace、场景 GUID 与冻结依赖闭包、全部场景/写操作缓存标记和计数一致，且场景计数、事件、EDM/Firebase 完整状态和写入集合均未变化，才接受真实未重跑场景回调的缓存结果。缺少 trace、混合未缓存场景或域身份改变保持 partial，不升级旧 Receipt。初始与最终各做一次完整 Version Handler 只读采集，中间仅双读绑定实际 DLL/MVID、域、owner/scheduler 身份及原调度不变量的廉价只读 pending 探针。首完整采集与探针绑定后启动唯一固定十秒的自然 Editor update 等待预算，至少观察一次真实 update；探针完成或到期后只做一次最终完整后验，采集可越过截止点但必须完整匹配且自然完成，仍 pending 保留 partial。回执分别保留真实 update 与成功探针计数，二者须相等且只含首末两份完整快照；旧无探针字段回执仍按每次 update 一份完整快照的原规则读取。未知探针或稳定输入变化立即失败，不重开窗口，不推进、调用或清空官方队列。该局部合同不解除 Force Resolve 的独立刷新门，也不代表实际 Bundle/Player 已构建。固定静态字段读取仍受正式已初始化 Editor 域前提约束，不代表任意冷域首次调用无副作用。

`hotfix.generate-artifacts` 分别冻结 HybridCLR 生成引用表与 Master AOT 保留表的路径，两者可不同且都必须纳入写集和精确产物；同路径去重，不改项目设置。它冻结 IL2CPP/平台前提、实际场景、PlayerSettings、临时构建选项和 Player 回调身份；未知 SDK/包/业务回调缺少确定写集时 Plan 返回具体缺口。GenerateAll 内部 script-only 构建仍会触发回调，不能仅声明 HybridCLR 七个生成路径。执行后的完整上下文与精确产物集合才可 finalized；异常仅返回未完成 Receipt，Verify 不重放生成或按当前设置补造旧计划。详见 [HybridCLR 生成边界](../EditorUtil.HybridCLR/EditorUtil.HybridCLR.md#generateall-的临时-script-only-player) 与 [Bundle Action](../EditorUtil.BundleBuilder/EditorUtil.BundleBuilder.md#受控-bundle-action)。

WebGL Generate 在同一内部 Player partial adapter 中复用准确 callbacks、typed scope、真实 StripAOT BuildReport 和 ProjectAuditor 自然复制；准确输出投影与编译前历史后验证书只用于该 Action。重载 Verify 保留持久源、当前自然分支和精确输出，允许的域瞬态逐字段受约束；不放宽普通 Player 或 Android。其原厂环境转换、深层链接拒绝、ProBuilder 自动保存字节早退与等待中重载 partial 边界统一见上述既有 HybridCLR 文档。

`config.inspect-bundle-collector` 的 `configuredPath/pathConfigured` 保留顶层原始配置含义；`resolvedPath/effectivePathConfigured` 表示 Master 当前坐标经 Override 解析后的路径。摘要按有效路径、资产存在和 Collector 类型加载判断，顶层为空而 Override 有效时可正常报告已加载。定位成功仍不证明 Collector 成员、地址或 Bundle 构建正确。

`network.export` 对 scope 统一去除首尾空白并忽略大小写，再检查 Proto 不能使用 data。版本 2 领域 Receipt 从 Plan 冻结范围独立保存 `expectedOutputs`；Verify 只读核对完整 `(target, kind, 项目内规范路径)` 集合及产物路径/类型绑定，再核 SHA-256 与数量。缺项、重复、路径替换、旧 Receipt 缺冻结范围或未取得执行 Hash 时为 partial，不读取当前设置补造旧计划、不重导、不重放。新收据另含 `sourceContractVersion=1`，按下方共享来源契约验证；含代码时还要求本次编译与重载。历史收据仅保留原产物摘要验收并附 warning，不继承新来源或编译证据。收据是恢复证据，不是防伪签名；导出验收不包含 Play 或服务响应。

- `success`：满足 Descriptor 全部最低证据；
- `partial`：已提交、等待稳定、发生中断或证据不足；
- `blocked`：写前安全拒绝，或无法安全继续；
- `not_applicable`：Action 未注册或当前项目不适用；
- Plan 额外使用 `ready`。

校验 Action 的“执行成功”和“业务对象有效”是两层语义。例如 `config.validate-coordinate` 可以 `success + data.valid=false`，表示校验闭环成功完成但目标配置存在问题。

同样，Collector 查询可成功返回 `collectorLoaded=false`，Readiness 查询成功也可包含 errors，都不表示 Bundle/Player 可构建。旧 `config.validate-coordinate` 的完整性预检按集合中是否存在完整条目判断，不修复首个重复残缺项或任意坏容器，后续 Getter/Validator 仍可能失败。旧 `config.ensure-plugin-instances` 预演会调用配置类型的正规构造；其 Verify 核当前实例和启用情况，不能继承新版 panel 编辑的整状态、meta、写入起点或保存后文件冻结。旧 `config.export-runtime` Verify 核本收据产物与 Runtime 坐标/Namespace，不提供所有当前源输入、YooAsset 内容及草稿的完整来源证明。这些差异不能因共用 Dispatcher 而抹平。

## 扩展要求

新增 Action 前必须证明输入、写入集、稳定领域 API 和自动证据均已成立。Table、Localization、Network、Sound、Vibration 与 UI 已提供设置解析和精确输出规划；UI 以及带新来源契约的 Sound、Vibration、Network、Localization 代码导出验证还绑定本次编译与代码重载证据。环境检查需要先抽取无 `SessionState` 写入的 pure probe；CDN Delivery 需要可信审批、凭据隔离与外部审计。

不得通过新增 Handler 绕过这些前置条件，也不得建立静态 Action Catalog 与 Registry 竞争真相。

### 配置只读发现

`config.validate-coordinate` 保持原请求与校验状态语义，并在结果附 `coordinateState`：当前 Master 坐标、已有 App 槽坐标、Namespace/App 掩码轴、解析 Namespace 与请求物理格五个 App 字段。密钥和 IV 只返回是否已配置；不输出其值、长度或逐字段摘要。App 槽存在不证明其他面板/Runtime 已就绪，缺槽不自动补齐。Execute/Verify 重读当前状态，Plan 不能当运行时配置快照。`config.export-runtime` 请求前置拒绝 `Channel=None`，共享 Generate 解析器语义不变。

### UI 单次导出

`ui.export` 复用既有 UI Exporter。Request 指定 `scope=all|code|data`，可选 `sourcePath` 为精确单文件相对路径；单文件只支持 code/data，全量才支持 all。Plan 冻结已保存场景、UISettings、激活 Master Namespace、Excel/模板/_configs 与精确替换和删除集。Execute 单次导出；Verify 需对应本次编译证据与准确产物/删除状态，不能借旧编译、旧产物或手写生成代码判成功。具体范围以 live Schema 和 [UI Exporter](../EditorUtil.UI/EditorUtil.UI.Exporter.md) 为准。

### 数据导出的来源与编译验收

Sound、Vibration、Network 的 Luban 分支与 Localization text/font 复用共享来源校验。Plan 只读冻结已保存的组件设置、精确 Excel、实际模板和生成工具目录成员、持久 `_configs` 文件与正式写入集；Execute 写前复核，写后只允许本次配置生成范围变化，Verify 重读当前组件、选集、输入范围和配置产物。目录快照同时核对新增、删除和文件 SHA-256，不只核旧文件。

Sound 与 Vibration 的领域导出成功后先保留本次基础 Receipt，再读取产物和配置、复核实际输入范围。写后取证异常、输入漂移或取消均返回 `partial` 并附带已有 Receipt；已捕获的输出路径和 SHA 保留，尚未捕获的证据不补造，也不重新导出。写前冻结状态不成立仍为 `blocked`；写后来源和产物复核通过时才安排本次代码编译，最终成功必须由 `Verify` 判定。

这些分支的命名空间取自 `WorkspaceActive` 已持久绑定的 `ConfigMasterSO.Namespace` 顶层值，同时冻结 Master 与 `ProjectSettings/Nova/Globals.json` 字节。未绑定或 Master 未保存时拒绝计划；已绑定 Master 明确配置的空 Namespace 仍合法。该路径不读取 `ConfigRuntimeSO.Namespace`，也不改用维度 Namespace 解析。Network Proto-only 与 Localization `target=languages, scope=data` 不依赖此持久 Master 来源。

写入集包括真实配置、正式产物、反格式数据删除、相关 `.meta` 及新建目录的 `.meta`。Sound/Vibration 按 Unit 调用时不冒充全量代码批次；Localization text/font 全量代码还核对实际输出清单，text 另核支持语言生成代码。新代码收据绑定单次编译令牌与载荷 Hash；Verify 要求导出后的本次编译周期和重载：实际收到程序集更新完成事件，或 Unity 核对本次输入后报告程序集无需更新；无任何程序集事件的空周期仍不成立。缓存复用不代表 C# 编译器实际重新执行，必要时同令牌最多补一次清缓存编译。随后按每个生成脚本的实际所属程序集核对完整类型名及 Table 契约。一次导出验收完成后再执行下一项，避免后续导出替换前一份编译证据。

新字段显式为 null、版本损坏或范围不完整时不能降级为历史收据。完全不含新字段的历史收据保留原摘要验收并附 warning，不能用于证明新来源契约。本次快照覆盖已建模的直接输入与实际工具目录；未建模的外部二次 include、dotnet 宿主与运行时不据此宣称完整依赖闭包。

共享路径、Hash、DTO 和 codec 助手只提供各自格式或有界完整性检查，不独立授予目录写入、反射调用、回调执行或重载后续接权限。Generate 的完整 before/final、原计划输入投影、真实阶段后态、首次编译/重载见证必须分别成立；自洽历史 Hash 不能代替当前实际来源。输入投影仅精确拥有的路径，祖先和 meta 保护仍覆盖非目标兄弟项；输出失败后保留已观察的路径/SHA，不填充未观察材料，也不把旧 partial 升级成较晚成功。

`table.export` 保持独立原生 Luban 配置，不强加 Master。版本 2 收据核对当前场景、组件、完整选中设置和 Job，以及自身 `luban.conf`、已解析的配置/数据/Schema 读根、模板和工具快照；只写正式 code/data 输出，不生成共享 `_configs` 或输出所有权清单。当前 Project 模型未解析的 Schema 二次外部 include 仍是验证边界。具体配置和读取范围见 [Table Exporter](../EditorUtil.Table/EditorUtil.Table.Exporter.md)。

`BuildCallbackSnapshot.knownEffects` 记录当前已审 SBP 内存统计与原统计外发条件和字段，AssetBundle / HybridCLR Generate Action 以 `ExternalWrite` 表达潜在原回调外发，继续使用原绑定确认门。当前 Adaptive Performance 非空 provider 集合的 getter 未核仍阻断，构建产物不证明外部收件；不通过临时关闭 Analytics 改变正常开发行为。


当前真实 WebGL Generate 使用与 Player 相同的准确回调捕获、typed 执行后验与真实 BuildReport，输出只接受已审默认 `HybridCLRGenerate/AOTGenericReferences.cs` 和 `HybridCLRGenerate/link.xml`，Master AOT XML 仍按独立冻结路径处理。父目录与其 meta 必须既存；拥有目录内全部现存后代均拒绝链接，包括悬空链接。Android 未闭合的回调合同继续阻断，不能因 WebGL 分支通过而放行。

完整写前/写后 typed 材料分别保存在 `Library/Nova/AgentActions/HybridCLRGenerate/<本次 N-GUID>/before.json` 和 `final.json`，每份严格 UTF-8 JSON 原文限 8MiB。Plan 只展示完整计划并保存有限未写指针，不创建证据目录；Execute 要求 GUID 目录不存在，先原子写 before，再恰一次生成，正式收尾及全部后验通过后保存 final。小 Receipt 绑定固定文件的 SHA-256 与长度，完整 Operation 预算限 192KiB，预留 Store 后续状态空间；不截断快照或扩大 Store。两个文件均列入准确 WriteSet 与正式 Artifacts，须保留供 Live Verify 读取；`report-backup` 子目录与正式证据分离，Auditor Commit 不删除这两个文件。

编译 payload 绑定上述准确文件摘要和编译前证书。重载后先核原编译令牌、载荷及 requireReload，再读原文件重核历史自洽及当前持久源/配置/输出；不把旧 managed 对象当作当前对象。官方 Refresh 正常执行，当前WebGL执行由本次公开租约保护至Dispatcher持久化尝试后；旧未 finalized 的早重载 Operation 仍保持 partial，已有 before/产物保留，不重放。WebGL 不在跨 await 的外层 finally 写回用户设置，恢复证据来自原同步 StripAOT finally 和真实 typed 后态；尚未尝试final时，失败/取消/收尾错误清除完成标记和证书并只首存一次；final已尝试后的编译前历史不改写或重试，外层失败Receipt保持未finalized。外部 telemetry 仅按原回调自然条件发生，产物或本地证书不证明外部送达。

Plan 的 `webglGeneratePlan` 保持完整可审阅，运输表示使用专用 `nova-hybridclr-generate-plan-gzip-v1` 封装：字段恰为 `codec`、`rawLength`、`rawSha256`、`gzipBase64`。固定本地 serializer 保留所有 null 和默认字段，严格 UTF-8 原文最多 8MiB，单 member gzip 最多 1MiB；规范 Base64 无空白，原文长度与 SHA-256 均绑定。`TryDecodePlanPreview` 只输出审阅 JSON，核严格 JSON/已知 DTO 字段、CRC/ISIZE、精确 DEFLATE 终点，拒绝截断、尾随/串联、超额解压、未知/重复字段、类型元数据和嵌套 codec。解码成功不代表业务 Plan 有效、没有漂移或获准执行。

Plan DataJson 同时保留原小 Receipt 与完整压缩预览，最多 1536KiB，为现有 2MiB 正式 Plan 响应保留外层空间；完整 Operation 仍限 192KiB（Store 256KiB 不变）。预览编码只操作本次内存 DTO，不写 Plan 文件、不截断数组、不更改原 State/Runner 绑定，也不参与 Execute、恢复或正式证书。最终 before/final 文件及其编译载荷绑定继续使用原完整证据协议，超预算明确 blocked。

当前真实 WebGL Generate 的 Execute 在输入重新核对后、写 before/生成前取得本状态唯一的公开 `EditorApplication.LockReloadAssemblies` 租约。正常 Refresh、编译及官方报告自然等待继续；不等待域重载自身，不新增 isCompiling/isUpdating 的 idle 门。完整 typed 后验、final 原文、原编译 Schedule 与 Receipt 返回后，由既有 Dispatcher 在 `TryCompleteExecution` 持久化尝试后的 State.Dispose 主线程配对一次 Unlock。持久化返回 false 时仍释放且保持 partial/warning，不宣称持久化成功。Plan、未执行/过期清理不获取原生锁；普通 Player、Android、Pipify 不变。

30分钟是 linked cancellation token 的协作期限，计时线程只取消，不释放锁；同步原生 Build 不能强杀，只能返回后再观察。取消、异常或超时保存可用实际材料后同样经 Dispatcher 清理自己的锁，绝不循环清除他人计数。仅尚未尝试 final 保存时可降级并首次保存失败材料；已保存的 final 是不可变编译前历史，之后取消/调度失败不改写或重试，partial Receipt 的 finalized 保持 false。最终正常编译与 requireReload 仍由原令牌/完整载荷协议核验；旧早重载操作保持 partial，不重放。不扩大AutoSync输出拥有域或复制授权；后续真实漂移照旧阻断，正式Copy按独立合同消费。

TMP 后态观察不重跑 Prepare：Plan/写前准备仍保留原安全 Load；Scope 后与 Verify 仅观察原许可 marker 的现存对象并重查全部路径、主类型、文件/meta/依赖。缺席必须有原 clean、原/后无强行或活 weak、真实 Scope after 已缺席的独立证明。历史输入行与当前 absent 分开记录；原 dirty 输入、未知项、完整 JSON 与新实例拒绝门不变。Generate 的较晚 current 不替代真实 Scope after；跨域没有独立实证合同仍 partial。


### Android Nova 父链内部合同

本节八型入口保留原兼容合同。Android Generate 另有按实际安装程序集捕获的生产入口；当前安装集为七型，不伪造未安装 AdMob，也不按 EnabledSDKs 删减真实工厂。两种入口均不代表普通 Android Player Action 已开放。

`NovaAndroidParentBuildContract` 是尚未接入公开 Player 准入的内部可组合父合同。它完整覆盖包含 AdMob 的八个 Android Nova SDK processor 的实际 TypeCache 发现顺序、默认构造元数据与 Priority 常量，并用当前 BCL 的两轮 `List.Sort` 重现 pre/post 的真实 tie 次序；不执行 SDK 构造、虚 getter 或回调。独立 Android DLL/MVID pin 必须经真实编译审查，缺失时明确拒绝，不能用当前 WebGL 身份自签。未来执行仍须原入口普通构造同一批实例、同一 `NovaBuildContext`/report，严格观察 constructed → pre → post；本合同不替代各 SDK 叶子的独立配置、写集和副作用证明。

父输出预测直接复用原 `CustomAndroidManifest`、Manifest 注入/规则和 Proguard 文本算法，冻结模板及准确 Manifest/Proguard 原文件、meta、属性和完整 SHA。它只支持三文件与 meta 已存在且完整祖先无链接的分支；写前必须保持完整原态，写后必须是准确预测的两个文件且保留原 meta/属性/模板，不宽放目录、导入尾部、Gradle、EDM 或其他 SDK 输出。预测仅在私有内存中产生；观察/释放不写源、不修复字节、不清队列。`NovaProguardBootstrapper.Rebuild(path, null)` 原来只生成头部，MAX 的 AndroidX 规则由 SDK 正常登记，不能为掩盖旧测试错期望而隐式补规则。

当前永久纯测试及实际 Unity DLL 离线编译只证明这批内部合同和原领域算法兼容，未证明 Android Player/Generate 已 ready 或原生执行成功。公开目标、原 callback 门和外部确认保持现状。AppLovin credential POST 与未知 `ios_setup2` Ruby 仍需用户手动；不能关闭 Quality、预填完成标记或把本地未接线工程缺口归为外部环境。


父内部三个通知点现位于原普通调用链，默认无observer；尚未接入公开Android Action的活动scope：`constructed` 是原 `CollectProcessors` 普通构造列表返回后、任何 SDK pre 之前；`pre` 与父两文件 `Prepared.VerifyAfter` 位于原规则 merge、`Manifest.Save`、`Proguard.Rebuild` 完成后、`OnAfterNovaPreprocessBuildOnAndroid` 叶子之前；`post` 位于原普通 `OnPostprocessBuildOnAndroid` 列表返回后、`OnAfterNovaPostprocessBuildOnAndroid` 叶子之前。AfterNova 叶子的物理 XML/其它输出转换必须由独立叶子合同合并最终后态，不能把整个 `NovaBuildPreprocessor` 外层返回后的状态补写成父阶段历史。


`NovaBuildParentObservers` 只在 Android 原普通构造列表返回后、父 merge/Manifest.Save/Proguard.Rebuild 后、原普通 post 列表后同步发出信号，各自仍位于相应 AfterNova 叶子之前。默认无observer时仅返回，不注册 Editor 事件、不改变排序/构造/SDK调用，不包裹或重放原回调。Core只依赖中立接口，Action层可选择 `NovaAndroidParentBuildObserverScope` 观察同一 Shared Context、report、列表与同批实例。scope Begin 在真实主线程重新核原工厂与父写前材料；构造点再核完整写前，合并点验完整Context和准确两输出；post只记录同批真实阶段。错误/取消同步传播并保留已取得帧，finally只解自身租约、释放借用引用；不Dispose调用者的Prepared，不回写静态状态、文件或SDK。

三个信号不是SDK执行许可。当前没有公开Player/Generate/Batch Handler调用scope；完整SDK配置来源、实际源写/导入尾部、AfterNova物理转换、外部手动边界与最终文件/BuildReport仍需独立合同。`parentPhasesObserved`只说明三个真实父阶段已按序观察，不能升级为整个SDK或Player成功。scope是同域同步观察，不跨重载恢复；外层异常不允许用较晚current补造阶段。当前额外严格要求pre/post传入report保持同一managed引用，其真实Android原生前后时序尚待验证，不能凭纯测试断言该现场已满足；若正常Unity包装身份不同，必须另核准确Native身份，不默认放宽。

Scope登记前还必须由Prepared.RequireProjectRoot精确绑定BuildActionCommon.ProjectRoot：完整路径规范化后严格比较私有冻结根。隔离tmp可作纯预测测试，但不能拿其文件后验作为实际工程的父验证；同字节源副本也拒绝。此门不重新选择根或回写预测，原文件链接/字节/身份检查继续独立执行。

Android SDK叶子验证按真实Nova八型完整工厂和本次原Runtime配置展开，不按EnabledSDKs删减工厂成员。内部`NovaAndroidSdkLeafBuildContract`只提供配置来源/引用/完整JSON摘要、原纯规则复用及准确未接效果查询；它不构造或提前执行SDK回调，不包含Complete许可，也不接收可伪造的Parent frame。真实Context/list/report/同SDK实例和constructed/pre/post历史只由原`NovaAndroidParentBuildContract`及唯一内部observer scope负责，叶子不能用外层较晚状态补造。Framework不新增SDK程序集强依赖：两个SDK内部纯builder只通过已核factory类型的固定metadata签名复用；无cctor、无任意方法/ctor/业务getter，仍需真实Android独立DLL/Module/MVID pins。

Google/FB/MAX规则从原const读取，Firebase与AppsFlyer的纯树由原回调调用同一builder，不复制Manifest转换算法。配置冻结仅未来实际Android Plan可调用：原active Runtime及Assets主资产path/GUID/localId/Native ID/dirty、源/meta与完整Native JSON摘要，四配置类型独立ManifestModule/MVID/DLL，以及原选择位置/引用都须保持；不输出Key/Token/配置正文。provider读取与序列化自然效果仍须真实观察，不称Native零变化。配置存在不等于对应SDK必定写入，也不等于SDK ready。

本批的固有缺口不会因调用者把八个可变模型自报complete或清空required而消失：Native启用场景的PreviewScene生命周期/AppInstaller、AF无条件properties重写及正规ImportAsset，以及实际provider捕获仍待接。非空AdMob广告配置需核自然唯一enabled选择、固定官方Settings资产/可能目录/meta创建与定向保存；MAX必须按原首个Max渠道（不按Enabled过滤）与原Instance真实选择/可能创建路径冻结，不能泛放Assets/MaxSdk；Facebook非空配置涉及原全局dirty保存集、Refresh、同实例m_AndroidSettingsApplied及官方AfterNova Manifest完整写集。自然no-op也需真实分支证明，不假设缺配置、不单独提取/输出/传输Key或Token、不预热单例或改开关。AppLovin携sdk_key的credential POST、iOS ios_setup2 POST及其未知下载Ruby执行三步继续用户手动，设备/业务环境/UPM/Raw按原延期。

叶子候选的纯规则、缺口与组合测试不能代替Android活动域TypeCache/SDK Native回调、正式Generate/Player或产物验收；公开Android门在全部callee合同闭合前继续阻断。

配置捕获及binding复核在读取原provider或调用任何纯builder前，均直接复用`Parent.VerifyFactoryBefore`重核真实Android同域TypeCache、完整处理器Module/DLL、源码及BCL；不能靠可变Factory DTO或同名类型自签。这沿用原休眠桥要求，活动observer租约期间拒绝另取工厂；在租约之外复核，不把外层后态补历史。永久规则fixture只读当前已加载的真实SDK类型，缺当前平台模块时明确Ignore/not_applicable，不引入任何NovaSDK派生测试型；独占tmp单源SDK片段只作离线纯回归，不能充作真实Android身份。

### Android Generate 的独立接线与未完成边界

`PlayerBuildAction.AndroidGenerateAdapter` 已接入既有 `hotfix.generate-artifacts` 的 Android 分支；请求字段和 Action ID 保持。它分别捕获实际 provider、完整回调集、SDK/NDK/JDK/Gradle 工具身份、Native preview 和 AppsFlyer 输入。`AndroidHybridClrGenerateEvidenceStore` 保存同次计划及实际报告、输出、阶段与失败材料；Store 的读写测试只证明存储合同，不证明 Generate 成功。`unboundContracts` 非空仍写前 blocked，不能修改 DTO 或借 WebGL 证据放行。

Core 的中立观察桥默认休眠。原 SDK pre 循环的 before/after、Native 场景 preview 和属性导入观察均围绕原调用执行，不额外调用 SDK。AF scope 在父 observer 租约之前完成完整工厂复核；异常/取消保留已取得的阶段，finally 仅释放自己的租约。AF-before 的 Manifest 从实际阶段取得，不使用父 merge 后的较晚文件补造；Import 记录保留 imported/deleted/moved/movedFrom 全部四数组。固定源码与 Module 身份仍独立检查。

当前未闭合项包括 `AppsFlyer.generated-assets-and-parent-manifest.import-tail` 及尚无完整合同的第三方回调、Search/Version 和后续 Manifest、代码导入尾链；Native preview、provider、工具链的局部接线不解除这些门。普通 Android Player、真正 Generate、最终 APK 和设备业务分别验收，纯规则或 Store 回归不提升这些结果。已审、已保存且副作用获授权的 Pipify 组合仍是已有独立入口，按原 Step、完整输入和实际产物验证；它不会把此 Action 的 blocked 改为 success。
