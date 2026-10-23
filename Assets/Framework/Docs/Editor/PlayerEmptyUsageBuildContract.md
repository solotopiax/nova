# SRP / Adaptive 当前空分支 usage 合同

仅支持真实 WebGL 活动域，保留 Analytics 原值。SRP 的精确 Editor/Runtime Android 与 WebGL 成对 MVID/SHA、完整 package 源码、ObjectPools 源码和实际 Engine/Core/AnalyticsCommon 身份全部保留；ObjectPools 四个完整类型两变体字段/IL逐字相同。仅增加两个已审具体类型，不做 namespace 白名单。

SRP 在 enabled 时要求当前/默认 pipeline 为 null，再通过已审只读 native getter核对 current global settings 实际为 null。GraphicsSettings.ForEach 因此在设置枚举前返回，不调用任何设置callback、Activator 或 ToNestedColumn。官方 Gather 仍可借还准确 ListPool<AnalyticsData>；Plan只读私有 backing，可自然执行已审纯内存 cctor（空Stack/ObjectPool、准确List.Clear委托/编译器owner），不调用Get/Release/Gather，不清原payload。原stack只允许准确空List。现存列表身份/容量和全部原容器保持；native注册失败可完全不Gather，首次空池只允许无变化或新增一个capacity0空列表。已有活跃借用计数保持，不归还他人的列表。正常List/Stack内部版本变化不回滚。实际 after/current 完整材料必须吻合。

Adaptive 在 enabled 时要求准确 s_Packages Dictionary为空、m_AddRequests为空且s_SearchRequest为null，阻止所有provider metadata getter、安装查询和业务设置加载；非空继续 blocked，绝不清表。只读backing可能自然执行已审 metadata cctor：初始化空容器/标量、订阅一次既有 PlayModeStateChanged方法并只读当前Play状态，不启动包查询或队列。该既有方法只在后来用户进入Play时StopAllQueues与StoreSessionState；本次Build不调用它，也未获得任意未来任务或写权限。Plan明确声明这项正常初始化效果，既有cache仅记录SHA。

原GetLoaders空结果仍创建/保留纯比较delegate cache（0项Sort不会调用它）；正式场景callback可以创建私有usage DTO并按原配置调用SendAnalytic。WebGL版本自然空串、providers自然null。真实构建可能重用已缓存场景，因此不猜测未观察的场景callback次数，也不把comparison初始化当实际发送证据。只允许原null或准确已知comparison的正常缓存效果，未知owner/方法继续拒绝。

共用SendAnalytic下游的analyticSent必须真实为空；不移除订阅。允许官方现配置native注册/队列/诊断效果及固定失败警告，SRP空DataList不产生payload发送；不声明native返回成功、远端接收或任何队列/Task退出，不重发事件。回执仅绑定官方报告GUID、真实WebGL平台和明确空payload范围。schema4缺emptyUsageBefore/After/Report/Error时保持partial。纯DTO测试不调用任何真实callback、GetLoaders、ForEach、Gather、池Get、初始化getter或发送入口；正式Plan/执行/Verify由主线程完成。

五项辅助合同 `PlayerFiniteAdditionalBuildContract` 另按准确已安装 DLL 身份接纳两个 Adaptive 构建处理器。当前自然无活动 Build Profile、两个已有配置槽均为 managed null，原预加载表不能含 null、销毁对象或准确 Basic/General 设置型；因此 Basic 预处理虽先运行 CleanOldSettings、两个后处理虽仍执行 Clean，但都不触发 SetPreloadedAssets，General 预处理在取得空设置后返回。不调用设置初始化 getter、provider 或预清表；正常固定 key/路径数组的纯内存初始化明确列入计划。真实配置非空继续给具体 blocked，不能把 usage 的空 metadata 门当构建处理器合同。

同一辅助合同中的 `RenderPipelineGlobalSettingsStripper` 只接受完整活动 WebGL 管线 roster 全 managed null，当前、默认及实际 global settings 都为空。官方预处理仍将两私有 dirty 布尔归零，并可从准确 `UnityEngine.Pool.CollectionPool<List<RenderPipelineAsset>, RenderPipelineAsset>` 借还一次原空列表；计划只读准确 backing，可自然运行已审纯池构造，绝不调用 Get/Release 或清原 payload。后验保持原 pool/list、其他空列表身份/容量、活跃借用数，只接受原态或原 fresh/末项的准确借还，冷池最多新增一空列表及 CountAll 增量一；被借列表允许正常容量增长。两布尔完成后须为 false，global 仍为空，因此不允许资产 dirty、ClearDirty、global setter 或 Save。未知池委托、派生列表、payload、引用或后验变化保持 blocked/partial，不恢复池或标量。

`PlayerPackageCallbacksContract` 独立接入另外十四项准确包回调。仅对 EDM 场景、ProBuilder、IAP 和三个 Shader 处理器复用 `BuildActionCommon.TryCapturePlayerSharedPackageCallback` 的六个准确声明/属性方法门；该桥不是 SBP flags 对 Player 的整体授权。EDM 要求活动 WebGL、原 resolver 为 null 且计数自然为零；其正规 post reset 仍按独立合同复核。ProBuilder 要求原 Settings 已初始化、原 repositories 身份保持且保存事件为空，完整 Player 输入的递归依赖脚本与全局 loaded 对象均无 ProBuilderMesh/EntityBehaviour（含派生类），因此只读 InvisibleFace Material 加载和 Pref 的固定内存构造不进入场景/Mesh 改写。IAP 依原 BillingMode 规则选择插件，只接受原 Android 兼容性已经等于目标的正规 setter；配置缓存可保持原值或替换为准确文件推导值，原文件、meta 和其它输入仍完整保护，不手动设置或保存 importer。

Timeline 三回调要求完整 Player 依赖脚本和 loaded 对象都无 TimelineAsset（含派生类），准确统计类型、默认 comparer 的七项 track 零字典、空 user 字典/uniqueDirectors、两个零计数和四个 Min/Max 哨兵都一致。官方 pre 正常替换统计对象；scene 不进入 Timeline 轨道/用户 getter，post 因空 uniqueDirectors 不发送 Timeline payload。Action 不打开场景、不调用回调或清统计；存在真实 Timeline 内容继续 blocked。

五个 Shader 回调分别固定 ShaderGraph/Core Editor/Core Runtime 编译与包源码，沿既有内部 stripper/事件空表、完整 Quality/当前/默认 SRP 为空门。ShaderGraph 实际编译没有 PROFILE_BUILD，按当前 shadows 推导官方唯一 features 值；写前保留原 List 和原值，正常 pre 可重建为一个准确值，after 必须实际出现。ShaderStripping 的 actual global 为 null、s_DefaultExport/warning 为 false 且原 reporter 为 null，故正规 ReportBegin 只建 Empty，ReportEnd 恢复 null；不允许导出 Temp/shader-stripping.json、compute-shader-stripping.json 或 profiler 写入，也不主动归零/移除旧报告。

ProjectAuditor 引擎 DLL 精确冻结；原 AnalyzeAfterBuild 的准确 BoolPref 和 EditorPrefs 都为 false，规则包 backing 与实际已注册包列表均未安装且无旧 DelayedPostBuildAudit。已审规则包 cctor 仅 FindAssets 与 GetAllRegisteredPackages 的只读信息，不调用 Client.List/Search；构造因未安装直接返回，不实例化自定义 module，post 不排 Audit。ShadersModule 两个实际 callback 则仅读 shader/compute metadata，向准确 BCL Dictionary<Shader/ComputeShader,List<固定DTO>> 追加本次 WebGL 编译统计；准确默认 comparer、完整原字典/key/list 身份、旧记录的顺序 SHA/target 前缀保持。允许零增长缓存命中，不调用会删除 PlayerDataCache 的 ClearBuildData，不调用 Analyze 或清字典。未知类型、旧前缀改写、删除/替换或非 WebGL 新记录全部拒绝。

这些分支以独立同域 `packageCallbacksBefore/After/Report/Error` 保护写前完整值与正常后态，Verify 仅只读复核。UGUI 的正式 Player payload 与报告绑定另见 [BuildEvents](PlayerBuildEventsContract.md)；所有 native usage 都只声明原配置下的本地尝试范围，不以场景静态缺失、文件存在或末态相等补造回调执行证据。
