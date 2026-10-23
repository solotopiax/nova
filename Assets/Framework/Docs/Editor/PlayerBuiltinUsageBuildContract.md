# Player 三项 Unity 内置 usage 合同

限定当前精确 CoreModule 的 `AudioRandomContainerBuildAnalyticsEvent`、`UIBuildAnalyticsEvent`、`InsightsEditorAnalytic`，并保留 Audio/UIElements/AnalyticsCommon/Core 的完整程序集指纹。原 `EditorAnalytics.enabled`、音频开关、EngineDiagnostics、云项目和命令行不修改；后两者只登记 SHA。`BuildEventsHandlerPostProcess` 的平台权限 provider、SRP/Adaptive 等其他回调不因此获得放行。

Audio/UI 只根据本轮真实报告的 packed groups 决定是否进入官方发送链。Audio 逐条统计准确 AudioRandomContainer；UI 仅读 GUID 的主类型，只有 VisualTreeAsset 按 GUID 去重，其余三种 UI 类型逐条计数。回执复制报告 GUID、session GUID、build type、分组数、记录摘要和计数；这只是按准确源码推导的 payload/尝试条件。`PackedAssets.GetContents` 的受保护 API 通过已审只读 native getter 读取，绝不反射调用发送函数、私有 callback 或 setter。

Insights 官方代码首次把命令行中精确 `-createproject` 的布尔结果缓存，并保留纯编译器 predicate；已有检查结果按原值保持。Plan 仅读 backing，允许读取所需准确 `<>c` 的纯空对象构造以及 DebuggerEventListHandler 默认内存对象/list/lock 初始化；不预调用 predicate。执行前完整值再次相等，合同 hash 只投影这些明确正常变化，Dispose/Verify 必须读到实际准确后验，不手动补缓存。未知 predicate 或任何 `analyticSent` 下游订阅均保留原状态并阻断。

官方发送链可以依当前配置完成原生 usage 注册、限流/排队、诊断列表追加及固定注册失败警告。这些是正常构建的引擎效果，没有追加任意 Library 写入权限。注册/发送返回值被原回调丢弃，回执不声明原生发送成功、远端送达、所有 telemetry 队列退出或任意后台任务结束；本合同只验证真实链返回、本地缓存与报告输入。失败或漂移保留 partial 材料，不重放事件、不清诊断列表、不关闭 Analytics。

schema 4 扩展增加 `builtinUsageBefore/After/Report/Error`；缺失这些必需材料的旧回执仍为 partial。测试只用独立 DTO 验证官方去重差异、自然缓存前后投影和准确配置/报告失败条件，不发送事件或改真实缓存；最终 Unity 编译、正式 Plan、真实构建和 Verify 由持有当前域的主执行线程完成。

独立的 `PlayerFiniteAdditionalBuildContract` 另覆盖准确 SRP `BuildTargetAnalytic` 与 InputSystem `InputBuildAnalytic.ReportProcessor`，不扩大上述三个 Core 回调的合同。前者冻结真实活动 WebGL、内置管线和 native 质量级数量，正式 `uBuildTargetAnalytic` payload 只包含平台名、激活/总质量级数量与固定 Built-In Render Pipeline 名；沿原 `unity.srp` 配置执行 10 次/小时、1000 元素上限的 native usage 注册、排队与诊断，不宣称送达。

Input 分支只接受正常已初始化 InputSystem 与准确既有 NativeInputRuntime；真实 manager/runtime 为 null 的原入口才自然早退。非空分支冻结现存准确设置、完整 feature flags、同域对象身份、actions 资产路径摘要及现存准确 InputSettings 对象集，拒绝自定义 runtime、派生设置或未知 comparer。官方 Gather 可创建一个准确临时默认 InputSettings（纯默认字段及 iOS 数据对象），用原设置只读属性与默认值比较，再于 finally DestroyImmediate；不预先构造默认设置或发送。`input_build_completed` payload 是这些准确设置/feature 布尔、是否设置/actions 资产与真实报告 GUID，沿原配置执行 100 次/小时、100 元素上限的 native usage；没有账号/目标变化，回执不声称 native 返回成功、送达或队列/Task 退出。后验必须原设置、flags、对象集和当前状态完整保持。

schema 4 增加独立 `finiteAdditionalBefore/After/Report/Error`，执行写前完整值复核，只有正式链返回后才复制真实报告范围；Verify 对原材料与当前值核对。缺新增材料的旧回执保持 partial，不补发或按当前设置补证。定向纯 DTO fixture 只验证分支和后验；不能替代本轮真实 Player Plan、构建产物和 Verify。
