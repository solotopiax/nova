# AssetDownloadUrlPolicy

`AssetDownloadUrlPolicy` 是 AssetManager 为每个 YooAsset 包独立持有的候选 URL 与失败重试策略，同时实现 `IDownloadUrlPolicy` 和 `IDownloadRetryPolicy`。

候选去重、最近成功优先排序、不可变计划、执行游标和偏好存储均复用 Core 的 `HttpFallbackPlanner`、`HttpFallbackExecutionPlan`、`HttpFallbackExecutionCursor` 与 `HttpFallbackPreferenceStore`；本类只适配 YooAsset 的 URL/失败回调和 Asset 专属错误码规则。

- 每个文件请求创建独立计划与游标；回调关联仍受下文同 URL FIFO 的边界约束。
- 候选先去重，再按 `C × R × (K + 1)` 计算最大物理尝试数；传给 YooAsset 的额外物理重试数为该值减一。
- `FallbackRoundCount` 的一轮会完整尝试全部候选；`RetryDownloadCount` 是下载重试次数，每次重试重新执行完整轮次组合。
- 404/408/416/429、5xx、无响应可继续；401/403 和其他 4xx 立即停止。
- 传输成功后的内容校验失败会继续使用该文件已冻结计划中的下一候选。
- 成功会更新同类请求的最近成功域名，新文件可从该域名开始；完整失败不会清除此偏好，候选配置变化且旧域名已不存在时才清除。
- URL 查询串和 fragment 不参与文件身份匹配，因此带时间戳的 `.version` 回调仍可正确收口。

白名单命中时，候选顺序由 `AssetRemoteService` 提供：白名单元数据主、白名单元数据备用、常规主、常规备用。全部逻辑组合失败后才进入 AssetManager 回退。WebGL 首包元数据回退也复用同一计划、错误分类和埋点，但候选临时收口为首包同源地址；Bundle 地址不受影响。同步加载行为不变，WebGL 仍受 YooAsset 异步加载边界约束。

Bundle 超时按平台互斥：WebGL 使用 `WebGLBundleRequestTimeout` 作为单次物理请求超时，其他平台使用 `IdleTimeout` 监测连续无字节流入；两者都不表示整条候选链的共享时限。

## 状态关联与调用约束

计划以首候选的路径作为 fileKey，独立序号形成 requestKey，并用规范化候选列表签名区分计划。文件路径保留大小写；host、query、fragment 不参与 fileKey，回调队列则按去掉 query/fragment 的完整 URL 关联。同一规范 URL 的选择按 FIFO 消费，状态字典和队列没有锁；这不提供同 URL 并发回调乱序时的精确身份保证。`OnRequestFailed` 与紧随的 `IsRetryableError` 必须成对使用；上一失败尚未消费时，同 URL 后续失败会被忽略。

`BeginMetadataRequest` 只清本次元数据选择与传输失败列表，不取消请求或清空全部文件状态。`CompleteMetadataRequest` 若有传输失败，分别按错误码裁决；若操作失败且没有传输失败，只将最后一个活动选择视为内容失败，其余视为成功。取消或异常在 Complete 前发生时，没有额外 finally 自动收口；上层按包 metadata gate 串行组织调用。

WebGL Player 的普通 Bundle HTTP 成功先更新成功域名与遥测，再保留可推进的游标供后续内容校验失败使用，不能据此认为内容已通过认证。元数据由最终 operation 结果确认；其他平台普通资源成功时收口状态。文件状态移除会过滤相应 pending/retry 队列；文件注销还可能删除包含该文件的整个 URL pending 队列，不能把注销视为其他同 URL 在飞请求完全独立。

显式下载器按文件名登记重试覆盖：同名文件增加引用计数，重试次数保留已登记的最大值，非空 operation ID 以后一次覆盖；部分注销不会重新计算剩余登记的最大值或 ID，归零才移除覆盖及残余状态。所有这些操作都不直接发网。

错误分类先排除 `file://`（不重试），再允许 0、404、408、416、429；其他 4xx 停止，其余数值允许继续，实际还受计划是否耗尽约束。`httpError` 仅用于记录。重试等待是 `min(10, previousDelay + 1)`，不使用 retryCount；它不额外校验 NaN 或负延迟。

构造将轮数至少归为 1、逻辑重试至少归为 0；超时字段只保存，不在策略构造时校验或设置物理请求。`.version/.hash/.bytes` 在本策略中按扩展名、忽略大小写分类；这比 `AssetRemoteService` 的精确元数据文件名路由更宽。请求超时与文件类型埋点均按此分类取值；调试日志保留原 URL 和错误文本。物理额外重试数超出 int 时饱和为 `int.MaxValue`，不表示会即时执行该数量。
